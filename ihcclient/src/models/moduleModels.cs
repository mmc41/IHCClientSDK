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

}