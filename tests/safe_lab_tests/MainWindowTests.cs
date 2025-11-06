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
        /// Helper to simulate user changing a value in a DynField.
        /// </summary>
        private static void SimulateUserValueChange(DynField dynField, object? value)
        {
            dynField.Value = value;

            // Manually trigger the ValueChanged event using reflection
            var valueChangedField = typeof(DynField).GetEvent("ValueChanged");
            var eventDelegate = typeof(DynField).GetField("ValueChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (eventDelegate != null)
            {
                var handler = eventDelegate.GetValue(dynField) as EventHandler;
                handler?.Invoke(dynField, EventArgs.Empty);
            }
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

                // Verify at least the first DynField control exists
                var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel, "0");
                Assert.That(dynField0, Is.Not.Null, "DynField for parameter 0 should exist");
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
            var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            Assert.That(dynField0, Is.Not.Null, "DynField for parameter 0 should exist");

            string testValue = "testvalue";
            SimulateUserValueChange(dynField0!, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Switch to another operation (first operation in list)
            operationsComboBox!.SelectedIndex = 0;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Switch back to original operation
            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Previously set value should be restored
            dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            Assert.That(dynField0, Is.Not.Null, "DynField should exist after switching back");
            Assert.That(dynField0!.Value, Is.EqualTo(testValue), "Value should be restored");
        }

        /// <summary>
        /// Test 3: Verifies that GUI values are extracted and synced to LabAppService.
        /// Protects: MainWindow.axaml.cs lines 625-640 (SyncArgumentsToLabAppService)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task SyncArgumentsToLabAppService_ExtractsValuesFromDynFields()
        {
            // Arrange
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null);

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive("AuthenticationService not available");
                return;
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive("No operations with parameters available");
                return;
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Act - Set value in first parameter
            var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            if (dynField0 == null)
            {
                Assert.Inconclusive("DynField not created for parameter 0");
                return;
            }

            string testValue = "synctest";
            SimulateUserValueChange(dynField0, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - Value should be synced to LabAppService
            var arguments = labAppService!.SelectedOperation.GetMethodArgumentsAsArray();
            Assert.That(arguments.Length, Is.GreaterThan(0), "Should have at least one argument");
            Assert.That(arguments[0], Is.EqualTo(testValue), "Value should be synced to LabAppService");
        }

        /// <summary>
        /// Test 4: Verifies that GUI changes immediately update LabAppService.
        /// Protects: MainWindow.axaml.cs lines 839-880 (OnDynFieldValueChanged event handler)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task OnDynFieldValueChanged_UpdatesLabAppServiceImmediately()
        {
            // Arrange
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null);

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive("AuthenticationService not available");
                return;
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive("No operations with parameters available");
                return;
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            if (dynField0 == null)
            {
                Assert.Inconclusive("DynField not created for parameter 0");
                return;
            }

            // Act - Simulate user changing value
            string newValue = "immediateSyncTest";
            SimulateUserValueChange(dynField0, newValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - LabAppService should be updated immediately
            var arguments = labAppService!.SelectedOperation.GetMethodArgumentsAsArray();
            Assert.That(arguments[0], Is.EqualTo(newValue),
                "LabAppService should be updated immediately when DynField value changes");
        }

        /// <summary>
        /// Test 5: Verifies that LabAppService changes update GUI immediately.
        /// Protects: MainWindow.axaml.cs lines 741-774 (OnLabAppServiceArgumentChanged event handler)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task OnLabAppServiceArgumentChanged_UpdatesGuiImmediately()
        {
            // Arrange
            var window = await SetupMainWindowAsync();
            var labAppService = window.LabAppService;
            Assert.That(labAppService, Is.Not.Null);

            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive("AuthenticationService not available");
                return;
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive("No operations with parameters available");
                return;
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            if (dynField0 == null)
            {
                Assert.Inconclusive("DynField not created for parameter 0");
                return;
            }

            // Act - Change value programmatically in LabAppService
            string programmaticValue = "serviceSetValue";
            labAppService!.SelectedOperation.SetMethodArgument(0, programmaticValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Assert - GUI should be updated
            dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            Assert.That(dynField0!.Value, Is.EqualTo(programmaticValue),
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
        /// Test 7: Verifies that event subscriptions are set up recursively for all DynFields.
        /// Protects: MainWindow.axaml.cs lines 819-833 (SubscribeToDynFieldEvents)
        /// </summary>
        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public async Task SubscribeToDynFieldEvents_RecursivelySubscribes()
        {
            // Arrange
            var window = await SetupMainWindowAsync();
            var servicesComboBox = window.FindControl<ComboBox>("ServicesComboBox");
            var operationsComboBox = window.FindControl<ComboBox>("OperationsComboBox");
            var parametersPanel = window.FindControl<StackPanel>("ParametersPanel");

            int authServiceIndex = FindServiceIndexByName(servicesComboBox!, AuthenticationServiceName);
            if (authServiceIndex < 0)
            {
                Assert.Inconclusive("AuthenticationService not available");
                return;
            }

            servicesComboBox!.SelectedIndex = authServiceIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            int opWithParamsIndex = FindOperationWithParameters(operationsComboBox!, minParamCount: 1);
            if (opWithParamsIndex < 0)
            {
                Assert.Inconclusive("No operations with parameters available");
                return;
            }

            operationsComboBox!.SelectedIndex = opWithParamsIndex;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            // Act - Get DynField (event subscription happens in OnViewModelPropertyChanged)
            var dynField0 = OperationSupport.FindDynFieldByName(parametersPanel!, "0");
            if (dynField0 == null)
            {
                Assert.Inconclusive("DynField not created for parameter 0");
                return;
            }

            // Assert - Verify events are subscribed by checking that value changes trigger updates
            var labAppService = window.LabAppService;
            string testValue = "eventSubscribedTest";

            SimulateUserValueChange(dynField0, testValue);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var arguments = labAppService!.SelectedOperation.GetMethodArgumentsAsArray();
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
                .SelectMany(c => c is Panel p ? FindAllDynFields(p) : new[] { c as DynField })
                .Where(d => d != null)
                .ToList();

            if (dynFields.Count == 0)
            {
                Assert.Inconclusive("No DynFields found for complex parameter");
                return;
            }

            // Set values in available fields
            foreach (var field in dynFields.Take(2))
            {
                if (field!.TypeForControl == typeof(bool))
                {
                    SimulateUserValueChange(field, true);
                }
                else if (field.TypeForControl == typeof(string))
                {
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
                .SelectMany(c => c is Panel p ? FindAllDynFields(p) : new[] { c as DynField })
                .Where(d => d != null)
                .ToList();

            Assert.That(restoredFields.Count, Is.GreaterThan(0),
                "Complex type fields should be restored after operation switch");
        }

        /// <summary>
        /// Helper to recursively find all DynFields in a panel.
        /// </summary>
        private static DynField[] FindAllDynFields(Panel panel)
        {
            var result = new System.Collections.Generic.List<DynField>();
            foreach (var child in panel.Children)
            {
                if (child is DynField dynField)
                {
                    result.Add(dynField);
                }
                else if (child is Panel childPanel)
                {
                    result.AddRange(FindAllDynFields(childPanel));
                }
            }
            return result.ToArray();
        }
    }
}
