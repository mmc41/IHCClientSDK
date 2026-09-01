using System;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// Thrown when a <c>&lt;!ELEMENT&gt;/&lt;!ATTLIST&gt;</c> DTD block cannot be parsed into an
    /// <see cref="ElementSchema"/> — always naming the element type and the malformed construct, so a bad
    /// block in a file's captured inline DTD surfaces as a diagnosable load-time error instead of a raw
    /// index exception at save time.
    /// </summary>
    public sealed class VisSchemaFormatException : FormatException
    {
        public VisSchemaFormatException(string message) : base(message)
        {
        }
    }
}
