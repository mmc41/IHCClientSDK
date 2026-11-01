namespace Ihc.Stubbing
{
    /// <summary>What became of one exchange.</summary>
    public enum StubOutcome
    {
        /// <summary>A hand-written answer, from a typed value.</summary>
        Typed,

        /// <summary>A value built from the response type.</summary>
        Shape,

        /// <summary>The test's raw override answered.</summary>
        Overridden,

        /// <summary>An injected SOAP fault.</summary>
        FaultInjected,

        /// <summary>An injected connection loss: no SOAP answer at all.</summary>
        ConnectionLost,

        /// <summary>No operation matched, or its answer could not be built.</summary>
        Unmodelled,

        /// <summary>The stub itself threw: a defect, never an answer.</summary>
        Defect,
    }

    /// <summary>
    /// One journalled exchange. Both the SDK's names and the SOAP names are kept, so the journal is read without the
    /// generated layer. <paramref name="SdkService"/> and <paramref name="SdkMethod"/> are the operation's owner,
    /// so a <c>getState</c> sent inside <c>StoreProject</c> is journalled under <c>GetControllerState</c>; both are
    /// null for an operation no public method sends. <paramref name="Payload"/> is the decoded SDK model a typed
    /// answer recorded, with its secrets, such as a password, cleared. <paramref name="Cookie"/> and
    /// <paramref name="SetCookie"/> are stub-issued session ids as sent, never a credential.
    /// </summary>
    public sealed record StubCall(
        long Seq,
        string Service,
        string SoapAction,
        string? SdkService,
        string? SdkMethod,
        StubOutcome Outcome,
        object? Payload,
        string? Cookie,
        string? SetCookie);

    /// <summary>Every exchange a stub answered, in the order it answered them, read the way the code under test is
    /// written.</summary>
    public sealed class StubJournal
    {
        private readonly Lock _gate;
        private readonly List<StubCall> _calls = [];

        internal StubJournal(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>
        /// Every exchange, in the order they were answered. That is the order they arrived in except for a long poll
        /// the stub holds, which is journalled when it ends, after the exchanges answered while it waited.
        /// </summary>
        public IReadOnlyList<StubCall> All => Gated.Copy(_gate, _calls);

        /// <summary>The exchanges journalled under <paramref name="sdkMethod"/> of <typeparamref name="TService"/>.</summary>
        /// <exception cref="ArgumentException"><typeparamref name="TService"/> is a service class rather than its
        /// interface, or the method does not exist, sends nothing, or owns no wire operation: nothing could ever be
        /// journalled under it, and the empty answer would pass an assertion that nothing was sent.</exception>
        public IReadOnlyList<StubCall> Calls<TService>(string sdkMethod) where TService : IIHCApiService
        {
            ArgumentNullException.ThrowIfNull(sdkMethod);
            SdkOperations.OwnedRow(typeof(TService), sdkMethod, nameof(sdkMethod),
                sendsNothing: "nothing is ever journalled under it",
                ownsNothing: "nothing is journalled under it, so read its steps from All by their SoapAction");
            lock (_gate)
            {
                return [.. _calls.Where(call => call.SdkService == typeof(TService).Name && call.SdkMethod == sdkMethod)];
            }
        }

        internal void Append(string service, string soapAction, SdkOperation? operation, StubOutcome outcome, object? payload,
            string? cookie, string? setCookie)
        {
            object? recorded = WithoutSecrets(payload);
            lock (_gate)
            {
                _calls.Add(new StubCall(_calls.Count + 1, service, soapAction, operation?.OwnerService?.Name, operation?.OwnerMethod,
                    outcome, recorded, cookie, setCookie));
            }
        }

        /// <summary>The exchanges after the first <paramref name="count"/>, for a reader that follows the journal as
        /// it grows and need not copy what it has already read.</summary>
        internal IReadOnlyList<StubCall> Since(int count)
        {
            lock (_gate)
            {
                return _calls[count..];
            }
        }

        /// <summary>
        /// <paramref name="payload"/> with every secret string member cleared, as a copy so the answer's own value is
        /// untouched. A secret is a member the SDK marks <see cref="SensitiveDataAttribute"/>, such as a network
        /// key, or one named for a password, which also covers a type the SDK does not mark. A journal is read and
        /// printed freely, so it holds no secret whatever an answer recorded.
        /// </summary>
        private static object? WithoutSecrets(object? payload)
        {
            if (payload is null || payload is string || payload.GetType().IsPrimitive)
            {
                return payload;
            }
            var secrets = payload.GetType().GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                                   && (property.IsDefined(typeof(SensitiveDataAttribute), inherit: true)
                                       || property.Name.Contains("password", StringComparison.OrdinalIgnoreCase))
                                   && property.GetIndexParameters().Length == 0
                                   && property.GetValue(payload) is not null)
                .ToList();
            if (secrets.Count == 0)
            {
                return payload;
            }

            object copy = typeof(object).GetMethod(nameof(MemberwiseClone), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(payload, null)!;
            foreach (var secret in secrets)
            {
                if (secret.SetMethod is null)
                {
                    throw new InvalidOperationException(
                        $"{payload.GetType().FullName}.{secret.Name} cannot be cleared, so the payload cannot be journalled");
                }
                secret.SetValue(copy, null);
            }
            return copy;
        }
    }
}
