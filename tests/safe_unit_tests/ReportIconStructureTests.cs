using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Projects;
using Ihc.Vis.Reporting;

namespace safe_unit_tests;

/// <summary>
/// T014/AC9 (S05): the default-unicode HTML variant has NO byte oracle, so it is pinned STRUCTURALLY
/// against the SVG variant — no definitions block, per-instance stand-ins as escaped text where the SVG
/// variant carries <c>&lt;use&gt;</c> fragments, and otherwise the byte-identical document: removing the
/// sprite line and substituting each icon fragment with its default stand-in must transform the SVG
/// variant EXACTLY into the default variant.
/// </summary>
public class ReportIconStructureTests
{
    private static readonly Regex IconFragment = new(
        """<svg class="icon icon-([a-z0-9-]+)" aria-hidden="true"><use href="#icon-\1"/></svg>""",
        RegexOptions.Compiled);

    [Test]
    public async Task DefaultUnicodeHtml_IsTheSvgVariant_WithSpriteRemoved_AndFragmentsAsStandIns()
    {
        var app = new ProjectAppService(new IhcSettings(), new BuiltInCatalog(), ReportOracles.Clock());
        Project project = await app.Load(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "project5-Dokumentation.vis"));

        using var withSvg = new MemoryStream();
        await app.GenerateReport(project, ReportKind.FunctionBlocks, ReportMode.Standard,
            ReportMimeTypes.Html, withSvg, new SvgReportIconProvider());
        using var withDefaults = new MemoryStream();
        await app.GenerateReport(project, ReportKind.FunctionBlocks, ReportMode.Standard,
            ReportMimeTypes.Html, withDefaults);

        string svgVariant = Encoding.UTF8.GetString(withSvg.ToArray());
        string defaultVariant = Encoding.UTF8.GetString(withDefaults.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(defaultVariant, Does.Not.Contain("style=\"display:none\""),
                "the default provider contributes no definitions block");
            Assert.That(defaultVariant, Does.Not.Contain("<use href=\"#icon-pin-in\""),
                "no per-instance <use> fragments without the provider");
            Assert.That(defaultVariant, Does.Contain("⇥").And.Contain("→").And.Contain("✓✓"),
                "the §7 unicode stand-ins render as text instead");
            Assert.That(TransformSvgVariantToDefault(svgVariant), Is.EqualTo(defaultVariant),
                "beyond sprite and fragments the two variants are byte-identical — same document structure");
        });
    }

    // Remove the sprite block (its opener through the first </svg> plus the line break) and replace every
    // per-instance icon fragment with the default stand-in for its key (stand-ins contain no
    // markup-significant characters, so their escaped form is the text itself).
    private static string TransformSvgVariantToDefault(string svgVariant)
    {
        const string spriteOpener = "<svg xmlns=\"http://www.w3.org/2000/svg\" style=\"display:none\">";
        int start = svgVariant.IndexOf(spriteOpener, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "the SVG variant must carry the sprite this transform removes");
        int end = svgVariant.IndexOf("</svg>\n", start, StringComparison.Ordinal) + "</svg>\n".Length;
        string withoutSprite = svgVariant.Remove(start, end - start);
        return IconFragment.Replace(withoutSprite, match => DefaultReportIcons.StandInFor(match.Groups[1].Value));
    }
}
