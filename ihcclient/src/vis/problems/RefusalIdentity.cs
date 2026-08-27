#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// The four facts a refusing site needs to state its identity: WHICH operation is being refused, and WHAT
    /// caused it, each with the Danish words a user reads.
    /// <para>
    /// It exists because a refusing site is often a SHARED helper. The undeclared-attribute guard runs at save,
    /// at edit-session open and at edit commit; the operation is therefore the CALLER's fact, not the guard's,
    /// and a guard that hard-coded one would name the wrong operation everywhere else it is used. Passing the
    /// whole identity — rather than the operation alone — also keeps the cause's Danish sentence at the site
    /// that raises it, which is what the layer rules require: a guard below the validation engine cannot read
    /// the catalogue to look one up.
    /// </para>
    /// <para>
    /// The composition it builds is <see cref="ProblemChain"/>'s: the operation carries the dotted family code
    /// and the cause keeps the bare published catalogue id.
    /// </para>
    /// </summary>
    /// <param name="Operation">The operation being refused, by its dotted code (<c>io.save</c>).</param>
    /// <param name="OperationLabel">The Danish sentence for the operation. Identified, not rendered.</param>
    /// <param name="Cause">The condition that caused it, by its bare published catalogue id.</param>
    /// <param name="CauseLabel">The Danish sentence the user actually reads.</param>
    public readonly record struct RefusalIdentity(
        ProblemCode Operation,
        string OperationLabel,
        ProblemCode Cause,
        string CauseLabel)
    {
        /// <summary>
        /// The same identity with its <see cref="CauseLabel"/> bound from <paramref name="arguments"/> — for the
        /// rows whose Danish sentence declares argument slots.
        /// <para>
        /// The REGISTRY member keeps the template, which is what the drift gate compares against the catalogue's
        /// entry; the raising site, which is the only place that knows the values, binds a copy. That is how a
        /// two-faced row — one that reports at validate and refuses at save — says the same sentence with the same
        /// data on both faces, without the refusing site reading the catalogue it may not depend on.
        /// </para>
        /// </summary>
        /// <param name="arguments">The values for the slots the label declares.</param>
        public RefusalIdentity Binding(params ProblemArgument[] arguments) =>
            this with { CauseLabel = ProblemTemplate.Bind(CauseLabel, arguments) };
    }

    /// <summary>
    /// The operation heads every refusing layer shares.
    /// <para>
    /// They live here, in the contract namespace, rather than beside any one family of causes, because an
    /// operation is refused from SEVERAL layers at once: a save is abandoned by the serializer, by a schema
    /// guard and by the atomic writer, which sit in three different namespaces with no dependency between
    /// them. A cause code belongs where it is raised; an operation head belongs where every raiser can see it.
    /// </para>
    /// </summary>
    public static class OperationCodes
    {
        /// <summary>Opening a project.</summary>
        public static ProblemCode Load { get; } = new("io.load");

        /// <summary>The Danish sentence for a refused open. Identified, never rendered beside its cause.</summary>
        public const string LoadLabel = "Projektet kunne ikke åbnes";

        /// <summary>Writing a project — a save or an export.</summary>
        public static ProblemCode Save { get; } = new("io.save");

        /// <summary>
        /// The Danish sentence for a refused save, and the <c>io.save</c> row's template verbatim.
        /// <para>
        /// It declares a <c>{count}</c> slot because the ONE place this sentence is ever RENDERED is the head of
        /// the save-validation aggregate, where the number of blocking errors is what the reader needs. In a
        /// cause/detail chain the operation is identified and never rendered beside its cause, so the unbound
        /// form is not a sentence anybody reads — which is why one row can serve both without the chain needing
        /// a number it does not have.
        /// </para>
        /// </summary>
        public const string SaveLabel = "Projektet kunne ikke gemmes: {count} fejl skal rettes først.";

        /// <summary>
        /// Opening a project for EDITING — the read-to-write boundary, where the guards a save would fail on run
        /// once, before a user invests any work.
        /// <para>
        /// It is an operation head like the others because the same causes refuse it: an undeclared attribute
        /// stops an edit session opening for exactly the reason it stops a save, and the two must answer with the
        /// same published cause id under different operations. Before this head existed the edit-open guard had
        /// no operation to name, so it refused WITHOUT an identity and the session reported a generic failure.
        /// </para>
        /// </summary>
        public static ProblemCode EditOpen { get; } = new("edit.open");

        /// <summary>The Danish sentence for a refused edit-open. Identified, never rendered beside its cause.</summary>
        public const string EditOpenLabel = "Projektet kunne ikke åbnes til redigering";

        /// <summary>Taking a catalog definition file in — a runtime import or the install-directory scan.</summary>
        public static ProblemCode ImportCatalog { get; } = new("import.catalog");

        /// <summary>The Danish sentence for a refused catalog read. Identified, never rendered beside its cause.</summary>
        public const string ImportCatalogLabel = "Katalogfilen kunne ikke indlæses";

        /// <summary>Fetching the stored project from the controller.</summary>
        public static ProblemCode BridgeDownload { get; } = new("bridge.download");

        /// <summary>The Danish sentence for a refused download. Identified, never rendered beside its cause.</summary>
        public const string BridgeDownloadLabel = "Projektet kunne ikke hentes fra controlleren";

        /// <summary>Storing a project on the controller.</summary>
        public static ProblemCode BridgeUpload { get; } = new("bridge.upload");

        /// <summary>The Danish sentence for a refused upload. Identified, never rendered beside its cause.</summary>
        public const string BridgeUploadLabel = "Projektet kunne ikke sendes til controlleren";

        /// <summary>
        /// Every operation head, so the set can be READ rather than re-listed. The catalogue's invariant on a
        /// declared refusal needs it, and re-spelling six codes there would be a second copy of this vocabulary
        /// with nothing keeping the two equal — which is the failure this whole class exists to prevent.
        /// </summary>
        public static EquatableArray<ProblemCode> All { get; } =
            ImmutableArray.Create(Load, Save, EditOpen, ImportCatalog, BridgeDownload, BridgeUpload);
    }
}
