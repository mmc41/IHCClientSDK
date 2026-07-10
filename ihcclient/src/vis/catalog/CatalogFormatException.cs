#nullable enable
using System;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Thrown when catalog component text (<c>.def</c>/<c>.ifb</c>) cannot be processed as such: a header whose
    /// inline DTD uses a construct outside the catalog grammar envelope in strict parsing
    /// (<see cref="CatalogDtdParser"/>), or writer output that does not reparse as well-formed XML
    /// (<see cref="CatalogFileWriter"/>'s well-formedness gate — the typed refusal that provably leaves the
    /// destination stream untouched). One typed catch for a caller's "this is not a writable catalog file" path.
    /// </summary>
    public sealed class CatalogFormatException : FormatException
    {
        public CatalogFormatException(string message) : base(message)
        {
        }

        public CatalogFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
