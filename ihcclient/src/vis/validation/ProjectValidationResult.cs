#nullable enable
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The outcome of validating a project against the pre-serialize checklist (id uniqueness, IDREF
    /// resolution, reciprocal link/scene bijection, function-block child sequence, Latin-1
    /// encodability, ...). <see cref="Errors"/> keeps the flat message list; <see cref="Findings"/> carries
    /// the structured form (severity, rule id, locator) a GUI filters and navigates by. <see cref="IsValid"/>
    /// means no <see cref="ValidationSeverity.Error"/> findings — the advisory tiers alone leave a project valid.
    /// <para>
    /// <see cref="Faults"/> is the run's OTHER channel, and <see cref="IsComplete"/> the other question: a rule
    /// that threw is a defect in the tool rather than a statement about the file, so it never moves
    /// <see cref="IsValid"/> and never appears among <see cref="Findings"/>. A gate that must not act on a
    /// partial answer asks <see cref="IsComplete"/> before it asks <see cref="IsValid"/>.
    /// </para>
    /// </summary>
    public sealed record ProjectValidationResult(bool IsValid, EquatableArray<string> Errors)
    {
        /// <summary>A clean result with no errors.</summary>
        public static ProjectValidationResult Success { get; } = new(true, []);

        /// <summary>
        /// Builds a result from a finding list: <see cref="IsValid"/> is true when no finding is an
        /// <see cref="ValidationSeverity.Error"/>, and <see cref="Errors"/> collects those error messages. An empty
        /// finding list returns <see cref="Success"/>. The shared shaping every validation entry point uses.
        /// </summary>
        public static ProjectValidationResult FromFindings(EquatableArray<ProjectValidationFinding> findings)
        {
            if (findings.IsEmpty)
            {
                return Success;
            }
            EquatableArray<string> errors =
                [.. findings.Where(f => f.Severity == ValidationSeverity.Error).Select(f => f.Message)];
            return new ProjectValidationResult(errors.IsEmpty, errors) { Findings = findings };
        }

        /// <summary>Every finding, whatever its severity, in document-scan order.</summary>
        public EquatableArray<ProjectValidationFinding> Findings { get; init; } = [];

        /// <summary>
        /// The rules that THREW during the run this result reports, if any. Their findings are missing from
        /// <see cref="Findings"/>, which is why a result carrying one answers <see cref="IsComplete"/> false.
        /// </summary>
        /// <remarks>
        /// A SECOND CHANNEL, never folded into <see cref="Findings"/>: a finding is a statement about the
        /// project and a fault is a statement about the tool, so a crashed rule given a severity here would be
        /// the engine describing its own defect as a defect in the user's file. Empty by default, because a
        /// result built from findings alone — the definition builders' among them — has no executor behind it
        /// and no fault channel to fill.
        /// <para>
        /// An <c>init</c> member rather than a positional one so every existing construction site keeps
        /// compiling and keeps reading as complete, which is what it was.
        /// </para>
        /// </remarks>
        public EquatableArray<InternalError> Faults { get; init; } = [];

        /// <summary>
        /// Whether the run REACHED A VERDICT: true unless a rule threw.
        /// </summary>
        /// <remarks>
        /// A SEPARATE QUESTION FROM <see cref="IsValid"/>, and the reason both exist. <see cref="IsValid"/>
        /// answers "does this project have defects"; a crashed rule says nothing about that, so it must not
        /// make a clean project invalid. But the checklist is then short by an amount nothing can measure, so
        /// <see cref="IsValid"/> alone would be a clean bill of health produced by the crash. A caller that must
        /// not act on a partial answer — anything writing, uploading or publishing — reads this FIRST and
        /// refuses on it, under its own identity rather than the errors-found one: that sentence counts the
        /// errors a user must repair, and a faulted run with none would ask for zero repairs.
        /// <para>
        /// READ from <see cref="ValidationGate"/> rather than spelled here, for the reason <see cref="IsValid"/>
        /// is the gate's: every reader in the product answers this from one definition, so the refusal a
        /// transfer raises and the gate that greys it can never disagree.
        /// </para>
        /// </remarks>
        public bool IsComplete => Faults.IsComplete;

        /// <summary>The warning messages (vendor-tolerated deviations; never block a save/upload).</summary>
        /// <remarks>Computed, so it has no backing field and carries no equality significance of its own — the
        /// <see cref="Findings"/> it derives from is the stored member equality compares.</remarks>
        public ImmutableArray<string> Warnings =>
            [.. Findings.Where(f => f.Severity == ValidationSeverity.Warning).Select(f => f.Message)];

        /// <summary>The informational messages (advisory only; never block a save/upload).</summary>
        /// <remarks>The <see cref="Warnings"/> pattern one tier down: computed from <see cref="Findings"/>, so
        /// it has no backing field and carries no equality significance of its own.</remarks>
        public ImmutableArray<string> Infos =>
            [.. Findings.Where(f => f.Severity == ValidationSeverity.Info).Select(f => f.Message)];

        // Equality and hashing are the record's, over both EquatableArray members. Findings still participates,
        // which is what keeps a warnings-only result (valid, no errors, but carrying warning findings) distinct
        // from Success — a change in warnings is never hidden by equality-based diffing.

        public override string ToString() =>
            $"ProjectValidationResult(IsValid={IsValid}, IsComplete={IsComplete}, Errors=string[{Errors.Length}], " +
            $"Findings=ProjectValidationFinding[{Findings.Length}], Faults=InternalError[{Faults.Length}])";
    }
}
