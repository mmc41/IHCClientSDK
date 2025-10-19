using System;
using System.Linq;

namespace Ihc
{
    public record SDInfo
    {
        public long Size { get; init; }

        public long Free { get; init; }

        public override string ToString()
        {
            return $"SDInfo(Size={Size}, Free={Free})";
        }
    }

    public enum ControllerState
    {
        Uninitialized,
        Ready,
        Initialize,
        Failed,
        RfConfiguration,
        Simulation,
        Unknown // Should not be used (unless controller has a hidden state we don't know)
    };

    public record BackupFile
    {
        // Hmm. Can't identify the file format.Does not seem to be compressed or encoded text. Raw Binary?

        [File(DefaultFileName: "backup.bin")]
        public byte[] Data { get; init; }

        public string Filename { get; init; }

        public override string ToString()
        {
            string dataAsHex = Data == null ? "null" :
                    Data.Length == 0 ? "[]" :
                    string.Join(" ", Data.Select(b => b.ToString("x2")));

            return $"BackupFile(Filename={Filename}, Data={dataAsHex})";
        }
    }

    public record ProjectInfo
    {
        public int VisualMinorVersion { get; init; }

        public int VisualMajorVersion { get; init; }

        public int ProjectMajorRevision { get; init; }

        public int ProjectMinorRevision { get; init; }

        public DateTimeOffset Lastmodified { get; init; }

        public string ProjectNumber { get; init; }

        public string CustomerName { get; init; }

        public string InstallerName { get; init; }

        public override string ToString()
        {
            return $"ProjectInfo(VisualMinorVersion={VisualMinorVersion}, VisualMajorVersion={VisualMajorVersion}, ProjectMajorRevision={ProjectMajorRevision}, ProjectMinorRevision={ProjectMinorRevision}, Lastmodified={Lastmodified}, ProjectNumber={ProjectNumber}, CustomerName={CustomerName}, InstallerName={InstallerName})";
        }
    }

    public record ProjectFile
    {
        [File(DefaultFileName: "Project.vis")]
        public string Data { get; init; }

        public string Filename { get; init; }

        /// <summary>
        /// Encoding used for the project file. Always ISO-8859-1 (Latin-1) as shown in top of xml project.
        /// </summary>
        public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding("ISO-8859-1");

        public override string ToString()
        {
            return $"ProjectFile(Filename={Filename}, Data={Data})";
        }
    }
}