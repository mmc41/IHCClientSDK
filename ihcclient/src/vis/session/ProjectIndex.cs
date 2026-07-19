#nullable enable
namespace Ihc.Vis.Session
{
    /// <summary>
    /// The per-commit id→element / id→parent lookup substrate (proposal P1a) that a command's
    /// <see cref="ProjectCommand.Evaluate"/> and the Wave-3 drag-over probe share, so legality checks stop paying
    /// repeated O(N) tree walks per pointer event.
    /// </summary>
    /// <remarks>
    /// W2-1 declares the type so <see cref="EditContext"/> can carry it; the <c>FrozenDictionary</c> build from a
    /// <c>Project</c> and the lookup methods land in W2-2.
    /// </remarks>
    internal sealed class ProjectIndex
    {
    }
}
