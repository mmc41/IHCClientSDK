#nullable enable
namespace Ihc.Projects
{
    /// <summary>
    /// Options controlling how a project is serialized. The default mimics IHC Visual (re-stamp
    /// <c>id2</c>/<c>modified</c> via the clock, optionally write a <c>.BAK</c> side-file);
    /// <see cref="PreserveExistingMetadata"/> writes the supplied metadata verbatim for byte-exact
    /// round-trip tests and import/export.
    /// </summary>
    public sealed record ProjectSaveOptions
    {
        /// <summary>When true, write timestamps/ids exactly as supplied instead of re-stamping.</summary>
        public bool WriteMetadataVerbatim { get; init; }

        /// <summary>When true (path saves only), rename any existing file to <c>.BAK</c> before writing.</summary>
        public bool CreateBackup { get; init; }

        /// <summary>
        /// When true, run the pre-serialize validation checklist before writing and throw
        /// <see cref="ProjectValidationException"/> on errors (warnings never block). Off by default so any
        /// loadable open-world file keeps re-saving verbatim; controller uploads validate independently
        /// (<see cref="ProjectAppService.UploadTo"/>).
        /// </summary>
        public bool ValidateBeforeSave { get; init; }

        /// <summary>
        /// When true, re-parse the just-serialized bytes and compare them semantically with the written model,
        /// throwing (with the first divergence named) when the bytes do not reproduce it — the self-revealing
        /// postcondition that catches model state the format cannot represent (e.g. an attribute value equal to
        /// its DTD default, which omit-if-default drops). Off by default for file saves;
        /// <see cref="ProjectAppService.UploadTo"/> always verifies (a controller flash has no <c>.BAK</c>).
        /// </summary>
        public bool VerifyRoundTrip { get; init; }

        /// <summary>The default, vendor-like save (re-stamp metadata, no backup).</summary>
        public static ProjectSaveOptions Default { get; } = new();

        /// <summary>A byte-preserving save that writes supplied metadata verbatim.</summary>
        public static ProjectSaveOptions PreserveExistingMetadata { get; } = new() { WriteMetadataVerbatim = true };

        public override string ToString() =>
            $"ProjectSaveOptions(WriteMetadataVerbatim={WriteMetadataVerbatim}, CreateBackup={CreateBackup}, " +
            $"ValidateBeforeSave={ValidateBeforeSave})";
    }
}
