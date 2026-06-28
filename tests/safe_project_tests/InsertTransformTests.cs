using System.Collections.Immutable;
using Ihc.Projects;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Deterministic (no install dir) tests for the catalog→project insert transform: fresh sequential ids with
    /// preserved type-code suffixes, intra-subtree IDREF remapping, <c>NN#</c> menu-prefix stripping, cross-DTD
    /// default materialization, and <c>enum_definition</c> hoisting with reference rewriting (spec ch. 09).
    /// </summary>
    public class InsertTransformTests
    {
        private static ProjectElement Node(string tag, string id, (string, string)[] attrs, ProjectElement[] children)
        {
            ElementId.TryParse(id, out ElementId parsed);
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            bag.Add(("id", id));
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        private static ProjectElement EmptyEnumDefinitions() =>
            new("enum_definitions", new ElementId(0x30, 0x46),
                ImmutableArray.Create(("id", "_0x3046")), ImmutableArray<ProjectElement>.Empty);

        [Test]
        public void Insert_ReallocatesIds_RemapsSceneRef_StripsPrefix_MaterializesDefaults()
        {
            // A Lampeudtag-like catalog body (effective values, as CatalogReader would yield with DTD defaults).
            ProjectElement body = Node("product_dataline", "_0x01",
                new[] { ("product_identifier", "_0x2202"), ("name", "01#Lampeudtag"), ("locked", "yes"), ("icon", "_0x86") },
                new[]
                {
                    Node("dataline_output", "_0x02", new[] { ("name", "Udgang"), ("backup", "yes") }, System.Array.Empty<ProjectElement>()),
                    Node("scenes", "_0x03", new[] { ("name", "Scenarier"), ("scene_resource", "_0x02") }, System.Array.Empty<ProjectElement>()),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);
            ProjectElement root = result.InsertedRoot;
            ProjectElement output = root.FindChild("dataline_output")!;
            ProjectElement scenes = root.FindChild("scenes")!;

            Assert.Multiple(() =>
            {
                Assert.That(root.GetAttribute("id"), Is.EqualTo("_0x5153"), "product_dataline suffix 0x53");
                Assert.That(output.GetAttribute("id"), Is.EqualTo("_0x525b"), "dataline_output suffix 0x5b");
                Assert.That(scenes.GetAttribute("id"), Is.EqualTo("_0x5349"), "scenes suffix 0x49");
                Assert.That(scenes.GetAttribute("scene_resource"), Is.EqualTo("_0x525b"), "scene_resource remapped to new output id");
                Assert.That(root.GetAttribute("name"), Is.EqualTo("Lampeudtag"), "NN# prefix stripped");
                Assert.That(root.GetAttribute("locked"), Is.EqualTo("yes"), "materialized vs project default 'no'");
                Assert.That(output.GetAttribute("backup"), Is.EqualTo("yes"), "materialized vs project default 'no'");
                Assert.That(allocator.LastUniqueIdToken, Is.EqualTo("_0x53"));
            });
        }

        [Test]
        public void Insert_HoistsUserEnum_AndRewritesResourceEnumReferences()
        {
            // A function-block-like body carrying a user enum (no typeid) referenced by a resource_enum.
            ProjectElement body = Node("functionblock", "_0x01",
                new[] { ("name", "Block"), ("master_type", "9.9.99") },
                new[]
                {
                    Node("enum_definition", "_0x10", new[] { ("name", "Mode") }, new[]
                    {
                        Node("enum_value", "_0x11", new[] { ("name", "A") }, System.Array.Empty<ProjectElement>()),
                    }),
                    Node("settings", "_0x20", new[] { ("name", "Settings") }, new[]
                    {
                        Node("resource_enum", "_0x21", new[] { ("name", "Tilstand"), ("typedef", "_0x10"), ("inivalue", "_0x11") }, System.Array.Empty<ProjectElement>()),
                    }),
                });

            var allocator = new IdAllocator(0x50);
            InsertResult result = InsertTransform.Insert(body, allocator, EmptyEnumDefinitions(), ProjectSchemaView.RegistryOnly);

            ProjectElement hoistedDef = result.EnumDefinitions.Children[0];
            ProjectElement resourceEnum = result.InsertedRoot.FindChild("settings")!.FindChild("resource_enum")!;

            Assert.Multiple(() =>
            {
                Assert.That(result.EnumDefinitions.Children, Has.Length.EqualTo(1), "user enum hoisted to project container");
                Assert.That(result.InsertedRoot.FindChild("enum_definition"), Is.Null, "enum removed from the inserted subtree");
                Assert.That(resourceEnum.GetAttribute("typedef"), Is.EqualTo(hoistedDef.GetAttribute("id")), "typedef rewired to hoisted def");
                Assert.That(resourceEnum.GetAttribute("inivalue"), Is.EqualTo(hoistedDef.Children[0].GetAttribute("id")), "inivalue rewired to hoisted value");
            });
        }
    }
}
