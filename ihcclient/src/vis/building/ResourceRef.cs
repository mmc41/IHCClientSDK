#nullable enable

namespace Ihc.Projects
{
    /// <summary>
    /// A live handle to a resource (a product input/output or a function-block input/output) in the
    /// edit session. Carries the resource's stable identity — its real <c>_0x</c> id, which is
    /// preserved when a project is loaded and allocated when a resource is newly added — so a link
    /// made via <see cref="ProjectEditor.Link"/> survives renames and round-trips losslessly.
    /// </summary>
    public sealed class ResourceRef
    {
        internal ResourceRef(string name, ElementId? id)
        {
            Name = name;
            Id = id;
        }

        /// <summary>The resource's display name.</summary>
        public string Name { get; }

        /// <summary>The resource's real id once allocated; <c>null</c> while still pending allocation.</summary>
        public ElementId? Id { get; }
    }
}
