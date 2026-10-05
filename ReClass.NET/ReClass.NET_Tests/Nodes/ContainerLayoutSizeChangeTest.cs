using System;
using System.Linq;
using NFluent;
using ReClassNET.Nodes;
using ReClassNET.UI;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Size changes which are not type changes: text length, array count, inner node changes, nested containers and
	/// referenced classes. All of them are reconciled by <see cref="BaseContainerNode.UpdateOffsets"/>.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ContainerLayoutSizeChangeTest
	{
		public ContainerLayoutSizeChangeTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		[Fact]
		public void TextLengthIncreaseConsumesSuccessors()
		{
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, new Hex32Node().Named("b"), c);

			text.Length = 8;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
			Check.That(classNode.MemorySize).IsEqualTo(12);
		}

		[Fact]
		public void TextLengthEditThroughHotSpotCompensates()
		{
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, new Hex32Node().Named("b"), c);

			var notified = 0;
			classNode.NodesChanged += _ => notified++;

			// Simulates the user editing the length in the memory view.
			text.Update(new HotSpot { Id = 0, Text = "8", Node = text });

			Check.That(notified).IsEqualTo(1);
			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(c.Offset).IsEqualTo(8);
		}

		[Fact]
		public void TextLengthDecreasePads()
		{
			var text = Text8(4).Named("text");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(text, b);

			text.Length = 2;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex16Node), 2), (typeof(Hex32Node), 4));
			Check.That(b.Offset).IsEqualTo(4);
		}

		[Fact]
		public void Utf16TextLengthChange()
		{
			var text = new Utf16TextNode { Length = 2 }.Named("text"); // 4 bytes
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, new Hex32Node().Named("b"), c);

			text.Length = 4; // 8 bytes
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(Utf16TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
		}

		[Fact]
		public void ArrayCountIncreaseAndDecrease()
		{
			var array = ArrayOf(new Hex32Node(), 1).Named("array");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(array, new Hex32Node().Named("b"), c);

			array.Count = 2;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(ArrayNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);

			array.Count = 1;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(ArrayNode), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(c);
		}

		[Fact]
		public void ArrayCountEditThroughHotSpotCompensates()
		{
			var array = ArrayOf(new Hex32Node(), 1).Named("array");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(array, new Hex32Node().Named("b"), c);

			array.Update(new HotSpot { Id = 0, Text = "2", Node = array });

			CheckLayout(classNode, (typeof(ArrayNode), 0), (typeof(Hex32Node), 8));
			Check.That(c.Offset).IsEqualTo(8);
		}

		[Fact]
		public void ChangingTheInnerNodeOfAnArrayCompensates()
		{
			var array = ArrayOf(new Hex32Node(), 2).Named("array"); // 8 bytes
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(array, new Hex32Node().Named("b"), new Hex32Node().Named("b2"), c);

			array.ChangeInnerNode(new Hex64Node()); // 16 bytes

			CheckLayout(classNode, (typeof(ArrayNode), 0), (typeof(Hex32Node), 16));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);

			array.ChangeInnerNode(new Hex16Node()); // 4 bytes

			Check.That(classNode.Nodes[0]).IsSameReferenceAs(array);
			Check.That(classNode.Nodes.Last()).IsSameReferenceAs(c);
			Check.That(c.Offset).IsEqualTo(16);
			Check.That(classNode.MemorySize).IsEqualTo(20);
		}

		[Fact]
		public void BitFieldSizeChangeCompensates()
		{
			var bits = new BitFieldNode { Bits = 32 }.Named("bits");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(bits, new Hex32Node().Named("b"), c);

			bits.Bits = 64;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(BitFieldNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
		}

		[Fact]
		public void UnionInsideClassGrowsWhenItsBiggestMemberGrows()
		{
			var member = new Hex32Node().Named("member");
			var union = UnionOf(member, new Hex16Node()).Named("union");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var e = new Hex32Node().Named("e");
			var classNode = CreateClass(new Hex32Node().Named("a"), union, c, d, e);

			Check.That(union.MemorySize).IsEqualTo(4);

			// The union doesn't compensate itself but the class has to keep the offset of e.
			union.ReplaceChildNode(member, new Vector3Node());

			Check.That(union.MemorySize).IsEqualTo(12);
			Check.That(union.Nodes.Select(n => n.Offset)).ContainsExactly(0, 0);

			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(UnionNode), 4), (typeof(Hex32Node), 16));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(e);
			Check.That(classNode.MemorySize).IsEqualTo(20);

			CheckDetached(classNode, c);
			CheckDetached(classNode, d);
		}

		[Fact]
		public void UnionInsideClassShrinksWhenItsBiggestMemberShrinks()
		{
			var member = new Vector3Node().Named("member");
			var union = UnionOf(member, new Hex16Node()).Named("union");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(union, c);

			union.ReplaceChildNode(member, new Hex32Node());

			// The union shrank by 8 bytes which get padded so c keeps its offset.
			Check.That(union.MemorySize).IsEqualTo(4);
			Check.That(classNode.Nodes[0]).IsSameReferenceAs(union);
			Check.That(classNode.Nodes.Last()).IsSameReferenceAs(c);
			Check.That(c.Offset).IsEqualTo(12);
			Check.That(classNode.MemorySize).IsEqualTo(16);

			// The padding uses the biggest hex node of the platform, there is no Hex64 on x86.
#if RECLASSNET64
			CheckLayout(classNode, (typeof(UnionNode), 0), (typeof(Hex64Node), 4), (typeof(Hex32Node), 12));
#else
			CheckLayout(classNode, (typeof(UnionNode), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8), (typeof(Hex32Node), 12));
#endif
		}

		[Fact]
		public void ChangingTheReferencedClassOfAnInstanceCompensates()
		{
			var small = CreateClass(new Hex32Node().Named("s"));
			var big = CreateClass(new Vector3Node().Named("v"));

			var instance = InstanceOf(small).Named("instance");
			var tail = new FloatNode().Named("tail");
			var outer = CreateClass(instance, new Hex32Node().Named("b"), new Hex32Node().Named("c"), tail);

			// "Change class type" in the memory view: the instance grows by 8 bytes.
			instance.ChangeInnerNode(big);

			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 12));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(tail);
			Check.That(outer.MemorySize).IsEqualTo(16);

			// And back: the instance shrinks and gets padded.
			instance.ChangeInnerNode(small);

			Check.That(outer.Nodes[0]).IsSameReferenceAs(instance);
			Check.That(outer.Nodes.Last()).IsSameReferenceAs(tail);
			Check.That(tail.Offset).IsEqualTo(12);
			Check.That(outer.Nodes.Skip(1).Take(outer.Nodes.Count - 2).All(n => n is BaseHexNode)).IsTrue();
			Check.That(outer.MemorySize).IsEqualTo(16);
		}

		[Fact]
		public void ChangingToAnEmptyClassShiftsInsteadOfConsuming()
		{
			var filled = CreateClass(new Hex32Node(), new Hex32Node());
			var empty = ClassNode.Create();

			var instance = InstanceOf(filled).Named("instance");
			var tail = new FloatNode().Named("tail");
			var outer = CreateClass(instance, tail);

			Check.That(tail.Offset).IsEqualTo(8);

			// The shrink to 0 bytes is compensated with padding, but the size of a class which was never filled is unknown.
			instance.ChangeInnerNode(empty);

			Check.That(outer.Nodes[0]).IsSameReferenceAs(instance);
			Check.That(outer.Nodes.Last()).IsSameReferenceAs(tail);
			Check.That(tail.Offset).IsEqualTo(8);
			Check.That(outer.MemorySize).IsEqualTo(12);
			Check.That(outer.Nodes.Skip(1).Take(outer.Nodes.Count - 2).Sum(n => n.MemorySize)).IsEqualTo(8);
			Check.That(instance.LayoutSize).IsEqualTo(-1);
			Check.That(outer.IsLayoutComplete).IsFalse();

			// Filling the class is intentional (project loading), the successors shift and the padding stays.
			empty.AddBytes(4);

			Check.That(outer.Nodes.Last()).IsSameReferenceAs(tail);
			Check.That(tail.Offset).IsEqualTo(12);
			Check.That(outer.MemorySize).IsEqualTo(16);
			Check.That(instance.LayoutSize).IsEqualTo(4);
			Check.That(outer.IsLayoutComplete).IsTrue();
		}

		[Fact]
		public void EmptiedClassKeepsItsTrackingAndTheRefillIsCompensated()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var instance = InstanceOf(inner).Named("instance");
			var f = new FloatNode().Named("f");
			var outer = CreateClass(instance, f);

			Check.That(f.Offset).IsEqualTo(4);

			// Removing the last node of a class keeps the class complete with the known size 0.
			inner.RemoveNode(x);

			Check.That(inner.MemorySize).IsEqualTo(0);
			Check.That(inner.IsLayoutComplete).IsTrue();
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 0), (typeof(FloatNode), 4));
			Check.That(instance.LayoutSize).IsEqualTo(0);

			// Refilling the class is a growth from 0 which consumes the padding, f keeps its offset.
			inner.AddBytes(4);

			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 4));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(f);
			Check.That(outer.MemorySize).IsEqualTo(8);
		}

		[Fact]
		public void PointerToAChangedClassDoesNotCompensate()
		{
			var inner = CreateClass(new Hex32Node().Named("x"));

			var pointer = new PointerNode().Named("pointer");
			pointer.ChangeInnerNode(InstanceOf(inner));
			var c = new FloatNode().Named("c");
			var outer = CreateClass(pointer, c);

			Check.That(pointer.LayoutSize).IsEqualTo(IntPtr.Size);
			Check.That(outer.IsLayoutComplete).IsTrue();

			inner.AddBytes(64);
			outer.UpdateOffsets();

			Check.That(outer.Nodes).ContainsExactly(pointer, c);
			Check.That(c.Offset).IsEqualTo(IntPtr.Size);
			Check.That(outer.MemorySize).IsEqualTo(IntPtr.Size + 4);
		}

		[Fact]
		public void ArrayOfPointersToAChangedClassDoesNotCompensate()
		{
			var inner = CreateClass(new Hex32Node().Named("x"));

			var pointer = new PointerNode().Named("pointer");
			pointer.ChangeInnerNode(InstanceOf(inner));
			var array = ArrayOf(pointer, 3).Named("array");
			var c = new FloatNode().Named("c");
			var outer = CreateClass(array, c);

			inner.AddBytes(64);
			outer.UpdateOffsets();

			Check.That(outer.Nodes).ContainsExactly(array, c);
			Check.That(c.Offset).IsEqualTo(3 * IntPtr.Size);
			Check.That(outer.MemorySize).IsEqualTo(3 * IntPtr.Size + 4);
		}

		[Fact]
		public void LayoutInsideABatchKeepsTheCompletenessOfTheClass()
		{
			var inner = CreateClass(new Hex32Node().Named("x"));

			var instance = InstanceOf(inner).Named("instance");
			var f = new FloatNode().Named("f");
			var outer = CreateClass(instance, new Hex32Node().Named("pad"), f);

			inner.BeginUpdate();

			// Something else changes while the batch is open and the project lays out every class.
			outer.UpdateOffsets();

			Check.That(inner.IsLayoutComplete).IsTrue();
			Check.That(instance.LayoutSize).IsEqualTo(4);

			inner.AddBytes(4);
			inner.EndUpdate();

			// The growth of the batched class is still compensated.
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 8));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(f);
		}

		[Fact]
		public void NewClassStaysIncompleteWhileItIsFilledInABatch()
		{
			var inner = ClassNode.Create();
			var instance = InstanceOf(inner).Named("instance");
			var f = new FloatNode().Named("f");
			var outer = CreateClass(instance, f);

			inner.BeginUpdate();
			inner.AddBytes(4);

			// A layout inside the batch (project handler) must not publish the half filled class.
			inner.UpdateOffsets();
			outer.UpdateOffsets();

			Check.That(inner.IsLayoutComplete).IsFalse();
			Check.That(instance.LayoutSize).IsEqualTo(-1);

			inner.AddBytes(4);
			inner.EndUpdate();

			Check.That(inner.IsLayoutComplete).IsTrue();
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 8));
			Check.That(instance.LayoutSize).IsEqualTo(8);
		}

		[Fact]
		public void ChildNotificationInsideABatchCompensatesImmediately()
		{
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, new Hex32Node().Named("b"), c);

			classNode.BeginUpdate();

			// A hot spot edit notifies the class directly, the class compensates at once and stays complete.
			text.Update(new HotSpot { Id = 0, Text = "8", Node = text });

			Check.That(classNode.Nodes.Count).IsEqualTo(2);
			Check.That(c.Offset).IsEqualTo(8);
			Check.That(classNode.IsLayoutComplete).IsTrue();

			classNode.EndUpdate();

			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
		}

		private sealed class CountingUnion : UnionNode
		{
			public int Layouts { get; set; }

			public override void UpdateOffsets()
			{
				Layouts++;

				base.UpdateOffsets();
			}
		}

		[Fact]
		public void NestedContainersAreSettledOncePerLayoutPass()
		{
			var union = new CountingUnion();
			union.AddNode(new Hex32Node());

			// The same union is referenced twice by the class.
			var outer = CreateClass(ArrayOf(union, 2).Named("first"), ArrayOf(union, 3).Named("second"));

			union.Layouts = 0;
			outer.UpdateOffsets();

			Check.That(union.Layouts).IsEqualTo(1);
			CheckLayout(outer, (typeof(ArrayNode), 0), (typeof(ArrayNode), 8));
			Check.That(outer.MemorySize).IsEqualTo(20);

			// A pass over several classes settles the shared container once too.
			var other = CreateClass(ArrayOf(union, 1).Named("third"), new FloatNode().Named("f"));

			union.Layouts = 0;
			BaseContainerNode.UpdateOffsets(new BaseContainerNode[] { outer, other });

			Check.That(union.Layouts).IsEqualTo(1);
			Check.That(other.Nodes[1].Offset).IsEqualTo(4);
		}

		[Fact]
		public void LayoutPassOverSeveralContainersCompensatesEveryContainer()
		{
			var text1 = Text8(4).Named("text1");
			var c1 = new Hex32Node().Named("c1");
			var class1 = CreateClass(text1, new Hex32Node().Named("b1"), c1);

			var text2 = Text8(4).Named("text2");
			var c2 = new Hex32Node().Named("c2");
			var class2 = CreateClass(text2, new Hex32Node().Named("b2"), c2);

			text1.Length = 8;
			text2.Length = 8;

			BaseContainerNode.UpdateOffsets(new BaseContainerNode[] { class1, class2 });

			CheckLayout(class1, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(class1.Nodes[1]).IsSameReferenceAs(c1);
			CheckLayout(class2, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(class2.Nodes[1]).IsSameReferenceAs(c2);
		}

		[Fact]
		public void ReferencedClassIsSettledBeforeTheReferencingClassInAPass()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var o = new Hex32Node().Named("o");
			var outer = CreateClass(InstanceOf(inner), new Hex32Node().Named("pad"), o);

			// Redirect the notifications of the inner class so the outer class is not updated automatically.
			InstanceOf(inner);

			inner.ReplaceChildNode(x, new Hex64Node());

			// The outer class comes first in the pass but the inner class is settled before it is used.
			BaseContainerNode.UpdateOffsets(new BaseContainerNode[] { outer, inner });

			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 8));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(o);
		}

		[Fact]
		public void ReferencedClassGrowthConsumesSuccessorsOfTheInstance()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var instance = InstanceOf(inner).Named("instance");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var e = new Hex32Node().Named("e");
			var outer = CreateClass(new Hex32Node().Named("a"), instance, c, d, e);

			Check.That(outer.MemorySize).IsEqualTo(20);

			// The inner class grows by 4 bytes (x is the last node of the class).
			inner.ReplaceChildNode(x, new Hex64Node());

			Check.That(inner.MemorySize).IsEqualTo(8);
			Check.That(instance.MemorySize).IsEqualTo(8);

			// The notification travels through the instance node to the outer class.
			CheckLayout(outer, (typeof(Hex32Node), 0), (typeof(ClassInstanceNode), 4), (typeof(Hex32Node), 12), (typeof(Hex32Node), 16));
			Check.That(outer.Nodes[2]).IsSameReferenceAs(d);
			Check.That(outer.Nodes[3]).IsSameReferenceAs(e);
			Check.That(outer.MemorySize).IsEqualTo(20);
			CheckDetached(outer, c);
		}

		[Fact]
		public void ReferencedClassShrinkPadsAfterTheInstance()
		{
			var x = new Hex32Node().Named("x");
			var y = new Hex32Node().Named("y");
			var inner = CreateClass(x, y);

			var instance = InstanceOf(inner).Named("instance");
			var c = new Hex32Node().Named("c");
			var outer = CreateClass(instance, c);

			Check.That(c.Offset).IsEqualTo(8);

			// Deleting a node shrinks the inner class, the outer class keeps the offset of c.
			inner.RemoveNode(y);

			Check.That(inner.MemorySize).IsEqualTo(4);
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(outer.Nodes[2]).IsSameReferenceAs(c);
			Check.That(outer.MemorySize).IsEqualTo(12);
		}

		[Fact]
		public void ReferencedClassChangeIsVisibleThroughUpdateOffsetsOfEveryReferencingClass()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var c1 = new Hex32Node().Named("c1");
			var outer1 = CreateClass(InstanceOf(inner), new Hex32Node(), c1);

			var c2 = new Hex32Node().Named("c2");
			var outer2 = CreateClass(InstanceOf(inner), new Hex32Node(), c2);

			inner.AddBytes(4);

			// Only the last referencing class gets notified automatically (the class has a single parent),
			// a project calls UpdateOffsets for every class.
			outer1.UpdateOffsets();
			outer2.UpdateOffsets();

			CheckLayout(outer1, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 8));
			Check.That(outer1.Nodes[1]).IsSameReferenceAs(c1);
			CheckLayout(outer2, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 8));
			Check.That(outer2.Nodes[1]).IsSameReferenceAs(c2);
		}

		[Fact]
		public void ArrayOfClassInstancesGrowsWithTheReferencedClass()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var array = ArrayOf(InstanceOf(inner), 2).Named("array"); // 8 bytes
			var tail = new FloatNode().Named("tail");
			var outer = CreateClass(array, new Hex32Node(), new Hex32Node(), tail);

			Check.That(tail.Offset).IsEqualTo(16);

			inner.ReplaceChildNode(x, new Hex64Node()); // array grows to 16 bytes

			outer.UpdateOffsets();

			CheckLayout(outer, (typeof(ArrayNode), 0), (typeof(FloatNode), 16));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(tail);
		}

		[Fact]
		public void NestedClassesSettleBottomUpRegardlessOfTheUpdateOrder()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var m = new Hex32Node().Named("m");
			var n = new Hex32Node().Named("n");
			var mid = CreateClass(InstanceOf(inner), m, n); // 12 bytes

			var o = new Hex32Node().Named("o");
			var outer = CreateClass(InstanceOf(mid), o); // 16 bytes

			// Redirect the notifications of the inner class to a detached instance so the middle class is not updated automatically.
			InstanceOf(inner);

			inner.ReplaceChildNode(x, new Hex64Node());

			Check.That(mid.Nodes.Count).IsEqualTo(3);
			Check.That(mid.MemorySize).IsEqualTo(16); // not settled yet

			// The outer class must settle the middle class first, otherwise it would see a transient growth.
			outer.UpdateOffsets();

			CheckLayout(mid, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 8));
			Check.That(mid.Nodes[1]).IsSameReferenceAs(n);
			Check.That(mid.MemorySize).IsEqualTo(12);

			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 12));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(o);
			Check.That(outer.MemorySize).IsEqualTo(16);
		}

		[Fact]
		public void InstanceOfAnEmptyClassIsNotCompensatedWhenTheClassGetsFilled()
		{
			// Simulates project loading: the outer class is laid out before the referenced class has nodes.
			var inner = ClassNode.Create();
			var b = new Hex32Node().Named("b");
			var c = new Hex32Node().Named("c");
			var outer = CreateClass(new Hex32Node().Named("a"), InstanceOf(inner), b, c);

			Check.That(inner.IsLayoutComplete).IsFalse();
			Check.That(outer.IsLayoutComplete).IsFalse();
			Check.That(outer.Nodes[1].LayoutSize).IsEqualTo(-1);
			Check.That(b.Offset).IsEqualTo(4);

			inner.BeginUpdate();
			inner.AddNodes(new BaseNode[] { new Hex32Node(), new Hex32Node() });
			inner.EndUpdate();

			Check.That(inner.IsLayoutComplete).IsTrue();
			Check.That(outer.IsLayoutComplete).IsTrue();

			// Filling the class is intentional, b and c move down instead of being consumed.
			CheckLayout(outer, (typeof(Hex32Node), 0), (typeof(ClassInstanceNode), 4), (typeof(Hex32Node), 12), (typeof(Hex32Node), 16));
			Check.That(outer.Nodes[2]).IsSameReferenceAs(b);
			Check.That(outer.Nodes[3]).IsSameReferenceAs(c);

			// From now on the instance is tracked: the next growth consumes b and c keeps its offset.
			inner.AddBytes(4);

			CheckLayout(outer, (typeof(Hex32Node), 0), (typeof(ClassInstanceNode), 4), (typeof(Hex32Node), 16));
			Check.That(outer.Nodes[2]).IsSameReferenceAs(c);
			Check.That(outer.MemorySize).IsEqualTo(20);
			CheckDetached(outer, b);
		}

		[Fact]
		public void BatchedSizeChangesAreReconciledOnEndUpdate()
		{
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, new Hex32Node().Named("b"), c);

			classNode.BeginUpdate();
			text.Length = 8;

			Check.That(classNode.Nodes.Count).IsEqualTo(3);

			classNode.EndUpdate();

			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
		}

		[Fact]
		public void ConsumedSuccessorWithPendingSizeChangeCountsWithItsOldSize()
		{
			var text = Text8(4).Named("text");
			var other = Text8(8).Named("other");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(text, other, c);

			classNode.BeginUpdate();
			other.Length = 4; // pending shrink
			text.Length = 8; // pending growth
			classNode.EndUpdate();

			// text consumes 4 of the 8 bytes "other" occupied, the remaining 4 bytes are padded and c keeps its offset.
			CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8), (typeof(Hex32Node), 12));
			Check.That(classNode.Nodes[0]).IsSameReferenceAs(text);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(c);
			CheckDetached(classNode, other);
		}

		[Fact]
		public void UnionKeepsAllMembersAtOffsetZero()
		{
			var union = UnionOf(new Hex32Node(), new Vector3Node(), Text8(3));

			union.UpdateOffsets();

			Check.That(union.Nodes.Select(n => n.Offset)).ContainsExactly(0, 0, 0);
			Check.That(union.MemorySize).IsEqualTo(12);
			Check.That(union.IsLayoutComplete).IsTrue();
		}

		[Fact]
		public void EmptyUnionHasNoSize()
		{
			Check.That(new UnionNode().MemorySize).IsEqualTo(0);
		}

		[Fact]
		public void LayoutSizeTracksTheLastLayout()
		{
			var text = Text8(4);
			var classNode = CreateClass(text, new Hex32Node());

			Check.That(text.LayoutSize).IsEqualTo(4);
			Check.That(new Hex32Node().LayoutSize).IsEqualTo(-1);

			text.Length = 6;

			Check.That(text.LayoutSize).IsEqualTo(4);

			classNode.UpdateOffsets();

			Check.That(text.LayoutSize).IsEqualTo(6);
		}

		[Fact]
		public void VirtualMethodTableKeepsItsSize()
		{
			var vtable = new VirtualMethodTableNode();
			vtable.Initialize();
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(vtable, c);

			vtable.RemoveNode(vtable.Nodes[0]);
			classNode.UpdateOffsets();

			Check.That(classNode.Nodes.Count).IsEqualTo(2);
			Check.That(c.Offset).IsEqualTo(IntPtr.Size);
			Check.That(classNode.IsLayoutComplete).IsTrue();
		}
	}
}
