namespace IhcLab;

/// <summary>
/// Constants for MainWindow control names.
///
/// IMPORTANT: These constants must be kept in sync with the Name attributes in MainWindow.axaml.
/// XAML cannot reference C# constants directly, so the AXAML file will still contain
/// hardcoded name strings. These constants serve as a single source of truth for:
/// - Unit tests that use FindControl()
/// - Documentation of the MainWindow UI structure
/// - Compile-time checking to prevent typos
/// </summary>
public static class MainWindowNames
{
    // Menu Items
    /// <summary>MenuItem for copying output/result to clipboard</summary>
    public const string CopyOutputMenuItem = nameof(CopyOutputMenuItem);

    /// <summary>MenuItem for copying error/warning to clipboard</summary>
    public const string CopyErrorMenuItem = nameof(CopyErrorMenuItem);

    /// <summary>MenuItem for opening telemetry diagnostics in browser</summary>
    public const string OpenTelemetryMenuItem = nameof(OpenTelemetryMenuItem);

    // Primary Controls
    /// <summary>ComboBox for selecting IHC service</summary>
    public const string ServicesComboBox = nameof(ServicesComboBox);

    /// <summary>ComboBox for selecting operation on the selected service</summary>
    public const string OperationsComboBox = nameof(OperationsComboBox);

    /// <summary>Button to execute the selected operation</summary>
    public const string RunButton = nameof(RunButton);

    // Layout Sections
    /// <summary>Border containing operation details section</summary>
    public const string Details = nameof(Details);

    /// <summary>Grid within Details section</summary>
    public const string DetailGrid = "detail";

    /// <summary>Border containing output/result section</summary>
    public const string Result = nameof(Result);

    /// <summary>Border containing error/warning status section</summary>
    public const string Status = nameof(Status);

    // Detail Section Controls
    /// <summary>TextBlock displaying the selected operation's description</summary>
    public const string OperationDescription = nameof(OperationDescription);

    /// <summary>StackPanel containing dynamically generated parameter controls</summary>
    public const string ParametersPanel = nameof(ParametersPanel);

    // Output Section Controls
    /// <summary>TextBlock heading for output section</summary>
    public const string OutputHeading = nameof(OutputHeading);

    /// <summary>SelectableTextBlock displaying operation output/results</summary>
    public const string Output = nameof(Output);

    // Status Section Controls
    /// <summary>TextBlock heading shown when errors are present</summary>
    public const string ErrorHeading = nameof(ErrorHeading);

    /// <summary>TextBlock heading shown when warnings are present</summary>
    public const string WarningHeading = nameof(WarningHeading);

    /// <summary>TextBlock displaying error or warning messages</summary>
    public const string ErrorWarningContent = nameof(ErrorWarningContent);
}
