using System;
using System.Collections.Generic;
using NFluent;
using ReClassNET.Nodes;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Type changes via <see cref="BaseContainerNode.ReplaceChildNode(BaseNode, BaseNode)"/> with the default policy:
	/// the offsets of all nodes following the changed node stay the same (upstream issues #88 and #196).
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ContainerLayoutReplaceTest
	{
		public ContainerLayoutReplaceTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		[Fact]
		public void GrowthConsumesFollowingNodes()
		{
			// [Hex32 a][Hex32 b][Hex32 c][Float d]; a -> Vector3 gives [Vector3 a][Float d] with d still at offset 12.
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var d = new FloatNode().Named("d");
			var classNode = CreateClass(a, b, c, d);

			classNode.ReplaceChildNode(a, new Vector3Node());

			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(FloatNode), 12));
			Check.That(classNode.Names()).ContainsExactly("a", "d");
			Check.That(classNode.MemorySize).IsEqualTo(16);

			CheckDetached(classNode, a);
			CheckDetached(classNode, b);
			CheckDetached(classNode, c);
		}

		[Fact]
		public void GrowthConsumesOnlyTheNeededBytes()
		{
			// [Hex32 a][Hex32 b][Hex32 c][Float d]; a -> Hex64 gives [Hex64 a][Hex32 c][Float d] with c at 8, d at 12.
			var a = new Hex32Node().Named("a");
			var classNode = CreateClass(a, new Hex32Node().Named("b"), new Hex32Node().Named("c"), new FloatNode().Named("d"));

			classNode.ReplaceChildNode(a, new Hex64Node());

			CheckLayout(classNode, (typeof(Hex64Node), 0), (typeof(Hex32Node), 8), (typeof(FloatNode), 12));
			Check.That(classNode.Names()).ContainsExactly("a", "c", "d");
			Check.That(classNode.MemorySize).IsEqualTo(16);
		}

		[Fact]
		public void PartiallyConsumedNodeIsRepadded()
		{
			// [Hex8 a][Hex64 b][Float f]; a -> Int32 grows by 3, b is partially consumed, the remaining 5 bytes are padded.
			var a = new Hex8Node().Named("a");
			var b = new Hex64Node().Named("b");
			var f = new FloatNode().Named("f");
			var classNode = CreateClass(a, b, f);

			var createdNodes = new List<BaseNode>();
			classNode.ReplaceChildNode(a, new Int32Node(), ref createdNodes);

			CheckLayout(classNode, (typeof(Int32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex8Node), 8), (typeof(FloatNode), 9));
			Check.That(f.Offset).IsEqualTo(9);
			Check.That(classNode.MemorySize).IsEqualTo(13);

			Check.That(createdNodes).HasSize(2);
			Check.That(createdNodes[0]).IsInstanceOf<Hex32Node>();
			Check.That(createdNodes[1]).IsInstanceOf<Hex8Node>();
			Check.That(createdNodes[0].ParentNode).IsSameReferenceAs(classNode);

			CheckDetached(classNode, b);
		}

		[Fact]
		public void GrowthWhichExactlyCoversTheSuccessorKeepsClassSize()
		{
			// [Hex32 a][Hex32 b]; a -> Hex64 gives [Hex64 a] with the unchanged class size 8.
			var a = new Hex32Node().Named("a");
			var classNode = CreateClass(a, new Hex32Node().Named("b"));

			classNode.ReplaceChildNode(a, new Hex64Node());

			CheckLayout(classNode, (typeof(Hex64Node), 0));
			Check.That(classNode.MemorySize).IsEqualTo(8);
		}

		[Fact]
		public void GrowthOfTheLastNodeGrowsTheClass()
		{
			var a = new Hex32Node().Named("a");
			var classNode = CreateClass(a);

			classNode.ReplaceChildNode(a, new Vector3Node());

			CheckLayout(classNode, (typeof(Vector3Node), 0));
			Check.That(classNode.MemorySize).IsEqualTo(12);
		}

		[Fact]
		public void GrowthAtTheEndConsumesWhatIsAvailable()
		{
			// [Hex32 a][Hex32 b]; b -> Vector3 consumes nothing and the class grows to 16.
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(new Hex32Node().Named("a"), b);

			classNode.ReplaceChildNode(b, new Vector3Node());

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Vector3Node), 4));
			Check.That(classNode.MemorySize).IsEqualTo(16);
		}

		[Fact]
		public void Issue196Vector3KeepsOffsetOfFollowingField()
		{
			// Issue #196: Hex32 -> Vector3 consumed the 8 following bytes, the next defined field keeps its offset.
			var position = new Hex32Node().Named("position");
			var health = new Int32Node().Named("health");
			var classNode = CreateClass(position, new Hex32Node(), new Hex32Node(), health, new Hex32Node());

			classNode.ReplaceChildNode(position, new Vector3Node());

			Check.That(classNode.Nodes[0]).IsInstanceOf<Vector3Node>();
			Check.That(classNode.Nodes[0].Name).IsEqualTo("position");
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(health);
			Check.That(health.Offset).IsEqualTo(12);
			Check.That(classNode.MemorySize).IsEqualTo(20);
		}

#if RECLASSNET64
		[Fact]
		public void Hex32ToInt64OnX64()
		{
			var a = new Hex32Node().Named("a");
			var f = new FloatNode().Named("f");
			var classNode = CreateClass(a, new Hex32Node().Named("b"), f);

			classNode.ReplaceChildNode(a, new Int64Node());

			CheckLayout(classNode, (typeof(Int64Node), 0), (typeof(FloatNode), 8));
			Check.That(f.Offset).IsEqualTo(8);
		}

		[Fact]
		public void Hex32ToPointerOnX64()
		{
			var a = new Hex32Node().Named("a");
			var f = new FloatNode().Named("f");
			var classNode = CreateClass(a, new Hex32Node().Named("b"), f);

			var pointer = new PointerNode();
			pointer.Initialize();
			classNode.ReplaceChildNode(a, pointer);

			CheckLayout(classNode, (typeof(PointerNode), 0), (typeof(FloatNode), 8));
		}
#endif

		[Fact]
		public void Hex8ToInt16()
		{
			var a = new Hex8Node().Named("a");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, new Hex8Node().Named("b"), c);

			classNode.ReplaceChildNode(a, new Int16Node());

			CheckLayout(classNode, (typeof(Int16Node), 0), (typeof(Hex32Node), 2));
			Check.That(c.Offset).IsEqualTo(2);
		}

		[Fact]
		public void Hex32ToClassInstanceConsumesPointerSizeBytes()
		{
			var a = new Hex32Node().Named("a");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, new Hex32Node(), new Hex32Node(), new Hex32Node(), tail);

			var instance = new ClassInstanceNode();
			instance.Initialize(); // creates a class with IntPtr.Size bytes
			classNode.ReplaceChildNode(a, instance);

			Check.That(instance.MemorySize).IsEqualTo(IntPtr.Size);
			Check.That(classNode.Nodes[0]).IsSameReferenceAs(instance);
			Check.That(tail.Offset).IsEqualTo(16);
			Check.That(classNode.MemorySize).IsEqualTo(20);
		}

		[Fact]
		public void ChainedGrowth()
		{
			var a = new Hex32Node().Named("a");
			var e = new Hex32Node().Named("e");
			var classNode = CreateClass(a, new Hex32Node(), new Hex32Node(), new Hex32Node(), e);

			classNode.ReplaceChildNode(a, new Hex64Node());

			var a64 = classNode.Nodes[0];
			Check.That(a64).IsInstanceOf<Hex64Node>();
			Check.That(e.Offset).IsEqualTo(16);

			classNode.ReplaceChildNode(a64, new Vector4Node());

			CheckLayout(classNode, (typeof(Vector4Node), 0), (typeof(Hex32Node), 16));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(e);
			Check.That(classNode.MemorySize).IsEqualTo(20);
		}

		[Fact]
		public void ShrinkPadsThenGrowConsumesThePadding()
		{
			var a = new Hex64Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			var createdNodes = new List<BaseNode>();
			classNode.ReplaceChildNode(a, new Hex32Node(), ref createdNodes);

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(createdNodes).HasSize(1);
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(createdNodes[0]);
			Check.That(b.Offset).IsEqualTo(8);

			classNode.ReplaceChildNode(classNode.Nodes[0], new Hex64Node());

			CheckLayout(classNode, (typeof(Hex64Node), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(b);
			Check.That(classNode.MemorySize).IsEqualTo(12);
		}

		[Fact]
		public void ShrinkOfTheLastNodePadsTheClass()
		{
			var a = new Hex64Node().Named("a");
			var classNode = CreateClass(a);

			classNode.ReplaceChildNode(a, new Hex16Node());

			CheckLayout(classNode, (typeof(Hex16Node), 0), (typeof(Hex32Node), 2), (typeof(Hex16Node), 6));
			Check.That(classNode.MemorySize).IsEqualTo(8);
		}

		[Fact]
		public void ReplacedNodeKeepsNameCommentAndOffset()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node { Comment = "the comment" }.Named("b");
			var classNode = CreateClass(a, b);

			var replacement = new Int32Node();
			classNode.ReplaceChildNode(b, replacement);

			Check.That(replacement.Name).IsEqualTo("b");
			Check.That(replacement.Comment).IsEqualTo("the comment");
			Check.That(replacement.Offset).IsEqualTo(4);
			Check.That(replacement.ParentNode).IsSameReferenceAs(classNode);

			CheckDetached(classNode, b);
		}

		[Fact]
		public void ConsumedNodesLoseSelectionAndParent()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node { IsSelected = true }.Named("b");
			var c = new Hex32Node { IsSelected = true }.Named("c");
			var d = new Hex32Node { IsSelected = true }.Named("d");
			var classNode = CreateClass(a, b, c, d);

			classNode.ReplaceChildNode(a, new Vector3Node());

			CheckDetached(classNode, b);
			CheckDetached(classNode, c);

			Check.That(classNode.ContainsNode(d)).IsTrue();
			Check.That(d.ParentNode).IsSameReferenceAs(classNode);
			Check.That(d.IsSelected).IsTrue();
		}

		[Fact]
		public void ZeroSizedSuccessorsAreSkipped()
		{
			var a = new Hex32Node().Named("a");
			var empty = Text8(0).Named("empty");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, empty, b, c);

			Check.That(empty.MemorySize).IsEqualTo(0);

			classNode.ReplaceChildNode(a, new Hex64Node());

			CheckLayout(classNode, (typeof(Hex64Node), 0), (typeof(Utf8TextNode), 8), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(empty);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(c);
			CheckDetached(classNode, b);
		}

		[Fact]
		public void GrowthOfAZeroSizedNodeConsumesSuccessors()
		{
			var empty = Text8(0).Named("empty");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(empty, b, c);

			classNode.ReplaceChildNode(empty, new Int32Node());

			CheckLayout(classNode, (typeof(Int32Node), 0), (typeof(Hex32Node), 4));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
			Check.That(classNode.MemorySize).IsEqualTo(8);
		}

		[Fact]
		public void FreshlyInsertedNodesDoNotTriggerCompensation()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			classNode.InsertBytes(b, 4);

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(b.Offset).IsEqualTo(8);

			classNode.InsertNode(b, new Vector3Node().Named("v"));

			Check.That(b.Offset).IsEqualTo(20);
			Check.That(classNode.MemorySize).IsEqualTo(24);

			classNode.AddBytes(8);

			Check.That(classNode.MemorySize).IsEqualTo(32);
			Check.That(classNode.Nodes.Count).IsEqualTo(4 + (IntPtr.Size == 8 ? 1 : 2));
		}

		[Fact]
		public void RemovingANodeMovesTheSuccessorsUp()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, b, c);

			b.IsSelected = true;

			Check.That(classNode.RemoveNode(b)).IsTrue();

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4));
			Check.That(c.Offset).IsEqualTo(4);
			Check.That(classNode.MemorySize).IsEqualTo(8);
			CheckDetached(classNode, b);
		}

		[Fact]
		public void RemovingANodeWhichAlreadyMovedKeepsItsNewParent()
		{
			var a = new Hex32Node().Named("a");
			var classNode = CreateClass(a, new Hex32Node().Named("b"));

			var otherClass = ClassNode.Create();
			otherClass.AddNode(a);

			classNode.RemoveNode(a);

			Check.That(a.ParentNode).IsSameReferenceAs(otherClass);
		}

		[Fact]
		public void ReplacingAnUnknownNodeThrows()
		{
			var classNode = CreateClass(new Hex32Node());

			Check.ThatCode(() => classNode.ReplaceChildNode(new Hex32Node(), new Int32Node())).Throws<ArgumentException>();
		}

		[Fact]
		public void ReplaceInBatchCompensatesImmediately()
		{
			var a = new Hex32Node().Named("a");
			var d = new FloatNode().Named("d");
			var classNode = CreateClass(a, new Hex32Node(), new Hex32Node(), d);

			classNode.BeginUpdate();

			var createdNodes = new List<BaseNode>();
			classNode.ReplaceChildNode(a, new Vector3Node(), ref createdNodes);

			Check.That(classNode.Nodes.Count).IsEqualTo(2);

			classNode.EndUpdate();

			CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(FloatNode), 12));
			Check.That(createdNodes).IsEmpty();
		}

		[Fact]
		public void NestedBatchesCompensateOnce()
		{
			var a = new Hex64Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			classNode.BeginUpdate();
			classNode.BeginUpdate();

			classNode.ReplaceChildNode(a, new Hex32Node());

			classNode.EndUpdate();
			classNode.EndUpdate();
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(b.Offset).IsEqualTo(8);
		}
	}
}
