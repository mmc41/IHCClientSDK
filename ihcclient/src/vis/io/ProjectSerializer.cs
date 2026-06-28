#nullable enable
using System;

namespace Ihc.Projects
{
    /// <summary>
    /// The pure, low-level <c>.vis</c> writer: serializes a <see cref="Project"/> to its on-disk bytes
    /// exactly as-is — no clock, no metadata re-stamping. Used directly by byte-fidelity tests. Metadata
    /// re-stamping is the job of <see cref="ProjectAppService"/>'s instance <c>Save</c> overloads, which hold
    /// the clock and delegate here once the metadata is settled; this serializer emits whatever metadata the
    /// project already carries.
    /// </summary>
    /// <remarks>Stage 1: signature only; the writer engine is delivered in Stage 2.</remarks>
    public static class ProjectSerializer
    {
        /// <summary>Serializes a project to its <c>.vis</c> byte representation, verbatim.</summary>
        public static byte[] Serialize(Project project) => throw new NotImplementedException();
    }
}
