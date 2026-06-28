using System.Collections.Immutable;
using Ihc.Projects;

namespace Ihc.Projects.Tests
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
    }
}
