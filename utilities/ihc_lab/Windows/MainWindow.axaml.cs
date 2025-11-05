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
using IhcLab.ViewModels;
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
/// <para><b>Binding Examples from MainWindow.axaml:</b></para>
/// <code>
/// <!-- ComboBox items populated from ViewModel.Services ObservableCollection -->
/// &lt;ComboBox ItemsSource="{Binding Services}"
///           SelectedIndex="{Binding SelectedServiceIndex}" /&gt;
///
/// <!-- TextBlock text updates when ViewModel.OperationDescription changes -->
/// &lt;TextBlock Text="{Binding OperationDescription}" /&gt;
///
/// <!-- Output panel visibility controlled by ViewModel.IsOutputVisible -->
/// &lt;StackPanel IsVisible="{Binding IsOutputVisible}"&gt;
///   &lt;TextBlock Text="{Binding OutputText}" /&gt;
/// &lt;/StackPanel&gt;
/// </code>
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
    private IhcDomain? ihcDomain;
    private IClipboard? clipboard;

    private ILogger<MainWindow> logger;
    private LabAppService? labAppService;
    private MainWindowViewModel? viewModel;
    private LabAppService.OperationItem? currentOperationItem;

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
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(MainWindow) + " constructor error", ex);
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
            ihcDomain = new IhcDomain();

            if (!ihcDomain.IhcSettings.IsValid())
            {
                var longinWindow = new LoginDialog(ihcDomain!.IhcSettings);
                await longinWindow.ShowDialog(this);
            }
            LoginUpdated();
        }
        catch (Exception ex)
        {
            activity?.SetError(ex);
            SetError(nameof(Start) + " error", ex);
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
    /// <para><b>The Complete Data Flow:</b></para>
    /// <code>
    /// IHC Services → LabAppService → MainWindowViewModel → DataContext Bindings → UI ComboBoxes
    ///                     ↑               ↑                      ↑
    ///                   Service        ViewModel               View
    /// </code>
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
                var operationFilter = CreateOperationFilter();
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
            SetWarning("OpenTelemetry not configured. It is recommended (but not required) to setup telemetry to view logs/traces. See guide in README for details.");
        }
    }

    public async void RunButtonClickHandler(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow)+"."+nameof(RunButtonClickHandler), ActivityKind.Internal);

        ClearErrorAndWarning();
        ClearOutput();

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
                        var operationMetadata = operationItem.OperationMetadata;

                        RunButton.IsEnabled = true;

                        // Create parameter controls with default values
                        OperationSupport.SetUpParameterControls(ParametersPanel, operationMetadata);

                        // Wait for Avalonia layout pass to complete so DynField.OnAttachedToVisualTree() is called
                        // and child controls are created before we try to restore values
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

                        // Restore previously set argument values from LabAppService
                        try
                        {
                            SyncArgumentsFromLabAppService();
                        }
                        catch (Exception ex)
                        {
                            // Log warning but continue with default values if restoration fails
                            logger.LogWarning(ex, "Failed to restore argument values from LabAppService for operation {OperationName}", operationMetadata.Name);
                        }

                        // Subscribe to ValueChanged events for real-time GUI → LabAppService synchronization
                        SubscribeToDynFieldEvents(ParametersPanel);

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
                SetError("Parameter control setup error", ex);
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
            ClearErrorAndWarning();
            ClearOutput();

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
    
     public void ClearOutput()
    {
        viewModel?.ClearOutput();
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

    private enum MessageLevel { Error, Warning }

    public void ClearErrorAndWarning()
    {
        viewModel?.ClearErrorAndWarning();
    }

    private void SetMessage(MessageLevel level, string text, Exception? ex = null)
    {
        if (level == MessageLevel.Error)
        {
            viewModel?.SetError(text, ex);
        }
        else
        {
            viewModel?.SetWarning(text, ex);
        }
    }

    public void SetError(string text, Exception? ex = null) => SetMessage(MessageLevel.Error, text, ex);

    public void SetWarning(string text, Exception? ex = null) => SetMessage(MessageLevel.Warning, text, ex);

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
        SetError(operation + " error", ex);
    }

    /// <summary>
    /// Creates an operation filter function for LabAppService.
    /// Excludes: AsyncEnumerable operations, operations with array parameters, operations with ResourceValue parameters.
    /// </summary>
    /// <returns>Filter function for LabAppService</returns>
    private static Func<ServiceOperationMetadata, bool> CreateOperationFilter()
    {
        return (ServiceOperationMetadata operation) =>
        {
            // Not sure how to support IAsyncEnumerable so disable it for now
            if (operation.Kind == ServiceOperationKind.AsyncEnumerable)
                return false;

            // Check if any parameter contains unsupported types
            foreach (var parameter in operation.Parameters)
            {
                if (ContainsUnsupportedTypeForFilter(parameter))
                    return false;
            }

            return true; // by default allow everything else
        };
    }

    /// <summary>
    /// Recursively checks if a field contains unsupported types.
    /// Checks for arrays, ResourceValue types, and recursively checks subtypes.
    /// </summary>
    /// <param name="field">The field metadata to check</param>
    /// <returns>True if the field contains unsupported types, false otherwise</returns>
    private static bool ContainsUnsupportedTypeForFilter(FieldMetaData field)
    {
        // Check if this field is an array
        if (field.IsArray)
            return true;

        // Check if this field is ResourceValue or ResourceValue[]
        if (field.Type == typeof(ResourceValue) || field.Type == typeof(ResourceValue[]))
            return true;

        // Recursively check subtypes
        foreach (var subType in field.SubTypes)
        {
            if (ContainsUnsupportedTypeForFilter(subType))
                return true;
        }

        return false; // by default nothing should be unsupported
    }

    /// <summary>
    /// Syncs parameter values from DynField GUI controls to LabAppService.SelectedOperation.Arguments.
    /// Extracts values from ParametersPanel and uses SetArgumentsFromArray for type-safe bulk setting.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LabAppService is not configured.</exception>
    private void SyncArgumentsToLabAppService()
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(SyncArgumentsToLabAppService), ActivityKind.Internal);

        if (labAppService == null)
            throw new InvalidOperationException("LabAppService is not configured");

        var operationMetadata = labAppService.SelectedOperation.OperationMetadata;

        // Extract parameter values from GUI controls using existing helper
        var parameterValues = OperationSupport.GetParameterValues(ParametersPanel, operationMetadata.Parameters);

        labAppService.SelectedOperation.SetMethodArgumentsFromArray(parameterValues);

        activity?.SetTag("arguments.synced_count", parameterValues.Length);
    }

    /// <summary>
    /// Syncs argument values FROM LabAppService.SelectedOperation.Arguments TO DynField GUI controls.
    /// This enables argument persistence - when user returns to an operation, previously set values are restored.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LabAppService is not configured.</exception>
    private void SyncArgumentsFromLabAppService()
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(SyncArgumentsFromLabAppService), ActivityKind.Internal);

        if (labAppService == null)
            throw new InvalidOperationException("LabAppService is not configured");

        var operationItem = labAppService.SelectedOperation;
        var operationMetadata = operationItem.OperationMetadata;
        var savedArguments = operationItem.GetMethodArgumentsAsArray();

        int restoredCount = 0;
        int failedCount = 0;

        // For each parameter, restore its value recursively
        for (int i = 0; i < operationMetadata.Parameters.Length; i++)
        {
            var parameter = operationMetadata.Parameters[i];
            var savedValue = savedArguments[i];

            try
            {
                RestoreFieldValue(ParametersPanel, parameter, savedValue, i.ToString(), ref restoredCount);
            }
            catch (Exception ex)
            {
                failedCount++;
                activity?.AddEvent(new ActivityEvent("argument_restore_failed", tags: new ActivityTagsCollection
                {
                    { "parameter.name", parameter.Name },
                    { "parameter.index", i.ToString() },
                    { "parameter.value", savedValue?.ToString() ?? "null" },
                    { "exception.type", ex.GetType().Name },
                    { "exception.message", ex.Message }
                }));
                // Continue with other parameters - don't fail entire restoration
            }
        }

        activity?.SetTag("arguments.restored_count", restoredCount);
        activity?.SetTag("arguments.failed_count", failedCount);
    }

    /// <summary>
    /// Recursively restores field values from saved arguments to DynField controls.
    /// Handles both simple types (single DynField) and complex types (multiple sub-fields).
    /// </summary>
    /// <param name="parent">The parent panel containing the DynField controls.</param>
    /// <param name="field">The field metadata describing the parameter structure.</param>
    /// <param name="savedValue">The saved value to restore.</param>
    /// <param name="indexPath">The index path for finding the DynField (e.g., "0", "1.2", "2.1.0").</param>
    /// <param name="restoredCount">Running count of successfully restored fields.</param>
    private void RestoreFieldValue(Panel parent, FieldMetaData field, object? savedValue, string indexPath, ref int restoredCount)
    {
        // For simple types and file types, find the DynField and set its value directly
        if (field.IsSimple || field.IsFile)
        {
            var dynField = OperationSupport.FindDynFieldByName(parent, indexPath);
            if (dynField != null)
            {
                // Restore value even if null - DynField will handle null values appropriately
                dynField.Value = savedValue;
                restoredCount++;
            }
            return;
        }

        // For complex types with subtypes, recursively restore sub-field values
        if (field.SubTypes.Length > 0)
        {
            // Skip restoration if saved value is null (can't extract sub-properties from null)
            if (savedValue == null)
                return;

            for (int i = 0; i < field.SubTypes.Length; i++)
            {
                var subField = field.SubTypes[i];
                string subIndexPath = $"{indexPath}.{i}";

                // Get the property value from the saved object
                var property = field.Type.GetProperty(subField.Name);
                if (property != null && property.CanRead)
                {
                    var subValue = property.GetValue(savedValue);
                    RestoreFieldValue(parent, subField, subValue, subIndexPath, ref restoredCount);
                }
            }
        }
    }

    /// <summary>
    /// Handles ArgumentChanged events from LabAppService.
    /// Updates GUI controls when LabAppService arguments are changed programmatically.
    /// </summary>
    private void OnLabAppServiceArgumentChanged(object? sender, LabAppService.MethodArgumentChangedEventArgs e)
    {
        if (labAppService == null)
            return;

        try
        {
            var operationMetadata = labAppService.SelectedOperation.OperationMetadata;
            if (e.Index < 0 || e.Index >= operationMetadata.Parameters.Length)
                return;

            var parameter = operationMetadata.Parameters[e.Index];
            var newValue = e.NewValue;

            // For simple types, directly update the DynField
            if (parameter.IsSimple || parameter.IsFile)
            {
                var dynField = OperationSupport.FindDynFieldByName(ParametersPanel, e.Index.ToString());
                if (dynField != null)
                {
                    dynField.Value = newValue;
                }
            }
            else
            {
                // For complex types, recursively update all sub-fields
                UpdateGuiFromComplexParameter(ParametersPanel, parameter, newValue, e.Index.ToString());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync LabAppService argument change to GUI");
        }
    }

    /// <summary>
    /// Recursively updates GUI controls from a complex parameter object.
    /// </summary>
    private void UpdateGuiFromComplexParameter(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        if (field.SubTypes.Length == 0 || value == null)
            return;

        // Recursively update each sub-field
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subIndexPath = $"{indexPath}.{i}";

            // Get the property value from the complex object
            var property = field.Type.GetProperty(subField.Name);
            if (property != null && property.CanRead)
            {
                var subValue = property.GetValue(value);

                if (subField.IsSimple || subField.IsFile)
                {
                    // Update the DynField for this sub-field
                    var dynField = OperationSupport.FindDynFieldByName(parent, subIndexPath);
                    if (dynField != null)
                    {
                        dynField.Value = subValue;
                    }
                }
                else
                {
                    // Recursively handle nested complex types
                    UpdateGuiFromComplexParameter(parent, subField, subValue, subIndexPath);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to ValueChanged events for all DynField controls in the panel.
    /// Enables real-time synchronization from GUI to LabAppService.
    /// </summary>
    /// <param name="parent">The panel containing DynField controls.</param>
    private void SubscribeToDynFieldEvents(Panel parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is DynField dynField)
            {
                dynField.ValueChanged += OnDynFieldValueChanged;
            }
            else if (child is Panel childPanel)
            {
                // Recursively subscribe to nested panels
                SubscribeToDynFieldEvents(childPanel);
            }
        }
    }

    /// <summary>
    /// Handles ValueChanged events from DynField controls.
    /// Immediately syncs the changed value to LabAppService.
    /// </summary>
    private void OnDynFieldValueChanged(object? sender, EventArgs e)
    {
        if (sender is not DynField dynField || labAppService == null)
            return;

        try
        {
            // Get the index path from the DynField name (e.g., "0", "1.2", "2.1.0")
            string indexPath = dynField.Name ?? "";
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
                var value = dynField.Value;
                labAppService.SelectedOperation.SetMethodArgument(parameterIndex, value);
            }
            else
            {
                // For complex types, reconstruct the ENTIRE object from ALL DynFields
                // This ensures all sub-fields are captured, not just the one that changed
                var reconstructedValue = OperationSupport.GetFieldValue(ParametersPanel, parameter, parameterIndex.ToString());
                labAppService.SelectedOperation.SetMethodArgument(parameterIndex, reconstructedValue);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync DynField value change to LabAppService");
        }
    }

}