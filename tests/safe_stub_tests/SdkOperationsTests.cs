using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The stub's operation table maps the generated wire operations and the public SDK methods completely and
    /// unambiguously.
    /// </summary>
    [TestFixture]
    public class SdkOperationsTests
    {
        /// <summary>
        /// The generated contracts hold this many wire operations. A literal, updated deliberately when the WSDL is
        /// regenerated, so an enumeration that found one contract instead of all of them cannot pass.
        /// </summary>
        private const int GeneratedWireOperations = 157;

        [Test]
        public void TheTable_HasOneRowPerGeneratedWireOperation()
        {
            Assert.That(SdkOperations.Rows, Has.Count.EqualTo(GeneratedWireOperations));
            Assert.That(SdkOperations.Rows.Select(row => (row.Service, row.WireAction)), Is.Unique);
            Assert.That(SdkOperations.Rows.Select(row => row.ContractMethod), Is.Unique);
        }

        /// <summary>
        /// The rows answered by hand, typed and request-aware together. A literal, raised deliberately as each group
        /// of typed answers lands, so the table is compared with a pinned number rather than with itself.
        /// </summary>
        [Test]
        public void TheTable_DeclaresTheTypedRows()
        {
            Assert.That(SdkOperations.Rows.Count(row => row.AnswerKind != StubAnswerKind.Shape), Is.EqualTo(95));
        }

        [Test]
        public void Building_RefusesAnAnswerKindForNoOperation_OrDeclaredTwice()
        {
            var inputs = SeedInputs(answerKinds:
            [
                ("ISeededContract", "send", StubAnswerKind.Typed),
                ("ISeededContract", "send", StubAnswerKind.RequestAware),
                ("ISeededContract", "resend", StubAnswerKind.Typed),
            ]);

            Assert.That(() => SdkOperations.Build(inputs), Throws.InvalidOperationException.With.Message
                .Contains("ISeededContract.send declares its answer kind twice")
                .And.Message.Contains("ISeededContract.resend is declared Typed but is no wire operation"));
        }

        [Test]
        public void ARowsWireAction_IsItsContractMethodWithoutAsync()
        {
            Assert.That(SdkOperations.Rows.Where(row => row.ContractMethod.Name != row.WireAction + "Async")
                    .Select(row => $"{row.Service}.{row.WireAction}"),
                Is.Empty);
        }

        /// <summary>
        /// Every public method of every public SDK service interface has an entry. The interfaces come from
        /// reflection rather than from the table, so a service or method the table does not know is still visited.
        /// </summary>
        [Test]
        public void EveryPublicSdkMethod_HasAnEntry()
        {
            Type[] services = typeof(IIHCApiService).Assembly.GetExportedTypes()
                .Where(type => type.IsInterface && type != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(type))
                .ToArray();
            Assert.That(services, Is.Not.Empty);

            var missing = services
                .SelectMany(service => service.GetMethods()
                    .Concat(service.GetInterfaces().SelectMany(inherited => inherited.GetMethods()))
                    .Where(method => !method.IsSpecialName)
                    .Select(method => (Service: service, method.Name)))
                .Distinct()
                .Where(method => SdkOperations.Entry(method.Service, method.Name) is null)
                .Select(method => $"{method.Service.Name}.{method.Name}")
                .ToList();

            Assert.That(missing, Is.Empty, "a public SDK method without an entry in the stub's operation table");
        }

        [Test]
        public void TheSixInternalTestMethods_AreGated()
        {
            var gated = SdkOperations.Entries.Where(entry => entry.Role == SdkMethodRole.Gated)
                .Select(entry => $"{entry.SdkService.Name}.{entry.Method}");

            Assert.That(gated, Is.EquivalentTo(new[]
            {
                "IInternalTestService.ProductionTestPassed", "IInternalTestService.BurnIO", "IInternalTestService.TestSdCard",
                "IInternalTestService.TestIOBoard", "IInternalTestService.SendRS485Data", "IInternalTestService.ReadRS485Data",
            }));
            Assert.That(SdkOperations.Entries.Where(entry => entry.Role == SdkMethodRole.Gated).SelectMany(entry => entry.WireActions),
                Is.Empty, "a gated method sends nothing a stub test could see");
        }

        [Test]
        public void TheLocalMethods_AreExplicitAndSendNothing()
        {
            var local = SdkOperations.Entries.Where(entry => entry.Role == SdkMethodRole.Local)
                .Select(entry => $"{entry.SdkService.Name}.{entry.Method}");

            Assert.That(local, Is.EquivalentTo(new[]
            {
                "IAuthenticationService.IsAuthenticated", "IAuthenticationService.GetCookieHandler",
                "IAuthenticationService.Dispose", "IAuthenticationService.DisposeAsync", "IOpenAPIService.GetCookieHandler",
            }));
        }

        [Test]
        public void TheTestSendMessageOverload_IsUnwrapped_AndTestSendMessage1IsOwned()
        {
            SdkOperation? overload = SdkOperations.Find("ConfigurationService", "testSendMessage");
            SdkOperation? sent = SdkOperations.Find("ConfigurationService", "testSendMessage1");

            Assert.That(overload, Is.Not.Null);
            Assert.That(SdkOperations.Rows.Where(row => row.IsUnwrapped), Is.EqualTo(new[] { overload }),
                "the testSendMessage overload is the one operation no public method sends");
            Assert.That(sent?.OwnerMethod, Is.EqualTo(nameof(IConfigurationService.TestSendMessage)));
        }

        [Test]
        public void OwnersFollowTheNameConvention_AndTheExceptions()
        {
            Assert.Multiple(() =>
            {
                AssertOwner("AuthenticationService", "authenticate", typeof(IAuthenticationService), nameof(IAuthenticationService.Authenticate));
                AssertOwner("ConfigurationService", "getSystemInfo", typeof(IConfigurationService), nameof(IConfigurationService.GetSystemInfo));
                AssertOwner("ControllerService", "getState", typeof(IControllerService), nameof(IControllerService.GetControllerState));
                AssertOwner("ControllerService", "getIHCProject", typeof(IControllerService), nameof(IControllerService.GetProject));
                AssertOwner("ControllerService", "storeIHCProject", typeof(IControllerService), nameof(IControllerService.StoreProject));
                AssertOwner("ControllerService", "waitForControllerStateChange", typeof(IControllerService),
                    nameof(IControllerService.WaitForControllerStateChange));
            });
        }

        [Test]
        public void AMultiCallMethod_ListsItsActionsInTheOrderItSendsThem()
        {
            Assert.That(SdkOperations.Entry(typeof(IControllerService), nameof(IControllerService.StoreProject))?.WireActions,
                Is.EqualTo(new[]
                {
                    "getState", "isSDCardReady", "isIHCProjectAvailable", "enterProjectChangeMode",
                    "waitForControllerStateChange", "storeIHCProject", "waitForControllerStateChange",
                    "exitProjectChangeMode", "waitForControllerStateChange",
                }));
        }

        [Test]
        public void Building_RefusesAWireActionMatchingTwoContractMethods()
        {
            var inputs = SeedInputs(contracts: [typeof(ISeededOverloadedContract)]);

            Assert.That(() => SdkOperations.Build(inputs),
                Throws.InvalidOperationException.With.Message.Contains("ISeededOverloadedContract.send matches two contract methods"));
        }

        [Test]
        public void Building_RefusesARowWithTwoOwners()
        {
            var inputs = SeedInputs(exceptions:
            [
                (typeof(ISeededSdkService), nameof(ISeededSdkService.Send), "send", ["send"]),
                (typeof(ISeededSdkService), nameof(ISeededSdkService.SendAgain), "send", ["send"]),
            ]);

            Assert.That(() => SdkOperations.Build(inputs),
                Throws.InvalidOperationException.With.Message.Contains("ISeededContract.send has two owners"));
        }

        [Test]
        public void Building_RefusesASentMethodMatchingNoRow()
        {
            var inputs = SeedInputs(unmatchedIsLocal: false);

            Assert.That(() => SdkOperations.Build(inputs),
                Throws.InvalidOperationException.With.Message.Contains("ISeededSdkService.Unmatched matches no wire operation"));
        }

        [Test]
        public void Building_RefusesAnExceptionNamingNoWireOperation_OrOwningAnActionItDoesNotSend()
        {
            var inputs = SeedInputs(exceptions:
            [
                (typeof(ISeededSdkService), nameof(ISeededSdkService.SendAgain), "send", ["resend"]),
            ]);

            Assert.That(() => SdkOperations.Build(inputs), Throws.InvalidOperationException.With.Message
                .Contains("ISeededSdkService.SendAgain owns send, which is not among the actions it sends")
                .And.Message.Contains("ISeededSdkService.SendAgain names resend, which is no wire operation of ISeededContract"));
        }

        /// <summary>The seeded inputs, without a refusal unless a test adds one: the seeded SDK service sends
        /// <c>send</c> by convention, and <c>Unmatched</c> is local.</summary>
        private static SdkOperations.Inputs SeedInputs(
            Type[]? contracts = null,
            (Type Service, string Method, string? Primary, string[] WireActions)[]? exceptions = null,
            bool unmatchedIsLocal = true,
            (string Service, string WireAction, StubAnswerKind Kind)[]? answerKinds = null)
        {
            (Type, string)[] local = unmatchedIsLocal
                ? [(typeof(ISeededSdkService), nameof(ISeededSdkService.Unmatched))]
                : [];
            exceptions ??= [(typeof(ISeededSdkService), nameof(ISeededSdkService.SendAgain), null, ["send"])];
            return new SdkOperations.Inputs(
                contracts ?? [typeof(ISeededContract)],
                new Dictionary<Type, string?> { [typeof(ISeededSdkService)] = contracts?[0].Name ?? nameof(ISeededContract) },
                local,
                [],
                exceptions,
                answerKinds ?? []);
        }

        [Test]
        public void TheSeededInputs_BuildCleanly()
        {
            SdkOperations.Table table = SdkOperations.Build(SeedInputs());

            Assert.That(table.Rows.Single().OwnerMethod, Is.EqualTo(nameof(ISeededSdkService.Send)));
        }

        private static void AssertOwner(string service, string wireAction, Type ownerService, string ownerMethod)
        {
            SdkOperation? row = SdkOperations.Find(service, wireAction);
            Assert.That(row, Is.Not.Null, $"{service}.{wireAction}");
            Assert.That((row!.OwnerService, row.OwnerMethod), Is.EqualTo((ownerService, ownerMethod)), $"{service}.{wireAction}");
        }
    }

    [ServiceContract]
    internal interface ISeededContract
    {
        [OperationContract]
        Task<string> sendAsync(string request);
    }

    [ServiceContract]
    internal interface ISeededOverloadedContract
    {
        [OperationContract]
        Task<string> sendAsync(string request);

        [OperationContract]
        Task<string> sendAsync(int request);
    }

    internal interface ISeededSdkService
    {
        Task Send();

        Task SendAgain();

        Task Unmatched();
    }
}
