using System.Linq;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M3 / ADR-002 / D07 (T008): the SDK <see cref="VariableTypeRegistry"/> is the single source the placement
    /// engine admits value insertion by, so the registry and the engine's accept-set cannot drift. The six types M3
    /// found dropped from the UI (resource_light_level/resource_humidity_level, kW/kWh/W/Wh) are all in the registry;
    /// the UI decides only whether to present or suppress each (VariablePaletteCompletenessTests, UNIT).
    /// </summary>
    public class VariableTypeRegistryTests
    {
        [Test]
        public void EveryValueTypeTag_IsAdmittedIntoFunctionBlockContainers()
        {
            Assert.Multiple(() =>
            {
                foreach (string tag in VariableTypeRegistry.ValueTypeTags)
                {
                    Assert.That(PlacementRules.CanInsert("settings", tag, "functionblock"), Is.True,
                        $"the engine admits the registry value type {tag} into a function-block settings container");
                    Assert.That(PlacementRules.CanInsert("inputs", tag, "functionblock"), Is.True,
                        $"{tag} is a value variable, legal in any block container");
                }
            });
        }

        [Test]
        public void Registry_ContainsTheSixTypesM3Dropped_AndClassifiesRoles()
        {
            var tags = VariableTypeRegistry.All.Select(t => t.Tag).ToHashSet();
            Assert.Multiple(() =>
            {
                foreach (string t in new[] { "resource_light_level", "resource_humidity_level", "kW", "kWh", "W", "Wh" })
                    Assert.That(tags, Does.Contain(t), $"{t} is in the SDK registry");
                Assert.That(VariableTypeRegistry.All.Single(t => t.Tag == "resource_input").Role, Is.EqualTo(VariableRole.Input));
                Assert.That(VariableTypeRegistry.All.Single(t => t.Tag == "resource_output").Role, Is.EqualTo(VariableRole.Output));
                Assert.That(VariableTypeRegistry.All.Single(t => t.Tag == "resource_flag").Role, Is.EqualTo(VariableRole.Value));
                Assert.That(VariableTypeRegistry.ValueTypeTags, Does.Not.Contain("resource_input"),
                    "a pin type is not a value type");
            });
        }
    }
}
