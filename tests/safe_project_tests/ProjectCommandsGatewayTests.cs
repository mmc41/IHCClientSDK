using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// R1/T003: the <see cref="ProjectCommands"/> gateway — the single authoring door reachable from
    /// <see cref="ProjectAppService"/> (D01) — builds the Locality command family (AddLocality/RenameLocality/
    /// DeleteLocality). Each factory must resolve <b>exactly</b> as direct construction (D10 parity, so the
    /// produced bytes are unchanged) and apply through a session with the same effect. This establishes the
    /// factory pattern every later R1 family follows.
    /// </summary>
    public class ProjectCommandsGatewayTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        [Test]
        public void Commands_GatewayIsReachableFromTheService()
        {
            Assert.That(App.Commands, Is.Not.Null, "the one door is reachable from ProjectAppService (D01)");
        }

        [Test]
        public async Task AddLocality_FactoryResolvesExactlyAsDirectConstruction()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");

            Assert.That(app.Commands.AddLocality(project, "Kitchen"),
                Is.EqualTo(new AddLocality("Kitchen")), "D10: the gateway builds the identical command");
        }

        [Test]
        public async Task RenameLocality_FactoryResolvesExactlyAsDirectConstruction()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId id = project.Groups.First().Id!.Value;

            Assert.That(app.Commands.RenameLocality(project, id, "Renamed", "a note"),
                Is.EqualTo(new RenameLocality(id, "Renamed", "a note")));
        }

        [Test]
        public async Task DeleteLocality_FactoryResolvesExactlyAsDirectConstruction()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId id = project.Groups.First().Id!.Value;

            Assert.That(app.Commands.DeleteLocality(project, id), Is.EqualTo(new DeleteLocality(id)));
        }

        [Test]
        public async Task GatewayAddLocality_AppliesThroughSession_AddsTheLocality()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            int before = session.Current!.Groups.Count;

            EditOutcome<ElementId> outcome = session.Apply(app.Commands.AddLocality(session.Current!, "Kitchen"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Groups.Count, Is.EqualTo(before + 1));
                Assert.That(session.Current!.FindById(outcome.Value)?.GetAttribute("name"), Is.EqualTo("Kitchen"),
                    "the gateway-built AddLocality commits and its id resolves to the new locality");
            });
        }

        // ---- Product family (T004) ----

        [Test]
        public async Task AddProduct_ResolvesCatalogProduct_AndReturnsNullForUnknown()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            Ihc.Vis.Products.ProductDefinition def = app.GetAvailableProducts().First();

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.AddProduct(project, locality, def.ProductIdentifier),
                    Is.EqualTo(new AddProduct(locality, def)), "D10: resolves the same catalog product as before");
                Assert.That(app.Commands.AddProduct(project, locality, "no-such-identifier"), Is.Null,
                    "an unknown identifier builds nothing");
            });
        }

        [Test]
        public async Task AddFunctionBlock_ResolvesByMasterType_AndReturnsNullForUnknown()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            Ihc.Vis.FunctionBlocks.FunctionBlockDefinition fb = app.GetAvailableFunctionBlocks().First();

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.AddFunctionBlock(project, locality, fb.MasterType),
                    Is.EqualTo(new AddFunctionBlock(locality, fb)));
                Assert.That(app.Commands.AddFunctionBlock(project, locality, "not-a-real-block"), Is.Null);
            });
        }

        [Test]
        public async Task AddEmptyFunctionBlock_UsesCatalogTemplate_AndAppliesUnderTheLocality()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            ElementId locality = session.Current!.Groups.First().Id!.Value;

            EditOutcome<ElementId> outcome =
                session.Apply(app.Commands.AddEmptyFunctionBlock(session.Current!, locality, "Empty block"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(outcome.Value)?.Kind, Is.EqualTo(ElementKind.FunctionBlock));
            });
        }

        [Test]
        public async Task AddVariable_NullForNonFunctionBlockSection()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId localityId = project.Groups.First().Id!.Value;   // a locality is not a FB variable section

            Assert.That(app.Commands.AddVariable(project, localityId, "resource_input", "X"), Is.Null);
        }

        [Test]
        public async Task UpdateProduct_CapturesTheProductsCurrentLocality()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement product = project.Root.DescendantsAndSelf()
                .First(e => Ihc.Vis.Products.ProductClassifier.IsProduct(e.Tag));
            ElementId productId = product.Id!.Value;
            ElementId parentId = project.FindParent(productId)!.Id!.Value;
            var r = new ProductPropertiesResult("N", parentId.ToToken(), "note", "", "", "", "");

            Assert.That(app.Commands.UpdateProduct(project, productId, r),
                Is.EqualTo(new UpdateProduct(productId, r, parentId)),
                "D10: captures the same current-parent id the app did");
        }

        [Test]
        public async Task UpdatePin_And_UnlockFunctionBlock_ArePassThroughFactories()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId anyId = project.Groups.First().Id!.Value;
            var pin = new PinPropertiesResult(0, 1, "red", "n", true);

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.UpdatePin(project, anyId, pin), Is.EqualTo(new UpdatePin(anyId, pin)));
                Assert.That(app.Commands.UnlockFunctionBlock(project, anyId), Is.EqualTo(new UnlockFunctionBlock(anyId)));
            });
        }

        [Test]
        public async Task WouldExceedModemLimit_FalseForNonModemProductInAModemlessProject()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            string firstNonModem = app.GetAvailableProducts()
                .First(p => !Ihc.Vis.Products.ProductClassifier.IsModem(p.Body.Tag)).ProductIdentifier;

            Assert.That(app.Commands.WouldExceedModemLimit(project, firstNonModem), Is.False,
                "the one-modem gate (sliver #10) is open when no modem is present");
        }

        // ---- Structure family (T005) ----

        [Test]
        public async Task MoveNode_CopyNode_DeleteNode_ArePassThroughFactories()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId a = project.Groups.First().Id!.Value;
            ElementId b = project.Groups.Skip(1).First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.MoveNode(project, a, b), Is.EqualTo(new MoveNode(a, b)));
                Assert.That(app.Commands.CopyNode(project, a, b), Is.EqualTo(new CopyNode(a, b)));
                Assert.That(app.Commands.DeleteNode(project, a, cascade: true), Is.EqualTo(new DeleteNode(a, true)));
            });
        }

        [Test]
        public async Task ReorderNode_ResolvesTargetIndex_AndReturnsNullAtTheEnds()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId first = project.Groups.First().Id!.Value;   // index 0 among same-tag group siblings

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.ReorderNode(project, first, +1), Is.EqualTo(new ReorderNode(first, 1)),
                    "D10: resolves the same target index the app did");
                Assert.That(app.Commands.ReorderNode(project, first, -1), Is.Null, "already at the top → no command");
                Assert.That(app.Commands.ReorderNode(project, first, 0), Is.Null, "a zero move is a no-op");
            });
        }

        [Test]
        public async Task CanReorderNode_TrueForSameParentSameTagSiblings_FalseOtherwise()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId g0 = project.Groups.First().Id!.Value;
            ElementId g1 = project.Groups.Skip(1).First().Id!.Value;
            ElementId product = project.Root.DescendantsAndSelf()
                .First(e => Ihc.Vis.Products.ProductClassifier.IsProduct(e.Tag)).Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.CanReorderNode(project, g0, g1), Is.True, "two localities are reorderable siblings");
                Assert.That(app.Commands.CanReorderNode(project, g0, product), Is.False, "different tags are not");
                Assert.That(app.Commands.ReorderNodeToSibling(project, g1, g0), Is.EqualTo(new ReorderNode(g1, 0)));
            });
        }

        [Test]
        public async Task PreviewDelete_ClassifiesLinkLocalityGeneralAndNotDeletable()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId localityWithContents = project.Groups.First(g => !g.Children.IsDefaultOrEmpty).Id!.Value;
            ElementId product = project.Root.DescendantsAndSelf()
                .First(e => Ihc.Vis.Products.ProductClassifier.IsProduct(e.Tag)).Id!.Value;
            ElementId linkHalf = project.Root.DescendantsAndSelf().First(e => e.IsLinkHalf).Id!.Value;

            Assert.Multiple(() =>
            {
                DeleteImpact locality = app.Commands.PreviewDelete(project, localityWithContents);
                Assert.That(locality.Kind, Is.EqualTo(DeleteKind.Locality));
                Assert.That(locality.Deletable, Is.True);
                Assert.That(locality.NeedsConfirm, Is.True, "a non-empty locality cascades → confirm (US-009)");

                DeleteImpact prod = app.Commands.PreviewDelete(project, product);
                Assert.That(prod.Kind, Is.EqualTo(DeleteKind.General));
                Assert.That(prod.Deletable, Is.True);

                DeleteImpact link = app.Commands.PreviewDelete(project, linkHalf);
                Assert.That(link.Kind, Is.EqualTo(DeleteKind.Link));
                Assert.That(link.NeedsConfirm, Is.False, "a link removes its reciprocal without a confirm (US-057)");

                DeleteImpact missing = app.Commands.PreviewDelete(project, new ElementId(0x7FFFFF, 0x32));
                Assert.That(missing.Deletable, Is.False);
                Assert.That(missing.Kind, Is.EqualTo(DeleteKind.NotDeletable));
            });
        }

        // ---- Link/Scene family (T006) ----

        [Test]
        public async Task LinkPins_RemoveLink_UpdateScene_ArePassThroughFactories()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId a = project.Groups.First().Id!.Value;
            ElementId b = project.Groups.Skip(1).First().Id!.Value;
            var sv = new SceneValueResult(true, 50, 0, 0);

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.LinkPins(project, a, b), Is.EqualTo(new LinkPins(a, b)));
                Assert.That(app.Commands.RemoveLink(project, a), Is.EqualTo(new RemoveLink(a)));
                Assert.That(app.Commands.UpdateSceneValue(project, a, sv), Is.EqualTo(new UpdateSceneValue(a, sv)));
                Assert.That(app.Commands.UpdateSceneContainer(project, a, "n"), Is.EqualTo(new UpdateSceneContainer(a, "n")));
            });
        }

        [Test]
        public async Task LinkScene_StampsTheVariantFromTheBoundOutputFamily()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            // A scenes container in the project (its bound output decides the relay/dimmer variant).
            ProjectElement scenes = project.Root.DescendantsAndSelf().First(e => e.IsScenesContainer);
            ElementId scenesId = scenes.Id!.Value;
            ElementId output = project.Groups.First().Id!.Value;   // any endpoint id — the variant comes from the scenes binding
            var sv = new SceneValueResult(true, 50, 0, 0);
            bool expectedDimmer = app.Commands.IsSceneWirelessDimming(project, scenesId);

            Assert.That(app.Commands.LinkScene(project, output, scenesId, sv),
                Is.EqualTo(new LinkScene(output, scenesId, sv, expectedDimmer)),
                "D10: LinkScene stamps the same isDimmer the app inferred (sliver #11)");
        }

        // ---- Program family (T007) ----

        [Test]
        public async Task ProgramPassThroughFactories_ResolveAsDirectConstruction()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId a = project.Groups.First().Id!.Value;
            ElementId b = project.Groups.Skip(1).First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.AddProgramCommand(project, a, b, "_0xa", "n", null), Is.EqualTo(new AddProgramCommand(a, b, "_0xa", "n", null)));
                Assert.That(app.Commands.AddSubProgram(project, a), Is.EqualTo(new AddSubProgram(a)));
                Assert.That(app.Commands.AddCondition(project, a, b, "_0xa", "n", null), Is.EqualTo(new AddCondition(a, b, "_0xa", "n", null)));
                Assert.That(app.Commands.SetConditionsLogic(project, a, true), Is.EqualTo(new SetConditionsLogic(a, true)));
                Assert.That(app.Commands.AddLogicGroup(project, a), Is.EqualTo(new AddLogicGroup(a)));
                Assert.That(app.Commands.AddArithmeticCommand(project, a, b, "_0x5a", a, "n"), Is.EqualTo(new AddArithmeticCommand(a, b, "_0x5a", a, "n")));
                Assert.That(app.Commands.AddCase(project, a, b), Is.EqualTo(new AddCase(a, b)));
                Assert.That(app.Commands.SetOutputBackup(project, a, true), Is.EqualTo(new SetOutputBackup(a, true)));
                Assert.That(app.Commands.ToggleLogMark(project, a), Is.EqualTo(new ToggleLogMark(a)));
            });
        }

        [Test]
        public async Task AddProgramEvent_ResolvesOwningProgram_NullForNonEventsTarget()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            Assert.That(app.Commands.AddProgramEvent(project, locality, locality, "_0xa", "n", null), Is.Null,
                "a non-events target builds nothing");
            Assert.That(app.Commands.AddPowerEvent(project, locality), Is.Null);

            ProjectElement? events = project.Root.DescendantsAndSelf()
                .FirstOrDefault(e => e.IsEventsContainer && e.Id is { } eid && project.FindParent(eid)?.IsProgram == true);
            if (events is { Id: { } eventsId })
            {
                ElementId program = project.FindParent(eventsId)!.Id!.Value;
                Assert.Multiple(() =>
                {
                    Assert.That(app.Commands.AddProgramEvent(project, eventsId, locality, "_0xa", "n", null),
                        Is.EqualTo(new AddProgramEvent(program, locality, "_0xa", "n", null)),
                        "the owning program is resolved from the events container");
                    Assert.That(app.Commands.AddPowerEvent(project, eventsId), Is.EqualTo(new AddPowerEvent(program)));
                });
            }
        }

        [Test]
        public async Task AddCaseValue_NullForNonCaseTarget_And_NumericTagsRelocatedToSdk()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.AddCaseValue(project, locality, "1"), Is.Null, "a non-case target builds nothing");
                Assert.That(Ihc.Vis.Programs.ProgramMethodCatalog.NumericVariableTags,
                    Is.EquivalentTo(new[] { "resource_floating_point", "resource_integer", "resource_counter" }),
                    "sliver #3: the arithmetic-eligibility set is now SDK-owned");
            });
        }

        // ---- Metadata family (T008) ----

        [Test]
        public async Task MetadataPassThroughFactories_ResolveAsDirectConstruction()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId id = project.Groups.First().Id!.Value;
            ElementId productId = project.Root.DescendantsAndSelf()
                .First(e => Ihc.Vis.Products.ProductClassifier.IsProduct(e.Tag)).Id!.Value;
            var dimmer = new AdvancedDimmerResult(1, 2, 3, 4, 5, "m");
            var modem = new ModemPropertiesResult("n", "loc", "note", "id", "0", "24", "r-", "r+", "pin", System.Array.Empty<string>());

            Assert.Multiple(() =>
            {
                Assert.That(app.Commands.UpdateProjectInfo(project, ProjectInfoData.Empty), Is.EqualTo(new UpdateProjectInfo(ProjectInfoData.Empty)));
                Assert.That(app.Commands.UpdateUserText(project, id, "t"), Is.EqualTo(new UpdateUserText(id, "t")));
                Assert.That(app.Commands.DeleteUserText(project, id), Is.EqualTo(new DeleteUserText(id)));
                Assert.That(app.Commands.UpdateDimmerSettings(project, productId, dimmer), Is.EqualTo(new UpdateDimmerSettings(productId, dimmer)));
                Assert.That(app.Commands.UpdateModem(project, productId, modem),
                    Is.EqualTo(new UpdateModem(productId, modem, project.FindParent(productId)?.Id)),
                    "D10: captures the same current-parent id the app did");
            });
        }

        [Test]
        public void AddUserText_ReportsTableExistence()
        {
            ProjectAppService app = App;
            Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
            Assert.That(app.Commands.AddUserText(project, "hello").TableExists, Is.False, "a fresh project has no user-texts table");

            ProjectDocumentSession session = Session(project);
            session.Apply(app.Commands.AddUserText(session.Current!, "hello"));   // creates the table
            Assert.That(app.Commands.AddUserText(session.Current!, "world").TableExists, Is.True, "the table now exists");
        }

        [Test]
        public async Task UpdateEnumStates_NullForNonEnum_AndDiffsAgainstExistingStates()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            Assert.That(app.Commands.UpdateEnumStates(project, locality, new[] { "x" }), Is.Null, "a non-enum target builds nothing");

            ProjectElement? enumVar = project.Root.DescendantsAndSelf().FirstOrDefault(e => e.Kind == ElementKind.EnumResource);
            if (enumVar is { Id: { } enumId } && app.Commands.UpdateEnumStates(project, enumId, new[] { "BrandNewStateXYZ" }) is { } cmd)
                Assert.That(cmd.Added, Does.Contain("BrandNewStateXYZ"), "a genuinely new state is in the delta");
        }
    }
}
