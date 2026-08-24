#nullable enable
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
        string CauseLabel);

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

        /// <summary>The Danish sentence for a refused save. Identified, never rendered beside its cause.</summary>
        public const string SaveLabel = "Projektet kunne ikke gemmes";

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
    }
}
