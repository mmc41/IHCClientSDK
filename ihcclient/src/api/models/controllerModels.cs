using System;
using System.Linq;

namespace Ihc
{
    /// <summary>
    /// High level model of SD card information without soap distractions.
    /// </summary>
    public record SDInfo
    {
        /// <summary>
        /// Total size of the SD card in bytes.
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
    /// Enumeration of possible IHC controller states.
    /// </summary>
    public enum ControllerState
    {
        /// <summary>Controller is uninitialized.</summary>
        Uninitialized,

        /// <summary>Controller is ready aka OK.</summary>
        Ready,

        /// <summary>Controller is initializing.</summary>
        Initialize,

        /// <summary>Controller has failed and is in an error state.</summary>
        Failed,

        /// <summary>Controller is in wireless RF configuration mode.</summary>
        RfConfiguration,

        /// <summary>Controller is in simulation mode.</summary>
        Simulation,

        /// <summary>Unknown state. Should not be used (unless controller has a hidden state we don't know).</summary>
        Unknown
    };

    /// <summary>
    /// High level model of a controller backup file without soap distractions.
    /// </summary>
    public record BackupFile : BinaryFile
    {
        /// <summary>
        /// Raw binary data of the backup file.
        /// Note: Can't identify the file format. Does not seem to be compressed or encoded text. Raw Binary?
        /// </summary>
        public byte[] Data { get; init; }

        /// <summary>
        /// Name of the backup file.
        /// </summary>
        public string Filename { get; init; }

        public BackupFile(string Filename, byte[] Data)
        {
            this.Data = Data;
            this.Filename = Filename;
        }
        
        public BackupFile(BinaryFile input)
        {
            this.Data = input.Data;
            this.Filename = input.Filename;
        }

        public override string ToString()
        {
            string dataAsHex = Data == null ? "null" :
                    Data.Length == 0 ? "[]" :
                    string.Join(" ", Data.Select(b => b.ToString("x2")));

            return $"BackupFile(Filename={Filename}, Data={dataAsHex})";
        }
    }

    /// <summary>
    /// High level model of IHC project information without soap distractions.
    /// </summary>
    public record ProjectInfo
    {
        /// <summary>
        /// Minor version of IHC Visual used to create the project.
        /// </summary>
        public int VisualMinorVersion { get; init; }

        /// <summary>
        /// Major version of IHC Visual used to create the project.
        /// </summary>
        public int VisualMajorVersion { get; init; }

        /// <summary>
        /// Major revision number of the project.
        /// </summary>
        public int ProjectMajorRevision { get; init; }

        /// <summary>
        /// Minor revision number of the project.
        /// </summary>
        public int ProjectMinorRevision { get; init; }

        /// <summary>
        /// Date and time when the project was last modified.
        /// </summary>
        public DateTimeOffset Lastmodified { get; init; }

        /// <summary>
        /// Project identification number.
        /// </summary>
        public string ProjectNumber { get; init; }

        /// <summary>
        /// Name of the customer for this project.
        /// </summary>
        public string CustomerName { get; init; }

        /// <summary>
        /// Name of the installer who created/modified the project.
        /// </summary>
        public string InstallerName { get; init; }

        public override string ToString()
        {
            return $"ProjectInfo(VisualMinorVersion={VisualMinorVersion}, VisualMajorVersion={VisualMajorVersion}, ProjectMajorRevision={ProjectMajorRevision}, ProjectMinorRevision={ProjectMinorRevision}, Lastmodified={Lastmodified}, ProjectNumber={ProjectNumber}, CustomerName={CustomerName}, InstallerName={InstallerName})";
        }
    }

    /// <summary>
    /// High level model of an IHC project file (XML format) without soap distractions.
    /// </summary>
    public record ProjectFile : TextFile
    {
        /// <summary>
        /// XML content of the project file.
        /// </summary>
        public string Data { get; init; }

        /// <summary>
        /// Name of the project file.
        /// </summary>
        public string Filename { get; init; }

        /// <summary>
        /// Encoding used for the project file. Always ISO-8859-1 (Latin-1) as shown in top of xml project.
        /// </summary>
        internal const string EncodingName = "ISO-8859-1";

        /// <summary>
        /// Text encoding for the project file (ISO-8859-1/Latin-1).
        /// </summary>
        public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding(EncodingName);

        public ProjectFile(string Filename, string Data)
        {
            this.Data = Data;
            this.Filename = Filename;
        }

        public ProjectFile(TextFile input)
        {
            this.Data = input.Data;
            this.Filename = input.Filename;
        }

        public override string ToString()
        {
            return $"ProjectFile(Filename={Filename}, Data={Data})";
        }
    }

    /// <summary>
    /// Segment of a project
    /// </summary>
    public record ProjectSegment
    {
      public byte[] Data { get; init; }

      public override string ToString()
      {
        return $"ProjectSegment(Data=byte[{Data?.Length ?? 0}])";
      }
    }
}