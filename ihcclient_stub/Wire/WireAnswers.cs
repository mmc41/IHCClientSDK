namespace Ihc.Stubbing
{
    /// <summary>
    /// A class of hand-written answers for one SOAP service. Each answer method has the generated contract method's
    /// name and signature, and the dispatcher finds it by them; the class does not implement the generated
    /// interface, because the operations it leaves out are answered from their shape.
    /// </summary>
    internal interface IWireAnswers
    {
        /// <summary>The SOAP service whose operations this class answers.</summary>
        string Service { get; }
    }

    /// <summary>
    /// What an answer adds beside its typed response: the decoded payload for the journal and a session cookie to
    /// issue. An answer keeps the generated signature, so these travel beside it: the dispatcher opens one context
    /// per call, which flows into the answer and no further back than the call that opened it.
    /// </summary>
    internal sealed class AnswerContext
    {
        internal static readonly AsyncLocal<AnswerContext?> Current = new();

        internal object? Payload { get; private set; }

        internal string? SetCookie { get; private set; }

        /// <summary>Records <paramref name="payload"/> as the current call's journal payload.</summary>
        internal static void RecordPayload(object? payload) => Open().Payload = payload;

        /// <summary>Answers the current call with a <c>Set-Cookie</c> header carrying <paramref name="cookie"/>.</summary>
        internal static void IssueCookie(string cookie) => Open().SetCookie = cookie;

        private static AnswerContext Open() =>
            Current.Value ?? throw new InvalidOperationException("an answer's context exists only inside an answer");
    }
}
