using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The REAL mode: launches <c>ihc_openvisual.exe</c> and drives it through the <c>aui</c> UI-Automation driver,
/// one <c>pwsh</c> process per verb.
/// </summary>
/// <remarks>
/// The driver is the surface the project already maintains and documents, so a scenario written through it
/// exercises the same vocabulary a person debugging the app types by hand — and a failure is reproducible by
/// copying one command line out of the assertion message. Reaching around it into raw UIA would test a path
/// nothing else uses.
/// </remarks>
internal sealed class AuiProcessDriver : IE2EDriver
{
    public string Name => "aui (real GUI)";

    /// <summary>Windows only: the driver reaches the app through <c>pwsh</c> and Windows UI Automation.</summary>
    public string? UnmetRequirement => OperatingSystem.IsWindows()
        ? null
        : "the real GUI is driven through Windows UI Automation, and this is "
          + $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}. Re-run with "
          // Quoted and prefixed with the argument separator so the sentence is a command line that runs as
          // pasted; single quotes are literal in every shell this message can be read in.
          + $"-- 'TestRunParameters.Parameter(name=\"{E2E.HeadlessParameter}\",value=\"true\")' for the headless mode.";

    private static string DriverPath() => Path.Combine(
        TestRepository.RequireRoot(), ".claude", "skills", "aui-openvisual", "scripts", "aui.ps1");

    public E2E.Envelope Run(string[] args)
    {
        // Forced to UTF-8 on the way out, not just on the way in. PowerShell encodes a redirected stream with
        // the console's legacy code page, so a Danish letter in an envelope arrives as a stray byte — 'ø' came
        // back as a bare 0x9B, which is a C1 CONTROL CHARACTER inside a JSON string. .NET happens to absorb it
        // as U+FFFD, so assertions comparing two mojibaked strings still matched each other and the corruption
        // stayed invisible; a stricter parser rejects the envelope outright. Since every message this panel
        // shows is Danish, that is not an edge case here.
        string invocation = $"[Console]::OutputEncoding=[Text.Encoding]::UTF8; & {Quote(DriverPath())} "
                            + string.Join(' ', args.Select(Quote));
        // The -Command payload is DOUBLE-quoted for the process argument parser, and everything inside it is
        // single-quoted for PowerShell. Quoting the payload the same way as its contents made the whole line a
        // literal string, which pwsh dutifully echoed instead of running.
        string arguments = $"-NoProfile -Command \"{invocation}\"";

        ProcessStartInfo start = new("pwsh", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = TestRepository.RequireRoot(),
        };

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("could not start pwsh");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        try
        {
            return E2E.Envelope.Parse(stdout);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"'aui {string.Join(' ', args)}' produced no JSON envelope.\n"
                + $"stdout: {stdout}\nstderr: {stderr}");
        }
    }

    /// <summary>
    /// Kills the application. Called in every teardown, and not only for tidiness: a surviving instance holds
    /// locks on its own binaries, so a later build fails with a file-in-use error that names nothing about tests.
    /// </summary>
    public void KillApp()
    {
        foreach (Process process in Process.GetProcessesByName("ihc_openvisual"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"could not kill a surviving app instance: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// Quotes an argument for the <c>-Command</c> string. Single quotes, because the whole invocation is itself
    /// double-quoted for the process arguments, and PowerShell does not expand inside single quotes — so a
    /// fixture path or a Danish row text cannot be re-interpreted as syntax.
    /// </summary>
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
