using System;

public record SDInfo {
        public long Size { get; init; }

        public long Free { get; init; }

        public override string ToString()
        {
          return $"SDInfo(Size={Size}, Free={Free})";
        }
}

/*
public enum ControllerState
{
        Ready,
        Initializing,
        Unknown
}*/

public static class ControllerStates
{
        public const string READY = "text.ctrl.state.ready";
        public const string INIT = "text.ctrl.state.initialize";
}

public record BackupFile
{
        // Hmm. Can't identify the file format.Does not seem to be compressed or encoded text. Raw Binary?
        public byte[] Data { get; init; }

        public string Filename { get; init; }

        public override string ToString()
        {
          return $"BackupFile(Data=byte[{Data?.Length ?? 0}], Filename={Filename})";
        }
}

public record ProjectInfo
{
        public int VisualMinorVersion { get; init; }

        public int VisualMajorVersion { get; init; }

        public int ProjectMajorRevision { get; init; }

        public int ProjectMinorRevision { get; init; }

        public DateTimeOffset? Lastmodified { get; init; }

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
        public string Data { get; init; }

        public string Filename { get; init; }

        /// <summary>
        /// Encoding used for the project file. Always ISO-8859-1 (Latin-1) as shown in top of xml project.
        /// </summary>
        public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding("ISO-8859-1");

        public override string ToString()
        {
          return $"ProjectFile(Data=string[{Data?.Length ?? 0}], Filename={Filename})";
        }
}