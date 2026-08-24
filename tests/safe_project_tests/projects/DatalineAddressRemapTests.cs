using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE DECLARED REMAP: one published id becomes three, and the old one is retired rather than re-pointed.
    ///
    /// <para><b>Why a split and not a rename.</b> The single id covered three conditions that share only the
    /// attribute they are about: an address that is not a token at all, one outside the legal module range, and
    /// one two terminals of the same direction both claim. They have three consequences and three repairs, so a
    /// user cannot filter on the id, cannot count per condition, and cannot be given a Danish label that says
    /// anything specific. The catalogue always described them as three rows; only the engine had one id.</para>
    ///
    /// <para><b>Why the old id stays in the catalogue.</b> Re-pointing it at one of its three successors would
    /// leave a published id quietly meaning something narrower than it used to, so a consumer filtering on it
    /// would silently start seeing a third of what it saw before. Retiring it keeps that from being possible, and
    /// keeping the row is also what keeps the id RESERVED — the duplicate-code invariant refuses a second entry
    /// claiming it.</para>
    /// </summary>
    [TestFixture]
    public sealed class DatalineAddressRemapTests
    {
        private static readonly ImmutableArray<string> Successors =
            ["dataline-address-malformed", "dataline-address-range", "dataline-address-duplicate"];

        private static RuleSet Rules() =>
            RuleSet.Create(ProblemCatalog.Current, DatalineAddressRules.All(ProblemCatalog.Current));

        [Test]
        public void EverySuccessorIsDeclaredWithADanishLabelAndAnEnglishDiagnostic() =>
            MigrationParity.AssertDeclaredWithBothLanguages(Successors, RuleKind.UserContentRule);

        /// <summary>
        /// The three successors reproduce the recording for their own ids — the same parity check every other
        /// migrated rule gets. Before the pipeline switched, this compared them against the ONE id's recorded
        /// rows; after it, the recording carries the successors themselves, so the comparison is the ordinary one
        /// and the historical equivalence is what the re-recording diff showed: 207 findings before and after,
        /// with the same sites.
        /// </summary>
        [Test]
        public void TheEngineReproducesTheRecordedTuplesForTheSuccessors() =>
            MigrationParity.AssertReproducesRecording(Successors, Rules());

        /// <summary>Each of the three conditions produces its OWN id — which is the whole point of splitting.</summary>
        [Test]
        public void EachConditionProducesItsOwnId()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("dataline_input", "_0x5101", [("address_dataline", "not-a-token")]),
                        Tree.Node("dataline_input", "_0x5102", [("address_dataline", "_0xff00")]),
                        Tree.Node("dataline_input", "_0x5103", [("address_dataline", "_0x5"), ("name", "A")]),
                        Tree.Node("dataline_input", "_0x5104", [("address_dataline", "_0x5"), ("name", "B")])))));

            ILookup<string, string> byId = new WholeProjectValidator(Rules())
                .Validate(project, ValidationProfile.Categorized)
                .ToLookup(f => f.Code.Value, f => f.Primary!.Locator!);

            Assert.Multiple(() =>
            {
                Assert.That(byId["dataline-address-malformed"], Is.EqualTo(new[] { "_0x5101" }).AsCollection);
                Assert.That(byId["dataline-address-range"], Is.EqualTo(new[] { "_0x5102" }).AsCollection);
                Assert.That(byId["dataline-address-duplicate"], Is.EqualTo(new[] { "_0x5104" }).AsCollection,
                    "the SECOND claimant is the duplicate; the first holds the address");
                Assert.That(byId["dataline-address"], Is.Empty, "the retired id is never emitted");
            });
        }

        /// <summary>
        /// An address the DTD leaves unset is legal while the installation is unconfigured. Reporting it would
        /// fire on every project part-way through commissioning, which is most of them.
        /// </summary>
        [Test]
        public void AnUnaddressedTerminalIsNotAFinding()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("dataline_input", "_0x5101", [("address_dataline", ElementId.NullToken)]),
                        Tree.Node("dataline_output", "_0x5201", [])))));

            Assert.That(new WholeProjectValidator(Rules()).Validate(project, ValidationProfile.Categorized), Is.Empty);
        }

        /// <summary>An input and an output may hold the same number: uniqueness is per DIRECTION.</summary>
        [Test]
        public void AnInputAndAnOutputMayShareAnAddress()
        {
            Project project = new(Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x2020", [],
                    Tree.Node("group", "_0x2121", [],
                        Tree.Node("dataline_input", "_0x5101", [("address_dataline", "_0x5")]),
                        Tree.Node("dataline_output", "_0x5201", [("address_dataline", "_0x5")])))));

            Assert.That(new WholeProjectValidator(Rules()).Validate(project, ValidationProfile.Categorized), Is.Empty);
        }

        /// <summary>
        /// The RECORDING carries the split: the three successors appear over the corpus and the old id does not.
        ///
        /// <para>This replaces an earlier check that the split was declared in the parity map. That declaration
        /// existed to let the change through the gate while both pipelines ran; once the recording was re-made
        /// against the engine, the split is simply what the corpus produces, and the map is back to identity. The
        /// durable record of the split is the retired catalogue entry below — a map line would have been a second
        /// place to say the same thing, and the one that rots first.</para>
        /// </summary>
        [Test]
        public void TheRecordingCarriesTheThreeSuccessorsAndNotTheOldId()
        {
            string[] recorded = [.. System.IO.File
                .ReadAllLines(TestData.PathOf("validation", "rule-characterization.txt"), System.Text.Encoding.UTF8)
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Select(line => line.Split('	')[2])
                .Distinct()];

            Assert.Multiple(() =>
            {
                Assert.That(recorded, Is.SupersetOf(Successors));
                Assert.That(recorded, Does.Not.Contain("dataline-address"),
                    "the old id is not produced by anything, which is what retirement means in practice");
            });
        }

        /// <summary>
        /// The retirement is real: the id is in the catalogue, marked, with no rule behind it — and registration
        /// refuses to give it one.
        /// </summary>
        [Test]
        public void TheOldIdIsRetiredReservedAndUnimplementable()
        {
            Assert.That(ProblemCatalog.Current.TryGet(DatalineAddressRules.RetiredPredecessor,
                out ProblemCatalogEntry retired), Is.True, "still in the catalogue, keeping the id occupied");

            Assert.Multiple(() =>
            {
                Assert.That(retired.Status, Is.EqualTo(ProblemCodeStatus.Retired));
                Assert.That(Rules().TryGet(retired.Code, out _), Is.False, "and nothing implements it");

                Assert.That(() => RuleSet.Create(ProblemCatalog.Current,
                    [new RuleBuilder(retired).Inspect(_ => { }).Build()]),
                    Throws.TypeOf<RuleRegistrationException>()
                        .With.Property(nameof(RuleRegistrationException.Fault))
                        .EqualTo(RuleRegistrationFault.CodeNotActive),
                    "a retired id cannot quietly acquire a new meaning");
            });
        }
    }
}
