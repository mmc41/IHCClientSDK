using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Module;

namespace Ihc {

    public record SceneProject {
            // TODO: Check if a more useful string format can be returned instead.
            public byte[] Data { get; init; }

            public string Filename { get; init; }
    }

    public record SceneProjectInfo {
            public string Name { get; init; }
            public int Size { get; init; }
            public string Filepath { get; init; }
            public bool Remote { get; init; }
            public string Version { get; init; }
            public DateTime? Created { get; init; }
            public DateTime? LastModified { get; init; }
            public string Description { get; init; }
            public long Crc { get; init; }
    }

}