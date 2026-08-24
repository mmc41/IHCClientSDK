using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Every refusal the SDK can put in front of an installer is Danish (FR-2.6 / D13).
    /// <para>The refusal channel is user-facing text that happens to live in the engine: <c>EditVerdict.Refuse</c>
    /// reasons and <c>EditRefusedException</c> messages are forwarded to the GUI and shown verbatim, rather than
    /// re-authored there. An English sentence in this channel is not an internal detail — it is English text on a
    /// Danish screen.</para>
    /// <para>Asserted over the SOURCE rather than by provoking each refusal one at a time: there are refusals only
    /// reachable through states this suite cannot easily build (a corrupt typedef, a section tag no builder emits),
    /// and those are exactly the ones a per-case test set would quietly omit. The per-condition behaviour tests
    /// below cover the reachable ones with their actual sentences.</para>
    /// </summary>
    public class RefusalLanguageTests
    {
        /// <summary>
        /// The refusal-carrying constructs and the literal each hands over. Deliberately NOT anchored on the quote
        /// immediately following the parenthesis: a refusal is often composed
        /// (<c>Refuse(SomeReason(…) ?? "fallback")</c>) or wrapped onto the next line, and an anchored pattern sees
        /// neither — it reads as a clean pass over text it never looked at.
        /// <para>The gap stops at a semicolon (<c>[^";]*</c>) rather than running to the first quote anywhere ahead.
        /// Unbounded it reaches past the end of the statement, and since a probe spans two lines, a refusal carrying
        /// no literal at all would be blamed for whatever string sits beside it — measured:
        /// <c>Refuse(SomeReason(id)); logger.LogDebug("The element was rejected");</c> reported the log message as
        /// an English refusal. A false alarm here breaks the build over correct code, which is worse than the miss
        /// it would take to fix.</para>
        /// <para>THREE constructs, not two: a refusal can also be built as the outcome itself
        /// (<c>new EditOutcome(EditStatus.Refused, label, "…", null)</c>), which names neither <c>Refuse</c> nor the
        /// exception and so was invisible to every scan here. That is not a hypothetical shape — it is how the
        /// session answers "nothing is open" and "the project moved under you", and both sentences were English
        /// while the rest of the channel was Danish. The status is matched inside the call rather than as a bare
        /// <c>EditStatus.Refused</c>, so the many doc-comment and comparison mentions of the enum are not
        /// candidates at all.</para>
        /// </summary>
        private static readonly Regex RefusalLiteral = new(
            @"(?:(?:EditVerdict\.Refuse|EditRefusedException)\(|EditOutcome(?:<[^>]*>)?\(EditStatus\.Refused,)[^"";]*\$?""([^""]*)""",
            RegexOptions.Compiled);

        /// <summary>How an English refusal opens (the original gate's list, backlog T015).</summary>
        private static readonly string[] EnglishOpeners =
            ["The ", "A ", "An ", "That ", "This ", "Not ", "No ", "is not ", "cannot ", "must ", "already ", "does not "];

        /// <summary>
        /// English that gives a refusal away mid-sentence, for the ones that open on an interpolated value or a
        /// domain word Danish shares. Every phrase here is chosen NOT to be Danish — which is why "at", "position",
        /// "under" and "is" (ice) are deliberately absent, and why each carries its spaces.
        /// </summary>
        private static readonly string[] EnglishFragments =
            [" has no ", " have no ", " no longer ", " is not ", " are not ", " does not ", " cannot ", " must be ",
             " already ", " so it ", " it cannot "];

        /// <summary>
        /// A noun handed to one of the <c>EditContext</c> guards, which splices it into a Danish sentence
        /// (<c>"{noun} findes ikke længere."</c>, <c>"Målet er ikke {noun}."</c>). This is the position the scan
        /// used to be blind to: such a noun never appears next to the word <c>Refuse</c>, so no amount of widening
        /// the refusal pattern could ever have found one. Danish nouns here start with a definite capital
        /// ("Elementet") or an indefinite article ("en"/"et"), so an English article is the giveaway.
        /// </summary>
        private static readonly Regex EnglishGuardNoun = new(
            @"Require(?:Exists|Tag|UnlockedTag)\([^,]+,\s*""(a |an |the )", RegexOptions.Compiled);

        /// <summary>
        /// The nouns that land at the START of their composed sentence — <c>RequireExists</c> on the Evaluate side
        /// and <c>ProjectEditor.Resolve</c> on the Execute side, which share the template
        /// <c>"{noun} findes ikke længere."</c>.
        /// <para>The <c>Resolve</c> half is the shape <see cref="EnglishGuardNoun"/> cannot judge: its nouns are
        /// definite forms, so they carry no article to give an English one away — the six that were English
        /// ("pin", "stored value", "scene member", "element", "scenes container", "enum variable") would all have
        /// passed an article test. What they share instead is that the sentence template needs a CAPITAL there, and
        /// each of them was lowercase — as was "felt", which was Danish but still produced "felt findes ikke
        /// længere." So the checkable rule is capitalization, and it happens to catch every one of them.</para>
        /// <para>Both arguments are matched as a literal second argument, so the unrelated <c>Resolve</c> overloads
        /// in this subtree (<c>ProductCatalogLookup</c>, <c>DefaultReportIcons</c> — neither takes a literal there)
        /// are not candidates at all.</para>
        /// </summary>
        private static readonly Regex SentenceInitialNoun = new(
            @"(?:\.Resolve|RequireExists)\([^,""]+,\s*""([^""]*)""", RegexOptions.Compiled);

        /// <summary>
        /// A refusal lifted out of its construct into a named constant, which every pattern above is blind to by
        /// construction: they match the sentence AT the <c>Refuse</c>/<c>throw</c>/outcome call, and a constant puts
        /// it somewhere else entirely. That is not a hypothetical either — the two sentences the session answers
        /// "nothing is open" and "the project moved under you" with are exactly this shape, named so both doors can
        /// forward one wording (D13).
        /// <para>Matched by NAME convention, the same trade the report-writer rule makes: a constant named
        /// <c>…Refusal…</c> is claiming to be one, so it is held to the language rule. A refusal parked in a
        /// differently-named constant still escapes — the convention is the limit of what a source scan can know,
        /// which is why the composed-sentence behaviour tests below exist alongside it.</para>
        /// </summary>
        private static readonly Regex NamedRefusalConstant = new(
            @"\b(?:const|readonly)\s+string\s+\w*Refusal\w*\s*=\s*\$?""([^""]*)""", RegexOptions.Compiled);

        // Tests run from bin/, so the SDK sources are located through the suite's one repo-root locator (anchored
        // on the solution file) rather than a second walk anchored on a directory that could disagree with it.
        private static IEnumerable<string> SdkSources() =>
            Directory.EnumerateFiles(
                Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis"),
                "*.cs", SearchOption.AllDirectories);

        // A refusal construct may wrap onto the following line, so each line is probed together with the one after
        // it. A hit is reported only when it BEGINS in the probe's own line (<see cref="Probe.OwnLength"/>),
        // otherwise every wrapped construct would be reported twice — once for its own line and once for the
        // preceding line's lookahead.
        private readonly record struct Probe(string Where, string Text, int OwnLength)
        {
            internal bool StartsHere(int index) => index < OwnLength;
        }

        private static IEnumerable<Probe> Probes()
        {
            foreach (string file in SdkSources())
            {
                foreach (Probe probe in ProbesFrom(Path.GetFileName(file), File.ReadAllLines(file)))
                {
                    yield return probe;
                }
            }
        }

        // Shared by the real scan and its positive control, so the control exercises the SAME line-pairing and
        // start-here logic rather than a hand-rolled lookalike that could agree for the wrong reason.
        private static IEnumerable<Probe> ProbesFrom(string where, IReadOnlyList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string next = i + 1 < lines.Count ? " " + lines[i + 1].Trim() : string.Empty;
                yield return new Probe($"{where}:{i + 1}  {lines[i].Trim()}", lines[i] + next, lines[i].Length);
            }
        }

        private static bool ReadsAsEnglish(string sentence) =>
            EnglishOpeners.Any(o => sentence.StartsWith(o, StringComparison.Ordinal))
            || EnglishFragments.Any(f => sentence.Contains(f, StringComparison.Ordinal));

        // The scans as reusable functions over a probe sequence — the shape a positive control needs, since nothing
        // can seed a violation into the SDK's real sources.
        private static List<string> EnglishRefusalOffenders(IEnumerable<Probe> probes) =>
            probes.SelectMany(probe => RefusalLiteral.Matches(probe.Text)
                    .Where(match => probe.StartsHere(match.Index) && ReadsAsEnglish(match.Groups[1].Value))
                    .Select(_ => probe.Where))
                .ToList();

        private static List<string> EnglishGuardNounOffenders(IEnumerable<Probe> probes) =>
            probes.Where(probe => EnglishGuardNoun.Match(probe.Text) is { Success: true } noun
                                  && probe.StartsHere(noun.Index))
                .Select(probe => probe.Where)
                .ToList();

        private static List<string> BadSentenceInitialNounOffenders(IEnumerable<Probe> probes) =>
            probes.SelectMany(probe => SentenceInitialNoun.Matches(probe.Text)
                    .Where(match => probe.StartsHere(match.Index)
                                    && (match.Groups[1].Value.Length == 0
                                        || !char.IsUpper(match.Groups[1].Value[0])
                                        || ReadsAsEnglish(match.Groups[1].Value)))
                    .Select(match => $"{probe.Where}   (noun: '{match.Groups[1].Value}')"))
                .ToList();

        private static List<string> EnglishNamedRefusalConstantOffenders(IEnumerable<Probe> probes) =>
            probes.SelectMany(probe => NamedRefusalConstant.Matches(probe.Text)
                    .Where(match => probe.StartsHere(match.Index) && ReadsAsEnglish(match.Groups[1].Value))
                    .Select(match => $"{probe.Where}   (sentence: '{match.Groups[1].Value}')"))
                .ToList();

        [Test]
        public void NoRefusalInTheSdkIsWrittenInEnglish()
        {
            List<string> offenders = EnglishRefusalOffenders(Probes())
                .Concat(EnglishNamedRefusalConstantOffenders(Probes()))
                .ToList();
            Assert.That(offenders, Is.Empty,
                "these refusals are shown to a Danish installer verbatim:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The other half of the same channel, and the half the scan could not see: the guards compose their
        /// sentence from a noun the CALL SITE supplies, so an English noun there produces half-Danish text
        /// ("a stored value findes ikke længere.") without a single English literal being written anywhere near
        /// the word Refuse.
        /// </summary>
        [Test]
        public void NoGuardNounInTheSdkIsEnglish()
        {
            List<string> offenders = EnglishGuardNounOffenders(Probes());
            Assert.That(offenders, Is.Empty,
                "these nouns are spliced into a Danish sentence shown to the installer:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The third position, and the one the two scans above still could not see: a noun spliced in at the START
        /// of its sentence. <c>RequireExists</c> and <c>ProjectEditor.Resolve</c> share the template
        /// <c>"{noun} findes ikke længere."</c>, so the noun opens the sentence and must be a capitalized Danish
        /// definite form ("Elementet", "Klemmen").
        /// <para>Guarding the <c>Resolve</c> half is what makes the require-or-throw resolver's refusal safe to
        /// rely on: it became a refusal shown verbatim to the installer, but its nouns live at nine scattered call
        /// sites that name no guard and carry no article, so nothing in the source scan could reach them. The six
        /// English ones and the lowercase Danish one were all found by hand once — this is what stops the tenth.</para>
        /// </summary>
        [Test]
        public void EverySentenceInitialNounInTheSdk_IsACapitalizedDanishDefinite()
        {
            List<string> offenders = BadSentenceInitialNounOffenders(Probes());
            Assert.That(offenders, Is.Empty,
                "these nouns open the sentence \"{noun} findes ikke længere.\" shown to the installer:\n  "
                + string.Join("\n  ", offenders));
        }

        // The shape the guard-noun scan judges, WITHOUT the English-article test — i.e. every call site the scan is
        // supposed to be looking at. Its offender pattern legitimately matches nothing today, so only this can tell
        // "no English nouns" apart from "the guard was renamed and the scan now looks at nothing".
        private static readonly Regex GuardNounCandidate = new(
            @"Require(?:Exists|Tag|UnlockedTag)\([^,]+,\s*""([^""]*)""", RegexOptions.Compiled);

        // One seeded violation of each shape the three scans exist to catch, plus the Danish counterpart of each.
        // Written as source lines because these scans read source, and run through the SAME probe pipeline.
        private static readonly string[] SeededEnglishRefusals =
        [
            @"return EditVerdict.Refuse(""The locality cannot be deleted."");",
            @"throw new EditRefusedException(""A pin is not free."");",
            @"return new EditOutcome(EditStatus.Refused, label, ""No project is open."", null);",
            @"return EditVerdict.Refuse(",          // the WRAPPED form: construct here, literal on the next line
            @"    ""This element cannot be deleted."");",
        ];

        private static readonly string[] SeededDanishRefusals =
        [
            @"return EditVerdict.Refuse(""Elementet kan ikke slettes."");",
            @"throw new EditRefusedException(""Klemmen findes ikke længere."");",
            @"return new EditOutcome(EditStatus.Refused, label, ""Der er ikke åbnet et projekt."", null);",
        ];

        private static readonly string[] SeededEnglishRefusalConstants =
        [
            @"public const string NoProjectOpenRefusal = ""No project is open."";",
            @"private static readonly string StaleRefusalMessage = ""The project changed since this edit was prepared."";",
        ];

        private static readonly string[] SeededDanishRefusalConstants =
        [
            @"public const string NoProjectOpenRefusal = ""Der er ikke åbnet et projekt."";",
        ];

        private static readonly string[] SeededEnglishGuardNouns =
        [
            @"context.RequireUnlockedTag(ProgramId, ""a program"", ""program_simple"");",
            @"context.RequireTag(MemberId, ""an enum variable"", ""resource_enum"");",
        ];

        private static readonly string[] SeededDanishGuardNouns =
        [
            @"context.RequireUnlockedTag(ProgramId, ""et program"", ""program_simple"");",
        ];

        private static readonly string[] SeededBadSentenceNouns =
        [
            @"ElementRef handle = editor.Resolve(Id, ""element"");",                 // lowercase
            @"return context.RequireExists(ScenesId, ""The container"");",           // capitalized but English
        ];

        private static readonly string[] SeededGoodSentenceNouns =
        [
            @"ElementRef handle = editor.Resolve(Id, ""Elementet"");",
            @"return context.RequireExists(ScenesId, ""Scenarie-beholderen"");",
        ];

        /// <summary>
        /// The positive control the source scans above need, and the check that they are still pointed at something.
        /// <para>Every one of them asserts an EMPTY offender list, which is exactly what a scan that has quietly
        /// stopped matching reports. That is not hypothetical here: the previous revision of this gate was measured
        /// to be blind to three whole positions (a composed refusal, a guard noun, a sentence-initial noun) and
        /// passed green the entire time. A rename of <c>EditVerdict.Refuse</c>, <c>RequireUnlockedTag</c> or
        /// <c>Resolve</c> would do the same again.</para>
        /// <para>So two things are pinned: each scan REPORTS its seeded violation and stays silent on the Danish
        /// counterpart (it discriminates, rather than flagging everything or nothing), and each scan still finds
        /// real candidates in more than one SDK file (it is looking at live constructs, not renamed ones). The
        /// file-count floor rather than a match-count floor keeps this stable against ordinary editing while still
        /// failing loudly on a rename, which takes every site out at once.</para>
        /// </summary>
        [Test]
        public void TheSourceScansAreArmed()
        {
            var sdkFiles = SdkSources().ToList();
            List<Probe> sdkProbes = Probes().ToList();

            int FilesWith(Regex pattern) => sdkProbes
                .Where(probe => pattern.Match(probe.Text) is { Success: true } m && probe.StartsHere(m.Index))
                .Select(probe => probe.Where.Split(':')[0])
                .Distinct()
                .Count();

            Assert.Multiple(() =>
            {
                Assert.That(sdkFiles, Is.Not.Empty, "the scan found no SDK sources at all — it is reading the wrong place");

                Assert.That(EnglishRefusalOffenders(ProbesFrom("seeded", SeededEnglishRefusals)), Has.Count.EqualTo(4),
                    "the refusal scan must report all three constructs, including the wrapped one");
                Assert.That(EnglishRefusalOffenders(ProbesFrom("seeded", SeededDanishRefusals)), Is.Empty,
                    "the refusal scan must leave the Danish counterparts alone");

                Assert.That(EnglishNamedRefusalConstantOffenders(ProbesFrom("seeded", SeededEnglishRefusalConstants)),
                    Has.Count.EqualTo(2),
                    "the constant scan must report an English refusal parked in a const and in a static readonly");
                Assert.That(EnglishNamedRefusalConstantOffenders(ProbesFrom("seeded", SeededDanishRefusalConstants)),
                    Is.Empty, "the constant scan must leave a Danish refusal constant alone");

                Assert.That(EnglishGuardNounOffenders(ProbesFrom("seeded", SeededEnglishGuardNouns)), Has.Count.EqualTo(2),
                    "the guard-noun scan must report an English article in the noun position");
                Assert.That(EnglishGuardNounOffenders(ProbesFrom("seeded", SeededDanishGuardNouns)), Is.Empty,
                    "the guard-noun scan must leave a Danish indefinite alone");

                Assert.That(BadSentenceInitialNounOffenders(ProbesFrom("seeded", SeededBadSentenceNouns)), Has.Count.EqualTo(2),
                    "the sentence-initial scan must report both a lowercase noun and an English one");
                Assert.That(BadSentenceInitialNounOffenders(ProbesFrom("seeded", SeededGoodSentenceNouns)), Is.Empty,
                    "the sentence-initial scan must leave a capitalized Danish definite alone");

                Assert.That(FilesWith(RefusalLiteral), Is.GreaterThan(1),
                    "no refusal literals were found in more than one SDK file — the refusal constructs were renamed and this gate is watching nothing");
                Assert.That(FilesWith(GuardNounCandidate), Is.GreaterThan(1),
                    "no guard nouns were found in more than one SDK file — the EditContext guards were renamed and that scan is watching nothing");
                Assert.That(FilesWith(SentenceInitialNoun), Is.GreaterThan(1),
                    "no sentence-initial nouns were found in more than one SDK file — Resolve/RequireExists were renamed and that scan is watching nothing");
                Assert.That(FilesWith(NamedRefusalConstant), Is.GreaterThan(0),
                    "no named refusal constants were found — the naming convention this scan matches on no longer describes the code");
            });
        }

        /// <summary>
        /// The source scan above can only see literals. This proves the composed sentence — guard noun plus guard
        /// template — comes out as Danish prose and not as "Locality findes ikke længere".
        /// </summary>
        [Test]
        public async Task AStaleIdRefusal_ReadsAsOneDanishSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId.TryParse("_0xdead01", out ElementId absent);

            EditOutcome outcome = session.Apply(new RenameLocality(absent, "X", ""));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                // RenameLocality passes the generic noun (it renames any named element, not only a locality),
                // so the composed sentence is "Elementet findes ikke længere." — Danish prose either way.
                Assert.That(outcome.Reason, Is.EqualTo("Elementet findes ikke længere."));
            });
        }

        /// <summary>
        /// The Execute-side twin of <see cref="AStaleIdRefusal_ReadsAsOneDanishSentence"/>, and the same sentence.
        /// <para>A stale id can also be reached INSIDE a command's Execute, where the pre-edit legality check could
        /// not have seen it: a <see cref="CompositeCommand"/> evaluates every part against the PRE-EDIT project, so
        /// a part targeting an element an earlier part deletes still looks perfectly legal. The require-or-throw
        /// resolver is what meets the miss, and it must refuse in Danish — the same sentence the Evaluate-side guard
        /// composes — instead of failing with an English engine message. Whether the installer's edit was bundled
        /// or applied one part at a time must not change what language they are answered in.</para>
        /// </summary>
        [Test]
        public async Task AStaleIdRefusalRaisedInsideExecute_IsARefusal_InTheSameDanishSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            ElementId doomed = project.Groups.First().Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new CompositeCommand("Slet og omdøb",
                [new DeleteLocality(doomed), new RenameLocality(doomed, "efter", "")]));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "a stale id is an expected condition wherever it is met, not an engine fault");
                Assert.That(outcome.Reason, Is.EqualTo("Elementet findes ikke længere."),
                    "the Execute-side resolver composes the same Danish sentence as the Evaluate-side guard");
            });
        }

        /// <summary>The tag guard's composed sentence, same reasoning.</summary>
        [Test]
        public async Task AWrongTagRefusal_ReadsAsOneDanishSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;   // a locality, not a function block
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new UnlockFunctionBlock(locality, "me", new System.DateOnly(2026, 1, 1)));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo("Målet er ikke en funktionsblok."));
            });
        }

        /// <summary>The out-of-range terminal refusal, which has its own sentence rather than a shared guard's.</summary>
        [Test]
        public async Task AnOutOfRangeTerminalRefusal_IsDanish()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            ProjectElement pin = project.Root.Descendants()
                .First(e => e.Tag is "dataline_input" or "dataline_output" && e.Id is not null);
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new UpdatePin(pin.Id!.Value,
                new PinPropertiesResult(DataLine: 99, Terminal: 99, "", "", InitialValueOn: false)));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo("Klemmenummeret ligger uden for datalinjens område."));
            });
        }

        /// <summary>
        /// The locked-block refusal is shared by the engine throw and the session verdict (T003), so it is the one
        /// sentence most likely to be re-authored in the GUI. Pinning its exact text is what makes T016's deletion
        /// of the GUI copies safe.
        /// </summary>
        [Test]
        public async Task TheLockedBlockRefusal_IsDanish_AndIsTheSameSentenceEverywhere()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project2-CustomBlock.vis");
            ProjectElement locked = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            ProjectElement inside = locked.Descendants().First(e => e.Id is not null && e.Tag.StartsWith("resource_"));
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new DeleteNode(inside.Id!.Value, Cascade: false));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("låst funktionsblok"));
                Assert.That(outcome.Reason, Does.Contain("lås blokken op"));
            });
        }

        // ── the CODE half, added beside the language half ────────────────────────────────────────────
        //
        // Every assertion above pins a SENTENCE and not one of them changed when the refusals gained codes.
        // That was the constraint: a code is an identity a caller can act on, and adding one must not move a
        // single word of what an installer reads. These four assert the other half.

        /// <summary>
        /// Every refusal the SDK itself raises names its code. The scan is over SOURCE for the same reason the
        /// language scan is: some refusals are only reachable through states this suite cannot build, and those
        /// are exactly the ones a per-case set would omit.
        /// </summary>
        [Test]
        public void NoRefusalInTheSdkIsRaisedWithoutACode()
        {
            string root = TestRepository.RequireRoot();
            List<string> anonymous = [];
            foreach (string path in Directory.EnumerateFiles(
                Path.Combine(root, "ihcclient", "src", "vis"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(
                    File.ReadAllText(path), @"EditVerdict\.Refuse\(\s*[$@]?"""))
                {
                    anonymous.Add($"{Path.GetFileName(path)}: {match.Value}");
                }
            }

            Assert.That(anonymous, Is.Empty,
                "an SDK refusal must name its code — the uncoded overload exists for a HOST, which owns its own "
                + "family, and must not become the easy path back to anonymous refusals inside the engine. Found:"
                + Environment.NewLine + string.Join(Environment.NewLine, anonymous));
        }

        /// <summary>
        /// The scan above is armed: a refusal written the anonymous way is caught. Without this the check reads
        /// as a clean pass over a pattern that might match nothing at all.
        /// </summary>
        [Test]
        public void TheCodeScanIsArmed()
        {
            const string anonymous = @"return EditVerdict.Refuse(""En afvisning uden kode."");";
            const string coded = @"return EditVerdict.Refuse(EditRefusalCodes.TargetMissing, ""Med kode."");";
            Regex probe = new(@"EditVerdict\.Refuse\(\s*[$@]?""");

            Assert.Multiple(() =>
            {
                Assert.That(probe.IsMatch(anonymous), Is.True, "the seeded anonymous refusal is caught");
                Assert.That(probe.IsMatch(coded), Is.False, "and a coded one is not");
            });
        }

        /// <summary>
        /// A refused verdict carries its code all the way to the caller, so two paths answering the same question
        /// can be compared by identity rather than by comparing two Danish sentences that happen to match.
        /// </summary>
        [Test]
        public async Task ARefusedVerdictCarriesItsCodeAlongsideItsSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId.TryParse("_0xdead01", out ElementId absent);

            EditVerdict verdict = session.CanApply(new RenameLocality(absent, "Nyt", string.Empty));

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False);
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.TargetMissing));
                Assert.That(verdict.Reason, Does.Contain("findes ikke længere"),
                    "and the sentence is untouched — the code is beside it, not instead of it");
            });
        }

        /// <summary>
        /// The catalogue's Danish template for a refusal says the same thing the site says. Checked on the codes
        /// whose sentence carries no interpolated value, where "the same thing" is exact equality; the rest are
        /// pinned sentence-by-sentence by the behaviour tests above.
        /// </summary>
        [Test]
        public void ACatalogueTemplateSaysWhatItsRefusalSiteSays()
        {
            (ProblemCode Code, string Sentence)[] fixedSentences =
            [
                (EditRefusalCodes.NoProjectOpen, EditRefusals.NoProjectOpenRefusal),
                (EditRefusalCodes.StaleBaseVersion, EditRefusals.StaleBaseVersionRefusal),
                (EditRefusalCodes.TerminalMissing, "Klemmen findes ikke længere."),
                (EditRefusalCodes.TerminalAddressRange, "Klemmenummeret ligger uden for datalinjens område."),
                (EditRefusalCodes.LinkDirection, "De to klemmer kan ikke linkes i den retning."),
                (EditRefusalCodes.MoveNotAllowed, "Den flytning er ikke tilladt."),
                (EditRefusalCodes.ContainerRejectsNode, "Den beholder kan ikke rumme denne node."),
                (EditRefusalCodes.NotALogRow, "Ikke en Logning-række."),
                (EditRefusalCodes.NotACommandGroup, "Målet er ikke en kommandogruppe."),
            ];

            Assert.Multiple(() =>
            {
                foreach ((ProblemCode code, string sentence) in fixedSentences)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True, code.Value);
                    Assert.That(entry.MessageTemplate, Is.EqualTo(sentence),
                        code.Value + ": the catalogue template and the refusing site must not drift apart");
                }
            });
        }
    }
}
