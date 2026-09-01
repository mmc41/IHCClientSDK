using System.Diagnostics.Metrics;

namespace Ihc
{
    /// <summary>
    /// The single home for the SDK layer's telemetry vocabulary: its instrumentation surface, its
    /// instruments, and the custom attribute names it emits.
    ///
    /// <para>A name written at its call site is invisible to everything except that call site: nothing can
    /// check it against the naming rules, nothing fails when a second site spells it differently, and a
    /// rename silently forks the backend's schema. Declaring each one here makes the set reviewable,
    /// greppable and testable as a unit - which is what the registry drift test does.</para>
    ///
    /// <para>Instruments are properties of THIS class rather than of a nested one, so touching the registry
    /// constructs every one of them. An instrument parked in a nested static nobody references is never
    /// created, and a metric that is never created is indistinguishable from one that is never recorded.</para>
    /// </summary>
    internal static class SdkTelemetryRegistry
    {
        /// <summary>
        /// The SDK's instrumentation scope. It ADOPTS <see cref="Telemetry.ActivitySource"/> rather than
        /// creating another source of the same name, so the shipped public source stays the one and only
        /// place SDK spans come from.
        /// </summary>
        internal static TelemetrySurface Surface { get; } =
            new(Telemetry.ActivitySource, new Meter(Telemetry.MeterName, VersionInfo.GetSdkVersionStr()));

        /// <summary>Duration of one controller SOAP operation, recorded where every such call passes.</summary>
        internal static Histogram<double> ControllerOperationDuration { get; } =
            Surface.Meter.CreateHistogram<double>(
                "ihc.controller.operation.duration", unit: "s",
                description: "Duration of a controller SOAP operation.");

        /// <summary>One count per applied edit, whatever its outcome.</summary>
        internal static Counter<long> EditApply { get; } =
            Surface.Meter.CreateCounter<long>(
                "ihc.edit.apply", unit: "{edit}",
                description: "Edits applied to a project document, by command and outcome.");

        /// <summary>Duration of one edit application.</summary>
        internal static Histogram<double> EditApplyDuration { get; } =
            Surface.Meter.CreateHistogram<double>(
                "ihc.edit.apply.duration", unit: "s",
                description: "Duration of applying one edit to a project document.");

        /// <summary>
        /// How long the built-in catalog took to materialize on first use.
        ///
        /// A HISTOGRAM rather than a once-only counter, because the Lazy guarding it is
        /// <c>PublicationOnly</c>: a rare concurrent double-run is legal and is exactly the event worth
        /// seeing. A counter asserting "one" would either be wrong or hide it.
        /// </summary>
        internal static Histogram<double> CatalogMaterializationDuration { get; } =
            Surface.Meter.CreateHistogram<double>(
                "ihc.catalog.materialization.duration", unit: "s",
                description: "Duration of materializing the built-in component catalog on first use.");

        /// <summary>Misses in the per-edit open-analysis cache; a full re-analysis rather than a reuse.</summary>
        internal static Counter<long> EditAnalysisMiss { get; } =
            Surface.Meter.CreateCounter<long>(
                "ihc.edit.analysis.miss", unit: "{analysis}",
                description: "Full open-analysis runs, i.e. misses of the per-edit analysis cache.");

        /// <summary>
        /// Custom attribute names this layer emits. Values live in <see cref="Values"/>; separating the two
        /// is what lets the drift test check names against the naming rules without tripping over vocabulary.
        /// </summary>
        internal static class Attributes
        {
            /// <summary>Wire size in bytes of the serialized SOAP request envelope.</summary>
            public const string SoapRequestBodySize = "ihc.soap.request.body.size";

            /// <summary>Wire size in bytes of the SOAP response envelope as received.</summary>
            public const string SoapResponseBodySize = "ihc.soap.response.body.size";

            /// <summary>
            /// How an instrumented operation ended, on both the span and its instruments' dimensions - the
            /// two must agree or a metric point cannot be traced back to the spans it came from.
            /// </summary>
            public const string OperationStatus = "ihc.edit.status";

            /// <summary>Which command was applied, as a metric dimension and a span attribute.</summary>
            public const string EditCommand = "ihc.edit.command";

            /// <summary>Elements added by a committed edit.</summary>
            public const string EditAddedCount = "ihc.edit.added_count";

            /// <summary>Elements removed by a committed edit.</summary>
            public const string EditRemovedCount = "ihc.edit.removed_count";

            /// <summary>Elements changed in place by a committed edit.</summary>
            public const string EditChangedCount = "ihc.edit.changed_count";

            /// <summary>Whether the save re-parsed its own bytes before declaring success.</summary>
            public const string SaveVerifyRoundTrip = "ihc.save.verify_round_trip";

            /// <summary>Whether the save ran the pre-serialize validation checklist.</summary>
            public const string SaveValidateBeforeSave = "ihc.save.validate_before_save";

            /// <summary>Whether the save kept the replaced file as a .BAK side-file.</summary>
            public const string SaveCreateBackup = "ihc.save.create_backup";

            /// <summary>Whether the save wrote timestamps and ids verbatim instead of re-stamping.</summary>
            public const string SaveWriteMetadataVerbatim = "ihc.save.write_metadata_verbatim";

            /// <summary>Size in bytes of the project content written or read.</summary>
            public const string ProjectFileSize = "ihc.project.file_size";

            /// <summary>
            /// SHA-256 of the exact bytes written or read. Opt-in only: a stable fingerprint links sessions
            /// that touched the same customer project.
            /// </summary>
            public const string ProjectContentDigest = "ihc.project.content.sha256";

            /// <summary>Which report was generated.</summary>
            public const string ReportKind = "ihc.report.kind";

            /// <summary>Which report mode (full or standard) was rendered.</summary>
            public const string ReportMode = "ihc.report.mode";

            /// <summary>The report's output media type.</summary>
            public const string ReportMime = "ihc.report.mime";

            /// <summary>Size in bytes of the rendered report.</summary>
            public const string ReportBytes = "ihc.report.bytes";

            /// <summary>How many rules a validation run actually executed.</summary>
            public const string ValidationRulesRun = "ihc.validation.rules_run";

            /// <summary>How many findings a validation run emitted.</summary>
            public const string ValidationFindingsEmitted = "ihc.validation.findings_emitted";

            /// <summary>Which rule a per-rule timing span is about.</summary>
            public const string ValidationRuleCode = "ihc.validation.rule.code";

            /// <summary>How many id-bearing elements the project holds.</summary>
            public const string ProjectElementCount = "ihc.project.element_count";

            /// <summary>Elements the change set reports as added.</summary>
            public const string DiffAddedCount = "ihc.diff.added_count";

            /// <summary>Elements the change set reports as removed.</summary>
            public const string DiffRemovedCount = "ihc.diff.removed_count";

            /// <summary>Elements the change set reports as changed in place.</summary>
            public const string DiffChangedCount = "ihc.diff.changed_count";

            /// <summary>Elements whose child list the change set reports as reordered.</summary>
            public const string DiffChildListChangedCount = "ihc.diff.child_list_changed_count";

            /// <summary>Elements open-normalization added. Equal to Removed on an authentic file (a re-hoist).</summary>
            public const string NormalizeAddedCount = "ihc.normalize.added_count";

            /// <summary>Elements open-normalization removed. Equal to Added on an authentic file.</summary>
            public const string NormalizeRemovedCount = "ihc.normalize.removed_count";

            /// <summary>
            /// Elements open-normalization changed IN PLACE. Zero on an authentic vendor file: normalization
            /// re-hoists definitions rather than editing them, so an in-place change here is anomalous.
            /// </summary>
            public const string NormalizeChangedCount = "ihc.normalize.changed_count";

            /// <summary>The id allocator high-water mark; a DECREASE between saves is corruption.</summary>
            public const string ProjectLastUniqueId = "ihc.project.last_unique_id";

            /// <summary>The catalogue code behind a refusal or a coded failure.</summary>
            public const string ProblemCode = "ihc.problem.code";

            /// <summary>Which controller service a SOAP operation belongs to.</summary>
            public const string Service = "ihc.service";

            /// <summary>Which operation (SOAP action) was invoked.</summary>
            public const string Operation = "ihc.operation";

            /// <summary>
            /// The failure's normalized identity. A published semantic convention rather than an IHC name,
            /// held here so the span attribute and the metric dimension cannot be spelled differently.
            /// </summary>
            public const string ErrorType = "error.type";
        }

        /// <summary>The closed vocabularies the attributes above take. Values, never names.</summary>
        internal static class Values
        {
            /// <summary>The operation did what it was asked to do.</summary>
            public const string StatusOk = "ok";

            /// <summary>The operation declined by design, with a coded reason. Not an error.</summary>
            public const string StatusRefused = "refused";

            /// <summary>The operation could not complete. The only status that marks a span Error.</summary>
            public const string StatusFailed = "failed";
        }
    }
}
