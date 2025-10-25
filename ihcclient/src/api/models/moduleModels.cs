using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Module;

namespace Ihc {

    public record SceneProject {
            // TODO: Check if a more useful string format can be returned instead.
            public byte[] Data { get; init; }

            public string Filename { get; init; }

            public override string ToString()
            {
              return $"SceneProject(Data=byte[{Data?.Length ?? 0}], Filename={Filename})";
            }
    }

    public record SceneProjectInfo {
            public string Name { get; init; }
            public int Size { get; init; }
            public string Filepath { get; init; }
            public bool Remote { get; init; }
            public string Version { get; init; }
            public DateTimeOffset Created { get; init; }
            public DateTimeOffset LastModified { get; init; }
            public string Description { get; init; }
            public long Crc { get; init; }

            public override string ToString()
            {
              return $"SceneProjectInfo(Name={Name}, Size={Size}, Filepath={Filepath}, Remote={Remote}, Version={Version}, Created={Created}, LastModified={LastModified}, Description={Description}, Crc={Crc})";
            }
    }

}