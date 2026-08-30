using System.Diagnostics.Metrics;
using Ihc;
using Ihc.Bootstrap;

namespace ihc_openvisual.Configuration;

/// <summary>
/// The single home for the host's telemetry vocabulary: its instrumentation surface, its instruments, and
/// the custom attribute names it emits. The SDK has its own registry (<see cref="Ihc"/>); the two are
/// separate because they are separate instrumentation scopes, and a host must never mint a name in the
/// SDK's family.
///
/// <para>Instruments are properties of THIS class rather than of a nested one, so touching the registry
/// constructs every one of them. An instrument parked in a nested static nobody references is never
/// created, and a metric that is never created is indistinguishable from one that is never recorded.</para>
/// </summary>
internal static class AppTelemetryRegistry
{
    /// <summary>
    /// The host's instrumentation scope. It ADOPTS <see cref="Telemetry.ActivitySource"/> rather than
    /// creating a second source under the same name, and pairs it with the meter the composition root
    /// registers by that same name.
    /// </summary>
    internal static TelemetrySurface Surface { get; } =
        new(Telemetry.ActivitySource,
            new Meter(Telemetry.ActivitySourceName, TelemetryBootstrap.GetAppVersionStr()));

    /// <summary>Duration of loading a project, from the shell's point of view.</summary>
    internal static Histogram<double> ProjectLoadDuration { get; } =
        Surface.Meter.CreateHistogram<double>(
            "ihc.project.load.duration", unit: "s",
            description: "Duration of loading a project into the editor.");

    /// <summary>Duration of saving a project, from the shell's point of view.</summary>
    internal static Histogram<double> ProjectSaveDuration { get; } =
        Surface.Meter.CreateHistogram<double>(
            "ihc.project.save.duration", unit: "s",
            description: "Duration of saving the edited project.");

    /// <summary>Duration of one whole-project validation run.</summary>
    internal static Histogram<double> ValidationRunDuration { get; } =
        Surface.Meter.CreateHistogram<double>(
            "ihc.validation.run.duration", unit: "s",
            description: "Duration of one debounced whole-project validation run.");

    /// <summary>
    /// Invocations of a REGISTERED shell command row - menu bar, toolbar, flyout and gesture alike.
    /// Deliberately NOT feature-usage data: the registry sees only rows it registered, carries no surface
    /// dimension because the shared local function cannot observe which surface invoked it, and no error
    /// dimension because it cannot observe a failure either.
    /// </summary>
    internal static Counter<long> CommandInvocation { get; } =
        Surface.Meter.CreateCounter<long>(
            "ihc.command.invocation", unit: "{invocation}",
            description: "Invocations of a registered shell command row, keyed by command id.");

    /// <summary>Duration of a tree refresh, whether it reconciled or rebuilt.</summary>
    internal static Histogram<double> TreeUpdateDuration { get; } =
        Surface.Meter.CreateHistogram<double>(
            "ihc.ui.tree_update.duration", unit: "s",
            description: "Duration of a tree refresh, dimensioned by reconcile or rebuild.");

    /// <summary>Duration of the command registry's context sweep.</summary>
    internal static Histogram<double> ContextRebuildDuration { get; } =
        Surface.Meter.CreateHistogram<double>(
            "ihc.ui.context_rebuild.duration", unit: "s",
            description: "Duration of the command registry's can-execute sweep on a context change.");

    /// <summary>Problems actually shown to a user, keyed by code and family.</summary>
    internal static Counter<long> ProblemRaised { get; } =
        Surface.Meter.CreateCounter<long>(
            "ihc.problem.raised", unit: "{problem}",
            description: "Problems presented to the user through the dialog service.");

    /// <summary>
    /// Custom attribute names this layer emits. Values live in <see cref="Values"/>; separating the two is
    /// what lets the drift test check names against the naming rules without tripping over vocabulary.
    /// </summary>
    internal static class Attributes
    {
        /// <summary>Where the opened project came from.</summary>
        public const string ProjectSource = "ihc.project.source";

        /// <summary>The project file path.</summary>
        public const string ProjectPath = "ihc.project.path";

        /// <summary>Size in bytes of the project file read or written.</summary>
        public const string ProjectFileSize = "ihc.project.file_size";

        /// <summary>The registered command row's id.</summary>
        public const string CommandId = "ihc.command.id";

        /// <summary>How a validation run ended: bound, superseded, abandoned or faulted.</summary>
        public const string ValidationOutcome = "ihc.validation.outcome";

        /// <summary>How many findings a validation result carried.</summary>
        public const string ValidationFindingCount = "ihc.validation.finding_count";

        /// <summary>Whether a tree refresh reconciled the existing nodes or rebuilt them.</summary>
        public const string TreeUpdate = "ihc.tree.update";

        /// <summary>Which of the four document transitions this change was.</summary>
        public const string DocumentBranch = "ihc.document.branch";

        /// <summary>Which document generation the validation monitor is on.</summary>
        public const string DocumentGeneration = "ihc.document.generation";

        /// <summary>The problem's catalogue code.</summary>
        public const string ProblemCode = "ihc.problem.code";

        /// <summary>The problem's family, i.e. the first dotted segment of its code.</summary>
        public const string ProblemFamily = "ihc.problem.family";
    }

    /// <summary>The closed vocabularies the attributes above take. Values, never names.</summary>
    internal static class Values
    {
        /// <summary>A refresh reused the existing tree nodes.</summary>
        public const string TreeReconcile = "reconcile";

        /// <summary>A refresh discarded and rebuilt the tree.</summary>
        public const string TreeRebuild = "rebuild";

        /// <summary>The run produced a result and it was bound to the panel.</summary>
        public const string ValidationBound = "bound";

        /// <summary>A newer run started before this one finished.</summary>
        public const string ValidationSuperseded = "superseded";

        /// <summary>The run was abandoned before producing a result.</summary>
        public const string ValidationAbandoned = "abandoned";

        /// <summary>The run threw.</summary>
        public const string ValidationFaulted = "faulted";

        /// <summary>The first document this monitor ever saw.</summary>
        public const string BranchFirst = "first";

        /// <summary>A commit, undo or redo: same document, new version.</summary>
        public const string BranchEdit = "edit";

        /// <summary>A save: the event fired but nothing about the document moved.</summary>
        public const string BranchSave = "save";

        /// <summary>A different document entirely; the generation increments.</summary>
        public const string BranchReplacement = "replacement";
    }
}
