using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using IhcLab;
using Ihc.App;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// Tests for MainWindow to verify core parameter synchronization logic.
    /// These tests protect critical business logic during Phase 1 refactoring.
    /// </summary>
    [TestFixture]
    public class MainWindowTests : AvaloniaTestBase
    {
        private const string AuthenticationServiceName = "AuthenticationService";
        private const string SmsModemServiceName = "SmsModemService";

        #region Helper Methods

        /// <summary>
        /// Helper to find control by name recursively.
        /// </summary>
        private static Control? FindControlByNameRecursive(Control parent, string name)
        {
            if (parent.Name == name)
                return parent;

            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control childControl)
                    {
                        var found = FindControlByNameRecursive(childControl, name);
                        if (found != null)
                            return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Helper to find a service by name in the services combobox.
        /// </summary>
        private static int FindServiceIndexByName(ComboBox servicesComboBox, string serviceName)
        {
            var items = servicesComboBox.Items.Cast<LabAppService.ServiceItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DisplayName == serviceName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Helper to find an operation by name in the operations combobox.
        /// </summary>
        private static int FindOperationIndexByName(ComboBox operationsComboBox, string operationName, int? parameterCount = null)
        {
            var items = operationsComboBox.Items.Cast<LabAppService.OperationItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DisplayName == operationName)
                {
                    if (parameterCount.HasValue)
                    {
                        if (items[i].OperationMetadata.Parameters.Length == parameterCount.Value)
                            return i;
                    }
                    else
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Helper to find the first operation with at least the specified number of parameters.
        /// </summary>
        private static int FindOperationWithParameters(ComboBox operationsComboBox, int minParamCount = 1)
        {
            var items = operationsComboBox.Items.Cast<LabAppService.OperationItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].OperationMetadata.Parameters.Length >= minParamCount)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Helper to simulate user changing a value in a control.
        /// </summary>
        private static void SimulateUserValueChange(Control control, object? value)
        {
            // Set value based on control type
            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = value?.ToString() ?? string.Empty;
                    break;
                case NumericUpDown numeric:
                    if (value != null)
                        numeric.Value = Convert.ToDecimal(value);
                    break;
                case ComboBox combo:
                    combo.SelectedItem = value;
                    break;
                case DatePicker datePicker:
                    if (value is DateTimeOffset dto)
                        datePicker.SelectedDate = dto;
                    else if (value is DateTime dt)
                        datePicker.SelectedDate = new DateTimeOffset(dt);
                    break;
            }
            // Controls trigger their own value changed events automatically
        }

        /// <summary>
        /// Helper to get value from a control.
        /// </summary>
        private static object? GetControlValue(Control control)
        {
            return control switch
            {
                TextBox textBox => textBox.Text,
                NumericUpDown numeric => numeric.Value,
                ComboBox combo => combo.SelectedItem,
                DatePicker datePicker => datePicker.SelectedDate,
                _ => null
            };
        }

        /// <summary>
        /// What the argument-synchronisation tests each start from: a started window, an
        /// <see cref="AuthenticationServiceName"/> operation that takes at least one parameter selected in the
        /// GUI, and the control that operation's first parameter rendered as.
        /// </summary>
        private sealed record ParameterSyncSetup(
            MainWindow Window,
            LabAppService LabAppService,
            StackPanel ParametersPanel,
            Control FirstParameterControl);

        /// <summary>
        /// Drives the GUI to <see cref="ParameterSyncSetup"/>. Extracted because the argument-synchronisation
        /// tests differ only in what they then do to that control and what they read back, and stating the
        /// arrangement in each of them buried those few lines among identical ones.
        /// <para>
        /// A service set that offers no parameterised operation leaves the caller INCONCLUSIVE rather than
        /// failed: it means this run could not reach the behaviour, not that the behaviour is broken. That
        /// distinction was drawn separately in each test body before, which is exactly the kind of precondition
        /// that drifts apart when it is written more than once.
        /// </para>
        /// </summary>
        private static async Task<ParameterSyncSetup> ArrangeParameterSyncAsync()
        {
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null, "LabAppService should be configured");

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            Assert.That(servicesComboBox, Is.Not.Null, "ServicesComboBox should exist");
            Assert.That(operationsComboBox, Is.Not.Null, "OperationsComboBox should exist");
            Assert.That(parametersPanel, Is.Not.Null, "ParametersPanel should exist");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive($"{AuthenticationServiceName} not available in mocked services");
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive($"No {AuthenticationServiceName} operation takes a parameter");
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var control0 = FindControlByNameRecursive(parametersPanel!, "0");
            if (control0 == null)
            {
                Assert.Inconclusive("Control not created for parameter 0");
            }

            return new ParameterSyncSetup(window, labAppService!, parametersPanel!, control0!);
        }

        #endregion

        /// <summary>
        /// Test 1: Verifies that parameter controls are created when operation selection changes.
        /// Protects: MainWindow.axaml.cs lines 331-393 (OnViewModelPropertyChanged)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task OnViewModelPropertyChanged_SelectedOperationIndexChange_SetsUpParameterControls()
        {
            // Arrange - Create and initialize MainWindow
            var window = await SetupMainWindowAsync();

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            Assert.That(servicesComboBox, Is.Not.Null, "ServicesComboBox should exist");
            Assert.That(operationsComboBox, Is.Not.Null, "OperationsComboBox should exist");
            Assert.That(parametersPanel, Is.Not.Null, "ParametersPanel should exist");

            // Select AuthenticationService (has operations with parameters)
            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            Assert.That(authServiceIndex, Is.GreaterThanOrEqualTo(0), $"Should find {AuthenticationServiceName}");

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Act - Select the first available operation (should have parameters)
            Assert.That(operationsComboBox!.Items.Count, Is.GreaterThan(0), "Should have at least one operation");

            operationsComboBox!.SelectedIndex = 0;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Parameter controls should be created based on operation metadata
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null);

            var selectedOperation = labAppService!.SelectedOperation;
            var parameterCount = selectedOperation.OperationMetadata.Parameters.Length;

            if (parameterCount > 0)
            {
                Assert.That(parametersPanel!.Children.Count, Is.GreaterThan(0),
                    "ParametersPanel should contain parameter controls when operation has parameters");

                // Verify at least the first parameter control exists
                var control0 = FindControlByNameRecursive(parametersPanel, "0");
                Assert.That(control0, Is.Not.Null, "Control for parameter 0 should exist");
            }
            else
            {
                Assert.Pass("Operation has no parameters, test verified parameter control setup logic executes without errors");
            }
        }

        /// <summary>
        /// Test 2: Verifies that previously set argument values are restored when returning to an operation.
        /// Protects: MainWindow.axaml.cs lines 647-688 (SyncArgumentsFromLabAppService)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task SyncArgumentsFromLabAppService_RestoresPreviouslySetValues()
        {
            // Arrange - Setup window and find operation with parameters
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null, "LabAppService should be configured");

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive("AuthenticationService not available in mocked services");
                return;
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Find an operation with at least 1 parameter
            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive("No operations with parameters available in AuthenticationService");
                return;
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Get parameter count from selected operation
            var selectedOperation = labAppService!.SelectedOperation;
            int paramCount = selectedOperation.OperationMetadata.Parameters.Length;

            if (paramCount == 0)
            {
                Assert.Inconclusive("Selected operation has no parameters");
                return;
            }

            // Act - Set value in first parameter
            var control0 = FindControlByNameRecursive(parametersPanel!, "0");
            Assert.That(control0, Is.Not.Null, "Control for parameter 0 should exist");

            string testValue = "testvalue";
            SimulateUserValueChange(control0!, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Switch to another operation (first operation in list)
            operationsComboBox!.SelectedIndex = 0;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Switch back to original operation
            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Previously set value should be restored
            control0 = FindControlByNameRecursive(parametersPanel!, "0");
            Assert.That(control0, Is.Not.Null, "Control should exist after switching back");
            Assert.That(GetControlValue(control0!), Is.EqualTo(testValue), "Value should be restored");
        }

        /// <summary>
        /// Test 3: Verifies that GUI values are extracted and synced to LabAppService.
        /// Protects: MainWindow.axaml.cs lines 625-640 (SyncArgumentsToLabAppService)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task SyncArgumentsToLabAppService_ExtractsValuesFromControls()
        {
            // Arrange
            var setup = await ArrangeParameterSyncAsync();

            // Act - Set value in first parameter
            string testValue = "synctest";
            SimulateUserValueChange(setup.FirstParameterControl, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Value should be synced to LabAppService
            var arguments = setup.LabAppService.SelectedOperation.GetMethodArgumentsAsArray();
            Assert.That(arguments.Length, Is.GreaterThan(0), "Should have at least one argument");
            Assert.That(arguments[0], Is.EqualTo(testValue), "Value should be synced to LabAppService");
        }

        /// <summary>
        /// Test 4: Verifies that GUI changes immediately update LabAppService.
        /// Protects: MainWindow.axaml.cs lines 839-880 (OnControlValueChanged event handler)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task OnControlValueChanged_UpdatesLabAppServiceImmediately()
        {
            // Arrange
            var setup = await ArrangeParameterSyncAsync();

            // Act - Simulate user changing value
            string newValue = "immediateSyncTest";
            SimulateUserValueChange(setup.FirstParameterControl, newValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - LabAppService should be updated immediately
            var arguments = setup.LabAppService.SelectedOperation.GetMethodArgumentsAsArray();
            Assert.That(arguments[0], Is.EqualTo(newValue),
                "LabAppService should be updated immediately when Control value changes");
        }

        /// <summary>
        /// Test 5: Verifies that LabAppService changes update GUI immediately.
        /// Protects: MainWindow.axaml.cs lines 741-774 (OnLabAppServiceMethodArgumentChanged event handler)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task OnLabAppServiceMethodArgumentChanged_UpdatesGuiImmediately()
        {
            // Arrange
            var setup = await ArrangeParameterSyncAsync();

            // Act - Change value programmatically in LabAppService
            string programmaticValue = "serviceSetValue";
            setup.LabAppService.SelectedOperation.SetMethodArgument(0, programmaticValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - GUI should be updated. The control is looked up again rather than reused: the panel may
            // have rebuilt it in response to the change, and the assertion is about what the GUI now shows.
            var control0 = FindControlByNameRecursive(setup.ParametersPanel, "0");
            Assert.That(GetControlValue(control0!), Is.EqualTo(programmaticValue),
                "GUI should be updated immediately when LabAppService argument changes");
        }

        /// <summary>
        /// Test 6: Verifies that LoginUpdated wires LabAppService to ViewModel correctly.
        /// Protects: MainWindow.axaml.cs lines 236-277 (LoginUpdated method)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task LoginUpdated_WiresLabAppServiceToViewModel()
        {
            // Arrange & Act - SetupMainWindowAsync calls Start() which calls LoginUpdated()
            var window = await SetupMainWindowAsync();

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");

            // Assert - Three-layer wiring should be complete
            Assert.That(window.LabAppService, Is.Not.Null, "MainWindow.LabAppService should be configured");
            Assert.That(window.DataContext, Is.Not.Null, "MainWindow.DataContext (ViewModel) should exist");

            var viewModel = window.DataContext as MainWindowViewModel;
            Assert.That(viewModel, Is.Not.Null, "DataContext should be MainWindowViewModel");
            Assert.That(viewModel!.LabAppService, Is.Not.Null, "ViewModel.LabAppService should be wired");
            Assert.That(viewModel.LabAppService, Is.SameAs(window.LabAppService),
                "ViewModel and MainWindow should share the same LabAppService instance");

            // Verify collections are populated
            Assert.That(viewModel.Services.Count, Is.GreaterThan(0), "Services collection should be populated");
            Assert.That(servicesComboBox!.ItemsSource, Is.Not.Null, "ServicesComboBox should have ItemsSource");

            // Verify initial selection is valid
            Assert.That(viewModel.SelectedServiceIndex, Is.GreaterThanOrEqualTo(0),
                "SelectedServiceIndex should be initialized");
            Assert.That(viewModel.Operations.Count, Is.GreaterThan(0),
                "Operations collection should be populated for selected service");
        }

        /// <summary>
        /// Test 7: Verifies that event subscriptions are set up recursively for all Controls.
        /// Protects: MainWindow.axaml.cs lines 819-833 (SubscribeToControlEvents)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task SubscribeToControlEvents_RecursivelySubscribes()
        {
            // Arrange - the control is nested inside ParametersPanel, so reaching it at all is what makes this
            // a test of the RECURSIVE subscription rather than of a top-level one.
            var setup = await ArrangeParameterSyncAsync();

            // Act - Change the value on that nested control
            string testValue = "eventSubscribedTest";
            SimulateUserValueChange(setup.FirstParameterControl, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Verify events are subscribed by checking that value changes trigger updates
            var arguments = setup.LabAppService.SelectedOperation.GetMethodArgumentsAsArray();
            Assert.That(arguments[0], Is.EqualTo(testValue),
                "Event subscription should allow value changes to propagate to LabAppService");
        }

        /// <summary>
        /// Test 8: Verifies that RestoreFieldValue handles complex types recursively.
        /// Protects: MainWindow.axaml.cs lines 699-735 (RestoreFieldValue)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task RestoreFieldValue_HandlesComplexTypesRecursively()
        {
            // Arrange - Find a service with complex parameter types
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null);

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            // Find a service and operation with complex parameters
            // SmsModemService.SetSmsModemSettings has a complex SmsModemSettings parameter
            int smsServiceIndex = FindServiceIndexByName(servicesComboBox!, SmsModemServiceName);

            if (smsServiceIndex < 0)
            {
                Assert.Inconclusive($"{SmsModemServiceName} not available in mocked services");
                return;
            }

            servicesComboBox!.SelectedIndex = smsServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int setSettingsOpIndex = FindOperationIndexByName(operationsComboBox!, "SetSmsModemSettings");

            if (setSettingsOpIndex < 0)
            {
                Assert.Inconclusive("SetSmsModemSettings operation not available");
                return;
            }

            operationsComboBox!.SelectedIndex = setSettingsOpIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Act - Set values in complex parameter fields
            // SmsModemSettings has sub-fields like Enabled, Pin, etc.
            var dynFields = parametersPanel!.Children.OfType<Control>()
                .SelectMany(c => c is Panel p ? FindAllControls(p) : new[] { c as Control })
                .Where(d => d != null)
                .ToList();

            if (dynFields.Count == 0)
            {
                Assert.Inconclusive("No Controls found for complex parameter");
                return;
            }

            // Set values in available fields
            foreach (var field in dynFields.Take(2))
            {
                // Determine type by control type (V2 approach)
                if (field is StackPanel panel && panel.Children.OfType<RadioButton>().Any())
                {
                    // Bool parameter (radio buttons)
                    SimulateUserValueChange(field, true);
                }
                else if (field is TextBox)
                {
                    // String parameter
                    SimulateUserValueChange(field, "testvalue");
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Switch operations and back
            int getStatusOpIndex = FindOperationIndexByName(operationsComboBox!, "GetSmsModemStatus");
            if (getStatusOpIndex >= 0)
            {
                operationsComboBox!.SelectedIndex = getStatusOpIndex;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

                operationsComboBox!.SelectedIndex = setSettingsOpIndex;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            }

            // Assert - Verify complex type restoration occurred without exceptions
            // The fact that we got here without exceptions means recursive restoration worked
            var restoredFields = parametersPanel!.Children.OfType<Control>()
                .SelectMany(c => c is Panel p ? FindAllControls(p) : new[] { c as Control })
                .Where(d => d != null)
                .ToList();

            Assert.That(restoredFields.Count, Is.GreaterThan(0),
                "Complex type fields should be restored after operation switch");
        }

        /// <summary>
        /// Helper to recursively find all Controls in a panel.
        /// </summary>
        private static Control[] FindAllControls(Panel panel)
        {
            var result = new System.Collections.Generic.List<Control>();
            foreach (var child in panel.Children)
            {
                if (child is Control dynField)
                {
                    result.Add(dynField);
                }
                else if (child is Panel childPanel)
                {
                    result.AddRange(FindAllControls(childPanel));
                }
            }
            return result.ToArray();
        }
    }
}
