using System;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The metamorphic law of <see cref="CompositeCommand"/>: bundling N parts into ONE gesture must reach the same
    /// project as applying those N parts one at a time — for parts that are INDEPENDENT, i.e. whose preconditions
    /// hold against the pre-edit project and do not depend on an earlier part having run. A composite evaluates
    /// every part against the pre-edit context and is all-or-nothing, so for DEPENDENT parts the two paths must
    /// legitimately differ; <see cref="DependentParts_DivergeBetweenTheTwoPaths_AndTheComparisonSeesIt"/> pins that
    /// divergence and doubles as this file's armed detector.
    /// </summary>
    /// <remarks>
    /// <b>This class is the metamorphic pattern for this repo — cite it, do not re-derive it.</b> Five choices, each
    /// load-bearing:
    /// <list type="number">
    /// <item><b>A MUTABLE carrier is mandatory.</b> <c>Check.SampleMetamorphic</c> drives both paths as
    /// <c>Action&lt;TCarrier, TParam&gt;</c> returning void: it builds one carrier per path from the initial
    /// generator, mutates each in place, then compares the two carriers. An IMMUTABLE carrier can record nothing, so
    /// the two compared values stay identical and the property passes VACUOUSLY — measured: a carrier of <c>int</c>
    /// with the contradictory paths <c>s += i</c> / <c>s -= i</c> passes. The carrier here is
    /// <see cref="ProjectDocumentSession"/>, whose <see cref="ProjectDocumentSession.Current"/> is the state under
    /// test (the <see cref="Project"/> inside it stays immutable — the SESSION is what mutates).</item>
    /// <item><b>An explicit <c>equal:</c> is mandatory.</b> The default is <c>Check.ModelEqual</c>, which for a
    /// carrier that is neither a list nor a value type falls through to <c>object.Equals</c> — reference equality,
    /// which two per-path carriers never satisfy, so the default would fail EVERY iteration on sight. This compares
    /// what the repo means by "the same project": the SERIALIZED BYTES (<see cref="SameSerializedBytes"/>), the
    /// same currency the oracle corpus is pinned in. (The vacuous pass above and this immediate red are the two
    /// halves of the same trap: whether an unusable carrier reads green or red is decided by <c>equal:</c>.)</item>
    /// <item><b>Both paths get IDENTICAL command objects.</b> The generator produces the parts from the BASE
    /// project's ids — never from the carrier's live state — so the two paths cannot drift apart by reading
    /// different projects while choosing what to do.</item>
    /// <item><b>Independence is by CONSTRUCTION, not by filtering.</b> <c>Gen.Shuffle(ids, n)</c> hands out n
    /// DISTINCT locality ids in random order and each part consumes one, so no part can target an element another
    /// part renamed or deleted. That is exactly the precondition D09 names.</item>
    /// <item><b><c>threads: 1</c></b>, matching the other property tests here
    /// (<c>SharingPreservingCommitPropertyTests</c>, <c>RebuildEquivalenceOracle</c>): the default is
    /// <c>Environment.ProcessorCount</c>, so iterations otherwise run concurrently — and a shrunk counterexample
    /// must be reproducible from its printed seed.</item>
    /// </list>
    /// <para><b>One pair per iteration, not a sequence.</b> Unlike the model-based sampler, a metamorphic iteration
    /// runs exactly ONE operation pair; only the initial state and that one parameter shrink. A multi-step relation
    /// like this one must therefore be folded INTO the two path lambdas — which is why the generated parameter is a
    /// whole <c>ProjectCommand[]</c> rather than a single command.</para>
    /// <para><b>The initial generator must be deterministic.</b> The sampler generates the two carriers back to
    /// back from a replayed seed, so any wall-clock or counter nondeterminism would make them differ BEFORE either
    /// path runs. Both carriers here wrap the same already-loaded, immutable base <see cref="Project"/>, so there
    /// is none.</para>
    /// </remarks>
    public class CompositeCommandMetamorphicTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private const string BaseProject = "testdata/projects/project3-KompleksWired.vis";

        // Wide enough that a bundle exercises interleaved kinds, small enough that 100 iterations of TWO sessions
        // stay cheap; project3 supplies more top-level localities than this, so Gen.Shuffle can always serve it.
        private const int MaxParts = 5;

        private enum PartKind { Add, Rename, Delete }

        private static readonly Gen<string> NameGen =
            Gen.OneOfConst("abcæø 09".ToCharArray()).Array[1, 5].Select(cs => new string(cs));

        /// <summary>The mutable carrier (pattern choice 1): a fresh session opened on the shared, immutable base
        /// project. One is built per path per iteration.</summary>
        private static Gen<ProjectDocumentSession> Sessions(Project baseProject) =>
            Gen.Const(() =>
            {
                var session = new ProjectDocumentSession();
                session.Open(baseProject);
                return session;
            });

        /// <summary>The explicit equality (pattern choice 2): two sessions are the same when their current
        /// projects serialize to the same bytes.</summary>
        private static bool SameSerializedBytes(ProjectDocumentSession a, ProjectDocumentSession b) =>
            ProjectSerializer.Serialize(a.Current!).AsSpan()
                .SequenceEqual(ProjectSerializer.Serialize(b.Current!));

        private static string Describe(ProjectDocumentSession session) =>
            session.Current is { } project
                ? $"luid={project.LastUniqueId} localities=[{string.Join(", ",
                    project.Groups.Select(g => g.GetAttribute("name")))}]"
                : "<no project>";

        /// <summary>Independent parts (pattern choices 3 and 4): n distinct locality ids, one consumed per part, so
        /// every part's precondition is settled by the pre-edit project alone.</summary>
        private static Gen<ProjectCommand[]> IndependentParts(Project baseProject)
        {
            ElementId[] localities = [.. baseProject.Groups.Select(g => g.Id!.Value)];
            return Gen.Int[0, Math.Min(MaxParts, localities.Length)].SelectMany(count =>
                Gen.Select(
                    Gen.Shuffle(localities, count),
                    Gen.OneOfConst(PartKind.Add, PartKind.Rename, PartKind.Delete).Array[count],
                    NameGen.Array[count],
                    (targets, kinds, names) => Compose(targets, kinds, names)));
        }

        private static ProjectCommand[] Compose(ElementId[] targets, PartKind[] kinds, string[] names)
        {
            var parts = new ProjectCommand[targets.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = kinds[i] switch
                {
                    // Add ignores its target but still consumes one, which only widens the distinctness margin.
                    PartKind.Add => new AddLocality(names[i]),
                    PartKind.Rename => new RenameLocality(targets[i], names[i], string.Empty),
                    _ => new DeleteLocality(targets[i]),
                };
            }
            return parts;
        }

        private static void ApplyAsOneComposite(ProjectDocumentSession session, ProjectCommand[] parts) =>
            session.Apply(new CompositeCommand("Sammensat handling", [.. parts]));

        private static void ApplyOneAtATime(ProjectDocumentSession session, ProjectCommand[] parts)
        {
            foreach (ProjectCommand part in parts)
            {
                session.Apply(part);
            }
        }

        [Test]
        public async Task OneComposite_OverIndependentParts_EqualsApplyingThemOneAtATime()
        {
            Project baseProject = await App.Load(BaseProject);

            Sessions(baseProject).SampleMetamorphic(
                IndependentParts(baseProject).Metamorphic<ProjectDocumentSession>(
                    parts => string.Join(" + ", parts.Select(p => p.ToString())),
                    ApplyAsOneComposite,
                    ApplyOneAtATime),
                equal: SameSerializedBytes,
                print: Describe,
                iter: 100,
                threads: 1);
        }

        /// <summary>
        /// The other half of D09, and the proof that the law above is not vacuous: two parts over the SAME target —
        /// delete it, then rename it — are DEPENDENT, and the two paths must part company. The composite evaluates
        /// the rename against the pre-edit project, where the target still exists, so nothing warns it; the bundle
        /// then fails as a unit and commits NOTHING, while the sequence commits the delete and only then refuses the
        /// rename. Asserted on the resulting PROJECTS (not on the outcome wording), which is the same comparison the
        /// property above uses — so this pins that the comparison can, in fact, fail.
        /// </summary>
        [Test]
        public async Task DependentParts_DivergeBetweenTheTwoPaths_AndTheComparisonSeesIt()
        {
            Project baseProject = await App.Load(BaseProject);
            ElementId doomed = baseProject.Groups[0].Id!.Value;
            ProjectCommand[] dependent = [new DeleteLocality(doomed), new RenameLocality(doomed, "efter", string.Empty)];

            var asComposite = new ProjectDocumentSession();
            asComposite.Open(baseProject);
            ApplyAsOneComposite(asComposite, dependent);

            var oneAtATime = new ProjectDocumentSession();
            oneAtATime.Open(baseProject);
            ApplyOneAtATime(oneAtATime, dependent);

            Assert.Multiple(() =>
            {
                Assert.That(asComposite.Current!.Groups.Select(g => g.Id), Has.Member(doomed),
                    "the all-or-nothing bundle committed nothing — the doomed locality is still there");
                Assert.That(oneAtATime.Current!.Groups.Select(g => g.Id), Has.No.Member(doomed),
                    "the sequence committed what it could — the delete stands, only the rename was refused");
                Assert.That(SameSerializedBytes(asComposite, oneAtATime), Is.False,
                    "the byte comparison the metamorphic property runs on DOES separate these two paths");
            });
        }
    }
}
