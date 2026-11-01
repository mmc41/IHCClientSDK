using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ihc.Stubbing;
using IhcLab;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Lab's title marks a stubbed session: whenever the current settings name a controller-stub endpoint, set again
    /// each time the login changes, and kept even when configuring the services on that endpoint fails. A refused
    /// endpoint leaves nothing to run, and one refused at start leaves nothing to set up.
    /// </summary>
    [TestFixture]
    public class LabTitleTests : AvaloniaTestBase
    {
        private const string Marked = "IHC Lab : CONTROLLER STUB (no real controller) : Endpoint set to ";

        [TestCase("mock://lab")]
        [TestCase("http://s1.controllerstub.invalid")]
        public void AStubEndpoint_IsMarked(string endpoint)
        {
            Assert.That(MainWindow.TitleFor(new IhcSettings { Endpoint = endpoint }), Is.EqualTo(Marked + endpoint));
        }

        [Test]
        public void ARealEndpoint_IsNotMarked()
        {
            Assert.That(MainWindow.TitleFor(new IhcSettings { Endpoint = "https://ihc.invalid" }),
                Is.EqualTo("IHC Lab : Endpoint set to https://ihc.invalid"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task AWindowStartedOnTheRunsStub_IsMarked()
        {
            MainWindow window = await SetupMainWindowAsync();

            Assert.That(window.Title, Is.EqualTo(Marked + Program.config!.ihcSettings.Endpoint));
        }

        /// <summary>
        /// The Setup dialog edits the shared settings in place and then refreshes the login, which is what this
        /// reproduces. A stub endpoint nothing registered is refused when the services are built on it: the title
        /// still names it as a stub, the refusal is reported, and nothing is left to run.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task AStubEndpointThatFailsToConfigure_IsStillMarked_AndLeavesNothingToRun()
        {
            MainWindow window = await SetupMainWindowAsync();

            await WithEndpoint("mock://lab-title-unregistered", () =>
            {
                RefreshLogin(window);

                var viewModel = (MainWindowViewModel)window.DataContext!;
                Assert.Multiple(() =>
                {
                    Assert.That(window.Title, Is.EqualTo(Marked + "mock://lab-title-unregistered"));
                    Assert.That(viewModel.IsErrorVisible, Is.True, "the refusal is reported");
                    Assert.That(window.LabAppService, Is.Null, "no services are left wired");
                    Assert.That(viewModel.LabAppService, Is.Null);
                    Assert.That(window.FindControl<Button>(MainWindowNames.RunButton)!.IsEnabled, Is.False, "Run has nothing to run");
                });
            });
        }

        /// <summary>
        /// The services an endpoint refused at Setup would have replaced were built on the endpoint before the edit,
        /// which may be a controller, and the dialog has already put its new login into the settings they share. So a
        /// refusal drops them: under the stub title the refusal shows, Run reaches neither endpoint. The earlier
        /// endpoint here is a stub of the case's own, so its journal shows what would have reached a controller.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task AnEndpointRefusedAtSetup_LeavesNoCallToTheEndpointBeforeIt()
        {
            var earlier = new ControllerStub();
            using ControllerStubRegistration registration = ControllerStubs.Register(earlier);
            MainWindow window = await SetupMainWindowAsync();
            var viewModel = (MainWindowViewModel)window.DataContext!;
            int sentBefore = 0;

            await WithEndpoint(registration.Endpoint, () =>
            {
                RefreshLogin(window);
                Assert.That(window.LabAppService, Is.Not.Null, "the services were built on the earlier endpoint");
                Program.config!.ihcSettings.Endpoint = "mock://lab-title-refused-after-a-working-one";
                RefreshLogin(window);
                sentBefore = earlier.Journal.All.Count;
            });

            window.RunButtonClickHandler(window, new RoutedEventArgs());
            for (int i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.Multiple(() =>
            {
                Assert.That(window.Title, Is.EqualTo(Marked + "mock://lab-title-refused-after-a-working-one"));
                Assert.That(earlier.Journal.All, Has.Count.EqualTo(sentBefore), "Run sent nothing to the endpoint before the refused one");
                Assert.That(viewModel.LabAppService, Is.Null);
            });
            earlier.VerifyNoFaults();
        }

        /// <summary>
        /// A settings file naming a stub endpoint nothing registered is refused when Lab builds its services at start,
        /// and is repaired by editing the file. So Setup offers nothing: it is disabled, and the start's refusal stays
        /// the error shown instead of one of Setup's own.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task AStaleStubEndpointInTheFile_IsRefusedAtStart_AndLeavesSetupDisabled()
        {
            await WithEndpoint("mock://lab-stale-file", async () =>
            {
                MainWindow window = await SetupMainWindowAsync();
                var viewModel = (MainWindowViewModel)window.DataContext!;

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ErrorWarningText, Does.Contain(nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED)));
                    Assert.That(window.FindControl<MenuItem>(MainWindowNames.SetupMenuItem)!.IsEnabled, Is.False,
                        "the file, not Setup, is where the endpoint is repaired");
                });
            });
        }

        /// <summary>Without the switch the process is no stub process, so a real endpoint reads as a real session.</summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task ARealEndpointEnteredAtRuntime_LosesTheMarker()
        {
            MainWindow window = await SetupMainWindowAsync();

            await WithEndpoint("https://ihc.invalid", () =>
            {
                RefreshLogin(window);
                Assert.That(window.Title, Is.EqualTo("IHC Lab : Endpoint set to https://ihc.invalid"));
            });
        }

        /// <summary>
        /// A process the switch started stays latched to stubs, so a real endpoint entered later is refused, and
        /// the title says so instead of reading as a real session.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task ARealEndpointEnteredInAStubProcess_KeepsTheMarker_AsRefusedUntilRestart()
        {
            MainWindow window = await SetupMainWindowAsync();
            Program.controllerStubProcess = true;
            try
            {
                await WithEndpoint("https://ihc.invalid", () =>
                {
                    RefreshLogin(window);
                    Assert.That(window.Title, Is.EqualTo(
                        "IHC Lab : CONTROLLER STUB (real endpoints refused until restart) : Endpoint set to https://ihc.invalid"));
                });
            }
            finally
            {
                Program.controllerStubProcess = false;
            }
        }

        private static Task WithEndpoint(string endpoint, Action check) =>
            WithEndpoint(endpoint, () =>
            {
                check();
                return Task.CompletedTask;
            });

        /// <summary>Runs <paramref name="check"/> with the run's settings pointed at <paramref name="endpoint"/>,
        /// then puts the run's stub endpoint back, because every later window reads the same settings.</summary>
        private static async Task WithEndpoint(string endpoint, Func<Task> check)
        {
            IhcSettings settings = Program.config!.ihcSettings;
            string? original = settings.Endpoint;
            settings.Endpoint = endpoint;
            try
            {
                await check();
            }
            finally
            {
                settings.Endpoint = original;
            }
        }

        /// <summary>The window's response to a changed login, reached the way the Setup dialog's close reaches it.</summary>
        private static void RefreshLogin(MainWindow window)
        {
            MethodInfo refresh = typeof(MainWindow).GetMethod("LoginUpdated", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(MainWindow), "LoginUpdated");
            try
            {
                refresh.Invoke(window, null);
            }
            catch (TargetInvocationException invocation) when (invocation.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(invocation.InnerException);
            }
        }
    }
}
