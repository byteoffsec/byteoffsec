using System;
using Moq;
using NFluent;
using ReClassNET.Memory;
using ReClassNET.Nodes;
using ReClassNET.Util.Conversion;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// The dissector replaces hex nodes with guessed types. A guessed type which is bigger than the hex node consumes
	/// the following nodes, which may be part of the dissected selection.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class NodeDissectorTest
	{
		private static readonly IntPtr DataAddress = (IntPtr)0x1000;

		public NodeDissectorTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		private static MemoryBuffer CreateMemory(int size, params byte[] data)
		{
			var buffer = new MemoryBuffer
			{
				Size = size
			};
			Array.Copy(data, buffer.RawData, data.Length);
			return buffer;
		}

		/// <summary>A process where <see cref="DataAddress"/> points into a data section which holds zeros.</summary>
		private static IProcessReader CreateReader()
		{
			var reader = new Mock<IProcessReader>();
			reader.SetupGet(r => r.BitConverter).Returns(EndianBitConverter.System);
			reader.Setup(r => r.GetSectionToPointer(DataAddress)).Returns(new Section { Category = SectionCategory.DATA });
			reader.Setup(r => r.ReadRemoteMemory(It.IsAny<IntPtr>(), It.IsAny<int>())).Returns((IntPtr address, int size) => new byte[size]);
			return reader.Object;
		}

		private static BaseHexNode CreateHalfPointerSizedNode()
		{
			return IntPtr.Size == 8 ? (BaseHexNode)new Hex32Node() : new Hex16Node();
		}

		[Fact]
		public void NodesConsumedByAnEarlierGuessAreSkipped()
		{
			// Three hex nodes of half the pointer size. The first one holds a pointer into a data section,
			// the guessed pointer node is a full pointer in size and consumes the second node.
			var a = CreateHalfPointerSizedNode().Named("a");
			var b = CreateHalfPointerSizedNode().Named("b");
			var c = CreateHalfPointerSizedNode().Named("c");
			var classNode = CreateClass(a, b, c);

			var halfSize = a.MemorySize;
			var memory = CreateMemory(3 * halfSize, 0x00, 0x10, 0x00, 0x00);

			Check.ThatCode(() => NodeDissector.DissectNodes(new[] { a, b, c }, CreateReader(), memory)).DoesNotThrow();

			Check.That(classNode.Nodes.Count).IsEqualTo(2);
			Check.That(classNode.Nodes[0]).IsInstanceOf<PointerNode>();
			Check.That(classNode.Nodes[0].Offset).IsEqualTo(0);
			Check.That(classNode.Nodes[0].Name).IsEqualTo("a");
			Check.That(classNode.Nodes[1]).IsSameReferenceAs(c);
			Check.That(c.Offset).IsEqualTo(IntPtr.Size);
			Check.That(classNode.MemorySize).IsEqualTo(IntPtr.Size + halfSize);

			CheckDetached(classNode, a);
			CheckDetached(classNode, b);
		}

		[Fact]
		public void NodesWithoutAContainerAreSkipped()
		{
			var orphan = new Hex32Node();

			Check.ThatCode(() => NodeDissector.DissectNodes(new[] { orphan }, CreateReader(), CreateMemory(4))).DoesNotThrow();
		}
	}
}
