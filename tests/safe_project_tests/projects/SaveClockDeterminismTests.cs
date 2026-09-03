using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The default (vendor-like) save re-stamps the root <c>id2</c> and the <c>&lt;modified&gt;</c> element from the
    /// service clock, so the bytes it writes are a function of that clock. A fixture that compares two such saves is
    /// therefore stable only while the clock is pinned — which is what <see cref="SessionCommandFixture.SaveClock"/>
    /// is for, and what these tests hold it to.
    /// <para>
    /// The failure this guards against is CI-shaped rather than reproducible on demand: the window between two saves
    /// is a few milliseconds, so on the system clock the two land either side of a second boundary at roughly that
    /// fraction of a second — often enough to reach a build agent, rarely enough to survive a developer's run.
    /// </para>
    /// </summary>
    public class SaveClockDeterminismTests : SessionCommandFixture
    {
        private const string Fixture = "project2-CustomBlock.vis";

        /// <summary>
        /// The mechanism, stated deterministically: one second of clock is one byte of <c>id2</c>, which packs
        /// day/hour/minute/second. The two stamps are the pair an agent run actually produced.
        /// </summary>
        [Test]
        public async Task ADefaultSave_OneSecondLater_WritesDifferentBytes()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 3, 16, 32, 4, TimeSpan.Zero));
            var app = new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), clock);
            Project project = await app.Load(TestData.PathOf("projects", Fixture));

            using var first = new MemoryStream();
            await app.Save(project, first);
            clock.Advance(TimeSpan.FromSeconds(1));
            using var second = new MemoryStream();
            await app.Save(project, second);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectReader.Read(first.ToArray()).Id2, Is.EqualTo("_0x3102004"),
                    "16:32:04 on the 3rd packs to _0x3102004");
                Assert.That(ProjectReader.Read(second.ToArray()).Id2, Is.EqualTo("_0x3102005"),
                    "…and one second later to _0x3102005");
                Assert.That(second.ToArray(), Is.Not.EqualTo(first.ToArray()),
                    "so the same project saved twice one second apart is NOT byte-identical");
            });
        }

        /// <summary>
        /// The guard: the family's saves carry the pinned stamp, so the wall clock cannot reach the bytes. Asserting
        /// the stamp rather than waiting out a real second boundary keeps this deterministic and free.
        /// </summary>
        [Test]
        public async Task TheFamilysSaves_AreStampedFromThePinnedClock_NotTheWallClock()
        {
            Project saved = ProjectReader.Read(await Bytes(await Load(Fixture)));

            Assert.Multiple(() =>
            {
                Assert.That(saved.Id2, Is.EqualTo(PackedStamp.FromDateTime(SaveClock).ToToken()),
                    "a default save through App is stamped from SaveClock");
                ProjectElement modified = saved.Child("modified")!;
                Assert.That(modified.GetAttribute("day"), Is.EqualTo("27"));
                Assert.That(modified.GetAttribute("hour"), Is.EqualTo("16"));
                Assert.That(modified.GetAttribute("minute"), Is.EqualTo("5"));
            });
        }

        /// <summary>The property every byte-fidelity fixture in this family rests on, asserted directly.</summary>
        [Test]
        public async Task RepeatedDefaultSaves_OfAnUneditedProject_AreByteIdentical()
        {
            Project project = await Load(Fixture);

            TestData.AssertBytesIdentical(await Bytes(project), await Bytes(project),
                "two saves of an unedited project agree");
        }
    }
}
