#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The five FUNCTION-BLOCK SHAPE rows: a block that does nothing, one nothing can reach, one that says the
    /// same thing twice, one that no longer matches the library entry it claims, and one whose locked content was
    /// edited after locking.
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

        /// <summary>The five implemented rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-block-empty", NoPrograms),
                Rule(catalog, "logic-block-no-pins", NoPins),
                Rule(catalog, "logic-duplicate-program", DuplicatePrograms),
                Rule(catalog, "logic-master-block-modified", MasterBlockModified),
                Rule(catalog, "logic-block-locked-content", LockedContentEdited));
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
