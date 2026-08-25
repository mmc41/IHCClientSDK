using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The restricted positional path builder, and the ambiguity question it answers for.
    ///
    /// <para><b>Two operations, deliberately one type.</b> <see cref="ElementNodePath.Of"/> says WHERE a node is;
    /// <see cref="ElementNodePath.WhenLocatorIsAmbiguous"/> says whether a reader needs to be told. They are tested
    /// together because the second is only ever interesting when the first is, and separating them would let the
    /// emit rule drift from the paths it emits.</para>
    ///
    /// <para><b>Reference identity is the subject.</b> <see cref="ProjectElement"/> is a record, so two structurally
    /// identical siblings are EQUAL by value. A path builder that located its target by value would hand both
    /// siblings the first one's path, which is precisely the defect an exact node path exists to close, so every
    /// fixture below builds its two candidate nodes as distinct instances and asserts they get distinct answers.</para>
    /// </summary>
    [TestFixture]
    public sealed class ElementNodePathTests
    {
        /// <summary>
        /// The builder over one tree. It is built from the run's shared analyses in production, so a test builds
        /// the analyses the same way rather than reaching past them — that keeps the fixture exercising the
        /// indexes the engine actually consults.
        /// </summary>
        private static ElementNodePath PathsIn(ProjectElement root) => new(new ProjectAnalyses(new Project(root)));

        private static ProjectElement Node(string tag, string? id = null, params ProjectElement[] children) =>
            new(
                tag,
                id is null ? null : ElementId.ParseOrNull(id),
                id is null
                    ? EquatableArray<(string, string)>.Empty
                    : ImmutableArray.Create((Name: "id", Value: id)),
                children.ToImmutableArray());

        /// <summary>
        /// The root is the path's own base case: it has no parent to be indexed within, so it renders as a single
        /// step and never as <c>/utcs_project[1]</c>.
        /// </summary>
        [Test]
        public void TheDocumentRootRendersAsItsBareTag()
        {
            ProjectElement root = Node("utcs_project");

            Assert.That(PathsIn(root).Of(root), Is.EqualTo("/utcs_project"));
        }

        /// <summary>
        /// The measured shared-token case: two <c>&lt;group&gt;</c> elements carrying <c>_0x2132</c>. The locator is
        /// the same string for both, so the path is the only thing that can tell them apart.
        /// </summary>
        [Test]
        public void TwoGroupsSharingAnIdTokenGetDistinctPaths()
        {
            ProjectElement first = Node("group", "_0x2132");
            ProjectElement second = Node("group", "_0x2132");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, first, second));

            ElementNodePath paths = PathsIn(root);
            string? firstPath = paths.Of(first);
            string? secondPath = paths.Of(second);

            Assert.Multiple(() =>
            {
                Assert.That(firstPath, Is.EqualTo("/utcs_project/groups/group[1]"));
                Assert.That(secondPath, Is.EqualTo("/utcs_project/groups/group[2]"));
                Assert.That(firstPath, Is.Not.EqualTo(secondPath));
            });
        }

        /// <summary>
        /// A same-tag index appears only where a sibling shares the tag. The intermediate <c>&lt;groups&gt;</c> step
        /// above carries no <c>[1]</c>, and neither does a lone child here.
        /// </summary>
        [Test]
        public void AStepWithNoSameTagSiblingCarriesNoIndex()
        {
            ProjectElement only = Node("group", "_0x2132");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, only));

            Assert.That(PathsIn(root).Of(only), Is.EqualTo("/utcs_project/groups/group"));
        }

        /// <summary>
        /// Indexes count SAME-TAG siblings, not all siblings: a <c>&lt;product&gt;</c> sitting between two
        /// <c>&lt;group&gt;</c>s must not push the second group to <c>[3]</c>.
        /// </summary>
        [Test]
        public void SiblingIndexesCountOnlyTheSameTag()
        {
            ProjectElement first = Node("group", "_0x2132");
            ProjectElement second = Node("group", "_0x2132");
            ProjectElement root = Node(
                "utcs_project", null, Node("groups", null, first, Node("product", "_0x40"), second));

            Assert.That(PathsIn(root).Of(second), Is.EqualTo("/utcs_project/groups/group[2]"));
        }

        /// <summary>An element that is not in the tree has no position in it.</summary>
        [Test]
        public void AnElementOutsideTheTreeHasNoPath()
        {
            ProjectElement root = Node("utcs_project", null, Node("groups"));

            Assert.That(PathsIn(root).Of(Node("group", "_0x2132")), Is.Null);
        }

        /// <summary>
        /// The measured tag case, both halves. One <c>bogus_element</c> needs no path because its tag already
        /// selects it; a second one appearing is exactly what makes the tag-count half of the emit rule
        /// non-optional, and then BOTH need one.
        /// </summary>
        [Test]
        public void ATagLocatorNeedsAPathOnlyOnceASecondSuchNodeExists()
        {
            ProjectElement lone = Node("bogus_element");
            ProjectElement singleNodeTree = Node("utcs_project", null, Node("groups", null, lone));

            ProjectElement first = Node("bogus_element");
            ProjectElement second = Node("bogus_element");
            ProjectElement twoNodeTree = Node("utcs_project", null, Node("groups", null, first, second));

            Assert.Multiple(() =>
            {
                Assert.That(
                    PathsIn(singleNodeTree).WhenLocatorIsAmbiguous(lone),
                    Is.Null,
                    "one node answers to the tag, so the locator already selects it");
                Assert.That(
                    PathsIn(twoNodeTree).WhenLocatorIsAmbiguous(first),
                    Is.EqualTo("/utcs_project/groups/bogus_element[1]"));
                Assert.That(
                    PathsIn(twoNodeTree).WhenLocatorIsAmbiguous(second),
                    Is.EqualTo("/utcs_project/groups/bogus_element[2]"));
            });
        }

        /// <summary>
        /// The document root's locator is its tag, and exactly one root exists, so it is never ambiguous — the
        /// 34 measured whole-project findings carry no path.
        /// </summary>
        [Test]
        public void TheDocumentRootIsNeverAmbiguous()
        {
            ProjectElement root = Node("utcs_project", null, Node("groups"));

            Assert.That(PathsIn(root).WhenLocatorIsAmbiguous(root), Is.Null);
        }

        /// <summary>A token exactly one element carries already selects that element.</summary>
        [Test]
        public void AUniqueIdTokenIsNotAmbiguous()
        {
            ProjectElement unique = Node("group", "_0x2132");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, unique, Node("group", "_0x2232")));

            Assert.That(PathsIn(root).WhenLocatorIsAmbiguous(unique), Is.Null);
        }

        /// <summary>Two elements answer to a shared token, so neither is selected by it.</summary>
        [Test]
        public void ASharedIdTokenIsAmbiguousAtEverySite()
        {
            ProjectElement first = Node("group", "_0x2132");
            ProjectElement second = Node("group", "_0x2132");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, first, second));

            Assert.Multiple(() =>
            {
                Assert.That(
                    PathsIn(root).WhenLocatorIsAmbiguous(first),
                    Is.EqualTo("/utcs_project/groups/group[1]"));
                Assert.That(
                    PathsIn(root).WhenLocatorIsAmbiguous(second),
                    Is.EqualTo("/utcs_project/groups/group[2]"));
            });
        }

        /// <summary>
        /// A malformed token selects NOTHING — it is not a key any index holds — so it is ambiguous for the
        /// opposite reason a shared one is. This is the state a writer testing <c>Element is null</c> would get
        /// right by accident and the 36 no-id findings would get wrong.
        /// </summary>
        [Test]
        public void AMalformedIdTokenIsAmbiguousBecauseItSelectsNothing()
        {
            ProjectElement malformed = Node("group", "_0xzz");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, malformed));

            Assert.Multiple(() =>
            {
                Assert.That(malformed.Id, Is.Null, "precondition: the token does not parse");
                Assert.That(malformed.GetAttribute("id"), Is.EqualTo("_0xzz"), "precondition: but it IS present");
                Assert.That(
                    PathsIn(root).WhenLocatorIsAmbiguous(malformed),
                    Is.EqualTo("/utcs_project/groups/group"));
            });
        }

        /// <summary>
        /// The three-state distinction stated as one assertion: an element with NO id attribute and an element
        /// with a MALFORMED one both have a null <see cref="ProjectElement.Id"/>, and only the second is
        /// ambiguous. A rule reading <c>Element is null</c> cannot tell them apart.
        /// </summary>
        [Test]
        public void AbsentAndMalformedIdsBothParseToNullAndOnlyOneIsAmbiguous()
        {
            ProjectElement noId = Node("bogus_element");
            ProjectElement malformed = Node("group", "_0xzz");
            ProjectElement root = Node("utcs_project", null, Node("groups", null, noId, malformed));

            Assert.Multiple(() =>
            {
                Assert.That(noId.Id, Is.Null);
                Assert.That(malformed.Id, Is.Null);
                Assert.That(PathsIn(root).WhenLocatorIsAmbiguous(noId), Is.Null);
                Assert.That(PathsIn(root).WhenLocatorIsAmbiguous(malformed), Is.Not.Null);
            });
        }

        /// <summary>
        /// The vendor null token is an ordinary unparseable-as-a-live-id case only insofar as it is well-formed:
        /// it parses, so a lone holder of it is not ambiguous. Guards against an emit rule that special-cases
        /// <c>_0x0</c> into the malformed branch.
        /// </summary>
        [Test]
        public void TheVendorNullTokenParsesAndIsNotAmbiguousWhenHeldOnce()
        {
            ProjectElement holder = Node("group", ElementId.NullToken);
            ProjectElement root = Node("utcs_project", null, Node("groups", null, holder));

            Assert.Multiple(() =>
            {
                Assert.That(holder.Id, Is.Not.Null, "precondition: _0x0 is well-formed");
                Assert.That(PathsIn(root).WhenLocatorIsAmbiguous(holder), Is.Null);
            });
        }

        /// <summary>
        /// Every path the builder emits must select exactly one node when read back — the property T022 asserts
        /// over the corpus, checked here on the shape that would break it first.
        /// </summary>
        [Test]
        public void EveryEmittedPathSelectsExactlyOneNode()
        {
            ProjectElement first = Node("group", "_0x2132");
            ProjectElement second = Node("group", "_0x2132");
            ProjectElement root = Node(
                "utcs_project", null,
                Node("groups", null, first, Node("product", "_0x40"), second),
                Node("groups", null, Node("group", "_0x2132")));

            ElementNodePath paths = PathsIn(root);
            foreach (ProjectElement target in root.DescendantsAndSelf())
            {
                string path = paths.Of(target)!;
                Assert.That(
                    root.DescendantsAndSelf().Count(e => paths.Of(e) == path),
                    Is.EqualTo(1),
                    $"path '{path}' must select exactly one node");
            }
        }
    }
}
