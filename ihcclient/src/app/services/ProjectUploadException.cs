#nullable enable
using System;

namespace Ihc.Vis
{
    /// <summary>
    /// Thrown when the controller declines to store an uploaded project (the SOAP operation returned
    /// <c>false</c> after change mode was already entered). The controller's project state is uncertain at
    /// that point, so the failure must surface as an exception rather than an easily-ignored return value.
    /// </summary>
    public sealed class ProjectUploadException : InvalidOperationException
    {
        public ProjectUploadException(string message) : base(message)
        {
        }
    }
}
