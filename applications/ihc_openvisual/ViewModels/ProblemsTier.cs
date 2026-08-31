using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// A tier of the Problemer panel — the axis its filter toggles, counts and default sort are keyed on.
///
/// <para><b>Why this is not <c>ValidationSeverity</c>.</b> A tier and a severity looked like the same thing while
/// they happened to be in one-to-one correspondence, and the panel keyed its toggles, counts and filtering
/// directly on the SDK's enum. They are not the same axis: a tier is a PRESENTATION grouping the host chooses,
/// and more than one tier can hold findings of one severity. A dictionary keyed on severity cannot express that
/// at all — the second tier would overwrite the first — so the panel names its own axis and maps rows onto it in
/// one place.</para>
///
/// <para><b>The order is the display order</b>, worst first: it is what the Alvor column sorts by and what the
/// toggle row reads left to right. Unlike the SDK's severity ordinals, these are the host's own and are read only
/// here, so there is nothing to keep stable for a caller.</para>
/// </summary>
public enum ProblemsTier
{
    /// <summary>
    /// A fault in the TOOL, not in the project — a rule that threw, an edit that broke, a handler that faulted.
    /// <para>
    /// FIRST, and therefore worst, which is a deliberate ranking rather than an accident of declaration order
    /// (D04). The tool failing outranks anything the tool reports: a crashed rule means rows are genuinely
    /// MISSING from a list the user will otherwise read as complete, so a fault has to be seen before the
    /// findings it may have swallowed.
    /// </para>
    /// <para>
    /// The one tier whose rows are not findings at all. It has no <see cref="ValidationSeverity"/>, carries no
    /// category about the project, and never reaches the findings export.
    /// </para>
    /// </summary>
    Internal,

    /// <summary>
    /// An Error finding whose rule ALSO refuses an operation — an undeclared attribute stops the save as well as
    /// being reported. Not a fourth severity: such a finding is an <see cref="ValidationSeverity.Error"/> like
    /// any other and withholds controller transfer for the same reason, and this tier says which faults stop the
    /// project being written at all.
    /// </summary>
    Fatal,

    /// <summary>A finding the engine reports as an error — the project is written, and this must be repaired.</summary>
    Error,

    /// <summary>A finding only the author can judge.</summary>
    Warning,

    /// <summary>A finding that is merely worth knowing.</summary>
    Info,
}
