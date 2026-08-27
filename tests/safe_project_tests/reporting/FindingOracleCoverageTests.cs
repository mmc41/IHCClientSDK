using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What the new format records that the tab-separated recording could not.
    ///
    /// <para><b>Why this is a task of its own.</b> The recording held six cells per finding, so the arguments,
    /// the related sites and the exact node paths were simply absent from it — 48 grouped findings lost every
    /// site but the first, and nothing anywhere pinned which findings needed a path. The byte gate now protects
    /// all of it, but only against CHANGE: it would go on passing if the writer had never emitted a single
    /// <c>@related</c>. These assertions say what should be there in the first place.</para>
    ///
    /// <para><b>They read the XML directly rather than through <see cref="RecordedFinding"/>.</b> That
    /// projection deliberately carries the six cells every reader shares; the attributes below are exactly the
    /// ones it does not, so this is the one place that looks at the raw elements.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingOracleCoverageTests
    {
        /// <summary>One emitted finding element, with the case it came from.</summary>
        private sealed record Line(string Case, XElement Element)
        {
            public string? Value(string attribute) => Element.Attribute(attribute)?.Value;

            public IEnumerable<string> Names => Element.Attributes().Select(a => a.Name.LocalName);

            public string Describe() => $"{Case} {Value("code")} @{Value("locator") ?? "<none>"}";
        }

        private static ImmutableArray<Line> Lines { get; } = Load();

        private static ImmutableArray<Line> Load()
        {
            var lines = ImmutableArray.CreateBuilder<Line>();
            foreach (string path in FindingOracleHarness.Files())
            {
                string caseName = FindingOracleHarness.CaseNameIn(path);
                using FileStream stream = File.OpenRead(path);
                foreach (XElement finding in XDocument.Load(stream).Root!.Elements("finding"))
                {
                    lines.Add(new Line(caseName, finding));
                }
            }

            return lines.ToImmutable();
        }

        /// <summary>Non-vacuity for everything below.</summary>
        [Test]
        public void TheWholeCorpusIsLoaded() => Assert.That(Lines, Has.Length.EqualTo(833));

        // ----- related sites -----

        /// <summary>
        /// Related sites reach the file at all — the 48 grouped findings whose other sites the recording simply
        /// dropped. Asserted as a population, so this fails if the writer ever stops emitting them.
        /// </summary>
        [Test]
        public void GroupedFindingsCarryTheirRelatedSites()
        {
            ImmutableArray<Line> grouped = [.. Lines.Where(l => l.Value("related") is not null)];

            Assert.Multiple(() =>
            {
                Assert.That(grouped, Is.Not.Empty, "at least one file carries a related-site list");
                Assert.That(
                    grouped.Select(l => l.Value("related")!), Is.All.Not.Empty,
                    "an emitted list is never an empty one — a finding with no related sites omits the attribute");
            });
        }

        /// <summary>
        /// The measured shape, named: the duplicate-id collision in <c>synthetic/ids</c> lists the second holder
        /// of the shared token. Both sites carry the SAME locator, which is the whole reason the collision needs
        /// paths to be navigable at all.
        /// </summary>
        [Test]
        public void TheDuplicateTokenFindingListsTheOtherHolder()
        {
            Line duplicate = Lines.Single(
                l => l.Case == "synthetic/ids" && l.Value("code") == "id-duplicate-token");

            Assert.Multiple(() =>
            {
                Assert.That(duplicate.Value("locator"), Is.EqualTo("_0x2132"));
                Assert.That(duplicate.Value("related"), Is.EqualTo("_0x2132"),
                    "the other holder answers to the same token, which is what makes it a collision");
                Assert.That(duplicate.Value("xpath"), Is.EqualTo("/utcs_project/groups/group[1]"));
                Assert.That(duplicate.Value("related_xpath"), Is.EqualTo("/utcs_project/groups/group[2]"),
                    "and the paths are the only thing that tells the two apart");
            });
        }

        /// <summary>
        /// <c>related_xpath</c> pairs POSITIONALLY with <c>related</c>: same count, so entry N of one belongs to
        /// entry N of the other. A shorter list would silently mis-attribute every entry after the gap.
        /// </summary>
        [Test]
        public void RelatedPathsPairPositionallyWithRelatedLocators()
        {
            foreach (Line line in Lines.Where(l => l.Value("related_xpath") is not null))
            {
                Assert.That(
                    line.Value("related_xpath")!.Split(' '), Has.Length.EqualTo(
                        line.Value("related")!.Split(' ').Length),
                    $"{line.Describe()}: the two lists pair by position, so they must be the same length");
            }
        }

        // ----- exact node paths -----

        /// <summary>
        /// Only the measured AMBIGUOUS lines carry a path; a line whose locator is a bare TAG carries none.
        ///
        /// <para>The second half is the one that matters: a tag locator names no id, so the naive rule "emit a
        /// path when the parsed id is null" would put a path on every one of them. They do not need one — a tag
        /// that names one element already selects it. The two populations are counted in the assertion, which is
        /// where a number belongs: a comment restating it drifts silently, the assertion cannot.</para>
        /// </summary>
        [Test]
        public void ExactlyTheAmbiguousLinesCarryAPath()
        {
            ImmutableArray<Line> pathed = [.. Lines.Where(l => l.Value("xpath") is not null)];
            ImmutableArray<Line> tagLocators =
                [.. Lines.Where(l => l.Value("locator") is { } locator && !locator.StartsWith("_0x", StringComparison.Ordinal))];

            Assert.Multiple(() =>
            {
                Assert.That(pathed, Has.Length.EqualTo(6), "under 1% of the corpus");
                Assert.That(tagLocators, Has.Length.EqualTo(60), "non-vacuity: there really are 60 of them");
                Assert.That(
                    tagLocators.Where(l => l.Value("xpath") is not null), Is.Empty,
                    "a tag that names one element already selects it");
                Assert.That(
                    pathed.Select(l => l.Case).Distinct(), Is.EqualTo(new[] { "synthetic/ids" }),
                    "and every one of them is a duplicate or malformed token in the ids case");
            });
        }

        /// <summary>
        /// Every emitted path selects EXACTLY ONE node in its own case's tree — the property that makes it an
        /// identity rather than a hint. A path that selected two would be no better than the ambiguous locator
        /// it exists to replace, and one that selected none would point at nothing at all.
        /// </summary>
        [Test]
        public void EveryEmittedPathSelectsExactlyOneNodeInItsCase()
        {
            var paths = Lines
                .SelectMany(l => Paths(l).Select(p => (l.Case, Path: p, Line: l)))
                .ToImmutableArray();

            Assert.Multiple(() =>
            {
                Assert.That(paths, Is.Not.Empty, "non-vacuity: paths are emitted at all");
                foreach ((string caseName, string path, Line line) in paths)
                {
                    Project project = ValidationCharacterizationTests.Corpus
                        .Single(c => c.Case == caseName).Build();
                    Assert.That(
                        Select(project.Root, path), Is.EqualTo(1),
                        $"{line.Describe()}: '{path}' must select exactly one node");
                }
            });
        }

        private static IEnumerable<string> Paths(Line line)
        {
            if (line.Value("xpath") is { } primary)
            {
                yield return primary;
            }

            foreach (string related in line.Value("related_xpath")?.Split(' ') ?? [])
            {
                yield return related;
            }
        }

        /// <summary>
        /// Evaluates a RESTRICTED positional path — element names plus same-tag sibling indexes — against the
        /// project tree, returning how many nodes it selects.
        /// <para>
        /// Walked directly rather than through an XPath engine because the tree is a
        /// <see cref="ProjectElement"/> graph, not a document: serializing it first would put the serializer's
        /// own rules (and its refusals on synthetic shapes) between this assertion and what it is asserting.
        /// </para>
        /// </summary>
        private static int Select(ProjectElement root, string path)
        {
            string[] steps = path.TrimStart('/').Split('/');
            if (steps.Length == 0 || Name(steps[0]) != root.Tag || Index(steps[0]) is > 1)
            {
                return 0;
            }

            IEnumerable<ProjectElement> current = [root];
            foreach (string step in steps.Skip(1))
            {
                string tag = Name(step);
                int? index = Index(step);
                current =
                [
                    .. current.SelectMany(node =>
                    {
                        ImmutableArray<ProjectElement> matches = [.. node.Children.Where(c => c.Tag == tag)];
                        // No index means the step claims a UNIQUE same-tag child: a step that matched two would
                        // be an ambiguous path, which is exactly what the count below has to reveal.
                        return index is { } position
                            ? matches.Skip(position - 1).Take(1)
                            : matches;
                    }),
                ];
            }

            return current.Count();
        }

        private static string Name(string step) =>
            step.IndexOf('[') is var bracket && bracket >= 0 ? step[..bracket] : step;

        private static int? Index(string step) =>
            step.IndexOf('[') is var bracket && bracket >= 0
                ? int.Parse(step[(bracket + 1)..step.IndexOf(']')], System.Globalization.CultureInfo.InvariantCulture)
                : null;

        // ----- arguments -----

        /// <summary>
        /// Every emitted argument value also appears inside the SAME line's message.
        ///
        /// <para>This is the genuinely new invariant the format buys, and it catches something no other gate
        /// can: a rule binding an argument its own sentence does not use. The two are one datum rendered twice
        /// — the message for a person, the arguments for a machine — and they are only worth having if they
        /// cannot come apart.</para>
        ///
        /// <para>Compared on UNESCAPED values, which is what the parser hands back, so an <c>&amp;</c> or a
        /// Danish character escaped one way in one attribute and another way in the other cannot make an
        /// agreeing pair look different.</para>
        /// </summary>
        [Test]
        public void EveryArgumentValueAppearsInsideItsOwnMessage()
        {
            var problems = new List<string>();
            int checked_ = 0;
            foreach (Line line in Lines)
            {
                string message = line.Value("message")!;
                foreach (XAttribute argument in line.Element.Attributes()
                    .Where(a => a.Name.LocalName.StartsWith("arg_", StringComparison.Ordinal)))
                {
                    checked_++;
                    if (!message.Contains(argument.Value, StringComparison.Ordinal))
                    {
                        problems.Add(
                            $"{line.Describe()}: {argument.Name.LocalName}=\"{argument.Value}\" "
                            + $"does not appear in \"{message}\"");
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(checked_, Is.GreaterThan(100), "non-vacuity: arguments really are emitted");
                Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(20)));
            });
        }

        // ----- the attribute vocabulary -----

        /// <summary>
        /// Every attribute on every finding is either one of the writer's FIXED names or <c>arg_</c> plus a slot
        /// the catalogue declares. Nothing else may appear.
        ///
        /// <para><b>The fixed names are read from the writer's own declaration, not restated here.</b> A list
        /// typed into this test would keep passing after the writer's had changed, and catching an attribute
        /// nobody meant to add is the entire point of the check.</para>
        /// </summary>
        [Test]
        public void EveryAttributeIsAFixedNameOrADeclaredSlot()
        {
            ImmutableHashSet<string> fixedNames = [.. FindingExportWriter.FixedFindingAttributes];
            ImmutableHashSet<string> declared =
            [
                .. ProblemCatalog.Current.Entries.SelectMany(e => e.Slots).Select(s => "arg_" + s.Name),
            ];

            var unknown = Lines
                .SelectMany(l => l.Names.Select(n => (l, n)))
                .Where(x => !fixedNames.Contains(x.n) && !declared.Contains(x.n))
                .Select(x => $"{x.l.Describe()}: unknown attribute '{x.n}'")
                .Distinct()
                .ToImmutableArray();

            Assert.Multiple(() =>
            {
                Assert.That(unknown, Is.Empty, string.Join(Environment.NewLine, unknown.Take(20)));
                Assert.That(
                    Lines.SelectMany(l => l.Names).Distinct().Count(n => n.StartsWith("arg_", StringComparison.Ordinal)),
                    Is.GreaterThan(1),
                    "non-vacuity: several distinct slots really are emitted");
            });
        }

        /// <summary>
        /// And the reverse reading of the same rule: every fixed name the writer declares is actually used
        /// somewhere in the corpus. A declared name nothing emits is either dead or a rule nobody exercises,
        /// and both are worth knowing about.
        /// </summary>
        [Test]
        public void EveryFixedAttributeTheWriterDeclaresIsWitnessedByTheCorpus()
        {
            ImmutableHashSet<string> emitted = [.. Lines.SelectMany(l => l.Names)];

            Assert.That(
                FindingExportWriter.FixedFindingAttributes.Where(n => !emitted.Contains(n)), Is.Empty,
                "every fixed attribute is exercised by at least one corpus finding");
        }
    }
}
