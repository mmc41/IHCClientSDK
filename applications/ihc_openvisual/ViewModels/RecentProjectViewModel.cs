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

    public ICommand OpenCommand { get; }
}
