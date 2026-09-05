using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Products;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Whether any editor command can commit a project that breaks the core invariant.
    ///
    /// <para><b>The window this fixture exists to probe.</b> <c>ProjectContracts.AssertCore</c> runs at the
    /// commit boundary but is <c>[Conditional("DEBUG")]</c> BY CONTRACT — the header calls it a Debug-tier
    /// interior observation point and puts the always-on guards where consequences are irreversible: the
    /// <c>Edit()</c> entry, <c>UploadTo</c>, and the opt-in save options. The obvious test cannot reach the
    /// flagged line at all: a crafted duplicate-id project is refused at open by
    /// <c>GuardNoDuplicateIdTokens</c> and never gets as far as an edit.</para>
    ///
    /// <para>What survives is narrower and real: an SDK-COMMITTED project skips the open guards through
    /// <c>EditAnalysisCache</c>, so a command that minted a duplicate id at commit would go unnoticed through
    /// every later edit and through a plain save, and surface only at upload. That is a window in the guard
    /// placement, not a defect on its own — it becomes one only if some command can actually corrupt.</para>
    ///
    /// <para><b>So this asks the question directly</b>, over the widest command sweep the surface allows and
    /// over the corpus projects, with the invariant checked in RELEASE terms (<c>CoreViolation</c> read as a
    /// value, never <c>Debug.Assert</c>) after every single commit. A failure here IS the demonstration the
    /// always-on check would be justified by; a pass is the finding that no such operation exists in the
    /// current command set, re-established on every run rather than written down once and left to go stale.</para>
    /// </summary>
    [TestFixture]
    public sealed class CommitCoreInvariantTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>A session whose every commit is checked against the core invariant, naming the command that
        /// broke it.</summary>
        private sealed class CheckedSession
        {
            private readonly ProjectDocumentSession session = new();
            private readonly List<string> applied = [];

            internal CheckedSession(Project project) => session.Open(project);

            internal Project Current => session.Current!;

            /// <summary>Applies a command and re-checks the invariant. A null command is skipped — several
            /// factory methods answer null when the project cannot host the edit, and that is not this
            /// fixture's subject.</summary>
            internal void Apply(ProjectCommand? command, string label)
            {
                if (command is null)
                {
                    return;
                }

                EditOutcome outcome = session.Apply(command);
                Check(label, outcome.Status);
            }

            /// <summary>The value-producing overload — <c>ProjectCommand&lt;ElementId&gt;</c> binds the generic
            /// <c>Apply</c>, and only that one carries the produced id.</summary>
            internal ElementId Require(ProjectCommand<ElementId> command, string label)
            {
                EditOutcome<ElementId> outcome = session.Apply(command);
                Check(label, outcome.Status);
                return outcome.Value;
            }

            private void Check(string label, EditStatus status)
            {
                applied.Add($"{label} -> {status}");
                if (status == EditStatus.Committed)
                {
                    Committed++;
                }

                Assert.That(ProjectContracts.CoreViolation(session.Current!), Is.Null,
                    $"after {label} (history: {string.Join(", ", applied)})");
            }

            /// <summary>How many commands actually CHANGED the project. Read by the sweep so a factory that
            /// started answering null, or a command that started refusing, turns the sweep red rather than
            /// leaving it green over nothing.</summary>
            internal int Committed { get; private set; }

            /// <summary>Every command applied and what it answered — the sweep's own account of itself.</summary>
            internal IReadOnlyList<string> Applied => applied;
        }

        /// <summary>
        /// The sweep. Every command family that ADDS, COPIES or MOVES a node — which is every way an id can be
        /// minted or duplicated, and so every way this invariant can break — driven in one session over one
        /// project so each commit is checked against the tree the previous ones built.
        /// </summary>
        [Test]
        public async Task NoCommandInTheSweepCommitsAProjectThatBreaksTheCoreInvariant()
        {
            ProjectAppService app = App;
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new CheckedSession(project);

            ElementId locality = session.Require(app.Commands.AddLocality(session.Current, "Ny stue"), "AddLocality");

            // Products: the id-minting path where fresh ids and catalog SEED ids have collided before.
            foreach (ProductDefinition definition in app.GetAvailableProducts().Take(12))
            {
                session.Apply(app.Commands.AddProduct(session.Current, locality, definition),
                    $"AddProduct({definition.ProductIdentifier}/{definition.DisplayName})");
            }

            // Function blocks: a whole sub-tree of ids spliced in from a definition file.
            session.Apply(app.Commands.AddEmptyFunctionBlock(session.Current, locality, "Tom blok"), "AddEmptyFunctionBlock");
            foreach (string masterType in app.GetAvailableFunctionBlocks().Take(6).Select(b => b.MasterType).Distinct())
            {
                session.Apply(app.Commands.AddFunctionBlock(session.Current, locality, masterType),
                    $"AddFunctionBlock({masterType})");
            }

            // Enumerator types and variables: ids minted into the project-global tables rather than a locality.
            session.Apply(app.Commands.AddStandaloneEnumType(session.Current, "Tilstand", ["Nat", "Dag"]), "AddStandaloneEnumType");
            session.Apply(app.Commands.AddEnumValue(session.Current, "Tilstand", "Aften"), "AddEnumValue");

            // Copy and move: the two operations that place an EXISTING sub-tree somewhere else, and the ones a
            // duplicate id would come from if the copy re-used its source's tokens.
            ProjectElement newLocality = session.Current.FindById(locality)!;
            foreach (ProjectElement child in newLocality.Children.Take(6).ToList())
            {
                session.Apply(app.Commands.CopyNode(session.Current, child.Id!.Value, locality),
                    $"CopyNode({child.Tag})");
            }

            ElementId secondLocality = session.Require(app.Commands.AddLocality(session.Current, "Anden stue"), "AddLocality#2");
            foreach (ProjectElement child in session.Current.FindById(locality)!.Children.Take(3).ToList())
            {
                session.Apply(app.Commands.MoveNode(session.Current, child.Id!.Value, secondLocality),
                    $"MoveNode({child.Tag})");
            }

            // User texts and project info: the two id-minting paths outside the locality tree entirely.
            session.Apply(app.Commands.AddUserText(session.Current, "En note"), "AddUserText");

            // Delete last, so everything above was checked against a tree that still held it.
            foreach (ProjectElement child in session.Current.FindById(secondLocality)!.Children.Take(3).ToList())
            {
                if (app.Commands.CanDelete(session.Current, child.Id!.Value))
                {
                    session.Apply(app.Commands.DeleteNode(session.Current, child.Id!.Value, cascade: true),
                        $"DeleteNode({child.Tag})");
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(ProjectContracts.CoreViolation(session.Current), Is.Null,
                    "and the project the whole sequence leaves behind is still sound");

                // WITHOUT THIS the sweep is green over nothing: every command factory above may answer null, and
                // every command may refuse, and the invariant check would pass each time by never being asked.
                Assert.That(session.Committed, Is.GreaterThan(20),
                    "the sweep must actually have changed the project: " + string.Join(", ", session.Applied));
                Assert.That(session.Applied.Count(a => a.StartsWith("AddProduct", StringComparison.Ordinal)
                                                       && a.EndsWith("Committed", StringComparison.Ordinal)),
                    Is.GreaterThan(5), "and it must have reached the id-minting product path in particular");
                Assert.That(session.Applied.Count(a => a.StartsWith("CopyNode", StringComparison.Ordinal)
                                                       && a.EndsWith("Committed", StringComparison.Ordinal)),
                    Is.GreaterThan(0), "and the copy path, which is where a duplicated id would come from");
            });
        }

        /// <summary>
        /// The same question of the CORPUS: every committed project the fixtures load must satisfy the invariant
        /// on the way in, so a failure of the sweep above can only be blamed on a command rather than on the
        /// file it started from.
        /// </summary>
        [TestCase("testdata/projects/Project1-SimpelWired.vis")]
        [TestCase("testdata/projects/project3-KompleksWired.vis")]
        public async Task ACorpusProjectSatisfiesTheInvariantBeforeAnyEdit(string path)
        {
            Assert.That(ProjectContracts.CoreViolation(await App.Load(path)), Is.Null);
        }

        /// <summary>
        /// The check itself is not vacuous: a project carrying a duplicate id token IS reported, so a green
        /// sweep above means the commands produced none rather than that nothing was looked at.
        /// </summary>
        [Test]
        public void TheInvariantCheckActuallyDetectsADuplicateId()
        {
            ProjectElement duplicated = Tree.Node("utcs_project", null, [],
                Tree.Node("groups", "_0x20", [],
                    Tree.Node("group", "_0x21", [("name", "A")]),
                    Tree.Node("group", "_0x21", [("name", "B")])));

            Assert.That(ProjectContracts.CoreViolation(new Project(duplicated)),
                Does.Contain("_0x21"), "a duplicate id token is what this invariant is about");
        }
    }
}
