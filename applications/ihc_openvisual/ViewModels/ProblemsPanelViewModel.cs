using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The four states the Problemer panel keeps distinct. They are a MODEL, not a rendering: the view decides what
/// each looks like, but nothing may collapse two of them, and in particular nothing may let
/// <see cref="Validating"/> read as <see cref="Clean"/>.
/// </summary>
public enum ProblemsState
{
    /// <summary>
    /// No result is bound for the current document yet — the first run of a generation. The list area says so.
    /// It must NEVER show the clean text: an unvalidated project reading as problem-free is a lie a user acts on.
    /// </summary>
    Validating,

    /// <summary>Up to date, and the run produced nothing.</summary>
    Clean,

    /// <summary>Up to date, with rows to show.</summary>
    Findings,

    /// <summary>
    /// The document has moved past the bound result — a quiet period is running or a run is in flight. The rows
    /// STAY visible and clickable (stale-while-revalidate); the list is never blanked.
    /// </summary>
    Stale,
}

/// <summary>
/// The Problemer panel's view-model: it READS a <see cref="ValidationMonitor"/> and presents what that found.
///
/// <para><b>It does not run the validation, and it does not decide what blocks.</b> Both live on the monitor,
/// beside the document they are about, because the blocking answer is asked by the transfer gate too — a panel
/// that owned it would be load-bearing for a gate that must work whether or not the panel was ever built. What
/// is left here is presentation and only presentation: rows, the sort, the tier filters, the four states, and
/// how long staleness must last before the panel says so.</para>
///
/// <para><b>What it does not do.</b> It holds no snapshot (every edit rebuilds the tree, so a retained one is
/// stale immediately) and it names no UI framework — the marshal back to the owning thread arrives as a delegate,
/// which is what lets the whole panel run deterministically in a test with no shell and no dispatcher.</para>
/// </summary>
public sealed partial class ProblemsPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// How long staleness must persist before the panel shows it. Below this a fast edit→validate cycle shows no
    /// indicator at all, which is the whole point of having a threshold rather than flashing on every keystroke.
    /// </summary>
    public static readonly TimeSpan StaleIndicatorDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How far the row area drops while the panel is visibly stale. Dimmed, not hidden and not disabled: the rows
    /// are the previous result and remain the best information there is.
    /// </summary>
    public const double StaleOpacity = 0.5;

    /// <summary>
    /// How long the dim takes to fade, in each direction. Short enough to feel immediate, long enough that the
    /// change reads as the panel doing something rather than as the list flickering.
    /// </summary>
    public static readonly TimeSpan StaleFadeDuration = TimeSpan.FromMilliseconds(150);

    /// <summary>Shown while the first result of a generation is still coming. Never the clean text.</summary>
    public const string ValidatingText = "Validerer projektet…";

    /// <summary>The empty-state text, shown only for a result that actually came back with nothing.</summary>
    public const string CleanText = "Ingen problemer fundet";

    private readonly ProjectWorkflow _session;
    private readonly ValidationMonitor _validation;
    private readonly Action<Action> _post;
    private readonly Action<string>? _setStatus;

    /// <summary>
    /// The ONE route-capability resolver. The row's promise and the activation's destination both come from it,
    /// so a tooltip saying "the field" and a click landing on a dialog cannot become two different answers.
    /// </summary>
    private readonly ProblemNavigationPlanner _planner;

    /// <summary>Where an ACTIVATED row's route is carried out. Optional, so the panel tests without a shell.</summary>
    private readonly Func<NavigationPlan, Task>? _activate;

    /// <summary>
    /// Where an internal row's activation goes. A SECOND delegate rather than a widening of
    /// <see cref="_activate"/>: that one carries a <see cref="NavigationPlan"/>, and a fault has no route to
    /// plan — pushing one through it would mean inventing a plan kind that means "not a navigation".
    /// </summary>
    private readonly Func<InternalError, Task>? _showInternalError;
    private readonly Func<FindingsExportRequest, Task>? _export;
    private readonly ITimer _staleTimer;
    private readonly EventHandler _onValidationChanged;

    private readonly EventHandler? _onInternalErrorsChanged;

    /// <summary>The result the rows were projected from, so an unchanged one is not projected twice.</summary>
    private ValidationOutcome? _shown;

    /// <summary>The generation the rows belong to, so a replacement empties the list before anything else.</summary>
    private int _shownGeneration;

    private bool _disposed;

    /// <param name="session">The document the rows name elements in.</param>
    /// <param name="validation">
    /// The run this panel presents. Taken as a parameter rather than read off <paramref name="session"/> so a
    /// test can drive every state transition over findings it wrote itself, without the cost — or the
    /// unpredictability — of a real validation. The shell passes the session's own.
    /// </param>
    /// <param name="export">
    /// Where the panel's list goes when the user asks for it. A delegate rather than a service: the panel
    /// decides WHAT is exported and must not learn about dialogs, files or the app service to do it. Optional
    /// so the panel can be tested without a shell.
    /// </param>
    /// <param name="setStatus">
    /// Where the panel says that an activation landed nowhere. A delegate for the same reason the others are:
    /// the panel owns the sentence, the shell owns where a sentence is shown.
    /// </param>
    /// <param name="activate">
    /// What an ACTIVATED row does — the whole navigation, which is the second tier of the gesture. A delegate
    /// for the same reason the others are: the panel decides WHICH route a row has and must not learn about
    /// trees or dialogs to carry one out.
    /// </param>
    public ProblemsPanelViewModel(
        ProjectWorkflow session,
        ValidationMonitor validation,
        Func<FindingsExportRequest, Task>? export = null,
        Action<string>? setStatus = null,
        Func<NavigationPlan, Task>? activate = null,
        InternalErrorLog? internalErrors = null,
        Func<InternalError, Task>? showInternalError = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validation);

        _session = session;
        _validation = validation;
        _internalErrors = internalErrors;
        _post = session.Post;
        _setStatus = setStatus;
        _export = export;
        // Over the SESSION's compose door: the descriptor is the SDK's to build, and asking it is what makes a
        // Field claim answerable rather than guessed.
        _planner = ProblemNavigationPlanner.Over(session.GetProductDialog);
        _activate = activate;
        _showInternalError = showInternalError;
        _staleTimer = session.Time.CreateTimer(_ => OnStaleThresholdElapsed(), null,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        Columns =
        [
            Header(ProblemsColumn.Severity, "Alvor"),
            Header(ProblemsColumn.Category, "Kategori"),
            Header(ProblemsColumn.Message, "Besked"),
            Header(ProblemsColumn.Element, "Element"),
            Header(ProblemsColumn.Code, "Kode"),
        ];

        Tiers =
        [
            new ProblemsTierViewModel(ProblemsTier.Internal, "Vis eller skjul interne fejl", ResortRows),
            new ProblemsTierViewModel(ProblemsTier.Fatal, "Vis eller skjul fatale fejl", ResortRows),
            new ProblemsTierViewModel(ProblemsTier.Error, "Vis eller skjul fejl", ResortRows),
            new ProblemsTierViewModel(ProblemsTier.Warning, "Vis eller skjul advarsler", ResortRows),
            new ProblemsTierViewModel(ProblemsTier.Info, "Vis eller skjul oplysninger", ResortRows),
        ];
        _tiers = Tiers.ToDictionary(t => t.Tier);
        ExportCommand = new AsyncRelayCommand(Export, () => CanExport);
        ShowSortOnHeaders();

        ProblemsColumnViewModel Header(ProblemsColumn column, string title) => new(column, title, SortBy);

        _onValidationChanged = (_, _) => OnValidationChanged();
        _validation.Changed += _onValidationChanged;
        if (_internalErrors is { } errors)
        {
            _onInternalErrorsChanged = (_, _) => OnInternalErrorsChanged();
            errors.Changed += _onInternalErrorsChanged;
            // Sync to whatever the sink already holds: a fault raised during start-up is there before this
            // panel exists, and it is exactly the fault with no other record. PUBLISHED here rather than left to
            // the monitor sync below: a fresh monitor is on generation zero with no result, so that sync binds
            // nothing at all and a start-up fault would sit in the projection unlisted.
            OnInternalErrorsChanged();
        }
        // Sync to whatever the monitor already holds: it starts with the document, and this panel does not.
        OnValidationChanged();
    }

    /// <summary>The findings of the last bound result, in the order the panel presents them.</summary>
    /// <remarks>
    /// A bulk collection because every one of the panel's interactive gestures — a bind, a tier toggle, a header
    /// click — REPLACES the whole list, and a per-item <c>Add</c> would drive one round of container and selection
    /// bookkeeping per row where a single Reset does.
    /// </remarks>
    public BulkObservableCollection<ProblemsPanelRowViewModel> Rows { get; } = [];

    /// <summary>
    /// The bound findings in the ENGINE's own order, kept so a re-sort starts from the document scan rather than
    /// from whatever the previous sort left behind. That is what makes every sort stable in the way that matters:
    /// rows with an equal key come out in document order, not in the order of the last sort.
    /// </summary>
    // The union the panel lists, and the two lists it is built from. Kept apart because they change for
    // different reasons and at different times: a validation run replaces every finding row and must leave the
    // faults standing, while a fault arriving must not disturb rows a run produced.
    private readonly List<ProblemsPanelRowViewModel> _asScanned = [];

    private readonly List<ProblemRowViewModel> _findingRows = [];

    private readonly List<InternalErrorRowViewModel> _internalRows = [];

    private readonly InternalErrorLog? _internalErrors;

    /// <summary>The five sortable headers, in screen order.</summary>
    public IReadOnlyList<ProblemsColumnViewModel> Columns { get; }

    /// <summary>
    /// The tiers, worst first — the filter toggles and their counts.
    /// </summary>
    /// <remarks>
    /// A filter hides ROWS and nothing else. Every tier's count, the session's blocking answer and this panel's state
    /// are all computed from the bound result, so switching a tier off never makes its findings look fixed.
    /// </remarks>
    public IReadOnlyList<ProblemsTierViewModel> Tiers { get; }

    private readonly Dictionary<ProblemsTier, ProblemsTierViewModel> _tiers;

    /// <summary>The Intern fejl tier — its toggle and its count.</summary>
    /// <inheritdoc cref="Fatals" path="/remarks"/>
    public ProblemsTierViewModel Internals => _tiers[ProblemsTier.Internal];

    /// <summary>The Fatale fejl tier — its toggle and its count.</summary>
    /// <remarks>
    /// Named accessors rather than an indexer or a positional binding, because the markup binds each toggle by
    /// name: <c>Tiers[0]</c> would tie the header row to the order this list happens to be built in, and a
    /// reordering would silently move a label onto another tier's button. All four read the same table, so
    /// there is still exactly one place a tier's word, glyph and count come from.
    /// </remarks>
    public ProblemsTierViewModel Fatals => _tiers[ProblemsTier.Fatal];

    /// <inheritdoc cref="Fatals"/>
    public ProblemsTierViewModel Errors => _tiers[ProblemsTier.Error];

    /// <inheritdoc cref="Errors"/>
    public ProblemsTierViewModel Warnings => _tiers[ProblemsTier.Warning];

    /// <inheritdoc cref="Errors"/>
    public ProblemsTierViewModel Infos => _tiers[ProblemsTier.Info];

    /// <summary>Which column the list is sorted by. Severity by default — worst first.</summary>
    public ProblemsColumn SortColumn { get; private set; } = ProblemsColumn.Severity;

    /// <summary>The sort direction. Two states, never three.</summary>
    public bool SortAscending { get; private set; } = true;

    /// <summary>
    /// Sorts by <paramref name="column"/>, reversing the direction when it is already the sorted column.
    /// </summary>
    /// <remarks>
    /// A newly chosen column always starts ASCENDING rather than inheriting the previous column's direction: the
    /// direction belongs to the question being asked, and carrying it over silently answers a new question the
    /// old way.
    /// </remarks>
    public void SortBy(ProblemsColumn column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        ResortRows();
        ShowSortOnHeaders();
    }

    /// <summary>Tells every header where the sort now is, so exactly one of them shows an arrow.</summary>
    private void ShowSortOnHeaders()
    {
        foreach (ProblemsColumnViewModel header in Columns)
            header.ShowSort(SortColumn, SortAscending);
    }

    /// <summary>
    /// Re-projects <see cref="Rows"/> from the engine-ordered findings under the current sort.
    /// </summary>
    /// <remarks>
    /// Collation is <b>da-DK</b> for every column carrying human text, and that is the point of not using a
    /// default <c>OrderBy(string)</c>: Æ, Ø and Å are the last three letters of the Danish alphabet, and an
    /// ordinal or invariant sort scatters them mid-alphabet — a name column a Danish reader cannot scan. The
    /// Kode column is the exception and sorts ORDINALLY, because a kebab-case rule id is a machine identifier
    /// rather than a word, and locale-aware collation of one would be an opinion about text that is not text.
    /// </remarks>
    private void ResortRows()
    {
        IEnumerable<ProblemsPanelRowViewModel> visible = _asScanned.Where(IsTierShown);
        IEnumerable<ProblemsPanelRowViewModel> sorted = SortColumn switch
        {
            ProblemsColumn.Severity => Order(r => (int)r.Tier),
            ProblemsColumn.Category => Order(r => r.CategoryLabel, DisplayOrder.Danish),
            ProblemsColumn.Message => Order(r => r.Message, DisplayOrder.Danish),
            ProblemsColumn.Element => Order(r => r.ElementName, DisplayOrder.Danish),
            ProblemsColumn.Code => Order(r => r.Code, StringComparer.Ordinal),
            _ => visible,
        };

        Rows.ReplaceAll(sorted);

        // The bulk-copy control reads Rows, so it moves whenever this does — including on a TIER TOGGLE, which
        // reaches the panel through here and nowhere else. Its binding is IsVisible, so a predicate that
        // changed without saying so leaves a button on screen answering the previous question.
        OnPropertyChanged(nameof(CanCopyInternals));

        IEnumerable<ProblemsPanelRowViewModel> Order<TKey>(
            Func<ProblemsPanelRowViewModel, TKey> key, IComparer<TKey>? comparer = null) =>
            // OrderBy/OrderByDescending are both STABLE, which is what preserves the engine's document order
            // among equal keys — the tie-break a reader navigating the tree depends on.
            SortAscending ? visible.OrderBy(key, comparer) : visible.OrderByDescending(key, comparer);
    }

    // A row whose tier has no toggle of its own is SHOWN, never hidden: a tier is a filtering and grouping
    // key and never a way to silence a finding, so an unrecognised one must fail towards visibility.
    private bool IsTierShown(ProblemsPanelRowViewModel row) =>
        !_tiers.TryGetValue(row.Tier, out ProblemsTierViewModel? tier) || tier.IsShown;

    [ObservableProperty] private ProblemsState _state = ProblemsState.Validating;

    /// <summary>
    /// Whether the staleness indicator is showing. Separate from <see cref="ProblemsState.Stale"/> on purpose:
    /// the panel is stale the instant an edit lands, but only SAYS so once that has lasted past
    /// <see cref="StaleIndicatorDelay"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowsOpacity))]
    private bool _isStaleIndicatorEngaged;

    /// <summary>
    /// The row area's opacity: full while up to date, <see cref="StaleOpacity"/> while visibly stale. The view
    /// binds it through a transition, so the change is a fade rather than a jump — and only a fade, never a
    /// disable: the rows stay clickable throughout.
    /// </summary>
    public double RowsOpacity => IsStaleIndicatorEngaged ? StaleOpacity : 1.0;

    /// <summary>
    /// The row the list has selected. Assigning one selects and nothing more: the panel is a list to read down,
    /// and a single click may not move the trees, switch editing mode or open a window under the reader.
    /// </summary>
    /// <remarks>
    /// Navigation is <see cref="ActivateRowAsync"/>'s, reached by double-click or Enter. The row still says
    /// where it would lead before either gesture, through its de-emphasis and its tooltip.
    /// </remarks>
    [ObservableProperty] private ProblemsPanelRowViewModel? _selectedRow;

    /// <summary>What the panel says when a row's element is nowhere on screen and nothing above it is either.</summary>
    public const string DeadEndStatus = "Elementet vises ikke i træet.";

    /// <summary>
    /// ACTIVATION — the second tier of the gesture, and the only tier that moves anything. A single click
    /// selects; a double-click or Enter reveals the element and takes the installer on to where the fix is made.
    /// </summary>
    /// <remarks>
    /// <para>The plan is re-derived HERE rather than carried on the row, and over the CURRENT project rather
    /// than the snapshot the run validated: the user is about to edit the document as it now stands, so that is
    /// the document the route has to be correct for. The row's promise came from the same planner, which is why
    /// the two agree without either being copied from the other.</para>
    /// <para>Both gestures come through this one entry point, so they cannot drift into doing different things —
    /// which is the whole content of the Enter/double-click parity requirement.</para>
    /// </remarks>
    /// <param name="row">The row being activated, or null when there is none.</param>
    public Task ActivateRowAsync(ProblemsPanelRowViewModel? row)
    {
        // ROW KIND first, before the planner and before the open-project guard below — and the order is the
        // point, not an optimisation. A fault has no element, so planning a route for one would ask the planner
        // about something that does not exist; and a fault is MOST likely to be the only thing on screen when
        // no project is open at all, which is exactly the case the guard below refuses. Branching after either
        // of them would make the internal row unopenable in the situation it matters most.
        if (row is InternalErrorRowViewModel fault)
        {
            return _showInternalError?.Invoke(fault.Error) ?? Task.CompletedTask;
        }
        if (row is not ProblemRowViewModel finding)
        {
            return Task.CompletedTask;
        }
        if (_activate is null || _session.Current is not { } project)
        {
            return Task.CompletedTask;
        }
        NavigationPlan plan = _planner.Plan(
            project, finding.Element, finding.Finding.TargetAttribute, finding.Finding.Code,
            finding.Finding.Fix);

        // A row that NAMED an element and still routes nowhere — the element is gone since the run, or neither
        // it nor anything above it is drawn — is the one case the gesture cannot show for itself, so it is said.
        // A row that named none says so before the click through its own de-emphasis, and needs no sentence.
        if (finding.Element is not null && plan.Kind is NavigationKind.None)
        {
            _setStatus?.Invoke(DeadEndStatus);
        }
        return _activate(plan);
    }

    /// <summary>The list area's own text, for the two states that have one. Empty where the rows speak.</summary>
    public string StateText => State switch
    {
        ProblemsState.Validating => ValidatingText,
        ProblemsState.Clean => CleanText,
        _ => string.Empty,
    };

    /// <summary>
    /// Completes when no validation is running or pending. Tests await it instead of sleeping; nothing in the
    /// panel reads it.
    /// </summary>
    public Task Idle => _validation.Idle;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _validation.Changed -= _onValidationChanged;
        if (_internalErrors is { } errors && _onInternalErrorsChanged is { } handler)
        {
            errors.Changed -= handler;
        }
        _staleTimer.Dispose();
    }

    /// <summary>
    /// The monitor moved: a result bound, the document moved past it, or the document was replaced. Runs on the
    /// owning thread — the monitor marshals a completed run there before it publishes.
    /// </summary>
    private void OnValidationChanged()
    {
        if (_disposed)
            return;

        if (_validation.Generation != _shownGeneration)
        {
            // Cleared IMMEDIATELY: rows about the previous file must not survive one frame over the new one.
            _shownGeneration = _validation.Generation;
            _shown = null;
            // The FINDING rows only, and the rebuild that follows is what publishes the cleared union. The faults
            // are the sink's to clear, and it clears them on the same generation move (D02) - emptying the bound
            // list here as well would be a second opinion about a lifetime that already has an owner, and a wrong
            // one the moment the two disagree.
            _findingRows.Clear();
            RebuildScannedRows();
        }

        // Reference identity, not value equality: the monitor hands back the SAME result object until a new run
        // binds, so this skips re-projecting rows on every staleness or fault notification.
        if (_validation.Result is { } outcome && !ReferenceEquals(outcome, _shown))
        {
            _shown = outcome;
            Bind(outcome);
        }

        RefreshState();
    }

    /// <summary>Projects one bound result into rows. On the owning thread, so the snapshot read below is safe.</summary>
    private void Bind(ValidationOutcome outcome)
    {
        // The panel's cost model, made observable. Binding walks the snapshot to index it by id and then
        // builds one row per finding, ON THE UI THREAD - so its cost is a function of the finding count, and
        // a project that produces a great many findings pays it on every validation run. The count is the
        // dimension that makes a slow bind explicable rather than merely slow.
        using Ihc.OperationScope scope = _telemetry.Start(nameof(Bind));
        scope.Activity?.SetTag(ihc_openvisual.Configuration.AppTelemetryRegistry.Attributes.ValidationFindingCount, outcome.Findings.Count);

        // Resolving names needs the snapshot the run validated, and this is where it is safe to read it back off
        // the session: the monitor publishes ONLY a result whose keys are still the latest it was notified with,
        // so no change event has landed since — meaning Current is that very snapshot instance, not a successor.
        Project? snapshot = _session.Current;

        Dictionary<ElementId, ProjectElement?> byId =
            snapshot is not null && outcome.Findings.Any(f => f.Primary?.Element is not null)
                ? IndexById(snapshot)
                : [];

        _findingRows.Clear();
        Dictionary<string, int> occurrences = [];
        foreach (ValidationFinding finding in outcome.Findings)
            _findingRows.Add(ToRow(finding, snapshot, byId, _planner, occurrences));
        RebuildScannedRows();
    }

    /// <summary>
    /// Rebuilds the union the panel lists, then re-sorts and re-counts it.
    /// </summary>
    /// <remarks>
    /// The faults go in FIRST, which costs nothing and means the unsorted order already matches the sorted one
    /// for the default column - Internal is the worst tier, so a reader who has not touched a header sees the
    /// same order either way.
    /// </remarks>
    private void RebuildScannedRows()
    {
        _asScanned.Clear();
        _asScanned.AddRange(_internalRows);
        _asScanned.AddRange(_findingRows);
        ResortRows();
        RecountRows();
    }

    /// <summary>
    /// Projects the sink into rows. Called on the owning thread: the sink marshals its announcement through the
    /// same post every other background result uses.
    /// </summary>
    private void OnInternalErrorsChanged()
    {
        if (_disposed || !ProjectInternalRows())
        {
            return;
        }
        RebuildScannedRows();
        RefreshState();

        // Reset, because the label is feedback about ONE copy: a "Kopieret" left standing after the list moved
        // would claim the reader has a copy of rows that were not in it.
        CopyInternalsText = CopyInternalsLabel;
    }

    /// <summary>Projects the sink into <c>_internalRows</c> and nothing else.</summary>
    /// <returns>False when there is no sink to project.</returns>
    private bool ProjectInternalRows()
    {
        if (_internalErrors is not { } log)
        {
            return false;
        }
        _internalRows.Clear();
        int at = 0;
        foreach (InternalErrorRow row in log.Rows)
        {
            _internalRows.Add(new InternalErrorRowViewModel(row.Error, $"{row.Error.Code.Value}@{at++}"));
        }
        return true;
    }

    /// <summary>The panel's entry point into the instrumentation core.</summary>
    private readonly Ihc.OperationTelemetry _telemetry =
        new(ihc_openvisual.Configuration.AppTelemetryRegistry.Surface, nameof(ProblemsPanelViewModel));

    /// <summary>
    /// Every element in the snapshot that carries an id, keyed by it — <b>with a null value against an id that
    /// more than one element carries</b>.
    /// </summary>
    /// <remarks>
    /// <para>ONE walk of the tree, not one per finding. <c>Project.FindById</c> is a depth-first scan with a
    /// delegate call per element, so resolving a name per row costs findings × elements — 150 rows over a
    /// 2 000-element project is the normal case here, not the edge, and this runs on the owning thread.</para>
    /// <para><b>A second holder ERASES the entry rather than being dropped.</b> A dictionary cannot hold two
    /// elements under one key, and keeping the first is the one resolution that cannot be told apart from a
    /// correct one: every row anchored at that token then wears the first holder's name and navigates there,
    /// including the rows about the second. Recording the collision instead lets <see cref="ToRow"/> say what is
    /// true — that the token names no single element — which is what the panel already does for the two other
    /// shapes that name none (a malformed id, and a finding about the project as a whole).</para>
    /// <para>The engine does not decide this for the panel: a duplicate <c>_0x</c> token is perfectly
    /// well-formed, so it parses and <c>FindingLocation.Element</c> is non-null on every one of the colliding
    /// sites. Whether two elements answer to it is a fact about the TREE, and this is where the tree is read.</para>
    /// </remarks>
    internal static Dictionary<ElementId, ProjectElement?> IndexById(Project snapshot)
    {
        Dictionary<ElementId, ProjectElement?> byId = [];
        foreach (ProjectElement element in snapshot.Root.DescendantsAndSelf())
        {
            if (element.Id is { } id && !byId.TryAdd(id, element))
                byId[id] = null;
        }

        return byId;
    }

    /// <summary>
    /// Which tier a row belongs to — the ONE place a finding is classified, read by the filter, the counts and
    /// the row's own chrome alike. A second classifier would let the tier a row is counted under and the tier it
    /// is hidden by become different answers.
    /// </summary>
    /// <remarks>
    /// Every arm is NAMED and the default throws, here and in the three below. A discard arm would have quietly
    /// filed a severity nobody had thought about — a fifth member, a cast integer off a stale binding — as
    /// Information, which is the one tier a user is least likely to look at. Better a loud failure at the
    /// classifier than a finding that silently disappears into the bottom of the list.
    /// </remarks>
    public static ProblemsTier TierOf(ValidationFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.Severity switch
        {
            // The PAIR is the definition, and neither half alone: an Error that refuses nothing is ordinary, and a
            // Warning that refused something would not be blocking. The fact travels ON the finding — the panel may
            // not read the SDK catalogue — so this asks the row it was given and nothing else.
            ValidationSeverity.Error when finding.RefusedOperations.Length > 0 => ProblemsTier.Fatal,
            ValidationSeverity.Error => ProblemsTier.Error,
            ValidationSeverity.Warning => ProblemsTier.Warning,
            ValidationSeverity.Info => ProblemsTier.Info,
            _ => throw new ArgumentOutOfRangeException(
                nameof(finding), finding.Severity, "Unknown validation severity"),
        };
    }

    /// <summary>
    /// The severity a tier reports as — the value the findings export records, since the file format speaks the
    /// SDK's severities and not the panel's tiers.
    /// </summary>
    /// <inheritdoc cref="TierOf" path="/remarks"/>
    public static ValidationSeverity? SeverityOf(ProblemsTier tier) => tier switch
    {
        // Fatal and Error are ONE severity. That is why the export cannot state its tier filters through
        // severities alone, and why hiding one of the two tiers is invisible in that attribute.
        ProblemsTier.Fatal => ValidationSeverity.Error,
        // NULL, and nullable for this one member: a fault in the tool is not a finding about the project, so
        // every severity here would be a claim about a project this tier says nothing about. Making it
        // representable rather than throwing is what makes D05 structural — the export below filters the nulls
        // out, so an internal row cannot reach a findings file even if someone forgets that it must not.
        ProblemsTier.Internal => null,
        ProblemsTier.Error => ValidationSeverity.Error,
        ProblemsTier.Warning => ValidationSeverity.Warning,
        ProblemsTier.Info => ValidationSeverity.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown problems tier"),
    };

    /// <summary>
    /// The Danish name of a tier, in the SINGULAR — the same string labels the filter chip and each ROW's
    /// Alvor cell, and a row is one finding. That is why this reads "Fatal fejl" and not "Fatale fejl", and
    /// why the Warning tier is "Advarsel" and not "Advarsler". The specification names the tiers in the
    /// plural because it is describing groups; the UI names one finding at a time.
    /// </summary>
    /// <inheritdoc cref="TierOf" path="/remarks"/>
    public static string TierLabel(ProblemsTier tier) => tier switch
    {
        ProblemsTier.Internal => "Intern fejl",
        ProblemsTier.Fatal => "Fatal fejl",
        ProblemsTier.Error => "Fejl",
        ProblemsTier.Warning => "Advarsel",
        ProblemsTier.Info => "Information",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown problems tier"),
    };

    /// <summary>
    /// The asset a tier's icon column shows — one glyph per tier, and the same one on the filter toggle and on
    /// every row of that tier.
    /// </summary>
    /// <inheritdoc cref="TierOf" path="/remarks"/>
    public static string TierIcon(ProblemsTier tier) => tier switch
    {
        ProblemsTier.Internal => "/Assets/severity-internal.svg",
        ProblemsTier.Fatal => "/Assets/severity-fatal.svg",
        ProblemsTier.Error => "/Assets/severity-error.svg",
        ProblemsTier.Warning => "/Assets/severity-warning.svg",
        ProblemsTier.Info => "/Assets/severity-info.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown problems tier"),
    };

    /// <summary>
    /// The Danish name of a check family.
    /// </summary>
    /// <remarks>
    /// The SDK offers two faces and neither belongs on a Danish screen: English member names
    /// (<c>FileIntegrity</c>, <c>Wiring</c>, …) and three-letter short codes (<c>INT</c>, <c>WIR</c>, …). So the
    /// panel names them, once, here. This is a TAXONOMY label — a filter and grouping key the host chooses, the
    /// same way it chooses "Fejl" for a severity — not user-facing message text, so it does not breach the rule
    /// that a message renders whole and is never re-worded downstream.
    /// </remarks>
    public static string CategoryLabel(ValidationCategory category) => category switch
    {
        ValidationCategory.FileIntegrity => "Filintegritet",
        ValidationCategory.Wiring => "Forbindelser",
        ValidationCategory.Logic => "Logik",
        ValidationCategory.Scenes => "Scenarier",
        ValidationCategory.Addressing => "Adressering",
        ValidationCategory.DeviceSettings => "Enhedsindstillinger",
        ValidationCategory.Documentation => "Dokumentation",
        ValidationCategory.ProjectStructure => "Projektstruktur",
        _ => category.ToString(),
    };

    /// <summary>One finding, projected against the snapshot the run validated and its <see cref="IndexById"/>.</summary>
    /// <param name="occurrences">
    /// How many rows each (code, site) pair has already produced, carried ACROSS the projection so a repeat can be
    /// told from the first. Optional: a caller projecting a single finding has nothing to disambiguate it against.
    /// </param>
    internal static ProblemRowViewModel ToRow(
        ValidationFinding finding, Project? snapshot, Dictionary<ElementId, ProjectElement?> byId,
        ProblemNavigationPlanner planner, Dictionary<string, int>? occurrences = null)
    {
        ElementId? element = finding.Primary?.Element;

        // The raw locator, not a blank: a whole-project row saying `utcs_project` tells a reader WHERE the engine
        // looked, and an empty cell tells them nothing. It is also the fallback for an element whose name is
        // present but EMPTY — which is exactly the `name-empty` row this panel exists to show, so NameOr rather
        // than a null check: a canonicalized project omits an empty name, so it reads back as "" and never null.
        string fallback = finding.Primary?.Locator ?? string.Empty;
        string name = fallback;

        if (element is { } id && snapshot is not null && byId.TryGetValue(id, out ProjectElement? holder))
        {
            if (holder is null)
            {
                // The index says TWO elements answer to this id, so the row keeps neither the name nor the
                // anchor. Both would be the same guess at which of the two the finding is about — and the
                // navigating half of that guess would act on it in silence, moving the tree to an element the
                // row was never about. It leads nowhere instead, which is what its dimmed cell and its tooltip
                // already say, and the locator left in the cell is the token the two share.
                element = null;
            }
            else
            {
                name = snapshot.NameOr(holder, fallback);
            }
        }

        // The finding travels WHOLE. The row reads its columns off it rather than copying them, and an export
        // of the panel's list is built from it — so what the user sees and what the file holds are one value.
        // The kind comes from the PLANNER, not from a second derivation beside it. That is what makes the
        // row's promise and its activation route the same value rather than two opinions that agree today.
        NavigationKind kind = snapshot is { } project
            ? planner.Plan(project, element, finding.TargetAttribute, finding.Code, finding.Fix).Kind
            : NavigationKind.None;

        return new ProblemRowViewModel(finding, element, name, kind, OccurrenceIdOf(finding, occurrences));
    }

    /// <summary>
    /// The row's per-occurrence identity: the code plus the site the engine recorded, and an ordinal only where
    /// even that pair repeats — which it can, since one rule may report the same element about two different
    /// attributes.
    /// </summary>
    /// <remarks>
    /// The finding's own <c>Primary.Locator</c>, never a re-derived one. It is what the engine wrote down about
    /// where it looked, so it survives an element that has since been renamed or deleted — and a whole-project
    /// row, which records a locator but no element, is named by it too.
    /// </remarks>
    private static string OccurrenceIdOf(ValidationFinding finding, Dictionary<string, int>? occurrences)
    {
        string identity = $"{finding.Code.Value}@{finding.Primary?.Locator ?? string.Empty}";
        if (occurrences is null)
            return identity;
        int seen = occurrences.TryGetValue(identity, out int count) ? count + 1 : 1;
        occurrences[identity] = seen;
        return seen == 1 ? identity : $"{identity}#{seen}";
    }


    /// <summary>
    /// Counts come from the BOUND RESULT, never from <see cref="Rows"/>. A count beside a filter toggle answers
    /// "how many of these does the project have?", and computing it from the filtered list would answer "how many
    /// are you currently looking at?" — which reads, on a tier that has just been switched off, as though its
    /// findings had been fixed.
    /// </summary>
    private void RecountRows()
    {
        foreach (ProblemsTierViewModel tier in Tiers)
            tier.Count = 0;

        foreach (ProblemsPanelRowViewModel row in _asScanned)
        {
            if (_tiers.TryGetValue(row.Tier, out ProblemsTierViewModel? tier))
                tier.Count++;
        }
    }

    private void RefreshState()
    {
        ProblemsState next = _validation switch
        {
            // FIRST, before the two arms below, and that order is the whole point. A fault raised while starting
            // up, or by an open that failed, arrives when no result is bound and the panel would otherwise say
            // "Validerer projektet..." across a row that is already listed - a sentence that is both false and
            // painted over the evidence. A listed fault means the panel HAS something to show, whatever the
            // validation run is doing, so it resolves to Findings.
            //
            // No sixth ProblemsState member: Findings already means "there are rows", the stale indicator keeps
            // its own separate job, and a state that existed only to say which KIND of row is listed would have
            // to be answered by every reader of State that does not care.
            _ when _internalRows.Count > 0 => ProblemsState.Findings,
            { Result: null } => ProblemsState.Validating,
            { IsStale: true } => ProblemsState.Stale,
            // _asScanned, not Rows: the clean state is a statement about the RESULT, and a list emptied by the
            // severity filters is not a clean project. Reading Rows here would tell a user who switched every
            // tier off that their project is fine because they hid the evidence.
            _ => _asScanned.Count == 0 ? ProblemsState.Clean : ProblemsState.Findings,
        };

        if (State != next)
        {
            State = next;
            OnPropertyChanged(nameof(StateText));

            // Inside the STATE-CHANGED block on purpose. Export is gated on State and on nothing else, so a
            // tier toggle — which moves the rows but never the state — correctly does not notify: under this
            // gate hiding a tier changes what the file contains, never whether it can be written.
            ExportCommand.NotifyCanExecuteChanged();
            // The button's grey and the sentence explaining it move with the same state, so they are raised
            // here too — a command that quietly stops executing while its control stays lit, or stays greyed
            // under a stale reason, is the failure this block exists to prevent.
            OnPropertyChanged(nameof(ExportAvailability));
            OnPropertyChanged(nameof(ExportHint));
        }

        if (next == ProblemsState.Stale)
        {
            _staleTimer.Change(StaleIndicatorDelay, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _staleTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            IsStaleIndicatorEngaged = false;
        }
    }

    private void OnStaleThresholdElapsed() =>
        // Through the marshal: the clock's callback arrives on a pool thread, and this sets bound UI state.
        _post(() =>
        {
            if (!_disposed && State == ProblemsState.Stale)
                IsStaleIndicatorEngaged = true;
        });
    // ── Export ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The export control's automation id — a fourth sibling of the panel's own vocabulary.</summary>
    // ── The bulk copy of the internal rows ──────────────────────────────────────────────────────────────────

    public const string CopyInternalsAutomationId = "problems.copy-internals";

    /// <summary>The control at rest.</summary>
    public const string CopyInternalsLabel = "Kopiér interne fejl";

    public const string CopyInternalsAccessibleName = "Kopiér alle interne fejl";

    /// <summary>After a successful copy — the same in-place feedback the details dialog gives.</summary>
    public const string CopyInternalsDoneLabel = "Kopieret";

    /// <summary>
    /// Shown only when there is something to copy. A control that is always there and usually does nothing is
    /// worse than one that appears with its subject: the Internal tier is empty in every healthy session, and a
    /// permanent button would invite the reader to wonder what it would have copied.
    /// <para>
    /// LISTED, not held, and asked of the SAME list <see cref="BuildInternalsPayload"/> assembles from. Counting
    /// the rows HELD left the button offered while the Internal tier was hidden, where the payload is empty: a
    /// control inviting the reader to copy nothing. Reading <see cref="Rows"/> rather than restating the filter
    /// that produced it is what keeps the two answers equal through whatever <see cref="ResortRows"/> filters on
    /// next.
    /// </para>
    /// </summary>
    public bool CanCopyInternals => Rows.OfType<InternalErrorRowViewModel>().Any();

    /// <summary>
    /// EVERY listed internal error, assembled the way the details dialog assembles ONE. That is the whole
    /// content of this control: it exists for the fault storm, where a dialog at a time will not do, and it is
    /// the answer to the findings export deliberately carrying none of these (D05).
    /// <para>
    /// LISTED, not held: the tier toggle filters this exactly as it filters the rows, because what the reader
    /// asked to copy is what the reader can see.
    /// </para>
    /// </summary>
    public string BuildInternalsPayload() => string.Join(
        $"{Environment.NewLine}{Environment.NewLine}────────{Environment.NewLine}{Environment.NewLine}",
        Rows.OfType<InternalErrorRowViewModel>()
            .Select(row => new InternalErrorViewModel(row.Error, AppVersion).Payload));

    /// <summary>The app build the payload records. Overridable so a test can pin the assembled text exactly.</summary>
    internal string AppVersion { get; init; } = Ihc.Bootstrap.TelemetryBootstrap.GetAppVersionStr();

    [ObservableProperty] private string _copyInternalsText = CopyInternalsLabel;

    /// <summary>The copy succeeded; say so on the control.</summary>
    public void MarkInternalsCopied() => CopyInternalsText = CopyInternalsDoneLabel;

    /// <summary>There was no clipboard. The same coded refusal the details dialog raises, said the same way.</summary>
    public void MarkInternalsCopyUnavailable() =>
        CopyInternalsText = HostProblems.ClipboardUnavailable().Message;

    public const string ExportAutomationId = "problems.export";

    /// <summary>What the button says.</summary>
    public const string ExportLabel = "Eksportér…";

    /// <summary>
    /// The button's glyph. Beside the label rather than instead of it: the three tier chips it sits with are
    /// icon-and-word, and a lone glyph in that row would be the one control a reader has to hover to identify.
    /// </summary>
    public const string ExportIcon = "/Assets/export_save.svg";

    /// <summary>What a screen reader announces, and what a driver addresses it by.</summary>
    public const string ExportAccessibleName = "Eksportér fejlliste";

    /// <summary>The tooltip and help text while the export is available, saying what the file is.</summary>
    public const string ExportHelpText = "Gem panelets liste som en XML-fil";

    /// <summary>Why the export is withheld in <see cref="ProblemsState.Validating"/>: there is no bound list yet.</summary>
    public const string ExportWhileValidatingReason = "Vent til valideringen er færdig.";

    /// <summary>Why it is withheld in <see cref="ProblemsState.Stale"/>: the rows are about a superseded tree.</summary>
    public const string ExportWhileStaleReason = "Projektet er ændret siden listen blev dannet.";

    /// <summary>
    /// Whether the panel's list can be written to a file — and, when it cannot, the Danish sentence saying why.
    ///
    /// <para>An <see cref="Availability"/> rather than a bare bool, and that is the point of it: this is the same
    /// value the command registry hands every persistent surface, so a greyed export explains itself exactly as a
    /// greyed menu or toolbar item does (US-044/QC-06) instead of being the one dead control in the window that
    /// says nothing. The command stays panel-local — its gate is a fact about the result THIS panel is showing,
    /// which is why the registry's ShellContext is the wrong home for it — but what it looks like when refused is
    /// not a place to invent a second vocabulary.</para>
    ///
    /// <para>The gate is a CORRECTNESS gate rather than a UX one: the two states it excludes are exactly the two
    /// in which the file's header would contradict its body.</para>
    /// <list type="bullet">
    /// <item><description><see cref="ProblemsState.Validating"/> — nothing is bound. A file naming the current
    /// project and holding no findings would read as a clean bill of health.</description></item>
    /// <item><description><see cref="ProblemsState.Stale"/> — the findings describe a SUPERSEDED tree while the
    /// file's source and save stamp would name the current one, and it would say so nowhere.</description></item>
    /// </list>
    /// <para>
    /// <see cref="ProblemsState.Clean"/> is deliberately included: a file saying "this save, these tiers, nothing
    /// found" is a legitimate record, and it is the statement the panel is already making. So is a panel with
    /// every tier switched off — that writes an empty list which SAYS it included no tiers, which is a different
    /// file from the clean one and must stay reachable.
    /// </para>
    /// </summary>
    public Availability ExportAvailability => State switch
    {
        ProblemsState.Clean or ProblemsState.Findings => Availability.Allow,
        ProblemsState.Stale => Availability.Disabled(ExportWhileStaleReason),
        _ => Availability.Disabled(ExportWhileValidatingReason),
    };

    /// <inheritdoc cref="ExportAvailability"/>
    /// <remarks>Read off <see cref="ExportAvailability"/> rather than off <see cref="State"/> a second time, so
    /// the command's predicate and the button's grey cannot come to disagree about the same states.</remarks>
    public bool CanExport => ExportAvailability.Enabled;

    /// <summary>
    /// The ONE text the export control announces and shows on hover: what the file is while the export is
    /// available, and why it is not while it is not.
    /// </summary>
    /// <remarks>
    /// One property rather than a static help text plus a conditional reason, because the two are alternatives
    /// and a control has one tooltip. It reaches a screen reader through <c>HelpText</c>, which IS announced on a
    /// disabled control, and a sighted user through a tooltip on the button's WRAPPER — a disabled control raises
    /// no pointer-enter of its own but does pass it to the nearest enabled ancestor (measured;
    /// <c>DisabledTooltipSpikeTests</c>).
    /// </remarks>
    public string ExportHint => ExportAvailability.Reason ?? ExportHelpText;

    /// <summary>
    /// The export gesture, as a command the view binds.
    /// <para>
    /// Constructed rather than declared with <c>[RelayCommand(CanExecute = …)]</c>, and that is an architecture
    /// rule rather than a style choice: the toolkit attribute's predicate competes with the command registry's
    /// Gate, which is the shell's single source of command enablement, so the GUI does not use it anywhere. The
    /// panel's tier toggles and sort headers are built the same way — this is a fourth sibling of theirs, not a
    /// registry row.
    /// </para>
    /// </summary>
    public IAsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// Hands the visible list to whoever knows where files go. This decides WHAT is exported and nothing else.
    /// <para>
    /// <see cref="Rows"/> is already filtered by the tier toggles and ordered by the chosen column, so one
    /// projection satisfies both fidelity requirements at once — the file holds every FINDING on screen, in the
    /// order it is on screen. The findings travel whole, so the file carries what the panel could not show.
    /// </para>
    /// <para>
    /// FINDINGS, not rows, and the narrowing is D05 rather than an oversight: the panel also lists faults in the
    /// tool, and a findings file is a statement about the PROJECT. A fault is not part of the project, so it has
    /// no place in a file forwarded to a support case as a description of one.
    /// </para>
    /// </summary>
    private Task Export() =>
        _export?.Invoke(new FindingsExportRequest(
            [.. Rows.OfType<ProblemRowViewModel>().Select(r => r.Finding)],
            $"host:{SortColumn.ToString().ToLowerInvariant()}{(SortAscending ? string.Empty : " desc")}",
            // Enum order, never click order: the attribute states a SET, and two users who hid the same tiers in
            // a different sequence must produce the same file. Tiers is built in enum order and IsShown does not
            // reorder it, so this is belt and braces — but the property is asserted rather than assumed.
            // The nulls drop out here, which is D05 enforced by the type rather than by remembering: a tier
            // with no severity contributes nothing to a file that speaks severities.
            [.. Tiers.Where(t => t.IsShown).Select(t => t.Severity)
                .OfType<ValidationSeverity>().Distinct().Order()],
            // Both Error tiers, stated separately, because the severity set above collapses them into one
            // value: showing only Fatale fejl and showing every error both record "Error".
            new ErrorTierFilter(Fatals.IsShown, Errors.IsShown)))
        ?? Task.CompletedTask;
}

// FindingsExportRequest — what this panel hands over — is declared beside its CONSUMER in
// Services/ProjectFindingsWorkflow.cs, as ValidationRequest is beside ValidationWorker.
