using System.Collections.Generic;
using NFluent;
using ReClassNET.Nodes;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// With <see cref="ContainerLayoutPolicy.PreserveSuccessorOffsets"/> disabled the containers behave like upstream ReClass.NET:
	/// a growing node shifts its successors and a shrinking node gets padded.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ContainerLayoutLegacyTest
	{
		[Fact]
		public void GrowthShiftsTheSuccessors()
		{
			using (new LayoutPolicyScope(false))
			{
				var a = new Hex32Node().Named("a");
				var b = new Hex32Node().Named("b");
				var d = new FloatNode().Named("d");
				var classNode = CreateClass(a, b, d);

				classNode.ReplaceChildNode(a, new Vector3Node());

				CheckLayout(classNode, (typeof(Vector3Node), 0), (typeof(Hex32Node), 12), (typeof(FloatNode), 16));
				Check.That(classNode.Nodes[1]).IsSameReferenceAs(b);
				Check.That(classNode.Nodes[2]).IsSameReferenceAs(d);
				Check.That(classNode.MemorySize).IsEqualTo(20);
			}
		}

		[Fact]
		public void ShrinkPads()
		{
			using (new LayoutPolicyScope(false))
			{
				var a = new Hex64Node().Named("a");
				var d = new FloatNode().Named("d");
				var classNode = CreateClass(a, d);

				var createdNodes = new List<BaseNode>();
				classNode.ReplaceChildNode(a, new Hex32Node(), ref createdNodes);

				CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(FloatNode), 8));
				Check.That(createdNodes).HasSize(1);
				Check.That(classNode.Nodes[1]).IsSameReferenceAs(createdNodes[0]);
			}
		}

		[Fact]
		public void TextLengthChangeShiftsTheSuccessors()
		{
			using (new LayoutPolicyScope(false))
			{
				var text = Text8(4).Named("text");
				var b = new Hex32Node().Named("b");
				var classNode = CreateClass(text, b);

				text.Length = 8;
				classNode.UpdateOffsets();

				CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 8));
				Check.That(classNode.Nodes[1]).IsSameReferenceAs(b);

				text.Length = 2;
				classNode.UpdateOffsets();

				CheckLayout(classNode, (typeof(Utf8TextNode), 0), (typeof(Hex32Node), 2));
			}
		}

		[Fact]
		public void ReferencedClassGrowthShiftsTheSuccessors()
		{
			using (new LayoutPolicyScope(false))
			{
				var x = new Hex32Node().Named("x");
				var inner = CreateClass(x);
				var c = new Hex32Node().Named("c");
				var outer = CreateClass(InstanceOf(inner), c);

				inner.ReplaceChildNode(x, new Hex64Node());

				CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(Hex32Node), 8));
				Check.That(outer.Nodes[1]).IsSameReferenceAs(c);
			}
		}

		[Fact]
		public void DryRunReportsNothing()
		{
			using (new LayoutPolicyScope(false))
			{
				var a = new Hex32Node().Named("a");
				var classNode = CreateClass(a, new FloatNode().Named("b"));

				Check.That(classNode.PreservesSuccessorOffsets).IsFalse();
				Check.That(classNode.GetNodesConsumedByResize(a, 12)).IsEmpty();
			}
		}

		[Fact]
		public void RangeReplacementReplacesEveryNodeAndItsPadding()
		{
			using (new LayoutPolicyScope(false))
			{
				var a = new Hex64Node().Named("a");
				var b = new Hex64Node().Named("b");
				var tail = new FloatNode().Named("tail");
				var classNode = CreateClass(a, b, tail);

				var placed = classNode.ReplaceNodeRange(new[] { a, b }, () => new Int32Node());

				Check.That(placed).HasSize(4);
				CheckLayout(classNode, (typeof(Int32Node), 0), (typeof(Int32Node), 4), (typeof(Int32Node), 8), (typeof(Int32Node), 12), (typeof(FloatNode), 16));
			}
		}

		[Fact]
		public void PolicyIsRestoredAfterTheScope()
		{
			var before = ContainerLayoutPolicy.PreserveSuccessorOffsets;

			using (new LayoutPolicyScope(!before))
			{
				Check.That(ContainerLayoutPolicy.PreserveSuccessorOffsets).IsEqualTo(!before);
			}

			Check.That(ContainerLayoutPolicy.PreserveSuccessorOffsets).IsEqualTo(before);
		}

		[Fact]
		public void TogglingThePolicyAtRuntimeUsesTheTrackedSizes()
		{
			var a = new Hex32Node().Named("a");
			var text = Text8(4).Named("text");
			var c = new Hex32Node().Named("c");
			var classNode = CreateClass(a, text, new Hex32Node().Named("b"), c);

			using (new LayoutPolicyScope(false))
			{
				text.Length = 6;
				classNode.UpdateOffsets();

				Check.That(c.Offset).IsEqualTo(14);
			}

			using (new LayoutPolicyScope(true))
			{
				// The layout was tracked in legacy mode too, so the next change is compensated against the last layout.
				text.Length = 8;
				classNode.UpdateOffsets();

				CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Utf8TextNode), 4), (typeof(Hex16Node), 12), (typeof(Hex32Node), 14));
				Check.That(classNode.Nodes[3]).IsSameReferenceAs(c);
			}
		}
	}
}
