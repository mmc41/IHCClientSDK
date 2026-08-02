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
    }
}
