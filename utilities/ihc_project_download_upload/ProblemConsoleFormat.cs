using System.Text;

using Ihc.Vis.Problems;

namespace Ihc.download_upload_example
{
    /// <summary>
    /// Renders an SDK coded problem for a CONSOLE — code, Danish message and declared arguments — in a utility that
    /// references the SDK and nothing else.
    /// <para>
    /// It exists to make a claim measurable rather than asserted (R17): the SDK's problem contract is meant to be
    /// the reusable bulk of the product's error vocabulary, usable by any frontend, and the only way to show that
    /// is a consumer with no reference to the GUI assembly that renders one anyway. An architecture test does
    /// exactly that with an arbitrary problem, and also checks this utility's own references.
    /// </para>
    /// <para>
    /// This is not a second presentation path in the sense the design forbids: that rule is about the SHELL, where
    /// one dialog must not render identity differently from another. A console tool is a different medium with a
    /// different reader, and it follows the same subordinate-identity rule anyway — the message first, the code in
    /// brackets after it — with one addition a diagnostic reader needs and a dialog does not: the declared
    /// arguments, spelled out by name, so a value that was formatted into the sentence can still be read back
    /// exactly as the producer bound it.
    /// </para>
    /// </summary>
    internal static class ProblemConsoleFormat
    {
        /// <summary>
        /// One problem, on one line: <c>message [code] (name=value, …)</c>. The argument list is omitted when the
        /// problem declares none, so a code with no data reads exactly as it does in the shell.
        /// </summary>
        /// <param name="problem">The problem to render. Its message is the SDK's own Danish text, unchanged.</param>
        public static string Describe(Problem problem)
        {
            StringBuilder line = new(problem.Message);
            if (problem.Code.Value is { Length: > 0 } code)
            {
                line.Append(" [").Append(code).Append(']');
            }

            if (problem.Arguments.Length > 0)
            {
                line.Append(" (");
                for (int i = 0; i < problem.Arguments.Length; i++)
                {
                    ProblemArgument argument = problem.Arguments[i];
                    line.Append(i == 0 ? string.Empty : ", ").Append(argument.Name).Append('=').Append(argument.Value);
                }

                line.Append(')');
            }

            return line.ToString();
        }

        /// <summary>
        /// A cause/detail chain: the CAUSE's line, exactly as the rule states — one failure is reported once. The
        /// operation is named separately on its own line here rather than dropped, because a console reader is a
        /// developer diagnosing which operation failed, not an installer being told what went wrong.
        /// </summary>
        /// <param name="chain">The chain to render.</param>
        public static string Describe(ProblemChain chain) =>
            $"{Describe(chain.Cause)}{System.Environment.NewLine}  operation: {chain.Operation.Code}";

        /// <summary>
        /// What to print for a failed operation: the SDK's coded problem when the exception carries one, and the
        /// exception's own English text when it does not. A utility is internal tooling, so English is correct
        /// here — what is NOT correct is throwing away an identity the SDK went to the trouble of attaching.
        /// </summary>
        /// <param name="error">The exception the operation failed with.</param>
        public static string Describe(System.Exception error) =>
            error is IProblemCarrier { Problems: { } chain } ? Describe(chain) : error.Message;
    }
}
