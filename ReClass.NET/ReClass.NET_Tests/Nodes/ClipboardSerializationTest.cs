using System;
using System.IO;
using System.Linq;
using NFluent;
using ReClassNET.DataExchange.ReClass;
using ReClassNET.Logger;
using ReClassNET.Nodes;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Copying nodes to the clipboard serializes them through a temporary class. The real nodes must not be affected by it.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ClipboardSerializationTest
	{
		public ClipboardSerializationTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		private static byte[] Serialize(params BaseNode[] nodes)
		{
			var stream = new MemoryStream();

			ReClassNetFile.SerializeNodesToStream(stream, nodes, new NullLogger());

			return stream.ToArray();
		}

		[Fact]
		public void CopyingNodesLeavesTheSourceClassUntouched()
		{
			var a = new Hex32Node().Named("a");
			var b = new Hex32Node().Named("b");
			var c = new FloatNode().Named("c");
			var classNode = CreateClass(a, b, c);

			Serialize(b, c);

			Check.That(b.ParentNode).IsSameReferenceAs(classNode);
			Check.That(c.ParentNode).IsSameReferenceAs(classNode);
			Check.That(b.LayoutSize).IsEqualTo(4);
			Check.That(c.LayoutSize).IsEqualTo(4);
			Check.That(classNode.ContainsNode(b)).IsTrue();
			Check.That(classNode.ContainsNode(c)).IsTrue();
			Check.That(b.GetParentContainer()).IsSameReferenceAs(classNode);

			// The offsets are restored too, the temporary class laid the nodes out at 0 and 4.
			CheckLayout(classNode, (typeof(Hex32Node), 0), (typeof(Hex32Node), 4), (typeof(FloatNode), 8));
			Check.That(classNode.IsLayoutComplete).IsTrue();
		}

		[Fact]
		public void CopiedNodesRoundTrip()
		{
			var b = new Hex32Node().Named("b");
			var c = new FloatNode().Named("c");
			CreateClass(new Hex32Node().Named("a"), b, c);

			using var input = new MemoryStream(Serialize(b, c));

			var (classes, nodes) = ReClassNetFile.DeserializeNodesFromStream(input, null, new NullLogger());

			Check.That(classes).IsEmpty();
			Check.That(nodes.Select(n => n.GetType())).ContainsExactly(typeof(Hex32Node), typeof(FloatNode));
			Check.That(nodes.Select(n => n.Name)).ContainsExactly("b", "c");
			Check.That(nodes.Select(n => n.Offset)).ContainsExactly(0, 4);
		}

		[Fact]
		public void CopyingAnInstanceKeepsTheReferencedClassIntact()
		{
			var x = new Hex32Node().Named("x");
			var inner = CreateClass(x);

			var instance = InstanceOf(inner).Named("instance");
			var f = new FloatNode().Named("f");
			var outer = CreateClass(instance, f);

			using var input = new MemoryStream(Serialize(instance));

			Check.That(instance.ParentNode).IsSameReferenceAs(outer);
			Check.That(instance.LayoutSize).IsEqualTo(4);
			Check.That(inner.Nodes).ContainsExactly(x);
			Check.That(inner.IsLayoutComplete).IsTrue();
			CheckLayout(outer, (typeof(ClassInstanceNode), 0), (typeof(FloatNode), 4));

			// The referenced class travels with the node.
			var (classes, nodes) = ReClassNetFile.DeserializeNodesFromStream(input, null, new NullLogger());

			Check.That(classes.Select(c => c.Uuid)).ContainsExactly(inner.Uuid);
			Check.That(nodes).HasSize(1);
			Check.That(nodes[0]).IsInstanceOf<ClassInstanceNode>();
			Check.That(nodes[0].MemorySize).IsEqualTo(4);

			// A size change of the inner class is still compensated in the source class.
			inner.AddBytes(4);

			Check.That(outer.Nodes).ContainsExactly(instance);
			Check.That(outer.MemorySize).IsEqualTo(8);
			CheckDetached(outer, f);
		}
	}
}
