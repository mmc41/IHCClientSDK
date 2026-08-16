#nullable enable
using System.Globalization;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// Mints the opaque keys <see cref="DefinitionDocumentation.Resources"/> stores per-resource help text under —
    /// the resource's <b>position</b> in the definition body, which is the only thing that identifies a resource.
    /// </summary>
    /// <remarks>
    /// <para>Neither of the obvious candidates works. A display <b>name</b> is not unique: the vendor catalog names an
    /// input and an output the same thing (block 1.4.03's two <c>"Sluk"</c>), and repeats one four times inside a
    /// single product (Beolink1000's <c>"Not in use"</c>). Nor is the placeholder <b>id</b>: Controller Link OUT/IN
    /// give all their <c>dataline_output</c> pins the same pinned <c>_0x02</c>. A position cannot collide with itself,
    /// so it is the one key that needs no measurement to be trusted.</para>
    /// <para><b>Both sides of the key must come from here.</b> The builder mints it as it appends the resource and the
    /// definition's projection recomputes it as it enumerates the body — a key format either side spelled for itself
    /// would drift, and a drifted key does not throw, it silently reads back nothing. Callers never see these keys:
    /// help text reaches them already attached to its pin, on <see cref="ResourceSummary.Documentation"/>.</para>
    /// </remarks>
    internal static class ResourceDocKey
    {
        /// <summary>The key for the product body child at <paramref name="bodyChildIndex"/> — the raw index among
        /// <see cref="ProjectElement.Children"/>, counting the structural children (<c>scenes</c>, the settings
        /// containers) the resource projection filters out, so the key never shifts when the filter changes.</summary>
        public static string ForProduct(int bodyChildIndex) =>
            "[" + bodyChildIndex.ToString(CultureInfo.InvariantCulture) + "]";

        /// <summary>The key for the function-block resource at <paramref name="indexInContainer"/> of the
        /// <paramref name="containerTag"/> container (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/
        /// <c>internalsettings</c>) — the container qualifies the index, since each container counts from zero.</summary>
        public static string ForBlock(string containerTag, int indexInContainer) =>
            containerTag + "[" + indexInContainer.ToString(CultureInfo.InvariantCulture) + "]";
    }
}
