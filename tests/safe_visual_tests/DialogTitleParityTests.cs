using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-49 — the properties dialogs' TITLES, taken as a set rather than one at a time.
///
/// <para>OpenVisual titled a function block's dialog <c>Rediger &lt;navn&gt; egenskaber</c> because that is the
/// pattern [F-16] measured — <b>on a locality</b>. The reference application does not use one pattern, and the
/// generalization was never checked against the other node types. Measured live 2026-08-11, every node type that
/// has a dialog:</para>
///
/// <code>
///   locality           Rediger &lt;navn&gt; egenskaber      MATCH
///   variable           Rediger &lt;navn&gt; egenskaber      MATCH
///   Betingelser        Rediger Betingelser egenskaber      MATCH
///   product            Lampeudtag        (the TYPE)        MATCH
///   scenes container   Scenarier         (the node's name) MATCH
///   function block     Funktionsblok egenskaber            ← the node TYPE, not its name
///   modem              SMS Modem Egenskaber                ← "&lt;type&gt; Egenskaber"
/// </code>
///
/// <para>So the original names three different things depending on the node: the element, its type, or a fixed
/// caption. Five of the seven already agreed; these tests pin the two that did not, and pin the agreeing
/// neighbours beside them so a future "make them consistent" cannot quietly flatten the set again.</para>
/// </summary>
public class DialogTitleParityTests
{
    [Test]
    public async Task AFunctionBlock_IsTitledByItsTYPE_NotItsName()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(locality);

        await vm.PropertiesCommand.ExecuteAsync(vm.FunctionNodes[0].Children[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Funktionsblok egenskaber"),
            "the original captions this dialog by the node type, whatever the block is called");
    }

    /// <summary>A LOCALITY keeps the name-based form — the pattern F-16 measured, and the reason the block's
    /// title looked right. Pinned next to it so the two cannot be unified in either direction.</summary>
    [Test]
    public async Task ALocality_KeepsTheNameBasedTitle()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;

        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Rediger Stue egenskaber"));
    }

    [Test]
    public async Task AModem_IsTitledTypeThenEgenskaber()
    {
        var (harness, vm) = await ShellAsync();
        using var _ = harness;
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ElementId modem = (await harness.Session.AddProductAsync(locality, "_0x3103"))!.Value;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, modem)!);

        Assert.That(harness.Dialogs.LastModemPropertiesInput?.Title, Is.EqualTo("SMS Modem Egenskaber"),
            "the original names the type and then the word, not 'Egenskaber for <type>'");
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> ShellAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }
}
