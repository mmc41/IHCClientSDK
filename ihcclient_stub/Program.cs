namespace Ihc.Stubbing
{
    /// <summary>
    /// The stub's standalone entry point, for a person who wants to run an application against a controller stub. It
    /// serves a default stub on a loopback port, prints the switch to start the application with, and then one line
    /// per call the stub answered, until Ctrl+C.
    /// </summary>
    /// <remarks>
    /// A named class in the stub's namespace rather than top-level statements, because those declare <c>Program</c> in
    /// the global namespace, which name lookup searches before a file's <c>using</c> directives: every suite that
    /// references the stub would then bind <c>Program</c> to this class instead of to its own application's.
    /// </remarks>
    internal static class StubProgram
    {
        private static async Task<int> Main()
        {
            using CancellationTokenSource stopping = new();
            Console.CancelKeyPress += (_, press) =>
            {
                // Stops serving and returns from Main, rather than letting the runtime end the process mid-write.
                press.Cancel = true;
                stopping.Cancel();
            };

            ConsoleReportingStub stub = new(new ControllerStub(), Console.Out);
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, stopping.Token);
            // The first line, so a person, or a script, can copy it without searching the output.
            Console.WriteLine(ControllerStubs.SwitchArgument + "=" + host.BaseAddress);

            try
            {
                await Task.Delay(Timeout.Infinite, stopping.Token);
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C: the normal way this run ends.
            }
            return 0;
        }
    }

    /// <summary>
    /// A stub that answers as <see cref="ControllerStub"/> does and, after each request, prints what that request
    /// added: the call, any operation the stub does not model, and any defect in the stub itself. In a manual run
    /// nothing else reads those, and the application sees only an HTTP 500 or a coded fault.
    /// </summary>
    /// <remarks>
    /// Each line names the service and operation, the outcome, and for a defect the exception's type. Never a
    /// payload, a cookie or a password: the output is read and pasted freely, and a defect's message is the stub's
    /// own text, which may echo a request value, so it stays in <see cref="ControllerStub.Defects"/>.
    /// </remarks>
    internal sealed class ConsoleReportingStub(ControllerStub inner, TextWriter output) : IControllerStub
    {
        private readonly Lock gate = new();
        private int callsPrinted;
        private int unmodelledPrinted;
        private int defectsPrinted;

        public async Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Report();
            }
        }

        private void Report()
        {
            lock (gate)
            {
                Print(inner.Journal.Since(callsPrinted), ref callsPrinted, call => $"call {call.Service}.{call.SoapAction} {call.Outcome}");
                Print(inner.Unmodelled.Skip(unmodelledPrinted), ref unmodelledPrinted, call => $"unmodelled {call.Service}.{call.SoapAction}");
                Print(inner.Defects.Skip(defectsPrinted), ref defectsPrinted,
                    defect => $"defect {defect.Service}.{defect.SoapAction}: {defect.Exception.GetType().Name}");
            }
        }

        /// <summary>Prints <paramref name="fresh"/>, the entries past what earlier reports printed, and counts them in
        /// <paramref name="printed"/>: each list only grows, so a count is all a report needs to find what is new.</summary>
        private void Print<T>(IEnumerable<T> fresh, ref int printed, Func<T, string> line)
        {
            foreach (T entry in fresh)
            {
                output.WriteLine(line(entry));
                printed++;
            }
        }
    }
}
