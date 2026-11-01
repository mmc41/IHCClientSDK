using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Ihc.Envelope;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Every generated contract operation is answered at the dispatcher. A shape-built request, written by the
    /// SDK's own serializer, goes straight to the stub without an SDK wrapper, and the answer must read back
    /// through both the stub's codec and the SDK's serializer. This proves the wire for operations no wrapper
    /// reaches in a stub test; it proves nothing about the SDK's mapping of the answer.
    /// </summary>
    [TestFixture]
    public class StubWireCompletenessTests
    {
        private const int GeneratedWireOperations = 157;

        private static IEnumerable<SdkOperation> Operations() => SdkOperations.Rows;

        [Test]
        public void TheEnumeration_FindsEveryGeneratedOperation()
        {
            Assert.That(Operations().Count(), Is.EqualTo(GeneratedWireOperations));
        }

        [TestCaseSource(nameof(Operations))]
        public async Task EveryOperation_IsAnsweredAtTheDispatcher(SdkOperation operation)
        {
            Type responseType = SoapDispatcher.ResponseTypeOf(operation);

            var stub = new ControllerStub();
            ControllerStubResponse answer = await stub.HandleAsync(
                StubExchanges.For(operation.Service, operation.WireAction), CancellationToken.None);

            Assert.That(stub.Unmodelled, Is.Empty);
            Assert.That(answer.StatusCode, Is.EqualTo(200), answer.Body);
            Assert.That(SoapCodec.ReadResponse(responseType, answer.Body), Is.InstanceOf(responseType));
            object? sdkRead = StubExchanges.SdkReadsResponse(responseType, answer.Body);
            Assert.That(sdkRead, Is.InstanceOf(responseType), "the SDK's own serializer reads the answer's body");
            // A member the reader failed to match would come back at its default and write differently, so an equal
            // rewrite is what shows the values arrived rather than only the element.
            Assert.That(SoapCodec.WriteResponse(responseType, sdkRead!), Is.EqualTo(answer.Body),
                "every member of the answer survives the SDK's read");
        }

        /// <summary>The two <c>testSendMessage</c> overloads declare one WSDL action between them; each is still
        /// answered as its own operation, because the dispatcher keys on the wire action the SDK posts.</summary>
        [Test]
        public void TwoOverloadsSharingAWsdlAction_AreDispatchedByWireAction()
        {
            SdkOperation[] overloads =
            [
                SdkOperations.Find("ConfigurationService", "testSendMessage")!,
                SdkOperations.Find("ConfigurationService", "testSendMessage1")!,
            ];

            Assert.That(overloads.Select(operation => operation.ContractMethod.GetCustomAttribute<OperationContractAttribute>()!.Action).Distinct(),
                Has.Exactly(1).Items);
            Assert.That(overloads.Select(operation => operation.ContractMethod.ReturnType).Distinct(), Has.Exactly(2).Items);
        }

        [Test]
        public async Task AnExchangeNoOperationMatches_AnswersAFault_AndIsRecorded()
        {
            var stub = new ControllerStub();

            ControllerStubResponse answer = await stub.HandleAsync(
                new ControllerStubRequest("ControllerService", "noSuchOperation", string.Empty, null), CancellationToken.None);

            Assert.That(answer.StatusCode, Is.EqualTo(500));
            Assert.That(answer.Body, Does.Contain("<faultstring>ihcclient_stub: ControllerService.noSuchOperation is not modelled</faultstring>"));
            Assert.That(stub.Unmodelled.Select(call => (call.Service, call.SoapAction)),
                Is.EqualTo(new[] { ("ControllerService", "noSuchOperation") }));
        }

        [Test]
        public async Task AQuotedSoapActionHeader_FindsTheSameOperation()
        {
            ControllerStubRequest unquoted = StubExchanges.For("ControllerService", "getState");
            var stub = new ControllerStub();

            ControllerStubResponse answer = await stub.HandleAsync(
                new ControllerStubRequest(unquoted.Service, "\"getState\"", unquoted.Body, null), CancellationToken.None);

            Assert.That(answer.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public void TheCodec_CachesOneConfiguredSerializerPerEnvelopeType()
        {
            Type envelope = typeof(ResponseEnvelope<>).MakeGenericType(SdkOperations.Find("ControllerService", "getState")!
                .ContractMethod.ReturnType.GetGenericArguments()[0]);

            XmlSerializer first = SoapCodec.SerializerFor(envelope);

            Assert.That(SoapCodec.SerializerFor(envelope), Is.SameAs(first));
        }

        [Test]
        public void EveryGeneratedDate_IsShapeBuiltAsTheFixedInstant()
        {
            Type[] dates = GeneratedTypesNamed("WSDate");
            Assert.That(dates, Has.Length.EqualTo(9), "one WSDate per SOAP namespace that carries a date");

            foreach (Type date in dates)
            {
                Assert.That(ShapeDefaults.TryBuild(date, out object? value, out string? refusal), Is.True, refusal);
                Assert.That(
                    new[] { "year", "monthWithJanuaryAsOne", "day", "hours", "minutes", "seconds" }.Select(name => date.GetProperty(name)!.GetValue(value)),
                    Is.EqualTo(new object[] { 2000, 1, 1, 0, 0, 0 }), date.FullName);
            }
        }

        [Test]
        public void EveryGeneratedResourceValue_IsShapeBuiltAsBooleanFalse()
        {
            Type[] values = GeneratedTypesNamed("WSResourceValue");
            Assert.That(values, Has.Length.EqualTo(2));

            foreach (Type valueType in values)
            {
                Assert.That(ShapeDefaults.TryBuild(valueType, out object? value, out string? refusal), Is.True, refusal);
                Assert.That(value!.GetType().Name, Is.EqualTo("WSBooleanValue"), valueType.FullName);
                Assert.That(value.GetType().GetProperty("value")!.GetValue(value), Is.EqualTo(false));
            }
        }

        [Test]
        public void ShapeDefaults_FillEveryMemberWithItsRule()
        {
            Assert.That(ShapeDefaults.TryBuild(typeof(SeededShape), out object? built, out string? refusal), Is.True, refusal);
            var shape = (SeededShape)built!;

            Assert.Multiple(() =>
            {
                Assert.That(shape.Text, Is.EqualTo("stub"));
                Assert.That(shape.Flag, Is.True);
                Assert.That(shape.Count, Is.Zero);
                Assert.That(shape.Ratio, Is.Zero);
                Assert.That(shape.Optional, Is.EqualTo(true), "a nullable gets the value its type would, never null");
                Assert.That(shape.Kind, Is.EqualTo(SeededKind.Second), "the first DECLARED member, not the lowest value");
                Assert.That(shape.When, Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
                Assert.That(shape.Bytes, Is.Empty);
                Assert.That(shape.Items, Is.Empty);
                Assert.That(shape.Inner?.Text, Is.EqualTo("stub"));
            });
        }

        [Test]
        public void ShapeDefaults_HaveNoDepthCap()
        {
            Assert.That(ShapeDefaults.TryBuild(typeof(SeededDepth0), out object? built, out _), Is.True);

            Assert.That(((SeededDepth0)built!).Next.Next.Next.Next.Next.Next.Leaf, Is.EqualTo("stub"));
        }

        [Test]
        public void ATypeThatWouldContainItself_IsRefused()
        {
            Assert.That(ShapeDefaults.TryBuild(typeof(SeededSelfContaining), out _, out string? refusal), Is.False);
            Assert.That(refusal, Does.Contain("would contain itself"));
        }

        [Test]
        public void ATypeNamingSubtypes_WithNoFactory_IsRefused()
        {
            Assert.That(ShapeDefaults.TryBuild(typeof(SeededPolymorphic), out _, out string? refusal), Is.False);
            Assert.That(refusal, Does.Contain("names subtypes"));
        }

        [Test]
        public void ATypeWithNoParameterlessConstructor_IsRefused()
        {
            Assert.That(ShapeDefaults.TryBuild(typeof(SeededNoDefaultConstructor), out _, out string? refusal), Is.False);
            Assert.That(refusal, Does.Contain("no public parameterless constructor"));
        }

        private static Type[] GeneratedTypesNamed(string name) =>
            typeof(IIHCApiService).Assembly.GetTypes()
                .Where(type => type.Name == name && type.Namespace?.StartsWith("Ihc.Soap.", StringComparison.Ordinal) == true)
                .ToArray();

    }

    internal enum SeededKind
    {
        Second = 2,
        First = 1,
    }

    internal sealed class SeededShape
    {
        public string Text { get; set; } = string.Empty;
        public bool Flag { get; set; }
        public int Count { get; set; } = 7;
        public double Ratio { get; set; } = 1.5;
        public bool? Optional { get; set; }
        public SeededKind Kind { get; set; } = SeededKind.First;
        public DateTime When { get; set; }
        public byte[]? Bytes { get; set; }
        public SeededShape[]? Items { get; set; }
        public SeededInner? Inner { get; set; }
    }

    internal sealed class SeededInner
    {
        public string Text { get; set; } = string.Empty;
    }

    internal sealed class SeededDepth0 { public SeededDepth1 Next = null!; }
    internal sealed class SeededDepth1 { public SeededDepth2 Next = null!; }
    internal sealed class SeededDepth2 { public SeededDepth3 Next = null!; }
    internal sealed class SeededDepth3 { public SeededDepth4 Next = null!; }
    internal sealed class SeededDepth4 { public SeededDepth5 Next = null!; }
    internal sealed class SeededDepth5 { public SeededDepth6 Next = null!; }
    internal sealed class SeededDepth6 { public string Leaf = string.Empty; }

    internal sealed class SeededSelfContaining
    {
        public SeededSelfContaining? Next { get; set; }
    }

    [XmlInclude(typeof(SeededPolymorphicChild))]
    internal class SeededPolymorphic
    {
    }

    internal sealed class SeededPolymorphicChild : SeededPolymorphic
    {
    }

    internal sealed class SeededNoDefaultConstructor(int value)
    {
        public int Value { get; } = value;
    }
}
