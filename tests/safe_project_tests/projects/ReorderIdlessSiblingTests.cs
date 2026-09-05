using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Editing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Reordering among same-tag siblings when some of them carry NO id.
    ///
    /// <para><c>ReorderSubtree</c> translates a same-tag position to an absolute child index by finding the child
    /// whose <c>Id</c> equals the target sibling's. <see cref="ElementId"/> is a record struct and
    /// <c>ProjectElement.Id</c> is <c>ElementId?</c>, so when the target sibling has no id the comparison is
    /// <c>null == null</c> — and the search stops at the FIRST id-less child of the parent, whatever its tag.</para>
    ///
    /// <para><b>This is an open-world shape, not a corpus one.</b> No fixture carries it, but the engine admits
    /// it deliberately: <c>ProjectSchemaRegistry</c> parses <c>#IMPLIED</c> attributes and round-trips undeclared
    /// element types, and the edit-open guard skips id-less elements rather than refusing them. So a hand-edited
    /// or imported file can present it, and the reorder then lands somewhere nobody asked for.</para>
    /// </summary>
    [TestFixture]
    public sealed class ReorderIdlessSiblingTests
    {
        /// <summary>An id of the right TYPE for its tag — the token an element of that tag actually carries.</summary>
        private static ElementId Id(string tag, int counter) => new(counter, Ihc.Vis.Schema.TypeCode.ForTag(tag) ?? 0);

        /// <summary>The open-world door itself: an element type the SDK registry does not declare is admitted
        /// when the PROJECT's own inline DTD declares it, which is what makes this shape reachable at all.</summary>
        private static readonly ImmutableDictionary<string, string> AnnotationDeclared =
            ImmutableDictionary<string, string>.Empty.Add("annotation",
                "<!ELEMENT annotation ANY>\r\n<!ATTLIST annotation name CDATA \"\">");

        /// <summary>
        /// The crafted shape: an id-less child of a DIFFERENT tag sits before the id-less same-tag sibling the
        /// reorder is aimed at. The two are indistinguishable to a null-equals-null comparison, and the first one
        /// wins.
        /// </summary>
        private static Project WithIdlessSiblings() =>
            new(Tree.Node("utcs_project", null, [("version_major", "4")],
                Tree.Node("groups", Id("groups", 0x20).ToToken(), [],
                    // An id-less element of an unrelated tag, FIRST among the parent's children.
                    Tree.Node("annotation", null, [("name", "Ikke en gruppe")]),
                    // The id-less same-tag sibling the reorder means to land on.
                    Tree.Node("group", null, [("name", "Uden id")]),
                    // The node being reordered — it must carry an id, because that is how it is addressed.
                    Tree.Node("group", Id("group", 0x21).ToToken(), [("name", "Med id")]))))
            { InlineDtdBlocks = AnnotationDeclared };

        [Test]
        public void ReorderingOntoAnIdlessSibling_LandsOnTheSiblingItNamed()
        {
            Project project = WithIdlessSiblings();
            ElementId moved = Id("group", 0x21);

            ProjectEditor editor = project.Edit();
            // Same-tag index 0 is the id-less <group>, which sits at ABSOLUTE index 1 — behind the annotation.
            editor.ReorderSubtree(moved, 0);
            Project after = editor.ToProject();

            string[] tags = [.. after.Root.Children.Single(c => c.Tag == "groups").Children.Select(Describe)];

            Assert.That(tags, Is.EqualTo(new[] { "annotation:Ikke en gruppe", "group:Med id", "group:Uden id" }).AsCollection,
                "the reorder must land among the node's own same-tag siblings — never in front of an unrelated "
                + "element that merely shares their lack of an id");
        }

        /// <summary>
        /// The control: with every sibling carrying an id, the same reorder is unambiguous and already correct —
        /// so the case above isolates the id-less comparison rather than the reorder itself.
        /// </summary>
        [Test]
        public void ReorderingAmongIdBearingSiblings_IsUnaffected()
        {
            Project project = new(Tree.Node("utcs_project", null, [("version_major", "4")],
                Tree.Node("groups", Id("groups", 0x20).ToToken(), [],
                    Tree.Node("annotation", null, [("name", "Ikke en gruppe")]),
                    Tree.Node("group", Id("group", 0x22).ToToken(), [("name", "Første")]),
                    Tree.Node("group", Id("group", 0x21).ToToken(), [("name", "Med id")]))))
            { InlineDtdBlocks = AnnotationDeclared };

            ProjectEditor editor = project.Edit();
            editor.ReorderSubtree(Id("group", 0x21), 0);

            string[] tags = [.. editor.ToProject().Root.Children.Single(c => c.Tag == "groups").Children.Select(Describe)];

            Assert.That(tags, Is.EqualTo(new[] { "annotation:Ikke en gruppe", "group:Med id", "group:Første" }).AsCollection);
        }

        /// <summary>
        /// The other half of the same open-world shape: an id-less PARENT.
        ///
        /// <para>The reorder addresses the parent BY ID to move the node within it, so a parent carrying none has
        /// nowhere to move to. That is reachable for exactly the reason the sibling case is — the engine admits
        /// element types a project's own DTD declares, and the edit-open guard skips id-less elements — and
        /// answering it with a bare <see cref="NullReferenceException"/> from a <c>!</c> would name neither the
        /// node nor the reason.</para>
        /// </summary>
        [Test]
        public void ReorderingUnderAnIdlessParent_IsRefusedByName()
        {
            Project project = new(Tree.Node("utcs_project", null, [("version_major", "4")],
                // The container itself carries no id — the shape an imported or hand-edited file can present.
                Tree.Node("annotation", null, [("name", "Uden id")],
                    Tree.Node("group", Id("group", 0x21).ToToken(), [("name", "Første")]),
                    Tree.Node("group", Id("group", 0x22).ToToken(), [("name", "Anden")]))))
            { InlineDtdBlocks = AnnotationDeclared };

            ProjectEditor editor = project.Edit();

            Assert.That(() => editor.ReorderSubtree(Id("group", 0x22), 0),
                Throws.InvalidOperationException.With.Message.Contains("annotation"),
                "the refusal must name the parent that cannot be addressed, not fail as a null dereference");
        }

        private static string Describe(ProjectElement element) =>
            $"{element.Tag}:{element.GetAttribute("name")}";
    }
}
