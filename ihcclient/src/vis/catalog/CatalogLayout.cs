namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Which on-disk shape a catalog file is written in. IHC Visual has two writers and they do not agree, so a
    /// file's layout says which one made it (uxparity S-22, measured on the vendor's own install):
    ///
    /// <list type="bullet">
    /// <item><b><see cref="Catalog"/></b> — the shipped corpus (<c>1.1.01.ifb</c>, every <c>.def</c>): two-space
    /// indent from column 0, <c>" /&gt;"</c> with a space, <c>&lt;!DOCTYPE root[</c> with none.</item>
    /// <item><b><see cref="Export"/></b> — what the interactive <i>Gem funktionsblok…</i> writes
    /// (<c>AutoProof.ifb</c>, and the <c>gemoracle-kip.ifb</c> oracle): three-space indent with the root's children
    /// at column 6, <c>"/&gt;"</c> with no space, <c>&lt;!DOCTYPE root [</c> with one.</item>
    /// </list>
    ///
    /// <para>The distinction matters because fidelity is judged differently either side of it. The shipped corpus is
    /// hand-formatted (mixed indent, blank lines, trailing spaces), so its whitespace cannot be reconstructed and
    /// <see cref="CatalogTextCompare"/> compares it normalized. An exported block is compared against a file the
    /// vendor's writer produced seconds earlier, which is perfectly regular — so that one is held to the byte.</para>
    /// </summary>
    public enum CatalogLayout
    {
        /// <summary>The shipped-catalog shape; the default, since reading and round-tripping the corpus is the
        /// common case.</summary>
        Catalog,

        /// <summary>The shape the vendor's save-to-library writes.</summary>
        Export
    }
}
