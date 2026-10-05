using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using NFluent;
using ReClassNET.Controls;
using ReClassNET.Nodes;
using ReClassNET.Project;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// A node whose size is determined by the tool at runtime, like a <see cref="FunctionNode"/> which gets its size
	/// from the disassembler while it is drawn. Such size changes are not user actions and must never consume successors.
	/// </summary>
	internal sealed class RuntimeSizedNode : BaseNode
	{
		// BaseContainerNode.ChildHasChanged is internal to the main assembly, the real nodes call it directly.
		private static readonly MethodInfo ChildHasChangedMethod = typeof(BaseContainerNode).GetMethod("ChildHasChanged", BindingFlags.Instance | BindingFlags.NonPublic);

		private int size;

		public RuntimeSizedNode()
			: this(IntPtr.Size)
		{

		}

		public RuntimeSizedNode(int initialSize)
		{
			size = initialSize;
		}

		public override int MemorySize => size;

		protected override bool ParticipatesInSizeCompensation => false;

		/// <summary>Simulates the tool discovering the real size of the node (see FunctionNode.DisassembleRemoteCode).</summary>
		public void DiscoverSize(int newSize)
		{
			size = newSize;

			var container = GetParentContainer();
			if (container != null)
			{
				ChildHasChangedMethod.Invoke(container, new object[] { this });
			}
		}

		public override void GetUserInterfaceInfo(out string name, out Image icon)
		{
			name = "Runtime sized";
			icon = null;
		}

		public override Size Draw(DrawContext context, int x, int y)
		{
			throw new NotSupportedException();
		}

		public override int CalculateDrawnHeight(DrawContext context)
		{
			return 0;
		}
	}

	[Collection(ContainerLayoutCollection.Name)]
	public class RuntimeSizedNodeTest
	{
		private sealed class FunctionProbe : FunctionNode
		{
			public bool Participates => ParticipatesInSizeCompensation;
		}

		private sealed class FunctionPtrProbe : FunctionPtrNode
		{
			public bool Participates => ParticipatesInSizeCompensation;
		}

		public RuntimeSizedNodeTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		[Fact]
		public void FunctionsDoNotParticipateButFunctionPointersDo()
		{
			Check.That(new FunctionProbe().Participates).IsFalse();
			Check.That(new FunctionPtrProbe().Participates).IsTrue();

			var function = new FunctionNode().Named("f");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(function, c);

			Check.That(function.LayoutSize).IsEqualTo(-1);
			Check.That(c.Offset).IsEqualTo(IntPtr.Size);
			Check.That(c.LayoutSize).IsEqualTo(4);

			var pointer = new FunctionPtrNode().Named("p");
			var tail = new Hex32Node().Named("tail");
			var other = CreateClass(pointer, tail);

			Check.That(pointer.LayoutSize).IsEqualTo(IntPtr.Size);
			Check.That(other.IsLayoutComplete).IsTrue();
		}

		[Fact]
		public void RuntimeSizeIsNeverTracked()
		{
			var f = new RuntimeSizedNode().Named("f");
			var classNode = CreateClass(f, new Hex32Node());

			Check.That(f.LayoutSize).IsEqualTo(-1);

			// The size of the class depends on the runtime, so it is not known either.
			Check.That(classNode.IsLayoutComplete).IsFalse();
		}

		[Fact]
		public void DiscoveredSizeShiftsTheSuccessorsInsteadOfConsumingThem()
		{
			// Class [Function f][Int32 health][Int32 ammo] after loading: f has its placeholder size until it is drawn.
			var f = new RuntimeSizedNode(8).Named("f");
			var health = new Int32Node().Named("health");
			var ammo = new Int32Node().Named("ammo");
			var classNode = CreateClass(f, health, ammo);

			f.DiscoverSize(300);

			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(Int32Node), 300), (typeof(Int32Node), 304));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(health);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(ammo);
			Check.That(classNode.MemorySize).IsEqualTo(308);

			// Attaching to another process gives a different size, nothing gets padded either.
			f.DiscoverSize(8);

			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(Int32Node), 8), (typeof(Int32Node), 12));
			Check.That(classNode.MemorySize).IsEqualTo(16);
		}

		[Fact]
		public void RuntimeSizedNodeInsideAnArrayShiftsInsteadOfConsuming()
		{
			// Class [Array of Function a][Int32 health][Int32 ammo]: the array is as volatile as its inner node.
			var f = new RuntimeSizedNode(8).Named("f");
			var a = ArrayOf(f, 1).Named("a");
			var health = new Int32Node().Named("health");
			var ammo = new Int32Node().Named("ammo");
			var classNode = CreateClass(a, health, ammo);

			f.DiscoverSize(300);

			CheckLayout(classNode, (typeof(ArrayNode), 0), (typeof(Int32Node), 300), (typeof(Int32Node), 304));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(health);
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(ammo);
			Check.That(classNode.MemorySize).IsEqualTo(308);

			// A pointer to a runtime sized node has a fixed size and keeps compensating.
			var p = new PointerNode();
			p.ChangeInnerNode(new RuntimeSizedNode(8));
			var tail = new Int32Node().Named("tail");
			var other = CreateClass(p, new Hex32Node(), new Hex32Node(), tail);
			other.ReplaceChildNode(p, new Hex32Node());

			Check.That(other.Nodes[other.Nodes.Count - 1]).IsSameReferenceAs(tail);
			Check.That(tail.Offset).IsEqualTo(IntPtr.Size + 8);
		}

		[Fact]
		public void DiscoveredSizeOfAnOtherChildIsStillCompensated()
		{
			// The user intent for the other children stays intact.
			var f = new RuntimeSizedNode(8).Named("f");
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var d = new Hex32Node().Named("d");
			var classNode = CreateClass(f, text, c, d);

			f.DiscoverSize(100);
			text.Length = 8;
			classNode.UpdateOffsets();

			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(Utf8TextNode), 100), (typeof(Hex32Node), 108));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(d);
			CheckDetached(classNode, c);
		}

		[Fact]
		public void DiscoveredSizePropagatesThroughInstancesAsAShift()
		{
			// The referencing classes are notified through the project like in the application.
			using var project = new ReClassNetProject();

			var f = new RuntimeSizedNode(8).Named("f");
			var x = new Int32Node().Named("x");
			var code = CreateClass(f, x);

			var instance = InstanceOf(code).Named("instance");
			var y = new FloatNode().Named("y");
			var outer = CreateClass(instance, y);

			project.AddClass(outer);
			project.AddClass(code);

			Check.That(instance.LayoutSize).IsEqualTo(-1);
			Check.That(outer.IsLayoutComplete).IsFalse();
			Check.That(y.Offset).IsEqualTo(12);

			f.DiscoverSize(100);

			CheckLayout(code, (typeof(RuntimeSizedNode), 0), (typeof(Int32Node), 100));
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 104));
			Check.That(outer.Nodes[1]).IsSameReferenceAs(y);
		}

		[Fact]
		public void ReplacingWithARuntimeSizedNodeShiftsOnGrowth()
		{
			var a = new Hex16Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			var createdNodes = new List<BaseNode>();
			var f = new RuntimeSizedNode(8);
			classNode.ReplaceChildNode(a, f, ref createdNodes);

			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(b);
			Check.That(createdNodes).IsEmpty();
			Check.That(f.LayoutSize).IsEqualTo(-1);
			Check.That(f.Name).IsEqualTo("a");
		}

		[Fact]
		public void ReplacingWithARuntimeSizedNodePadsOnShrink()
		{
			var a = new Hex64Node().Named("a");
			var b = new Hex32Node().Named("b");
			var classNode = CreateClass(a, b);

			var createdNodes = new List<BaseNode>();
			classNode.ReplaceChildNode(a, new RuntimeSizedNode(4), ref createdNodes);

			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(Hex32Node), 4), (typeof(Hex32Node), 8));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(b);
			Check.That(createdNodes).HasSize(1);
		}

		[Fact]
		public void ReplacingARuntimeSizedNodeWithADefinedTypeCompensatesItsOccupiedSize()
		{
			var f = new RuntimeSizedNode(300).Named("f");
			var x = new Int32Node().Named("x");
			var classNode = CreateClass(f, x);

			classNode.ReplaceChildNode(f, new Hex32Node());

			Check.That(classNode.Nodes[0]).IsInstanceOf<Hex32Node>();
			Check.That(classNode.Nodes.Last()).IsSameReferenceAs(x);
			Check.That(x.Offset).IsEqualTo(300);
			Check.That(classNode.Nodes.Skip(1).Take(classNode.Nodes.Count - 2).All(n => n is BaseHexNode)).IsTrue();
			Check.That(classNode.MemorySize).IsEqualTo(304);
		}

		[Fact]
		public void DryRunsReportNothingForRuntimeSizedNodes()
		{
			var f = new RuntimeSizedNode(4).Named("f");
			var a = new Hex32Node().Named("a");
			var x = new Int32Node().Named("x");
			var classNode = CreateClass(f, a, x);

			Check.That(classNode.GetNodesConsumedByResize(f, 100)).IsEmpty();
			Check.That(classNode.GetNodesConsumedByResize(a, 100)).ContainsExactly(x);

			var ranges = new[] { (Container: (BaseContainerNode)classNode, Nodes: (IReadOnlyList<BaseNode>)new BaseNode[] { a }) };

			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new RuntimeSizedNode(100))).IsEmpty();
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new FunctionNode())).IsEmpty();
			Check.That(BaseContainerNode.GetNodesConsumedByReplacingRanges(ranges, new Vector3Node())).ContainsExactly(x);
		}

		[Fact]
		public void RangeReplacementWithRuntimeSizedNodesReplacesEveryNode()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var tail = new FloatNode().Named("tail");
			var classNode = CreateClass(a, b, tail);

			var placed = classNode.ReplaceNodeRange(new[] { a, b }, () => new RuntimeSizedNode(8));

			// Legacy behaviour: every node of the range is replaced and the successors shift.
			Check.That(placed).HasSize(2);
			CheckLayout(classNode, (typeof(RuntimeSizedNode), 0), (typeof(RuntimeSizedNode), 8), (typeof(FloatNode), 16));
			Check.That(classNode.Nodes[2]).IsSameReferenceAs(tail);
			Check.That(placed.Select(n => n.Name)).ContainsExactly("a", "b");
		}
	}
}
