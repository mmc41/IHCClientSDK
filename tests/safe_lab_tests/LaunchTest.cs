using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using IhcLab;
using System.Threading.Tasks;

namespace Ihc.Tests
{
    /// <summary>
    /// Headless UI tests for IHC Lab application launch and initialization
    /// </summary>
    [TestFixture]
    public class LaunchTests
    {
        [AvaloniaTest]
        public async Task ApplicationCanLaunchSuccessfullyAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            // Act - Show the window and wait for it to be ready
            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Assert - Verify window is visible
            Assert.That(window.IsVisible, Is.True, "MainWindow should be visible after showing");
        }

        [AvaloniaTest]
        public async Task MainWindowShowsWithoutErrorsOrWarningsAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Act - Find the error/warning content TextBlock
            var errorWarningContent = window.FindControl<TextBlock>(MainWindowNames.ErrorWarningContent);

            // Assert - Verify no errors or warnings are displayed
            Assert.That(errorWarningContent, Is.Not.Null, "ErrorWarningContent TextBlock should exist");

            var hasError = !string.IsNullOrWhiteSpace(errorWarningContent?.Text);
            Assert.That(hasError, Is.False,
                $"No errors or warnings should be displayed on launch. Found: {errorWarningContent?.Text}");
        }

        [AvaloniaTest]
        public async Task ServicesComboBoxIsPopulatedAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Act - Find the Services ComboBox
            var servicesComboBox = window.FindControl<ComboBox>(MainWindowNames.ServicesComboBox);

            // Assert - Verify ComboBox exists and is populated
            Assert.That(servicesComboBox, Is.Not.Null, "ServicesComboBox should exist");
            Assert.That(servicesComboBox?.ItemCount, Is.GreaterThan(0),
                "ServicesComboBox should contain service items");

            // Verify a service is pre-selected (constructor sets SelectedIndex = 0)
            Assert.That(servicesComboBox?.SelectedIndex, Is.GreaterThanOrEqualTo(0),
                "ServicesComboBox should have a service selected by default");
            Assert.That(servicesComboBox?.SelectedItem, Is.Not.Null,
                "ServicesComboBox should have a selected service item");
        }

        [AvaloniaTest]
        public async Task OperationsComboBoxIsPopulatedAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Act - Find the Operations ComboBox
            var operationsComboBox = window.FindControl<ComboBox>(MainWindowNames.OperationsComboBox);

            // Assert - Verify ComboBox exists and is populated
            Assert.That(operationsComboBox, Is.Not.Null, "OperationsComboBox should exist");
            Assert.That(operationsComboBox?.ItemCount, Is.GreaterThan(0),
                "OperationsComboBox should contain operation items for the selected service");

            // Verify the operations are valid metadata objects
            var firstItem = operationsComboBox?.Items.Cast<object>().FirstOrDefault();
            Assert.That(firstItem, Is.Not.Null,
                "OperationsComboBox should have valid operation metadata items");
        }

        [AvaloniaTest]
        public async Task BothDropdownsAreNonEmptyAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Act - Find both ComboBoxes
            var servicesComboBox = window.FindControl<ComboBox>(MainWindowNames.ServicesComboBox);
            var operationsComboBox = window.FindControl<ComboBox>(MainWindowNames.OperationsComboBox);

            // Assert - Verify both dropdowns are populated
            Assert.Multiple(() =>
            {
                Assert.That(servicesComboBox?.ItemCount, Is.GreaterThan(0),
                    "ServicesComboBox should be non-empty");
                Assert.That(operationsComboBox?.ItemCount, Is.GreaterThan(0),
                    "OperationsComboBox should be non-empty");
            });
        }

        [AvaloniaTest]
        public async Task MainWindowHasAllExpectedControlsAsync()
        {
            // Arrange - Create and initialize the main window
            var window = await new MainWindow().Start();

            window.Show();
            Dispatcher.UIThread.RunJobs(); // Process all pending UI operations

            // Act - Find all expected controls
            var servicesComboBox = window.FindControl<ComboBox>(MainWindowNames.ServicesComboBox);
            var operationsComboBox = window.FindControl<ComboBox>(MainWindowNames.OperationsComboBox);
            var runButton = window.FindControl<Button>(MainWindowNames.RunButton);
            var parametersPanel = window.FindControl<StackPanel>(MainWindowNames.ParametersPanel);
            var outputTextBlock = window.FindControl<TextBlock>(MainWindowNames.Output);
            var errorWarningContent = window.FindControl<TextBlock>(MainWindowNames.ErrorWarningContent);

            // Assert - Verify all critical UI controls exist
            Assert.Multiple(() =>
            {
                Assert.That(servicesComboBox, Is.Not.Null, "ServicesComboBox should exist");
                Assert.That(operationsComboBox, Is.Not.Null, "OperationsComboBox should exist");
                Assert.That(runButton, Is.Not.Null, "RunButton should exist");
                Assert.That(parametersPanel, Is.Not.Null, "ParametersPanel should exist");
                Assert.That(outputTextBlock, Is.Not.Null, "Output TextBlock should exist");
                Assert.That(errorWarningContent, Is.Not.Null, "ErrorWarningContent TextBlock should exist");
            });
        }
    }
}
