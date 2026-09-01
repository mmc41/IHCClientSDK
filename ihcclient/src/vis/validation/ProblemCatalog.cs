using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The SDK's problem catalogue: every governed code, in one place, as compiled declarations.
    /// <para>
    /// Immutable and shareable across threads: built once, frozen, and holding no per-run state. That is what
    /// makes it correct for a GUI to hold one for the process lifetime while a background validation and a
    /// foreground command evaluation both use it. Nothing here makes an EDIT SESSION concurrent — serializing
    /// edits remains the document's job.
    /// </para>
    /// <para>
    /// Retired and ruled-out entries stay IN <see cref="Entries"/>. That is not an oversight and it is the whole
    /// reservation mechanism: an entry that stays keeps its id occupied, so
    /// <see cref="CatalogViolation.DuplicateCode"/> already refuses to reuse it for a different condition, and no
    /// separate reserved-id list exists to fall out of sync.
    /// </para>
    /// </summary>
    public sealed class ProblemCatalog
    {
        private readonly Dictionary<string, ProblemCatalogEntry> byCode;

        private ProblemCatalog(EquatableArray<ProblemCatalogEntry> entries)
        {
            Entries = entries;
            byCode = new Dictionary<string, ProblemCatalogEntry>(StringComparer.Ordinal);
            foreach (ProblemCatalogEntry entry in entries)
            {
                byCode[entry.Code.Value] = entry;
            }
        }

        /// <summary>The SDK's catalogue, assembled from the per-section declarations. Built once, frozen.</summary>
        public static ProblemCatalog Current { get; } = Validated(ProblemCatalogEntries.All);

        /// <summary>
        /// Every entry, ordered by code. The count a completeness check reads is <see cref="Total"/> — never a
        /// hard-coded number, since codes ship outside the original draft.
        /// </summary>
        public EquatableArray<ProblemCatalogEntry> Entries { get; }

        /// <summary>How many entries this catalogue holds, across every section.</summary>
        public int Total => Entries.Length;

        /// <summary>Looks up one entry. Ids are unique across ALL sections, not per section.</summary>
        /// <param name="code">The code to find.</param>
        /// <param name="entry">The entry it names, when it names one.</param>
        public bool TryGet(ProblemCode code, out ProblemCatalogEntry entry)
        {
            if (code.Value is { } value && byCode.TryGetValue(value, out ProblemCatalogEntry? found))
            {
                entry = found;
                return true;
            }

            entry = null!;
            return false;
        }

        /// <summary>The entries in one section.</summary>
        /// <param name="section">The section to list.</param>
        public EquatableArray<ProblemCatalogEntry> InSection(ProblemCatalogSection section) =>
            Entries.Where(e => e.Section == section).ToImmutableArray();

        /// <summary>
        /// Builds a catalogue from an explicit entry set — for tests needing a small or deliberately broken one,
        /// and for merging a host's own entries with <see cref="Current"/>'s.
        /// </summary>
        /// <param name="entries">The entries to hold; they are ordered by code.</param>
        public static ProblemCatalog From(EquatableArray<ProblemCatalogEntry> entries) =>
            new(entries.OrderBy(e => e.Code.Value, StringComparer.Ordinal).ToImmutableArray());

        /// <summary>
        /// Builds a catalogue and REFUSES a defective one — the door a composition root uses.
        /// <para>
        /// The entry-local invariants (<see cref="CatalogInvariants.CheckEntries"/>) hold here, and a violation
        /// throws rather than being reported: a catalogue is composed once, in a static initializer, and a
        /// defective one handed out there is a defect every reader inherits. <see cref="From"/> stays unchecked
        /// beside this, and must, because the invariants' own negative controls are deliberately broken
        /// catalogues that have to be constructible.
        /// </para>
        /// </summary>
        /// <param name="entries">The entries to hold.</param>
        /// <exception cref="InvalidOperationException">An entry breaks an invariant it declares.</exception>
        public static ProblemCatalog Validated(EquatableArray<ProblemCatalogEntry> entries)
        {
            ProblemCatalog catalog = From(entries);
            EquatableArray<CatalogDefect> defects = CatalogInvariants.CheckEntries(catalog);
            return defects.Length == 0
                ? catalog
                : throw new InvalidOperationException(
                    "The problem catalogue is malformed: "
                    + string.Join(", ", defects.Select(d => $"{d.Code.Value}={d.Violation}")));
        }
    }

    /// <summary>Why a catalogue is malformed.</summary>
    public enum CatalogViolation
    {
        /// <summary>Two entries share a code.</summary>
        DuplicateCode,

        /// <summary>An entry that nothing implements — legitimate during the phase-in, a defect at the end of it.</summary>
        EntryWithoutRule,

        /// <summary>A registered rule whose code has no entry.</summary>
        RuleWithoutEntry,

        /// <summary>
        /// A category present on an operation-outcome entry, or absent from a content entry. One member for one
        /// biconditional: category non-null ⟺ section is not
        /// <see cref="ProblemCatalogSection.OperationOutcomes"/>.
        /// </summary>
        CategoryMisplaced,

        /// <summary>
        /// An entry declares a refused operation that is not one of the six
        /// <see cref="Ihc.Vis.Problems.OperationCodes"/> heads — a cause code, a host code or a typo. Such a
        /// declaration names an operation no filter and no send gate can act on.
        /// </summary>
        RefusedOperationNotAnOperationHead,

        /// <summary>
        /// A <see cref="CatalogDisposition.Warning"/> or <see cref="CatalogDisposition.Info"/> entry declares a
        /// refused operation. Refusing is the hardest consequence a row can carry — the operation does not
        /// happen — so it cannot sit under a disposition whose whole meaning is that the project remains usable.
        /// The panel's Fatal tier reads exactly this pairing, and a demotion below Error would make a row that
        /// blocks a save read as advice.
        /// </summary>
        RefusedOperationOnAdvisoryDisposition,

        /// <summary>
        /// A <see cref="CatalogDisposition.Refusal"/> CONTENT row names no operation head. The disposition
        /// already asserts that an operation cannot proceed; without a declared head nothing can say which one,
        /// so no filter, no send gate and no panel tier can act on it. Operation-outcome rows are exempt because
        /// a head IS the operation rather than a cause of one.
        /// </summary>
        RefusalWithoutRefusedOperation,

        /// <summary>
        /// The internal-fault BICONDITIONAL, broken in one direction or the other. One member for one
        /// biconditional, as <see cref="CategoryMisplaced"/> is:
        /// <see cref="RuleKind.InternalFault"/> ⟺ no category, no faces, no refused operations and
        /// <see cref="CatalogDisposition.NotApplicable"/>.
        /// <para>
        /// Forward, it stops a row about a fault in the TOOL from carrying a claim about the project it could not
        /// examine — a severity, a face an executor would consume, an operation it refused. Backward, it stops the
        /// catalogue growing a second shape that means the same thing under a different kind, which is how two
        /// rows come to be treated differently for no reason a reader can find.
        /// </para>
        /// </summary>
        InternalFaultMisdeclared,
    }

    /// <summary>One violated invariant.</summary>
    /// <param name="Code">The code it is about.</param>
    /// <param name="Violation">Which invariant.</param>
    public readonly record struct CatalogDefect(ProblemCode Code, CatalogViolation Violation);

    /// <summary>
    /// The invariants a valid catalogue satisfies, checked over the declarations. The single door a completeness
    /// check goes through.
    /// <para>
    /// This is all that survives of a ten-type governance apparatus, and the reduction is not a judgement call —
    /// each removed piece had lost its job. Argument arity and type moved to the compiler, because each code's
    /// factory takes its declared slots as real parameters; a malformed entry cannot exist because there is no
    /// parser to accept one; the descriptor-versus-entry mismatch became impossible when the descriptor was
    /// folded in; and a release record's one unique justification — "a rename looks like a removal plus an
    /// addition" — was already false, since renames are forbidden and a retirement is recorded on the entry.
    /// </para>
    /// </summary>
    public static class CatalogInvariants
    {
        /// <summary>Every violation in this catalogue. Empty is the passing state.</summary>
        /// <param name="catalog">The catalogue to check.</param>
        /// <param name="implementedCodes">
        /// The codes something actually implements, for the entry-without-rule and rule-without-entry pair. Pass
        /// an empty set to check only the invariants that are about the declarations alone.
        /// </param>
        public static EquatableArray<CatalogDefect> Check(
            ProblemCatalog catalog, IReadOnlyCollection<ProblemCode> implementedCodes)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(implementedCodes);

            ImmutableArray<CatalogDefect>.Builder defects = ImmutableArray.CreateBuilder<CatalogDefect>();
            defects.AddRange(CheckEntries(catalog));
            if (implementedCodes.Count == 0)
            {
                return defects.ToImmutable();
            }
            return CheckRulePairing(catalog, implementedCodes, defects);
        }

        /// <summary>
        /// The half that needs NOTHING but the declarations: duplicate ids, category placement, refused-operation
        /// legality, and the internal-fault biconditional. Split out because that is exactly what a composition
        /// root can enforce — the rest asks which codes something implements, and only a completeness check knows
        /// that.
        /// <para>
        /// Before the split these invariants were reachable only from a test, so a defective entry reached
        /// production whenever the test that would have caught it was not run.
        /// </para>
        /// </summary>
        /// <param name="catalog">The catalogue to check.</param>
        /// <returns>Every entry-local violation. Empty is the passing state.</returns>
        public static EquatableArray<CatalogDefect> CheckEntries(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            ImmutableArray<CatalogDefect>.Builder defects = ImmutableArray.CreateBuilder<CatalogDefect>();
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (ProblemCatalogEntry entry in catalog.Entries)
            {
                if (!seen.Add(entry.Code.Value))
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.DuplicateCode));
                }

                bool wantsCategory = entry.Section != ProblemCatalogSection.OperationOutcomes;
                if (wantsCategory != (entry.Category is not null))
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.CategoryMisplaced));
                }

                bool refuses = entry.RefusedOperations.Length > 0;
                if (refuses && entry.RefusedOperations.Any(op => !OperationCodes.All.Contains(op)))
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.RefusedOperationNotAnOperationHead));
                }

                if (refuses && entry.Disposition is CatalogDisposition.Warning or CatalogDisposition.Info)
                {
                    defects.Add(
                        new CatalogDefect(entry.Code, CatalogViolation.RefusedOperationOnAdvisoryDisposition));
                }

                if (!refuses
                    && entry.Disposition == CatalogDisposition.Refusal
                    && entry.Section != ProblemCatalogSection.OperationOutcomes)
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.RefusalWithoutRefusedOperation));
                }

                // Both directions of one biconditional, so a fault row cannot describe a project and a row
                // shaped like a fault cannot go unlabelled. Written as an equality between two booleans rather
                // than as two ifs: that is what makes it a biconditional rather than two rules that could drift.
                bool saysNothingAboutAProject =
                    entry.Category is null
                    && entry.Faces == RuleFaces.None
                    && !refuses
                    && entry.Disposition == CatalogDisposition.NotApplicable;
                if ((entry.Kind == RuleKind.InternalFault) != saysNothingAboutAProject)
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.InternalFaultMisdeclared));
                }
            }

            return defects.ToImmutable();
        }

        // The other half: which codes something implements is not a property of the declarations, so it cannot
        // be answered where a catalogue is composed. Only a completeness check knows the implemented set.
        private static EquatableArray<CatalogDefect> CheckRulePairing(
            ProblemCatalog catalog, IReadOnlyCollection<ProblemCode> implementedCodes,
            ImmutableArray<CatalogDefect>.Builder defects)
        {
            HashSet<string> implemented = new(implementedCodes.Select(c => c.Value), StringComparer.Ordinal);
            foreach (ProblemCatalogEntry entry in catalog.Entries)
            {
                if (entry.Status == ProblemCodeStatus.Active && !implemented.Contains(entry.Code.Value))
                {
                    defects.Add(new CatalogDefect(entry.Code, CatalogViolation.EntryWithoutRule));
                }
            }

            foreach (ProblemCode code in implementedCodes)
            {
                if (!catalog.TryGet(code, out _))
                {
                    defects.Add(new CatalogDefect(code, CatalogViolation.RuleWithoutEntry));
                }
            }

            return defects.ToImmutable();
        }
    }
}
