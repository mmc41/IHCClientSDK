using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Saving a block to the library is TWO acts — write the <c>.ifb</c>, then register it so the block appears under
/// <i>Indsæt ▸ FunktionsBlokke</i> — and the second one could fail while the gesture reported success. The
/// registration's answer was discarded, so a rejected import showed its own rejection dialog and the status line
/// then said the block had been saved: two contradictory statements about one action, in the same second.
/// </summary>
public class FunctionBlockLibrarySaveHonestyTests
{
    /// <summary>Reproduce-first: with the registration rejected, the gesture must not also announce a success.</summary>
    [Test]
    public async Task ARejectedRegistration_IsNotAlsoAnnouncedAsASave()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.WithNewFunctionBlockAsync();
        TreeNodeViewModel block = ShellHarness.NewBlockNode(vm);
        string path = Path.Combine(harness.CatalogDir, "Reusable.ifb");
        // The library COMMIT raises StateChanged, which is after the export and before the registration. Taking
        // the written file away there is what makes the registration fail on a gesture whose export succeeded --
        // the shape the defect needs, and one no assertion can reach from outside the flow.
        harness.Session.StateChanged += (_, _) =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        };
        harness.Dialogs.PropertiesResult = new PropertiesResult("Reusable", "note");

        await vm.SaveFunctionBlockCommand.ExecuteAsync(block);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null,
                "sanity: the registration really was rejected, and said so");
            Assert.That(vm.StatusText, Does.Not.Contain("Gemte funktionsblokken"),
                "the same action may not be reported as rejected and as saved");
        });
    }

    /// <summary>The whole gesture succeeding is unchanged — the control, so the test above cannot pass by making
    /// every save silent.</summary>
    [Test]
    public async Task ASuccessfulSave_StillAnnouncesItself()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.WithNewFunctionBlockAsync();
        TreeNodeViewModel block = ShellHarness.NewBlockNode(vm);
        harness.Dialogs.PropertiesResult = new PropertiesResult("Reusable", "note");

        await vm.SaveFunctionBlockCommand.ExecuteAsync(block);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Is.EqualTo("Gemte funktionsblokken 'Reusable' i biblioteket."));
            Assert.That(harness.Dialogs.LastProblem, Is.Null, "and nothing was reported as wrong");
        });
    }
}
