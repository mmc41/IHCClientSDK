using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Editor mutations must fail loudly instead of silently corrupting: a delete that would leave dangling
    /// IDREFs throws before committing; a cascade never deletes a non-half element a stray <c>link</c> points
    /// at; <see cref="FunctionBlockRef.Setting"/> targets the value-variable containers (never a same-named
    /// pin); <see cref="ProductRef"/> is family-aware; and the smaller guard rails (empty-template check,
    /// container-bound pins, enum-value messages) reject bad input at the call.
    /// </summary>
    public class EditorGuardTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load(string name) =>
            new ProjectAppService(Settings).Load("testdata/" + name);

        private static ProjectElement Node(string tag, string? id, (string, string)[] attrs, params ProjectElement[] children)
        {
            ElementId? parsed = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        // ----- DeleteById: dangling references block the delete; strays never cascade innocents -----

        [Test]
        public async Task DeleteById_OutputBoundToScenes_Throws_InsteadOfDanglingTheBinding()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectEditor editor = project.Edit();
            GroupRef stue = editor.Group("Stue");
            ProductRef lamp = stue.Product("Lampeudtag");

            Assert.That(() => lamp.RemoveOutput(lamp.Output("Udgang")),
                Throws.InvalidOperationException.With.Message.Contains("scene_resource"),
                "the scenes container still binds this output — deleting it must not dangle the binding");
        }

        [Test]
        public void DeleteById_StrayLinkAttrPointingAtAProduct_DoesNotDeleteTheProduct()
        {
            // A foreign-generated file may carry a link half whose `link` IDREF mistakenly names a product.
            // The cascade must only ever remove genuine reciprocal halves — never the innocent element.
            ProjectElement stray = Node("link_from_resource", "_0x6155",
                new[] { ("name", "Følg Link"), ("icon", "_0x47"), ("link", "_0x5153") });
            ProjectElement input = Node("dataline_input", "_0x6052", new[] { ("name", "D") }, stray);
            ProjectElement product = Node("product_dataline", "_0x5153",
                new[] { ("product_identifier", "_0x2202"), ("name", "P") });
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x70") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "A") }, input),
                    Node("group", "_0x2232", new[] { ("name", "B") }, product)));
            ProjectEditor editor = new Project(root).Edit();

            editor.DeleteById(new ElementId(0x60, 0x52));

            Project after = editor.ToProject();
            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(new ElementId(0x51, 0x53)), Is.Not.Null, "the product survives");
                Assert.That(after.FindById(new ElementId(0x60, 0x52)), Is.Null, "the input is gone");
            });
        }

        [Test]
        public void DeleteById_EnumDefinitionInUse_Throws()
        {
            ProjectElement definition = Node("enum_definition", "_0x4147", new[] { ("name", "Tilstand") },
                Node("enum_value", "_0x4248", new[] { ("name", "Av") }));
            ProjectElement consumer = Node("resource_enum", "_0x6023",
                new[] { ("name", "Valg"), ("typedef", "_0x4147"), ("inivalue", "_0x4248") });
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x70") },
                Node("enum_definitions", "_0x3046", new[] { ("name", "E") }, definition),
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "A") }, consumer)));
            ProjectEditor editor = new Project(root).Edit();

            Assert.That(() => editor.DeleteById(new ElementId(0x41, 0x47)),
                Throws.InvalidOperationException.With.Message.Contains("typedef"),
                "a definition still referenced by a resource_enum must not be silently deleted");
        }

        // ----- FunctionBlockRef.Setting: scoped to the value-variable containers -----

        [Test]
        public async Task Setting_NameSharedWithAnInputPin_TargetsTheSettingNotThePin()
        {
            // project3's "4.1.04. Driftstimetæller" carries "Reset" both as an input pin and as an
            // internalsettings timer — the vendor reuses display names across sections.
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ProjectElement block = project.Root.Descendants().First(e =>
                e.Tag == "functionblock"
                && e.FindChild("inputs")?.Children.Any(c => c.GetAttribute("name") == "Reset") == true
                && e.FindChild("internalsettings")?.Children.Any(c => c.GetAttribute("name") == "Reset") == true);
            var fb = new FunctionBlockRef(editor, block.Id!.Value);

            fb.Setting("Reset", s => s.Minutes(9));

            Project after = editor.ToProject();
            ProjectElement blockAfter = after.FindById(block.Id!.Value)!;
            ProjectElement pin = blockAfter.FindChild("inputs")!.Children.First(c => c.GetAttribute("name") == "Reset");
            ProjectElement timer = blockAfter.FindChild("internalsettings")!.Children.First(c => c.GetAttribute("name") == "Reset");
            Assert.Multiple(() =>
            {
                Assert.That(timer.GetAttribute("minute"), Is.EqualTo("9"), "the internalsettings timer took the override");
                Assert.That(pin.GetAttribute("minute"), Is.Null, "the same-named input pin is untouched");
            });
        }

        // ----- ProductRef family awareness -----

        [Test]
        public async Task AirlinkProduct_IsReachableByName_AndDatalineIoMethodsRejectIt()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ProjectElement airlink = project.Root.Descendants().First(e => e.Tag == "product_airlink");
            ProjectElement room = project.FindParent(airlink.Id!.Value)!;
            ProductRef product = editor.Group(room.GetAttribute("name")!).Product(airlink.GetAttribute("name")!);

            Assert.That(() => product.AddInput("Tryk", b => b.Address("_0x1")),
                Throws.InvalidOperationException.With.Message.Contains("product_airlink"),
                "authoring a wired-bus pin onto a wireless product must fail, not write a bogus child");
        }

        [Test]
        public async Task RemoveScenes_MatchesByTag_NotByLocalizedDefaultName()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            ProjectElement owner = project.Root.Descendants().First(e =>
                !e.Children.IsDefaultOrEmpty
                && e.Children.Any(c => c.Tag == "scenes" && c.GetAttribute("name") != "Scenarier"));
            ProjectElement room = FindRoom(project, owner);
            ProductRef product = editor.Group(room.GetAttribute("name")!).Product(owner.GetAttribute("name")!);

            product.RemoveScenes();

            ProjectElement ownerAfter = editor.ToProject().FindById(owner.Id!.Value)!;
            Assert.That(ownerAfter.Children.IsDefaultOrEmpty || ownerAfter.Children.All(c => c.Tag != "scenes"),
                Is.True, "every scenes container is removed regardless of its display name");
        }

        private static ProjectElement FindRoom(Project project, ProjectElement element)
        {
            ProjectElement current = element;
            while (project.FindParent(current.Id!.Value) is { } parent)
            {
                if (parent.Tag == "group")
                {
                    return parent;
                }
                current = parent;
            }
            throw new InvalidOperationException("element is not inside a room");
        }

        // ----- small guard rails -----

        [Test]
        public async Task AddEmptyFunctionBlock_FullCatalogBlockAsTemplate_Throws()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectEditor editor = project.Edit();
            var notATemplate = new FunctionBlockDefinition("1.1.01", "e", "Kip", "1.1.01.e. Kip", "Cat",
                new ProjectElement("functionblock", null,
                    ImmutableArray<(string, string)>.Empty, ImmutableArray<ProjectElement>.Empty));

            Assert.That(() => editor.Group("Stue").AddEmptyFunctionBlock(notATemplate, new DateOnly(2026, 7, 4)),
                Throws.ArgumentException.With.Message.Contains("Tom blok"),
                "a full catalog block must not have its identity forged by the empty-template path");
        }

        [Test]
        public async Task FunctionBlock_AddInput_RejectsOutputPinTypes()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectEditor editor = project.Edit();
            FunctionBlockRef kip = editor.Group("Stue").FunctionBlock("1.1.01.e. Kip tænd sluk");

            Assert.That(() => kip.AddInput("resource_output", "Forkert"),
                Throws.ArgumentException.With.Message.Contains("inputs"));
        }

        [Test]
        public async Task EnumInitialValue_UnknownName_NamesTheEnumAndItsValues()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectEditor editor = project.Edit();
            EnumDefinitionRef definition = editor.AddEnumDefinition("MyEnum", "A", "B");

            Assert.That(() => definition.InitialValue("C"),
                Throws.InvalidOperationException
                    .With.Message.Contains("MyEnum").And.Message.Contains("A | B"));
        }
    }
}
