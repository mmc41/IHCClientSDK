using System.Reflection;

namespace Ihc.Stubbing
{
    /// <summary>How the dispatcher answered: the response, what it was, and the decoded payload a typed answer
    /// recorded for the journal. An unmodelled answer carries why.</summary>
    internal sealed record WireAnswer(ControllerStubResponse Response, StubOutcome Outcome, object? Payload, string? Unmodelled = null)
    {
        private const int ServerFault = 500;

        /// <summary>A SOAP fault the stub raises itself, which says so, because a person reading it in a client's
        /// error would otherwise take it for the controller's.</summary>
        internal static WireAnswer Fault(StubOutcome outcome, string what, string? unmodelled = null) =>
            new(new ControllerStubResponse(ServerFault, SoapCodec.Fault("ihcclient_stub: " + what)), outcome, null, unmodelled);
    }

    /// <summary>
    /// Answers an operation the stub neither overrides nor fails: with a typed answer where one exists, else with a
    /// value built from the response type. Reads the request and writes the answer through the codec.
    /// </summary>
    /// <remarks>
    /// The operation is found by the wire action the SDK posts, never by the WSDL's declared action, which two
    /// generated overloads share. An operation whose answer cannot be built is unmodelled: it answers a SOAP fault
    /// naming the operation, which the SDK raises as <see cref="Errors.CONTROLLER_SOAP_FAULT"/> with that text.
    /// </remarks>
    internal sealed class SoapDispatcher
    {
        private const int Ok = 200;

        private readonly Dictionary<(string Service, string WireAction), (object Target, MethodInfo Method)> _answers;

        internal SoapDispatcher(IEnumerable<IWireAnswers> answerClasses)
        {
            _answers = [];
            foreach (IWireAnswers answers in answerClasses)
            {
                foreach (MethodInfo method in AnswerMethodsOf(answers.GetType()))
                {
                    if (AnsweredOperation(answers.Service, method) is { } operation)
                    {
                        _answers[(operation.Service, operation.WireAction)] = (answers, method);
                    }
                }
            }
        }

        /// <summary>The methods an answer class declares that are named as answers. A constant answer reads no state
        /// and may be static.</summary>
        internal static IEnumerable<MethodInfo> AnswerMethodsOf(Type answerClass) =>
            answerClass.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => SdkOperations.WireActionOf(method.Name) is not null);

        /// <summary>The generated operation <paramref name="method"/> answers: one of <paramref name="service"/>'s,
        /// with the contract method's name and signature.</summary>
        internal static SdkOperation? AnsweredOperation(string service, MethodInfo method)
        {
            if (SdkOperations.WireActionOf(method.Name) is not { } wireAction
                || SdkOperations.Find(service, wireAction) is not { } operation)
            {
                return null;
            }
            bool sameSignature = method.ReturnType == operation.ContractMethod.ReturnType
                                 && method.GetParameters().Select(parameter => parameter.ParameterType)
                                     .SequenceEqual(operation.ContractMethod.GetParameters().Select(parameter => parameter.ParameterType));
            return sameSignature ? operation : null;
        }

        internal static Type RequestTypeOf(SdkOperation operation) => operation.ContractMethod.GetParameters()[0].ParameterType;

        internal static Type ResponseTypeOf(SdkOperation operation) => operation.ContractMethod.ReturnType.GetGenericArguments()[0];

        internal async Task<WireAnswer> AnswerAsync(SdkOperation operation, ControllerStubRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Type responseType = ResponseTypeOf(operation);
            // Read even where the answer does not depend on it, so a request the operation cannot read fails here
            // as a stub defect rather than being answered as though it had been understood.
            object? body = SoapCodec.ReadRequest(RequestTypeOf(operation), request.Body);

            if (_answers.TryGetValue((operation.Service, operation.WireAction), out var answer))
            {
                var context = new AnswerContext();
                AnswerContext.Current.Value = context;
                var task = (Task)answer.Method.Invoke(answer.Target, BindingFlags.DoNotWrapExceptions, null, [body], null)!;
                await task.ConfigureAwait(false);
                object typed = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task)!;
                return new WireAnswer(new ControllerStubResponse(Ok, SoapCodec.WriteResponse(responseType, typed), context.SetCookie),
                    StubOutcome.Typed, context.Payload);
            }

            if (!ShapeDefaults.TryBuild(responseType, out object? shaped, out string? refusal))
            {
                return Unmodelled(operation.Service, operation.WireAction, refusal);
            }
            return new WireAnswer(new ControllerStubResponse(Ok, SoapCodec.WriteResponse(responseType, shaped)), StubOutcome.Shape, null);
        }

        internal static WireAnswer Unmodelled(string service, string wireAction, string reason) =>
            WireAnswer.Fault(StubOutcome.Unmodelled, $"{service}.{wireAction} is not modelled", reason);
    }
}
