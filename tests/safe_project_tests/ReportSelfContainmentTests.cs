using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests;

/// <summary>
/// The registered difference "documentation reports render as self-contained static HTML that works in any modern
/// browser … with no dependency on a legacy browser component".
///
/// <para><see cref="ReportHtmlOracleTests"/> pins the exact bytes, which sounds like it covers this — but a byte
/// oracle only says the output has not CHANGED. Regenerate the oracles alongside a change that starts pulling a
/// stylesheet, a web font or an icon from a URL and both stay green, while the report silently stops working on the
/// machine it matters on: an installer's laptop in a plant room, offline, opening a file that was e-mailed to them.
/// Self-containment is a property of the output, so it is asserted as one.</para>
///
/// <para>The scope is deliberately every reference that leaves the file — remote URLs, but also <c>src</c>/<c>href</c>
/// pointing at a sibling file, since a report is handed on as a single document and its neighbours do not travel
/// with it. Fragment links (<c>#icon-logo</c>) and <c>data:</c> URIs stay inside and are fine.</para>
/// </summary>
public class ReportSelfContainmentTests
{
    private static ProjectAppService App() =>
        new(new IhcSettings(), new BuiltInCatalog(), ReportOracleHarness.Clock());

    private static string TestData(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "testdata" }.Concat(parts).ToArray());

    private static object[][] HtmlOracleCases() => [.. ReportOracleHarness.Cases("html")];

    /// <summary>Anything that would make the browser fetch something: a scheme-qualified or protocol-relative URL,
    /// or a relative path in a resource attribute. <c>data:</c> and <c>#fragment</c> are excluded by the pattern.</summary>
    private static readonly Regex ExternalReference = new(
        """(?:src|href)\s*=\s*"(?!#|data:)([^"]*)"|url\(\s*['"]?(?!data:)([^'")]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [TestCaseSource(nameof(HtmlOracleCases))]
    public async Task HtmlReport_ReferencesNothingOutsideItself(
        string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
    {
        Project project = await App().Load(TestData("projects", projectFile));
        using var output = new MemoryStream();

        await App().GenerateReport(project, kind, mode, ReportMimeTypes.Html, output, new SvgReportIconProvider());

        string html = Encoding.UTF8.GetString(output.ToArray());
        string[] references = ExternalReference.Matches(html)
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .Where(r => r.Length > 0)
            .Distinct()
            .ToArray();

        Assert.That(references, Is.Empty,
            $"{oracleFile} fetches something the file does not carry, so it breaks offline and when forwarded: "
            + string.Join(", ", references));
    }

    /// <summary>The other half of "no dependency on a legacy browser component": nothing in the output asks for the
    /// vendor's XSLT/ActiveX pipeline, which is what tied the original's reports to one browser on one platform.</summary>
    [TestCaseSource(nameof(HtmlOracleCases))]
    public async Task HtmlReport_AsksForNoBrowserComponent(
        string oracleFile, string projectFile, ReportKind kind, ReportMode mode)
    {
        Project project = await App().Load(TestData("projects", projectFile));
        using var output = new MemoryStream();

        await App().GenerateReport(project, kind, mode, ReportMimeTypes.Html, output, new SvgReportIconProvider());

        string html = Encoding.UTF8.GetString(output.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("<object").IgnoreCase, "no embedded component");
            Assert.That(html, Does.Not.Contain("ActiveX").IgnoreCase);
            Assert.That(html, Does.Not.Contain("xml-stylesheet").IgnoreCase, "the page is finished HTML, not a transform");
        });
    }
}
