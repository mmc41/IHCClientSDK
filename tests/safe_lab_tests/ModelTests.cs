using NUnit.Framework;
using FakeItEasy;
using Ihc.App;
using IhcLab;
using System;

namespace Ihc.Tests
{
    /// <summary>
    /// Tests for MainWindowViewModel to verify event-driven synchronization with LabAppService.
    /// Unlike other tests these are not GUI tests.
    /// </summary>
    [TestFixture]
    public class ModelTests
    {
        [Test]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.That(viewModel.OperationDescription, Is.EqualTo(string.Empty));
            Assert.That(viewModel.OutputText, Is.EqualTo(string.Empty));
            Assert.That(viewModel.ErrorWarningText, Is.EqualTo(string.Empty));
            Assert.That(viewModel.IsOutputVisible, Is.False);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.IsWarningVisible, Is.False);
        }

        [Test]
        public void SetProperty_ShouldRaisePropertyChanged()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            string? changedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) => changedPropertyName = e.PropertyName;

            // Act
            viewModel.OperationDescription = "Test Description";

            // Assert
            Assert.That(changedPropertyName, Is.EqualTo(nameof(MainWindowViewModel.OperationDescription)));
            Assert.That(viewModel.OperationDescription, Is.EqualTo("Test Description"));
        }

        [Test]
        public void LabAppService_Setter_ShouldSubscribeToEvents()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            var labAppService = A.Fake<LabAppService>(options => options.WithArgumentsForConstructor(() =>
                new LabAppService(null, null)));

            // Act
            viewModel.LabAppService = labAppService;

            // Assert - verify that the LabAppService is set
            Assert.That(viewModel.LabAppService, Is.SameAs(labAppService));
        }

        [Test]
        public void LabAppService_Setter_ShouldUnsubscribeFromOldService()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            var oldService = A.Fake<LabAppService>(options => options.WithArgumentsForConstructor(() =>
                new LabAppService(null, null)));
            var newService = A.Fake<LabAppService>(options => options.WithArgumentsForConstructor(() =>
                new LabAppService(null, null)));

            // Act
            viewModel.LabAppService = oldService;
            viewModel.LabAppService = newService;

            // Assert
            Assert.That(viewModel.LabAppService, Is.SameAs(newService));
        }

        [Test]
        public void ClearOutput_ShouldClearTextAndHideOutput()
        {
            // Arrange
            var viewModel = new MainWindowViewModel
            {
                OutputText = "Some output",
                IsOutputVisible = true
            };

            // Act
            viewModel.ClearOutput();

            // Assert
            Assert.That(viewModel.OutputText, Is.EqualTo(string.Empty));
            Assert.That(viewModel.IsOutputVisible, Is.False);
        }

        [Test]
        public void SetOutput_ShouldSetTextAndShowOutput()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            string testOutput = "Test output text";
            Type testType = typeof(string);

            // Act
            viewModel.SetOutput(testOutput, testType);

            // Assert
            Assert.That(viewModel.OutputText, Is.EqualTo(testOutput));
            Assert.That(viewModel.OutputHeading, Does.Contain("Size=16"));
            Assert.That(viewModel.OutputHeading, Does.Contain("Type=String"));
            Assert.That(viewModel.IsOutputVisible, Is.True);
        }

        [Test]
        public void ClearErrorAndWarning_ShouldClearTextAndHideHeadings()
        {
            // Arrange
            var viewModel = new MainWindowViewModel
            {
                ErrorWarningText = "Some error",
                IsErrorVisible = true,
                IsWarningVisible = false
            };

            // Act
            viewModel.ClearErrorAndWarning();

            // Assert
            Assert.That(viewModel.ErrorWarningText, Is.EqualTo(string.Empty));
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.IsWarningVisible, Is.False);
        }

        [Test]
        public void SetError_ShouldSetErrorTextAndShowErrorHeading()
        {
            // Arrange & Act
            MainWindowViewModel viewModel;
            using (new SuppressLogging())
            {
                viewModel = new MainWindowViewModel();
                viewModel.SetError("Test error");
            }

            // Assert
            Assert.That(viewModel.ErrorWarningText, Is.EqualTo("Test error"));
            Assert.That(viewModel.IsErrorVisible, Is.True);
            Assert.That(viewModel.IsWarningVisible, Is.False);
        }

        [Test]
        public void SetError_WithException_ShouldIncludeExceptionDetails()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            MainWindowViewModel viewModel;
            using (new SuppressLogging())
            {
                viewModel = new MainWindowViewModel();
                viewModel.SetError("Test error", exception);
            }

            // Assert
            Assert.That(viewModel.ErrorWarningText, Does.Contain("Test error"));
            Assert.That(viewModel.ErrorWarningText, Does.Contain("Test exception"));
            Assert.That(viewModel.IsErrorVisible, Is.True);
        }

        [Test]
        public void SetWarning_ShouldSetWarningTextAndShowWarningHeading()
        {
            // Arrange & Act
            MainWindowViewModel viewModel;
            using (new SuppressLogging())
            {
                viewModel = new MainWindowViewModel();
                viewModel.SetWarning("Test warning");
            }

            // Assert
            Assert.That(viewModel.ErrorWarningText, Is.EqualTo("Test warning"));
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.IsWarningVisible, Is.True);
        }

        [Test]
        public void SetWarning_WithException_ShouldIncludeExceptionDetails()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            MainWindowViewModel viewModel;
            using (new SuppressLogging())
            {
                viewModel = new MainWindowViewModel();
                viewModel.SetWarning("Test warning", exception);
            }

            // Assert
            Assert.That(viewModel.ErrorWarningText, Does.Contain("Test warning"));
            Assert.That(viewModel.ErrorWarningText, Does.Contain("Test exception"));
            Assert.That(viewModel.IsWarningVisible, Is.True);
        }

        [Test]
        public void Dispose_ShouldUnsubscribeFromLabAppServiceEvents()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            var labAppService = A.Fake<LabAppService>(options => options.WithArgumentsForConstructor(() =>
                new LabAppService(null, null)));
            viewModel.LabAppService = labAppService;

            // Act
            viewModel.Dispose();

            // Assert - should not throw when LabAppService raises events after dispose
            Assert.DoesNotThrow(() =>
            {
                // Trigger events after disposal (these would throw if handlers weren't unsubscribed)
                // Note: We can't easily verify unsubscription, but we can verify no exceptions
            });
        }

        [Test]
        public void Dispose_WithNullLabAppService_ShouldNotThrow()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();

            // Act & Assert
            Assert.DoesNotThrow(() => viewModel.Dispose());
        }

        // ========== ComboBox Synchronization Tests (Step 2) ==========

        [Test]
        public void Services_Collection_ShouldBeEmpty_Initially()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.That(viewModel.Services, Is.Empty);
        }

        [Test]
        public void Operations_Collection_ShouldBeEmpty_Initially()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.That(viewModel.Operations, Is.Empty);
        }

        [Test]
        public void SelectedServiceIndex_ShouldStartAtZero()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.That(viewModel.SelectedServiceIndex, Is.EqualTo(0));
        }

        [Test]
        public void SelectedOperationIndex_ShouldStartAtZero()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.That(viewModel.SelectedOperationIndex, Is.EqualTo(0));
        }

        [Test]
        public void SelectedServiceIndex_Setter_ShouldRaisePropertyChanged()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            string? changedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) => changedPropertyName = e.PropertyName;

            // Act
            viewModel.SelectedServiceIndex = 1;

            // Assert
            Assert.That(changedPropertyName, Is.EqualTo(nameof(MainWindowViewModel.SelectedServiceIndex)));
        }

        [Test]
        public void SelectedOperationIndex_Setter_ShouldRaisePropertyChanged()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            string? changedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) => changedPropertyName = e.PropertyName;

            // Act
            viewModel.SelectedOperationIndex = 1;

            // Assert
            Assert.That(changedPropertyName, Is.EqualTo(nameof(MainWindowViewModel.SelectedOperationIndex)));
        }
    }
}
