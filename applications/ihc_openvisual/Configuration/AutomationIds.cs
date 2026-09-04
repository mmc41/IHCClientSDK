namespace ihc_openvisual.Configuration;

/// <summary>
/// The automation ids a UI-Automation driver targets, declared once.
///
/// <para>These ids are a CONTRACT with the end-to-end suite, which finds elements by them. Written as literals
/// they were that contract in two places — the markup that publishes one and the test that quotes it — with
/// nothing comparing the copies, so a renamed id failed at run time in a suite that is outside every default
/// verification. Declared here, the markup binds through <c>{x:Static}</c> and the suite references the same
/// field, so a rename is a compile error on one side or the other.</para>
///
/// <para><b>Only what a driver targets belongs here.</b> The application publishes far more ids than this, and
/// most are audited generically rather than quoted by name — a constant for one of those would be a declaration
/// nothing reads. <c>AutomationIdConstantsTests</c> holds every constant below to a control that publishes it.</para>
///
/// <para><b>Command ids are NOT here.</b> A menu leaf publishes its <c>CommandRegistry</c> row id
/// (<c>edit.undo</c>, <c>project.info</c>), and the registry is that id's single source already — with
/// <c>RegistryXamlConsistencyTests</c> pinning the markup to it. Copying those rows into this class would create
/// the second source this class exists to remove.</para>
/// </summary>
public static class AutomationIds
{
    /// <summary>The shell.</summary>
    public const string MainWindow = "MainWindow";

    /// <summary>The locality tree (<c>Lokaliteter</c>).</summary>
    public const string InstallationTree = "InstallationTree";

    /// <summary>The function tree (<c>Funktioner</c>).</summary>
    public const string FunctionsTree = "FunctionsTree";

    /// <summary>The shell's menu bar.</summary>
    public const string MenuBar = "MenuBar";

    /// <summary>The <c>Rediger</c> menu title.</summary>
    public const string MenuEdit = "MenuEdit";

    /// <summary>The <c>Vis</c> menu title.</summary>
    public const string MenuView = "MenuView";

    /// <summary>The <c>Dokumentation</c> menu title.</summary>
    public const string MenuDocumentation = "MenuDocumentation";

    /// <summary>The Problemer panel's root.</summary>
    public const string ProblemsPanel = "ProblemsPanel";

    /// <summary>The Problemer panel's row list.</summary>
    public const string ProblemsList = "ProblemsList";

    /// <summary>The Problemer panel's state line, which a driver reads to tell validating from bound.</summary>
    public const string ProblemsStateText = "ProblemsStateText";

    /// <summary>The Problemer panel's progress indicator; its visibility is what "still validating" looks like.</summary>
    public const string ProblemsSpinner = "ProblemsSpinner";

    /// <summary>
    /// The stem of a tier's count id, completed with the lower-cased tier — <c>problems.count.error</c>.
    /// A prefix rather than a set of constants, so the ids follow the tier set instead of being a second list.
    /// </summary>
    public const string ProblemsCountPrefix = "problems.count.";

    /// <summary>The project-information dialog.</summary>
    public const string ProjectInfoWindow = "ProjectInfoWindow";

    /// <summary>Its project-number field, which the dialog pre-focuses.</summary>
    public const string ProjNumberBox = "ProjNumberBox";

    /// <summary>The terminal-addressing dialog.</summary>
    public const string PinPropertiesWindow = "PinPropertiesWindow";

    /// <summary>Its <c>Ledningsfarve</c> field.</summary>
    public const string CableColourBox = "CableColourBox";

    /// <summary>Its terminal picker.</summary>
    public const string TerminalList = "TerminalList";

    /// <summary>
    /// The terminal dialog's commit button. The same x:Name appears in several dialogs; this constant is about
    /// THIS one, which is the dialog the end-to-end scenarios operate.
    /// </summary>
    public const string OkButton = "OkButton";

    /// <summary>The terminal dialog's dismiss button, on the same terms as <see cref="OkButton"/>.</summary>
    public const string CancelButton = "CancelButton";

    /// <summary>The composed product dialog.</summary>
    public const string ProductDialogWindow = "ProductDialogWindow";
}
