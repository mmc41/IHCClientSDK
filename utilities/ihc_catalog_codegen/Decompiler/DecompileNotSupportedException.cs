#nullable enable
using System;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Raised when the decompiler meets a construct the current bring-up stage cannot yet reverse into a fluent
    /// builder call — a nested container (needs <c>RawChild</c>, plan step B1b), a catalog-vs-project default bake
    /// (B1c), an embedded enum / IDREF wiring (B1d) or an open-world element type (needs <c>InlineDtdBlock</c>, B1d).
    /// The self-verify harness reports these as <c>UNSUPPORTED</c> (with the reason) rather than a fidelity failure,
    /// so a flat-product pass cleanly distinguishes "not implemented yet" from "wrong".
    /// </summary>
    internal sealed class DecompileNotSupportedException : Exception
    {
        public DecompileNotSupportedException(string message) : base(message)
        {
        }
    }
}
