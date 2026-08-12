using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The enum-manager command family behind IHC Visual's <i>Bibliotek ▸ Rediger Enumerator typer</i>. Measured from
    /// the vendor (session 2026-08-04, <c>g10 4-10-2025</c>): the dialog is TWO panes — <i>Enumerator type</i> and
    /// <i>Enumerator værdier - &lt;type&gt;</i> — each with <c>Ny</c> / <c>Slet</c> / <c>Omdøb</c>, and selecting a
    /// <c>[read only]</c> built-in greys type-<c>Slet</c>, type-<c>Omdøb</c> AND all three value buttons while
    /// type-<c>Ny</c> stays live. These tests pin the five commands that back those buttons, and in particular that an
    /// illegal edit comes back <see cref="EditStatus.Refused"/> with a sentence — not <see cref="EditStatus.Failed"/>.
    /// </summary>
    public class EnumTypeEditorCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        // A user (non-catalog) type with at least one value — the target every editable case needs.
        private static EnumTypeView EditableTypeWithValues(Project project) =>
            project.GetEnumeratorTypeViews().First(t => !t.IsReadOnly && t.Values.Length > 0);

        [Test]
        public async Task GetEnumeratorTypeViews_ReportsValues_AndMarksBuiltInsReadOnly()
        {
            Project project = await Load("project3-KompleksWired.vis");

            var views = project.GetEnumeratorTypeViews();

            Assert.Multiple(() =>
            {
                Assert.That(views.Select(v => v.Name), Is.EquivalentTo(project.GetEnumeratorTypes()),
                    "the two-pane projection covers the same types as the picker projection (order differs — see below)");
                Assert.That(views.Any(v => v.IsReadOnly), Is.True, "the catalog (typeid-bearing) types are flagged");
                Assert.That(views.Where(v => v.IsReadOnly).Select(v => v.DisplayName),
                    Is.All.EndWith(" [read only]"), "…and carry the vendor's marker in their display name");
                Assert.That(views.Where(v => !v.IsReadOnly).Select(v => v.DisplayName),
                    Is.All.Not.Contains("[read only]"), "a user type carries no marker");
            });
        }

        // Measured 2026-08-04: creating "IdxTest" in the vendor's dialog put it between "Hustilstand" and
        // "Komfort-lys" — the type list is ALPHABETICAL, not document order.
        [Test]
        public async Task TheTypeList_IsAlphabetical_NotDocumentOrder()
        {
            Project project = await Load("project3-KompleksWired.vis");

            var shown = project.GetEnumeratorTypeViews().Select(v => v.DisplayName).ToList();

            Assert.That(shown, Is.Ordered.Using<string>((a, b) =>
                string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// The vendor lists a type's values by their <c>index</c>, and the FILE does not store them in that order:
        /// its own "Dimmer status" is written Reguléret(2), Sidste niveau(1), Slukket(0), … yet the dialog shows
        /// Slukket, Sidste niveau, Reguléret, … (measured 2026-08-04). A document-order read gets the right names in
        /// the wrong order — and because the editor addresses a value by its POSITION in this list, it would then
        /// rename and delete the wrong one.
        /// </summary>
        [Test]
        public async Task TheValueList_IsInIndexOrder_EvenWhenTheFileStoresThemScrambled()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            session.Apply(new AddStandaloneEnumType("Scrambled", ["Nul", "En", "To"]));
            // Re-write the middle two indexes so document order and index order disagree, as the vendor's file does.
            Project scrambled = Scramble(session.Current!, "Scrambled");

            Assert.That(scrambled.GetEnumeratorTypeViews().First(t => t.Name == "Scrambled").Values,
                Is.EqualTo(new[] { "En", "Nul", "To" }),
                "index order (En=0, Nul=1, To=2) — NOT the document order Nul, En, To");
        }

        // The engine's re-numbering must follow index order too, for the same reason: numbering by document position
        // would keep every reference valid while silently PERMUTING what each value means.
        [Test]
        public async Task DeletingAValue_RenumbersInIndexOrder_NotDocumentOrder()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            session.Apply(new AddStandaloneEnumType("Scrambled", ["Nul", "En", "To"]));
            var reopened = new ProjectDocumentSession();
            reopened.Open(Scramble(session.Current!, "Scrambled"));

            // Values now read En(0), Nul(1), To(2). Delete position 1 = "Nul".
            EditOutcome outcome = reopened.Apply(new DeleteEnumValue("Scrambled", 1));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(Values(reopened, "Scrambled"), Is.EqualTo(new[] { "En", "To" }));
                Assert.That(Indexes(reopened, "Scrambled").OrderBy(i => i), Is.EqualTo(new[] { "0", "1" }),
                    "contiguous again, exactly as the vendor rewrote CCC from index 2 to index 1");
            });
        }

        /// <summary>Swaps the first two values' <c>index</c> attributes so document order and index order disagree —
        /// reproducing the layout the vendor's own projects carry.</summary>
        private static Project Scramble(Project project, string typeName)
        {
            ProjectEditor editor = project.Edit();
            EnumDefinitionRef def = editor.EnumDefinition(typeName);
            editor.SetAttributeById(def.Values[0].Id, "index", "1");
            editor.SetAttributeById(def.Values[1].Id, "index", "0");
            return editor.ToProject();
        }

        [Test]
        public async Task RenameEnumType_RenamesInPlace_AndKeepsReferencesValid()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView target = EditableTypeWithValues(project);

            EditOutcome outcome = session.Apply(new RenameEnumType(target.Name, "OmdøbtType"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.GetEnumeratorTypes(), Does.Contain("OmdøbtType"));
                Assert.That(session.Current!.GetEnumeratorTypes(), Does.Not.Contain(target.Name));
                Assert.That(session.Current!.GetEnumeratorTypeViews().First(t => t.Name == "OmdøbtType").Values,
                    Is.EqualTo(target.Values), "a rename touches the name only — the values are untouched");
                Assert.That(session.Current!.LastUniqueId, Is.EqualTo(project.LastUniqueId),
                    "a rename allocates no ids");
            });
        }

        // The vendor greys Omdøb on a "[read only]" type; we refuse with a reason rather than faulting.
        [Test]
        public async Task RenameEnumType_OnABuiltIn_IsRefused_NotFailed()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView builtIn = project.GetEnumeratorTypeViews().First(t => t.IsReadOnly);

            EditOutcome outcome = session.Apply(new RenameEnumType(builtIn.Name, "ShouldNotStick"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("read only"));
                Assert.That(session.Current!.GetEnumeratorTypes(), Does.Contain(builtIn.Name), "…and nothing changed");
            });
        }

        [Test]
        public async Task DeleteEnumType_RemovesAnUnreferencedType()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            session.Apply(new AddStandaloneEnumType("KunTilSletning", ["A", "B"]));
            int before = session.Current!.GetEnumeratorTypes().Count;

            EditOutcome outcome = session.Apply(new DeleteEnumType("KunTilSletning"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.GetEnumeratorTypes(), Does.Not.Contain("KunTilSletning"));
                Assert.That(session.Current!.GetEnumeratorTypes(), Has.Count.EqualTo(before - 1));
            });
        }

        // A type a resource still points at cannot go: the typedef would dangle and no reader could repair it.
        [Test]
        public async Task DeleteEnumType_StillReferenced_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView inUse = project.GetEnumeratorTypeViews()
                .First(t => !t.IsReadOnly && IsReferenced(project, t.Name));

            EditOutcome outcome = session.Apply(new DeleteEnumType(inUse.Name));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("bruges stadig"));
                Assert.That(session.Current!.GetEnumeratorTypes(), Does.Contain(inUse.Name));
            });
        }

        [Test]
        public async Task AddEnumValue_AppendsOneValue()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView target = EditableTypeWithValues(project);

            EditOutcome outcome = session.Apply(new AddEnumValue(target.Name, "NyVærdi"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(Values(session, target.Name), Is.EqualTo(target.Values.Append("NyVærdi")),
                    "the value is APPENDED — the vendor's values pane adds to the end");
            });
        }

        [Test]
        public async Task RenameEnumValue_RelabelsByPosition()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView target = EditableTypeWithValues(project);

            EditOutcome outcome = session.Apply(new RenameEnumValue(target.Name, 0, "FørsteOmdøbt"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(Values(session, target.Name).First(), Is.EqualTo("FørsteOmdøbt"));
                Assert.That(Values(session, target.Name), Has.Count.EqualTo(target.Values.Length),
                    "a relabel adds and removes nothing");
                Assert.That(session.Current!.LastUniqueId, Is.EqualTo(project.LastUniqueId),
                    "a relabel allocates no ids");
            });
        }

        // Removing a value must renumber the survivors: AddEnumValues continues `index` from the value COUNT, so a
        // hole would make the next append collide on an index that is already taken.
        [Test]
        public async Task DeleteEnumValue_RemovesIt_AndTheNextAppendStillNumbersContiguously()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            session.Apply(new AddStandaloneEnumType("TreVærdier", ["En", "To", "Tre"]));

            EditOutcome outcome = session.Apply(new DeleteEnumValue("TreVærdier", 1));
            session.Apply(new AddEnumValue("TreVærdier", "Fire"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(Values(session, "TreVærdier"), Is.EqualTo(new[] { "En", "Tre", "Fire" }));
                Assert.That(Indexes(session, "TreVærdier"), Is.EqualTo(new[] { "0", "1", "2" }),
                    "the survivors are renumbered 0-based, so the append lands on a free index");
            });
        }

        [Test]
        public async Task ValueCommands_OnAnOutOfRangePosition_AreRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView target = EditableTypeWithValues(project);

            EditOutcome outcome = session.Apply(new DeleteEnumValue(target.Name, target.Values.Length));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("ingen værdi på plads"));
            });
        }

        [Test]
        public async Task ValueCommands_OnABuiltIn_AreRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            EnumTypeView builtIn = project.GetEnumeratorTypeViews().First(t => t.IsReadOnly);

            Assert.Multiple(() =>
            {
                Assert.That(session.Apply(new AddEnumValue(builtIn.Name, "Nej")).Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(session.Apply(new RenameEnumValue(builtIn.Name, 0, "Nej")).Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(session.Apply(new DeleteEnumValue(builtIn.Name, 0)).Status, Is.EqualTo(EditStatus.Refused));
            });
        }

        private static System.Collections.Generic.IReadOnlyList<string> Values(ProjectDocumentSession session, string typeName) =>
            session.Current!.GetEnumeratorTypeViews().First(t => t.Name == typeName).Values.ToList();

        private static System.Collections.Generic.IEnumerable<string> Indexes(ProjectDocumentSession session, string typeName) =>
            session.Current!.Child("enum_definitions")!.ChildrenOrEmpty()
                .First(c => c.Tag == "enum_definition" && c.GetAttribute("name") == typeName)
                .ChildrenOrEmpty().Where(v => v.Tag == "enum_value")
                .Select(v => v.GetAttribute("index") ?? "0");

        private static bool IsReferenced(Project project, string typeName)
        {
            ProjectElement def = project.Child("enum_definitions")!.ChildrenOrEmpty()
                .First(c => c.Tag == "enum_definition" && c.GetAttribute("name") == typeName);
            return project.Root.DescendantsAndSelf().Any(e =>
                e.GetAttribute("typedef") is { } token
                && ElementId.TryParse(token, out ElementId referenced)
                && referenced == def.Id);
        }
    }
}
