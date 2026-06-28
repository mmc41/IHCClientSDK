using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;
using IhcLab;
using Ihc;
using Ihc.App;

namespace Ihc.Tests
{
    /// <summary>
    /// GUI-level smoke tests for the IHC Lab application running against the full set of mocked IHC services.
    ///
    /// <para>
    /// These drive the real MainWindow / ViewModel exactly as a user would: selecting each service and each
    /// operation through the ComboBoxes and (for one operation) clicking Run. They verify the mocked fakes
    /// provide enough coverage that the whole app lights up - every operation renders its parameter controls
    /// without error, and an operation can be executed end-to-end to produce output. Operation execution itself
    /// is exhaustively covered at the service layer by <c>LabSmokeTests</c>; this focuses on the GUI wiring.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LabGuiSmokeTests : AvaloniaTestBase
    {
        /// <summary>Flush queued dispatcher work and let a render pass complete.</summary>
        private static async Task Pump()
        {
            Dispatcher.UIThread.RunJobs();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        private static int IndexOfService(ComboBox combo, string displayName)
        {
            var items = combo.Items.Cast<LabAppService.ServiceItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
                if (items[i].DisplayName == displayName)
                    return i;
            return -1;
        }

        private static int IndexOfOperation(ComboBox combo, string displayName)
        {
            var items = combo.Items.Cast<LabAppService.OperationItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
                if (items[i].DisplayName == displayName)
                    return i;
            return -1;
        }

        /// <summary>
        /// Every service and every operation the Lab exposes can be selected through the GUI ComboBoxes, and
        /// each operation's parameter controls are generated without surfacing an error. This dynamically
        /// exercises the parameter-control generation for all ~150 operations across all 13 mocked services.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task EveryServiceAndOperation_CanBeSelectedAndRendered_WithoutError()
        {
            var window = await SetupMainWindowAsync();
            var vm = (MainWindowViewModel)window.DataContext!;
            var servicesCombo = window.FindControl<ComboBox>("ServicesComboBox")!;
            var operationsCombo = window.FindControl<ComboBox>("OperationsComboBox")!;
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel")!;

            Assert.That(servicesCombo.Items.Count, Is.EqualTo(13), "All 13 mocked services should be listed in the GUI");

            var failures = new List<string>();
            int serviceCount = servicesCombo.Items.Count;

            for (int si = 0; si < serviceCount; si++)
            {
                servicesCombo.SelectedIndex = si;
                await Pump();

                string serviceName = window.LabAppService!.Services[si].DisplayName;
                int opCount = operationsCombo.Items.Count;

                if (opCount == 0)
                {
                    failures.Add($"{serviceName}: no operations listed");
                    continue;
                }

                for (int oi = 0; oi < opCount; oi++)
                {
                    operationsCombo.SelectedIndex = oi;
                    await Pump();

                    var op = window.LabAppService.SelectedOperation;
                    string opName = op.DisplayName;

                    if (vm.IsErrorVisible)
                        failures.Add($"{serviceName}.{opName}: error shown after selection: {vm.ErrorWarningText}");

                    // Parameters other than the harness-injected CancellationToken should each yield a control.
                    int renderableParams = op.OperationMetadata.Parameters.Count(p => p.Type != typeof(CancellationToken));
                    if (renderableParams > 0 && parametersPanel.Children.Count == 0)
                        failures.Add($"{serviceName}.{opName}: has {renderableParams} renderable parameter(s) but no controls were generated");
                }
            }

            Assert.That(failures, Is.Empty,
                "The GUI failed to render some operations:\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// Selecting a service + operation and clicking Run executes the operation against the mocked service
        /// and shows output - smoke-testing the full GUI run path (RunButton -> DynCallSelectedOperation ->
        /// output rendering) against the fakes.
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task RunButton_ExecutesSelectedOperation_AndShowsOutput()
        {
            var window = await SetupMainWindowAsync();
            var vm = (MainWindowViewModel)window.DataContext!;
            var servicesCombo = window.FindControl<ComboBox>("ServicesComboBox")!;
            var operationsCombo = window.FindControl<ComboBox>("OperationsComboBox")!;

            int serviceIndex = IndexOfService(servicesCombo, "ControllerService");
            Assert.That(serviceIndex, Is.GreaterThanOrEqualTo(0), "ControllerService should be available");
            servicesCombo.SelectedIndex = serviceIndex;
            await Pump();

            int opIndex = IndexOfOperation(operationsCombo, "GetControllerState");
            Assert.That(opIndex, Is.GreaterThanOrEqualTo(0), "GetControllerState should be available");
            operationsCombo.SelectedIndex = opIndex;
            await Pump();

            // RunButtonClickHandler is async void; trigger it then pump the dispatcher until it completes.
            window.RunButtonClickHandler(window, new RoutedEventArgs());
            for (int i = 0; i < 100 && !vm.IsOutputVisible && !vm.IsErrorVisible; i++)
                await Pump();

            Assert.That(vm.IsErrorVisible, Is.False, $"Run produced an error: {vm.ErrorWarningText}");
            Assert.That(vm.IsOutputVisible, Is.True, "Run should produce visible output");
            Assert.That(vm.OutputText, Does.Contain("Ready"),
                $"GetControllerState should report the mocked Ready state, but output was: {vm.OutputText}");
        }
    }
}
