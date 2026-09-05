using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using static Ihc.Vis.Tests.RuleProbe;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T055 — four of the five FUNCTION-BLOCK SHAPE rows, and the fifth's absence.
    ///
    /// <para><b>The fifth row landed later, under D27</b>, and its tests are here beside its siblings:
    /// <c>logic-block-locked-content</c> needs the block's LIBRARY body to tell an edited value from a library
    /// default, so it declares that context and is skipped without it — which is the first thing
    /// <see cref="TheLockedContentRowIsSkippedWithoutALibrary"/> proves.</para>
    ///
    /// <para><b>The duplicate-program signature is tested from both edges</b> — a re-labelled copy still counts,
    /// and a copy with a different operand does not — because a signature that is too loose reports every pair of
    /// similar programs and one that is too tight reports nothing.</para>
    /// </summary>
    [TestFixture]
    public sealed class FunctionBlockShapeRulesTests
    {
        /// <summary>
        /// A library holding one 1.1.01/e block whose named timer stores the given minute — plus, for the port's
        /// type-only half, whatever further identities a caller names. Only 1.1.01/e has a BODY; the extra
        /// identities exist to be asked about, which is exactly the asymmetry a real library has where a rule
        /// wants to know what it holds without wanting to read it.
        /// </summary>
        /// <param name="minute">The minute the library's own timer setting stores.</param>
        /// <param name="named">That timer's name.</param>
        /// <param name="alsoHolding">Further (type, version) identities the library holds, bodies aside.</param>
        private static ILibraryBlockSource Library(
            string minute, string named = "Timer", (string Type, string Version)[]? alsoHolding = null) =>
            new StubLibrary(
                Tree.Node("functionblock", null, [("name", "1.1.01.e. Kip tænd sluk")],
                    Tree.Node("inputs", null, [("name", "Input")]),
                    Tree.Node("outputs", null, [("name", "Output")]),
                    Tree.Node("settings", null, [("name", "Indstillinger")],
                        Tree.Node("resource_timer", null,
                            [("name", named), ("hour", "0"), ("minute", minute), ("second", "0")])),
                    Tree.Node("internalsettings", null, [("name", "Interne")]),
                    Tree.Node("programs", null, [("name", "Programmer")])),
                alsoHolding ?? []);

        private sealed class StubLibrary(ProjectElement body, (string Type, string Version)[] alsoHolding)
            : ILibraryBlockSource
        {
            private readonly ImmutableArray<(string Type, string Version)> held =
                [("1.1.01", "e"), .. alsoHolding];

            public bool TryGetBody(string masterType, string masterVersion, out ProjectElement found)
            {
                found = body;
                return masterType == "1.1.01" && masterVersion == "e";
            }

            public bool TryGetVersions(string masterType, out EquatableArray<string> versions)
            {
                versions =
                [
                    .. this.held.Where(i => i.Type == masterType)
                        .Select(i => i.Version)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                ];
                return versions.Length > 0;
            }
        }

        // ── the library port's type-only half (D02) ─────────────────────────────────────────────────

        /// <summary>
        /// The widened port answers BOTH questions a rule can ask about a master identity without reading a body:
        /// is this TYPE held at all, and at which versions.
        ///
        /// <para><b>Why both, and why one member.</b> They are one lookup with two readings — a miss IS "the
        /// library does not have this type", and a hit carries what it does have. Splitting them into two members
        /// would let a caller ask the second without the first and read an empty answer as a version list.</para>
        ///
        /// <para><b>Why the answer is PLURAL.</b> The built-in library happens to hold every type at exactly one
        /// version today, but that is a property of the data, not of a library: an installed Visual library may
        /// ship two revisions of one block. A single-version return would encode the accident. The order is pinned
        /// ordinal-ascending so a rule binding the answer into a message produces the same sentence on every run.</para>
        /// </summary>
        [Test]
        public void TheLibraryPortAnswersWhetherATypeIsHeldAndAtWhichVersions()
        {
            ILibraryBlockSource library = Library(
                "5", alsoHolding: [("2.1.01", "c"), ("2.1.01", "a"), ("2.1.01", "a")]);

            Assert.Multiple(() =>
            {
                Assert.That(library.TryGetVersions("1.1.01", out EquatableArray<string> single), Is.True,
                    "the identity whose body the library also holds");
                Assert.That(single, Is.EqualTo(new[] { "e" }).AsCollection);

                Assert.That(library.TryGetVersions("2.1.01", out EquatableArray<string> several), Is.True);
                Assert.That(several, Is.EqualTo(new[] { "a", "c" }).AsCollection,
                    "distinct and ordinal-ascending, so a bound argument is stable across runs");

                Assert.That(library.TryGetVersions("9.9.99", out EquatableArray<string> none), Is.False,
                    "a type the library does not hold at ANY version — the question 3.7's row asks");
                Assert.That(none, Is.Empty, "and the out value is an empty list rather than null on a miss");

                Assert.That(library.TryGetBody("2.1.01", "a", out _), Is.False,
                    "holding an identity is not the same as being able to hand out its body");
            });
        }

        /// <summary>
        /// The same two questions against the REAL adapter, over the built-in catalog. The stub above pins the
        /// contract; this pins the only implementation that ships, and the two halves share an index — so a
        /// regression in the version half that also broke body lookup would show here and nowhere else until a
        /// rule consumes it.
        /// </summary>
        [Test]
        public void TheCatalogAdapterAnswersTheSameTwoQuestionsOverTheBuiltInLibrary()
        {
            var catalog = new BuiltInCatalog();
            ILibraryBlockSource library = new Ihc.App.CatalogLibraryBlockSource(() => catalog.FunctionBlocks);

            Assert.Multiple(() =>
            {
                Assert.That(library.TryGetVersions("1.1.01", out EquatableArray<string> held), Is.True);
                Assert.That(held, Is.EqualTo(new[] { "e" }).AsCollection,
                    "the corpus's own master identity, at the one version the built-in library ships it");
                Assert.That(library.TryGetVersions("9.9.99", out EquatableArray<string> none), Is.False,
                    "and a type no catalog block declares");
                Assert.That(none, Is.Empty);

                Assert.That(library.TryGetBody("1.1.01", "e", out ProjectElement body), Is.True,
                    "the body half still answers — the two readings come out of one pass over the definitions");
                Assert.That(body.Tag, Is.EqualTo("functionblock"));
                Assert.That(library.TryGetBody("1.1.01", "a", out _), Is.False,
                    "a version the library does not hold, even though the TYPE is present — which is exactly the "
                    + "distinction the version half exists to make");
            });
        }

        /// <summary>
        /// The adapter and the composite must resolve one identity to the SAME body.
        ///
        /// <para>The composite documents imported-wins and gets it from <c>MaterializedCatalog</c>'s last-wins rule
        /// over an enumeration that lists base components first and imports last. The adapter indexed the very same
        /// enumeration first-wins, calling that a convention — so for a master type and version present in both, the
        /// library port answered with the BUILT-IN body while every other lookup answered with the imported one.</para>
        /// </summary>
        [Test]
        public void TheAdapterResolvesAShadowedIdentityToTheSameBodyTheCompositeDoes()
        {
            var composite = new CompositeCatalog(new BuiltInCatalog());
            FunctionBlockDefinition builtIn = composite.FunctionBlocks
                .First(b => !string.IsNullOrEmpty(b.MasterType) && !string.IsNullOrEmpty(b.MasterVersion));
            FunctionBlockDefinition imported = builtIn with
            {
                DisplayName = "Importeret",
                Body = builtIn.Body.WithAttribute("name", "Importeret"),
            };
            composite.Import(imported);

            ILibraryBlockSource library = new Ihc.App.CatalogLibraryBlockSource(() => composite.FunctionBlocks);

            Assert.Multiple(() =>
            {
                Assert.That(library.TryGetBody(builtIn.MasterType, builtIn.MasterVersion, out ProjectElement body), Is.True);
                Assert.That(body.GetAttribute("name"), Is.EqualTo("Importeret"),
                    "the imported body shadows the built-in one, exactly as the composite's own lookup resolves it");
                Assert.That(composite.FunctionBlocks.Last(b => b.MasterType == builtIn.MasterType
                        && b.MasterVersion == builtIn.MasterVersion).Body.GetAttribute("name"),
                    Is.EqualTo("Importeret"),
                    "and the enumeration the adapter is handed is the one that puts imports last");
            });
        }

        /// <summary>Two versions of one type both stay listed — the VERSIONS half is a separate contract and this
        /// change must not have narrowed it.</summary>
        [Test]
        public void TwoVersionsOfOneTypeBothRemainListed()
        {
            var composite = new CompositeCatalog(new BuiltInCatalog());
            FunctionBlockDefinition builtIn = composite.FunctionBlocks
                .First(b => !string.IsNullOrEmpty(b.MasterType) && !string.IsNullOrEmpty(b.MasterVersion));
            composite.Import(builtIn with { MasterVersion = builtIn.MasterVersion + "z" });

            ILibraryBlockSource library = new Ihc.App.CatalogLibraryBlockSource(() => composite.FunctionBlocks);

            Assert.That(library.TryGetVersions(builtIn.MasterType, out EquatableArray<string> held), Is.True);
            Assert.That(held, Does.Contain(builtIn.MasterVersion).And.Contain(builtIn.MasterVersion + "z"));
        }

        /// <summary>
        /// A library block carrying full master identity whose <c>Timer</c> setting stores the given minute — null
        /// stores nothing at all.
        /// </summary>
        private static Project LockedLibraryBlock(string? storedMinutes, bool locked = true)
        {
            (string, string)[] timer = storedMinutes is null
                ? [("name", "Timer")]
                : [("name", "Timer"), ("hour", "0"), ("minute", storedMinutes), ("second", "0")];
            (string, string)[] identity = locked
                ? [("name", "1.1.01.e. Kip tænd sluk"), ("master_type", "1.1.01"), ("master_version", "e"),
                   ("master_name", "Kip tænd sluk"), ("locked", "yes")]
                : [("name", "1.1.01.e. Kip tænd sluk"), ("master_type", "1.1.01"), ("master_version", "e"),
                   ("master_name", "Kip tænd sluk")];

            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70), identity,
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_input", Token("resource_input", 0x80),
                            [("name", "Indgang"), ("note", "N")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x88), [("name", "Udgang")])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")],
                        Tree.Node("resource_timer", Token("resource_timer", 0x90), timer)),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                        Program(0x9a, "Program", 0x80)))));
        }

        // ── fb-revision-defective-confirmed ─────────────────────────────────────────────────────────

        /// <summary>
        /// FB01/FB02/FB13: the three block revisions the manufacturer itself confirmed defective — `1.1.01.c`,
        /// `6.3.02.d`, and `6.3.04` below revision `b`.
        ///
        /// <para><b>Why this is an ERROR while its sibling row is a Warning.</b> The evidence axis decides, not
        /// the consequence: these three are manufacturer-confirmed, and a defective revision embedded in the
        /// project is defective on every firmware — no controller upgrade rewrites it. The community-reported
        /// set ships separately as a Warning for exactly that reason.</para>
        ///
        /// <para><b>What "confirmed" does and does not mean.</b> LK ACKNOWLEDGED the defect (and for `6.3.02.d`
        /// supplied the fix). It does NOT mean anyone measured it on v3 — the source labels all three
        /// generation-unknown. The Error rests on manufacturer confirmation of the defect, not on a v3
        /// measurement, and the entry says so, so a later reader does not take the grade for more than it is.</para>
        ///
        /// <para><b>No `RequiresLibrary`, and that is what makes the row shippable.</b> A placed block carries
        /// `master_type` and `master_version` IN the `.vis`, so which revision the project embeds is decidable
        /// with no library present. Comparing a block's BODY against the library is a different row's job.</para>
        /// </summary>
        [Test]
        public void EachManufacturerConfirmedDefectiveRevisionIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Revision("1.1.01", "c"), "fb-revision-defective-confirmed"), Is.EqualTo(1));
                Assert.That(Count(Revision("6.3.02", "d"), "fb-revision-defective-confirmed"), Is.EqualTo(1));
                Assert.That(Validate(Revision("1.1.01", "c")).Findings
                    .Single(f => f.RuleId == "fb-revision-defective-confirmed").Severity,
                    Is.EqualTo(ValidationSeverity.Error), "manufacturer-confirmed, and no upgrade rewrites it");
                Assert.That(Message(Revision("1.1.01", "c"), "fb-revision-defective-confirmed"),
                    Does.Contain("1.1.01").And.Contain("c"),
                    "the reader has to know WHICH revision to replace");
            });
        }

        /// <summary>
        /// The neighbouring revisions, which are NOT reported — and the first is the one that matters: the whole
        /// committed corpus embeds `1.1.01/e`, one letter from the affected `1.1.01.c`. A predicate matching the
        /// TYPE would report ten authentic blocks across five vendor files.
        /// </summary>
        [Test]
        public void ANeighbouringRevisionOfTheSameTypeIsNotReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Revision("1.1.01", "e"), "fb-revision-defective-confirmed"), Is.Zero,
                    "the revision the corpus actually carries, one letter away");
                Assert.That(Count(Revision("1.1.01", "b"), "fb-revision-defective-confirmed"), Is.Zero,
                    "and the letter BELOW it: only 'c' is confirmed, so this is not a below-threshold family");
                Assert.That(Count(Revision("6.3.02", "g"), "fb-revision-defective-confirmed"), Is.Zero,
                    "the revision the library ships");
                Assert.That(Count(Revision("6.3.03", "a"), "fb-revision-defective-confirmed"), Is.Zero,
                    "a neighbouring TYPE, which the corpus also carries");
            });
        }

        /// <summary>
        /// `6.3.04` is the one member of the set the source names WITHOUT a revision letter, and the remedy is
        /// what resolves it: <i>replace with 6.3.04b or later</i>. So the affected revisions are everything
        /// BELOW `b` — `a`, and the version-less form, which is a real shape (the library ships `6.3.05` with an
        /// empty version). Neither "every version of the type" nor a single named revision would be right.
        /// </summary>
        [Test]
        public void TheVersionLessTypeIsResolvedAsEverythingBelowItsFixedRevision()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Revision("6.3.04", "a"), "fb-revision-defective-confirmed"), Is.EqualTo(1),
                    "below the fixed revision");
                Assert.That(Count(Revision("6.3.04", ""), "fb-revision-defective-confirmed"), Is.EqualTo(1),
                    "and the version-less form, which is a shape the library really ships");
                Assert.That(Count(Revision("6.3.04", "b"), "fb-revision-defective-confirmed"), Is.Zero,
                    "the remedy names 'b' as the fix, so 'b' itself is not affected");
                Assert.That(Count(Revision("6.3.04", "c"), "fb-revision-defective-confirmed"), Is.Zero,
                    "nor anything after it — the type as a whole is NOT the subject");
            });
        }

        // ── fb-revision-defective-reported ──────────────────────────────────────────────────────────

        /// <summary>
        /// FB03/FB07–FB12/FB14: the eight block revisions reported defective by the COMMUNITY rather than
        /// confirmed by the manufacturer.
        ///
        /// <para><b>Same subject as `fb-revision-defective-confirmed`, different evidence, different severity —
        /// and that is the whole reason they are two rows.</b> A single row would have to grade the whole set at
        /// one confidence, which would either overstate eight community reports or understate three the
        /// manufacturer acknowledged. The two Danish sentences say which population a finding came from, so a
        /// reader never has to consult the catalogue to know how much to trust it.</para>
        ///
        /// <para><b>Why a row that no authentic file triggers is still worth shipping.</b> These reports are
        /// mostly v2-only, so a v3 project reaches such a revision only by having been MIGRATED from v2 — and a
        /// migrated project is exactly the case where nobody remembers which revisions came along.</para>
        /// </summary>
        [Test]
        public void EachCommunityReportedDefectiveRevisionIsReportedAsAWarning()
        {
            (string Type, string Version)[] affected =
            [
                ("5.2.02", "c"), ("5.2.05", "a"), ("5.2.03", "d"), ("4.2.03", "a"),
                ("1.4.03", "b"), ("1.2.03", "c"), ("6.1.02", "b"), ("1.4.06", "a"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string type, string version) in affected)
                {
                    Assert.That(
                        Count(Revision(type, version), "fb-revision-defective-reported"), Is.EqualTo(1),
                        $"{type}.{version}");
                    Assert.That(
                        Count(Revision(type, version), "fb-revision-defective-confirmed"), Is.Zero,
                        $"{type}.{version} is community-reported, so the manufacturer-confirmed row stays out");
                }

                Assert.That(Validate(Revision("5.2.02", "c")).Findings
                    .Single(f => f.RuleId == "fb-revision-defective-reported").Severity,
                    Is.EqualTo(ValidationSeverity.Warning), "community-reported, on section 8.1's third row");
            });
        }

        /// <summary>
        /// The two rows never both fire, and the neighbouring revisions of the affected types are quiet — the
        /// second half being the guard that matters, since `1.2.03` sits one digit from the corpus's `1.2.04`
        /// and `1.4.03`/`1.4.06` one from its `1.4.02`.
        /// </summary>
        [Test]
        public void TheTwoRevisionRowsPartitionAndNeighbouringRevisionsAreQuiet()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Revision("1.1.01", "c"), "fb-revision-defective-reported"), Is.Zero,
                    "a manufacturer-confirmed revision is not also community-reported");
                Assert.That(Count(Revision("1.2.03", "d"), "fb-revision-defective-reported"), Is.Zero,
                    "one letter past the affected revision of the same type");
                Assert.That(Count(Revision("1.2.04", "e"), "fb-revision-defective-reported"), Is.Zero,
                    "and the neighbouring TYPE, which the corpus carries nine times");
                Assert.That(Count(Revision("1.4.02", "a"), "fb-revision-defective-reported"), Is.Zero,
                    "as does this one, ten times — 1.4.03 and 1.4.06 are affected, 1.4.02 is not");
                Assert.That(Message(Revision("5.2.02", "c"), "fb-revision-defective-reported"),
                    Does.Contain("5.2.02.c").And.Contain("rapporteret"),
                    "the sentence names the revision and says the evidence is a report");
                Assert.That(Message(Revision("1.1.01", "c"), "fb-revision-defective-confirmed"),
                    Does.Contain("bekræftet"),
                    "while the confirmed row says the manufacturer confirmed it — the reader can tell them apart");
            });
        }

        // ── fb-short-press-below-default ────────────────────────────────────────────────────────────

        /// <summary>
        /// FB15: revision `1.2.03.d` with <i>Max tid for kort tryk</i> set BELOW its 0,4 s default.
        ///
        /// <para><b>The trap worth shipping.</b> `1.2.03.d` is the revision that `1.2.03.c`'s own remedy
        /// recommends as its fix — so a user who follows one piece of advice lands squarely on this one. The
        /// cross-reference belongs in the English diagnostic; the Danish sentence states the condition, because
        /// a user-facing message that explains the catalogue's internal cross-references is telling the reader
        /// about the tool rather than about their project.</para>
        ///
        /// <para><b>A conjunction, so both halves are excluded separately.</b> The revision alone is not the
        /// condition — `1.2.03.d` at or above the default is a perfectly good block — and the value alone is not
        /// either, since another revision at 0,2 s is not affected. The two tests below are those two halves.</para>
        /// </summary>
        [Test]
        public void TheAffectedRevisionBelowTheShortPressDefaultIsReported()
        {
            Project affected = ShortPress("1.2.03", "d", milliseconds: 200);

            Assert.Multiple(() =>
            {
                Assert.That(Count(affected, "fb-short-press-below-default"), Is.EqualTo(1));
                Assert.That(Validate(affected).Findings
                    .Single(f => f.RuleId == "fb-short-press-below-default").Severity,
                    Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(Message(affected, "fb-short-press-below-default"),
                    Does.Contain("200").And.Contain("400"),
                    "the configured value and the default it is below, in the MILLISECONDS the file stores — "
                    + "which also keeps the sentence on whole numbers, since no shipped row has ever had to "
                    + "render a decimal and the engine formats numbers invariantly");
                Assert.That(Message(affected, "fb-short-press-below-default"),
                    Does.Not.Contain("1.2.03.c"),
                    "the cross-reference to the OTHER row's remedy is diagnostic text, not user-facing");
            });
        }

        /// <summary>
        /// The first half of the conjunction: the same revision at or above the default is not reported. The
        /// boundary is inclusive — 0,4 s IS the default, so a block sitting exactly on it is untouched.
        /// </summary>
        [Test]
        public void TheAffectedRevisionAtOrAboveTheDefaultIsNotReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(ShortPress("1.2.03", "d", 400), "fb-short-press-below-default"), Is.Zero,
                    "exactly at the default is not below it");
                Assert.That(Count(ShortPress("1.2.03", "d", 401), "fb-short-press-below-default"), Is.Zero,
                    "and above it");
                Assert.That(Count(ShortPress("1.2.03", "d", 399), "fb-short-press-below-default"), Is.EqualTo(1),
                    "one millisecond under it is");
                Assert.That(Count(ShortPress("1.2.03", "d", null), "fb-short-press-below-default"), Is.Zero,
                    "and a block that carries no such setting has no value to be below anything");
            });
        }

        /// <summary>
        /// The second half: a DIFFERENT revision at the same low value is not reported. Notably `1.2.03.c` —
        /// which is affected by the community-reported row and whose remedy points at `.d` — draws that row and
        /// not this one, however its short-press time is set.
        /// </summary>
        [Test]
        public void ADifferentRevisionAtTheSameValueIsNotReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(ShortPress("1.2.03", "c", 200), "fb-short-press-below-default"), Is.Zero,
                    "the revision whose remedy recommends the affected one is not itself affected by this");
                Assert.That(Count(ShortPress("1.2.03", "c", 200), "fb-revision-defective-reported"),
                    Is.EqualTo(1), "it draws its own row instead");
                Assert.That(Count(ShortPress("1.2.03", "e", 200), "fb-short-press-below-default"), Is.Zero,
                    "a later revision of the same type");
                Assert.That(Count(ShortPress("1.2.04", "e", 200), "fb-short-press-below-default"), Is.Zero,
                    "and a neighbouring type the corpus carries nine times");
            });
        }

        /// <summary>
        /// A placed block at the given revision, carrying the short-press timer at
        /// <paramref name="milliseconds"/> — or carrying no such setting when that is null.
        /// </summary>
        private static Project ShortPress(string type, string version, int? milliseconds)
        {
            ProjectElement[] settings = milliseconds is { } ms
                ?
                [
                    Tree.Node("resource_timer", Token("resource_timer", 0x90),
                        [("name", "Max tid for kort tryk"), ("hour", "0"), ("minute", "0"),
                         ("second", (ms / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                         ("millisecond", (ms % 1000).ToString(System.Globalization.CultureInfo.InvariantCulture))]),
                ]
                : [];

            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "Lysdæmper"), ("master_schneider_electric", "yes"), ("master_name", "Blok"),
                     ("master_type", type), ("master_version", version)],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_input", Token("resource_input", 0x80), [("name", "Kip"), ("note", "N")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x88), [("name", "Udgang")])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")], settings),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                        Program(0x9a, "Program", 0x80)))));
        }

        /// <summary>A placed vendor block frozen at the given master type and revision.</summary>
        private static Project Revision(string type, string version) =>
            BlockWithMaster(
                ("master_schneider_electric", "yes"),
                ("master_name", "Blok"),
                ("master_type", type),
                ("master_version", version));

        // ── fb-holiday-input-custom-block ───────────────────────────────────────────────────────────

        /// <summary>
        /// A04: a CUSTOM function block carrying a holiday input, on one field report of an upload that fails
        /// against an HW 7.1 controller.
        ///
        /// <para><b>Distinct from `logic-holiday-schedule-firmware` (A29), and section 8.4 says so.</b> A29 is
        /// the project depending on the holiday schedule at all; this is a user-built block carrying a holiday
        /// INPUT PIN. A project can draw both, and they are not the same statement: A29 narrows away on firmware
        /// 3.3.21, and this one has no established fix at all.</para>
        ///
        /// <para><b>"Custom" is `fb-user-authored`'s population, by SHARED PREDICATE rather than by a second
        /// reading.</b> That matters here because the discriminator is subtle: a vendor block whose flag was
        /// stripped keeps its `master_name` and is NOT custom. The cases below pin both halves, because a rule
        /// reading only the vendor flag would report every unlocked library block that happens to have one.</para>
        /// </summary>
        [Test]
        public void ACustomBlockWithAHolidayInputIsReportedAndAVendorOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(HolidayInputBlock(), "fb-holiday-input-custom-block"), Is.EqualTo(1));
                Assert.That(
                    Count(HolidayInputBlock(("master_schneider_electric", "yes"),
                        ("master_name", "Kip tænd sluk")), "fb-holiday-input-custom-block"),
                    Is.Zero, "an intact vendor block is not the subject");
                Assert.That(
                    Count(HolidayInputBlock(("master_name", "Kip tænd sluk")), "fb-holiday-input-custom-block"),
                    Is.Zero,
                    "nor is a vendor block whose flag was stripped — master_name survives, so it is not custom");
                Assert.That(Count(BlockWithMaster(), "fb-holiday-input-custom-block"), Is.Zero,
                    "and a custom block with no holiday input has nothing to report");
                Assert.That(Validate(HolidayInputBlock()).Findings
                    .Single(f => f.RuleId == "fb-holiday-input-custom-block").Severity,
                    Is.EqualTo(ValidationSeverity.Warning), "single field report, on section 8.1's third row");
            });
        }

        /// <summary>
        /// A holiday resource in a block's OUTPUTS or SETTINGS is not a holiday INPUT, and the distinction is
        /// not academic: <c>project2-CustomBlock</c> carries one <c>resource_holiday</c> in each of the four
        /// containers, so a rule that walked the block instead of its input pins would report a block whose
        /// input container holds none.
        /// </summary>
        [Test]
        public void AHolidayResourceOutsideTheInputContainerIsNotAHolidayInput()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(HolidayBlock("outputs"), "fb-holiday-input-custom-block"), Is.Zero);
                Assert.That(Count(HolidayBlock("settings"), "fb-holiday-input-custom-block"), Is.Zero);
                Assert.That(Count(HolidayBlock("internalsettings"), "fb-holiday-input-custom-block"), Is.Zero);
                Assert.That(Count(HolidayBlock("inputs"), "fb-holiday-input-custom-block"), Is.EqualTo(1),
                    "non-vacuity: the same tree with the resource moved into inputs DOES report");
            });
        }

        /// <summary>A custom block whose inputs hold a holiday resource, with the given identity attributes.</summary>
        private static Project HolidayInputBlock(params (string Name, string Value)[] master) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "Ferieblok"), .. master],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_holiday", Token("resource_holiday", 0x80), [("name", "Helligdag")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")]))));

        /// <summary>A custom block with one holiday resource, in whichever container is named.</summary>
        private static Project HolidayBlock(string container)
        {
            ProjectElement Holder(string tag, int at) =>
                tag == container
                    ? Tree.Node(tag, Token(tag, at), [("name", tag)],
                        Tree.Node("resource_holiday", Token("resource_holiday", 0x80), [("name", "Helligdag")]))
                    : Tree.Node(tag, Token(tag, at), [("name", tag)]);

            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70), [("name", "Ferieblok")],
                    Holder("inputs", 0x71),
                    Holder("outputs", 0x72),
                    Holder("settings", 0x73),
                    Holder("internalsettings", 0x74),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")]))));
        }

        // ── fb-user-authored ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Losing the <c>.ifb</c> of a user-built block means it can never be re-inserted elsewhere: the
        /// <c>.vis</c> carries its CONTENTS but not a reusable file, and no Visual install will re-supply it.
        ///
        /// <para><b>The discriminator against `fb-provenance-rewritten` is the whole risk.</b> Unlocking a
        /// vendor block, or saving one to the library, STRIPS the vendor flag but KEEPS `master_name` — so the
        /// flag's absence alone does not mean "not an LK block". This row needs BOTH halves: no vendor flag AND
        /// no master name, which is the from-scratch signature.</para>
        /// </summary>
        [Test]
        public void AFromScratchBlockIsReportedAndAStrippedVendorBlockIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(BlockWithMaster(), "fb-user-authored"), Is.EqualTo(1),
                    "no master attributes at all: the from-scratch signature");
                Assert.That(Count(BlockWithMaster(("master_schneider_electric", "no")), "fb-user-authored"),
                    Is.EqualTo(1),
                    "the defensive branch — an imported file could write the DTD default explicitly");
                Assert.That(Count(BlockWithMaster(("master_name", "Kip tænd sluk")), "fb-user-authored"),
                    Is.Zero,
                    "a surviving master_name means an LK block whose flag was stripped — that is 3.6's row");
                Assert.That(
                    Count(BlockWithMaster(("master_schneider_electric", "yes"), ("master_name", "Kip tænd sluk")),
                        "fb-user-authored"),
                    Is.Zero, "and an intact vendor block is not user-built by any reading");
                Assert.That(Message(BlockWithMaster(), "fb-user-authored"),
                    Is.EqualTo("Funktionsblokken 'Trappelys' er egenudviklet og følger ikke med nogen "
                        + "installation af IHC Visual, så dens .ifb-fil bør arkiveres sammen med projektet."));
            });
        }

        /// <summary>
        /// The corpus load is real and expected: user-built blocks are ordinary, not exceptional, so this row is
        /// among the largest movers in the catalogue. It says something worth knowing about a correct project —
        /// which is exactly what the Information tier is for.
        /// </summary>
        [Test]
        public void TheUserAuthoredRowIsInformationAndFiresOnAuthenticProjects()
        {
            Project authentic = Authentic("project2-CustomBlock.vis");

            Assert.Multiple(() =>
            {
                Assert.That(Count(authentic, "fb-user-authored"), Is.GreaterThan(0),
                    "an authentic project with a custom block reports it");
                Assert.That(Validate(authentic).Findings
                    .First(f => f.RuleId == "fb-user-authored").Severity,
                    Is.EqualTo(ValidationSeverity.Info),
                    "nothing is wrong with building your own block; the .ifb is just worth keeping");
            });
        }

        // ── fb-provenance-rewritten ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Without the vendor trio the block cannot be checked against errata or against a fixed revision, and
        /// the operation that removed it is irreversible.
        ///
        /// <para><b>THE COMPLEMENT OF `fb-user-authored`, exactly.</b> That row needs both provenance halves
        /// absent; this one needs the NAME present and the trio gone. Between them the two cover every block that
        /// arrived as a file — which is the point: keying the archive advice on the from-scratch row alone would
        /// miss precisely the blocks most likely to have been downloaded or exported.</para>
        /// </summary>
        [Test]
        public void AStrippedVendorBlockIsReportedAndTheOtherProvenanceShapesAreNot()
        {
            Project stripped = BlockWithMaster(("master_name", "Kip tænd sluk"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(stripped, "fb-provenance-rewritten"), Is.EqualTo(1));
                Assert.That(Count(BlockWithMaster(), "fb-provenance-rewritten"), Is.Zero,
                    "a from-scratch block never had a trio to lose — that is fb-user-authored's row");
                Assert.That(
                    Count(BlockWithMaster(("master_name", "Kip tænd sluk"), ("master_type", "1.1.01")),
                        "fb-provenance-rewritten"),
                    Is.Zero, "a surviving type means the trio is not gone");
                Assert.That(
                    Count(BlockWithMaster(("master_name", "Kip tænd sluk"),
                            ("master_schneider_electric", "yes")),
                        "fb-provenance-rewritten"),
                    Is.Zero, "and a surviving vendor flag likewise");
            });
        }

        /// <summary>
        /// THE TWO ROWS PARTITION THE POPULATION: no block reports both, and the corpus carries one of each shape
        /// in the same file. <c>project2-CustomBlock</c> holds <c>AutoProof</c> (master_name, no trio) and
        /// <c>Custom blok</c> (neither), which is what makes it the witness for the pair rather than for either.
        /// </summary>
        [Test]
        public void TheTwoProvenanceRowsPartitionTheBlocksBetweenThem()
        {
            Project stripped = BlockWithMaster(("master_name", "Kip tænd sluk"));
            Project scratch = BlockWithMaster();
            ProjectValidationResult authentic = Validate(Authentic("project2-CustomBlock.vis"));

            Assert.Multiple(() =>
            {
                Assert.That(Count(stripped, "fb-user-authored"), Is.Zero, "no block reports both");
                Assert.That(Count(scratch, "fb-provenance-rewritten"), Is.Zero, "in either direction");
                Assert.That(authentic.Findings.Count(f => f.RuleId == "fb-provenance-rewritten"), Is.EqualTo(1),
                    "the AutoProof block");
                Assert.That(authentic.Findings.Count(f => f.RuleId == "fb-user-authored"), Is.EqualTo(1),
                    "and the Custom blok beside it");
            });
        }

        /// <summary>
        /// The sentence names unlock or save-as as the LIKELY cause and must not assert which one ran: the source
        /// measured that the two commands are not always distinguishable from the file.
        /// </summary>
        [Test]
        public void TheProvenanceSentenceHedgesTheCauseAndCarriesTheArchiveAdvice()
        {
            string message = Message(BlockWithMaster(("master_name", "Kip tænd sluk")), "fb-provenance-rewritten");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("typisk"),
                    "the cause is hedged — the file does not always distinguish the two commands");
                Assert.That(message, Does.Contain(".ifb-fil bør arkiveres"),
                    "and the archive advice is on THIS half too, not only on fb-user-authored's");
            });
        }

        // ── fb-master-missing-from-library ──────────────────────────────────────────────────────────

        /// <summary>
        /// Whole block types are dropped between Visual releases with no announcement, and a project depending on
        /// one that is gone cannot be rebuilt from a clean install.
        ///
        /// <para><b>The type-only question is the one this row asks</b>, and it is why the port was widened: a
        /// body lookup keyed on an exact identity cannot say "absent at EVERY version". A type present at a
        /// different version is a different finding entirely.</para>
        /// </summary>
        [Test]
        public void ABlockWhoseMasterTypeIsNotInTheLibraryIsReported()
        {
            ILibraryBlockSource library = Library("5", alsoHolding: [("2.1.01", "a")]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(MasterBlock("9.9.99", "z"), "fb-master-missing-from-library", library),
                    Is.EqualTo(1), "a type the library does not hold at any version");
                Assert.That(Count(MasterBlock("1.1.01", "e"), "fb-master-missing-from-library", library),
                    Is.Zero, "the library holds this type at this version");
                Assert.That(Count(MasterBlock("2.1.01", "c"), "fb-master-missing-from-library", library),
                    Is.Zero,
                    "the library holds the TYPE, at another version — a version mismatch is not this row");
                Assert.That(Message(MasterBlock("9.9.99", "z"), "fb-master-missing-from-library", library),
                    Does.Contain("9.9.99"), "the type, so the reader can search a library for it");
            });
        }

        /// <summary>
        /// SKIPPED, NEVER GUESSED, when the caller supplies no library. The row declares
        /// <c>RequiresLibrary</c>, so a caller with nothing to compare against gets silence rather than a finding
        /// derived from an absent fact — exactly as the capacity rows behave without controller limits.
        /// </summary>
        [Test]
        public void TheMissingMasterRowIsSkippedWithoutALibrary()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(MasterBlock("9.9.99", "z"), "fb-master-missing-from-library", library: null),
                    Is.Zero, "no library means no answer, and no answer means no finding");
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("fb-master-missing-from-library"),
                    out ProblemCatalogEntry entry), Is.True);
                Assert.That(entry.RequiresLibrary, Is.True,
                    "declared, so the PROFILE skips the rule rather than the rule handling absence");
            });
        }

        /// <summary>
        /// A block with no <c>master_type</c> is not this row's business: it is one of the two provenance rows'
        /// populations, and asking a library about a type that was never claimed would be asking the wrong
        /// question.
        /// </summary>
        [Test]
        public void ABlockClaimingNoMasterTypeIsNotAskedAboutTheLibrary()
        {
            ILibraryBlockSource library = Library("5");

            Assert.Multiple(() =>
            {
                Assert.That(Count(BlockWithMaster(), "fb-master-missing-from-library", library), Is.Zero,
                    "a from-scratch block claims no type");
                Assert.That(
                    Count(BlockWithMaster(("master_name", "Kip tænd sluk")), "fb-master-missing-from-library",
                        library),
                    Is.Zero, "and neither does a stripped one");
            });
        }

        // ── fb-master-version-differs ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Behaviour can change materially between revisions of the same nominal block, and swapping is a manual
        /// re-commissioning job rather than a drop-in — so a project frozen at one revision while the library
        /// ships another is worth knowing about.
        ///
        /// <para><b>It fires in BOTH directions.</b> Older than the library and newer than it are the same
        /// finding: what matters is that the two disagree, not which way. A rule that only reported "behind"
        /// would say nothing about a project carrying a revision the installed library has since dropped.</para>
        /// </summary>
        [Test]
        public void AVersionMismatchIsReportedInEitherDirection()
        {
            ILibraryBlockSource library = Library("5", alsoHolding: [("2.1.01", "c")]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(MasterBlock("2.1.01", "a"), "fb-master-version-differs", library),
                    Is.EqualTo(1), "older than the library");
                Assert.That(Count(MasterBlock("2.1.01", "e"), "fb-master-version-differs", library),
                    Is.EqualTo(1), "newer than the library — the same finding, not a different one");
                Assert.That(Count(MasterBlock("2.1.01", "c"), "fb-master-version-differs", library),
                    Is.Zero, "and a match reports nothing");
                Assert.That(Message(MasterBlock("2.1.01", "a"), "fb-master-version-differs", library),
                    Does.Contain("a").And.Contain("c"), "both revisions, so the reader can compare them");
            });
        }

        /// <summary>
        /// THE LIBRARY MAY HOLD SEVERAL VERSIONS OF ONE TYPE, and the port's answer is plural for that reason.
        /// The built-in library happens to hold each type once, but an installed one may ship two revisions side
        /// by side — so a block matching ANY of them is in sync, and one matching none is not.
        ///
        /// <para>A rule assuming a single version would report a perfectly current block whenever the library
        /// held a second revision of its type. This is the case T003 widened the port for.</para>
        /// </summary>
        [Test]
        public void AMatchAgainstAnyHeldVersionIsInSync()
        {
            ILibraryBlockSource twoRevisions = Library("5", alsoHolding: [("2.1.01", "a"), ("2.1.01", "c")]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(MasterBlock("2.1.01", "a"), "fb-master-version-differs", twoRevisions),
                    Is.Zero, "the library holds this revision, alongside another");
                Assert.That(Count(MasterBlock("2.1.01", "c"), "fb-master-version-differs", twoRevisions),
                    Is.Zero, "and the other one too");
                Assert.That(Count(MasterBlock("2.1.01", "b"), "fb-master-version-differs", twoRevisions),
                    Is.EqualTo(1), "a revision it holds NEITHER of is the mismatch");
                Assert.That(Message(MasterBlock("2.1.01", "b"), "fb-master-version-differs", twoRevisions),
                    Does.Contain("a").And.Contain("c"),
                    "and the sentence names every revision the library has, not an arbitrary one of them");
            });
        }

        /// <summary>
        /// A VERSION-LESS BLOCK IS A REVISION LIKE ANY OTHER. Requiring a letter made this row silent about
        /// every version-less family the built-in library ships — <c>4.1.01</c> and <c>4.1.04</c> among them,
        /// both in the committed corpus — and those are precisely the families where a later lettered revision
        /// is the drift worth reporting.
        ///
        /// <para>The same defect T055 found in the insert-name reconstruction, one rule further along. Absent
        /// and blank read alike, because the two spell one thing: the vendor writes no attribute at all and an
        /// importer may write an empty one.</para>
        /// </summary>
        [Test]
        public void AVersionlessBlockIsComparedAgainstTheLibraryLikeAnyOther()
        {
            ILibraryBlockSource lettered = Library("5", alsoHolding: [("2.1.01", "c")]);
            ILibraryBlockSource versionless = Library("5", alsoHolding: [("2.1.01", "")]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(VersionlessMasterBlock("2.1.01"), "fb-master-version-differs", lettered),
                    Is.EqualTo(1), "no version, where the library holds a letter, is a mismatch");
                Assert.That(Count(MasterBlock("2.1.01", ""), "fb-master-version-differs", lettered),
                    Is.EqualTo(1), "and a blank attribute is the same shape as an absent one");
                Assert.That(Count(VersionlessMasterBlock("2.1.01"), "fb-master-version-differs", versionless),
                    Is.Zero, "a library holding that same form is in sync — 4.1.01 and 4.1.04 are this case");
                Assert.That(Count(MasterBlock("2.1.01", "a"), "fb-master-version-differs", versionless),
                    Is.EqualTo(1), "and the mismatch is symmetric: a letter against a version-less library");
            });
        }

        /// <summary>
        /// THE VERSION-LESS FORM IS SPELLED, on either side of the comparison. The sentence reads
        /// <i>"indsat som version {frozen}, mens blokbiblioteket nu indeholder version {library}"</i>, so binding
        /// the empty string produced <i>"indsat som version , mens …"</i> — a sentence a reader cannot parse
        /// rather than a fact they can act on. The library side was reachable before this row was widened at all.
        /// </summary>
        [Test]
        public void TheVersionlessFormIsSpelledOnBothSidesOfTheSentence()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    Message(VersionlessMasterBlock("2.1.01"), "fb-master-version-differs",
                        Library("5", alsoHolding: [("2.1.01", "c")])),
                    Does.Contain("version uden betegnelse").And.Contain("version c"),
                    "the block's own side");
                Assert.That(
                    Message(MasterBlock("2.1.01", "a"), "fb-master-version-differs",
                        Library("5", alsoHolding: [("2.1.01", "")])),
                    Does.Contain("version a").And.Contain("version uden betegnelse"),
                    "and the library's");
            });
        }

        /// <summary>
        /// The two library rows partition their population: a type the library does not hold AT ALL is
        /// `fb-master-missing-from-library`, and a type it holds at another revision is this one. No block
        /// reports both, and a block with no library to compare against reports neither.
        /// </summary>
        [Test]
        public void TheTwoLibraryRowsPartitionTheBlocksBetweenThem()
        {
            ILibraryBlockSource library = Library("5", alsoHolding: [("2.1.01", "c")]);

            Assert.Multiple(() =>
            {
                Assert.That(Count(MasterBlock("9.9.99", "z"), "fb-master-version-differs", library), Is.Zero,
                    "a type the library lacks entirely has no revision to differ from");
                Assert.That(Count(MasterBlock("9.9.99", "z"), "fb-master-missing-from-library", library),
                    Is.EqualTo(1), "that is the other row");
                Assert.That(Count(MasterBlock("2.1.01", "a"), "fb-master-missing-from-library", library),
                    Is.Zero, "and a held type is not missing");
                Assert.That(Count(MasterBlock("2.1.01", "a"), "fb-master-version-differs", library: null),
                    Is.Zero, "without a library neither row can answer, so neither does");
            });
        }

        // ── fb-pir-dusk-gated ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A wired-but-inert <c>Skumring</c> pin reads in the field as a broken PIR, and nothing is broken: the
        /// block only reacts to motion while that input is ON.
        ///
        /// <para><b>An UNWIRED pin does not gate</b>, which is why wiring is the predicate rather than the pin's
        /// mere existence — every instance of this block type has the pin.</para>
        /// </summary>
        [Test]
        public void ADuskGatedPirBlockIsReportedAndAnUnwiredOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(PirBlock("1.4.02", duskWired: true), "fb-pir-dusk-gated"), Is.EqualTo(1));
                Assert.That(Count(PirBlock("1.4.02", duskWired: false), "fb-pir-dusk-gated"), Is.Zero,
                    "an unwired Skumring pin does not gate anything");
                Assert.That(Count(PirBlock("1.1.01", duskWired: true), "fb-pir-dusk-gated"), Is.Zero,
                    "and another master type's block is not this one, however its pins are named");
            });
        }

        /// <summary>
        /// THE SENTENCE IS A CONSEQUENCE TO VERIFY, NOT A FAULT. The rule cannot evaluate whether the linked
        /// source ever turns ON — that is a runtime question about another part of the installation — so the
        /// message describes what will happen if it does not, and stops there.
        /// </summary>
        [Test]
        public void ThePirSentenceDescribesTheConsequenceRatherThanAssertingAFault()
        {
            string message = Message(PirBlock("1.4.02", duskWired: true), "fb-pir-dusk-gated");

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("kun på bevægelse, mens"),
                    "the gating condition, stated as the behaviour it is");
                Assert.That(message, Does.Contain("virke død"),
                    "and the consequence to look for, conditional on a source that never turns ON");
                Assert.That(Validate(PirBlock("1.4.02", duskWired: true)).Findings
                    .Single(f => f.RuleId == "fb-pir-dusk-gated").Severity,
                    Is.EqualTo(ValidationSeverity.Info),
                    "nothing here is wrong — a gated PIR is exactly what the block is for");
            });
        }

        /// <summary>
        /// SILENT ON A STRIPPED BLOCK, BY CONSTRUCTION. This row keys on <c>master_type</c>, which unlock and
        /// save-as remove — so it goes quiet on precisely the blocks `fb-provenance-rewritten` reports.
        ///
        /// <para>That is correct rather than a hole: the rule cannot know which master an unlocked block came
        /// from, and guessing from pin names would report any block that happened to name a pin
        /// <c>Skumring</c>. It is asserted here so the coverage edge is recorded rather than discovered.</para>
        /// </summary>
        [Test]
        public void APirBlockWhoseMasterTypeWasStrippedIsNotReported()
        {
            Project stripped = Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "PIR"), ("master_name", "PIR og timer")],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        DuskPin(wired: true)),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "S")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "IS")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "P")]))));

            Assert.Multiple(() =>
            {
                Assert.That(Count(stripped, "fb-pir-dusk-gated"), Is.Zero,
                    "no master_type means no way to know this was a 1.4.02 block");
                Assert.That(Count(stripped, "fb-provenance-rewritten"), Is.EqualTo(1),
                    "and the row that DOES speak for it says the provenance is gone");
            });
        }

        // ── fb-pulse-constant-default ───────────────────────────────────────────────────────────────

        /// <summary>
        /// An unchanged default constant silently mis-scales every reading if the physical meter differs — and
        /// the project cannot check the meter's rating plate.
        ///
        /// <para><b>An instance whose constant was CHANGED is not reported:</b> somebody has already made the
        /// decision this row asks for. That is the whole exclusion, and it is what stops the row nagging about a
        /// commissioned meter.</para>
        /// </summary>
        [Test]
        public void APulseBlockStillAtItsDefaultConstantIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(PulseBlock("4.2.03", "100"), "fb-pulse-constant-default"), Is.EqualTo(1));
                Assert.That(Count(PulseBlock("4.2.03", "250"), "fb-pulse-constant-default"), Is.Zero,
                    "changed away from the default: the decision has been made");
                Assert.That(Count(PulseBlock("1.1.01", "100"), "fb-pulse-constant-default"), Is.Zero,
                    "another master type's block is not this one");
            });
        }

        /// <summary>
        /// THE CONSTANT IS BOUND FROM THE INSTANCE, NEVER FROM THE THRESHOLD. An earlier draft offered a
        /// fallback that reported any instance of the type while binding the declared default — which renders
        /// "regner med 100 impulser" at a project that set 250, a sentence contradicting the project's own
        /// content.
        ///
        /// <para>The threshold exists to decide WHETHER to report; the message says what the project actually
        /// carries. Here the two coincide, because a reported instance is by definition still at the default —
        /// so the test proves the binding by reading the resource the rule read, not by trusting the number.</para>
        /// </summary>
        [Test]
        public void TheReportedConstantIsTheInstancesOwn()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("fb-pulse-constant-default"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "DefaultPulsesPerKwh");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(100));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorDocumented),
                    "the mirrored 4.2.03.ifb ships inivalue=\"100\" — measured, not authored");
                Assert.That(Message(PulseBlock("4.2.03", "100"), "fb-pulse-constant-default"),
                    Does.Contain("100"));
                Assert.That(entry.MessageTemplate, Does.Contain("{pulses}").And.Not.Contain("100"),
                    "the template carries the placeholder; the number comes from the instance");
            });
        }

        /// <summary>
        /// THE CONTAINER IS `settings`, NOT `internalsettings`. An earlier draft sent the rule to the wrong
        /// group: `internalsettings` holds only timers and scratch integers on this block, so a rule written
        /// against it would never fire at all.
        /// </summary>
        [Test]
        public void TheConstantIsReadFromTheSettingsGroupAndNotTheInternalOne()
        {
            Assert.That(Count(PulseBlock("4.2.03", "100", inInternalSettings: true),
                "fb-pulse-constant-default"), Is.Zero,
                "a constant sitting in the INTERNAL group is not the settings constant this row reads");
        }

        // ── logic-block-empty ───────────────────────────────────────────────────────────────────────

        [Test]
        public void ABlockWithNoProgramsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(programs: 0), "logic-block-empty"), Is.EqualTo(1));
                Assert.That(Message(Block(programs: 0), "logic-block-empty"),
                    Is.EqualTo("Blokken 'Trappelys' har ingen programmer."));
                Assert.That(Count(Block(programs: 1), "logic-block-empty"), Is.Zero,
                    "every inserted block ships one default program, so one is the ordinary state");
            });
        }

        // ── logic-block-no-pins ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ABlockWithNeitherInputsNorOutputsIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(programs: 1, inputs: 0, outputs: 0), "logic-block-no-pins"), Is.EqualTo(1));
                Assert.That(Message(Block(programs: 1, inputs: 0, outputs: 0), "logic-block-no-pins"),
                    Is.EqualTo("Blokken 'Trappelys' har hverken ind- eller udgange."));
                Assert.That(Count(Block(programs: 1, inputs: 1, outputs: 0), "logic-block-no-pins"), Is.Zero,
                    "an input alone is a way in");
                Assert.That(Count(Block(programs: 1, inputs: 0, outputs: 1), "logic-block-no-pins"), Is.Zero,
                    "and an output alone is a way out — the row needs BOTH to be empty");
            });
        }

        // ── logic-duplicate-program ─────────────────────────────────────────────────────────────────

        [Test]
        public void TwoStructurallyIdenticalProgramsAreOneFinding()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Programs(("A", 0x80), ("B", 0x80)), "logic-duplicate-program"), Is.EqualTo(1),
                    "same operand, different label: still a copy");
                Assert.That(Message(Programs(("A", 0x80), ("B", 0x80)), "logic-duplicate-program"),
                    Is.EqualTo("Blokken 'Trappelys' har to identiske programmer."));
                Assert.That(Count(Programs(("A", 0x80), ("B", 0x81)), "logic-duplicate-program"), Is.Zero,
                    "a different operand is a different program, however similar it looks");
                Assert.That(Count(Programs(("A", 0x80)), "logic-duplicate-program"), Is.Zero);
            });
        }

        [Test]
        public void ThreeCopiesOfOneProgramAreTwoFindings()
        {
            Assert.That(Count(Programs(("A", 0x80), ("B", 0x80), ("C", 0x80)), "logic-duplicate-program"),
                Is.EqualTo(2),
                "each later copy is its own redundancy, and each one is separately deletable");
        }

        [Test]
        public void IdenticalProgramsInTwoDifferentBlocksAreNotDuplicates()
        {
            Project twoBlocks = Tree.WithRoot(Locality(
                BlockShell(0x70, "Blok A", 1, 1, [Program(0x90, "P", 0x80)]),
                BlockShell(0xa0, "Blok B", 1, 1, [Program(0xc0, "P", 0x80)])));

            Assert.That(Count(twoBlocks, "logic-duplicate-program"), Is.Zero,
                "the row is about two programs in the SAME block; two blocks may do the same thing");
        }

        // ── the library-block naming border ─────────────────────────────────────────────────────────

        /// <summary>
        /// A LIBRARY BLOCK RENAMED BY ITS AUTHOR DRAWS NOTHING, and that is the deletion this test records.
        /// <c>logic-master-block-modified</c> compared a block's <c>name</c> with the insert name rebuilt from its
        /// master identity, so every descriptive rename — <i>Kip tænd sluk (lokalt tilpasset)</i>, the very thing
        /// the vendor's own naming guidance asks for — was reported as a local modification the block had not
        /// necessarily undergone. Paired with <c>name-default</c> it also guaranteed each reconstructible library
        /// block exactly one advisory whatever its author did, which is a row that carries no information.
        ///
        /// <para>What survives is the half that is TRUE of a name: <c>name-default</c> still reports a block left
        /// AT its insert name. Content that genuinely diverges from the library is
        /// <c>logic-block-locked-content</c>'s finding — it compares against the library body — and a version that
        /// does is <c>fb-master-version-differs</c>'s.</para>
        /// </summary>
        [Test]
        public void ARenamedLibraryBlockIsNoLongerReported()
        {
            Project renamed = LibraryBlock("Kip tænd sluk (lokalt tilpasset)");
            Project untouched = LibraryBlock("1.1.01.e. Kip tænd sluk");

            Assert.Multiple(() =>
            {
                Assert.That(Validate(renamed).Findings.Select(f => f.RuleId), Has.None.EqualTo("name-default"),
                    "the block moved away from its insert name, which is what that row is about");
                Assert.That(Count(untouched, "name-default"), Is.EqualTo(1),
                    "and the block left at its insert name is still named by the row that survives");
            });
        }

        /// <summary>
        /// The versionless library form the shared reader also understands — a block whose master identity carries
        /// <c>master_type</c> and <c>master_name</c> but no version.
        /// </summary>
        [Test]
        public void TheVersionlessLibraryFormIsRecognisedByTheSurvivingRow()
        {
            Project untouched = Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", "4.1.04. Driftstimetæller"), ("master_type", "4.1.04"),
                        ("master_name", "Driftstimetæller"), ("locked", "yes"),
                    ],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));

            Assert.That(Count(untouched, "name-default"), Is.EqualTo(1),
                "the versionless insert name reconstructs, so a block still at it is reported");
        }

        // ── logic-block-locked-content (D27) ────────────────────────────────────────────────────────

        /// <summary>
        /// The declared-context half, and the reason the row could not exist before D27: with no library supplied
        /// the rule is not evaluated at all — not evaluated against a guessed default, which is what would make the
        /// same project valid on one workstation and invalid on another.
        /// </summary>
        [Test]
        public void TheLockedContentRowIsSkippedWithoutALibrary()
        {
            ProblemCode code = new("logic-block-locked-content");
            Project edited = LockedLibraryBlock(storedMinutes: "5");

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True);
                Assert.That(entry.RequiresLibrary, Is.True, "the row DECLARES the context it needs");
                Assert.That(ValidationProfile.Categorized.Includes(entry), Is.False,
                    "so a profile carrying no library does not evaluate it");
                Assert.That(Count(edited, "logic-block-locked-content", library: null), Is.Zero);
                Assert.That(Count(edited, "logic-block-locked-content", library: Library("3")), Is.EqualTo(1),
                    "and the same project reports once a library IS supplied");
            });
        }

        [Test]
        public void AValueChangedUnderALockIsReportedAgainstTheLibrarys()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo(1), "the error fixture's own witness: a timer moved from 3 to 5 minutes under lock");
                Assert.That(Message(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo("Den låste blok '1.1.01.e. Kip tænd sluk' har ændret 'Timer'."),
                    "the sentence names the block and the variable; a timer's four-part value would be machine "
                    + "text in Danish prose, so the comparison detail stays in the English diagnostic");
                Assert.That(Count(LockedLibraryBlock("3"), "logic-block-locked-content", Library("3")), Is.Zero,
                    "a value equal to the library's is not an edit");
            });
        }

        [Test]
        public void AnUnlockedBlockIsNotThisRowsBusiness()
        {
            Assert.That(Count(LockedLibraryBlock("5", locked: false), "logic-block-locked-content", Library("3")),
                Is.Zero,
                "an unlocked block may be edited freely — the lock is what this row is about");
        }

        [Test]
        public void ATimersValueLivesInItsTimePartsNotInAValueAttribute()
        {
            Assert.Multiple(() =>
            {
                // The reading that cost a wrong first attempt: a resource_timer stores no value/inivalue at all.
                Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3")),
                    Is.EqualTo(1), "the minute part IS the value");
                Assert.That(Count(LockedLibraryBlock(null), "logic-block-locked-content", Library("3")), Is.Zero,
                    "a variable storing nothing is at its default and cannot have been edited");
            });
        }

        [Test]
        public void AVariableTheLibraryDoesNotHaveIsNotAnEditedValue()
        {
            Assert.That(Count(LockedLibraryBlock("5"), "logic-block-locked-content", Library("3", named: "Andet")),
                Is.Zero,
                "pairing is by NAME; a variable the library has no counterpart for is a structural difference "
                + "rather than an edited value, and is nobody's finding");
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static ProjectElement Locality(params ProjectElement[] contents) =>
            Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents));

        private static ImmutableArray<ProjectElement> Sections(
            int at, int inputs, int outputs, ProjectElement[] programs) =>
            [
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")],
                    [.. Enumerable.Range(0, inputs).Select(i => Tree.Node("resource_input",
                        Token("resource_input", 0x80 + i), [("name", $"Indgang {i}"), ("note", "N")]))]),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")],
                    [.. Enumerable.Range(0, outputs).Select(i => Tree.Node("resource_output",
                        Token("resource_output", 0x88 + i), [("name", $"Udgang {i}")]))]),
                Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")]),
                Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")], programs),
            ];

        private static ProjectElement BlockShell(
            int at, string name, int inputs, int outputs, ProjectElement[] programs) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                [.. Sections(at, inputs, outputs, programs)]);

        /// <summary>One program whose event and action both name the operand at <paramref name="operandAt"/>.</summary>
        private static ProjectElement Program(int at, string name, int operandAt) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", name)],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    Tree.Node("event", Token("event", at + 2),
                        [("name", "%P -> ON"), ("link1", Token("resource_input", operandAt)), ("method", "_0xa")])),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    Tree.Node("action", Token("action", at + 4),
                        [("name", "%P = ON"), ("link1", Token("resource_output", operandAt)), ("method", "_0xa")])));

        /// <summary>A block with the given number of programs, inputs and outputs.</summary>
        private static Project Block(int programs, int inputs = 1, int outputs = 1) =>
            Tree.WithRoot(Locality(BlockShell(0x70, "Trappelys", inputs, outputs,
                [.. Enumerable.Range(0, programs).Select(i => Program(0x90 + (i * 0x10), $"Program {i}", 0x80))])));

        /// <summary>A block whose programs are named and operand-bound as given — the duplicate-signature cases.</summary>
        private static Project Programs(params (string Name, int OperandAt)[] programs) =>
            Tree.WithRoot(Locality(BlockShell(0x70, "Trappelys", 1, 1,
                [.. programs.Select((p, i) => Program(0x90 + (i * 0x10), p.Name, p.OperandAt))])));

        /// <summary>
        /// A block carrying exactly the master attributes given — the four provenance attributes are what the
        /// authorship rows read, and each of the four states below is one of their partitions.
        /// </summary>
        /// <param name="master">The <c>master_*</c> attributes to write, verbatim.</param>
        private static Project BlockWithMaster(params (string Name, string Value)[] master) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "Trappelys"), .. master],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));

        /// <summary>
        /// The PIR block's twilight input pin, named exactly as the mirrored <c>1.4.02.ifb</c> ships it, wired
        /// or not. An unwired pin is the ordinary state and does not gate.
        /// </summary>
        /// <param name="wired">Whether the pin carries a <c>link_to_resource</c>.</param>
        private static ProjectElement DuskPin(bool wired) =>
            Tree.Node("resource_input", Token("resource_input", 0x80), [("name", "Skumring"), ("note", "N")],
                wired
                    ? [Tree.Node("link_to_resource", Token("link_to_resource", 0x82),
                        [("name", "Følg Link"), ("link", Token("resource_output", 0x88))])]
                    : []);

        /// <summary>
        /// A block of the given master type carrying the PIR block's pin set: the twilight input, wired or not,
        /// beside an ordinary one.
        /// </summary>
        /// <param name="masterType">The <c>master_type</c> the block claims.</param>
        /// <param name="duskWired">Whether its <c>Skumring</c> pin is linked.</param>
        private static Project PirBlock(string masterType, bool duskWired) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "PIR og timer"), ("master_type", masterType), ("master_version", "a"),
                     ("master_name", "PIR og timer"), ("master_schneider_electric", "yes")],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        DuskPin(duskWired),
                        Tree.Node("resource_input", Token("resource_input", 0x81),
                            [("name", "PIR"), ("note", "N")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x88), [("name", "Udgang")])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "S")]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "IS")]),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "P")],
                        Program(0x90, "Program", 0x81)))));

        /// <summary>
        /// A pulse-counting block of the given master type whose <c>1 Kwh/M3</c> constant carries the given
        /// value — in the <c>settings</c> group the mirrored <c>4.2.03.ifb</c> puts it in, or in
        /// <c>internalsettings</c> to prove the rule does not read that one.
        /// </summary>
        /// <param name="masterType">The <c>master_type</c> the block claims.</param>
        /// <param name="pulses">The constant's <c>inivalue</c>.</param>
        /// <param name="inInternalSettings">Put the constant in the wrong group on purpose.</param>
        private static Project PulseBlock(string masterType, string pulses, bool inInternalSettings = false)
        {
            ProjectElement constant = Tree.Node("resource_integer", Token("resource_integer", 0x84),
                [("name", "1 Kwh/M3"), ("inivalue", pulses)]);

            return Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [("name", "Energimåler"), ("master_type", masterType), ("master_version", "b"),
                     ("master_name", "Impulstæller"), ("master_schneider_electric", "yes")],
                    Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")],
                        Tree.Node("resource_input", Token("resource_input", 0x80),
                            [("name", "Impuls"), ("note", "N")])),
                    Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")],
                        Tree.Node("resource_output", Token("resource_output", 0x88), [("name", "Udgang")])),
                    Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")],
                        inInternalSettings ? [] : [constant]),
                    Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")],
                        inInternalSettings ? [constant] : []),
                    Tree.Node("programs", Token("programs", 0x75), [("name", "P")],
                        Program(0x90, "Program", 0x80)))));
        }

        /// <summary>A block claiming the given master identity, with a name that does not encode it.</summary>
        /// <param name="masterType">The <c>master_type</c> the block claims.</param>
        /// <param name="masterVersion">The <c>master_version</c> it claims.</param>
        private static Project MasterBlock(string masterType, string masterVersion) =>
            BlockWithMaster(
                ("master_type", masterType), ("master_version", masterVersion),
                ("master_name", "Kip tænd sluk"), ("master_schneider_electric", "yes"));

        /// <summary>
        /// A block claiming a master TYPE and no revision letter at all — the shape the built-in library's
        /// version-less families ship, and the shape the committed corpus carries for <c>4.1.01</c>/<c>4.1.04</c>.
        /// </summary>
        /// <param name="masterType">The <c>master_type</c> the block claims.</param>
        private static Project VersionlessMasterBlock(string masterType) =>
            BlockWithMaster(
                ("master_type", masterType), ("master_name", "Kip tænd sluk"),
                ("master_schneider_electric", "yes"));

        /// <summary>A library block carrying full master identity, under the given name.</summary>
        private static Project LibraryBlock(string name) =>
            Tree.WithRoot(Locality(
                Tree.Node("functionblock", Token("functionblock", 0x70),
                    [
                        ("name", name), ("master_type", "1.1.01"), ("master_version", "e"),
                        ("master_name", "Kip tænd sluk"), ("locked", "yes"),
                    ],
                    [.. Sections(0x70, 1, 1, [Program(0x90, "Program", 0x80)])])));
    }
}
