// Turns the merged Visual Studio coverage XML into a human summary: a Summary.txt beside the report
// and a single line on stdout, which Directory.Build.targets re-emits after every test run.
//
// This reads the report rather than computing coverage, so it must never fail a build: every error
// path writes a short diagnostic to stdout and exits 0. The MSBuild step is ContinueOnError as well,
// because coverage here is a report and not a gate.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

if (args.Length < 3)
{
    Console.WriteLine("coverage-summary: expected <coverage.xml> <raw-directory> <summary-output>.");
    return 0;
}

string reportPath = Path.GetFullPath(args[0]);
string rawDirectory = Path.GetFullPath(args[1]);
string summaryPath = Path.GetFullPath(args[2]);

if (!File.Exists(reportPath))
{
    Console.WriteLine($"coverage-summary: no coverage report at {reportPath}.");
    return 0;
}

XDocument document;
try
{
    // The report is machine-generated and local, but it is still untrusted input as far as the
    // security analyzers are concerned: resolving a DTD would let a report reach the filesystem
    // or the network.
    XmlReaderSettings settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };
    using XmlReader reader = XmlReader.Create(reportPath, settings);
    document = XDocument.Load(reader);
}
catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
{
    Console.WriteLine($"coverage-summary: could not read {reportPath}: {ex.Message}");
    return 0;
}

List<ModuleTotals> modules = [];
foreach (XElement element in document.Descendants("module"))
{
    string name = (string?)element.Attribute("name") ?? "(unnamed)";
    modules.Add(new ModuleTotals(
        name,
        ReadLong(element, "lines_covered"),
        ReadLong(element, "lines_partially_covered"),
        ReadLong(element, "lines_not_covered")));
}

if (modules.Count == 0)
{
    Console.WriteLine("coverage-summary: the coverage report contains no modules.");
    return 0;
}

modules.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

long covered = modules.Sum(m => m.Covered);
long partial = modules.Sum(m => m.Partial);
long notCovered = modules.Sum(m => m.NotCovered);
long total = covered + partial + notCovered;

// Partially covered lines count as covered, which is how the Visual Studio tooling reports a
// line_coverage percentage. Counting them as misses would make this summary disagree with every
// other view of the same file.
double percent = total == 0 ? 0d : (covered + partial) * 100d / total;

List<SuiteSlice> slices = ReadSlices(rawDirectory);
List<string> staleSuites = FindStaleSuites(slices, modules, rawDirectory);

StringBuilder summary = new();
summary.AppendLine("Code coverage");
summary.AppendLine("=============");
summary.AppendLine(CultureInfo.InvariantCulture, $"Generated : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
summary.AppendLine();
summary.AppendLine(CultureInfo.InvariantCulture, $"Lines covered           : {covered}");
summary.AppendLine(CultureInfo.InvariantCulture, $"Lines partially covered : {partial}");
summary.AppendLine(CultureInfo.InvariantCulture, $"Lines not covered       : {notCovered}");
summary.AppendLine(CultureInfo.InvariantCulture, $"Total lines             : {total}");
summary.AppendLine(CultureInfo.InvariantCulture, $"Line coverage           : {percent:0.00}%");
summary.AppendLine();

summary.AppendLine("Per module");
summary.AppendLine("----------");
foreach (ModuleTotals module in modules)
{
    long moduleTotal = module.Covered + module.Partial + module.NotCovered;
    double modulePercent = moduleTotal == 0 ? 0d : (module.Covered + module.Partial) * 100d / moduleTotal;
    summary.AppendLine(CultureInfo.InvariantCulture,
        $"{module.Name,-34} {modulePercent,6:0.00}%   {module.Covered + module.Partial} / {moduleTotal}");
}
summary.AppendLine();

// Which suites the number above actually rests on. Each suite refreshes only its own slice, so a
// suite absent here contributed nothing to this roll-up -- and one listed as stale contributed
// numbers measured against an older build.
summary.AppendLine("Contributing suites");
summary.AppendLine("-------------------");
if (slices.Count == 0)
{
    summary.AppendLine("(none)");
}
else
{
    foreach (SuiteSlice slice in slices)
    {
        string marker = staleSuites.Contains(slice.Name) ? "  STALE (older than the build it is merged with)" : "";
        summary.AppendLine(CultureInfo.InvariantCulture,
            $"{slice.Name,-34} {slice.WrittenUtc:yyyy-MM-dd HH:mm:ss}Z{marker}");
    }
}

try
{
    string? summaryDirectory = Path.GetDirectoryName(summaryPath);
    if (!string.IsNullOrEmpty(summaryDirectory))
    {
        Directory.CreateDirectory(summaryDirectory);
    }
    File.WriteAllText(summaryPath, summary.ToString());
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.WriteLine($"coverage-summary: could not write {summaryPath}: {ex.Message}");
    return 0;
}

string suiteWord = slices.Count == 1 ? "suite" : "suites";
string staleNote = staleSuites.Count == 0
    ? ""
    : $" | STALE: {string.Join(", ", staleSuites)}";
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"Coverage {percent:0.00}% lines ({covered + partial}/{total}) from {slices.Count} {suiteWord}{staleNote}"));

return 0;

static long ReadLong(XElement element, string attributeName) =>
    long.TryParse((string?)element.Attribute(attributeName), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out long value)
        ? value
        : 0L;

static List<SuiteSlice> ReadSlices(string rawDirectory)
{
    List<SuiteSlice> slices = [];
    if (!Directory.Exists(rawDirectory))
    {
        return slices;
    }

    foreach (string suiteDirectory in Directory.EnumerateDirectories(rawDirectory))
    {
        DateTime newest = DateTime.MinValue;
        foreach (string file in Directory.EnumerateFiles(suiteDirectory, "*.coverage", SearchOption.AllDirectories))
        {
            DateTime written = File.GetLastWriteTimeUtc(file);
            if (written > newest)
            {
                newest = written;
            }
        }

        if (newest > DateTime.MinValue)
        {
            slices.Add(new SuiteSlice(Path.GetFileName(suiteDirectory), newest));
        }
    }

    slices.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
    return slices;
}

// The merge is cumulative by design: running one suite refreshes one slice and leaves the rest.
// That is the price of a per-suite layout, and this names the suites it is currently costing
// rather than letting a stale roll-up read as current.
static List<string> FindStaleSuites(List<SuiteSlice> slices, List<ModuleTotals> modules, string rawDirectory)
{
    List<string> stale = [];
    if (slices.Count == 0)
    {
        return stale;
    }

    string? repositoryRoot = Path.GetDirectoryName(Path.GetDirectoryName(rawDirectory.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
    repositoryRoot = repositoryRoot is null ? null : Path.GetDirectoryName(repositoryRoot);
    if (repositoryRoot is null || !Directory.Exists(repositoryRoot))
    {
        return stale;
    }

    DateTime newestBuild = DateTime.MinValue;
    foreach (string binDirectory in EnumerateBinDirectories(repositoryRoot))
    {
        foreach (ModuleTotals module in modules)
        {
            foreach (string file in SafeEnumerateFiles(binDirectory, module.Name))
            {
                DateTime written = File.GetLastWriteTimeUtc(file);
                if (written > newestBuild)
                {
                    newestBuild = written;
                }
            }
        }
    }

    if (newestBuild == DateTime.MinValue)
    {
        return stale;
    }

    foreach (SuiteSlice slice in slices)
    {
        if (slice.WrittenUtc < newestBuild)
        {
            stale.Add(slice.Name);
        }
    }

    return stale;
}

// Bounded to the two directory depths this repository actually nests projects at, so the walk stays
// cheap enough to run after every test run.
static IEnumerable<string> EnumerateBinDirectories(string repositoryRoot)
{
    foreach (string first in SafeEnumerateDirectories(repositoryRoot))
    {
        string directBin = Path.Combine(first, "bin");
        if (Directory.Exists(directBin))
        {
            yield return directBin;
        }

        foreach (string second in SafeEnumerateDirectories(first))
        {
            string nestedBin = Path.Combine(second, "bin");
            if (Directory.Exists(nestedBin))
            {
                yield return nestedBin;
            }
        }
    }
}

static IEnumerable<string> SafeEnumerateDirectories(string path)
{
    try
    {
        return Directory.EnumerateDirectories(path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return [];
    }
}

static IEnumerable<string> SafeEnumerateFiles(string path, string pattern)
{
    try
    {
        return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return [];
    }
}

internal sealed record ModuleTotals(string Name, long Covered, long Partial, long NotCovered);

internal sealed record SuiteSlice(string Name, DateTime WrittenUtc);
