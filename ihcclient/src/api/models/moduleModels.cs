using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Module;

namespace Ihc {

    /// <summary>
    /// A scene project file of IHC SceneDesign and IHC SceneView, or one segment of it, as
    /// <see cref="IModuleService"/> moves it.
    /// </summary>
    public record SceneProject : BinaryFile
    {
      /// <summary>
      /// Raw bytes of the scene project's .icz file, or of one segment of it. The file is a ZIP archive holding the
      /// project's XML (<c>project.icw</c>) and its images.
      /// </summary>
      public byte[] Data { get; init; }

      /// <summary>
      /// The name the transfer carries, without extension: the controller names its copy <c>iczproject</c> and does not
      /// keep a name given on upload.
      /// </summary>
      public string? Filename { get; init; }

      /// <summary>
      /// Canonical file extensions (without leading dot) for a scene project.
      /// </summary>
      public static string[] FileExtensions { get; } = { "icw", "icz" };

      public SceneProject(string? Filename, byte[] Data)
      {
        this.Data = Data;
        this.Filename = Filename;
      }

      /// <summary>
      /// Copy-constructor required by the BinaryFile contract.
      /// </summary>
      public SceneProject(BinaryFile input)
      {
        ArgumentNullException.ThrowIfNull(input);
        this.Data = input.Data;
        this.Filename = input.Filename;
      }

      public override string ToString()
      {
        return $"SceneProject(Data=byte[{Data.Length}], Filename={Filename})";
      }
    }
    
    /// <summary>
    /// Segment of a scene project (a raw binary chunk of a segmented scene-project transfer).
    /// </summary>
    public record SceneProjectSegment : BinaryFile
    {
      /// <summary>
      /// Raw bytes of one segment of the scene project's .icz file; empty when the reply carries no data.
      /// </summary>
      public byte[] Data { get; init; } = Array.Empty<byte>();

      /// <summary>
      /// Name of the segment file; <c>null</c> from a segment download, which supplies none.
      /// </summary>
      public string? Filename { get; init; }

      public SceneProjectSegment() { }

      /// <summary>
      /// Copy-constructor required by the BinaryFile contract.
      /// </summary>
      public SceneProjectSegment(BinaryFile input)
      {
        ArgumentNullException.ThrowIfNull(input);
        this.Data = input.Data;
        this.Filename = input.Filename;
      }

      public override string ToString()
      {
        return $"SceneProjectSegment(Data=byte[{Data.Length}])";
      }
    }

    /// <summary>
    /// What the controller reports about the scene project it holds. The name, dates and description come from
    /// the project file itself.
    /// </summary>
    public record SceneProjectInfo {
            /// <summary>
            /// The project's own name, as stored in its project file; not the file name.
            /// </summary>
            public string? Name { get; init; }

            /// <summary>
            /// Size of the scene project file in bytes.
            /// </summary>
            public int Size { get; init; }

            /// <summary>
            /// Path of the .icz file on the controller, a fixed path ending in <c>iczproject.icz</c>. Its file name
            /// without <c>.icz</c> is the name <see cref="IModuleService.GetSceneProjectSegment"/> takes.
            /// </summary>
            public string? Filepath { get; init; }

            /// <summary>
            /// <c>true</c> for a project held on the controller, as opposed to a local file.
            /// </summary>
            public bool Remote { get; init; }

            /// <summary>
            /// The controller reports an empty string; the format version inside the file is not given here.
            /// </summary>
            public string? Version { get; init; }

            /// <summary>
            /// When the project was created, as recorded in the project file. The SDK reads it at a fixed +01:00
            /// offset, so the instant is right only for a time stamped at UTC+1; <c>DateTimeOffset.MinValue</c> when
            /// the reply carries no readable date.
            /// </summary>
            public DateTimeOffset Created { get; init; }

            /// <summary>
            /// When the project was last saved, as recorded in the project file, not the controller's file time. The
            /// SDK reads it at a fixed +01:00 offset, so the instant is right only for a time stamped at UTC+1;
            /// <c>DateTimeOffset.MinValue</c> when the reply carries no readable date.
            /// </summary>
            public DateTimeOffset LastModified { get; init; }

            /// <summary>
            /// The description stored in the project file; empty when it has none.
            /// </summary>
            public string? Description { get; init; }

            /// <summary>
            /// Checksum of the project: the XOR of the CRC-32 values of the entries in the .icz archive, not a CRC of
            /// the whole file.
            /// </summary>
            public long Crc { get; init; }

            public override string ToString()
            {
              return $"SceneProjectInfo(Name={Name}, Size={Size}, Filepath={Filepath}, Remote={Remote}, Version={Version}, Created={Created}, LastModified={LastModified}, Description={Description}, Crc={Crc})";
            }
    }

}