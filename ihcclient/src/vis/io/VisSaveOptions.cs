#nullable enable
namespace Ihc.Vis.Io
{
    /// <summary>
    /// Options controlling how a project is serialized. The default mimics IHC Visual (re-stamp
    /// <c>id2</c>/<c>modified</c> via the clock, optionally write a <c>.BAK</c> side-file);
    /// <see cref="PreserveExistingMetadata"/> writes the supplied metadata verbatim for byte-exact
    /// round-trip tests and import/export.
    /// </summary>
    public sealed record VisSaveOptions
    {
        /// <summary>When true, write timestamps/ids exactly as supplied instead of re-stamping.</summary>
        public bool PreserveMetadata { get; init; }

        /// <summary>When true (path saves only), rename any existing file to <c>.BAK</c> before writing.</summary>
        public bool CreateBackup { get; init; }

        /// <summary>The default, vendor-like save (re-stamp metadata, no backup).</summary>
        public static VisSaveOptions Default { get; } = new();

        /// <summary>A byte-preserving save that writes supplied metadata verbatim.</summary>
        public static VisSaveOptions PreserveExistingMetadata { get; } = new() { PreserveMetadata = true };
    }
}
