using System.IO;
using System.Windows.Input;

namespace ihc_openvisual.ViewModels;

/// <summary>One entry in the <i>File</i> menu's recent-projects list (US-004): the file name to show, the full
/// path to open, and the shell command that opens it.</summary>
public sealed class RecentProjectViewModel
{
    public RecentProjectViewModel(string path, ICommand openCommand)
    {
        Path = path;
        DisplayName = System.IO.Path.GetFileName(path);
        OpenCommand = openCommand;
    }

    public string Path { get; }

    public string DisplayName { get; }

    /// <summary>The stable id this generated menu item publishes to UI Automation. Derived from the FULL path, which
    /// is the entry's identity: <see cref="DisplayName"/> is only the file name, so two projects called
    /// <c>anlaeg.vis</c> in different folders would share it — and the display text is the very thing an id exists to
    /// avoid addressing by (UX review SPEC-01; the items carried no id at all). The prefix keeps it in the coined
    /// <c>menu.*</c> namespace the non-registry items use.</summary>
    public string AutomationId => $"menu.recentProject:{Path}";

    public ICommand OpenCommand { get; }
}
