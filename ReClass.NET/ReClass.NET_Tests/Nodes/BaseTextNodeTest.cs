using NFluent;
using ReClassNET.Nodes;
using Xunit;

namespace ReClass.NET_Tests.Nodes
{
	public class BaseTextNodeTest
	{
		[Fact]
		public void CopyFromNodeKeepsNameCommentAndOffset()
		{
			var source = new Hex32Node
			{
				Name = "health",
				Comment = "the comment",
				Offset = 8
			};

			var text = new Utf8TextNode();
			text.CopyFromNode(source);

			Check.That(text.Name).IsEqualTo("health");
			Check.That(text.Comment).IsEqualTo("the comment");
			Check.That(text.Offset).IsEqualTo(8);
			Check.That(text.Length).IsEqualTo(4);
			Check.That(text.MemorySize).IsEqualTo(4);
		}

		[Fact]
		public void CopyFromNodeAdoptsTheSizeInCharacters()
		{
			var source = new Hex64Node();

			var utf16 = new Utf16TextNode();
			utf16.CopyFromNode(source);

			Check.That(utf16.Length).IsEqualTo(4);
			Check.That(utf16.MemorySize).IsEqualTo(8);

			var utf32 = new Utf32TextNode();
			utf32.CopyFromNode(source);

			Check.That(utf32.Length).IsEqualTo(2);
			Check.That(utf32.MemorySize).IsEqualTo(8);
		}

		[Fact]
		public void CopyFromNodeNeverCreatesAnEmptyText()
		{
			var utf16 = new Utf16TextNode();
			utf16.CopyFromNode(new Hex8Node());

			Check.That(utf16.Length).IsEqualTo(1);
			Check.That(utf16.MemorySize).IsEqualTo(2);
		}
	}
}
