using Avalonia.Controls;

namespace ihc_openvisual.Views;

/// <summary>
/// The Problemer panel: the shell's bottom region listing the current project's validation findings.
/// </summary>
/// <remarks>
/// A UserControl rather than more markup inside <see cref="MainWindow"/>, because the panel grows a findings
/// table, per-severity chrome and a staleness presentation of its own — and because the seam is where its
/// view-model binds. It inherits the shell's DataContext until that view-model exists.
/// </remarks>
public partial class ProblemsPanel : UserControl
{
    public ProblemsPanel() => InitializeComponent();
}
