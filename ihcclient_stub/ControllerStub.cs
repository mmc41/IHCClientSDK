namespace Ihc.Stubbing
{
    /// <summary>The failure <see cref="ControllerStub.FailOn{TService}"/> injects.</summary>
    public enum StubFault
    {
        /// <summary>HTTP 500 with a SOAP fault: the SDK sees an <c>ErrorWithCodeException</c> with
        /// <c>Errors.CONTROLLER_SOAP_FAULT</c>, carrying the fault's text.</summary>
        SoapFault,

        /// <summary>A transport failure, never a SOAP answer: the SDK sees an <c>HttpRequestException</c>.</summary>
        ConnectionLost,
    }

    /// <summary>An exchange the stub could not answer: its service, its wire action, and why.</summary>
    public sealed record UnmodelledCall(string Service, string SoapAction, string Reason);

    /// <summary>An exception the stub threw while answering, which is a defect in the stub rather than an answer.</summary>
    public sealed record StubDefect(string Service, string SoapAction, Exception Exception);

    /// <summary>
    /// A controller stub: register it through <see cref="ControllerStubs.Register"/>, point the SDK's settings at
    /// the endpoint the registration returns, and every operation the SDK sends is answered here.
    /// </summary>
    /// <remarks>
    /// <para><b>Precedence.</b> Per call the first that applies answers: a raw override, then an injected failure,
    /// then a typed answer, then a value built from the response type. An override or an injected failure answers
    /// before any answer runs, so it never makes the state change the operation would; the call is journalled with
    /// its outcome all the same.</para>
    /// <para><b>Failures kept apart.</b> An injected failure is deliberate and journalled. A cancellation passes
    /// through unchanged. Anything else thrown inside the stub is a defect: it is recorded in <see cref="Defects"/>
    /// and rethrown, never turned into a SOAP fault, which would make it look like an answer the stub meant to
    /// give.</para>
    /// </remarks>
    public sealed class ControllerStub : IControllerStub
    {
        /// <summary>Guards every piece of state the stub holds. Taken only with the <c>lock</c> statement, so the
        /// compiler refuses to hold it across an <c>await</c>.</summary>
        private readonly Lock _gate = new();

        private readonly List<UnmodelledCall> _unmodelled = [];
        private readonly List<StubDefect> _defects = [];
        private readonly Dictionary<(string Service, string WireAction), StubFault> _failures = [];
        private readonly Dictionary<(string Service, string WireAction), Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>>> _overrides = [];
        private readonly SoapDispatcher _dispatcher;

        /// <summary>A password every login refuses, for a test of the refusal path; every other non-blank password
        /// logs in.</summary>
        public const string IllegalPassword = "illegalpassword";

        public ControllerStub()
            : this(null)
        {
        }

        /// <summary>A stub answering with <paramref name="answers"/> in place of its own typed answers, or with its
        /// own when null.</summary>
        internal ControllerStub(IEnumerable<IWireAnswers>? answers)
        {
            Journal = new StubJournal(_gate);
            Authentication = new StubAuthentication(_gate);
            Controller = new StubController(_gate);
            Configuration = new StubConfiguration(_gate);
            Resources = new StubResources(_gate);
            UserManager = new StubUserManager(_gate);
            Time = new StubTime(_gate);
            SmsModem = new StubSmsModem(_gate);
            LedDimmers = new StubLedDimmers(_gate);
            Airlink = new StubAirlink(_gate);
            _dispatcher = new SoapDispatcher(answers ??
            [
                new AuthenticationAnswers(Authentication),
                new ControllerAnswers(Controller),
                new ConfigurationAnswers(Configuration),
                new ResourceInteractionAnswers(Resources),
                new OpenApiAnswers(Authentication, Controller, Resources),
                new UserManagerAnswers(UserManager),
                new TimeManagerAnswers(Time),
                new SmsModemAnswers(SmsModem),
                new LedDimmerManagementAnswers(LedDimmers),
                new AirlinkManagementAnswers(Airlink),
            ]);
        }

        /// <summary>Every exchange the stub received.</summary>
        public StubJournal Journal { get; }

        /// <summary>Who a login answers as, and which logins are refused.</summary>
        public StubAuthentication Authentication { get; }

        /// <summary>The controller's state and its stored project.</summary>
        public StubController Controller { get; }

        /// <summary>The controller's system information, user log, wireless network and test-send outcome.</summary>
        public StubConfiguration Configuration { get; }

        /// <summary>The one resource store, its change queue, and the logged data and scene positions around it.</summary>
        public StubResources Resources { get; }

        /// <summary>The controller's user list.</summary>
        public StubUserManager UserManager { get; }

        /// <summary>The controller's clock, time settings and time-server outcome.</summary>
        public StubTime Time { get; }

        /// <summary>The SMS modem's information, settings and status.</summary>
        public StubSmsModem SmsModem { get; }

        /// <summary>The LED dimmers, their detection queue and the firmware upgrade's progress.</summary>
        public StubLedDimmers LedDimmers { get; }

        /// <summary>The wireless devices, their low-battery list, and the detection and test-result queues.</summary>
        public StubAirlink Airlink { get; }

        /// <summary>
        /// Every exchange the stub could not answer, in the order they arrived. The SDK raises the fault the stub
        /// answers as <see cref="Errors.CONTROLLER_SOAP_FAULT"/>, its text naming the operation, but code under test
        /// may swallow that, so this list is where the diagnostic is read.
        /// </summary>
        public IReadOnlyList<UnmodelledCall> Unmodelled => Gated.Copy(_gate, _unmodelled);

        /// <summary>Every exception the stub threw while answering, in the order they happened.</summary>
        public IReadOnlyList<StubDefect> Defects => Gated.Copy(_gate, _defects);

        /// <summary>
        /// Fails <paramref name="sdkMethod"/> of <typeparamref name="TService"/> from now on. A multi-call method fails
        /// at its primary action; a step it takes is failed through the method that owns the step.
        /// </summary>
        /// <exception cref="ArgumentException"><typeparamref name="TService"/> is a service class rather than its
        /// interface, or the method does not exist, sends nothing, or owns no wire operation, so the failure could
        /// never happen.</exception>
        public void FailOn<TService>(string sdkMethod, StubFault fault = StubFault.SoapFault) where TService : IIHCApiService
        {
            ArgumentNullException.ThrowIfNull(sdkMethod);
            if (!Enum.IsDefined(fault))
            {
                throw new ArgumentOutOfRangeException(nameof(fault), fault, "not a stub fault");
            }
            SdkOperation owned = SdkOperations.OwnedRow(typeof(TService), sdkMethod, nameof(sdkMethod),
                sendsNothing: "a failure injected into it could never happen",
                ownsNothing: "fail the method that owns one of its steps");
            lock (_gate)
            {
                _failures[(owned.Service, owned.WireAction)] = fault;
            }
        }

        /// <summary>
        /// Answers <paramref name="soapAction"/> of <paramref name="service"/> with <paramref name="answer"/> from now
        /// on, ahead of everything else: the escape hatch for a malformed body or a network-shaped failure.
        /// </summary>
        /// <remarks>
        /// Keyed by the WIRE names, as <see cref="StubCall.Service"/> and <see cref="StubCall.SoapAction"/> read:
        /// <c>SMSModemService</c> and <c>getIHCProject</c>, not the SDK's <c>SmsModemService</c> and <c>GetProject</c>.
        /// The wire names rather than an SDK method, because an override must also reach an operation no public
        /// method owns, such as a step of a change stream.
        /// </remarks>
        /// <exception cref="ArgumentException">No wire operation has these names, so no exchange could match the
        /// override; the message names the wire name meant when the SDK's was given.</exception>
        public void OverrideRaw(string service, string soapAction,
            Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>> answer)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(soapAction);
            ArgumentNullException.ThrowIfNull(answer);
            if (SdkOperations.Find(service, soapAction) is null)
            {
                throw new ArgumentException(SdkOperations.NoWireOperation(service, soapAction), nameof(soapAction));
            }
            lock (_gate)
            {
                _overrides[(service, soapAction)] = answer;
            }
        }

        /// <summary>
        /// The teardown check: throws, listing every entry, when the stub met a call it could not answer or threw
        /// while answering one. Either can be swallowed by the code under test, so a suite asserts it at teardown.
        /// </summary>
        public void VerifyNoFaults()
        {
            List<string> found;
            lock (_gate)
            {
                found =
                [
                    .. _unmodelled.Select(call => $"unmodelled {call.Service}.{call.SoapAction}: {call.Reason}"),
                    // The exception's type only: its message is the stub's own and may not be readable.
                    .. _defects.Select(defect => $"defect in {defect.Service}.{defect.SoapAction}: {defect.Exception.GetType().FullName}"),
                ];
            }
            if (found.Count > 0)
            {
                throw new InvalidOperationException("ihcclient_stub: " + string.Join("; ", found));
            }
        }

        /// <inheritdoc/>
        public async Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            // HTTP lets a client quote the header's value; the SDK does not, but a hand-run client may.
            string wireAction = request.SoapAction.Trim('"');
            SdkOperation? operation = SdkOperations.Find(request.Service, wireAction);

            try
            {
                WireAnswer answer = await AnswerAsync(request, wireAction, operation, cancellationToken).ConfigureAwait(false);
                if (answer.Unmodelled is { } reason)
                {
                    lock (_gate)
                    {
                        _unmodelled.Add(new UnmodelledCall(request.Service, wireAction, reason));
                    }
                }
                Journal.Append(request.Service, wireAction, operation, answer.Outcome, answer.Payload, request.Cookie, answer.Response.SetCookie);
                return answer.Response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (_gate)
                {
                    _defects.Add(new StubDefect(request.Service, wireAction, ex));
                }
                Journal.Append(request.Service, wireAction, operation, StubOutcome.Defect, null, request.Cookie, null);
                throw;
            }
        }

        private async Task<WireAnswer> AnswerAsync(ControllerStubRequest request, string wireAction, SdkOperation? operation,
            CancellationToken cancellationToken)
        {
            Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>>? rawOverride;
            StubFault? failure;
            lock (_gate)
            {
                rawOverride = _overrides.GetValueOrDefault((request.Service, wireAction));
                failure = _failures.TryGetValue((request.Service, wireAction), out var injected) ? injected : null;
            }

            if (rawOverride is not null)
            {
                ControllerStubResponse response = await rawOverride(request, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("the raw override returned no response");
                return new WireAnswer(response, StubOutcome.Overridden, null);
            }
            if (failure is { } fault)
            {
                return Injected(request.Service, wireAction, fault);
            }
            if (operation is null)
            {
                return SoapDispatcher.Unmodelled(request.Service, wireAction, "no generated operation has this service and wire action");
            }
            return await _dispatcher.AnswerAsync(operation, request, cancellationToken).ConfigureAwait(false);
        }

        private static WireAnswer Injected(string service, string wireAction, StubFault fault) =>
            fault == StubFault.ConnectionLost
                ? new WireAnswer(ControllerStubResponse.ConnectionLost, StubOutcome.ConnectionLost, null)
                : WireAnswer.Fault(StubOutcome.FaultInjected, $"injected fault on {service}.{wireAction}");
    }
}
