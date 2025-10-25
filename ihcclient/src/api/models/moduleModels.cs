using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Module;

namespace Ihc {

    /// <summary>
    /// High level model of a scene project file without soap distractions.
    /// </summary>
    public record SceneProject {
            /// <summary>
            /// Raw binary data of the scene project file.
            /// TODO: Check if a more useful string format can be returned instead.
            /// </summary>
            public byte[] Data { get; init; }

            /// <summary>
            /// Name of the scene project file.
            /// </summary>
            public string Filename { get; init; }

            public override string ToString()
            {
              return $"SceneProject(Data=byte[{Data?.Length ?? 0}], Filename={Filename})";
            }
    }

    /// <summary>
    /// High level model of scene project information without soap distractions.
    /// </summary>
    public record SceneProjectInfo {
            /// <summary>
            /// Name of the scene project.
            /// </summary>
            public string Name { get; init; }

            /// <summary>
            /// Size of the scene project file in bytes.
            /// </summary>
            public int Size { get; init; }

            /// <summary>
            /// File path to the scene project.
            /// </summary>
            public string Filepath { get; init; }

            /// <summary>
            /// Indicates whether the project is stored remotely.
            /// </summary>
            public bool Remote { get; init; }

            /// <summary>
            /// Version of the scene project.
            /// </summary>
            public string Version { get; init; }

            /// <summary>
            /// Date and time when the project was created.
            /// </summary>
            public DateTimeOffset Created { get; init; }

            /// <summary>
            /// Date and time when the project was last modified.
            /// </summary>
            public DateTimeOffset LastModified { get; init; }

            /// <summary>
            /// Description of the scene project.
            /// </summary>
            public string Description { get; init; }

            /// <summary>
            /// CRC checksum of the project file.
            /// </summary>
            public long Crc { get; init; }

            public override string ToString()
            {
              return $"SceneProjectInfo(Name={Name}, Size={Size}, Filepath={Filepath}, Remote={Remote}, Version={Version}, Created={Created}, LastModified={LastModified}, Description={Description}, Crc={Crc})";
            }
    }

}