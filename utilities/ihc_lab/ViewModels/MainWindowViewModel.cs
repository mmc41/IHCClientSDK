using System;
using System.Collections.ObjectModel;
using System.Linq;
using Ihc.App;

namespace IhcLab.ViewModels
{
    /// <summary>
    /// ViewModel for MainWindow implementing the presentation logic for the IHC Lab GUI application.
    /// Serves as the intermediary between the View (MainWindow.axaml) and the business logic layer (LabAppService).
    ///
    /// <para><b>MVVM Architecture Overview:</b></para>
    /// <para>
    /// This ViewModel follows the event-driven MVVM pattern where <see cref="LabAppService"/> is the single source
    /// of truth for application state. The ViewModel subscribes to service events and exposes bindable properties
    /// that automatically update the UI through Avalonia's data binding system.
    /// </para>
    ///
    /// <para><b>Bidirectional Synchronization Design:</b></para>
    /// <list type="number">
    ///   <item><b>GUI → ViewModel → LabAppService:</b> User changes ComboBox selection → SelectedServiceIndex setter
    ///   → Updates LabAppService.SelectedServiceIndex → LabAppService raises CurrentOperationChanged event</item>
    ///   <item><b>LabAppService → ViewModel → GUI:</b> LabAppService.CurrentOperationChanged event fires →
    ///   OnCurrentOperationChanged handler → Updates ViewModel properties → INotifyPropertyChanged triggers UI refresh</item>
    /// </list>
    ///
    /// <para><b>Circular Update Prevention:</b></para>
    /// <para>
    /// The <c>_isUpdatingFromEvent</c> flag prevents infinite loops. When LabAppService fires an event, we set this flag
    /// to prevent property setters from pushing changes BACK to LabAppService. This ensures changes flow in one direction
    /// at a time: either from GUI to service, or from service to GUI, but never both simultaneously.
    /// </para>
    ///
    /// <para><b>ObservableCollection vs Regular Collections:</b></para>
    /// <para>
    /// <see cref="ObservableCollection{T}"/> implements <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>,
    /// which notifies the UI when items are added/removed/reordered. This is critical for ComboBox ItemsSource bindings -
    /// when we call Services.Clear() or Services.Add(), the ComboBox automatically reflects these changes without manual
    /// ItemsSource reassignment. Regular List{T} would require full ItemsSource replacement to update UI.
    /// </para>
    ///
    /// <para><b>Lifetime and Resource Management:</b></para>
    /// <para>
    /// Implements <see cref="IDisposable"/> to properly unsubscribe from LabAppService events. Avalonia doesn't automatically
    /// unsubscribe event handlers, so failing to dispose can cause memory leaks where the ViewModel remains in memory even
    /// after the Window closes (event subscription keeps it alive).
    /// </para>
    ///
    /// <para><b>Thread Safety Note:</b></para>
    /// <para>
    /// This ViewModel assumes all property changes and event handlers execute on the UI thread. Avalonia data binding
    /// requires PropertyChanged events to be raised on the UI thread. If LabAppService fires events from background threads,
    /// you must use Dispatcher.UIThread.Invoke() to marshal back to UI thread before updating ViewModel properties.
    /// </para>
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private LabAppService? _labAppService;
        private string _operationDescription = string.Empty;
        private string _outputText = string.Empty;
        private string _outputHeading = "Output";
        private bool _isOutputVisible = false;
        private string _errorWarningText = string.Empty;
        private bool _isErrorVisible = false;
        private bool _isWarningVisible = false;
        private int _selectedServiceIndex = 0;
        private int _selectedOperationIndex = 0;

        /// <summary>
        /// Flag to prevent circular updates between ViewModel and LabAppService.
        /// When true, property setters will NOT propagate changes back to LabAppService,
        /// breaking potential infinite event loops when synchronizing state.
        /// </summary>
        private bool _isUpdatingFromEvent = false;

        /// <summary>
        /// Observable collection of IHC services available for selection.
        /// Bound to ServicesComboBox.ItemsSource in MainWindow.axaml.
        ///
        /// <para><b>Why ObservableCollection:</b></para>
        /// <para>
        /// ObservableCollection notifies UI controls when items are added/removed. When we call Services.Clear() and
        /// Services.Add(), the bound ComboBox automatically reflects these changes. With List{T}, we'd need to reassign
        /// the entire ItemsSource property, breaking selected item tracking and causing visual glitches.
        /// </para>
        ///
        /// <para><b>Population Strategy:</b></para>
        /// <para>
        /// Populated from LabAppService.Services array when LabAppService property is set or when ServicesChanged event fires.
        /// We iterate and Add() each service individually rather than replacing the collection instance, preserving the
        /// binding reference and avoiding ComboBox flickering.
        /// </para>
        /// </summary>
        public ObservableCollection<LabAppService.ServiceItem> Services { get; } = new ObservableCollection<LabAppService.ServiceItem>();

        /// <summary>
        /// Observable collection of operations available for the currently selected service.
        /// Bound to OperationsComboBox.ItemsSource in MainWindow.axaml.
        ///
        /// <para><b>Dynamic Content:</b></para>
        /// <para>
        /// Contents change whenever SelectedServiceIndex changes. When user selects a different service, we clear this
        /// collection and repopulate with operations from the newly selected service's OperationItems array. The UI
        /// ComboBox automatically updates to show the new operations list.
        /// </para>
        ///
        /// <para><b>Lifecycle:</b></para>
        /// <para>
        /// Updated in OnCurrentOperationChanged event handler when LabAppService.SelectedServiceIndex changes.
        /// Also cleared in ClearOutput methods when switching contexts.
        /// </para>
        /// </summary>
        public ObservableCollection<LabAppService.OperationItem> Operations { get; } = new ObservableCollection<LabAppService.OperationItem>();

        /// <summary>
        /// Gets or sets the LabAppService instance which provides business logic and state management.
        ///
        /// <para><b>Setter Behavior - Event Subscription Management:</b></para>
        /// <list type="number">
        ///   <item>Unsubscribes from previous LabAppService events (prevents memory leaks)</item>
        ///   <item>Assigns new service reference</item>
        ///   <item>Subscribes to new service's CurrentOperationChanged and ServicesChanged events</item>
        ///   <item>Populates Services and Operations collections from new service state</item>
        ///   <item>Sets initial selection indices to match service state</item>
        /// </list>
        ///
        /// <para><b>Why Event-Driven Instead of Polling:</b></para>
        /// <para>
        /// Instead of the ViewModel periodically polling LabAppService for state changes, we use event subscriptions.
        /// When LabAppService state changes (either from GUI actions or programmatic changes), it raises events that
        /// the ViewModel handles to update bound properties. This is more efficient than polling and provides immediate
        /// UI updates with zero delay.
        /// </para>
        ///
        /// <para><b>Force Binding Update Pattern:</b></para>
        /// <para>
        /// Notice the pattern: _selectedServiceIndex = -1; OnPropertyChanged(); _selectedServiceIndex = 0; OnPropertyChanged().
        /// This forces Avalonia to update ComboBox.SelectedIndex even if the logical index is 0 (same as default).
        /// Without the -1 reset, Avalonia might not detect a "change" and fail to update the UI selection.
        /// </para>
        ///
        /// <para><b>Usage Pattern:</b></para>
        /// <code>
        /// // In MainWindow.LoginUpdated():
        /// labAppService = new LabAppService(...);
        /// labAppService.Configure(settings, services);
        /// viewModel.LabAppService = labAppService; // Triggers full UI initialization
        /// </code>
        /// </summary>
        public LabAppService? LabAppService
        {
            get => _labAppService;
            set
            {
                if (_labAppService != value)
                {
                    // Unsubscribe from old service events to prevent memory leaks
                    if (_labAppService != null)
                    {
                        _labAppService.CurrentOperationChanged -= OnCurrentOperationChanged;
                        _labAppService.ServicesChanged -= OnServicesChanged;
                    }

                    _labAppService = value;

                    // Subscribe to new service events and initialize collections
                    if (_labAppService != null)
                    {
                        _labAppService.CurrentOperationChanged += OnCurrentOperationChanged;
                        _labAppService.ServicesChanged += OnServicesChanged;

                        // Initial population of collections
                        _isUpdatingFromEvent = true;
                        try
                        {
                            Services.Clear();
                            foreach (var service in _labAppService.Services)
                            {
                                Services.Add(service);
                            }

                            if (_labAppService.Services.Length > 0)
                            {
                                // Force binding update by setting to -1 first (in case value is already 0)
                                _selectedServiceIndex = -1;
                                OnPropertyChanged(nameof(SelectedServiceIndex));
                                _selectedServiceIndex = _labAppService.SelectedServiceIndex;
                                OnPropertyChanged(nameof(SelectedServiceIndex));

                                Operations.Clear();
                                var selectedService = _labAppService.SelectedService;
                                if (selectedService != null)
                                {
                                    foreach (var operation in selectedService.OperationItems)
                                    {
                                        Operations.Add(operation);
                                    }

                                    // Force binding update by setting to -1 first (in case value is already 0)
                                    _selectedOperationIndex = -1;
                                    OnPropertyChanged(nameof(SelectedOperationIndex));
                                    _selectedOperationIndex = selectedService.SelectedOperationIndex;
                                    OnPropertyChanged(nameof(SelectedOperationIndex));

                                    // Set initial operation description
                                    if (selectedService.OperationItems.Length > 0 && _selectedOperationIndex < selectedService.OperationItems.Length)
                                    {
                                        OperationDescription = selectedService.OperationItems[_selectedOperationIndex].OperationMetadata.Description;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            _isUpdatingFromEvent = false;
                        }
                    }

                    OnPropertyChanged(nameof(LabAppService));
                }
            }
        }

        /// <summary>
        /// Gets or sets the operation description text displayed in the GUI.
        /// </summary>
        public string OperationDescription
        {
            get => _operationDescription;
            set => SetProperty(ref _operationDescription, value);
        }

        /// <summary>
        /// Gets or sets the output text displayed after running an operation.
        /// </summary>
        public string OutputText
        {
            get => _outputText;
            set => SetProperty(ref _outputText, value);
        }

        /// <summary>
        /// Gets or sets the output heading text (includes size and type information).
        /// </summary>
        public string OutputHeading
        {
            get => _outputHeading;
            set => SetProperty(ref _outputHeading, value);
        }

        /// <summary>
        /// Gets or sets whether the output section is visible.
        /// </summary>
        public bool IsOutputVisible
        {
            get => _isOutputVisible;
            set => SetProperty(ref _isOutputVisible, value);
        }

        /// <summary>
        /// Gets or sets the error/warning text displayed in the status section.
        /// </summary>
        public string ErrorWarningText
        {
            get => _errorWarningText;
            set => SetProperty(ref _errorWarningText, value);
        }

        /// <summary>
        /// Gets or sets whether the error heading is visible.
        /// </summary>
        public bool IsErrorVisible
        {
            get => _isErrorVisible;
            set => SetProperty(ref _isErrorVisible, value);
        }

        /// <summary>
        /// Gets or sets whether the warning heading is visible.
        /// </summary>
        public bool IsWarningVisible
        {
            get => _isWarningVisible;
            set => SetProperty(ref _isWarningVisible, value);
        }

        /// <summary>
        /// Gets or sets the selected service index. Bound bidirectionally to ServicesComboBox.SelectedIndex.
        ///
        /// <para><b>Bidirectional Binding Flow:</b></para>
        /// <para>
        /// <b>User changes ComboBox → ViewModel → LabAppService:</b>
        /// 1. User selects different service in ServicesComboBox
        /// 2. Avalonia sets SelectedServiceIndex via this setter (binding)
        /// 3. Setter updates _labAppService.SelectedServiceIndex
        /// 4. LabAppService raises CurrentOperationChanged event
        /// 5. Event handler updates Operations collection and OperationDescription
        /// </para>
        ///
        /// <para>
        /// <b>LabAppService changes → ViewModel → ComboBox:</b>
        /// 1. LabAppService.SelectedServiceIndex changes programmatically
        /// 2. LabAppService raises CurrentOperationChanged event
        /// 3. OnCurrentOperationChanged handler sets _isUpdatingFromEvent = true
        /// 4. Handler updates _selectedServiceIndex and calls OnPropertyChanged
        /// 5. Avalonia binding updates ComboBox.SelectedIndex
        /// 6. Setter is called but _isUpdatingFromEvent prevents pushing back to LabAppService
        /// </para>
        ///
        /// <para><b>Circular Update Prevention:</b></para>
        /// <para>
        /// The _isUpdatingFromEvent flag breaks potential infinite loops. Without it:
        /// LabAppService event → ViewModel property update → Setter pushes to LabAppService → LabAppService event → loop!
        /// With the flag, event handlers set it to true before updating properties, preventing setters from
        /// propagating changes back during event handling.
        /// </para>
        ///
        /// <para><b>Error Handling Strategy:</b></para>
        /// <para>
        /// If LabAppService.SelectedServiceIndex setter throws (invalid index, etc.), we catch the exception and revert
        /// the ViewModel property to LabAppService's current value. This keeps ViewModel and service in sync even when
        /// validation fails, preventing the UI from showing an index that doesn't match the actual service state.
        /// </para>
        /// </summary>
        public int SelectedServiceIndex
        {
            get => _selectedServiceIndex;
            set
            {
                if (_selectedServiceIndex != value)
                {
                    _selectedServiceIndex = value;
                    OnPropertyChanged(nameof(SelectedServiceIndex));

                    // Update LabAppService when GUI changes selection (but not during event updates to prevent loops)
                    if (!_isUpdatingFromEvent && _labAppService != null && value >= 0 && value < _labAppService.Services.Length)
                    {
                        try
                        {
                            _labAppService.SelectedServiceIndex = value;
                            // Operations will be updated via CurrentOperationChanged event
                        }
                        catch (Exception ex)
                        {
                            // Log the exception for diagnostics even though we recover from it
                            System.Diagnostics.Debug.WriteLine($"Failed to update LabAppService.SelectedServiceIndex to {value}: {ex.Message}");

                            // If update fails, revert to previous value to maintain synchronization
                            _selectedServiceIndex = _labAppService.SelectedServiceIndex;
                            OnPropertyChanged(nameof(SelectedServiceIndex));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected operation index. Bound bidirectionally to OperationsComboBox.SelectedIndex.
        ///
        /// <para><b>Relationship to SelectedServiceIndex:</b></para>
        /// <para>
        /// This property is subordinate to SelectedServiceIndex. When the service changes, the Operations collection
        /// is repopulated, and this index may be reset to 0 (first operation of new service). The ViewModel, service,
        /// and UI all stay synchronized through the event-driven architecture.
        /// </para>
        ///
        /// <para><b>Synchronization Flow (Same Pattern as SelectedServiceIndex):</b></para>
        /// <list type="number">
        ///   <item><b>GUI to Service:</b> User changes operation → Setter updates LabAppService.SelectedOperationIndex
        ///   → LabAppService raises CurrentOperationChanged event → ViewModel updates OperationDescription</item>
        ///   <item><b>Service to GUI:</b> LabAppService event → Handler updates property with _isUpdatingFromEvent=true
        ///   → Binding updates ComboBox → Setter called but flag prevents loop</item>
        /// </list>
        ///
        /// <para><b>Validation Note:</b></para>
        /// <para>
        /// The setter doesn't explicitly check array bounds (unlike SelectedServiceIndex) because LabAppService
        /// performs validation. If the index is invalid, LabAppService.SelectedOperationIndex setter will throw,
        /// caught by our try-catch, reverting the ViewModel property to maintain consistency.
        /// </para>
        /// </summary>
        public int SelectedOperationIndex
        {
            get => _selectedOperationIndex;
            set
            {
                if (_selectedOperationIndex != value)
                {
                    _selectedOperationIndex = value;
                    OnPropertyChanged(nameof(SelectedOperationIndex));

                    // Update LabAppService when GUI changes selection (but not during event updates to prevent loops)
                    if (!_isUpdatingFromEvent && _labAppService != null)
                    {
                        try
                        {
                            _labAppService.SelectedOperationIndex = value;
                            // Operation details will be updated via CurrentOperationChanged event
                        }
                        catch (Exception ex)
                        {
                            // Log the exception for diagnostics even though we recover from it
                            System.Diagnostics.Debug.WriteLine($"Failed to update LabAppService.SelectedOperationIndex to {value}: {ex.Message}");

                            // If update fails, revert to previous value to maintain synchronization
                            _selectedOperationIndex = _labAppService.SelectedOperationIndex;
                            OnPropertyChanged(nameof(SelectedOperationIndex));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears the output text and hides the output section.
        /// </summary>
        public void ClearOutput()
        {
            OutputText = string.Empty;
            IsOutputVisible = false;
        }

        /// <summary>
        /// Sets the output text and heading, and shows the output section.
        /// </summary>
        /// <param name="text">The output text to display.</param>
        /// <param name="type">The return type of the operation.</param>
        public void SetOutput(string text, Type type)
        {
            OutputText = text;
            OutputHeading = $"Output (Size={text.Length}, Type={type.Name}):";
            IsOutputVisible = true;
        }

        /// <summary>
        /// Clears error and warning messages and hides their headings.
        /// </summary>
        public void ClearErrorAndWarning()
        {
            ErrorWarningText = string.Empty;
            IsErrorVisible = false;
            IsWarningVisible = false;
        }

        /// <summary>
        /// Sets an error message and shows the error heading.
        /// </summary>
        /// <param name="text">The error message text.</param>
        /// <param name="ex">Optional exception to include in the message.</param>
        public void SetError(string text, Exception? ex = null)
        {
            string txt = text ?? string.Empty;
            if (ex != null)
                txt = txt + ": " + ex.Source + " : " + ex.Message;

            ErrorWarningText = txt;
            IsErrorVisible = true;
            IsWarningVisible = false;
        }

        /// <summary>
        /// Sets a warning message and shows the warning heading.
        /// </summary>
        /// <param name="text">The warning message text.</param>
        /// <param name="ex">Optional exception to include in the message.</param>
        public void SetWarning(string text, Exception? ex = null)
        {
            string txt = text ?? string.Empty;
            if (ex != null)
                txt = txt + ": " + ex.Source + " : " + ex.Message;

            ErrorWarningText = txt;
            IsErrorVisible = false;
            IsWarningVisible = true;
        }

        /// <summary>
        /// Handles CurrentOperationChanged event from LabAppService.
        /// Updates OperationDescription, selected indices, and operations collection when selection changes.
        /// </summary>
        private void OnCurrentOperationChanged(object? sender, LabAppService.CurrentOperationChangedEventArgs e)
        {
            _isUpdatingFromEvent = true;
            try
            {
                // Always update service selection - event only fires when something changed
                _selectedServiceIndex = e.NewServiceIndex;
                OnPropertyChanged(nameof(SelectedServiceIndex));

                // Always update operations collection - may have changed even if index is same
                Operations.Clear();
                if (e.NewService != null)
                {
                    foreach (var operation in e.NewService.OperationItems)
                    {
                        Operations.Add(operation);
                    }
                }

                // Always update operation selection
                _selectedOperationIndex = e.NewOperationIndex;
                OnPropertyChanged(nameof(SelectedOperationIndex));

                // Update operation description
                if (e.NewOperation != null)
                {
                    OperationDescription = e.NewOperation.OperationMetadata.Description;
                }
                else
                {
                    OperationDescription = string.Empty;
                }

                // Clear output and errors when switching operations
                ClearOutput();
                ClearErrorAndWarning();
            }
            finally
            {
                _isUpdatingFromEvent = false;
            }
        }

        /// <summary>
        /// Handles ServicesChanged event from LabAppService.
        /// Repopulates Services collection and clears output/errors when services are reconfigured.
        /// </summary>
        private void OnServicesChanged(object? sender, EventArgs e)
        {
            _isUpdatingFromEvent = true;
            try
            {
                // Repopulate services collection
                Services.Clear();
                if (_labAppService != null && _labAppService.Services != null)
                {
                    foreach (var service in _labAppService.Services)
                    {
                        Services.Add(service);
                    }

                    // Update selected indices to match LabAppService
                    if (_labAppService.Services.Length > 0)
                    {
                        // Force binding update by setting to -1 first (in case value is already 0)
                        _selectedServiceIndex = -1;
                        OnPropertyChanged(nameof(SelectedServiceIndex));
                        _selectedServiceIndex = _labAppService.SelectedServiceIndex;
                        OnPropertyChanged(nameof(SelectedServiceIndex));

                        // Populate operations for the selected service
                        Operations.Clear();
                        var selectedService = _labAppService.SelectedService;
                        if (selectedService != null)
                        {
                            foreach (var operation in selectedService.OperationItems)
                            {
                                Operations.Add(operation);
                            }

                            // Force binding update by setting to -1 first (in case value is already 0)
                            _selectedOperationIndex = -1;
                            OnPropertyChanged(nameof(SelectedOperationIndex));
                            _selectedOperationIndex = selectedService.SelectedOperationIndex;
                            OnPropertyChanged(nameof(SelectedOperationIndex));
                        }
                    }
                }

                ClearOutput();
                ClearErrorAndWarning();
            }
            finally
            {
                _isUpdatingFromEvent = false;
            }
        }

        /// <summary>
        /// Disposes the view model and unsubscribes from events.
        /// </summary>
        public void Dispose()
        {
            if (_labAppService != null)
            {
                _labAppService.CurrentOperationChanged -= OnCurrentOperationChanged;
                _labAppService.ServicesChanged -= OnServicesChanged;
            }
        }
    }
}
