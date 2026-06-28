#nullable enable
using System;

namespace Ihc.Projects
{
    /// <summary>
    /// Nested fluent builder for authoring a custom program (<c>program_simple</c>/<c>program_sub</c>/
    /// <c>program_case</c>) directly, rather than receiving one via a catalog deep-copy. Leaf
    /// event/condition/action elements reference resources by live <see cref="ResourceRef"/> handle —
    /// the same handles consumed by <see cref="ProjectEditor.Link"/>, so programs and links address
    /// resources the same way.
    /// </summary>
    /// <remarks>
    /// Stage 1: full method signatures, stub bodies. Most programs arrive from the catalog;
    /// this surface exists for the future "author your own logic" path.
    /// </remarks>
    public sealed class ProgramBuilder
    {
        /// <summary>Sets the program name.</summary>
        public ProgramBuilder Name(string name) => throw new NotImplementedException();

        /// <summary>Adds an <c>event</c> triggered by the given resource.</summary>
        public ProgramBuilder OnEvent(ResourceRef trigger) => throw new NotImplementedException();

        /// <summary>Adds a <c>condition</c> gating the program on the given resource.</summary>
        public ProgramBuilder When(ResourceRef condition) => throw new NotImplementedException();

        /// <summary>Adds an <c>action</c> driving the given resource.</summary>
        public ProgramBuilder Do(ResourceRef target) => throw new NotImplementedException();
    }
}
