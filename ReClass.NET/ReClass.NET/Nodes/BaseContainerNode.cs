using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;

namespace ReClassNET.Nodes
{
	public abstract class BaseContainerNode : BaseNode
	{
		/// <summary>Source of the layout pass ids, shared by all threads so an id is never handed out twice.</summary>
		private static int lastLayoutPass;

		/// <summary>The id of the layout pass running on the current thread or 0 if no pass is active (see <see cref="UpdateLayout"/>).</summary>
		[ThreadStatic]
		private static int currentLayoutPass;

		private readonly List<BaseNode> nodes = new List<BaseNode>();

		private int updateCount;

		private bool isLayoutInProgress;

		/// <summary>The layout pass which settled this container the last time (see <see cref="UpdateLayout"/>).</summary>
		private int settledLayoutPass;

		/// <summary>True once <see cref="IsLayoutComplete"/> was true at least once.</summary>
		private bool hasBeenComplete;

		/// <summary>The child nodes of the container.</summary>
		public IReadOnlyList<BaseNode> Nodes => nodes;

		/// <summary>
		/// True if the container was laid out at least once outside of a <see cref="BeginUpdate"/> / <see cref="EndUpdate"/> batch
		/// and the size of every child was known at that time. A container which needs nodes (see <see cref="RequiresNodesForCompleteLayout"/>)
		/// is complete once it was laid out with nodes, so a class which gets emptied keeps the known size 0 while a class which
		/// is still loading has an unknown size. The size of a node which is determined at runtime (see <see cref="BaseNode.ParticipatesInSizeCompensation"/>)
		/// is never known. Containers which wrap this container (class instances, arrays of class instances, unions) only record a
		/// <see cref="BaseNode.LayoutSize"/> for it if the layout is complete, so a half loaded class never triggers a size compensation.
		/// The value is not touched by layouts performed inside a batch, it gets decided by the layout <see cref="EndUpdate"/> performs.
		/// </summary>
		public bool IsLayoutComplete { get; private set; }

		/// <summary>
		/// If true and the size of replaced nodes differs, the gap will be padded with default nodes (see <see cref="CreateDefaultNodeForSize"/>).
		/// If <see cref="ContainerLayoutPolicy.PreserveSuccessorOffsets"/> is set a growing node consumes the bytes of its successors too.
		/// </summary>
		protected abstract bool ShouldCompensateSizeChanges { get; }

		/// <summary>
		/// True if this container keeps the offsets of the successors of a resized child stable (see <see cref="ContainerLayoutPolicy"/>).
		/// </summary>
		public bool PreservesSuccessorOffsets => ShouldCompensateSizeChanges && ContainerLayoutPolicy.PreserveSuccessorOffsets;

		/// <summary>
		/// If true an empty container doesn't count as laid out (see <see cref="IsLayoutComplete"/>).
		/// Classes are created empty and get filled afterwards (project loading), so the size of a class instance
		/// isn't known until the class contains nodes. Containers with a fixed size or which are filled before they get
		/// inserted into a class (unions, vtables) are complete even if they are empty.
		/// </summary>
		protected virtual bool RequiresNodesForCompleteLayout => false;

		/// <summary>
		/// Should be called before adding a child to test if the container can handle the node type.
		/// </summary>
		/// <param name="node">The new child node.</param>
		/// <returns>True if the container can handle the child node or false otherwise.</returns>
		public abstract bool CanHandleChildNode(BaseNode node);

		private void CheckCanHandleChildNode(BaseNode node)
		{
			if (!CanHandleChildNode(node))
			{
				throw new ArgumentException();
			}
		}

		public override void ClearSelection()
		{
			base.ClearSelection();

			foreach (var node in Nodes)
			{
				node.ClearSelection();
			}
		}

		/// <summary>
		/// Calculates the offset of every child node.
		/// If <see cref="PreservesSuccessorOffsets"/> is true, children which changed their size since the last layout are
		/// compensated first (see <see cref="ContainerLayoutPolicy"/>): a grown child consumes its successors, a shrunk child gets padded.
		/// </summary>
		public virtual void UpdateOffsets()
		{
			UpdateLayout(true);
		}

		/// <summary>
		/// Lays out all containers in one pass. A container which gets settled as the nested container of another one
		/// (a class referenced by many classes) is laid out only once.
		/// </summary>
		/// <param name="containers">The containers to lay out.</param>
		public static void UpdateOffsets(IEnumerable<BaseContainerNode> containers)
		{
			Contract.Requires(containers != null);

			var isOutermost = BeginLayoutPass();
			try
			{
				foreach (var container in containers)
				{
					if (container.settledLayoutPass != currentLayoutPass)
					{
						container.UpdateOffsets();
					}
				}
			}
			finally
			{
				EndLayoutPass(isOutermost);
			}
		}

		/// <summary>
		/// Shared implementation of <see cref="UpdateOffsets"/>.
		/// </summary>
		/// <param name="sequentialOffsets">True to place the children one after another, false to place all children at offset 0 (union).</param>
		protected void UpdateLayout(bool sequentialOffsets)
		{
			if (isLayoutInProgress)
			{
				return;
			}

			var isOutermost = BeginLayoutPass();

			isLayoutInProgress = true;
			try
			{
				// Nested containers must settle first, otherwise their size is not final yet.
				// A container which was already settled in this pass (another instance of the same class) is final.
				foreach (var node in nodes)
				{
					var nested = GetNestedContainer(node);
					if (nested != null && nested.settledLayoutPass != currentLayoutPass)
					{
						nested.UpdateOffsets();
					}
				}

				if (PreservesSuccessorOffsets)
				{
					List<BaseNode> dummy = null;
					ReconcileSizeChanges(ref dummy);
				}

				var offset = 0;
				var complete = nodes.Count > 0 || hasBeenComplete || !RequiresNodesForCompleteLayout;
				foreach (var node in nodes)
				{
					node.Offset = sequentialOffsets ? offset : 0;
					offset += node.MemorySize;

					var isKnown = IsSizeKnown(node);
					node.LayoutSize = isKnown ? node.MemorySize : -1;
					complete &= isKnown;
				}

				// The layout inside a batch is transient, the layout performed by EndUpdate decides.
				if (updateCount == 0)
				{
					IsLayoutComplete = complete;
					hasBeenComplete |= complete;
				}

				settledLayoutPass = currentLayoutPass;
			}
			finally
			{
				isLayoutInProgress = false;

				EndLayoutPass(isOutermost);
			}
		}

		/// <summary>
		/// Starts a layout pass on the current thread if none is active.
		/// </summary>
		/// <returns>True if a new pass was started, false if the running pass is reused.</returns>
		private static bool BeginLayoutPass()
		{
			if (currentLayoutPass != 0)
			{
				return false;
			}

			do
			{
				currentLayoutPass = Interlocked.Increment(ref lastLayoutPass);
			} while (currentLayoutPass == 0);

			return true;
		}

		/// <summary>
		/// Ends the layout pass started by <see cref="BeginLayoutPass"/>.
		/// </summary>
		/// <param name="isOutermost">The return value of <see cref="BeginLayoutPass"/>.</param>
		private static void EndLayoutPass(bool isOutermost)
		{
			if (isOutermost)
			{
				currentLayoutPass = 0;
			}
		}

		/// <summary>
		/// Gets the container which determines the size of the node: the node itself if it is a container (union)
		/// or the most inner node of a wrapper chain with value semantics (class instance, array of class instances).
		/// Pointers don't count because their size doesn't depend on the pointed to node.
		/// </summary>
		private static BaseContainerNode GetNestedContainer(BaseNode node)
		{
			switch (node)
			{
				case BaseContainerNode container:
					return container;
				case BaseWrapperNode wrapper when wrapper.ShouldPerformCycleCheckForInnerNode() && wrapper.ResolveMostInnerNode() is BaseContainerNode inner:
					return inner;
				default:
					return null;
			}
		}

		/// <summary>
		/// Tests if the size of the node is final. The size of a node which is determined at runtime or which depends
		/// on a container whose layout is not complete is not known.
		/// </summary>
		private static bool IsSizeKnown(BaseNode node)
		{
			if (!node.ParticipatesInSizeCompensation)
			{
				return false;
			}

			var nested = GetNestedContainer(node);

			return nested == null || nested.IsLayoutComplete;
		}

		/// <summary>
		/// Gets the number of bytes the node occupies in the current layout or its real size if it was never laid out.
		/// </summary>
		private static int GetOccupiedSize(BaseNode node)
		{
			return node.LayoutSize >= 0 ? node.LayoutSize : node.MemorySize;
		}

		/// <summary>
		/// Compensates every child which changed its size since the last layout.
		/// Children which were never laid out (freshly inserted) don't get compensated because inserting is intentional.
		/// </summary>
		private void ReconcileSizeChanges(ref List<BaseNode> createdNodes)
		{
			for (var index = 0; index < nodes.Count; ++index)
			{
				var node = nodes[index];

				var layoutSize = node.LayoutSize;
				if (layoutSize < 0)
				{
					continue;
				}

				var currentSize = node.MemorySize;
				if (currentSize == layoutSize)
				{
					continue;
				}

				CompensateSizeChange(index, layoutSize, ref createdNodes);

				node.LayoutSize = IsSizeKnown(node) ? currentSize : -1;
			}
		}

		/// <summary>
		/// Keeps the offsets of all successors of the node at <paramref name="index"/> stable after it changed its size from <paramref name="oldSize"/>.
		/// A grown node consumes the bytes of its successors, a shrunk node gets padded with default nodes.
		/// </summary>
		private void CompensateSizeChange(int index, int oldSize, ref List<BaseNode> createdNodes)
		{
			var newSize = nodes[index].MemorySize;

			if (newSize < oldSize)
			{
				InsertBytesCore(index + 1, oldSize - newSize, ref createdNodes);
			}
			else if (newSize > oldSize)
			{
				ConsumeSuccessors(index + 1, newSize - oldSize, ref createdNodes);
			}
		}

		/// <summary>
		/// Removes <paramref name="size"/> bytes of nodes beginning at <paramref name="index"/>.
		/// If the last node is only partially covered the remainder is padded with default nodes so the following node keeps its offset.
		/// </summary>
		private void ConsumeSuccessors(int index, int size, ref List<BaseNode> createdNodes)
		{
			while (size > 0 && index < nodes.Count)
			{
				var successor = nodes[index];

				var successorSize = GetOccupiedSize(successor);
				if (successorSize <= 0)
				{
					// Nodes without a size don't occupy any bytes, leave them alone.
					index++;

					continue;
				}

				DetachNode(successor);
				nodes.RemoveAt(index);

				if (successorSize > size)
				{
					InsertBytesCore(index, successorSize - size, ref createdNodes);
				}

				size -= Math.Min(size, successorSize);
			}
		}

		/// <summary>
		/// Dry run of a size change: gets the successors which would be removed if <paramref name="node"/> changed its size to <paramref name="newSize"/>.
		/// Partially covered successors are included because they get replaced by padding.
		/// </summary>
		/// <param name="node">The child node which changes its size.</param>
		/// <param name="newSize">The new size of the node.</param>
		/// <returns>The nodes which would be removed. Empty if nothing would be removed, if the container doesn't preserve
		/// the offsets of the successors or if the size of the node is not compensated (see <see cref="BaseNode.ParticipatesInSizeCompensation"/>).</returns>
		public IReadOnlyList<BaseNode> GetNodesConsumedByResize(BaseNode node, int newSize)
		{
			Contract.Requires(node != null);

			var index = FindNodeIndex(node);
			if (index == -1)
			{
				throw new ArgumentException($"Node {node} is not a child of {this}.");
			}

			if (!PreservesSuccessorOffsets || !node.ParticipatesInSizeCompensation)
			{
				return new List<BaseNode>();
			}

			return GetSuccessorsCoveredByGrowth(index, newSize - GetOccupiedSize(node));
		}

		/// <summary>
		/// Gets the successors of the node at <paramref name="index"/> which are covered (completely or partially) if the node grows by <paramref name="growth"/> bytes.
		/// </summary>
		private List<BaseNode> GetSuccessorsCoveredByGrowth(int index, int growth)
		{
			var covered = new List<BaseNode>();

			for (var i = index + 1; growth > 0 && i < nodes.Count; ++i)
			{
				var successor = nodes[i];

				var successorSize = GetOccupiedSize(successor);
				if (successorSize <= 0)
				{
					continue;
				}

				covered.Add(successor);

				growth -= successorSize;
			}

			return covered;
		}

		/// <summary>
		/// Dry run for replacing several ranges of nodes (see <see cref="ReplaceNodeRange"/>) with instances of one node type:
		/// gets the nodes which are not part of any range and not just padding (hex nodes) but would be consumed by the replacement.
		/// </summary>
		/// <param name="ranges">The contiguous nodes to replace, grouped by their container.</param>
		/// <param name="probe">An instance of the new node type. It gets initialized from the first node of every range
		/// (see <see cref="BaseNode.CopyFromNode"/>) because some node types adopt the size of the replaced node.</param>
		/// <returns>The distinct nodes which would be consumed, in order of appearance.</returns>
		public static IReadOnlyList<BaseNode> GetNodesConsumedByReplacingRanges(IEnumerable<(BaseContainerNode Container, IReadOnlyList<BaseNode> Nodes)> ranges, BaseNode probe)
		{
			Contract.Requires(ranges != null);
			Contract.Requires(probe != null);

			var consumed = new List<BaseNode>();

			if (!probe.ParticipatesInSizeCompensation)
			{
				// The new nodes shift their successors like the legacy layout did (see ReplaceChildNode).
				return consumed;
			}

			var rangeList = ranges.ToList();

			var selected = new HashSet<BaseNode>(rangeList.SelectMany(r => r.Nodes));

			foreach (var (container, nodes) in rangeList)
			{
				if (container == null || !container.PreservesSuccessorOffsets || nodes.Count == 0)
				{
					continue;
				}

				var index = container.FindNodeIndex(nodes[0]);
				if (index == -1)
				{
					continue;
				}

				probe.CopyFromNode(nodes[0]);

				// Only the first node of a range can grow beyond the range, all other nodes have to fit into it.
				consumed.AddRange(
					container.GetSuccessorsCoveredByGrowth(index, probe.MemorySize - GetOccupiedSize(nodes[0]))
						.Where(n => !selected.Contains(n) && !(n is BaseHexNode))
				);
			}

			return consumed.Distinct().ToList();
		}

		/// <summary>Searches for the node and returns the zero based index.</summary>
		/// <param name="node">The node to search.</param>
		/// <returns>The found node index or -1 if the node was not found.</returns>
		public int FindNodeIndex(BaseNode node)
		{
			Contract.Requires(node != null);
			Contract.Ensures(Contract.Result<int>() >= -1 && Contract.Result<int>() < nodes.Count);

			return nodes.FindIndex(n => n == node);
		}

		/// <summary>
		/// Checks if the node exists in the container.
		/// </summary>
		/// <param name="node">The node to search.</param>
		/// <returns>True if the node exists in the container, false otherwise.</returns>
		public bool ContainsNode(BaseNode node)
		{
			return FindNodeIndex(node) != -1;
		}

		/// <summary>
		/// Tries to get the predecessor of the given node in the container.
		/// </summary>
		/// <param name="node">The root node.</param>
		/// <param name="predecessor">The predecessor of the given node.</param>
		/// <returns>True if a predecessor exists, otherwise false.</returns>
		public bool TryGetPredecessor(BaseNode node, out BaseNode predecessor)
		{
			Contract.Requires(node != null);

			return TryGetNeighbour(node, -1, out predecessor);
		}

		/// <summary>
		/// Tries to get the successor of the given node in the container.
		/// </summary>
		/// <param name="node">The root node.</param>
		/// <param name="successor">The successor of the given node.</param>
		/// <returns>True if a successor exists, otherwise false.</returns>
		public bool TryGetSuccessor(BaseNode node, out BaseNode successor)
		{
			Contract.Requires(node != null);

			return TryGetNeighbour(node, 1, out successor);
		}

		private bool TryGetNeighbour(BaseNode node, int offset, out BaseNode neighbour)
		{
			Contract.Requires(node != null);

			neighbour = null;

			var index = FindNodeIndex(node);
			if (index == -1)
			{
				return false;
			}

			var neighbourIndex = index + offset;
			if (neighbourIndex < 0 || neighbourIndex >= nodes.Count)
			{
				return false;
			}

			neighbour = nodes[neighbourIndex];

			return true;
		}

		/// <summary>
		/// Disables internal events to speed up batch processing.
		/// <see cref="EndUpdate"/> must be called to restore the functionality.
		/// </summary>
		public void BeginUpdate()
		{
			updateCount++;
		}

		/// <summary>
		/// Enables internal events disabled by <see cref="BeginUpdate"/>.
		/// </summary>
		public void EndUpdate()
		{
			updateCount = Math.Max(0, updateCount - 1);

			OnNodesUpdated();
		}

		private void OnNodesUpdated()
		{
			if (updateCount == 0)
			{
				UpdateOffsets();

				GetParentContainer()?.ChildHasChanged(this);
			}
		}

		/// <summary>
		/// Removes the node from this container without touching the node list. Removed nodes are no longer selected
		/// and lose their parent so stale references (selections, hot spots) can detect that the node is gone.
		/// </summary>
		private void DetachNode(BaseNode node)
		{
			if (node.ParentNode == this)
			{
				node.ParentNode = null;
			}

			node.IsSelected = false;
		}

		/// <summary>Replaces the old node with the new node.</summary>
		/// <param name="oldNode">The old node to replacce.</param>
		/// <param name="newNode">The new node.</param>
		public void ReplaceChildNode(BaseNode oldNode, BaseNode newNode)
		{
			Contract.Requires(oldNode != null);
			Contract.Requires(newNode != null);

			List<BaseNode> dummy = null;
			ReplaceChildNode(oldNode, newNode, ref dummy);
		}

		/// <summary>Replaces the old node with the new node.</summary>
		/// <param name="oldNode">The old node to replacce.</param>
		/// <param name="newNode">The new node.</param>
		/// <param name="additionalCreatedNodes">[out] A list for additional created nodes (see <see cref="ShouldCompensateSizeChanges"/>) or null if not needed.</param>
		public void ReplaceChildNode(BaseNode oldNode, BaseNode newNode, ref List<BaseNode> additionalCreatedNodes)
		{
			Contract.Requires(oldNode != null);
			Contract.Requires(newNode != null);

			CheckCanHandleChildNode(newNode);

			var index = FindNodeIndex(oldNode);
			if (index == -1)
			{
				throw new ArgumentException($"Node {oldNode} is not a child of {this}.");
			}

			newNode.CopyFromNode(oldNode);

			var oldSize = GetOccupiedSize(oldNode);

			DetachNode(oldNode);

			newNode.ParentNode = this;

			nodes[index] = newNode;

			if (ShouldCompensateSizeChanges)
			{
				// The size of a node which doesn't participate in the compensation (function) is just a placeholder
				// until the node is drawn, so it is placed like the legacy layout did.
				if (ContainerLayoutPolicy.PreserveSuccessorOffsets && newNode.ParticipatesInSizeCompensation)
				{
					GetNestedContainer(newNode)?.UpdateOffsets();

					CompensateSizeChange(index, oldSize, ref additionalCreatedNodes);
				}
				else
				{
					// Legacy behaviour: only shrinking gets padded, growing shifts the successors.
					var newSize = newNode.MemorySize;
					if (newSize < oldSize)
					{
						InsertBytesCore(index + 1, oldSize - newSize, ref additionalCreatedNodes);
					}
				}
			}

			// The replacement is compensated, don't do it again in the next layout.
			newNode.LayoutSize = IsSizeKnown(newNode) ? newNode.MemorySize : -1;

			OnNodesUpdated();
		}

		/// <summary>
		/// Replaces the byte range covered by the given contiguous child nodes with nodes created by <paramref name="createNode"/>.
		/// If <see cref="PreservesSuccessorOffsets"/> is true the range is filled with as many instances as fit,
		/// the remainder is padded with default nodes and nodes inside the range which can't be replaced are converted to default nodes.
		/// If the first instance is bigger than the whole range it consumes bytes beyond the range exactly like a single growth.
		/// Otherwise, and for node types which don't participate in the compensation (see <see cref="BaseNode.ParticipatesInSizeCompensation"/>),
		/// every node of the range (and every padding node created by it) is replaced on its own like the legacy ReClass.NET did.
		/// </summary>
		/// <param name="range">The contiguous child nodes to replace, ordered by position. Nodes which are no longer children are ignored.</param>
		/// <param name="createNode">Factory for the new nodes. Gets called once per created instance.</param>
		/// <returns>The nodes created by <paramref name="createNode"/> which were placed into the container.</returns>
		public IReadOnlyList<BaseNode> ReplaceNodeRange(IEnumerable<BaseNode> range, Func<BaseNode> createNode)
		{
			Contract.Requires(range != null);
			Contract.Requires(createNode != null);

			var rangeNodes = range.Where(ContainsNode).ToList();

			var placedNodes = new List<BaseNode>();
			if (rangeNodes.Count == 0)
			{
				return placedNodes;
			}

			var startIndex = FindNodeIndex(rangeNodes[0]);
			for (var i = 1; i < rangeNodes.Count; ++i)
			{
				if (startIndex + i >= nodes.Count || nodes[startIndex + i] != rangeNodes[i])
				{
					throw new ArgumentException("The nodes of the range must be contiguous.", nameof(range));
				}
			}

			// The first node decides how the range gets filled.
			var firstNode = createNode();

			BeginUpdate();
			try
			{
				if (rangeNodes.Count == 1 || !PreservesSuccessorOffsets || !firstNode.ParticipatesInSizeCompensation)
				{
					ReplaceNodesIndividually(rangeNodes, firstNode, createNode, placedNodes);
				}
				else
				{
					RefillRange(startIndex, rangeNodes.Sum(n => n.MemorySize), firstNode, createNode, placedNodes);
				}
			}
			finally
			{
				EndUpdate();
			}

			return placedNodes;
		}

		/// <summary>
		/// Legacy range replacement: every node gets replaced on its own and padding created by a replacement gets replaced too.
		/// </summary>
		private void ReplaceNodesIndividually(List<BaseNode> rangeNodes, BaseNode firstNode, Func<BaseNode> createNode, List<BaseNode> placedNodes)
		{
			var replaceMultiple = rangeNodes.Count > 1;

			var queue = new Queue<BaseNode>(rangeNodes);
			while (queue.Count > 0)
			{
				var target = queue.Dequeue();
				if (!ContainsNode(target))
				{
					continue;
				}

				var node = firstNode ?? createNode();
				firstNode = null;

				var createdNodes = new List<BaseNode>();
				ReplaceChildNode(target, node, ref createdNodes);

				placedNodes.Add(node);

				if (replaceMultiple)
				{
					foreach (var createdNode in createdNodes)
					{
						queue.Enqueue(createdNode);
					}
				}
			}
		}

		/// <summary>
		/// Fills <paramref name="rangeSize"/> bytes beginning at <paramref name="startIndex"/> with as many created nodes as fit.
		/// The remainder of the range is converted to default nodes.
		/// </summary>
		private void RefillRange(int startIndex, int rangeSize, BaseNode firstNode, Func<BaseNode> createNode, List<BaseNode> placedNodes)
		{
			var index = startIndex;
			var filled = 0;

			while (filled < rangeSize && index < nodes.Count)
			{
				var target = nodes[index];

				var node = firstNode ?? createNode();
				firstNode = null;

				node.CopyFromNode(target); // Some nodes (text, bit field) adopt the size of the replaced node.

				var nodeSize = node.MemorySize;
				if (nodeSize <= 0)
				{
					break;
				}

				// The first node may consume bytes beyond the range, all other nodes have to fit.
				if (placedNodes.Count > 0 && filled + nodeSize > rangeSize)
				{
					break;
				}

				List<BaseNode> dummy = null;
				ReplaceChildNode(target, node, ref dummy);

				placedNodes.Add(node);

				filled += nodeSize;
				index++;
			}

			// Pad the remainder of the range with default nodes.
			while (filled < rangeSize && index < nodes.Count)
			{
				var rest = nodes[index];

				var restSize = rest.MemorySize;
				if (restSize > 0 && !(rest is BaseHexNode))
				{
					var padding = CreateDefaultNodeForSize(restSize);
					if (padding != null)
					{
						List<BaseNode> dummy = null;
						ReplaceChildNode(rest, padding, ref dummy);

						restSize = padding.MemorySize;
					}
				}

				filled += restSize;
				index++;
			}
		}

		/// <summary>
		/// Creates the default container node which takes up to <paramref name="size"/> bytes.
		/// </summary>
		/// <param name="size">The maximum size in bytes.</param>
		/// <returns>A new node or null if no node is available for this size.</returns>
		protected virtual BaseNode CreateDefaultNodeForSize(int size)
		{
			Contract.Requires(size > 0);

#if RECLASSNET64
			if (size >= 8)
			{
				return new Hex64Node();
			}
#endif
			if (size >= 4)
			{
				return new Hex32Node();
			}
			if (size >= 2)
			{
				return new Hex16Node();
			}

			return new Hex8Node();
		}

		/// <summary>Adds the specific amount of bytes at the end of the node.</summary>
		/// <param name="size">The number of bytes to insert.</param>
		public void AddBytes(int size)
		{
			List<BaseNode> dummy = null;
			InsertBytes(nodes.Count, size, ref dummy);
		}

		public void InsertBytes(BaseNode position, int size)
		{
			List<BaseNode> dummy = null;
			InsertBytes(FindNodeIndex(position), size, ref dummy);
		}

		/// <summary>Inserts <paramref name="size"/> bytes at the specified position.</summary>
		/// <param name="index">Zero-based position.</param>
		/// <param name="size">The number of bytes to insert.</param>
		/// <param name="createdNodes">[out] A list with the created nodes.</param>
		protected void InsertBytes(int index, int size, ref List<BaseNode> createdNodes)
		{
			if (index < 0 || index > nodes.Count)
			{
				throw new ArgumentOutOfRangeException($"The index {index} is not in the range [0, {nodes.Count}].");
			}

			if (size == 0)
			{
				return;
			}

			InsertBytesCore(index, size, ref createdNodes);

			OnNodesUpdated();
		}

		/// <summary>
		/// Inserts default nodes for <paramref name="size"/> bytes at <paramref name="index"/> without notifying anyone.
		/// </summary>
		private void InsertBytesCore(int index, int size, ref List<BaseNode> createdNodes)
		{
			while (size > 0)
			{
				var node = CreateDefaultNodeForSize(size);
				if (node == null || node.MemorySize <= 0)
				{
					break;
				}

				node.ParentNode = this;

				nodes.Insert(index, node);

				createdNodes?.Add(node);

				size -= node.MemorySize;

				index++;
			}
		}

		/// <summary>
		/// Adds all nodes at the end of the container in one batch (see <see cref="AddNode"/>).
		/// </summary>
		/// <param name="nodes">The nodes to add.</param>
		public void AddNodes(IEnumerable<BaseNode> nodes)
		{
			Contract.Requires(nodes != null);

			BeginUpdate();
			try
			{
				foreach (var node in nodes)
				{
					AddNode(node);
				}
			}
			finally
			{
				EndUpdate();
			}
		}

		/// <summary>
		/// Adds the node at the end of the container.
		/// Every call outside of a batch lays the container out and publishes the new size to the containers which
		/// reference it. A class which gets filled node by node therefore grows step by step and, if it is referenced
		/// by a class whose layout is already complete, the instances consume their successors on every step
		/// (see <see cref="ContainerLayoutPolicy"/>). Bulk fills (importers, generators) must be batched with
		/// <see cref="BeginUpdate"/> / <see cref="EndUpdate"/> or use <see cref="AddNodes"/>, which publishes the final size once.
		/// </summary>
		/// <param name="node">The node to add.</param>
		public void AddNode(BaseNode node)
		{
			Contract.Requires(node != null);

			CheckCanHandleChildNode(node);

			node.ParentNode = this;
			node.LayoutSize = -1;

			nodes.Add(node);

			OnNodesUpdated();
		}

		/// <summary>
		/// Inserts the node infront of the <paramref name="position"/> node.
		/// </summary>
		/// <param name="position">The target node.</param>
		/// <param name="node">The node to insert.</param>
		public void InsertNode(BaseNode position, BaseNode node)
		{
			Contract.Requires(node != null);

			CheckCanHandleChildNode(node);

			var index = FindNodeIndex(position);
			if (index == -1)
			{
				throw new ArgumentException();
			}

			node.ParentNode = this;
			node.LayoutSize = -1;

			nodes.Insert(index, node);

			OnNodesUpdated();
		}

		/// <summary>Removes the specified node. The successors of the node move up.</summary>
		/// <param name="node">The node to remove.</param>
		/// <returns>True if it succeeds, false if it fails.</returns>
		public bool RemoveNode(BaseNode node)
		{
			Contract.Requires(node != null);

			var result = nodes.Remove(node);
			if (result)
			{
				DetachNode(node);

				OnNodesUpdated();
			}
			return result;
		}

		/// <summary>
		/// Called by a child if it has changed (for example its size). The default implementation recalculates the
		/// layout of this container and notifies the parent container.
		/// </summary>
		/// <param name="child">The child.</param>
		protected internal virtual void ChildHasChanged(BaseNode child)
		{
			if (child == this)
			{
				// A container without a parent notifies itself (see GetParentContainer), nothing to propagate.
				return;
			}

			OnNodesUpdated();
		}
	}
}
