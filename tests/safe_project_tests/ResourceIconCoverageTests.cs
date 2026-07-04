using System.Collections.Generic;
using System.Linq;
using Ihc.Projects;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Coverage guard for the per-resource-type GUI icon table (review suggestion #2). Every element type whose schema
    /// declares an <c>icon</c> attribute must be explicitly categorized as either carrying a non-default icon override
    /// (<see cref="ResourceMaterialization.IconOverrideTags"/>) or using its DTD-default icon
    /// (<see cref="ResourceMaterialization.KnownDefaultIconTags"/>). A newly declared icon-bearing type that is in
    /// neither set fails these tests, turning a silent <c>"_0x0"</c> fall-through into a forced decision — so the icon
    /// table can never fall behind the grammar without the suite flagging it. Deterministic (no install dir).
    /// </summary>
    public class ResourceIconCoverageTests
    {
        private static IEnumerable<string> IconBearingTags() =>
            ProjectSchemaRegistry.AllSchemas
                .Where(s => s.Attrs.Any(a => a.Name == "icon"))
                .Select(s => s.Tag);

        [Test]
        public void EveryIconBearingType_IsCategorized_AsOverrideOrKnownDefault()
        {
            HashSet<string> overrides = ResourceMaterialization.IconOverrideTags.ToHashSet();
            IReadOnlySet<string> knownDefault = ResourceMaterialization.KnownDefaultIconTags;

            List<string> uncategorized = IconBearingTags()
                .Where(tag => !overrides.Contains(tag) && !knownDefault.Contains(tag))
                .OrderBy(tag => tag)
                .ToList();

            Assert.That(uncategorized, Is.Empty,
                "icon-bearing element types missing from BOTH ResourceMaterialization.Icons and KnownDefaultIconTags — "
                + "each must be characterized (non-default icon override vs DTD-default icon) rather than silently "
                + "emitting _0x0. Add the type to whichever set is correct for its vendor icon.");
        }

        [Test]
        public void OverrideAndKnownDefaultSets_AreDisjoint()
        {
            List<string> overlap = ResourceMaterialization.IconOverrideTags
                .Where(ResourceMaterialization.KnownDefaultIconTags.Contains)
                .OrderBy(tag => tag)
                .ToList();

            Assert.That(overlap, Is.Empty, "a type cannot be both an icon override and a known-default icon type");
        }

        [Test]
        public void IconTables_ContainNoStaleTags_NotDeclaredWithAnIconAttribute()
        {
            HashSet<string> iconBearing = IconBearingTags().ToHashSet();

            List<string> stale = ResourceMaterialization.IconOverrideTags
                .Concat(ResourceMaterialization.KnownDefaultIconTags)
                .Where(tag => !iconBearing.Contains(tag))
                .OrderBy(tag => tag)
                .ToList();

            Assert.That(stale, Is.Empty,
                "icon-table tags that no registry schema declares with an `icon` attribute (stale or renamed element types)");
        }
    }
}
