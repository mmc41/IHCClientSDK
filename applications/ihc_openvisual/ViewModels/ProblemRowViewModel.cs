using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Model;
using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// One finding, as the Problemer panel binds it.
///
/// <para>Everything here is decided ONCE, when the result binds, and never re-derived: the message is the Danish
/// sentence the problem already carries, rendered whole; the code is the problem's own code; the element name was
/// resolved against the snapshot the run validated. A presentation path that re-worded any of them would be
/// inventing text the catalogue did not author.</para>
///
/// <para><b>Identity is an <see cref="ElementId"/>, never an element reference.</b> Every edit rebuilds the
/// immutable tree, so a retained element goes stale on the next keystroke — the row keeps the id and lets the
/// navigation step resolve it against whatever the current tree is.</para>
///
/// <para><b>Holding the <see cref="Finding"/> does not breach that.</b> The rule is about retaining a
/// <see cref="ProjectElement"/>, which is a node in a tree the next edit replaces. A
/// <see cref="ValidationFinding"/> is not a node: it is the immutable RESULT of one validation run, and its
/// <c>Primary.Locator</c> and <c>Primary.Xpath</c> describe the tree that run saw. It goes stale in the same
/// sense the whole result does — which is a state the panel already models and refuses to export from — rather
/// than in the sense a retained element does, where the object silently ceases to be part of any live tree.</para>
/// </summary>
public sealed partial class ProblemRowViewModel : ObservableObject
{
    public ProblemRowViewModel(
        ValidationFinding finding,
        ElementId? element,
        string elementName)
    {
        Finding = finding;
        Element = element;
        ElementName = elementName;
    }

    /// <summary>
    /// The finding this row was projected from, kept whole.
    /// <para>
    /// It is what an export of the panel's list is built from, and that is why it is retained rather than
    /// re-derived: the file must hold the findings the user is actually looking at, and a row that had only the
    /// columns could not produce one. The columns below all READ from it instead of copying it, so a row and
    /// its finding cannot come to disagree.
    /// </para>
    /// <para>
    /// Note the two things the row deliberately shows differently from what this carries: a duplicate-id row
    /// drops its <see cref="Element"/> anchor and its resolved name, while the finding still knows the locator
    /// and the exact node. That asymmetry is intended — the panel cannot choose between two holders, the file
    /// does not have to.
    /// </para>
    /// <para>
    /// Internal, unlike the columns beside it. Those are what a view renders; this is what the EXPORT is built
    /// from, and it reaches the problem's raw arguments and the catalogue's slots. A binding path into those
    /// would be a presentation path re-deriving user-facing text, which is exactly what the whole-message rule
    /// forbids — so the type does not offer one. The app's sources compile into the UI test assembly, so the
    /// fidelity tests read it unchanged.
    /// </para>
    /// </summary>
    internal ValidationFinding Finding { get; }

    /// <summary>
    /// The finding's SEVERITY, verbatim from the engine — not its tier. The two stopped being one value
    /// when Fatale fejl and Fejl came to share this one, so the icon column, the filter toggles and the
    /// default sort all read <see cref="Tier"/> instead. This is the window the fidelity tests read the
    /// engine's own answer through; nothing in the panel binds it.
    /// </summary>
    public ValidationSeverity Severity => Finding.Severity;

    /// <summary>
    /// Which tier the row is listed under — the filter, the counts, the default sort and the row's own
    /// chrome all read this one answer, so the tier a row is counted under cannot differ from the tier it
    /// is hidden by.
    /// </summary>
    internal ProblemsTier Tier => ProblemsPanelViewModel.TierOf(Finding);

    /// <summary>The finding's kebab-case code (<c>Problem.Code.Value</c>) — the Kode column and the id sort key.</summary>
    public string Code => Finding.Code.Value;

    /// <summary>The Danish sentence, verbatim from the problem. Never re-derived, never re-worded.</summary>
    public string Message => Finding.Problem.Message;

    /// <summary>The check family the finding belongs to — the Kategori column and its sort key.</summary>
    public ValidationCategory Category => Finding.Category;

    /// <summary>
    /// The navigation anchor: the parsed id of the finding's primary site, or null when it has none.
    /// </summary>
    /// <remarks>
    /// <para>Null for the three shapes that genuinely name no single element — a malformed id, a duplicate id
    /// that resolves to two, and a whole-project finding. The panel NEVER re-parses a locator to try to recover
    /// one: the engine already decided whether the token was well-formed, and second-guessing it here would be a
    /// second parser with its own opinion.</para>
    /// <para><b>The three do not all arrive null.</b> The engine nulls the malformed and whole-project ones —
    /// there is no id to parse — but a DUPLICATE token is well-formed, so it parses and reaches the panel as an
    /// ordinary non-null <c>FindingLocation.Element</c>. Which ids two elements answer to is a fact about the
    /// tree rather than about the finding, so it is <see cref="ProblemsPanelViewModel.IndexById"/> that finds the
    /// collision and <see cref="ProblemsPanelViewModel.ToRow"/> that drops the anchor.</para>
    /// </remarks>
    public ElementId? Element { get; }

    /// <summary>
    /// What the Element column shows: the element's name, or — when there is no element — the raw locator the
    /// engine recorded, so a whole-project row reads <c>utcs_project</c> rather than as a blank cell.
    /// </summary>
    public string ElementName { get; }

    /// <summary>
    /// Whether clicking the row goes anywhere. Keyed on <see cref="Element"/>, NOT on whether the finding had a
    /// primary location: <c>doc-project-info-blank</c> reports the project ROOT, which produces a perfectly
    /// non-null location whose element is null (the root carries no id attribute). Keying on the location would
    /// call that row navigable and then have nothing to select.
    /// </summary>
    public bool IsNavigable => Element is not null;

    /// <summary>The tier's Danish name — the Alvor column's text and part of the row's accessible name.</summary>
    /// <remarks>
    /// From the row's TIER rather than its severity, so a row sitting under the Fatale fejl toggle does not read
    /// "Fejl". The two agreed while every tier was a severity; a tier narrower than a severity separates them,
    /// and the panel's contract is that a filter button and its rows show the same word and the same glyph.
    /// </remarks>
    public string TierLabel => ProblemsPanelViewModel.TierLabel(Tier);

    /// <summary>The tier's icon asset — the Alvor column's glyph.</summary>
    /// <inheritdoc cref="TierLabel" path="/remarks"/>
    public string TierIcon => ProblemsPanelViewModel.TierIcon(Tier);

    /// <summary>The check family's Danish name — the Kategori column's text.</summary>
    public string CategoryLabel => ProblemsPanelViewModel.CategoryLabel(Category);

    /// <summary>
    /// What a screen reader announces for the whole row, and what a driver reads back. The columns are separate
    /// cells visually; to an automation client the row is one thing, so it says the tier, the sentence and where
    /// it is — in that order, because the tier is what decides whether the rest is worth hearing.
    /// </summary>
    public string AccessibleText => $"{TierLabel}: {Message} ({ElementName})";

    /// <summary>
    /// How strongly the Element cell is drawn — full for a row you can click through to, dimmed for one you
    /// cannot. A plain double rather than a brush or a style class, so the view-model stays free of Avalonia
    /// types; the cell binds it to Opacity.
    /// </summary>
    /// <remarks>
    /// Without it a non-navigable row looks exactly like a navigable one and a click on it reads as a bug. The
    /// row is still LISTED — it is a real finding — it just says, before the click, that it has nowhere to go.
    /// </remarks>
    public double ElementEmphasis => IsNavigable ? 1.0 : 0.55;

    /// <summary>The row's tooltip: where a click goes, or why it goes nowhere.</summary>
    public string NavigationHint => IsNavigable
        ? "Klik for at vise elementet i træet."
        : "Denne meddelelse peger ikke på et enkelt element.";
}
