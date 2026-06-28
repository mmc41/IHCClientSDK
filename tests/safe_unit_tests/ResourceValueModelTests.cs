using System;
using System.Linq;
using NUnit.Framework;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A3 / D10: the SDK models all 12 ResourceValue kinds. These tests cover the enum completeness and the
    /// UnionValue payload fields for the four kinds added per D10 (PhoneNumber, SceneDimmer, SceneRelay,
    /// SceneShutter). The SOAP round-trip for these four is verified by Demonstration against a real/recorded
    /// controller (not mockable), per D10.
    /// </summary>
    [TestFixture]
    public class ResourceValueModelTests
    {
        [Test]
        public void ValueKind_HasAllTwelveKinds()
        {
            var kinds = Enum.GetNames(typeof(ResourceValue.ValueKind)).ToList();

            Assert.That(kinds, Has.Count.EqualTo(12));
            Assert.That(kinds, Does.Contain("PhoneNumber"));
            Assert.That(kinds, Does.Contain("SceneDimmer"));
            Assert.That(kinds, Does.Contain("SceneRelay"));
            Assert.That(kinds, Does.Contain("SceneShutter"));
        }

        [Test]
        public void UnionValue_ToString_RendersEachNewKindFields()
        {
            // The four kinds added per D10 must surface their payload field(s) in ToString (the C1/C2 result path
            // renders a ResourceValue via ToString). One representative union per kind - this also exercises the
            // model's payload fields functionally, replacing the earlier pure auto-property get/set tests.
            var phone = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.PhoneNumber, PhoneNumberValue = "+4512345678" };
            Assert.That(phone.ToString(), Does.Contain("PhoneNumberValue=+4512345678"));

            var dimmer = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.SceneDimmer, DimmerPercentage = 50, DimmerDelayTime = 100, DimmerRampTime = 200 };
            Assert.That(dimmer.ToString(), Does.Contain("SceneDimmer").And.Contain("DimmerPercentage=50").And.Contain("DimmerDelayTime=100").And.Contain("DimmerRampTime=200"));

            var relay = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.SceneRelay, RelayDelayTime = 30, RelayValue = true };
            Assert.That(relay.ToString(), Does.Contain("RelayDelayTime=30").And.Contain("RelayValue=True"));

            var shutter = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.SceneShutter, ShutterPositionIsUp = true, ShutterDelayTime = 15 };
            Assert.That(shutter.ToString(), Does.Contain("ShutterPositionIsUp=True").And.Contain("ShutterDelayTime=15"));
        }
    }
}
