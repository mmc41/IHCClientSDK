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
using Avalonia.Platform.Storage;
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

    // The most recent operation result and the name of the operation that produced it, used by
    // "Save Result to File…" so it saves the real result (e.g. binary bytes), not the display preview.
    private LabAppService.OperationResult? lastOperationResult;
    private string? lastResultOperationName;

    // True while a streaming (IAsyncEnumerable) operation is running; the Run button then acts as Stop (D7).
    private bool isStreaming;

    // Guards against a service->GUI->service echo while we push service values into the controls.
    private bool isSyncingFromService;

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
            viewModel?.SetError("Application startup error", ex);
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
            viewModel?.SetError("Startup error", ex);
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
            HandleOperationError(activity, "Login setup", ex);
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

                // Report the new endpoint in the title only once configuration has actually succeeded.
                this.Title = "IHC Lab : Endpoint set to " + (ihcDomain.IhcSettings.Endpoint ?? "(no endpoint set)");
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                viewModel?.SetError("Service configuration failed - check endpoint and credentials", ex);
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

        // While a stream is running the Run button acts as Stop (D7): cancel and let the running stream end.
        if (isStreaming)
        {
            labAppService?.StopStream();
            return;
        }

        viewModel?.ClearErrorAndWarning();
        viewModel?.ClearOutput();

        this.Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            // Streaming operations (IAsyncEnumerable, e.g. GetResourceValueChanges) use a live Start/Stop output
            // surface instead of the single-result Run path (D7).
            if (labAppService?.SelectedOperation?.OperationMetadata.Kind == ServiceOperationKind.AsyncEnumerable)
            {
                await RunStreamingOperation();
                return;
            }

            // Disable RUN for the duration of a normal (non-streaming) execute so it cannot be double-clicked. The
            // streaming branch above returns first, leaving its button enabled to act as STOP.
            RunButton.IsEnabled = false;

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

            // Remember the raw result so "Save Result to File…" can write the real bytes, not the display preview.
            lastOperationResult = operationResult;
            lastResultOperationName = labAppService.SelectedOperation.OperationMetadata.Name;

            await SetOutput(operationResult.DisplayResult, operationResult.ReturnType);
        } catch (Exception ex)
        {
           HandleOperationError(activity, "Run operation", ex);
        } finally
        {
            this.Cursor = Cursor.Default;
            RunButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Runs the selected streaming (IAsyncEnumerable) operation as a live stream (D7): the Run button becomes
    /// Stop, items append to the output area as they arrive, and Stop cancels via LabAppService's stream token.
    /// </summary>
    private async Task RunStreamingOperation()
    {
        if (labAppService == null)
            return;

        isStreaming = true;
        RunButton.Content = "STOP";
        ToolTip.SetTip(RunButton, "Click to stop streaming");
        this.Cursor = Cursor.Default; // streaming is long-running, not a brief wait
        viewModel?.SetOutput("(streaming - click STOP to end)\n", typeof(string));

        try
        {
            await labAppService.StartStream(item =>
            {
                // Items arrive on a background context; marshal to the UI thread to append to the output.
                Dispatcher.UIThread.Post(() =>
                {
                    if (viewModel != null)
                        viewModel.OutputText = AppendStreamLine(viewModel.OutputText, item?.ToString() ?? "null");
                });
            });
        }
        catch (Exception ex)
        {
            HandleOperationError(null, "Streaming operation", ex);
        }
        finally
        {
            isStreaming = false;
            RunButton.Content = "RUN";
            ToolTip.SetTip(RunButton, "Execute IHC operation");
        }
    }

    // Cap the retained streaming output so an indefinite long-poll (e.g. GetResourceValueChanges) cannot grow the
    // bound text without bound. When the cap is exceeded the buffer is trimmed (at a whole-line boundary) back to
    // StreamRetainChars and the cut is marked so the truncation is visible (not silent). Trimming a chunk rather
    // than a single oldest line matters because, once saturated, this runs on every streamed item: a per-item
    // single-line trim would rescan and recopy the whole buffer for each item (O(items x buffer)), whereas
    // trimming back to StreamRetainChars amortises that cost over the many items it takes to refill the headroom.
    private const int MaxStreamOutputChars = 100_000;
    private const int StreamRetainChars = 75_000;
    private const string StreamTrimMarker = "... (older output trimmed) ...\n";

    private static string AppendStreamLine(string current, string line)
    {
        string text = current + line + "\n";
        if (text.Length <= MaxStreamOutputChars)
            return text;

        int cut = text.Length - StreamRetainChars;
        int newlineIndex = text.IndexOf('\n', cut);
        string tail = newlineIndex >= 0 ? text.Substring(newlineIndex + 1) : text.Substring(cut);
        return StreamTrimMarker + tail;
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
                            OnParameterControlValueChanged);

                        // Initialize any still-uninitialized (null) service arguments from the freshly created
                        // GUI controls. This gives complex reference-type parameters a valid default instance
                        // instead of null, while deliberately NOT touching arguments that already hold a value -
                        // so values entered on a previous visit to this operation survive and are restored below
                        // rather than being overwritten with control defaults.
                        parameterSyncCoordinator.InitializeUninitializedArguments(ParametersPanel, operationItem);

                        // Restore previously set argument values from LabAppService into the GUI (argument persistence).
                        // Suppress the GUI change events this restore raises so they don't echo straight back as
                        // redundant GUI->service syncs - the values being pushed in already came from the service.
                        isSyncingFromService = true;
                        try
                        {
                            parameterSyncCoordinator.SyncFromService(ParametersPanel, operationItem);
                        }
                        catch (Exception ex)
                        {
                            // Log warning but continue with default values if restoration fails
                            logger.LogWarning(ex, "Failed to restore argument values from LabAppService for operation {OperationName}", operationItem.OperationMetadata.Name);
                        }
                        finally
                        {
                            isSyncingFromService = false;
                        }

                        // Unsubscribe from previous operation's MethodArgumentChanged event
                        if (currentOperationItem != null)
                        {
                            currentOperationItem.MethodArgumentChanged -= OnLabAppServiceMethodArgumentChanged;
                        }

                        // Subscribe to MethodArgumentChanged events for LabAppService → GUI synchronization
                        currentOperationItem = operationItem;
                        currentOperationItem.MethodArgumentChanged += OnLabAppServiceMethodArgumentChanged;
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
        // Stop any running stream first so its CancellationTokenSource is cancelled (releasing the controller
        // subscription via the enumerator's DisposeAsync) before the IHC domain is torn down.
        labAppService?.StopStream();

        // Clean up IHC domain when window is closing
        ihcDomain?.Dispose();

        // Unsubscribe from LabAppService events
        if (currentOperationItem != null)
        {
            currentOperationItem.MethodArgumentChanged -= OnLabAppServiceMethodArgumentChanged;
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
            HandleOperationError(activity, "About dialog", ex);
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

    /// <summary>
    /// Saves the most recent operation result to a file (US-C4 / D6 "smart save"): real bytes for a byte[] /
    /// BinaryFile result, the real XML for an IHC ProjectFile result (ISO-8859-1, *.vis), or the shown text
    /// otherwise - each with a type-appropriate file name/extension. Reads the raw result object rather than
    /// the display preview.
    /// </summary>
    public async void SaveOutputMenuItemClick(object sender, RoutedEventArgs e)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(MainWindow) + "." + nameof(SaveOutputMenuItemClick), ActivityKind.Internal);

        try
        {
            if (lastOperationResult == null)
            {
                viewModel?.SetWarning("No result to save. Run an operation first.");
                return;
            }

            var content = LabAppService.BuildResultFileContent(
                lastOperationResult.RawResult, lastOperationResult.DisplayResult, lastResultOperationName ?? "result");

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                throw new NotSupportedException("No top-level window available for the save dialog");

            // Carry the type-appropriate extension (e.g. "vis" for a project) so the dialog applies it even when
            // the user edits the name. Map an extension-less suggestion to null (not "") so every storage provider
            // treats it as "no default extension" rather than appending a stray trailing dot.
            string suggestedExtension = System.IO.Path.GetExtension(content.SuggestedFileName).TrimStart('.');

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Result to File",
                SuggestedFileName = content.SuggestedFileName,
                DefaultExtension = suggestedExtension.Length == 0 ? null : suggestedExtension
            });

            if (file == null)
                return; // user cancelled

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(content.Bytes);
        }
        catch (Exception ex)
        {
            HandleOperationError(activity, "Save result to file", ex);
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
    /// Handles MethodArgumentChanged events from LabAppService.
    /// Updates GUI controls when LabAppService arguments are changed programmatically.
    /// </summary>
    private void OnLabAppServiceMethodArgumentChanged(object? sender, LabAppService.MethodArgumentChangedEventArgs e)
    {
        if (labAppService == null)
            return;

        var operationMetadata = labAppService.SelectedOperation.OperationMetadata;
        if (e.Index < 0 || e.Index >= operationMetadata.Parameters.Length)
            return;

        // Suppress the GUI change events this update raises so they don't echo straight back to the service.
        isSyncingFromService = true;
        try
        {
            parameterSyncCoordinator.UpdateGuiFromParameter(ParametersPanel, e.NewValue, e.Index.ToString());
        }
        finally
        {
            isSyncingFromService = false;
        }
    }

    /// <summary>
    /// Handles ValueChanged events from strategy controls.
    /// Immediately syncs the changed value to LabAppService.
    /// </summary>
    private void OnParameterControlValueChanged(object? sender, EventArgs e)
    {
        if (labAppService == null || sender is not Control control)
            return;

        // Ignore GUI change events raised while we are programmatically restoring service values into the
        // controls; otherwise the service->GUI update would immediately echo back as a GUI->service sync.
        if (isSyncingFromService)
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

            // Extract the value via the control's own strategy. GetFieldValue locates the control, reads the
            // strategy from its ControlMetadata tag (falling back to the registry), and for complex types
            // reconstructs the entire object from all sub-controls - so simple, file and complex parameters
            // are all handled uniformly here, matching the service->GUI direction in ParameterSyncCoordinator.
            var value = OperationSupport.GetFieldValue(ParametersPanel, parameter, parameterIndex.ToString());
            labAppService.SelectedOperation.SetMethodArgument(parameterIndex, value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync control value change to LabAppService");
        }
    }

}