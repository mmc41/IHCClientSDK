#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The FUNCTION-BLOCK rows, in two groups. What a block's own SHAPE says — it does nothing, nothing can
    /// reach it, it says the same thing twice, its locked content was edited after locking — and what its
    /// claimed PROVENANCE says: which library entry it came from, whether that entry still exists at that
    /// revision, whether the revision is one the manufacturer or the community reported defective, and whether a
    /// setting still carries the value that library shipped.
    ///
    /// <para><b><c>logic-block-locked-content</c> IS here now, and it took a ruling to get here (D27).</b> Its
    /// condition is content edited AFTER locking, and the error fixture's witness is an attribute edit — a
    /// <i>Timer</i> setting moved from 3 to 5 minutes under <c>locked="yes"</c>. Nothing in the file distinguishes
    /// that value from a library default, and the id-ordering proxy that looked promising was REFUTED by
    /// measurement (it fires on nearly every locked product in every authentic project, because links and
    /// terminals legitimately get their ids after the product was placed). What decides it is the block's LIBRARY
    /// body, which the rule now receives through <see cref="ILibraryBlockSource"/> — declared, and skipped when the
    /// caller has no library, exactly as the capacity rows behave without controller limits.</para>
    ///
    /// <para><b>What <c>logic-master-block-modified</c> can and cannot see.</b> It reports a block that KEEPS its
    /// library identity while its name no longer matches the insert name that identity implies — the error
    /// fixture's <i>Kip tænd sluk (lokalt tilpasset)</i>, renamed and re-noted while still locked, with
    /// <c>Nummer</c>, <c>Version</c>, <c>Oprettet</c> and <c>Udviklet af</c> all surviving. It cannot see a block
    /// whose LOGIC diverges from the library while keeping the name, for the same reason as above.</para>
    /// </summary>
    public static class FunctionBlockShapeRules
    {
        /// <summary>The container holding a block's programs, and the only child tag that is one.</summary>
        private const string ProgramsContainer = "programs";

        private const string ProgramTag = "program_simple";

        /// <summary>
        /// The attributes a structural comparison IGNORES: identity, the rendered label, the icon and the note.
        /// Two programs are "identical events and commands" when their operands and methods match; a different
        /// label or note does not make a duplicate into an original.
        /// </summary>
        private static readonly ImmutableHashSet<string> IncidentalAttributes =
            ["id", "name", "icon", "note"];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-block-empty", NoPrograms),
                Rule(catalog, "logic-block-no-pins", NoPins),
                Rule(catalog, "logic-duplicate-program", DuplicatePrograms),
                Rule(catalog, "logic-master-block-modified", MasterBlockModified),
                Rule(catalog, "logic-block-locked-content", LockedContentEdited),
                Rule(catalog, "fb-user-authored", UserAuthored),
                Rule(catalog, "fb-holiday-input-custom-block", HolidayInputOnCustomBlock),
                Rule(catalog, "fb-revision-defective-confirmed", DefectiveRevisionConfirmed),
                Rule(catalog, "fb-revision-defective-reported", DefectiveRevisionReported),
                Rule(catalog, "fb-short-press-below-default", ShortPressBelowDefault(catalog)),
                Rule(catalog, "fb-provenance-rewritten", ProvenanceRewritten),
                Rule(catalog, "fb-master-missing-from-library", MasterMissingFromLibrary),
                Rule(catalog, "fb-master-version-differs", MasterVersionDiffers),
                Rule(catalog, "fb-pir-dusk-gated", PirDuskGated),
                Rule(catalog, "fb-pulse-constant-default", PulseConstantDefault(catalog)));
        }

        /// <summary>
        /// The three block revisions the MANUFACTURER confirmed defective, as exact (type, version) pairs.
        /// <para>
        /// <c>6.3.04</c> IS THE ONE THE SOURCE NAMES WITHOUT A LETTER, and its remedy resolves it: replace with
        /// <c>6.3.04b</c> or later. So the affected revisions are everything BELOW <c>b</c> — <c>a</c> and the
        /// VERSION-LESS form, which is a real shape rather than a defensive guess (the library ships
        /// <c>6.3.05</c> with an empty version). Enumerating the two is exact where a string comparison against
        /// "b" would quietly assume a single-letter ordering the format does not promise.
        /// </para>
        /// <para>
        /// The COMMUNITY-reported revisions are a different set behind a different code, because their evidence
        /// grade is different and the two rows' severities say so.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<(string Type, string Version)> ConfirmedDefectiveRevisions =
        [
            ("1.1.01", "c"),
            ("6.3.02", "d"),
            ("6.3.04", "a"),
            ("6.3.04", ""),
        ];

        /// <summary>
        /// The eight block revisions the COMMUNITY reported defective, as exact (type, version) pairs.
        /// <para>
        /// DISJOINT FROM <see cref="ConfirmedDefectiveRevisions"/> by construction, and a test asserts the two
        /// rows never both fire: a revision is either manufacturer-confirmed or community-reported, never graded
        /// twice.
        /// </para>
        /// <para>
        /// EIGHT, NOT ELEVEN. Three of the source's rows are excluded for structural reasons rather than
        /// convenience: one names no revision letter at all (a type-wide row would report every instance of a
        /// block whose defect is revision-specific), one is not a revision condition but a CO-OCCURRENCE of two
        /// types, and one names a revision the library CURRENTLY ships and the corpus carries — listing it would
        /// report vendor-current blocks on authentic files.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<(string Type, string Version)> ReportedDefectiveRevisions =
        [
            ("1.2.03", "c"),
            ("1.4.03", "b"),
            ("1.4.06", "a"),
            ("4.2.03", "a"),
            ("5.2.02", "c"),
            ("5.2.03", "d"),
            ("5.2.05", "a"),
            ("6.1.02", "b"),
        ];

        /// <summary>
        /// The revision <c>fb-short-press-below-default</c> is about, and the setting it reads.
        /// <para>WORTH REPORTING BECAUSE OF WHERE USERS ARRIVE FROM: <c>1.2.03.d</c> is what
        /// <c>1.2.03.c</c>'s own remedy recommends, so a user following that advice lands here.</para>
        /// </summary>
        private const string ShortPressBlockType = "1.2.03";

        private const string ShortPressBlockVersion = "d";

        private const string ShortPressSettingName = "Max tid for kort tryk";

        /// <summary>
        /// The pulse-counting block's master type, the exact name of its scaling constant, and the container the
        /// mirrored <c>.ifb</c> puts that constant in.
        /// <para><c>settings</c>, NOT <c>internalsettings</c>: the internal group holds only timers and scratch
        /// integers on this block, so a rule reading it would never fire.</para>
        /// </summary>
        private const string PulseBlockMasterType = "4.2.03";

        private const string PulseConstantName = "1 Kwh/M3";

        private const string SettingsContainer = "settings";

        /// <summary>
        /// A pulse-counting block still carrying the library's default scaling constant: if the physical meter
        /// differs, every reading is silently mis-scaled.
        /// <para>
        /// THE INSTANCE'S OWN VALUE IS BOUND, not the declared default. Here the two coincide by construction —
        /// a reported instance is one still at the default — but binding the threshold would be wrong the moment
        /// anyone added a fallback that reported a changed constant too, which an earlier draft proposed.
        /// </para>
        /// <para>An instance whose constant was CHANGED reports nothing: the decision has been made.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared default, are declared in.</param>
        private static ProjectInspection PulseConstantDefault(ProblemCatalog catalog)
        {
            double declaredDefault = Threshold(catalog, "fb-pulse-constant-default", "DefaultPulsesPerKwh");
            return inspection =>
            {
                foreach (ProjectElement block in Blocks(inspection.Analyses))
                {
                    if (block.GetAttribute(MasterTypeAttribute) != PulseBlockMasterType)
                    {
                        continue;
                    }

                    foreach (ProjectElement constant in Section(block, SettingsContainer))
                    {
                        if (constant.GetAttribute("name") != PulseConstantName
                            || constant.GetAttribute("inivalue") is not { Length: > 0 } raw
                            || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                out long pulses)
                            || pulses != declaredDefault)
                        {
                            continue;
                        }

                        inspection.Report(block, Arguments(("name", Name(block)), ("pulses", pulses)));
                    }
                }
            };
        }

        /// <summary>The PIR-and-timer block's master type, and the exact name of its twilight input pin.</summary>
        private const string PirBlockMasterType = "1.4.02";

        private const string DuskPinName = "Skumring";

        /// <summary>
        /// A PIR block whose twilight input is WIRED: it reacts to motion only while that input is ON, so a
        /// source that never turns ON makes it look dead.
        /// <para>WIRING IS THE CONDITION, not the pin's existence — every instance of this block type ships the
        /// pin, so a rule keyed on the name alone would report all of them.</para>
        /// <para>SILENT ON A STRIPPED BLOCK, by construction: this keys on <c>master_type</c>, which unlock and
        /// save-as remove. The entry records that coverage edge rather than leaving it to be found.</para>
        /// </summary>
        private static void PirDuskGated(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.GetAttribute(MasterTypeAttribute) != PirBlockMasterType)
                {
                    continue;
                }

                // Short-circuits on the first wired dusk pin instead of listing the block's whole subtree first.
                // Over the CHILDREN's subtrees rather than the block's own, so the block cannot match itself.
                bool gated = block.Children.Any(child => child.FindDescendantOrSelf(pin =>
                    pin.GetAttribute("name") == DuskPinName
                    && pin.Children.Any(half => half.Tag == ReciprocalTags.FollowLinkToTag)) is not null);
                if (gated)
                {
                    inspection.Report(block, Arguments(("name", Name(block))));
                }
            }
        }

        /// <summary>
        /// A block frozen at a revision the library does not hold, while holding the TYPE: a placed instance is
        /// never re-synced, and behaviour can change materially between revisions.
        /// <para>
        /// BOTH DIRECTIONS, because the versions are compared as a SET rather than ordered. Older and newer are
        /// the same finding, and no revision ordering has to be invented for letters whose sequence the format
        /// does not define.
        /// </para>
        /// <para>
        /// A MATCH AGAINST ANY HELD REVISION IS IN SYNC. The library may ship two revisions of one type side by
        /// side, so membership is the question — not equality with a single "current" one, which would report a
        /// perfectly current block whenever a second revision existed.
        /// </para>
        /// <para>
        /// THE VERSION-LESS FORM IS A REVISION LIKE ANY OTHER, and requiring a letter here was the same defect
        /// T055 found in <see cref="LibraryBlockIdentity.InsertName"/>: a large minority of the built-in
        /// library's entries ship a <c>master_type</c> and no <c>master_version</c>, so a rule that skipped an
        /// absent version was silent about all of them — precisely the families where a later lettered revision
        /// is the drift worth reporting. Absent and empty read alike, exactly as
        /// <see cref="ReportRevisionsIn"/> reads them.
        /// </para>
        /// </summary>
        private static void MasterVersionDiffers(IProjectInspection inspection)
        {
            if (inspection.Library is not { } library)
            {
                return;   // unreachable: the profile skips a rule declaring RequiresLibrary
            }

            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                string masterVersion = block.GetAttribute(MasterVersionAttribute) ?? string.Empty;
                if (block.GetAttribute(MasterTypeAttribute) is not { Length: > 0 } masterType
                    || !library.TryGetVersions(masterType, out EquatableArray<string> held)
                    || held.Contains(masterVersion))
                {
                    continue;
                }

                inspection.Report(block, Arguments(
                    ("name", Name(block)), ("frozen", VersionLabel(masterVersion)),
                    ("library", string.Join(", ", held.Select(VersionLabel)))));
            }
        }

        /// <summary>
        /// The Danish the sentence's <c>version {frozen}</c> and <c>version {library}</c> slots need for a
        /// revision that carries no letter.
        /// <para>BOTH SIDES, because both can be version-less: the block's own revision, and — reachable on
        /// today's library, where 29 families are held only that way — every revision the library holds. Binding
        /// the empty string produced <i>"indsat som version , mens …"</i>, which is a sentence the reader cannot
        /// parse rather than a fact they can act on.</para>
        /// </summary>
        /// <param name="version">The revision letter, or the empty string for the version-less form.</param>
        private static string VersionLabel(string version) =>
            version.Length == 0 ? "uden betegnelse" : version;

        /// <summary>
        /// A block claiming a master type the available library does not hold at ANY version: the project cannot
        /// be rebuilt from a clean install.
        /// <para>
        /// THE TYPE-ONLY LOOKUP, and nothing else. <see cref="ILibraryBlockSource.TryGetVersions"/> answers
        /// PRESENCE; <see cref="ILibraryBlockSource.TryGetBody"/> could not, because a miss on an exact identity
        /// is equally true of a type held at another revision. What the versions ARE is the next row's question.
        /// </para>
        /// <para>
        /// The library is guaranteed present here: the entry declares <c>RequiresLibrary</c>, so the profile
        /// skips this rule when the caller supplies none. The guard below is unreachable and says so.
        /// </para>
        /// </summary>
        private static void MasterMissingFromLibrary(IProjectInspection inspection)
        {
            if (inspection.Library is not { } library)
            {
                return;   // unreachable: the profile skips a rule declaring RequiresLibrary
            }

            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.GetAttribute(MasterTypeAttribute) is { Length: > 0 } masterType
                    && !library.TryGetVersions(masterType, out _))
                {
                    inspection.Report(block, Arguments(("name", Name(block)), ("master", masterType)));
                }
            }
        }

        /// <summary>The four attributes that carry a block's provenance — where it came from, and as what.</summary>
        private const string VendorFlagAttribute = "master_schneider_electric";

        private const string MasterNameAttribute = "master_name";

        private const string MasterTypeAttribute = "master_type";

        private const string MasterVersionAttribute = "master_version";

        /// <summary>
        /// A vendor block whose provenance TRIO has been stripped while its name survived: the vendor revision
        /// is no longer traceable, and the operation that did it cannot be undone.
        /// <para>
        /// THE EXACT COMPLEMENT of <see cref="UserAuthored"/>. That predicate needs both halves absent; this one
        /// needs the name present and all three of flag, type and version gone. The two partition the blocks
        /// between them, which is why neither has to know about the other beyond this comment.
        /// </para>
        /// </summary>
        private static void ProvenanceRewritten(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.GetAttribute(MasterNameAttribute) is not null
                    && block.GetAttribute(VendorFlagAttribute) is null
                    && block.GetAttribute(MasterTypeAttribute) is null
                    && block.GetAttribute(MasterVersionAttribute) is null)
                {
                    inspection.Report(block, Arguments(("name", Name(block))));
                }
            }
        }

        /// <summary>
        /// A block built from scratch: no Visual install will re-supply it, so its <c>.ifb</c> is worth archiving
        /// with the project.
        /// <para>
        /// BOTH HALVES ABSENT, and the second is the load-bearing one. Unlocking a vendor block or saving one to
        /// the library strips the VENDOR FLAG but keeps <c>master_name</c>, so the flag alone does not say
        /// "not an LK block" — a rule reading only the flag would call every unlocked library block user-built.
        /// </para>
        /// <para>
        /// The explicit <c>no</c> is a defensive read: the DTD default IS <c>no</c>, so default-omission means a
        /// vendor-written file never spells it out, and only an importer would.
        /// </para>
        /// </summary>
        private static void UserAuthored(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (IsUserAuthored(block))
                {
                    inspection.Report(block, Arguments(("name", Name(block))));
                }
            }
        }

        /// <summary>
        /// Revision <c>1.2.03.d</c> with its short-press maximum set BELOW the block's own default: short
        /// presses stop registering reliably.
        /// <para>A CONJUNCTION, and both halves matter. The revision alone is a perfectly good block, and the
        /// low value alone is unremarkable on any other revision — which is why the two exclusions are tested
        /// separately rather than as one negative case.</para>
        /// <para>THE SETTING IS FOUND BY NAME, ANYWHERE IN THE BLOCK. The library ships no <c>1.2.03</c> at all,
        /// so there is nothing to confirm which container the vendor writes it into; the name is specific enough
        /// to be the discriminator on its own, and scoping to a guessed container would risk a rule that
        /// silently never fires.</para>
        /// <para>STRICTLY BELOW: a block sitting exactly on the default is not below it. The threshold is read
        /// from the entry, so the declaration and the comparison cannot drift.</para>
        /// </summary>
        /// <para>MEASURED IN MILLISECONDS, not seconds, and that is deliberate on two counts. It is the unit the
        /// file stores, so the sentence quotes the value the author typed; and it keeps both the comparison and
        /// the message on whole numbers, so the boundary is exact rather than a float comparison and the Danish
        /// sentence never has to choose a decimal separator the engine has no precedent for.</para>
        /// <param name="catalog">The catalogue the declared default is read from.</param>
        private static ProjectInspection ShortPressBelowDefault(ProblemCatalog catalog)
        {
            long defaultMs = (long)Math.Round(
                Threshold(catalog, "fb-short-press-below-default", "ShortPressDefaultSeconds") * 1000);
            return inspection =>
            {
                foreach (ProjectElement block in Blocks(inspection.Analyses))
                {
                    if (block.GetAttribute(MasterTypeAttribute) != ShortPressBlockType
                        || block.GetAttribute(MasterVersionAttribute) != ShortPressBlockVersion)
                    {
                        continue;
                    }

                    foreach (ProjectElement timer in block.DescendantsAndSelf()
                        .Where(e => e.Tag == "resource_timer" && Name(e) == ShortPressSettingName))
                    {
                        if (Milliseconds(timer) is { } stored && stored < defaultMs)
                        {
                            inspection.Report(block, Arguments(
                                ("name", Name(block)), ("value", stored), ("default", defaultMs)));
                        }
                    }
                }
            };
        }

        /// <summary>A timer resource's stored duration in whole milliseconds, or null when it stores none.</summary>
        /// <param name="timer">The <c>resource_timer</c> element.</param>
        private static long? Milliseconds(ProjectElement timer)
        {
            long total = 0;
            bool any = false;
            foreach ((string attribute, long scale) in TimerParts)
            {
                if (timer.GetAttribute(attribute) is { } raw
                    && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long part))
                {
                    total += part * scale;
                    any = true;
                }
            }

            return any ? total : null;
        }

        /// <summary>The four attributes a <c>resource_timer</c> stores its duration in, and their scale in ms.</summary>
        private static readonly (string Attribute, long Scale)[] TimerParts =
            [("hour", 3_600_000), ("minute", 60_000), ("second", 1_000), ("millisecond", 1)];

        /// <summary>
        /// The block revisions the MANUFACTURER confirmed defective. An embedded revision is defective on every
        /// firmware, because nothing a controller upgrade does rewrites it.
        /// <para>NO LIBRARY IS NEEDED: a placed block carries its <c>master_type</c> and <c>master_version</c>
        /// in the <c>.vis</c>, so the revision the project embeds is decidable from the file alone.</para>
        /// <para>EXACT PAIRS, not types. The committed corpus embeds <c>1.1.01/e</c> ten times, one letter from
        /// the affected <c>1.1.01/c</c> — a predicate on the TYPE would report ten authentic vendor blocks.</para>
        /// </summary>
        private static void DefectiveRevisionConfirmed(IProjectInspection inspection) =>
            ReportRevisionsIn(inspection, ConfirmedDefectiveRevisions);

        /// <summary>
        /// The block revisions the COMMUNITY reported defective. Same subject as the confirmed row, different
        /// evidence grade — which is why they are two rows with two severities and two Danish sentences.
        /// <para>MOSTLY v2-ONLY, so a v3 project reaches one of these only through a MIGRATED project. That is
        /// why the row can be quiet on every authentic corpus file and still be worth shipping.</para>
        /// </summary>
        private static void DefectiveRevisionReported(IProjectInspection inspection) =>
            ReportRevisionsIn(inspection, ReportedDefectiveRevisions);

        /// <summary>
        /// Reports every placed block whose embedded revision is in <paramref name="revisions"/>.
        /// <para>SHARED so the two revision rows cannot drift apart in how they read a block's identity: they
        /// differ in their SET and in what their sentences claim, and in nothing else.</para>
        /// </summary>
        /// <param name="inspection">The run being inspected.</param>
        /// <param name="revisions">The exact (type, version) pairs this row reports.</param>
        private static void ReportRevisionsIn(
            IProjectInspection inspection, ImmutableHashSet<(string Type, string Version)> revisions)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.GetAttribute(MasterTypeAttribute) is not { } type)
                {
                    continue;
                }

                string version = block.GetAttribute(MasterVersionAttribute) ?? string.Empty;
                if (revisions.Contains((type, version)))
                {
                    inspection.Report(block, Arguments(
                        ("name", Name(block)), ("master", Revision(type, version))));
                }
            }
        }

        /// <summary>How a revision is written for a reader: <c>1.1.01.c</c>, or the bare type when version-less.</summary>
        /// <param name="type">The block's master type.</param>
        /// <param name="version">The block's master version, possibly empty.</param>
        private static string Revision(string type, string version) =>
            version.Length == 0 ? type : type + "." + version;

        /// <summary>
        /// A user-authored block carrying a holiday INPUT, reported to fail the upload to the controller.
        /// <para>THE INPUT CONTAINER ONLY, and that is not pedantry: an authentic corpus file carries a
        /// <c>resource_holiday</c> in EACH of a block's four containers, so a rule that walked the block would
        /// report one whose inputs hold none.</para>
        /// <para>CUSTOM IS <see cref="IsUserAuthored"/>, shared with <c>fb-user-authored</c> rather than
        /// re-derived — a vendor block whose flag was stripped keeps its master name and is not custom.</para>
        /// </summary>
        private static void HolidayInputOnCustomBlock(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (IsUserAuthored(block)
                    && Section(block, "inputs").Any(pin => pin.Tag == "resource_holiday"))
                {
                    inspection.Report(block, Arguments(("name", Name(block))));
                }
            }
        }

        /// <summary>
        /// Whether a block was built from scratch rather than supplied by a Visual install — the population
        /// <c>fb-user-authored</c> reports.
        /// <para>ONE DEFINITION, shared. <c>fb-holiday-input-custom-block</c> is scoped to the same population,
        /// and a second reading of "custom" would be a second answer to the same question.</para>
        /// </summary>
        /// <param name="block">The function block to classify.</param>
        private static bool IsUserAuthored(ProjectElement block)
        {
            bool vendorFlagged = block.GetAttribute(VendorFlagAttribute) is { } flag
                && !flag.Equals("no", StringComparison.Ordinal);
            return !vendorFlagged && block.GetAttribute(MasterNameAttribute) is null;
        }

        /// <summary>
        /// A block with no programs: it never does anything.
        /// <para>MEASURED: every block inserted through the application ships with a default <c>Program</c>, so
        /// this state requires the author to have DELETED it — which is why it fires twice in the error fixture
        /// (<c>Tom blok</c> and <c>Kobling</c>, both recorded as having had their default program deleted) and on
        /// no authentic project.</para>
        /// </summary>
        private static void NoPrograms(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (!Programs(block).Any())
                {
                    inspection.Report(block, Arguments(("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// A block with neither inputs nor outputs: nothing outside it can reach it.
        /// <para>
        /// READ LITERALLY, unlike the two documentation rows whose literal condition contradicted their own
        /// consequence: here the condition is stated in terms of the file and matches the consequence exactly. A
        /// block with no pins genuinely cannot be reached, whatever the author intended.
        /// </para>
        /// <para>
        /// MEASURED: 15 blocks across the corpus, every one of them a freshly inserted empty block left in place.
        /// The row's own reasonable-disagreement column covers the deliberate case (a block driven entirely by
        /// timers or internal state).
        /// </para>
        /// </summary>
        private static void NoPins(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (!Section(block, "inputs").Any() && !Section(block, "outputs").Any())
                {
                    inspection.Report(block, Arguments(("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// Two programs of one block with the same events and the same commands: one of them is redundant.
        /// <para>
        /// COMPARED STRUCTURALLY, on a signature of each program's subtree — tag plus every attribute except
        /// identity, label, icon and note, in document order. The operands and methods are what make two programs
        /// the same program; a re-labelled copy is still a copy.
        /// </para>
        /// <para>LOCATION: the second program, which is the one to delete. MEASURED: one pair in the whole corpus,
        /// in the error fixture's <c>Zoo</c> block, and none in any authentic project.</para>
        /// </summary>
        private static void DuplicatePrograms(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
                foreach (ProjectElement program in Programs(block))
                {
                    string signature = Signature(program);
                    if (seen.TryGetValue(signature, out ProjectElement? first))
                    {
                        inspection.ReportGroup(program, [first], Arguments(("block", Name(block))));
                    }
                    else
                    {
                        seen[signature] = program;
                    }
                }
            }
        }

        /// <summary>
        /// A block that keeps its library identity while its name no longer matches it: the block no longer matches
        /// the library version it claims to be.
        /// <para>
        /// SUBJECT: a block carrying master identity whose insert name is reconstructible and whose <c>name</c>
        /// differs from it. A block the user saved to the library keeps <c>master_name</c> but gets no
        /// <c>master_type</c>, so no insert name can be reconstructed and it is never reported — correct, since it
        /// IS its own library entry.
        /// </para>
        /// <para>
        /// WHAT IT SHARES A BORDER WITH: <c>name-default</c> reports a library block still AT its insert name, and
        /// this row reports one moved away from it, so between them every reconstructible library block draws
        /// exactly one advisory. That is a consequence of the catalogue carrying both rows, and both are dismissible
        /// per their own disagreement columns; it is recorded in the entry so a reader does not take it for a bug.
        /// </para>
        /// </summary>
        private static void MasterBlockModified(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (!LibraryBlockIdentity.HasMasterIdentity(block)
                    || LibraryBlockIdentity.InsertName(block) is not { } insertName
                    || block.GetAttribute("name") is not { Length: > 0 } name
                    || name == insertName)
                {
                    continue;
                }

                inspection.Report(block, Arguments(
                    ("block", name), ("master", block.GetAttribute("master_name") ?? string.Empty)));
            }
        }

        /// <summary>
        /// A LOCKED block whose stored content no longer matches the library body it claims: the lock no longer
        /// reflects the state it was meant to protect.
        /// <para>
        /// WHAT IS COMPARED, and it is deliberately narrow: the values a locked block still lets an author change.
        /// The vendor's lock disables a block's <c>Navn</c> field but not its variables' initial values, so this
        /// walks the four variable sections and compares each variable's STORED value — <c>inivalue</c> for a
        /// declared variable, <c>value</c> for a setting — against the same-named variable in the library body.
        /// A variable the library does not have at all is a structural difference rather than an edited value, and
        /// is left to <c>logic-master-block-modified</c>.
        /// </para>
        /// <para>
        /// PAIRED BY NAME, not by id: a placed block's ids are re-stamped at insert, so the library body and the
        /// placed copy share no id. Names are what the vendor keeps stable, and a renamed variable inside a locked
        /// block would itself be content the lock failed to protect.
        /// </para>
        /// <para>LOCATION: the variable, because that is the thing to put back. ARGUMENTS: the block's name, the
        /// variable's, and the value the library holds — so the reader can see what it was.</para>
        /// </summary>
        private static void LockedContentEdited(IProjectInspection inspection)
        {
            if (inspection.Library is not { } library)
            {
                return;   // unreachable: the profile skips a rule declaring RequiresLibrary
            }

            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.GetAttribute("locked") != "yes"
                    || block.GetAttribute("master_type") is not { Length: > 0 } type
                    || !library.TryGetBody(type, block.GetAttribute("master_version") ?? string.Empty,
                        out ProjectElement body))
                {
                    continue;
                }

                foreach ((ProjectElement variable, string stored) in StoredValues(block))
                {
                    if (LibraryValue(body, variable) is not { } original || original == stored)
                    {
                        continue;
                    }

                    inspection.Report(variable, Arguments(
                        ("block", Name(block)), ("variable", Name(variable))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// Every variable of a block's four sections that STORES a value, with that value. A variable storing
        /// nothing is at its default and cannot have been edited — the canonicalizer's omit-if-default rule again.
        /// </summary>
        private static IEnumerable<(ProjectElement Variable, string Stored)> StoredValues(ProjectElement block)
        {
            foreach ((string container, string _) in FunctionBlockSections.All)
            {
                foreach (ProjectElement variable in Section(block, container))
                {
                    if (StoredValue(variable) is { } stored)
                    {
                        yield return (variable, stored);
                    }
                }
            }
        }

        /// <summary>The same variable in the library body, by NAME, or null when the library has no such variable.</summary>
        private static string? LibraryValue(ProjectElement body, ProjectElement variable)
        {
            string name = Name(variable);
            foreach ((string container, string _) in FunctionBlockSections.All)
            {
                foreach (ProjectElement candidate in Section(body, container))
                {
                    if (candidate.Tag == variable.Tag && Name(candidate) == name)
                    {
                        return StoredValue(candidate) ?? string.Empty;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The value-bearing attributes a block variable can store, in one fixed order so two elements compare as
        /// one string.
        /// <para>
        /// THE TIMER PARTS ARE HERE BECAUSE THE FIXTURE'S WITNESS IS A TIMER, and finding that out cost a wrong
        /// first reading: a <c>resource_timer</c> does not store a <c>value</c> or an <c>inivalue</c> at all — its
        /// value is <c>hour</c>/<c>minute</c>/<c>second</c>/<c>millisecond</c>, which is why the error fixture's
        /// <i>Timer</i> (0:05:00, noted "Lokalt ændret timer efter låsning") was invisible to a reading that looked
        /// only at the two obvious attributes.
        /// </para>
        /// </summary>
        private static readonly ImmutableArray<string> ValueAttributes =
            ["value", "inivalue", "hour", "minute", "second", "millisecond"];

        /// <summary>
        /// What a variable stores, as one canonical string, or null when it stores nothing at all — the
        /// canonicalizer's omit-if-default rule again: a variable holding no value attribute is at its default and
        /// cannot have been edited.
        /// </summary>
        private static string? StoredValue(ProjectElement variable)
        {
            string? stored = null;
            foreach (string attribute in ValueAttributes)
            {
                if (variable.GetAttribute(attribute) is { } value)
                {
                    stored = stored is null ? $"{attribute}={value}" : $"{stored};{attribute}={value}";
                }
            }

            return stored;
        }


        /// <summary>
        /// A program's structural signature: its subtree in document order, each element as its tag plus the
        /// attributes that carry meaning. Built into one string so two programs compare in one comparison.
        /// </summary>
        private static string Signature(ProjectElement program)
        {
            StringBuilder builder = new();
            Append(builder, program);
            return builder.ToString();

            static void Append(StringBuilder builder, ProjectElement element)
            {
                builder.Append('[').Append(element.Tag).Append('(');
                foreach ((string name, string value) in element.Attrs
                    .Where(a => !IncidentalAttributes.Contains(a.Name))
                    .OrderBy(a => a.Name, StringComparer.Ordinal))
                {
                    builder.Append(name).Append('=').Append(value).Append(';');
                }

                builder.Append(')');
                foreach (ProjectElement child in element.Children)
                {
                    Append(builder, child);
                }

                builder.Append(']');
            }
        }

        private static IEnumerable<ProjectElement> Programs(ProjectElement block) =>
            Section(block, ProgramsContainer).Where(c => c.Tag == ProgramTag);

        private static IEnumerable<ProjectElement> Section(ProjectElement block, string container) =>
            block.FindChild(container) is { } section ? section.Children : [];
    }
}
