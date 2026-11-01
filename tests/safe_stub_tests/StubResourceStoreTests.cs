using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;
using static Ihc.ResourceValue;
using static Ihc.Tests.StubResourceInteractionTests;

namespace Ihc.Tests
{
    /// <summary>
    /// The single-resource operations and OpenAPI's value reads and writes share the one resource store: a value
    /// written through one API reads back through every other.
    /// </summary>
    [TestFixture]
    public class StubResourceStoreTests
    {
        [TestCaseSource(typeof(StubResourceInteractionTests), nameof(EveryWritableKind))]
        public async Task AValueWrittenThroughOpenApi_ReadsBackThroughEveryApi(ResourceValue value)
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.OpenApi.SetValues([value]), Is.True);

            Assert.That((await sdk.OpenApi.GetValues([Id])).Single().Value, Is.EqualTo(value.Value));
            Assert.That((await sdk.Resources.GetRuntimeValue(Id)).Value, Is.EqualTo(value.Value));
            Assert.That((await sdk.Resources.GetRuntimeValues([Id])).Single().Value, Is.EqualTo(value.Value));
        }

        [TestCaseSource(typeof(StubResourceInteractionTests), nameof(EveryWritableKind))]
        public async Task AValueWrittenSingly_ReadsBackThroughEveryApi(ResourceValue value)
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Resources.SetResourceValue(value), Is.True);

            Assert.That(Timeless(await sdk.Resources.GetRuntimeValue(Id)), Is.EqualTo(Timeless(value)));
            Assert.That((await sdk.OpenApi.GetValues([Id])).Single().Value, Is.EqualTo(value.Value));
            Assert.That(sdk.Stub.Journal.Calls<IResourceInteractionService>(nameof(IResourceInteractionService.SetResourceValue)).Single().Payload,
                Is.EqualTo(value with { ValueTime = ShapeDefaults.Instant }));
        }

        [Test]
        public async Task AnOpenApiRead_AnswersOneValuePerId_InRequestOrder_WithFalseForAnUnknownId()
        {
            var stub = new ControllerStub();
            stub.Resources.Values = ImmutableDictionary<int, ResourceValue>.Empty
                .Add(1, Int(1, 10))
                .Add(2, Int(2, 20));
            using var sdk = StubbedSdk.Start(stub);

            IReadOnlyList<ResourceValue> read = await sdk.OpenApi.GetValues([2, 99, 1]);

            Assert.That(read.Select(value => (value.ResourceID, value.Value.ValueKind, value.Value.IntValue, value.Value.BoolValue)), Is.EqualTo(new[]
            {
                (2, ValueKind.INT, (int?)20, (bool?)null),
                (99, ValueKind.BOOL, null, false),
                (1, ValueKind.INT, 10, null),
            }));
        }

        [Test]
        public async Task ASingleRead_OfAnUnknownId_AnswersFalse()
        {
            using var sdk = StubbedSdk.Start();

            ResourceValue runtime = await sdk.Resources.GetRuntimeValue(99);
            ResourceValue initial = await sdk.Resources.GetInitialValue(99);

            Assert.That((runtime.ResourceID, runtime.Value.ValueKind, runtime.Value.BoolValue, runtime.IsValueRuntime),
                Is.EqualTo((99, ValueKind.BOOL, (bool?)false, true)));
            Assert.That((initial.Value.BoolValue, initial.IsValueRuntime), Is.EqualTo(((bool?)false, false)));
        }

        /// <summary>OpenAPI carries a bare value, so a write keeps the resource's own type, and it is a change the
        /// resource interaction service's next wait reports, as any other write is.</summary>
        [Test]
        public async Task AnOpenApiWrite_KeepsTheResourceType_AndQueuesAChange()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.OpenApi.SetValues([ResourceValue.CreateBooleanRuntimeInput(23386, true)]);

            ResourceValue stored = sdk.Stub.Resources.Values[23386];
            Assert.That((stored.TypeString, stored.IsValueRuntime, stored.Value.BoolValue), Is.EqualTo(("dataline_input", true, (bool?)true)));
            Assert.That((await sdk.Resources.WaitForResourceValueChanges(1)).Select(Timeless), Is.EqualTo(new[] { Timeless(stored) }));
            Assert.That(sdk.Stub.Journal.Calls<IOpenAPIService>(nameof(IOpenAPIService.SetValues)).Single().Payload,
                Is.EqualTo(ImmutableArray.Create(stored)));
        }

        [Test]
        public async Task AnOpenApiWrite_OfAnUnknownResource_StoresARuntimeValueOfNoType()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.OpenApi.SetValues([Int(Id, 7)]);

            ResourceValue stored = sdk.Stub.Resources.Values[Id];
            Assert.That((stored.TypeString, stored.IsValueRuntime, stored.Value.IntValue), Is.EqualTo(((string?)null, true, (int?)7)));
        }

        private static ResourceValue Int(int id, int value) =>
            new() { ResourceID = id, IsValueRuntime = true, Value = new UnionValue { ValueKind = ValueKind.INT, IntValue = value } };
    }
}
