using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Every public SDK wrapper, called against one default <see cref="ControllerStub"/>, so every mapper runs over
    /// the stub's default answers. The wrappers are read from the SDK's public service interfaces, never from the
    /// stub's operation table, so a wrapper the table has no entry for is still visited, and fails.
    /// </summary>
    /// <remarks>
    /// This proves that every operation maps, not that it succeeds plausibly: a shape default the wrapper reads as
    /// <c>false</c> or <c>0</c> passes here. What it does catch is a default the mapper cannot read, as an exception
    /// or as a date the SDK had to substitute.
    /// </remarks>
    [TestFixture]
    public class PublicWrapperCoverageTests
    {
        /// <summary>Changed deliberately when the SDK gains or loses a public service method.</summary>
        private const int WrapperCount = 167;

        /// <summary>Long enough for a change stream to subscribe and wait once; a stream is read until then.</summary>
        private static readonly TimeSpan StreamReadFor = TimeSpan.FromMilliseconds(300);

        public static IEnumerable<TestCaseData> EveryWrapper() =>
            Wrappers().Select(wrapper => new TestCaseData(wrapper.Service, wrapper.Method)
                .SetArgDisplayNames($"{wrapper.Service.Name}.{wrapper.Method.Name}({wrapper.Method.GetParameters().Length})"));

        [Test]
        public void TheWrappers_AreReadFromTheSdk_AndCounted()
        {
            Assert.That(Wrappers().Count(), Is.EqualTo(WrapperCount));
        }

        [TestCaseSource(nameof(EveryWrapper))]
        public async Task EveryWrapper_MeetsItsRole_AgainstTheDefaultStub(Type service, MethodInfo method)
        {
            SdkMethodEntry entry = EntryOf(service, method);
            using var capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);
            using var sdk = StubbedSdk.Start();

            Exception? raised = await Call(sdk, service, method);

            switch (entry.Role)
            {
                case SdkMethodRole.Sent:
                    Assert.That(raised, Is.Null, $"{service.Name}.{method.Name} raised over the stub's defaults");
                    Assert.That(SentActions(sdk.Stub, method), Is.EqualTo(entry.WireActions));
                    break;
                case SdkMethodRole.Local:
                    Assert.That(raised, Is.Null);
                    Assert.That(sdk.Stub.Journal.All, Is.Empty, "a local method sends nothing");
                    break;
                case SdkMethodRole.Gated:
                    Assert.That(raised, Is.InstanceOf<InvalidOperationException>(), "the gate refuses");
                    Assert.That(sdk.Stub.Journal.All, Is.Empty, "the gate refuses before it sends");
                    break;
            }
            Assert.That(DateSubstitutions(capture), Is.Empty, "the default answers carry only dates the SDK can read");
        }

        [TestCaseSource(nameof(EveryWrapper))]
        public async Task UnderFailOn_EveryWrapper_MeetsItsRole(Type service, MethodInfo method)
        {
            SdkMethodEntry entry = EntryOf(service, method);
            foreach (StubFault fault in Enum.GetValues<StubFault>())
            {
                using var sdk = StubbedSdk.Start();
                if (entry.Role == SdkMethodRole.Local)
                {
                    Assert.That(() => FailOn(sdk.Stub, service, method.Name, fault),
                        Throws.ArgumentException, "a failure injected into a local method could never happen");
                    continue;
                }

                FailOn(sdk.Stub, service, FailedMethod(service, entry), fault);
                Exception? raised = await Call(sdk, service, method);

                if (entry.Role == SdkMethodRole.Gated)
                {
                    Assert.That(raised, Is.InstanceOf<InvalidOperationException>(), $"{fault}: the gate still refuses first");
                    Assert.That(sdk.Stub.Journal.All, Is.Empty, $"{fault}: the gate refuses before it sends");
                }
                else if (fault == StubFault.SoapFault)
                {
                    // A SOAP fault is the controller's own refusal, raised with its code and text.
                    Assert.That(raised, Is.InstanceOf<ErrorWithCodeException>()
                        .And.Matches<ErrorWithCodeException>(coded => coded.ErrorCode == Errors.CONTROLLER_SOAP_FAULT),
                        $"{fault} raises the production error");
                }
                else if (service == typeof(IConfigurationService) && method.Name == nameof(IConfigurationService.SetNetworkSettings))
                {
                    // A lost connection is this write's captured outcome, raised as unconfirmed with the transport's error.
                    Assert.That(raised, Is.InstanceOf<ErrorWithCodeException>()
                        .And.Matches<ErrorWithCodeException>(coded => coded.ErrorCode == Errors.CONTROLLER_WRITE_UNCONFIRMED)
                        .And.InnerException.InstanceOf<HttpRequestException>(), $"{fault} raises the production error");
                }
                else
                {
                    Assert.That(raised, Is.InstanceOf<HttpRequestException>(), $"{fault} raises the production error");
                }
            }
        }

        /// <summary>
        /// Each method of each public service interface, with the members a service inherits from an interface that
        /// is not itself a service, such as disposal and the session's cookie handler.
        /// </summary>
        private static IEnumerable<(Type Service, MethodInfo Method)> Wrappers() =>
            typeof(IIHCApiService).Assembly.GetExportedTypes()
                .Where(type => type.IsInterface && type != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(type))
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .SelectMany(service => service.GetInterfaces()
                    .Where(inherited => !typeof(IIHCApiService).IsAssignableFrom(inherited))
                    .Prepend(service)
                    .SelectMany(declaring => declaring.GetMethods())
                    .Where(method => !method.IsSpecialName)
                    .Select(method => (service, method)));

        private static SdkMethodEntry EntryOf(Type service, MethodInfo method) =>
            SdkOperations.Entry(service, method.Name)
            ?? throw new AssertionException($"{service.Name}.{method.Name} has no entry in the stub's operation table");

        /// <summary>
        /// The method that owns the row the wrapper's failure is injected at: the wrapper itself when it owns one,
        /// otherwise the owner of its first action, as a change stream owns none of the actions it sends.
        /// </summary>
        private static string FailedMethod(Type service, SdkMethodEntry entry)
        {
            if (entry.Role != SdkMethodRole.Sent || SdkOperations.Rows.Any(row => row.OwnerService == service && row.OwnerMethod == entry.Method))
            {
                return entry.Method;
            }
            SdkOperation first = SdkOperations.Rows.Single(row => row.OwnerService == service && row.WireAction == entry.WireActions[0]);
            return first.OwnerMethod!;
        }

        private static void FailOn(ControllerStub stub, Type service, string method, StubFault fault)
        {
            try
            {
                typeof(ControllerStub).GetMethod(nameof(ControllerStub.FailOn))!.MakeGenericMethod(service).Invoke(stub, [method, fault]);
            }
            catch (TargetInvocationException invocation) when (invocation.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(invocation.InnerException);
            }
        }

        /// <summary>
        /// The wire actions the stub received, in order. A change stream waits as often as it is read, so for one its
        /// repeated wait counts once: what is pinned is that it subscribes, waits and unsubscribes.
        /// </summary>
        private static IEnumerable<string> SentActions(ControllerStub stub, MethodInfo method)
        {
            IEnumerable<string> actions = stub.Journal.All.Select(call => call.SoapAction);
            return IsStream(method) ? actions.Where((action, index) => index == 0 || action != stub.Journal.All[index - 1].SoapAction) : actions;
        }

        private static IEnumerable<string> DateSubstitutions(TelemetryCapture capture) =>
            capture.Spans
                .SelectMany(span => span.Events.Select(@event => (span.OperationName, Event: @event)))
                .Where(item => item.Event.Name == "Warning")
                .Select(item => (item.OperationName, Type: item.Event.Tags.FirstOrDefault(tag => tag.Key == "type").Value as string))
                .Where(item => item.Type is "UnreadableWireDate" or "AbsentWireDate")
                .Select(item => $"{item.OperationName}: {item.Type}");

        /// <summary>Calls the wrapper with the shared builder's arguments and answers what it raised, if anything.</summary>
        private static async Task<Exception?> Call(StubbedSdk sdk, Type service, MethodInfo method)
        {
            using var reading = new CancellationTokenSource(StreamReadFor);
            object?[] arguments =
            [
                .. method.GetParameters().Select(parameter =>
                    parameter.ParameterType == typeof(CancellationToken) ? reading.Token : SdkArguments.For(parameter.ParameterType)),
            ];
            try
            {
                object? returned;
                try
                {
                    returned = method.Invoke(sdk.Service(service), arguments);
                }
                catch (TargetInvocationException invocation) when (invocation.InnerException is not null)
                {
                    ExceptionDispatchInfo.Throw(invocation.InnerException);
                    throw;
                }
                switch (returned)
                {
                    case Task task:
                        await task;
                        break;
                    case ValueTask valueTask:
                        await valueTask;
                        break;
                    case IAsyncEnumerable<ResourceValue> stream:
                        await ReadOnce(stream, reading.Token);
                        break;
                }
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        /// <summary>Reads a change stream until its first change or until reading stops, then ends it, which is
        /// when it unsubscribes. Stopping is how a reader ends a stream, so it is no failure.</summary>
        private static async Task ReadOnce(IAsyncEnumerable<ResourceValue> stream, CancellationToken reading)
        {
            try
            {
                await foreach (ResourceValue _ in stream.WithCancellation(reading))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (reading.IsCancellationRequested)
            {
            }
        }

        private static bool IsStream(MethodInfo method) =>
            method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
    }
}
