using System.Reflection;
using System.ServiceModel;

namespace Ihc.Stubbing
{
    /// <summary>How the stub answers a wire operation. Declared per row, never inferred from whether an answer
    /// method exists, so a count of typed rows is a fact the table states rather than one it derives.</summary>
    public enum StubAnswerKind
    {
        /// <summary>A plausible response built from the generated response type.</summary>
        Shape,

        /// <summary>A hand-written answer that reads or writes a typed value.</summary>
        Typed,

        /// <summary>A hand-written answer that also reads the request, against the same state a typed one uses.</summary>
        RequestAware,
    }

    /// <summary>What a public SDK method does on the wire, which is what each completeness check expects of it.</summary>
    public enum SdkMethodRole
    {
        /// <summary>It sends its wire actions, which must appear in the journal in order.</summary>
        Sent,

        /// <summary>It sends nothing, so it is neither expected in the journal nor counted as missing.</summary>
        Local,

        /// <summary>It refuses before it sends while dangerous internal test calls are disallowed, which no stub
        /// test allows, so only the dispatcher's wire-completeness check reaches its operation.</summary>
        Gated,
    }

    /// <summary>
    /// One wire operation: what the dispatcher finds by service and wire action. The owner is the SDK method that
    /// sends it as its own or primary action, which names it in the journal and selects it for an injected
    /// failure; an operation no public method sends has none and is <see cref="IsUnwrapped"/>.
    /// </summary>
    public sealed record SdkOperation(
        string Service,
        string WireAction,
        MethodInfo ContractMethod,
        StubAnswerKind AnswerKind,
        Type? OwnerService,
        string? OwnerMethod)
    {
        /// <summary>True when no public SDK method sends this operation.</summary>
        public bool IsUnwrapped => OwnerMethod is null;
    }

    /// <summary>
    /// One public SDK method, by its interface and name; overloads share an entry, since they share a name and the
    /// actions they send. <see cref="WireActions"/> lists, in order, the actions a <see cref="SdkMethodRole.Sent"/>
    /// method sends, each naming a row of its service; it is empty for the other roles.
    /// </summary>
    public sealed record SdkMethodEntry(Type SdkService, string Method, SdkMethodRole Role, IReadOnlyList<string> WireActions);

    /// <summary>
    /// The one operation descriptor table: a row per generated wire operation, and an entry per public SDK method.
    /// Dispatch, injected failures, journal identity and both completeness checks read it, and nothing keeps a
    /// second copy.
    /// </summary>
    /// <remarks>
    /// <para><b>Rows are wire operations</b>, because the dispatcher's key is the wire operation: one wire action
    /// can be sent by several methods (<c>getState</c>, from <c>GetControllerState</c> and inside
    /// <c>StoreProject</c>), and one is sent by none.</para>
    /// <para><b>Owners follow a convention</b>: an SDK method owns the row of its own service whose wire action has
    /// its name, compared without case. The exceptions below are explicit, as are the roles that are not
    /// <see cref="SdkMethodRole.Sent"/>, so the convention never has to guess.</para>
    /// <para><b>Ambiguity is refused when the table is built</b>: a service and wire action matching two contract
    /// methods, a row with two owners, and a sent method matching no row each throw, naming every case at once.</para>
    /// </remarks>
    public static class SdkOperations
    {
        /// <summary>The wire service each public SDK service interface posts to. An interface with no operations
        /// maps to null.</summary>
        private static readonly Dictionary<Type, string?> WireServiceOf = new()
        {
            [typeof(IAirlinkManagementService)] = "AirlinkManagementService",
            [typeof(IAuthenticationService)] = "AuthenticationService",
            [typeof(IConfigurationService)] = "ConfigurationService",
            [typeof(IControllerService)] = "ControllerService",
            [typeof(IInternalTestService)] = "TestIhcService",
            [typeof(ILedDimmerManagementService)] = "LEDDimmerManagementService",
            [typeof(IMessageControlLogService)] = "MessageControlLogService",
            [typeof(IModuleService)] = "ModuleService",
            [typeof(INotificationManagerService)] = "NotificationManagerService",
            [typeof(IOpenAPIService)] = "OpenAPIService",
            [typeof(IProductionTestService)] = null,
            [typeof(IResourceInteractionService)] = "ResourceInteractionService",
            [typeof(ISmsModemService)] = "SMSModemService",
            [typeof(ITimeManagerService)] = "TimeManagerService",
            [typeof(IUserManagerService)] = "UserManagerService",
        };

        /// <summary>Methods that send nothing: a field read, the session's cookie handler, or disposal.</summary>
        private static readonly (Type Service, string Method)[] LocalMethods =
        [
            (typeof(IAuthenticationService), nameof(IAuthenticationService.IsAuthenticated)),
            (typeof(IAuthenticationService), nameof(IAuthenticationService.GetCookieHandler)),
            (typeof(IAuthenticationService), nameof(IAuthenticationService.Dispose)),
            (typeof(IAuthenticationService), nameof(IAuthenticationService.DisposeAsync)),
            (typeof(IOpenAPIService), nameof(IOpenAPIService.GetCookieHandler)),
        ];

        /// <summary>The methods that refuse before they send while dangerous internal test calls are disallowed.</summary>
        private static readonly (Type Service, string Method)[] GatedMethods =
        [
            (typeof(IInternalTestService), nameof(IInternalTestService.ProductionTestPassed)),
            (typeof(IInternalTestService), nameof(IInternalTestService.BurnIO)),
            (typeof(IInternalTestService), nameof(IInternalTestService.TestSdCard)),
            (typeof(IInternalTestService), nameof(IInternalTestService.TestIOBoard)),
            (typeof(IInternalTestService), nameof(IInternalTestService.SendRS485Data)),
            (typeof(IInternalTestService), nameof(IInternalTestService.ReadRS485Data)),
        ];

        /// <summary>
        /// The methods the name convention does not describe, each with the actions it sends in order and the
        /// primary action it owns. A method whose actions all belong to other methods, such as a change stream
        /// built from a subscribe and a wait, owns none.
        /// </summary>
        /// <remarks>
        /// <c>TestSendMessage</c> is here although a row carries its name: it sends <c>testSendMessage1</c>, and the
        /// <c>testSendMessage</c> overload is sent by no public method, which leaves it unwrapped.
        /// </remarks>
        private static readonly (Type Service, string Method, string? Primary, string[] WireActions)[] Exceptions =
        [
            (typeof(IControllerService), nameof(IControllerService.GetControllerState), "getState", ["getState"]),
            (typeof(IControllerService), nameof(IControllerService.GetProject), "getIHCProject", ["getIHCProject"]),
            (typeof(IControllerService), nameof(IControllerService.StoreProject), "storeIHCProject",
            [
                "getState", "isSDCardReady", "isIHCProjectAvailable", "enterProjectChangeMode",
                "waitForControllerStateChange", "storeIHCProject", "waitForControllerStateChange",
                "exitProjectChangeMode", "waitForControllerStateChange",
            ]),
            (typeof(IConfigurationService), nameof(IConfigurationService.TestSendMessage), "testSendMessage1", ["testSendMessage1"]),
            // A change stream subscribes, waits as often as it is read, and unsubscribes when it ends.
            (typeof(IResourceInteractionService), nameof(IResourceInteractionService.GetResourceValueChanges), null,
                ["enableRuntimeValueNotifications", "waitForResourceValueChanges", "disableRuntimeValueNotifactions"]),
            (typeof(IOpenAPIService), nameof(IOpenAPIService.GetResourceValueChanges), null,
                ["enableSubscription", "waitForEvents", "disableSubscription"]),
        ];

        /// <summary>
        /// The operations answered by hand rather than from their shape, each with its kind. Declared here rather
        /// than read from which answer methods exist, so a count of typed rows compares the table with a pinned
        /// number instead of with itself.
        /// </summary>
        private static readonly (string Service, string WireAction, StubAnswerKind Kind)[] AnswerKinds =
        [
            ("AuthenticationService", "authenticate", StubAnswerKind.Typed),
            ("AuthenticationService", "disconnect", StubAnswerKind.Typed),
            ("AuthenticationService", "ping", StubAnswerKind.Typed),
            ("ControllerService", "getState", StubAnswerKind.Typed),
            ("ControllerService", "waitForControllerStateChange", StubAnswerKind.Typed),
            ("ControllerService", "isSDCardReady", StubAnswerKind.Typed),
            ("ControllerService", "isIHCProjectAvailable", StubAnswerKind.Typed),
            ("ControllerService", "getProjectInfo", StubAnswerKind.Typed),
            ("ControllerService", "enterProjectChangeMode", StubAnswerKind.Typed),
            ("ControllerService", "exitProjectChangeMode", StubAnswerKind.Typed),
            ("ControllerService", "storeIHCProject", StubAnswerKind.Typed),
            ("ControllerService", "getIHCProject", StubAnswerKind.Typed),
            ("ControllerService", "getIHCProjectSegmentationSize", StubAnswerKind.Typed),
            ("ControllerService", "getIHCProjectNumberOfSegments", StubAnswerKind.Typed),
            ("ControllerService", "getIHCProjectSegment", StubAnswerKind.Typed),
            ("ControllerService", "storeIHCProjectSegment", StubAnswerKind.Typed),
            ("ControllerService", "getSdCardInfo", StubAnswerKind.Typed),
            ("ControllerService", "getS0MeterValue", StubAnswerKind.Typed),
            ("ControllerService", "resetS0Values", StubAnswerKind.Typed),
            ("ControllerService", "setS0Consumption", StubAnswerKind.Typed),
            ("ControllerService", "restore", StubAnswerKind.Typed),
            ("ConfigurationService", "getSystemInfo", StubAnswerKind.Typed),
            ("ConfigurationService", "getUserLog", StubAnswerKind.Typed),
            ("ConfigurationService", "clearUserLog", StubAnswerKind.Typed),
            ("ConfigurationService", "getWLanInterface", StubAnswerKind.Typed),
            ("ConfigurationService", "getWLanScan", StubAnswerKind.Typed),
            ("ConfigurationService", "testSendMessage1", StubAnswerKind.Typed),
            ("ConfigurationService", "testSettingsNow", StubAnswerKind.Typed),
            ("ConfigurationService", "delayedReboot", StubAnswerKind.Typed),
            ("ConfigurationService", "getNetworkSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "setNetworkSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "getDNSServers", StubAnswerKind.Typed),
            ("ConfigurationService", "setDNSServers", StubAnswerKind.Typed),
            ("ConfigurationService", "getWLanSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "setWLanSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "getSMTPSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "setSMTPSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "getEmailControlSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "setEmailControlSettings", StubAnswerKind.Typed),
            ("ConfigurationService", "getWebAccessControl", StubAnswerKind.Typed),
            ("ConfigurationService", "setWebAccessControl", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getRuntimeValues", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getInitialValues", StubAnswerKind.Typed),
            ("ResourceInteractionService", "setResourceValues", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getAllDatalineInputs", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getLoggedData", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getSceneGroupResourceIdAndPositions", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getScenePositionsForSceneValueResource", StubAnswerKind.Typed),
            ("ResourceInteractionService", "enableRuntimeValueNotifications", StubAnswerKind.Typed),
            ("ResourceInteractionService", "disableRuntimeValueNotifactions", StubAnswerKind.Typed),
            ("ResourceInteractionService", "enableInitialValueNotifications", StubAnswerKind.Typed),
            ("ResourceInteractionService", "disableInitialValueNotifactions", StubAnswerKind.Typed),
            ("ResourceInteractionService", "waitForResourceValueChanges", StubAnswerKind.Typed),
            ("ResourceInteractionService", "getRuntimeValue", StubAnswerKind.RequestAware),
            ("ResourceInteractionService", "getInitialValue", StubAnswerKind.RequestAware),
            ("ResourceInteractionService", "setResourceValue", StubAnswerKind.RequestAware),
            ("OpenAPIService", "getValues", StubAnswerKind.RequestAware),
            ("OpenAPIService", "setValues", StubAnswerKind.RequestAware),
            ("OpenAPIService", "authenticate", StubAnswerKind.RequestAware),
            ("OpenAPIService", "isIHCProjectAvailable", StubAnswerKind.RequestAware),
            ("OpenAPIService", "getProjectInfo", StubAnswerKind.RequestAware),
            ("OpenAPIService", "getIHCProjectSegmentationSize", StubAnswerKind.RequestAware),
            ("OpenAPIService", "getIHCProjectNumberOfSegments", StubAnswerKind.RequestAware),
            ("OpenAPIService", "getIHCProjectSegment", StubAnswerKind.RequestAware),
            ("OpenAPIService", "waitForEvents", StubAnswerKind.Typed),
            ("UserManagerService", "getUsers", StubAnswerKind.Typed),
            ("UserManagerService", "addUser", StubAnswerKind.Typed),
            ("UserManagerService", "updateUser", StubAnswerKind.Typed),
            ("UserManagerService", "removeUser", StubAnswerKind.Typed),
            ("TimeManagerService", "getCurrentLocalTime", StubAnswerKind.Typed),
            ("TimeManagerService", "getSettings", StubAnswerKind.Typed),
            ("TimeManagerService", "setSettings", StubAnswerKind.Typed),
            ("TimeManagerService", "getTimeFromServer", StubAnswerKind.Typed),
            ("SMSModemService", "getSMSModemInfo", StubAnswerKind.Typed),
            ("SMSModemService", "getSMSModemSettings", StubAnswerKind.Typed),
            ("SMSModemService", "setSMSModemSettings", StubAnswerKind.Typed),
            ("SMSModemService", "getSMSModemStatus", StubAnswerKind.Typed),
            ("SMSModemService", "resetSMSModem", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "enterConfiguration", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "exitConfiguration", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "waitForDeviceDetected", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "assignID", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "getDeviceCount", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "getDeviceList", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "getFirmwareUpgradeProgress", StubAnswerKind.Typed),
            ("LEDDimmerManagementService", "startFirmwareUpgrade", StubAnswerKind.Typed),
            ("AirlinkManagementService", "enterRFConfiguration", StubAnswerKind.Typed),
            ("AirlinkManagementService", "exitRFConfiguration", StubAnswerKind.Typed),
            ("AirlinkManagementService", "enterRFTest", StubAnswerKind.Typed),
            ("AirlinkManagementService", "exitRFTest", StubAnswerKind.Typed),
            ("AirlinkManagementService", "waitForDeviceDetected", StubAnswerKind.Typed),
            ("AirlinkManagementService", "waitForDeviceTestResult", StubAnswerKind.Typed),
            ("AirlinkManagementService", "getDetectedDeviceList", StubAnswerKind.Typed),
            ("AirlinkManagementService", "getDevicesRunningOutOfBattery", StubAnswerKind.Typed),
            ("AirlinkManagementService", "testRFActuatorWithSerialNumber", StubAnswerKind.Typed),
        ];

        private static readonly Lazy<Table> Built = new(Build);

        /// <summary>One row per generated wire operation, ordered by service and wire action.</summary>
        public static IReadOnlyList<SdkOperation> Rows => Built.Value.Rows;

        /// <summary>One entry per public SDK method, ordered by service and method name.</summary>
        public static IReadOnlyList<SdkMethodEntry> Entries => Built.Value.Entries;

        /// <summary>The row the dispatcher answers for <paramref name="service"/> and <paramref name="wireAction"/>,
        /// or null when there is none.</summary>
        public static SdkOperation? Find(string service, string wireAction) =>
            Built.Value.ByKey.GetValueOrDefault((service, wireAction));

        /// <summary>The entry for <paramref name="method"/> on <paramref name="sdkService"/>, or null when the
        /// interface declares no such method.</summary>
        public static SdkMethodEntry? Entry(Type sdkService, string method) =>
            Built.Value.Entries.FirstOrDefault(entry => entry.SdkService == sdkService && entry.Method == method);

        /// <summary>
        /// The row <paramref name="sdkMethod"/> of <paramref name="sdkService"/> owns, for a stub member that names an
        /// operation by the SDK method sending it. A name that could never select a row is refused, saying what to
        /// name instead, because the member would otherwise wait for an exchange that never comes: a failure that is
        /// never injected, or a journal lookup whose empty answer also passes an assertion that nothing was sent.
        /// </summary>
        /// <param name="sdkService">The service the member was given, which must be the SDK's interface.</param>
        /// <param name="sdkMethod">The SDK method the member was given.</param>
        /// <param name="paramName">The member's parameter, which the refusal names.</param>
        /// <param name="sendsNothing">What follows for the member when the method sends nothing.</param>
        /// <param name="ownsNothing">What the member's caller does instead when the method's actions all belong to
        /// other methods; the actions follow it.</param>
        /// <exception cref="ArgumentException">The service is a class, or the method is not declared, sends nothing,
        /// or owns no wire operation.</exception>
        internal static SdkOperation OwnedRow(Type sdkService, string sdkMethod, string paramName, string sendsNothing, string ownsNothing)
        {
            if (!sdkService.IsInterface)
            {
                Type? named = WireServiceOf.Keys.FirstOrDefault(candidate => candidate.IsAssignableFrom(sdkService));
                throw new ArgumentException(named is null
                    ? $"{sdkService.Name} is no SDK service interface, which is what every stub member names"
                    : $"{sdkService.Name} is a service class; name its interface, {named.Name}, which is what every stub member names",
                    paramName);
            }
            SdkMethodEntry entry = Entry(sdkService, sdkMethod)
                ?? throw new ArgumentException($"{sdkService.Name} declares no method {sdkMethod}", paramName);
            if (entry.Role == SdkMethodRole.Local)
            {
                throw new ArgumentException($"{sdkService.Name}.{sdkMethod} sends nothing, so {sendsNothing}", paramName);
            }
            return Rows.FirstOrDefault(row => row.OwnerService == sdkService && row.OwnerMethod == sdkMethod)
                ?? throw new ArgumentException(
                    $"{sdkService.Name}.{sdkMethod} owns no wire operation; {ownsNothing}: " + string.Join(", ", entry.WireActions),
                    paramName);
        }

        /// <summary>
        /// Why no row is keyed by <paramref name="service"/> and <paramref name="wireAction"/>, naming the wire name
        /// meant when the pair is the SDK's name for one: a service interface or class, an SDK method, or a wire
        /// action in another case. The wire names differ from the SDK's in exactly the places a test author would
        /// write <c>nameof</c>.
        /// </summary>
        internal static string NoWireOperation(string service, string wireAction)
        {
            string head = $"{service}.{wireAction} names no wire operation, so nothing sent could ever match it; ";
            if (!Rows.Any(row => row.Service == service))
            {
                string? posted = WireServiceOf
                    .Where(pair => pair.Key.Name == service || pair.Key.Name == "I" + service)
                    .Select(pair => pair.Value)
                    .FirstOrDefault();
                if (posted is not null)
                {
                    return head + $"{service} is the SDK's name for the service, and the wire name it posts to is {posted}";
                }
                string? casedService = WireServiceOf.Values
                    .FirstOrDefault(name => string.Equals(name, service, StringComparison.OrdinalIgnoreCase));
                return head + (casedService is null
                    ? $"{service} is no wire service. The wire services are "
                      + string.Join(", ", Rows.Select(row => row.Service).Distinct())
                    : $"wire services are compared with their case, and this one is {casedService}");
            }

            SdkMethodEntry? method = Entries.FirstOrDefault(entry =>
                WireServiceOf.GetValueOrDefault(entry.SdkService) == service && entry.Method == wireAction);
            if (method is not null)
            {
                SdkOperation? owned = Rows.FirstOrDefault(row => row.OwnerService == method.SdkService && row.OwnerMethod == method.Method);
                return head + (owned is not null
                    ? $"{wireAction} is the SDK method, and the wire action it sends is {owned.WireAction}"
                    : $"{wireAction} is the SDK method, and the wire actions it sends are " + string.Join(", ", method.WireActions));
            }

            string? cased = Rows
                .Where(row => row.Service == service && string.Equals(row.WireAction, wireAction, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.WireAction)
                .FirstOrDefault();
            return head + (cased is null
                ? $"{service} has no such wire action"
                : $"wire actions are compared with their case, and {service}'s is {cased}");
        }

        internal sealed record Table(
            IReadOnlyList<SdkOperation> Rows,
            IReadOnlyList<SdkMethodEntry> Entries,
            IReadOnlyDictionary<(string Service, string WireAction), SdkOperation> ByKey);

        /// <summary>What the table is built from. The production inputs are the declarations above; a test builds
        /// from seeded inputs to show each refusal, since the real ones are built to contain none.</summary>
        internal sealed record Inputs(
            IReadOnlyCollection<Type> Contracts,
            IReadOnlyDictionary<Type, string?> WireServiceOf,
            IReadOnlyCollection<(Type Service, string Method)> LocalMethods,
            IReadOnlyCollection<(Type Service, string Method)> GatedMethods,
            IReadOnlyCollection<(Type Service, string Method, string? Primary, string[] WireActions)> Exceptions,
            IReadOnlyCollection<(string Service, string WireAction, StubAnswerKind Kind)> AnswerKinds);

        /// <summary>The generated contracts: every interface under <c>Ihc.Soap</c> that declares a service contract.</summary>
        private static Type[] GeneratedContracts() =>
            [.. typeof(IIHCApiService).Assembly.GetTypes()
                .Where(type => type.IsInterface && GeneratedTypes.IsGenerated(type)
                               && type.GetCustomAttribute<ServiceContractAttribute>() is not null)];

        private static Table Build() =>
            Build(new Inputs(GeneratedContracts(), WireServiceOf, LocalMethods, GatedMethods, Exceptions, AnswerKinds));

        internal static Table Build(Inputs inputs)
        {
            var problems = new List<string>();
            var contracts = ContractMethods(inputs.Contracts, problems);
            var entries = new List<SdkMethodEntry>();
            var owners = new Dictionary<(string Service, string WireAction), List<(Type Service, string Method)>>();

            foreach ((Type sdkService, string? wireService) in inputs.WireServiceOf.OrderBy(pair => pair.Key.Name, StringComparer.Ordinal))
            {
                foreach (string method in PublicMethodNames(sdkService))
                {
                    SdkMethodEntry entry = EntryFor(inputs, sdkService, method, wireService, contracts, problems, owners);
                    entries.Add(entry);
                }
            }

            var kinds = new Dictionary<(string Service, string WireAction), StubAnswerKind>();
            foreach (var declared in inputs.AnswerKinds)
            {
                if (!contracts.ContainsKey((declared.Service, declared.WireAction)))
                {
                    problems.Add($"{declared.Service}.{declared.WireAction} is declared {declared.Kind} but is no wire operation");
                }
                else if (!kinds.TryAdd((declared.Service, declared.WireAction), declared.Kind))
                {
                    problems.Add($"{declared.Service}.{declared.WireAction} declares its answer kind twice");
                }
            }

            var rows = contracts
                .OrderBy(pair => pair.Key.Service, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.WireAction, StringComparer.Ordinal)
                .Select(pair =>
                {
                    owners.TryGetValue(pair.Key, out var owning);
                    if (owning is { Count: > 1 })
                    {
                        problems.Add($"{pair.Key.Service}.{pair.Key.WireAction} has two owners: "
                            + string.Join(" and ", owning.Select(owner => $"{owner.Service.Name}.{owner.Method}")));
                    }
                    (Type Service, string Method)? owner = owning is { Count: 1 } ? owning[0] : null;
                    return new SdkOperation(pair.Key.Service, pair.Key.WireAction, pair.Value,
                        kinds.GetValueOrDefault(pair.Key, StubAnswerKind.Shape), owner?.Service, owner?.Method);
                })
                .ToList();

            if (problems.Count > 0)
            {
                throw new InvalidOperationException("The SDK operation table is ambiguous or incomplete: "
                    + string.Join("; ", problems));
            }

            return new Table(rows, entries, rows.ToDictionary(row => (row.Service, row.WireAction)));
        }

        private static SdkMethodEntry EntryFor(Inputs inputs, Type sdkService, string method, string? wireService,
            IReadOnlyDictionary<(string Service, string WireAction), MethodInfo> contracts, List<string> problems,
            Dictionary<(string Service, string WireAction), List<(Type Service, string Method)>> owners)
        {
            if (inputs.LocalMethods.Contains((sdkService, method)))
            {
                return new SdkMethodEntry(sdkService, method, SdkMethodRole.Local, []);
            }

            bool gated = inputs.GatedMethods.Contains((sdkService, method));
            var exception = inputs.Exceptions.FirstOrDefault(declared => declared.Service == sdkService && declared.Method == method);
            string[] actions;
            string? owned;
            if (exception.WireActions is not null)
            {
                actions = exception.WireActions;
                owned = exception.Primary;
                if (owned is not null && !actions.Contains(owned))
                {
                    problems.Add($"{sdkService.Name}.{method} owns {owned}, which is not among the actions it sends");
                }
            }
            else
            {
                string? byName = wireService is null
                    ? null
                    : contracts.Keys.Where(key => key.Service == wireService
                                                  && string.Equals(key.WireAction, method, StringComparison.OrdinalIgnoreCase))
                        .Select(key => key.WireAction)
                        .FirstOrDefault();
                if (byName is null)
                {
                    problems.Add($"{sdkService.Name}.{method} matches no wire operation of {wireService ?? "(no wire service)"}"
                        + " and is declared neither local, gated nor an exception");
                    return new SdkMethodEntry(sdkService, method, SdkMethodRole.Sent, []);
                }
                actions = [byName];
                owned = byName;
            }

            foreach (string action in actions)
            {
                if (wireService is null || !contracts.ContainsKey((wireService, action)))
                {
                    problems.Add($"{sdkService.Name}.{method} names {action}, which is no wire operation of "
                        + (wireService ?? "(no wire service)"));
                }
            }
            if (owned is not null && wireService is not null)
            {
                var key = (wireService, owned);
                if (!owners.TryGetValue(key, out var list))
                {
                    owners[key] = list = [];
                }
                list.Add((sdkService, method));
            }

            return gated
                ? new SdkMethodEntry(sdkService, method, SdkMethodRole.Gated, [])
                : new SdkMethodEntry(sdkService, method, SdkMethodRole.Sent, actions);
        }

        /// <summary>
        /// Every generated contract operation, keyed by its wire service and wire action. The wire action is the
        /// contract method's name without <c>Async</c>, which is what every SDK implementation posts; the WSDL's
        /// declared action is not the key, because two overloads can declare the same one.
        /// </summary>
        private static Dictionary<(string Service, string WireAction), MethodInfo> ContractMethods(
            IEnumerable<Type> contracts, List<string> problems)
        {
            var found = new Dictionary<(string Service, string WireAction), MethodInfo>();
            foreach (Type contract in contracts)
            {
                foreach (MethodInfo operation in contract.GetMethods()
                             .Where(method => method.GetCustomAttribute<OperationContractAttribute>() is not null))
                {
                    string wireAction = WireActionOf(operation.Name) ?? operation.Name;
                    if (!found.TryAdd((contract.Name, wireAction), operation))
                    {
                        problems.Add($"{contract.Name}.{wireAction} matches two contract methods");
                    }
                }
            }
            return found;
        }

        /// <summary>The wire action a contract or answer method named <paramref name="methodName"/> stands for: its
        /// name without <c>Async</c>. Null for a name without that suffix, which stands for no operation.</summary>
        internal static string? WireActionOf(string methodName) =>
            methodName.EndsWith("Async", StringComparison.Ordinal) ? methodName[..^"Async".Length] : null;

        /// <summary>The names of the public methods <paramref name="sdkService"/> declares or inherits, property
        /// accessors aside.</summary>
        private static IEnumerable<string> PublicMethodNames(Type sdkService) =>
            sdkService.GetMethods()
                .Concat(sdkService.GetInterfaces().SelectMany(inherited => inherited.GetMethods()))
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .Distinct()
                .Order(StringComparer.Ordinal);
    }
}
