using System;
using System.Collections.Generic;
using System.Linq;
using NFluent;
using ReClassNET.Nodes;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Multi selection replacement (<see cref="BaseContainerNode.ReplaceNodeRange"/>) and the dry run APIs the UI uses
	/// to ask for confirmation before defined nodes get consumed.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ReplaceNodeRangeTest
	{
		public ReplaceNodeRangeTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		[Fact]
		public void RangeIsFilledWithAsManyInstancesAsFit()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var e = new FloatNode().Named("e");
			var classNode = CreateClass(a, b, c, d, e);

			var created = 0;
			var placed = classNode.ReplaceNodeRange(new[] { a, b, c }, () =>
			{
				created++;
				return new Vector2Node();
			});

			// 12 bytes: one Vector2 fits, the second one doesn't, the remaining 4 bytes stay padding.
			Check.That(placed).HasSize(1);
			Check.That(created).IsEqualTo(2);
			CheckLayout(classNode, (typeof(Vector2Node), 0), (typeof(Hex32Node), 8), (typeof(Hex32Node), 12), (typeof(FloatNode), 16));
			Check.That(classNode.Nodes[0]).IsSameReferenceAs(placed[0]);
			Check.That(classNode.Nodes[0].Name).IsEqualTo("a");
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(d);
			Check.That(classNode.Nodes[3]).IsSameReferenceAs(e);
		}

		[Fact]
		public void RangeIsFilledCompletelyWhenTheTypeFitsExactly()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var e = new FloatNode().Named("e");
			var classNode = CreateClass(a, b, c, d, e);

			var placed = classNode.ReplaceNodeRange(new[] { a, b, c, d }, () => new Vector2Node());

			Check.That(placed).HasSize(2);
			CheckLayout(classNode, (typeof(Vector2Node), 0), (typeof(Vector2Node), 8), (typeof(FloatNode), 16));
			Check.That(classNode.Nodes.Take(2)).ContainsExactly(placed);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(e);
		}

		[Fact]
		public void SmallerTypeRefillsTheRangeAndPadsTheRemainder()
		{
			var a = new Hex64Node().Named("a");
			var b = new Hex64Node().Named("b");
			var c = new Hex8Node().Named("c");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, b, c, tail);

			var placed = classNode.ReplaceNodeRange(new BaseNode[] { a, b, c }, () => new FloatNode());

			// 17 bytes: four floats and one byte of padding.
			Check.That(placed).HasSize(4);
			CheckLayout(classNode, (typeof(FloatNode), 0), (typeof(FloatNode), 4), (typeof(FloatNode), 8), (typeof(FloatNode), 12), (typeof(Hex8Node), 16), (typeof(FloatNode), 17));
			Check.That(classNode.Nodes[5]).IsSameReferenceAs(tail);
			Check.That(classNode.Nodes[4]).IsSameReferenceAs(c);
		}

		[Fact]
		public void BiggerTypeThanTheRangeConsumesBeyondTheRange()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex64Node().Named("c");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, b, c, tail);

			var placed = classNode.ReplaceNodeRange(new[] { a, b }, () => new Vector3Node());

			// The Vector3 (12 bytes) covers a, b and 4 bytes of c. The rest of c gets padded.
			Check.That(placed).HasSize(1);
			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(Hex32Node), 12), (typeof(FloatNode), 16));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(tail);
			CheckDetached(classNode, c);
		}

		[Fact]
		public void DefinedNodesInsideTheRangeWhichDoNotFitBecomePadding()
		{
			var a = new FloatNode().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Int32Node().Named("c");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, b, c, tail);

			var placed = classNode.ReplaceNodeRange(new BaseNode[] { a, b, c }, () => new Hex64Node());

			Check.That(placed).HasSize(1);
			CheckLayout(classNode, (typeof(Hex64Node), 0), (typeof(Hex32Node), 8), (typeof(FloatNode), 12));
			Check.That(classNode.Nodes[0].Name).IsEqualTo("a");
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(tail);
			CheckDetached(classNode, b);
			CheckDetached(classNode, c);
		}

		[Fact]
		public void TypesWhichAdoptTheSizeReplaceEveryNodeOfTheRange()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex64Node().Named("b");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, b, tail);

			var placed = classNode.ReplaceNodeRange(new BaseNode[] { a, b }, () => new Utf8TextNode());

			Check.That(placed).HasSize(2);
			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Utf8TextNode), 4), (typeof(FloatNode), 12));
			Check.That(((Utf8TextNode)placed[0]).Length).IsEqualTo(4);
			Check.That(((Utf8TextNode)placed[1]).Length).IsEqualTo(8);
			Check.That(placed[0].Name).IsEqualTo("a");
			Check.That(placed[1].Name).IsEqualTo("b");
		}

		[Fact]
		public void SingleNodeRangeIsAPlainReplacement()
		{
			var a = new Hex64Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			var placed = classNode.ReplaceNodeRange(new[] { a }, () => new Int32Node());

			// The padding of a single shrunk node is not refilled.
			Check.That(placed).HasSize(1);
			CheckLayout(classNode, (typeof(Int32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(b);
		}

		[Fact]
		public void NodesWhichAreNoLongerChildrenAreIgnored()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, b, c);

			classNode.ReplaceChildNode(a, new Hex64Node()); // consumes b

			var placed = classNode.ReplaceNodeRange(new[] { b, c }, () => new FloatNode());

			Check.That(placed).HasSize(1);
			CheckLayout(classNode, (typeof(Hex64Node), 0), (typeof(FloatNode), 8));

			Check.That(classNode.ReplaceNodeRange(new[] { b }, () => new FloatNode())).IsEmpty();
		}

		[Fact]
		public void NonContiguousRangeThrows()
		{
			var a = new Hex32Node().Named("a");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, new Hex32Node().Named("b"), c);

			Check.ThatCode(() => classNode.ReplaceNodeRange(new[] { a, c }, () => new FloatNode())).Throws<ArgumentException>();
		}

		[Fact]
		public void ZeroSizedTypeDoesNotLoopForever()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			var placed = classNode.ReplaceNodeRange(new[] { a, b }, () => new ArrayNode { Count = 0 }.WithInner(new Hex32Node()));

			Check.That(placed).IsEmpty();
			Check.That(classNode.Nodes).ContainsExactly(a, b);
		}

		[Fact]
		public void RangeInsideAUnionReplacesEveryMember()
		{
			var m1 = new Hex32Node().Named("m1");
			var m2 = new Hex32Node().Named("m2");
			var union = UnionOf(m1, m2);
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(union, new Hex32Node(), new Hex32Node(), tail);

			var placed = union.ReplaceNodeRange(new[] { m1, m2 }, () => new Vector3Node());

			// Unions don't compensate, every member gets replaced. The class keeps the offset of the tail.
			Check.That(placed).HasSize(2);
			Check.That(union.Nodes).ContainsExactly(placed);
			Check.That(union.MemorySize).IsEqualTo(12);
			CheckLayout(classNode, (typeof(UnionNode), 0), (typeof(FloatNode), 12));
			Check.That(tail.Offset).IsEqualTo(12);
		}

		[Fact]
		public void DryRunReportsFullyAndPartiallyConsumedSuccessors()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex64Node().Named("c");
			var d = new FloatNode().Named("d");
			var classNode = CreateClass(a, b, c, d);

			Check.That(classNode.GetNodesConsumedByResize(a, 4)).IsEmpty();
			Check.That(classNode.GetNodesConsumedByResize(a, 2)).IsEmpty();
			Check.That(classNode.GetNodesConsumedByResize(a, 8)).ContainsExactly(b);
			Check.That(classNode.GetNodesConsumedByResize(a, 12)).ContainsExactly(b, c);
			Check.That(classNode.GetNodesConsumedByResize(a, 16)).ContainsExactly(b, c);
			Check.That(classNode.GetNodesConsumedByResize(a, 20)).ContainsExactly(b, c, d);
			Check.That(classNode.GetNodesConsumedByResize(a, 100)).ContainsExactly(b, c, d);
			Check.That(classNode.GetNodesConsumedByResize(d, 100)).IsEmpty();

			// The dry run doesn't change anything.
			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex64Node), 8), (typeof(FloatNode), 16));
		}

		[Fact]
		public void DryRunSkipsZeroSizedNodes()
		{
			var a = new Hex32Node().Named("a");
			var empty = Text8(0).Named("empty");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, empty, b);

			Check.That(classNode.GetNodesConsumedByResize(a, 8)).ContainsExactly(b);
		}

		[Fact]
		public void DryRunForUnknownNodeThrows()
		{
			var classNode = CreateClass(new Hex32Node());

			Check.ThatCode(() => classNode.GetNodesConsumedByResize(new Hex32Node(), 8)).Throws<ArgumentException>();
		}

		[Fact]
		public void DryRunForRangesReportsOnlyDefinedNodesOutsideTheSelection()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var health = new Int32Node().Named("health");
			var pad = new Hex32Node().Named("pad");
			var ammo = new Int32Node().Named("ammo");
			var classNode = CreateClass(a, b, health, pad, ammo);

			var ranges = new[] { (Container: (BaseContainerNode)classNode, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { a, b }) };

			// Vector2 fits into the selection.
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Vector2Node())).IsEmpty();

			// Vector3 consumes health.
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Vector3Node())).ContainsExactly(health);

			// Vector4 consumes health and the padding, only health is reported.
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Vector4Node())).ContainsExactly(health);

			// Matrix3x3 (36 bytes) consumes everything.
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Matrix3x3Node())).ContainsExactly(health, ammo);

			// Nodes which adopt the size of the replaced node never consume anything.
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Utf8TextNode())).IsEmpty();

			// Selected nodes are never reported.
			var selectedRanges = new[] { (Container: (BaseContainerNode)classNode, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { a, b, health }) };
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(selectedRanges, new Vector3Node())).IsEmpty();
		}

		[Fact]
		public void DryRunForRangesHandlesMultipleContainersAndDetachedNodes()
		{
			var a1 = new Hex32Node().Named("a1");
			var f1 = new FloatNode().Named("f1");
			var class1 = CreateClass(a1, f1);

			var a2 = new Hex32Node().Named("a2");
			var f2 = new FloatNode().Named("f2");
			var class2 = CreateClass(a2, f2);

			var detached = new Hex32Node().Named("detached");

			var ranges = new[]
			{
				(Container: (BaseContainerNode)class1, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { a1 }),
				(Container: (BaseContainerNode)class2, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { a2 }),
				(Container: (BaseContainerNode)class2, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { detached }),
				(Container: (BaseContainerNode)class2, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[0])
			};

			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Hex64Node())).ContainsExactly(f1, f2);
		}

		/// <summary>
		/// Runs the dry run and the real replacement on the same fixture and checks that exactly the predicted
		/// defined nodes (not selected, not padding) were consumed. The confirmation dialog depends on both agreeing.
		/// </summary>
		private static void CheckDryRunMatchesReplacement(Func<BaseNode> createNode, ClassNode classNode, params BaseNode[][] ranges)
		{
			var selected = new HashSet<BaseNode>(ranges.SelectMany(r => r));
			var defined = classNode.Nodes.Where(n => !selected.Contains(n) && !(n is BaseHexNode)).ToList();

			var predicted = BaseContainerNode.GetNodesConsumedByReplacingRanges(
				ranges.Select(r => ((BaseContainerNode)classNode, (IReadOnlyList<BaseNode>)r)),
				createNode()
			);

			foreach (var range in ranges)
			{
				// Like the UI does: nodes consumed by a previous range are skipped.
				classNode.ReplaceNodeRange(range.Where(classNode.ContainsNode), createNode);
			}

			var consumed = defined.Where(n => !classNode.ContainsNode(n)).ToList();

			Check.That(consumed).ContainsExactly(predicted);
		}

		[Fact]
		public void DryRunMatchesTheReplacementWithAZeroSizedNodeBehindTheRange()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b, Text8(0).Named("empty"), new Int32Node().Named("x"), new FloatNode().Named("y"));

			CheckDryRunMatchesReplacement(() => new Vector3Node(), classNode, new BaseNode[] { a, b });

			Check.That(classNode.Names()).ContainsExactly("a", "empty", "y");
		}

		[Fact]
		public void DryRunMatchesTheReplacementWithAPartiallyConsumedPaddingNode()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b, new Hex64Node().Named("c"), new FloatNode().Named("d"));

			CheckDryRunMatchesReplacement(() => new Vector3Node(), classNode, new BaseNode[] { a, b });

			Check.That(classNode.Nodes.Last().Name).IsEqualTo("d");
			Check.That(classNode.Nodes.Last().Offset).IsEqualTo(16);
		}

		[Fact]
		public void DryRunMatchesTheReplacementWithAPartiallyConsumedDefinedNode()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Int64Node().Named("c");
			var classNode = CreateClass(a, b, c, new FloatNode().Named("d"));

			CheckDryRunMatchesReplacement(() => new Vector3Node(), classNode, new BaseNode[] { a, b });

			// The remaining 4 bytes of c are padding now.
			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(Hex32Node), 12), (typeof(FloatNode), 16));
			Check.That(classNode.Nodes.Last().Name).IsEqualTo("d");
			CheckDetached(classNode, c);
		}

		[Fact]
		public void DryRunMatchesTheReplacementWithTwoPartitions()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, new Int32Node().Named("x"), b, c, new Int32Node().Named("d"));

			// The first partition grows into the second one, the second one grows beyond the class.
			CheckDryRunMatchesReplacement(() => new Vector3Node(), classNode, new BaseNode[] { a }, new BaseNode[] { b, c });

			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(Vector3Node), 12));
			Check.That(classNode.Names()).ContainsExactly("a", "c");
		}

		[Fact]
		public void DryRunMatchesTheReplacementForSizeAdoptingTypes()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex64Node().Named("b");
			var classNode = CreateClass(a, b, new Int32Node().Named("x"));

			CheckDryRunMatchesReplacement(() => new Utf8TextNode(), classNode, new BaseNode[] { a, b });

			Check.That(classNode.Names()).ContainsExactly("a", "b", "x");
		}

		[Fact]
		public void DryRunMatchesTheReplacementWhenTheRangeIsRefilledWithSmallerNodes()
		{
			var a = new Hex64Node().Named("a");
			var b = new Int64Node().Named("b");
			var classNode = CreateClass(a, b, new Int32Node().Named("x"));

			CheckDryRunMatchesReplacement(() => new FloatNode(), classNode, new BaseNode[] { a, b });

			CheckLayout(classNode, (typeof(FloatNode), 0), (typeof(FloatNode), 4), (typeof(FloatNode), 8), (typeof(FloatNode), 12), (typeof(Int32Node), 16));
		}

		[Fact]
		public void ContiguityCheckUsesThePositionOfTheNodes()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, b, c);

			// Reversed order is not contiguous either.
			Check.ThatCode(() => classNode.ReplaceNodeRange(new[] { b, a }, () => new FloatNode())).Throws<ArgumentException>();
			Check.ThatCode(() => classNode.ReplaceNodeRange(new[] { c, c }, () => new FloatNode())).Throws<ArgumentException>();

			var placed = classNode.ReplaceNodeRange(new[] { b, c }, () => new FloatNode());

			Check.That(placed).HasSize(2);
			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(FloatNode), 4), (typeof(FloatNode), 8));
		}

		[Fact]
		public void ConsumedNodesAreNotReplacedAgain()
		{
			// Simulates the UI processing two partitions where the first one grows into the second one.
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var e = new Hex32Node().Named("e");
			var classNode = CreateClass(a, b, c, d, e);

			var first = classNode.ReplaceNodeRange(new[] { a }, () => new Vector3Node());
			var second = classNode.ReplaceNodeRange(new[] { b, c, d }.Where(classNode.ContainsNode), () => new Vector3Node());

			Check.That(first).HasSize(1);
			Check.That(second).HasSize(1);
			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(Vector3Node), 12));
			Check.That(classNode.MemorySize).IsEqualTo(24);
		}
	}

	internal static class ArrayNodeTestExtension
	{
		public static ArrayNode WithInner(this ArrayNode node, BaseNode inner)
		{
			node.ChangeInnerNode(inner);
			return node;
		}
	}
}
