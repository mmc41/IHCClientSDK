#nullable enable

using Ihc.Vis.Model;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// A live handle to a product's <c>scenes</c> container in the edit session — the target side of a scene
    /// membership (US-024). Obtained via <see cref="ProductRef.Scenes"/> and consumed by
    /// <see cref="ProjectEditor.LinkScene(ResourceRef,ScenesRef,SceneValue)"/>/<see cref="ProjectEditor.UnlinkScene(ResourceRef,ScenesRef)"/>, which append/remove the
    /// member rows inside it. Carries the container's stable identity like <see cref="ResourceRef"/>.
    /// </summary>
    public sealed class ScenesRef
    {
        internal ScenesRef(string name, ElementId id)
        {
            Name = name;
            Id = id;
        }

        /// <summary>The container's display name (e.g. "Scenarier", or "Scenarier/regulering" on dimmers).</summary>
        public string Name { get; }

        /// <summary>The container's stable <c>_0x</c> identity.</summary>
        public ElementId Id { get; }
    }
}
