using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ihc;
using Ihc.App;
using IhcLab.ParameterControls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IhcLab;

/// <summary>
/// Main application window implementing the View component of the MVVM pattern.
/// Connects UI controls defined in MainWindow.axaml to business logic via MainWindowViewModel.
///
/// <para><b>MVVM Pattern - View Responsibilities:</b></para>
/// <list type="bullet">
///   <item>Loads UI layout from MainWindow.axaml (XAML markup defining controls, layouts, styles)</item>
///   <item>Creates and assigns <see cref="MainWindowViewModel"/> as DataContext (enables data binding)</item>
///   <item>Handles UI-specific logic that doesn't belong in ViewModel (cursor changes, ScrollViewer configuration)</item>
///   <item>Subscribes to ViewModel PropertyChanged to trigger View-specific behaviors (parameter control generation)</item>
///   <item>Manages window lifecycle events (Closing, cleanup)</item>
/// </list>
///
/// <para><b>What is DataContext in Avalonia/WPF?</b></para>
/// <para>
/// DataContext is the "data source" for all bindings in the window and its child controls. When you set
/// DataContext = viewModel, all bindings in MainWindow.axaml like {Binding SelectedServiceIndex} resolve
/// against the ViewModel instance. This is how the XAML markup connects to C# properties without explicit
/// control.Property = value assignments.
/// </para>
///
/// <para><b>DataContext Inheritance:</b></para>
/// <para>
/// Child controls automatically inherit DataContext from their parent. Setting DataContext on the Window means
/// all ComboBoxes, TextBoxes, etc. inside it can bind to ViewModel properties without each control needing its
/// own DataContext assignment. You can override per-control if needed.
/// </para>
///
/// <para><b>Why Subscribe to PropertyChanged Manually?</b></para>
/// <para>
/// Most ViewModel properties (like SelectedServiceIndex) automatically update bound UI controls via
/// INotifyPropertyChanged. However, some actions require imperative code that can't be expressed in
/// XAML bindings alone - like dynamically generating parameter controls based on operation metadata.
/// For these cases, we manually subscribe to PropertyChanged and trigger the logic in OnViewModelPropertyChanged.
/// </para>
///
/// <para><b>Separation of Concerns:</b></para>
/// <list type="bullet">
///   <item><b>ViewModel (MainWindowViewModel):</b> Holds application state (selected indices, output text, etc.),
///   handles business logic events, no knowledge of specific UI controls</item>
///   <item><b>View (MainWindow):</b> Creates controls, responds to UI-specific needs (cursor changes, parameter
///   panels), delegates business logic to ViewModel</item>
///   <item><b>Service (LabAppService):</b> Business logic, IHC service interaction, no GUI knowledge</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    private IhcSetup? ihcDomain;
    private IClipboard? clipboard;

    private ILogger<MainWindow> logger;
    private LabAppService? labAppService;
    private MainWindowViewModel? viewModel;
    private LabAppService.OperationItem? currentOperationItem;

    // Extracted coordinators for better separation of concerns
    private readonly ParameterSyncCoordinator parameterSyncCoordinator;
    private readonly ParameterControlCoordinator parameterControlCoordinator;

    /// <summary>
    /// Exposes the LabAppService for testing purposes (headless Avalonia tests).
    /// </summary>
    public LabAppService? LabAppService => labAppService;

    /// <summary>
    /// Initializes the MainWindow and sets up MVVM data binding infrastructure.
    ///
    /// <para><b>Initialization Sequence:</b></para>
    /// <list type="number">
    ///   <item>Calls InitializeComponent() - Generated method that loads MainWindow.axaml UI markup and creates controls</item>
    ///   <item>Creates MainWindowViewModel instance</item>
    ///   <item>Assigns ViewModel as DataContext - This is THE KEY step that activates all {Binding} expressions in AXAML</item>
    ///   <item>Subscribes to ViewModel.PropertyChanged for View-specific logic (parameter control generation)</item>
    ///   <item>Initializes clipboard and event handlers</item>
    /// </list>
    ///
    /// <para><b>Why Set DataContext Here Instead of XAML?</b></para>
    /// <para>
    /// You CAN set DataContext in XAML like: &lt;Window.DataContext&gt;&lt;vm:MainWindowViewModel/&gt;&lt;/Window.DataContext&gt;
    /// However, doing it in code-behind gives us:
    /// 1. Reference to ViewModel instance for programmatic access (viewModel.ClearOutput(), etc.)
    /// 2. Control over ViewModel creation timing and constructor parameters
    /// 3. Ability to subscribe to events before any bindings are evaluated
    /// </para>
    ///
    /// <para><b>DataContext Assignment Mechanism:</b></para>
    /// <para>
    /// When you assign DataContext = viewModel, Avalonia internally:
    /// 1. Stores the ViewModel reference in the Window's DataContext property
    /// 2. Raises a PropertyChanged-like notification for DataContext
    /// 3. Triggers reevaluation of all {Binding} expressions in the XAML tree
    /// 4. Each binding subscribes to ViewModel.PropertyChanged for the bound property
    /// 5. Future ViewModel property changes automatically propagate to UI
    /// </para>
    /// </summary>
    public MainWindow()
    {
        // Use OpenTel activities as the primary way to keep track of operations. This mixes well with the SDK activities.
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + ".Ctr", ActivityKind.Internal);

        // Avalonia uses logger so we might as well use it for important things like errors and warnings.
        this.logger = Program.loggerFactory != null ? Program.loggerFactory.CreateLogger<MainWindow>() : NullLoggerFactory.Instance.CreateLogger<MainWindow>();

        // Initialize extracted coordinators
        var loggerFactory = Program.loggerFactory ?? NullLoggerFactory.Instance;
        this.parameterSyncCoordinator = new ParameterSyncCoordinator(loggerFactory.CreateLogger<ParameterSyncCoordinator>());
        this.parameterControlCoordinator = new ParameterControlCoordinator(loggerFactory.CreateLogger<ParameterControlCoordinator>());

        try
        {
            // Load and build UI from MainWindow.axaml
            InitializeComponent();

            // Create ViewModel - this holds all application state and presentation logic
            viewModel = new MainWindowViewModel();

            // CRITICAL: Assign ViewModel as DataContext. This activates ALL {Binding} expressions in the AXAML file.
            // After this line, Avalonia evaluates every {Binding Property} and subscribes to ViewModel.PropertyChanged.
            DataContext = viewModel;

            // Subscribe to ViewModel property changes for View-specific logic (generating parameter controls)
            // Most property changes are handled automatically via bindings, but some require imperative UI updates.
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            clipboard = this.Clipboard;

            if (clipboard == null)
            {
                CopyOutputMenuItem.IsEnabled = false;
                CopyErrorMenuItem.IsEnabled = false;
            }

            // Handle window closing event (when user clicks X button)
            Closing += OnWindowClosing;


            logger.LogInformation(message: "MainWindow sucessfully constructed (awaiting start)");
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            viewModel?.SetError(nameof(MainWindow) + " constructor error", ex);
            RunButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// Async initialization of MainWindow that sets up IhcDomain and login if needed. Returns this for chaining.
    /// </summary>
    /// <returns>this</returns>
    public async Task<MainWindow> Start()
    {
        // Use OpenTel activities as the primary way to keep track of operations. This mix well with the SDK activities.
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(Start), ActivityKind.Internal);

        try
        {
            ihcDomain = new IhcSetup();

            if (!ihcDomain.IhcSettings.IsValid())
            {
                var longinWindow = new LoginDialog(ihcDomain!.IhcSettings);
                await longinWindow.ShowDialog(this);
            }
            LoginUpdated();

            logger.LogInformation(message: "MainWindow sucessfully started");
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            viewModel?.SetError(nameof(Start) + " error", ex);
        }

        return this;
    }

    public async void SetupMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(SetupMenuItemClick), ActivityKind.Internal);
        try
        {
            var longinWindow = new LoginDialog(ihcDomain!.IhcSettings);
            await longinWindow.ShowDialog(this);
            LoginUpdated();
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "LoginDialog", ex);
        }
    }
      
    /// <summary>
    /// Called after login configuration changes (initial startup or Setup menu item clicked).
    /// Connects the service layer (LabAppService) to the ViewModel, completing the MVVM wiring.
    ///
    /// <para><b>MVVM Layer Connection:</b></para>
    /// <list type="number">
    ///   <item><b>Service Layer:</b> Creates LabAppService and configures with IHC services (business logic)</item>
    ///   <item><b>ViewModel Layer:</b> Assigns service to viewModel.LabAppService property</item>
    ///   <item><b>View Layer:</b> Already bound to ViewModel via DataContext (set in constructor)</item>
    /// </list>
    ///
    /// <para><b>What Happens When viewModel.LabAppService Is Set:</b></para>
    /// <para>
    /// Setting viewModel.LabAppService triggers the LabAppService property setter in MainWindowViewModel:
    /// 1. ViewModel subscribes to LabAppService.CurrentOperationChanged and ServicesChanged events
    /// 2. ViewModel populates Services and Operations ObservableCollections from service state
    /// 3. ViewModel sets SelectedServiceIndex and SelectedOperationIndex to match service
    /// 4. These property changes trigger PropertyChanged notifications
    /// 5. Avalonia bindings update UI ComboBoxes with populated data and correct selections
    /// 6. User sees populated ComboBoxes with IHC services and operations ready to run
    /// </para>
    ///
    /// <para><b>Why Not Directly Bind View to LabAppService?</b></para>
    /// <para>
    /// We could theoretically bind the View directly to LabAppService (skip ViewModel layer). However, ViewModel provides:
    /// 1. Presentation logic layer - Maps service data to UI-friendly formats (DisplayName, descriptions)
    /// 2. UI state management - Tracks output visibility, error states independent of service
    /// 3. Circular update prevention - _isUpdatingFromEvent flag prevents binding loops
    /// 4. Testability - Can test ViewModel behavior without instantiating Window or UI controls
    /// 5. Separation of Concerns - Service doesn't need ObservableCollection or PropertyChanged (those are UI concerns)
    /// </para>
    /// </summary>
    private void LoginUpdated()
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(LoginUpdated), ActivityKind.Internal);

        ihcDomain?.UpdateSetup();
        this.Title = "IHC Lab : Endpoint set to " + ihcDomain?.IhcSettings.Endpoint ?? "(no endpoint set)";

        if (ihcDomain != null)
        {
            try
            {
                // Create LabAppService with operation filter (business logic layer)
                var operationFilter = OperationFilterConfiguration.CreateDefaultFilter();
                labAppService = new LabAppService(globalSupportedServiceFilter: null, globalSupportedOperationFilter: operationFilter);

                // Configure with all IHC services (populates service.Services array with real/mocked IHC APIs)
                labAppService.Configure(ihcDomain.IhcSettings, ihcDomain.AllIhcServices);

                // CRITICAL: Wire up LabAppService to ViewModel. This completes the MVVM chain:
                // Service → ViewModel → View (via DataContext bindings)
                // Setting this property triggers ViewModel's event subscription and collection population.
                if (viewModel != null)
                {
                    viewModel.LabAppService = labAppService;
                }

                activity?.SetTag("labappservice.configured.service_count", labAppService.Services.Length);
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                // ViewModel will handle clearing collections on failure
            }
        }

        // Do this last so Warning is not cleared unless we are testing.
        if (string.IsNullOrEmpty(Program.config?.telemetryConfig?.Host) && (Program.config?.ihcSettings?.Endpoint?.StartsWith(SpecialEndpoints.MockedPrefix) == false))
        {
            OpenTelemetryMenuItem.IsEnabled = false;
            viewModel?.SetWarning("OpenTelemetry not configured. It is recommended (but not required) to setup telemetry to view logs/traces. See guide in README for details.");
        }
    }

    public async void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(RunButtonClickHandler), ActivityKind.Internal);

        viewModel?.ClearErrorAndWarning();
        viewModel?.ClearOutput();

        this.Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            activity?.SetParameters(
                (nameof(sender), sender),
                (nameof(e), e)
            );

            if (labAppService == null)
            {
                throw new Exception("LabAppService not configured.");
            }

            if (ServicesComboBox.SelectedIndex < 0)
            {
                throw new Exception("No service selected.");
            }

            if (OperationsComboBox.SelectedIndex < 0)
            {
                throw new Exception("No operation selected.");
            }

            activity?.SetTag("ihcoperation", labAppService.SelectedOperation.DisplayName);

            var operationResult = await labAppService.DynCallSelectedOperation();

            activity?.SetTag("result", operationResult.DisplayResult);

            logger.LogInformation(message: $"Operation {labAppService.SelectedOperation.DisplayName} sucessfullly called with result {operationResult.DisplayResult}");

            await SetOutput(operationResult.DisplayResult, operationResult.ReturnType);
        } catch (Exception ex)
        {
           HandleOperationError(activity, nameof(RunButtonClickHandler), ex);
        } finally
        {
            this.Cursor = Cursor.Default;
        }
    }

    /// <summary>
    /// Handles ViewModel property changes to trigger UI-specific actions like parameter control setup.
    /// </summary>
    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When operation selection changes, update parameter controls
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedOperationIndex))
        {
            try
            {
                if (labAppService != null && labAppService.SelectedService != null)
                {
                    var selectedOperationIndex = viewModel?.SelectedOperationIndex ?? 0;
                    var operations = labAppService.SelectedService.OperationItems;

                    if (selectedOperationIndex >= 0 && selectedOperationIndex < operations.Length)
                    {
                        var operationItem = operations[selectedOperationIndex];

                        RunButton.IsEnabled = true;

                        // Set up parameter controls using ParameterControlCoordinator
                        await parameterControlCoordinator.SetupControlsAsync(
                            ParametersPanel,
                            operationItem,
                            OnDynFieldValueChanged);

                        // Initialize LabAppService with default values from GUI controls
                        // This ensures complex parameters have valid default instances (not null)
                        // Note: This may fail for nullable parameters with empty/null values, which is expected
                        // as it prevents overwriting previously saved values
                        try
                        {
                            parameterSyncCoordinator.SyncToService(ParametersPanel, operationItem);
                        }
                        catch (Exception ex)
                        {
                            // Log as Debug since this is expected when controls have empty values
                            // The exception prevents overwriting saved values, which is desirable behavior
                            logger.LogDebug(ex, "Skipped syncing empty default values for operation {OperationName}", operationItem.OperationMetadata.Name);
                        }

                        // Restore previously set argument values from LabAppService (if any exist)
                        // This overwrites defaults with any values that were set in a previous invocation
                        try
                        {
                            parameterSyncCoordinator.SyncFromService(ParametersPanel, operationItem);
                        }
                        catch (Exception ex)
                        {
                            // Log warning but continue with default values if restoration fails
                            logger.LogWarning(ex, "Failed to restore argument values from LabAppService for operation {OperationName}", operationItem.OperationMetadata.Name);
                        }

                        // Unsubscribe from previous operation's ArgumentChanged event
                        if (currentOperationItem != null)
                        {
                            currentOperationItem.ArgumentChanged -= OnLabAppServiceArgumentChanged;
                        }

                        // Subscribe to ArgumentChanged events for LabAppService → GUI synchronization
                        currentOperationItem = operationItem;
                        currentOperationItem.ArgumentChanged += OnLabAppServiceArgumentChanged;
                    }
                    else
                    {
                        RunButton.IsEnabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                viewModel?.SetError("Parameter control setup error", ex);
                RunButton.IsEnabled = false;
            }
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Clean up IHC domain when window is closing
        ihcDomain?.Dispose();

        // Unsubscribe from LabAppService events
        if (currentOperationItem != null)
        {
            currentOperationItem.ArgumentChanged -= OnLabAppServiceArgumentChanged;
            currentOperationItem = null;
        }

        // Clean up ViewModel and unsubscribe from events
        if (viewModel != null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Dispose();
        }
    }

    public void ExitMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(ExitMenuItemClick), ActivityKind.Internal);

        Close(); // Calls in turn OnWindowClosing which will dispose our IhcDomain.
    }

    public async void AboutMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(AboutMenuItemClick), ActivityKind.Internal);
        try
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "AboutMenu", ex);
        }
    }

    public async void ShowSettingsMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(ShowSettingsMenuItemClick), ActivityKind.Internal);
        try
        {
            viewModel?.ClearErrorAndWarning();
            viewModel?.ClearOutput();

            // Convert IConfigurationSection to dictionary to properly serialize values
            var loggingConfigDict = Program.config?.loggingConfig?.GetChildren()
                .ToDictionary(
                    section => section.Key,
                    section => section.GetChildren().Any()
                        ? section.GetChildren().ToDictionary(child => child.Key, child => child.Value)
                        : (object?)section.Value
                );

            var settings = new
            {
                IhcSettings = Program.config?.ihcSettings,
                TelemetryConfiguration = Program.config?.telemetryConfig,
                LoggingConfig = loggingConfigDict
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(settings, options);
            await SetOutput(json, settings.GetType());
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "Show settings", ex);
        }
    }

    public void OpenTelemetryMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(OpenTelemetryMenuItemClick), ActivityKind.Internal);
        try
        {
            string? telemetryUrl = Program.config?.telemetryConfig?.Host;
            if (string.IsNullOrEmpty(telemetryUrl))
            {
                throw new NotSupportedException("Telemetry host not set");
            }

            var psi = new ProcessStartInfo
            {
                FileName = telemetryUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "Open telemetry in browser", ex);
        }
    }

    public async void CopyOutputMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(CopyOutputMenuItemClick), ActivityKind.Internal);

        try
        {
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(Output.Text ?? string.Empty);
            } else throw new NotSupportedException("No clipboard available");
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "Output to clipboard", ex);
        }
    }

    public async void CopyErrorMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(CopyErrorMenuItemClick), ActivityKind.Internal);

        try
        {
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(ErrorWarningContent.Text ?? string.Empty);
            }
            else throw new NotSupportedException("No clipboard available");
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "Error to clipboard", ex);
        }
    }

    public async Task SetOutput(string text, Type type)
    {
        // Update ViewModel (this updates bindings)
        viewModel?.SetOutput(text, type);

        // UI-specific logic: adjust text wrapping based on content size
        bool isLargeContent = text.Length > 10000;

        Output.TextWrapping = isLargeContent
            ? Avalonia.Media.TextWrapping.NoWrap
            : Avalonia.Media.TextWrapping.Wrap;

        // Get the parent ScrollViewer and update its scroll mode
        if (Output.Parent is ScrollViewer scrollViewer)
        {
            scrollViewer.HorizontalScrollBarVisibility = isLargeContent
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
        }

        // Wait for UI to complete layout and rendering
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Helper method to handle exceptions consistently: logs to activity and displays error to user.
    /// Reduces code duplication across catch blocks.
    /// </summary>
    /// <param name="activity">The OpenTelemetry activity to record the error (can be null)</param>
    /// <param name="operation">Name of the operation that failed (e.g., "LoginDialog", "RunButtonClick")</param>
    /// <param name="ex">The exception that occurred</param>
    private void HandleOperationError(Activity? activity, string operation, Exception ex)
    {
        activity?.SetError(ex);
        viewModel?.SetError(operation + " error", ex);
    }

    /// <summary>
    /// Creates an operation filter function for LabAppService.
    /// Excludes: AsyncEnumerable operations, operations with array parameters, operations with ResourceValue parameters.
    /// </summary>
    /// <returns>Filter function for LabAppService</returns>

    /// <summary>
    /// Syncs parameter values from GUI controls to LabAppService.SelectedOperation.Arguments.
    /// Extracts values from ParametersPanel and uses SetArgumentsFromArray for type-safe bulk setting.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LabAppService is not configured.</exception>
    private void SyncArgumentsToLabAppService()
    {
        if (labAppService == null)
            throw new InvalidOperationException("LabAppService is not configured");

        parameterSyncCoordinator.SyncToService(ParametersPanel, labAppService.SelectedOperation);
    }


    /// <summary>
    /// Handles ArgumentChanged events from LabAppService.
    /// Updates GUI controls when LabAppService arguments are changed programmatically.
    /// </summary>
    private void OnLabAppServiceArgumentChanged(object? sender, LabAppService.MethodArgumentChangedEventArgs e)
    {
        if (labAppService == null)
            return;

        var operationMetadata = labAppService.SelectedOperation.OperationMetadata;
        if (e.Index < 0 || e.Index >= operationMetadata.Parameters.Length)
            return;

        var parameter = operationMetadata.Parameters[e.Index];
        parameterSyncCoordinator.UpdateGuiFromParameter(ParametersPanel, parameter, e.NewValue, e.Index.ToString());
    }

    /// <summary>
    /// Handles ValueChanged events from strategy controls.
    /// Immediately syncs the changed value to LabAppService.
    /// </summary>
    private void OnDynFieldValueChanged(object? sender, EventArgs e)
    {
        if (labAppService == null || sender is not Control control)
            return;

        try
        {
            // Get index path from control.Name
            string indexPath = control.Name ?? "";

            if (string.IsNullOrEmpty(indexPath))
                return;

            // Parse the index path to get the parameter index
            string[] parts = indexPath.Split('.');
            if (parts.Length == 0 || !int.TryParse(parts[0], out int parameterIndex))
                return;

            var operationMetadata = labAppService.SelectedOperation.OperationMetadata;
            if (parameterIndex < 0 || parameterIndex >= operationMetadata.Parameters.Length)
                return;

            var parameter = operationMetadata.Parameters[parameterIndex];

            // For simple types, directly set the argument value
            if (parameter.IsSimple || parameter.IsFile)
            {
                // Extract value using strategy
                var metadata = control.Tag as OperationSupport.ControlMetadata;
                object? value;
                if (metadata != null)
                {
                    value = metadata.Strategy.ExtractValue(control, metadata.Field);
                }
                else
                {
                    // Fallback if metadata is missing (shouldn't happen)
                    var strategy = ParameterControlRegistry.Instance.GetStrategy(parameter);
                    value = strategy.ExtractValue(control, parameter);
                }

                labAppService.SelectedOperation.SetMethodArgument(parameterIndex, value);
            }
            else
            {
                // For complex types, reconstruct the ENTIRE object from ALL controls
                // This ensures all sub-fields are captured, not just the one that changed
                var reconstructedValue = OperationSupport.GetFieldValue(ParametersPanel, parameter, parameterIndex.ToString());
                labAppService.SelectedOperation.SetMethodArgument(parameterIndex, reconstructedValue);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync control value change to LabAppService");
        }
    }

}