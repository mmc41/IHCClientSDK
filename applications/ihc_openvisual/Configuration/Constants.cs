using System;
using System.IO;

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

    /// <summary>
    /// One of the application's own files or folders under the user's application data — every preference store,
    /// the imported catalog and the last-resort start-up log land through here.
    /// </summary>
    /// <remarks>
    /// The folder name is <see cref="AppName"/> rather than a literal beside each store. Spelled out per site it
    /// was the one string that decides where a user's preferences live, retyped once per store — and a typo in
    /// any copy silently gives that store a folder of its own that nothing else reads.
    /// </remarks>
    /// <param name="name">The file or folder name inside the application's app-data folder.</param>
    public static string AppDataPath(string name) =>
        UnderAppFolder(Environment.SpecialFolder.ApplicationData, name);

    /// <summary>
    /// One of the application's own files or folders under the user's LOCAL application data — the folder for
    /// what must not follow the user to another machine.
    /// </summary>
    /// <remarks>
    /// The distinction is roaming, and it is the whole reason this exists beside <see cref="AppDataPath"/>: a
    /// preference the user set belongs to the user and should follow them, while a cache keyed to this machine's
    /// hardware or to these exact binaries belongs to the installation and must not. Putting the second kind in
    /// the roaming folder syncs it at every logon to be discarded on arrival.
    /// </remarks>
    /// <param name="name">The file or folder name inside the application's local app-data folder.</param>
    public static string LocalAppDataPath(string name) =>
        UnderAppFolder(Environment.SpecialFolder.LocalApplicationData, name);

    // The AppName segment lives here alone, for the reason AppDataPath's remarks give: retyped per door, it is
    // the one string a typo silently forks into a folder nothing else reads.
    private static string UnderAppFolder(Environment.SpecialFolder folder, string name) =>
        Path.Combine(Environment.GetFolderPath(folder), AppName, name);
}
