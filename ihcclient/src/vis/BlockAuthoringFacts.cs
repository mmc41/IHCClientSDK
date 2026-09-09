using Ihc.Vis.Schema;

namespace Ihc.Vis
{
    /// <summary>
    /// One section a function block declares — the identity a caller addresses it by, the caption it falls back to
    /// when the file names none, and whether it is internal to the block.
    /// </summary>
    /// <param name="Container">The container tag the section is stored under, and the identity to address it by.</param>
    /// <param name="Caption">
    /// The fallback caption. A block's own stored name wins where it has one — a name in the file is data, not a
    /// caption the SDK owns — and this is what a section that leaves it unset reads as.
    /// </param>
    /// <param name="IsInternal">
    /// Whether the section holds variables internal to the block rather than part of its interface. It is a fact
    /// about the section, not a display rule: what a frontend does with it — hide it outside programming mode, sort
    /// it last, mark it — stays the frontend's decision.
    /// </param>
    public sealed record FunctionBlockSectionInfo(string Container, string Caption, bool IsInternal);

    /// <summary>
    /// One variable type a container will accept, with what that type IS.
    /// </summary>
    /// <param name="Tag">The element tag the variable is created with.</param>
    /// <param name="Role">
    /// What the type is in the block's model: an input pin, an output pin, or a value the block holds. Carried from
    /// the same registry that decides which types exist at all, so a type added there is classified here without a
    /// caller re-deriving it from tag spellings — and kept as the role rather than a signal/value flag, because
    /// which DIRECTION a signal has is a question a palette or a bar menu asks too.
    /// </param>
    public sealed record InsertableVariableType(string Tag, VariableRole Role)
    {
        /// <summary>Whether the type is one of the block's SIGNALS — its wiring in or out — rather than a value it
        /// holds. The convenience over <see cref="Role"/> that the section-flyout ordering asks for.</summary>
        public bool IsSignal => Role is not VariableRole.Value;
    }
}
