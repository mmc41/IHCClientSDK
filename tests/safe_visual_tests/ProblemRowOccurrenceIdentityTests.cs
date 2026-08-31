using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;

namespace safe_visual_tests;

/// <summary>
/// A <i>Problemer</i> row is addressable per OCCURRENCE, not per code. The panel stamps the code as each row's
/// <c>AutomationId</c>, and the authored error corpus emits several codes many times over — so "the
/// <c>doc-cable-colour</c> row" names eight of them and a driver asking for one gets an arbitrary one.
/// <para>The code stays published: every existing E2E addresses rows by it. The occurrence identity is a SECOND
/// automation property beside it, so both selectors work.</para>
/// </summary>
public class ProblemRowOccurrenceIdentityTests : AvaloniaTestBase
{
    [AvaloniaTest]
    public async Task EveryRowOfAMultiOccurrenceCodeCarriesADistinctIdentity()
    {
        using ProblemsWindowRig rig = await ProblemsWindowRig.ShowingFindingsAsync();

        var repeated = rig.Shell.Problems.Rows
            .GroupBy(r => r.Code)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(repeated, Is.Not.Empty,
                "sanity: the fixture emits at least one code more than once, which is the whole problem");
            foreach (var group in repeated)
            {
                Assert.That(group.Select(r => r.OccurrenceId).Distinct().Count(), Is.EqualTo(group.Count()),
                    $"the {group.Count()} '{group.Key}' rows must be separately addressable");
            }
        });
    }

    [AvaloniaTest]
    public async Task NoTwoRowsInTheWholeListShareAnIdentity()
    {
        using ProblemsWindowRig rig = await ProblemsWindowRig.ShowingFindingsAsync();

        var ids = rig.Shell.Problems.Rows.Select(r => r.OccurrenceId).ToList();

        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count),
            "an identity that repeats across codes would be as unusable as the code was");
    }

    [AvaloniaTest]
    public async Task TheIdentityNamesTheCodeAndTheSiteTheEngineRecorded()
    {
        using ProblemsWindowRig rig = await ProblemsWindowRig.ShowingFindingsAsync();

        ProblemRowViewModel row = rig.Shell.Problems.Rows.OfType<ProblemRowViewModel>()
            .First(r => r.Element is not null);

        Assert.That(row.OccurrenceId, Does.StartWith(row.Code),
            "so a driver matching loosely on a code still reaches the occurrence rows of that code");
    }

    /// <summary>
    /// Both properties reach automation. The code keeps <c>AutomationId</c> — every existing E2E and the driver's
    /// own <c>--row &lt;code&gt;</c> selector read it there — and the occurrence identity travels on its own
    /// channel rather than displacing it.
    /// </summary>
    [AvaloniaTest]
    public async Task ARealizedRowPublishesBothTheCodeAndItsOccurrenceIdentity()
    {
        using ProblemsWindowRig rig = await ProblemsWindowRig.ShowingFindingsAsync();

        TableViewRow[] realized = [.. rig.Window.GetVisualDescendants().OfType<TableViewRow>()];
        Assert.That(realized, Is.Not.Empty, "sanity: rows are realized");

        Assert.Multiple(() =>
        {
            foreach (TableViewRow row in realized)
            {
                if (row.DataContext is not ProblemRowViewModel vm)
                    continue;
                Assert.That(AutomationProperties.GetAutomationId(row), Is.EqualTo(vm.Code),
                    "the code is still published where the driver looks for it today");
                Assert.That(AutomationProperties.GetItemStatus(row), Is.EqualTo(vm.OccurrenceId),
                    "and the occurrence identity is published beside it");
            }
        });
    }
}
