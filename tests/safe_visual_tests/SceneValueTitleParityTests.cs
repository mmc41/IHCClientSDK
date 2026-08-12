using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-49's last cell — a scene MEMBER's value dialog is titled by the member's TYPE.
///
/// <para>Measured live 2026-08-11 by wiring a function block's scene pin to two different products and opening
/// the membership each one raised:</para>
/// <code>
///   relay   Lampeudtag ▸ Scenarier                    →  Relæ scenarie egenskaber   (+ a "Relæ værdi" OFF/ON combo)
///   dimmer  Lampeudtag dimmer ▸ Scenarier/regulering  →  Lysdæmper scenarie egenskaber
/// </code>
///
/// <para>OpenVisual titled both <c>Scenarie værdi</c>. BOTH members were measured rather than one and the other
/// inferred — this finding exists precisely because a per-type rule was once generalized from a single reading
/// (F-49), and the two words share no stem to guess from.</para>
/// </summary>
public class SceneValueTitleParityTests
{
    [Test]
    public async Task ARelayMember_IsTitledRelae()
    {
        Assert.That(await TitleForAsync(isDimmer: false), Is.EqualTo("Relæ scenarie egenskaber"));
    }

    [Test]
    public async Task ADimmerMember_IsTitledLysdaemper()
    {
        Assert.That(await TitleForAsync(isDimmer: true), Is.EqualTo("Lysdæmper scenarie egenskaber"));
    }

    /// <summary>The dialog is raised from TWO places — once when the link is being MADE, once when the stored
    /// membership is edited — and the original captions both the same way. Only the edit-time one was corrected
    /// first, and the link-time one went on reading <c>Scenarie værdi</c>; it took actually making a link in the
    /// running app to see it. Both are pinned here, and both now read one shared helper.</summary>
    [Test]
    public void BothCallSites_ShareTheOriginalsCaption()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SceneValueTitles.For(isDimmer: false), Is.EqualTo("Relæ scenarie egenskaber"));
            Assert.That(SceneValueTitles.For(isDimmer: true), Is.EqualTo("Lysdæmper scenarie egenskaber"));
        });
    }

    /// <summary>Opens the value dialog of a scene membership of the requested variant and reports its title. The
    /// membership is authored through the SDK's own <c>LinkScene</c>, whose <c>IsDimmer</c> the app infers from the
    /// scenes container's bound output — so the variant under test is the one the application would really see.</summary>
    private static async Task<string?> TitleForAsync(bool isDimmer)
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        // The PRODUCT decides the variant: a Lampeudtag's Scenarier takes relay members, a Lampeudtag dimmer's
        // Scenarier/regulering takes dimmer ones — the engine refuses the mismatched pairing outright, which is
        // why each case places its own product rather than flipping a flag.
        // _0x4304 is one of D22's shared identifiers (also "1-10v converter - Lampeudtag dimmer"), so the
        // wanted product is NAMED rather than guessed at — the factory refuses an ambiguous id (T046).
        await (isDimmer
            ? harness.Session.AddProductAsync(locality, "_0x4304", "Lampeudtag dimmer")
            : harness.Session.AddProductAsync(locality, "_0x2202"));
        ElementId block = (await harness.Session.AddEmptyFunctionBlockAsync(locality))!.Value;
        ElementId outputs = harness.Session.Current!.FindById(block)!.Descendants()
            .First(e => e.Tag == "outputs").Id!.Value;
        ElementId scenePin = (await harness.Session.AddVariableAsync(outputs, "resource_scene", "Scenarie"))!.Value;

        ProjectElement scenes = harness.Session.Current!.Root.DescendantsAndSelf()
            .First(e => e.IsScenesContainer);
        await harness.Session.ApplyAsync(new LinkScene(
            scenePin, scenes.Id!.Value, new SceneValueResult(On: true, LevelPercent: 50, RampMinutes: 0, RampSeconds: 0),
            IsDimmer: isDimmer));

        ProjectElement member = harness.Session.Current!.Root.DescendantsAndSelf()
            .First(e => e.IsSceneMember && !e.IsSceneShutter);
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, member.Id!.Value)!);

        return harness.Dialogs.LastSceneValueInput?.Title;
    }
}
