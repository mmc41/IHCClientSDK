namespace Ihc.UiAutomation;

/// <summary>
/// How far from an element a search reaches.
/// </summary>
/// <remarks>
/// The toolkit's own enum rather than UI Automation's <c>TreeScope</c>, because the generated COM types are
/// internal to this assembly — a public signature may not name one. The values are the ones a driver has a use
/// for; UI Automation's remaining scopes (parent, ancestors) are searches upward, which nothing here does.
/// </remarks>
public enum UiaScope
{
    /// <summary>The element's immediate children only.</summary>
    Children,

    /// <summary>Every element beneath it, at any depth, excluding the element itself.</summary>
    Descendants,

    /// <summary>The element and everything beneath it.</summary>
    Subtree,
}

/// <summary>
/// UI Automation's control-type vocabulary, as the numeric ids the platform publishes.
/// </summary>
/// <remarks>
/// A closed set fixed by the Windows SDK, mirrored here for the same reason as <see cref="UiaScope"/>: the
/// generated <c>UIA_CONTROLTYPE_ID</c> is internal, and a control type is something a caller both reads off an
/// element and searches by. The numeric values ARE the contract — they are what crosses the COM boundary.
/// </remarks>
public enum UiaControlType
{
    /// <summary>Not one of the standard types, or an element that reports none.</summary>
    Unknown = 0,

    Button = 50000,
    Calendar = 50001,
    CheckBox = 50002,
    ComboBox = 50003,
    Edit = 50004,
    Hyperlink = 50005,
    Image = 50006,
    ListItem = 50007,
    List = 50008,
    Menu = 50009,
    MenuBar = 50010,
    MenuItem = 50011,
    ProgressBar = 50012,
    RadioButton = 50013,
    ScrollBar = 50014,
    Slider = 50015,
    Spinner = 50016,
    StatusBar = 50017,
    Tab = 50018,
    TabItem = 50019,
    Text = 50020,
    ToolBar = 50021,
    ToolTip = 50022,
    Tree = 50023,
    TreeItem = 50024,
    Custom = 50025,
    Group = 50026,
    Thumb = 50027,
    DataGrid = 50028,
    DataItem = 50029,
    Document = 50030,
    SplitButton = 50031,
    Window = 50032,
    Pane = 50033,
    Header = 50034,
    HeaderItem = 50035,
    Table = 50036,
    TitleBar = 50037,
    Separator = 50038,
    SemanticZoom = 50039,
    AppBar = 50040,
}

/// <summary>Where a ranged control currently sits, and how far it can travel.</summary>
/// <param name="Value">The current position.</param>
/// <param name="Minimum">The lowest position it accepts.</param>
/// <param name="Maximum">The highest position it accepts.</param>
/// <param name="LargeChange">
/// How far one page moves it. Zero when the control declares none, in which case a caller that wants to page
/// has to choose a step of its own.
/// </param>
public readonly record struct UiaRange(double Value, double Minimum, double Maximum, double LargeChange);

/// <summary>Whether a control that expands is currently open, and whether it can be.</summary>
public enum UiaExpandState
{
    /// <summary>The element exposes no ExpandCollapse pattern.</summary>
    NotExpandable,

    /// <summary>Closed, with children to show.</summary>
    Collapsed,

    /// <summary>Open.</summary>
    Expanded,

    /// <summary>Open, but only partly — some descendants are still collapsed.</summary>
    PartiallyExpanded,

    /// <summary>It exposes the pattern but has nothing to expand.</summary>
    LeafNode,
}
