// Builds the merged coverage report from whatever per-suite slices are on disk: merge, convert to the
// Visual Studio XML the summary reads, render the browsable HTML, then print the one-line roll-up.
//
// It exists as a script rather than as four MSBuild steps because those four steps must not interleave.
// Every suite rebuilds the WHOLE report after its own run -- no suite is reliably last, so each one has to
// -- and a solution-wide `dotnet test` runs the suites in parallel. Four separate steps then have several
// processes writing and reading one merged.coverage, one coverage.xml, one html/ directory and one
// Summary.txt at the same time; the observed symptom was `dotnet-coverage merge` exiting 1 because it was
// reading a merged.coverage another suite was still writing. MSBuild has no way to hold a lock across
// tasks, so the lock has to live inside one process, and that process is this file.
//
// The lock is a lock FILE rather than a named mutex: named mutexes are Windows-only in .NET, and this repo
// builds on three desktops.
//
// Exit code carries the same contract as the Exec steps it replaces: non-zero ONLY when a step could not
// run, never because coverage came out low. Coverage here is a report and not a gate, so the caller leaves
// this ContinueOnError and a genuine failure surfaces as a warning.

using System.Diagnostics;
using System.Globalization;

if (args.Length < 4)
{
    Console.WriteLine("coverage-report: expected <raw-directory> <report-directory> <html-report-types> <summary-script>.");
    return 0;
}

string rawDirectory = Path.GetFullPath(args[0]);
string reportDirectory = Path.GetFullPath(args[1]);
string htmlReportTypes = args[2];
string summaryScript = Path.GetFullPath(args[3]);

// An ADVISORY look, outside the lock, so a run that collected nothing neither creates a report directory nor
// queues behind another suite's report. The list it produces is NOT the one merged -- see below -- and it can
// only be wrong in the direction that costs nothing: a slice written after this look belongs to a suite that
// runs this same reporter after itself.
if (FindSlices(rawDirectory).Count == 0)
{
    // No suite has left a slice yet. Not a failure: the caller runs after every test project, including
    // ones whose collection is switched off.
    return 0;
}

try
{
    Directory.CreateDirectory(reportDirectory);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.WriteLine($"coverage-report: could not create {reportDirectory}: {ex.Message}");
    return 1;
}

string mergedPath = Path.Combine(reportDirectory, "merged.coverage");
string xmlPath = Path.Combine(reportDirectory, "coverage.xml");
string htmlDirectory = Path.Combine(reportDirectory, "html");
string summaryPath = Path.Combine(reportDirectory, "Summary.txt");

// Generous, because the wait is bounded by how long ONE report takes and a solution-wide run can queue
// every suite behind each other. A timeout here means something is stuck, not merely busy.
using FileStream? gate = AcquireLock(Path.Combine(reportDirectory, ".report.lock"), TimeSpan.FromMinutes(5));
if (gate is null)
{
    Console.WriteLine("coverage-report: timed out waiting for another suite to finish writing the report.");
    return 1;
}

// The list that is actually merged, read UNDER the lock. Read before it, a reporter that then waited out
// another suite's whole run would merge the slices as they stood before that run and overwrite the complete
// report with an older one -- and the suite whose slice went missing has already run its own reporter, so
// nothing would put it back. The wait is exactly when the tree changes, so the list has to be read after it.
List<string> slices = FindSlices(rawDirectory);
if (slices.Count == 0)
{
    // Everything the advisory look saw has gone while this waited -- a suite clearing its own slice directory
    // for a rerun is the ordinary way that happens. Merging nothing is not a failure either.
    return 0;
}

bool failed = false;

// Each step runs even if an earlier one failed, which is what the four ContinueOnError steps this replaces
// did: a stale input produces a stale report, and that is better than a half-written one plus no diagnostic.
failed |= !Step("merge the slices",
    "dotnet", ["dotnet-coverage", "merge", .. slices, "-o", mergedPath, "--nologo"], out _);

failed |= !Step("convert the merge to XML",
    "dotnet", ["dotnet-coverage", "merge", mergedPath, "-o", xmlPath, "-f", "xml", "--nologo"], out _);

// Switching between the summary and the detailed mode otherwise leaves the previous mode's pages behind.
try
{
    if (Directory.Exists(htmlDirectory))
    {
        Directory.Delete(htmlDirectory, recursive: true);
    }
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.WriteLine($"coverage-report: could not clear {htmlDirectory}: {ex.Message}");
}

// Fed the standard Microsoft coverage XML directly -- the same file Summary.txt is built from, with no
// intermediate format. ReportGenerator derives its own coverable-line denominator from whatever it is
// given, so each conversion produces a slightly different headline percentage. Reading the same file keeps
// the two as close as the tools allow; they still do not agree exactly, and Summary.txt is the number this
// repository quotes. Converting to a third-party format first was measured and drifts further, so do not
// add a conversion step here.
if (File.Exists(xmlPath))
{
    failed |= !Step("render the HTML report",
        "dotnet", ["reportgenerator", $"-reports:{xmlPath}", $"-targetdir:{htmlDirectory}",
            $"-reporttypes:{htmlReportTypes}", "-verbosity:Warning"], out _);
}

// The summary is the only step whose output belongs on the console; MSBuild re-emits it so it survives at
// the verbosity CI runs at.
if (Step("summarise the report", "dotnet", ["run", summaryScript, "--", xmlPath, rawDirectory, summaryPath],
        out string summaryOutput))
{
    string line = summaryOutput.Trim();
    if (line.Length > 0)
    {
        Console.WriteLine(line);
    }
}
else
{
    failed = true;
}

return failed ? 1 : 0;

// The blame data collector stages a byte-identical second copy of each attachment under In/<machine>/.
// Merging is a union so the percentage is unaffected either way, but merging the same bytes twice is waste.
// Only that known staging path is excluded, so an unfamiliar layout is still picked up rather than dropped.
static List<string> FindSlices(string rawDirectory)
{
    if (!Directory.Exists(rawDirectory))
    {
        return [];
    }

    try
    {
        return [.. Directory.EnumerateFiles(rawDirectory, "*.coverage", SearchOption.AllDirectories)
            .Where(path => !IsBlameStagingCopy(path, rawDirectory))
            .Order(StringComparer.Ordinal)];
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.WriteLine($"coverage-report: could not read {rawDirectory}: {ex.Message}");
        return [];
    }
}

static bool IsBlameStagingCopy(string path, string rawDirectory) =>
    Path.GetRelativePath(rawDirectory, path)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => string.Equals(segment, "In", StringComparison.Ordinal));

// Held for the whole pipeline, so a second suite waits rather than reading a file the first is mid-write.
// FileShare.None IS the lock; the file's contents are never read. DeleteOnClose keeps the report directory
// free of a leftover marker, and the retry loop absorbs the moment where one process is deleting it while
// another is opening it.
static FileStream? AcquireLock(string lockPath, TimeSpan timeout)
{
    DateTime deadline = DateTime.UtcNow + timeout;
    while (true)
    {
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.DeleteOnClose);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            Thread.Sleep(200);
        }
    }
}

// Tool chatter is captured rather than streamed: these steps ran at low output importance before, and a
// green run should say one line about coverage, not several screens. A failure prints what it produced.
static bool Step(string what, string fileName, string[] arguments, out string standardOutput)
{
    standardOutput = "";
    ProcessStartInfo start = new()
    {
        FileName = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (string argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    try
    {
        using Process? process = Process.Start(start);
        if (process is null)
        {
            Console.WriteLine($"coverage-report: could not start {fileName} to {what}.");
            return false;
        }

        // Read before waiting: a full pipe buffer would deadlock a process that is still writing.
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        standardOutput = output.GetAwaiter().GetResult();
        string standardError = error.GetAwaiter().GetResult();

        if (process.ExitCode == 0)
        {
            return true;
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"coverage-report: failed to {what} (exit {process.ExitCode})."));
        WriteIfAny(standardOutput);
        WriteIfAny(standardError);
        return false;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                   or System.ComponentModel.Win32Exception)
    {
        Console.WriteLine($"coverage-report: could not {what}: {ex.Message}");
        return false;
    }
}

static void WriteIfAny(string text)
{
    if (!string.IsNullOrWhiteSpace(text))
    {
        Console.WriteLine(text.Trim());
    }
}
