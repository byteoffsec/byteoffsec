using System;
using System.IO;
using System.Linq;
using NFluent;
using ReClassNET.DataExchange.ReClass;
using ReClassNET.Logger;
using ReClassNET.Nodes;
using ReClassNET.Project;
using Xunit;
using static ReClass.NET_Tests.Nodes.LayoutTestUtil;

namespace ReClass.NET_Tests.Nodes
{
	/// <summary>
	/// Loading a project must reproduce the saved layout byte for byte, no matter in which order the classes are stored.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class ProjectRoundTripTest
	{
		public ProjectRoundTripTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		private static ReClassNetProject CreateProject(out ClassNode outer, out ClassNode inner)
		{
			var project = new ReClassNetProject();

			var innermost = CreateClass(new Hex32Node().Named("i1"), new FloatNode().Named("i2"));
			innermost.Name = "Innermost";

			inner = CreateClass(new Hex32Node().Named("x"), InstanceOf(innermost).Named("deep"), new Hex32Node().Named("y"));
			inner.Name = "Inner";

			var pointer = new PointerNode();
			pointer.ChangeInnerNode(null);

			outer = CreateClass(
				new Hex32Node().Named("a"),
				InstanceOf(inner).Named("instance"),
				Text8(6).Named("text"),
				new Hex16Node().Named("b"),
				ArrayOf(new Hex32Node(), 3).Named("array"),
				ArrayOf(InstanceOf(inner), 2).Named("instances"),
				pointer.Named("pointer"),
				UnionOf(new Hex32Node().Named("u1"), new Vector3Node().Named("u2")).Named("union"),
				new BitFieldNode { Bits = 16 }.Named("bits"),
				new FloatNode().Named("tail")
			);
			outer.Name = "Outer";

			// The outer class is stored first, so the loader fills it before the classes it references have any nodes.
			project.AddClass(outer);
			project.AddClass(inner);
			project.AddClass(innermost);

			return project;
		}

		private static ReClassNetProject SaveAndLoad(ReClassNetProject project)
		{
			var logger = new NullLogger();

			// Save closes the stream together with the zip archive, the buffer is still readable.
			using var input = new MemoryStream(Save(project));

			var loaded = new ReClassNetProject();
			new ReClassNetFile(loaded).Load(input, logger);

			return loaded;
		}

		private static byte[] Save(ReClassNetProject project)
		{
			var stream = new MemoryStream();

			new ReClassNetFile(project).Save(stream, new NullLogger());

			return stream.ToArray();
		}

		private static void CheckSameLayout(ClassNode expected, ClassNode actual)
		{
			Check.That(actual.Name).IsEqualTo(expected.Name);
			Check.That(actual.MemorySize).IsEqualTo(expected.MemorySize);
			Check.That(actual.Nodes.Select(n => n.GetType())).ContainsExactly(expected.Nodes.Select(n => n.GetType()));
			Check.That(actual.Nodes.Select(n => n.Name)).ContainsExactly(expected.Nodes.Select(n => n.Name));
			Check.That(actual.Nodes.Select(n => n.Offset)).ContainsExactly(expected.Nodes.Select(n => n.Offset));
			Check.That(actual.Nodes.Select(n => n.MemorySize)).ContainsExactly(expected.Nodes.Select(n => n.MemorySize));
		}

		[Fact]
		public void LoadedProjectHasTheSavedLayout()
		{
			using var project = CreateProject(out var outer, out var inner);

			var expectedOffsets = outer.Nodes.Select(n => n.Offset).ToList();
			Check.That(outer.Nodes.Single(n => n.Name == "tail").Offset).IsEqualTo(outer.MemorySize - 4);

			using var loaded = SaveAndLoad(project);

			Check.That(loaded.Classes).HasSize(3);

			foreach (var classNode in project.Classes)
			{
				CheckSameLayout(classNode, loaded.GetClassByUuid(classNode.Uuid));
			}

			var loadedOuter = loaded.GetClassByUuid(outer.Uuid);
			Check.That(loadedOuter.Nodes.Select(n => n.Offset)).ContainsExactly(expectedOffsets);

			Check.That(loaded.Classes.All(c => c.IsLayoutComplete)).IsTrue();
			Check.That(loadedOuter.Nodes.All(n => n.LayoutSize == n.MemorySize)).IsTrue();
		}

		[Fact]
		public void LoadedProjectSurvivesASecondRoundTrip()
		{
			using var project = CreateProject(out var outer, out _);
			using var loaded = SaveAndLoad(project);
			using var loadedAgain = SaveAndLoad(loaded);

			foreach (var classNode in project.Classes)
			{
				CheckSameLayout(classNode, loadedAgain.GetClassByUuid(classNode.Uuid));
			}
		}

		[Fact]
		public void LoadedClassesCompensateTypeChanges()
		{
			using var project = CreateProject(out var outer, out _);
			using var loaded = SaveAndLoad(project);

			var loadedOuter = loaded.GetClassByUuid(outer.Uuid);
			var a = loadedOuter.Nodes[0];
			var instance = loadedOuter.Nodes[1];
			var text = loadedOuter.Nodes[2];
			var size = loadedOuter.MemorySize;

			loadedOuter.ReplaceChildNode(a, new Hex64Node());

			// The instance (16 bytes) is partially consumed and the remaining 12 bytes are padded, the text keeps its offset.
			Check.That(loadedOuter.Nodes[0]).IsInstanceOf<Hex64Node>();
			Check.That(loadedOuter.ContainsNode(instance)).IsFalse();
			Check.That(loadedOuter.ContainsNode(text)).IsTrue();
			Check.That(text.Offset).IsEqualTo(20);
			Check.That(loadedOuter.Nodes.Take(loadedOuter.FindNodeIndex(text)).Sum(n => n.MemorySize)).IsEqualTo(20);
			Check.That(loadedOuter.Nodes.Skip(1).Take(loadedOuter.FindNodeIndex(text) - 1).All(n => n is BaseHexNode)).IsTrue();
			Check.That(loadedOuter.MemorySize).IsEqualTo(size);
		}

		[Fact]
		public void ReferencedClassChangeAfterLoadingIsCompensatedInTheProject()
		{
			using var project = CreateProject(out var outer, out var inner);
			using var loaded = SaveAndLoad(project);

			var loadedOuter = loaded.GetClassByUuid(outer.Uuid);
			var loadedInner = loaded.GetClassByUuid(inner.Uuid);

			var text = loadedOuter.Nodes.Single(n => n.Name == "text");
			var textOffset = text.Offset;
			var tail = loadedOuter.Nodes.Single(n => n.Name == "tail");
			var tailOffset = tail.Offset;
			var size = loadedOuter.MemorySize;

			// Grow the inner class by 4 bytes: the single instance consumes the text,
			// the array of two instances grows by 8 bytes and consumes the pointer.
			loadedInner.AddBytes(4);

			Check.That(loadedOuter.ContainsNode(text)).IsFalse();
			Check.That(loadedOuter.ContainsNode(tail)).IsTrue();
			Check.That(tail.Offset).IsEqualTo(tailOffset);
			Check.That(loadedOuter.MemorySize).IsEqualTo(size);
			Check.That(loadedOuter.Nodes.Single(n => n.Name == "b").Offset).IsEqualTo(textOffset + 6);
		}

		[Fact]
		public void SavingKeepsTheFileFormat()
		{
			using var project = CreateProject(out _, out _);

			using var stream = new MemoryStream(Save(project));

			using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
			var entry = archive.GetEntry("Data.xml");
			Check.That(entry).IsNotNull();

			using var reader = new StreamReader(entry.Open());
			var xml = reader.ReadToEnd();

			Check.That(xml).Contains("<node type=\"Hex32Node\" name=\"a\"");
			Check.That(xml).Not.Contains("LayoutSize");
		}
	}
}
