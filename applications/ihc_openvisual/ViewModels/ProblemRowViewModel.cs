using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// One row of the Problemer panel, whatever it is a row ABOUT.
///
/// <para>The panel lists two different kinds of thing: findings about the project, and faults in the tool. They
/// share the listed columns and nothing else — a fault has no severity, no check family and no element — so what is
/// common here is exactly the values those columns bind, and everything a finding additionally knows stays on
/// the finding's own row type where no other row can be asked for it.</para>
///
/// <para><b>Why a base rather than one row type with nullable extras.</b> A single type would have had to answer
/// "which category?" and "which severity?" for a fault, and every answer is a claim about a project the fault
/// says nothing about. Making the question unaskable is the point: an internal row has no such member to return
/// a lie from.</para>
/// </summary>
public abstract partial class ProblemsPanelRowViewModel : ObservableObject
{
    /// <param name="occurrenceId">What addresses THIS row; see <see cref="OccurrenceId"/>.</param>
    protected ProblemsPanelRowViewModel(string occurrenceId) => OccurrenceId = occurrenceId;

    /// <summary>
    /// Which tier the row is listed under — the filter, the counts, the default sort and the row's own chrome
    /// all read this one answer, so the tier a row is counted under cannot differ from the tier it is hidden by.
    /// </summary>
    internal abstract ProblemsTier Tier { get; }

    /// <summary>The row's code — the Kode column and the id sort key.</summary>
    public abstract string Code { get; }

    /// <summary>The Danish sentence, verbatim. Never re-derived, never re-worded.</summary>
    public abstract string Message { get; }

    /// <summary>What the Kategori column shows.</summary>
    public abstract string CategoryLabel { get; }

    /// <summary>What the Element column shows.</summary>
    public abstract string ElementName { get; }

    /// <summary>The navigation anchor, or null when the row names no single element.</summary>
    public abstract ElementId? Element { get; }

    /// <summary>WHICH destination the row has — decided once, and the promise its tooltip makes.</summary>
    public abstract NavigationKind NavigationKind { get; }

    /// <summary>
    /// What names THIS row rather than its code. Assigned once at projection, so it does not move when the list
    /// is re-sorted.
    /// </summary>
    /// <remarks>
    /// <para>The code alone does not address a row. The authored error corpus emits several codes many times
    /// over, and the row's accessible sentence does not break the tie either: most of those sites share a name
    /// or carry none, and the messages take no argument that would separate them.</para>
    /// <para>It LEADS with the code so a client matching loosely on a code still reaches that code's rows.</para>
    /// </remarks>
    public string OccurrenceId { get; }

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
    /// <para>Without it a non-navigable row looks exactly like a navigable one and a click on it reads as a bug.
    /// The row is still LISTED — it is a real message — it just says, before the click, that it has nowhere to
    /// go.</para>
    /// <para>Two values, not one per kind. The cell shows the element's NAME, which is a fact whether or not the
    /// tree draws that element; WHERE the click lands is what <see cref="NavigationHint"/> carries. A third
    /// opacity for the ancestor case would be a shade the reader has no way to decode.</para>
    /// </remarks>
    public double ElementEmphasis => NavigationKind is NavigationKind.None ? 0.55 : 1.0;

    /// <summary>The row's tooltip: which element a click goes to, or why it goes nowhere.</summary>
    public string NavigationHint => NavigationKind switch
    {
        NavigationKind.Tree => "Klik for at vise elementet i træet.",
        NavigationKind.Ancestor => "Klik for at vise det nærmeste overordnede element i træet.",
        // A WHOLE-PROJECT finding has a window but no element, so the tree half of the sentence below would be
        // a promise nothing can keep (T046).
        NavigationKind.Dialog when Element is null => "Dobbeltklik for at åbne dialogen.",
        NavigationKind.Dialog => "Klik for at vise elementet i træet. Dobbeltklik for at åbne dialogen.",
        NavigationKind.Field => "Klik for at vise elementet i træet. Dobbeltklik for at åbne dialogen ved feltet.",
        _ => "Denne meddelelse peger ikke på et enkelt element.",
    };
}

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
public sealed partial class ProblemRowViewModel : ProblemsPanelRowViewModel
{
    public ProblemRowViewModel(
        ValidationFinding finding,
        ElementId? element,
        string elementName,
        NavigationKind navigationKind,
        string occurrenceId)
        : base(occurrenceId)
    {
        Finding = finding;
        Element = element;
        ElementName = elementName;
        NavigationKind = navigationKind;
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
    /// default sort all read <see cref="ProblemsPanelRowViewModel.Tier"/> instead. This is the window the
    /// fidelity tests read the engine's own answer through; nothing in the panel binds it.
    /// <para>
    /// A WINDOW ONLY A FINDING HAS. It is not on the row base, and cannot be: a fault in the tool has no
    /// severity, so a base member here would force every internal row to answer with one.
    /// </para>
    /// </summary>
    public ValidationSeverity Severity => Finding.Severity;

    /// <inheritdoc/>
    internal override ProblemsTier Tier => ProblemsPanelViewModel.TierOf(Finding);

    /// <summary>The finding's kebab-case code (<c>Problem.Code.Value</c>) — the Kode column and the id sort key.</summary>
    public override string Code => Finding.Code.Value;

    /// <summary>The Danish sentence, verbatim from the problem. Never re-derived, never re-worded.</summary>
    public override string Message => Finding.Problem.Message;

    /// <summary>
    /// The check family the finding belongs to — the Kategori column's sort key.
    /// <inheritdoc cref="Severity" path="/summary/para"/>
    /// </summary>
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
    public override ElementId? Element { get; }

    /// <summary>
    /// What the Element column shows: the element's name, or — when there is no element — the raw locator the
    /// engine recorded, so a whole-project row reads <c>utcs_project</c> rather than as a blank cell.
    /// </summary>
    public override string ElementName { get; }

    /// <summary>
    /// WHICH destination the row has — decided once when the result binds, and the promise its tooltip makes.
    /// <para>Never keyed on whether the finding had a primary location: <c>doc-project-info-blank</c> reports the
    /// project ROOT, which produces a perfectly non-null location whose element is null (the root carries no id
    /// attribute). Keying on the location would call that row navigable and then have nothing to select.</para>
    /// <para>Not re-derived afterwards either. It is a fact as of the validation RUN, exactly like the row's
    /// message and its severity: an element deleted after the run leaves the promise standing, and the honesty
    /// about that belongs to what the ACTIVATION reports, not to a tooltip that silently rewrote itself.</para>
    /// </summary>
    public override NavigationKind NavigationKind { get; }

    /// <summary>The check family's Danish name — the Kategori column's text.</summary>
    public override string CategoryLabel => ProblemsPanelViewModel.CategoryLabel(Category);
}

/// <summary>
/// A fault in the TOOL, as the Problemer panel binds it — a rule that threw, an edit that broke, a handler that
/// faulted.
///
/// <para>It fills the same columns as a finding and means something different in several of them. The Alvor
/// and Kategori cells both read <i>Intern fejl</i>: a real word rather than an em-dash, because the Kategori
/// column sorts under Danish collation and a dash has no place in that order. The Element cell IS an em-dash,
/// dimmed, because a fault in the software is about no element at all — and dimming is how every other
/// unnavigable row already says it has nowhere to go.</para>
///
/// <para>The sentence is the one the catalogue authored for the fault's code, rendered whole. Nothing here
/// composes it, and nothing binds the captured exception text: a stack trace in a cell is exactly the leak the
/// whole-message rule forbids.</para>
/// </summary>
public sealed partial class InternalErrorRowViewModel : ProblemsPanelRowViewModel
{
    /// <summary>What the Element cell shows: a fault is about no element, and an em-dash says so.</summary>
    private const string NoElement = "—";

    /// <param name="error">The fault this row was projected from.</param>
    /// <param name="occurrenceId">What addresses this row.</param>
    public InternalErrorRowViewModel(InternalError error, string occurrenceId)
        : base(occurrenceId) => Error = error;

    /// <summary>
    /// The fault, kept whole. Internal for the reason <see cref="ProblemRowViewModel.Finding"/> is: a binding
    /// path into the captured detail would put an engine stack trace in a cell, and the details surface is
    /// where that text is shown deliberately instead.
    /// </summary>
    internal InternalError Error { get; }

    /// <inheritdoc/>
    internal override ProblemsTier Tier => ProblemsTier.Internal;

    /// <inheritdoc/>
    public override string Code => Error.Code.Value;

    /// <inheritdoc/>
    public override string Message => Error.Message;

    /// <inheritdoc/>
    /// <remarks>
    /// The tier's own word, not a second copy of it. A fault has no <c>ValidationCategory</c>, so the Kategori
    /// cell says what the Alvor cell says — and saying it once is what keeps the two cells from ever disagreeing
    /// about the same row.
    /// </remarks>
    public override string CategoryLabel => TierLabel;

    /// <inheritdoc/>
    public override string ElementName => NoElement;

    /// <inheritdoc/>
    public override ElementId? Element => null;

    /// <inheritdoc/>
    public override NavigationKind NavigationKind => NavigationKind.None;
}
