#nullable enable
using System;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// Thrown by <see cref="ProjectAppService.Load(System.IO.Stream)"/>/<see cref="ProjectReader"/> when the
    /// input is not a loadable <c>.vis</c>/<c>.ihc</c> project: empty or compressed data, a BOM or wrong
    /// declared encoding, malformed XML, a non-<c>utcs_project</c> root, character data the attribute-only
    /// model cannot represent, or a malformed inline DTD. One typed catch for a GUI's "could not open this
    /// file" path, always carrying enough context (position, element, excerpt) to act on.
    /// </summary>
    public sealed class ProjectFormatException : FormatException
    {
        public ProjectFormatException(string message) : base(message)
        {
        }

        public ProjectFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
