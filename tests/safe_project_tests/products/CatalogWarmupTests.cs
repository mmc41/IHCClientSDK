using Ihc;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Products;

using NUnit.Framework;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Warming the embedded catalog before anything needs it, so its cost lands on a background thread at launch
    /// instead of inside the first user operation. Two doors, at two layers: <see cref="ProjectAppService.Warmup"/>
    /// is the one a HOST calls — the facade owns the SDK's warm-up, so a frontend never names an engine namespace
    /// to get it — and <c>BuiltInCatalog.Warmup</c> is the internal mechanism it forwards to.
    /// </summary>
    /// <remarks>
    /// <para><b>What this fixture does NOT prove, deliberately.</b> It says nothing about the single-publication
    /// property the warm-up rests on — that a caller arriving mid-build waits for the one materialization rather
    /// than starting a second. That property cannot be honestly tested from here, and an earlier version of this
    /// file that appeared to test it was measured to be vacuous twice over:</para>
    /// <para>The materialized value is process-wide and static, and other catalog fixtures sort ahead of this one
    /// — so in any real run the value is already published and every thread takes the lock-free fast path (542 ms
    /// for a racing test run alone, 8 ms in company). And even in a genuine race the assertion it made could not
    /// fail: <c>LazyThreadSafetyMode.PublicationOnly</c>, the spelling the warm-up replaced, lets the FACTORY run
    /// repeatedly but still publishes exactly one value to every caller — measured at sixteen factory runs and a
    /// single observed instance. So reference identity is green on both implementations, and the only thing that
    /// actually changed — how many times the factory RUNS — is not observable from a test without a production
    /// reset door and a metric read.</para>
    /// <para><b>So state the limit plainly rather than leaving a reader to infer it: emptying <c>Warmup</c>'s
    /// body leaves both tests below green.</b> What they pin is the door — that it exists on the facade, that a
    /// host reaches it without naming an engine namespace, that it does not throw, and that the catalog it
    /// points at is the one the facade serves. What no test here pins is that it BUILT anything, and the
    /// paragraphs above are why that is not an omission anyone can fix from inside the suite.</para>
    /// <para>Both spellings are correct under concurrency; what the lock buys is that the work is not duplicated.
    /// That is a structural property of the publication in <c>BuiltInCatalog</c>, established by review of the
    /// lock rather than by a test that would have passed either way — and a test that cannot fail is a worse
    /// guarantee than none, because it reads like a gate. See ADR-001, which admits multithreaded tests only as
    /// the deliverable of a decision and names the two that qualify.</para>
    /// </remarks>
    public class CatalogWarmupTests
    {
        /// <summary>
        /// What the host's door owes a caller, minus the one clause no test here can carry: it is reachable
        /// without naming an engine type, it does not throw, and the catalog it points at is the one the FACADE
        /// itself goes on to serve. That last clause is the one that can fail for a real reason — a door aimed
        /// at a catalog the service does not use would pass any weaker assertion while buying a launch nothing.
        /// <para>It does NOT assert that the door built anything, which is why it is no longer named as though
        /// it did: emptying <c>Warmup</c>'s body leaves this green, for the reason the fixture's remarks give.
        /// </para>
        /// </summary>
        [Test]
        public void TheHostsDoorIsReachable_AndPointsAtTheCatalogTheFacadeServes()
        {
            ProjectAppService.Warmup();

            ProductDefinition throughFacade = new ProjectAppService(new IhcSettings()).GetAvailableProducts()[0];

            Assert.That(throughFacade, Is.SameAs(new BuiltInCatalog().Products[0]),
                        "the facade serves a catalog other than the one its warm-up door points at, so warming "
                        + "it buys the launch nothing");
        }

        /// <summary>
        /// The mechanism below the door: a second warm-up reuses what the first published rather than replacing
        /// it. That the catalog is COMPLETE is deliberately not restated here — <c>BuiltInCatalogTests</c> and
        /// <c>BuiltInCatalogDigestTests</c> own that, over this same process-wide value and more strictly than a
        /// non-empty check could.
        /// </summary>
        /// <remarks>
        /// Identity is asserted on a DEFINITION, never on the list: <c>Products</c> returns an
        /// <c>ImmutableArray&lt;T&gt;</c> through an <c>IReadOnlyList&lt;T&gt;</c> property, so every read boxes the
        /// struct afresh and two reads of one shared catalog are never reference-equal. The definitions inside are
        /// records allocated once by the factory, so <c>Products[0]</c> is the probe that means anything.
        /// </remarks>
        [Test]
        public void ASecondWarmupReusesTheCatalogTheFirstPublished()
        {
            BuiltInCatalog.Warmup();
            ProductDefinition first = new BuiltInCatalog().Products[0];

            BuiltInCatalog.Warmup();

            Assert.That(new BuiltInCatalog().Products[0], Is.SameAs(first),
                        "a second warm-up replaced the published catalog");
        }
    }
}
