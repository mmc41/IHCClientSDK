using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-3: the id-keyed structural diff — add/remove/attr-change/child-reorder each populate the right
    /// set, plus the three id-less rules (metadata-block change → MetadataChanged only; nested id-less change → its
    /// id-bearing ancestor rolls up to Changed; a counter-only root delta reports nothing) and a full-document diff
    /// over an oracle mutation.
    /// </summary>
    public class ProjectChangeSetTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static readonly ElementId GroupsId = new(0x10, 0x31);
        private static readonly ElementId GroupId = new(0x11, 0x32);
        private static readonly ElementId ProductId = new(0x12, 0x53);
        private static readonly ElementId PinA = new(0x13, 0x5a);
        private static readonly ElementId PinB = new(0x14, 0x5b);

        private static ProjectElement El(string tag, ElementId id, (string, string)[] attrs, params ProjectElement[] children) =>
            ProjectElement.Create(tag, id, attrs, children);

        private static ProjectElement Pin(string tag, ElementId id) => El(tag, id, [("name", tag)]);

        private static ProjectElement Product(string name, params ProjectElement[] pins) =>
            El("product_dataline", ProductId, [("name", name)], pins);

        // Root -> groups -> group -> product, plus any id-less root extras (e.g. project_info); last_unique_id on root.
        private static Project Build(ProjectElement product, params ProjectElement[] rootExtras)
        {
            ProjectElement groups = El("groups", GroupsId, [], El("group", GroupId, [], product));
            ProjectElement[] rootChildren = [.. rootExtras, groups];
            return new Project(ProjectElement.Create("utcs_project", null, [("last_unique_id", "_0x100")], rootChildren));
        }

        private static ProjectChangeSet Diff(Project old, Project updated) =>
            ProjectChangeSet.Diff(old, updated, baseVersion: 1, newVersion: 2, origin: "test", label: "Test edit");

        [Test]
        public void Add_PopulatesAdded()
        {
            ProjectChangeSet cs = Diff(
                Build(Product("P", Pin("dataline_input", PinA))),
                Build(Product("P", Pin("dataline_input", PinA), Pin("dataline_output", PinB))));

            Assert.Multiple(() =>
            {
                Assert.That(cs.Added, Does.Contain(PinB));
                Assert.That(cs.Removed, Is.Empty);
            });
        }

        [Test]
        public void Remove_PopulatesRemoved()
        {
            ProjectChangeSet cs = Diff(
                Build(Product("P", Pin("dataline_input", PinA), Pin("dataline_output", PinB))),
                Build(Product("P", Pin("dataline_input", PinA))));

            Assert.Multiple(() =>
            {
                Assert.That(cs.Removed, Does.Contain(PinB));
                Assert.That(cs.Added, Is.Empty);
            });
        }

        // T037: an id-BEARING element added under an id-LESS container. The container carries no id, so it is not a
        // diff key; SelfAndIdlessEqual excludes the id-bearing child from the id-less roll-up, and the container's
        // nearest id-bearing ancestor's ChildIdSequence (direct id-bearing children only) never sees the nested add.
        // Characterization (no behavior change): the add surfaces ONLY as Added(child) — the ancestor (ProductId) is
        // reported in NEITHER Changed NOR ChildListChanged, so a purely-incremental reconcile has no container edge
        // for the new row (it relies on the Added-child's own subtree / the rebuild fallback).
        [Test]
        public void Diff_IdBearingAddUnderIdlessContainer_SurfacesOnlyAsAdded()
        {
            static ProjectElement Holder(params ProjectElement[] children) =>
                ProjectElement.Create("holder", null, System.Array.Empty<(string, string)>(), children);

            ProjectChangeSet cs = Diff(
                Build(Product("P", Holder())),
                Build(Product("P", Holder(Pin("dataline_output", PinB)))));

            Assert.Multiple(() =>
            {
                Assert.That(cs.Added, Does.Contain(PinB), "the id-bearing child is Added");
                Assert.That(cs.ChildListChanged, Does.Not.Contain(ProductId),
                    "the id-less container's ancestor is NOT ChildListChanged — a nested id-bearing child is invisible to ChildIdSequence");
                Assert.That(cs.Changed, Does.Not.Contain(ProductId),
                    "nor Changed — SelfAndIdlessEqual excludes the id-bearing child from the id-less roll-up comparison");
            });
        }

        [Test]
        public void AttrChange_PopulatesChanged_NotTheUnchangedChild()
        {
            ProjectChangeSet cs = Diff(
                Build(Product("Old", Pin("dataline_input", PinA))),
                Build(Product("New", Pin("dataline_input", PinA))));

            Assert.Multiple(() =>
            {
                Assert.That(cs.Changed, Does.Contain(ProductId));
                Assert.That(cs.Changed, Does.Not.Contain(PinA));
            });
        }

        [Test]
        public void ChildReorder_PopulatesChildListChanged_WithoutAddRemoveOrContentChange()
        {
            ProjectChangeSet cs = Diff(
                Build(Product("P", Pin("dataline_input", PinA), Pin("dataline_output", PinB))),
                Build(Product("P", Pin("dataline_output", PinB), Pin("dataline_input", PinA))));

            Assert.Multiple(() =>
            {
                Assert.That(cs.ChildListChanged, Does.Contain(ProductId));
                Assert.That(cs.Added, Is.Empty);
                Assert.That(cs.Removed, Is.Empty);
                Assert.That(cs.Changed, Is.Empty, "a pure reorder changes no element's content");
            });
        }

        [Test]
        public void MetadataBlockChange_SetsMetadataChangedOnly()
        {
            ProjectElement Info(string desc) => ProjectElement.Create("project_info", null, [("description", desc)], []);
            ProjectChangeSet cs = Diff(
                Build(Product("P", Pin("dataline_input", PinA)), Info("X")),
                Build(Product("P", Pin("dataline_input", PinA)), Info("Y")));

            Assert.Multiple(() =>
            {
                Assert.That(cs.MetadataChanged, Is.True);
                Assert.That(cs.Added, Is.Empty);
                Assert.That(cs.Removed, Is.Empty);
                Assert.That(cs.Changed, Is.Empty, "the id-less metadata block is not reported in Changed");
            });
        }

        [Test]
        public void NestedIdlessChange_RollsUpToTheIdBearingAncestor()
        {
            ProjectElement Marker(string v) => ProjectElement.Create("marker", null, [("v", v)], []);
            ProjectChangeSet cs = Diff(
                Build(El("product_dataline", ProductId, [("name", "P")], Marker("a"))),
                Build(El("product_dataline", ProductId, [("name", "P")], Marker("b"))));

            Assert.That(cs.Changed, Does.Contain(ProductId),
                "the id-less child's change surfaces as its nearest id-bearing ancestor being Changed");
        }

        [Test]
        public void CounterOnlyRootDelta_ReportsNothing()
        {
            ProjectElement Body() =>
                El("groups", GroupsId, [], El("group", GroupId, [], Product("P", Pin("dataline_input", PinA))));
            var old = new Project(ProjectElement.Create("utcs_project", null, [("last_unique_id", "_0x100")], [Body()]));
            var updated = new Project(ProjectElement.Create("utcs_project", null, [("last_unique_id", "_0x200")], [Body()]));

            ProjectChangeSet cs = Diff(old, updated);

            Assert.Multiple(() =>
            {
                Assert.That(cs.Added, Is.Empty);
                Assert.That(cs.Removed, Is.Empty);
                Assert.That(cs.Changed, Is.Empty);
                Assert.That(cs.ChildListChanged, Is.Empty);
                Assert.That(cs.MetadataChanged, Is.False);
            });
        }

        [Test]
        public async Task FullDocumentDiff_OverOracleMutation_DetectsTheChange()
        {
            Project baseline = await Load("project3-KompleksWired.vis");
            Project mutated = await Load("project3-KompleksWired-mutated.vis");

            ProjectChangeSet cs = Diff(baseline, mutated);

            bool anyChange = cs.Added.Count > 0 || cs.Removed.Count > 0 || cs.Changed.Count > 0
                || cs.ChildListChanged.Count > 0 || cs.MetadataChanged;
            Assert.That(anyChange, Is.True, "the full-document diff detects the oracle mutation");
        }
    }
}
