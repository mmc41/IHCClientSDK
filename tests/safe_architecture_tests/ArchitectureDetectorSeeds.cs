namespace Ihc.Vis.Editing.Seeded
{
    // Test-only types in a nested engine namespace. Architecture detector controls use them to prove that namespace
    // subtree checks and generic-constraint traversal do not stop at the exact Ihc.Vis.Editing namespace.
    internal interface INestedEngineContract { }

    internal sealed class NestedEngineType : INestedEngineContract { }
}
