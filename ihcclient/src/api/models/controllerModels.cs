using System;
using System.Globalization;
using System.Linq;

namespace Ihc
{
    /// <summary>
    /// Capacity and free space of the controller's SD card, which it uses for logging and the project backup.
    /// </summary>
    public record SDInfo
    {
        /// <summary>
        /// Total size of the SD card in bytes, probably that of its mounted file system: a nominal 512 MB card reports
        /// about 480 MB.
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// Free space available on the SD card in bytes.
        /// </summary>
        public long Free { get; init; }

        public override string ToString()
        {
            return $"SDInfo(Size={Size}, Free={Free})";
        }
    }

    /// <summary>
    /// The controller's run state. The controller reports it as a key such as <c>text.ctrl.state.ready</c>, which
    /// this SDK maps to a member.
    /// </summary>
    public enum ControllerState
    {
        /// <summary>Controller is uninitialized (key <c>text.ctrl.state.uninitialized</c>).</summary>
        Uninitialized,

        /// <summary>Running normally (key <c>text.ctrl.state.ready</c>).</summary>
        Ready,

        /// <summary>
        /// Controller is initializing (key <c>text.ctrl.state.initialize</c>): reported throughout project change mode,
        /// from <see cref="IControllerService.EnterProjectChangeMode"/> until
        /// <see cref="IControllerService.ExitProjectChangeMode"/> completes.
        /// </summary>
        Initialize,

        /// <summary>Controller has failed and is in an error state (key <c>text.ctrl.state.failed</c>).</summary>
        Failed,

        /// <summary>
        /// Controller is in wireless RF configuration mode (key <c>text.ctrl.state.rfconfiguration</c>).
        /// </summary>
        RfConfiguration,

        /// <summary>Controller is in simulation mode (key <c>text.ctrl.state.simulation</c>).</summary>
        Simulation,

        /// <summary>
        /// The controller's state could not be read: it named a state this SDK does not know, or it named none at
        /// all. Deliberately not the enum's zero member, so an unread state is distinguishable from an unassigned
        /// variable and from <see cref="Uninitialized"/>, a defined controller state.
        /// </summary>
        Unknown
    };

    /// <summary>
    /// A backup file as <see cref="IControllerService.GetBackup"/> returns it: a name and raw bytes.
    /// </summary>
    public record BackupFile : BinaryFile
    {
        /// <summary>
        /// Raw bytes of the backup file, in a format that is not known; probably neither compressed nor encoded text.
        /// </summary>
        public byte[] Data { get; init; }

        /// <summary>
        /// Name of the backup file.
        /// </summary>
        public string? Filename { get; init; }

        public BackupFile(string? Filename, byte[] Data)
        {
            this.Data = Data;
            this.Filename = Filename;
        }
        
        public BackupFile(BinaryFile input)
        {
            ArgumentNullException.ThrowIfNull(input);
            this.Data = input.Data;
            this.Filename = input.Filename;
        }

        public override string ToString()
        {
            string dataAsHex = Data.Length == 0 ? "[]" :
                    string.Join(" ", Data.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

            return $"BackupFile(Filename={Filename}, Data={dataAsHex})";
        }
    }

    /// <summary>
    /// Metadata of the project stored on the controller, read from the stored file itself: its identity, format
    /// version, project details and last-save stamp.
    /// </summary>
    public record ProjectInfo
    {
        /// <summary>
        /// Minor part of the stored file's format version (the project root's <c>version_minor</c>).
        /// </summary>
        public int VisualMinorVersion { get; init; }

        /// <summary>
        /// Major part of the stored file's format version (the project root's <c>version_major</c>).
        /// </summary>
        public int VisualMajorVersion { get; init; }

        /// <summary>
        /// First half of the stored project's identity, not a revision counter: the file's <c>id1</c>, a packed
        /// timestamp of its creation, so it tells another project from a version of the same one; pass it to
        /// <see cref="IControllerService.GetIHCProjectSegment"/>.
        /// </summary>
        public int ProjectMajorRevision { get; init; }

        /// <summary>
        /// Second half of the stored project's identity, not a revision counter: the file's <c>id2</c>, a packed
        /// timestamp of the save that wrote it; pass it to <see cref="IControllerService.GetIHCProjectSegment"/>.
        /// </summary>
        public int ProjectMinorRevision { get; init; }

        /// <summary>
        /// When the stored file was last saved: its own <c>modified</c> stamp in the saving PC's local time, seconds
        /// always 0. The SDK reads it at a fixed +01:00 offset, so the instant is right only for a time stamped at
        /// UTC+1; <c>DateTimeOffset.MinValue</c> when the reply carries no readable date.
        /// </summary>
        public DateTimeOffset Lastmodified { get; init; }

        /// <summary>
        /// The project's number (Danish UI: "Projekt nummer").
        /// </summary>
        public string? ProjectNumber { get; init; }

        /// <summary>
        /// Customer name from the stored project's customer information.
        /// </summary>
        public string? CustomerName { get; init; }

        /// <summary>
        /// Installer name from the stored project's installer information.
        /// </summary>
        public string? InstallerName { get; init; }

        public override string ToString()
        {
            return $"ProjectInfo(VisualMinorVersion={VisualMinorVersion}, VisualMajorVersion={VisualMajorVersion}, ProjectMajorRevision={ProjectMajorRevision}, ProjectMinorRevision={ProjectMinorRevision}, Lastmodified={Lastmodified}, ProjectNumber={ProjectNumber}, CustomerName={CustomerName}, InstallerName={InstallerName})";
        }
    }

    /// <summary>
    /// An IHC project file, its <c>.vis</c> XML held as text. On the wire it travels gzip-compressed and
    /// base64-encoded; the SDK compresses and decompresses it.
    /// </summary>
    public record ProjectFile : TextFile
    {
        /// <summary>
        /// XML content of the project file, carried as ISO-8859-1 bytes inside the wire's gzip stream.
        /// </summary>
        public string Data { get; init; }

        /// <summary>
        /// Name of the project file (*.vis), or <c>null</c> when the controller supplies none, which a download may
        /// do. A controller names each project segment it serves <c>unknown.bin</c>, not the name the project was
        /// uploaded with.
        /// </summary>
        public string? Filename { get; init; }

        /// <summary>
        /// Encoding used for the project file. Always ISO-8859-1 (Latin-1) as shown in top of xml project.
        /// </summary>
        internal const string EncodingName = "ISO-8859-1";

        /// <summary>
        /// Text encoding for the project file (ISO-8859-1/Latin-1).
        /// </summary>
        public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding(EncodingName);

        /// <summary>
        /// Strict ISO-8859-1: throws on any text outside the Latin-1 repertoire instead of substituting '?'. The
        /// single source of truth for the byte-exact <c>.vis</c>/<c>.ihc</c> wire — used by the serializer and the
        /// controller upload path so an out-of-repertoire character fails loudly rather than silently corrupting the file.
        /// </summary>
        public static System.Text.Encoding StrictEncoding { get; } = System.Text.Encoding.GetEncoding(
            EncodingName, System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);

        /// <summary>
        /// Canonical file extension (without leading dot) for an IHC project file.
        /// </summary>
        public static string FileExtension { get; } = "vis";

        public ProjectFile(string? Filename, string Data)
        {
            this.Data = Data;
            this.Filename = Filename;
        }

        public ProjectFile(TextFile input)
        {
            ArgumentNullException.ThrowIfNull(input);
            this.Data = input.Data;
            this.Filename = input.Filename;
        }

        public override string ToString()
        {
            return $"ProjectFile(Filename={Filename}, Data={Data})";
        }
    }

    /// <summary>
    /// Segment of a project (a raw binary chunk of a segmented project transfer).
    /// </summary>
    public record ProjectSegment : BinaryFile
    {
      /// <summary>
      /// Raw bytes of the segment, a slice of the controller's gzip stream of the project; it decompresses only when
      /// joined with the other segments in index order.
      /// </summary>
      public byte[] Data { get; init; } = Array.Empty<byte>();

      /// <summary>
      /// Name of the segment file; <c>null</c> from a segment download, since the SDK does not map the name a
      /// controller gives a segment (<c>unknown.bin</c> on firmware 3.3.44).
      /// </summary>
      public string? Filename { get; init; }

      public ProjectSegment() { }

      /// <summary>
      /// Copy-constructor required by the BinaryFile contract.
      /// </summary>
      public ProjectSegment(BinaryFile input)
      {
        ArgumentNullException.ThrowIfNull(input);
        this.Data = input.Data;
        this.Filename = input.Filename;
      }

      public override string ToString()
      {
        return $"ProjectSegment(Data=byte[{Data.Length}])";
      }
    }
}