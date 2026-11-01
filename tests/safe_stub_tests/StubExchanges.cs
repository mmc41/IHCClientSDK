using System;
using Ihc.Envelope;
using Ihc.Stubbing;

namespace Ihc.Tests
{
    /// <summary>Exchanges for a stub, written the way the SDK writes them, without an SDK wrapper in between.</summary>
    internal static class StubExchanges
    {
        /// <summary>A request to <paramref name="service"/>'s <paramref name="wireAction"/> carrying a shape-built
        /// body, written by the SDK's own serializer.</summary>
        internal static ControllerStubRequest For(string service, string wireAction, string? cookie = null)
        {
            SdkOperation operation = SdkOperations.Find(service, wireAction)
                ?? throw new ArgumentException($"no operation {service}.{wireAction}", nameof(wireAction));
            Type requestType = SoapDispatcher.RequestTypeOf(operation);
            if (!ShapeDefaults.TryBuild(requestType, out object? body, out string? refusal))
            {
                throw new InvalidOperationException(refusal);
            }
            return new ControllerStubRequest(service, wireAction, SdkWritesRequest(requestType, body), cookie);
        }

        /// <summary>The request envelope the SDK itself writes around <paramref name="body"/>.</summary>
        internal static string SdkWritesRequest(Type bodyType, object body)
        {
            Type closed = typeof(RequestEnvelope<>).MakeGenericType(bodyType);
            return (string)typeof(Serialization).GetMethod(nameof(Serialization.SerializeXml))!.MakeGenericMethod(closed)
                .Invoke(null, [Activator.CreateInstance(closed, body)])!;
        }

        /// <summary>The body the SDK's own serializer reads from a response envelope.</summary>
        internal static object? SdkReadsResponse(Type bodyType, string xml)
        {
            Type closed = typeof(ResponseEnvelope<>).MakeGenericType(bodyType);
            object? envelope = typeof(Serialization).GetMethod(nameof(Serialization.DeserializeXml))!.MakeGenericMethod(closed)
                .Invoke(null, [xml]);
            return envelope is null ? null : closed.GetField(nameof(ResponseEnvelope<object>.Body))!.GetValue(envelope);
        }
    }
}
