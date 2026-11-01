using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Stubbing;
using NUnit.Framework;

namespace safe_visual_stubbed_tests;

/// <summary>
/// OpenVisual composed over a controller stub, the way start-up composes it once the switch has chosen the
/// controller: the configuration is rebuilt through its copying constructor, and the stub flag is computed once
/// from the settings the services would be built on. What this establishes is the composition and the indicator;
/// routing, refusals and round trips are the SDK's, and are tested where the SDK is.
/// </summary>
[TestFixture]
public class OpenVisualStubIndicatorTests : AvaloniaTestBase
{
    private const string Suffix = " – STUB-controller (ingen rigtig controller)";

    /// <summary>Settings naming <paramref name="endpoint"/>, checked to target a stub before anything uses them.</summary>
    private static IhcSettings StubSettings(string endpoint)
    {
        var settings = new IhcSettings { Endpoint = endpoint, UserName = "stub-user", Password = "stub-pw" };
        Assert.That(settings.IsStubConfigured(), Is.True, $"{endpoint} must be a stub endpoint before any call is made");
        return settings;
    }

    /// <summary>The configuration start-up builds when no settings file exists, with <paramref name="controller"/>
    /// in place of its blank controller settings.</summary>
    private static AppConfiguration ComposedConfiguration(IhcSettings controller)
    {
        using var noFile = new ScratchDir("ihc_ov_stub_");
        return new AppConfiguration(new AppConfiguration(noFile.Path), controller);
    }

    private static MainWindowViewModel ComposedViewModel(ShellHarness harness, IhcSettings controller) =>
        harness.CreateViewModel(config: ComposedConfiguration(controller), controllerStubInUse: controller.IsStubConfigured());

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task InProcess_TheWindowTitle_CarriesTheStubSuffix()
    {
        var stub = new ControllerStub();
        using ControllerStubRegistration registration = ControllerStubs.Register(stub);
        IhcSettings settings = StubSettings(registration.Endpoint);
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = ComposedViewModel(harness, settings);
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();

        Assert.That(window.Title, Is.EqualTo("unavngivet - IHC OpenVisual" + Suffix));
        Assert.That(stub.Journal.All, Is.Empty, "composing the window calls nothing");
    }

    [Test]
    public async Task BlankSettings_LeaveTheTitleByteIdentical()
    {
        var blank = new IhcSettings();
        Assert.That(blank.IsStubConfigured(), Is.False, "the control: blank settings target no stub");
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = ComposedViewModel(harness, blank);
        await viewModel.InitializeAsync();

        Assert.That(viewModel.Title, Is.EqualTo("unavngivet - IHC OpenVisual"));
    }

    /// <summary>OpenVisual does not transfer projects yet, so a stub changes nothing about Send and Retrieve: both
    /// still refuse, and the stub hears nothing.</summary>
    [Test]
    public async Task InProcess_SendAndRetrieve_StillRefuse_AndTheStubHearsNothing()
    {
        var stub = new ControllerStub();
        using ControllerStubRegistration registration = ControllerStubs.Register(stub);
        IhcSettings settings = StubSettings(registration.Endpoint);
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = ComposedViewModel(harness, settings);
        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SendProjectCommand.CanExecute(null), Is.False, "Send");
            Assert.That(viewModel.RetrieveProjectCommand.CanExecute(null), Is.False, "Retrieve");
            Assert.That(stub.Journal.All, Is.Empty);
        });
    }

    [Test]
    public async Task InProcess_TheSettingsText_ShowsTheStubEndpoint()
    {
        using ControllerStubRegistration registration = ControllerStubs.Register(new ControllerStub());
        IhcSettings settings = StubSettings(registration.Endpoint);
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = ComposedViewModel(harness, settings);
        await viewModel.InitializeAsync();

        await viewModel.ShowSettingsCommand.ExecuteAsync(null);

        Assert.That(harness.Dialogs.LastSettingsText, Does.Contain("Slutpunkt: " + registration.Endpoint)
            .And.Contain("Indstillingsfil: (ingen — bruger standardværdier)"));
    }

    /// <summary>A <c>mock://</c> endpoint nothing registered still names a stub, whose every call the SDK refuses,
    /// so the title marks it: it tells the person nothing reaches a controller.</summary>
    [Test]
    public async Task AnUnregisteredMockEndpoint_IsStillMarked()
    {
        IhcSettings settings = StubSettings("mock://t");
        using var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = ComposedViewModel(harness, settings);
        await viewModel.InitializeAsync();

        Assert.That(viewModel.Title, Is.EqualTo("unavngivet - IHC OpenVisual" + Suffix));
    }

    /// <summary>The fail-closed fixture's positive control: a settings file planted in a folder makes its check
    /// refuse, and the same folder without one passes, so the check is not a refusal of everything.</summary>
    [Test]
    public void TheFixtureCheck_RefusesAPlantedSettingsFile()
    {
        using var folder = new ScratchDir("ihc_ov_stub_planted_");
        Assert.That(NoSettingsFileBesideTheSuite.SettingsFileRefusal(folder.Path), Is.Null, "an empty folder");

        File.WriteAllText(Path.Combine(folder.Path, NoSettingsFileBesideTheSuite.SettingsFileName), "{}");

        Assert.That(NoSettingsFileBesideTheSuite.SettingsFileRefusal(folder.Path), Does.Contain(NoSettingsFileBesideTheSuite.SettingsFileName));
    }
}
