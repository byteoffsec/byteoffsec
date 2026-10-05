using System;
using System.Collections.Generic;
using System.Linq;
using NFluent;
using ReClassNET.Nodes;
using Xunit;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Every test which depends on <see cref="ContainerLayoutPolicy.PreserveSuccessorOffsets"/> belongs to this collection
	/// so the tests never run in parallel (xunit runs test classes of different collections in parallel).
	/// </summary>
	[CollectionDefinition(Name)]
	public class ContainerLayoutCollection
	{
		public const string Name = "ContainerLayoutPolicy";
	}

	/// <summary>
	/// Sets <see cref="ContainerLayoutPolicy.PreserveSuccessorOffsets"/> for the lifetime of the scope and restores the old value afterwards.
	/// </summary>
	internal sealed class LayoutPolicyScope : IDisposable
	{
		private readonly bool previous;

		public LayoutPolicyScope(bool preserveSuccessorOffsets)
		{
			previous = ContainerLayoutPolicy.PreserveSuccessorOffsets;

			ContainerLayoutPolicy.PreserveSuccessorOffsets = preserveSuccessorOffsets;
		}

		public void Dispose()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = previous;
		}
	}

	internal static class LayoutTestUtil
	{
		/// <summary>Creates a class with the given nodes and lays it out.</summary>
		public static ClassNode CreateClass(params BaseNode[] nodes)
		{
			var classNode = ClassNode.Create();
			classNode.BeginUpdate();
			classNode.AddNodes(nodes);
			classNode.EndUpdate();
			return classNode;
		}

		public static T Named<T>(this T node, string name) where T : BaseNode
		{
			node.Name = name;
			return node;
		}

		public static Utf8TextNode Text8(int length)
		{
			return new Utf8TextNode { Length = length };
		}

		public static ClassInstanceNode InstanceOf(ClassNode classNode)
		{
			var node = new ClassInstanceNode();
			node.ChangeInnerNode(classNode);
			return node;
		}

		public static ArrayNode ArrayOf(BaseNode inner, int count)
		{
			var node = new ArrayNode();
			node.ChangeInnerNode(inner);
			node.Count = count;
			return node;
		}

		public static UnionNode UnionOf(params BaseNode[] nodes)
		{
			var node = new UnionNode();
			node.AddNodes(nodes);
			return node;
		}

		/// <summary>Checks the node types and offsets of the container.</summary>
		public static void CheckLayout(BaseContainerNode container, params (Type Type, int Offset)[] expected)
		{
			Check.That(container.Nodes.Select(n => n.GetType())).ContainsExactly(expected.Select(e => e.Type));
			Check.That(container.Nodes.Select(n => n.Offset)).ContainsExactly(expected.Select(e => e.Offset));
		}

		/// <summary>Checks that the node is no longer part of the container.</summary>
		public static void CheckDetached(BaseContainerNode container, BaseNode node)
		{
			Check.That(container.ContainsNode(node)).IsFalse();
			Check.That(node.ParentNode).IsNull();
			Check.That(node.IsSelected).IsFalse();
		}

		public static IEnumerable<string> Names(this BaseContainerNode container)
		{
			return container.Nodes.Select(n => n.Name);
		}
	}
}
