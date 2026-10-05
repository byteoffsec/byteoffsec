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
	/// Importers fill classes node by node. A class which is stored after the classes which reference it must not
	/// grow step by step in the referencing classes, otherwise the instances consume the fields behind them.
	/// </summary>
	[Collection(ContainerLayoutCollection.Name)]
	public class LegacyImportTest
	{
		public LegacyImportTest()
		{
			ContainerLayoutPolicy.PreserveSuccessorOffsets = true;
		}

		private static void CheckEntityLayout(ClassNode entity, ClassNode vec3)
		{
			Check.That(vec3.MemorySize).IsEqualTo(12);

			CheckLayout(entity, (typeof(Hex32Node), 0), (typeof(ClassInstanceNode), 4), (typeof(FloatNode), 16));
			Check.That(entity.Names()).ContainsExactly("flags", "pos", "health");
			Check.That(((ClassInstanceNode)entity.Nodes[1]).InnerNode).IsSameReferenceAs(vec3);
			Check.That(entity.MemorySize).IsEqualTo(20);
			Check.That(entity.IsLayoutComplete).IsTrue();
		}

		[Fact]
		public void FillingTheReferencedClassAfterTheReferencingClassInBatchesKeepsTheLayout()
		{
			using var project = new ReClassNetProject();

			var entity = ClassNode.Create();
			var vec3 = ClassNode.Create();
			project.AddClass(entity);
			project.AddClass(vec3);

			// The importer pattern: the referencing class is filled first, one node at a time, inside a batch.
			entity.BeginUpdate();
			entity.AddNode(new Hex32Node().Named("flags"));
			entity.AddNode(InstanceOf(vec3).Named("pos"));
			entity.AddNode(new FloatNode().Named("health"));
			entity.EndUpdate();

			vec3.BeginUpdate();
			vec3.AddNode(new FloatNode().Named("x"));
			vec3.AddNode(new FloatNode().Named("y"));
			vec3.AddNode(new FloatNode().Named("z"));
			vec3.EndUpdate();

			CheckEntityLayout(entity, vec3);
		}

		[Fact]
		public void AddNodesFillsTheReferencedClassInOneBatch()
		{
			using var project = new ReClassNetProject();

			var entity = ClassNode.Create();
			var vec3 = ClassNode.Create();
			project.AddClass(entity);
			project.AddClass(vec3);

			entity.AddNodes(new BaseNode[] { new Hex32Node().Named("flags"), InstanceOf(vec3).Named("pos"), new FloatNode().Named("health") });
			vec3.AddNodes(new BaseNode[] { new FloatNode().Named("x"), new FloatNode().Named("y"), new FloatNode().Named("z") });

			CheckEntityLayout(entity, vec3);
		}

		private static void ImportTempFile(string extension, string content, Action<string, ReClassNetProject> import, Action<ReClassNetProject> check)
		{
			var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
			try
			{
				File.WriteAllText(path, content);

				using var project = new ReClassNetProject();

				import(path, project);

				check(project);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Fact]
		public void ReClassFileImportKeepsTheFieldsBehindAnInstanceOfALaterClass()
		{
			// ReClass 2016 stores the classes in creation order: Entity (with an instance of Vec3) comes first.
			// The instance of Entity inside Entity is a cycle and gets skipped.
			// Type ids: 1 = class instance, 4 = Hex32, 13 = Float.
			const string Content = @"<?xml version=""1.0""?>
<ReClass>
<!--ReClass 2016 by KN4CK3R-->
  <Class Name=""Entity"" strOffset=""400000"">
    <Node Type=""4"" Name=""flags"" Comment="""" bHidden=""0"" />
    <Node Type=""1"" Name=""pos"" Comment="""" bHidden=""0"" Instance=""Vec3"" />
    <Node Type=""1"" Name=""self"" Comment="""" bHidden=""0"" Instance=""Entity"" />
    <Node Type=""13"" Name=""health"" Comment="""" bHidden=""0"" />
  </Class>
  <Class Name=""Vec3"" strOffset=""0"">
    <Node Type=""13"" Name=""x"" Comment="""" bHidden=""0"" />
    <Node Type=""13"" Name=""y"" Comment="""" bHidden=""0"" />
    <Node Type=""13"" Name=""z"" Comment="""" bHidden=""0"" />
  </Class>
</ReClass>";

			ImportTempFile(
				ReClassFile.FileExtension,
				Content,
				(path, project) => new ReClassFile(project).Load(path, new NullLogger()),
				project =>
				{
					Check.That(project.Classes.Select(c => c.Name)).ContainsExactly("Entity", "Vec3");

					CheckEntityLayout(project.Classes[0], project.Classes[1]);
				}
			);
		}

		[Fact]
		public void ReClassQtFileImportKeepsTheFieldsBehindAnInstanceOfALaterClass()
		{
			// The instance of Entity inside Entity is a cycle and gets skipped.
			// Type ids: 3 = class instance, 5 = Hex32, 17 = Float.
			const string Content = @"<?xml version=""1.0""?>
<ReClassQt>
  <Namespace Name=""Default"">
    <Class ClassId=""1"" Name=""Entity"" Address=""400000"" DerefTwice=""0"">
      <Node Type=""5"" Name=""flags"" Comments="""" />
      <Node Type=""3"" Name=""pos"" Comments="""" PointToClass=""2"" />
      <Node Type=""3"" Name=""self"" Comments="""" PointToClass=""1"" />
      <Node Type=""17"" Name=""health"" Comments="""" />
    </Class>
    <Class ClassId=""2"" Name=""Vec3"" Address=""0"" DerefTwice=""0"">
      <Node Type=""17"" Name=""x"" Comments="""" />
      <Node Type=""17"" Name=""y"" Comments="""" />
      <Node Type=""17"" Name=""z"" Comments="""" />
    </Class>
  </Namespace>
</ReClassQt>";

			ImportTempFile(
				ReClassQtFile.FileExtension,
				Content,
				(path, project) => new ReClassQtFile(project).Load(path, new NullLogger()),
				project =>
				{
					Check.That(project.Classes.Select(c => c.Name)).ContainsExactly("Entity", "Vec3");

					CheckEntityLayout(project.Classes[0], project.Classes[1]);
				}
			);
		}
	}
}
