namespace ihc_openvisual.Configuration;

/// <summary>
/// Fixed identity strings for the application (shown in the title, the About dialog and telemetry).
/// </summary>
public static class Constants
{
    public const string AppName = "IHC OpenVisual";
    public const string SdkRepoLink = "https://github.com/mmc41/IHCClientSDK";
    public const string Authors = "Morten Christensen (mmc41)";
    public const string AppDescription =
        "Open source-editor til IHC (Intelligent House Concept) .vis-projektfiler, på tværs af platforme.";

    /// <summary>The document name shown before the project has ever been saved to a file — the vendor's own token,
    /// lowercase as the vendor displays it ("unavngivet - LK IHC Visual"; alignment F-14, measured 2026-08-09).</summary>
    public const string UntitledDocument = "unavngivet";
}
