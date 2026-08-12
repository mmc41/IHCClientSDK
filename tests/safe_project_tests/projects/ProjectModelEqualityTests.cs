using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Verifies that the project model records (built on <see cref="ImmutableArray{T}"/>) compare by value, not by
    /// backing-array reference — the behaviour a <c>record</c> is expected to have. These build trees independently
    /// (no shared array instances), so a reference-equality model would report them unequal.
    /// </summary>
    public class ProjectModelEqualityTests
    {
        private static ProjectElement Leaf(string tag, params (string, string)[] attrs) =>
            new ProjectElement(tag, new ElementId(1, 2), attrs.ToImmutableArray(), ImmutableArray<ProjectElement>.Empty);

        private static ProjectElement Tree(params ProjectElement[] children) =>
            new ProjectElement("root", null,
                ImmutableArray<(string, string)>.Empty, children.ToImmutableArray());

        [Test]
        public void ProjectElement_SameContent_DifferentArrays_AreEqual()
        {
            ProjectElement a = Tree(Leaf("group", ("name", "Stue")), Leaf("group", ("name", "Entré")));
            ProjectElement b = Tree(Leaf("group", ("name", "Stue")), Leaf("group", ("name", "Entré")));

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void ProjectElement_DifferingNestedAttribute_AreNotEqual()
        {
            ProjectElement a = Tree(Leaf("group", ("name", "Stue")));
            ProjectElement b = Tree(Leaf("group", ("name", "Kontor"))); // differs deep in a child

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Project_EqualityDerivesFromRoot()
        {
            Project a = new Project(Tree(Leaf("group", ("name", "Stue"))));
            Project b = new Project(Tree(Leaf("group", ("name", "Stue"))));

            Assert.That(a, Is.EqualTo(b));
        }

        // review C1: DefinitionDocumentation is a record whose Resources is an ImmutableDictionary (no value Equals),
        // so the synthesized equality compared it BY REFERENCE. Content-equal documentation must compare equal,
        // independent of dictionary identity or build order.
        [Test]
        public void DefinitionDocumentation_SameContent_DifferentDictionaries_AreEqual()
        {
            var a = new DefinitionDocumentation("overview",
                ImmutableDictionary<string, string>.Empty.Add("In", "help A").Add("Out", "help B"));
            var b = new DefinitionDocumentation("overview",
                ImmutableDictionary<string, string>.Empty.Add("Out", "help B").Add("In", "help A"));   // built in a different order

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void DefinitionDocumentation_DifferingResourceText_AreNotEqual()
        {
            var a = new DefinitionDocumentation("s", ImmutableDictionary<string, string>.Empty.Add("In", "x"));
            var b = new DefinitionDocumentation("s", ImmutableDictionary<string, string>.Empty.Add("In", "y"));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        // review C1 propagation: a definition record carries Documentation, so once it is value-equal the whole
        // definition compares by value even when the two Documentation instances were built independently.
        [Test]
        public void ProductDefinition_EqualDocumentation_PropagatesToRecordEquality()
        {
            ProjectElement body = Leaf("product_dataline", ("name", "P"));
            ProductDefinition Def() => new ProductDefinition("_0x2101", "P", "cat", body)
            {
                Documentation = new DefinitionDocumentation("d", ImmutableDictionary<string, string>.Empty.Add("k", "v")),
            };
            ProductDefinition a = Def();
            ProductDefinition b = Def();   // independently built Documentation instances

            Assert.That(a, Is.EqualTo(b));
        }

        // review F2: FunctionBlockDefinition.ExplicitCloseIds is an ImmutableHashSet, previously reference-compared by
        // the synthesized equality. Content-equal blocks with independently-built close-id sets must compare equal,
        // while a genuinely different set stays unequal.
        [Test]
        public void FunctionBlockDefinition_ExplicitCloseIds_ComparedBySetContent()
        {
            ProjectElement body = Leaf("functionblock", ("name", "FB"));
            FunctionBlockDefinition Def(params ElementId[] closeIds) =>
                new FunctionBlockDefinition("1.1.01", "e", "Kip", "1.1.01.e. Kip", "cat", body)
                {
                    ExplicitCloseIds = ImmutableHashSet.CreateRange(closeIds),
                };

            FunctionBlockDefinition ab = Def(new ElementId(1, 2), new ElementId(3, 4));
            FunctionBlockDefinition ba = Def(new ElementId(3, 4), new ElementId(1, 2));   // same set, different insertion order
            FunctionBlockDefinition one = Def(new ElementId(1, 2));
            FunctionBlockDefinition oneAgain = Def(new ElementId(1, 2));
            FunctionBlockDefinition other = Def(new ElementId(9, 9));

            Assert.Multiple(() =>
            {
                Assert.That(ab, Is.EqualTo(ba), "same set, different insertion order → equal");
                Assert.That(one.GetHashCode(), Is.EqualTo(oneAgain.GetHashCode()));
                Assert.That(one, Is.Not.EqualTo(other), "a different close set is unequal");
            });
        }

        [Test]
        public void ProjectValidationResult_ComparesErrorsByValue()
        {
            ProjectValidationResult a = new ProjectValidationResult(false, ImmutableArray.Create("e1", "e2"));
            ProjectValidationResult b = new ProjectValidationResult(false, ImmutableArray.Create("e1", "e2"));
            ProjectValidationResult c = new ProjectValidationResult(false, ImmutableArray.Create("e1"));

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(c));
                Assert.That(ProjectValidationResult.Success, Is.EqualTo(new ProjectValidationResult(true, ImmutableArray<string>.Empty)));
            });
        }

        [Test]
        public void ProjectValidationResult_WarningsOnly_IsNotEqualToSuccess()
        {
            ProjectValidationResult warningsOnly = new ProjectValidationResult(true, ImmutableArray<string>.Empty)
            {
                Findings = ImmutableArray.Create(
                    new ProjectValidationFinding(ValidationSeverity.Warning, "vendor-tolerated", null, "a warning")),
            };

            Assert.Multiple(() =>
            {
                Assert.That(warningsOnly.IsValid, Is.True, "warnings alone leave the project valid");
                Assert.That(warningsOnly, Is.Not.EqualTo(ProjectValidationResult.Success),
                    "a result carrying a warning must not compare equal to the clean Success result");
            });
        }

        [Test]
        public void ProjectValidationResult_SameFindings_AreEqualWithMatchingHash()
        {
            static ProjectValidationResult WithWarning() =>
                new ProjectValidationResult(true, ImmutableArray<string>.Empty)
                {
                    Findings = ImmutableArray.Create(
                        new ProjectValidationFinding(ValidationSeverity.Warning, "rule-x", "_0x1", "w")),
                };

            ProjectValidationResult a = WithWarning();
            ProjectValidationResult b = WithWarning();

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        // ── the dialog-metadata model (T017) ────────────────────────────────────────────────────────
        //
        // Same pitfall, new records: ProductDialogModel and DialogGroupModel each hold an ImmutableArray, which a
        // record compares by backing-array REFERENCE. Value equality matters here beyond tidiness — T020 asserts
        // that the builder path and the catalog-reader path carry the SAME model for a family, and a
        // reference-compared model would fail that for two models that are in fact identical.

        private static ProductDialogModel DialogModel() =>
            new(ImmutableArray.Create(
                new DialogGroupModel("identitet", "Produkt egenskaber", 1, ImmutableArray.Create<DialogPartModel>(
                    new DialogFieldModel("navn", "Navn", DialogControlKind.Text,
                        new DialogBinding.RootAttribute("name"), ReadOnly: true),
                    new DialogFieldModel("placering", "Placering", DialogControlKind.ComboSuggest,
                        new DialogBinding.RootAttribute("position")))),
                new DialogGroupModel("telefonnumre", "Telefon numre", 3, ImmutableArray.Create<DialogPartModel>(
                    new DialogRepeatModel("nummer", "Nummer {0}", "sms_modem_phonenumber", "address",
                        "phonenumber", DialogControlKind.Text, DialogValueRule.PhoneNumber)))));

        [Test]
        public void ProductDialogModel_SameContent_DifferentArrays_AreEqual()
        {
            ProductDialogModel a = DialogModel();
            ProductDialogModel b = DialogModel();

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b), "two independently constructed identical models compare equal");
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        /// <summary>A difference buried in a nested part must break equality — otherwise the check is decorative.</summary>
        [Test]
        public void ProductDialogModel_DifferingNestedCaption_AreNotEqual()
        {
            ProductDialogModel a = DialogModel();
            ProductDialogModel b = new(ImmutableArray.Create(
                new DialogGroupModel("identitet", "Produkt egenskaber", 1, ImmutableArray.Create<DialogPartModel>(
                    new DialogFieldModel("navn", "Name", DialogControlKind.Text,      // English caption
                        new DialogBinding.RootAttribute("name"), ReadOnly: true)))));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void DialogGroupModel_DifferingColumns_AreNotEqual()
        {
            var three = new DialogGroupModel("g", "C", 3, ImmutableArray<DialogPartModel>.Empty);
            var one = new DialogGroupModel("g", "C", 1, ImmutableArray<DialogPartModel>.Empty);

            Assert.That(three, Is.Not.EqualTo(one), "the column count is content, not presentation trivia");
        }

        /// <summary>
        /// The three part kinds are distinct types, so two parts sharing an id are never equal. Without this a
        /// widget slot could satisfy a test expecting a field of the same name.
        /// </summary>
        [Test]
        public void DialogParts_OfDifferentKinds_AreNeverEqual()
        {
            DialogPartModel field = new DialogFieldModel("x", "X", DialogControlKind.Text,
                new DialogBinding.RootAttribute("x"));
            DialogPartModel widget = new DialogWidgetModel("x", DialogWidgetKind.TerminalGrids);

            Assert.That(field, Is.Not.EqualTo(widget));
        }

        /// <summary>The two binding kinds likewise: a root attribute is not a descendant attribute of the same name.</summary>
        [Test]
        public void DialogBindings_CompareByKindAndContent()
        {
            DialogBinding root = new DialogBinding.RootAttribute("value");
            DialogBinding descendant = new DialogBinding.DescendantAttribute("value");

            Assert.Multiple(() =>
            {
                Assert.That(root, Is.Not.EqualTo(descendant));
                Assert.That(new DialogBinding.DescendantAttribute("sms_modem_pincode"),
                    Is.EqualTo(new DialogBinding.DescendantAttribute("sms_modem_pincode", "value")),
                    "the default attribute name participates in equality as its literal value");
            });
        }

        /// <summary>Empty and default arrays mean the same thing here, as they do everywhere else in the model.</summary>
        [Test]
        public void ProductDialogModel_Empty_IsEmptyAndEqualsAnEmptyModel()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProductDialogModel.Empty.IsEmpty, Is.True);
                Assert.That(ProductDialogModel.Empty, Is.EqualTo(new ProductDialogModel(default)));
                Assert.That(DialogModel().IsEmpty, Is.False);
            });
        }
    }
}
