using System.IO;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;

namespace ihc_openvisual.DesignTime;

/// <summary>
/// The shell view-model the XAML PREVIEWER binds to (architecture checklist A-13). Without one the designer renders
/// an empty frame: every pane, label and command state in <c>MainWindow.axaml</c> is bound, so with no data context
/// there is nothing to lay out and nothing to look at while editing the markup.
/// <para>
/// A subclass rather than a second constructor on <see cref="MainWindowViewModel"/> — the shape the earlier
/// review settled on (AP-18/A-13). The parameterless constructor that used to live on the production view-model
/// was reachable only from here, was called by nobody, and created two never-deleted temp files every time it ran;
/// keeping design-time construction in its own type means the real constructor cannot drift from the real
/// composition root, and it is pinned by <c>OpenVisualDesignTimeTests</c>.
/// </para>
/// <para>
/// It lives OUTSIDE <c>ViewModels</c> because of what it does rather than what it is: it composes the null
/// dialog/theme adapters, and a view-model that names a concrete UI-effect adapter is exactly what the layering
/// rule forbids (<c>ViewModels_DependOnUiEffectPortsNotImplementations</c>). This is a composition root for the
/// designer — a sibling of <c>App.OnFrameworkInitializationCompleted</c>, which composes the real ones — and it
/// belongs with the other composition, not with the view-models it composes.
/// </para>
/// <para>
/// Side-effect free, and that is a REQUIREMENT, not a nicety: the previewer re-runs this constructor on every
/// markup change, so it must not touch the installer's real state. Every store is pointed at a path under the temp
/// directory that is only ever read (each store treats a missing file as empty), the project is built in memory
/// from the SDK template, and nothing is ever written.
/// </para>
/// </summary>
public sealed class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel()
        : this(new NullDialogService(), new RecentProjectsStore(DesignPath("recent.json")))
    {
    }

    // Chained through a private constructor so the shell and the workflow it wraps share ONE dialog adapter and ONE
    // recent-projects store, which is the shape of the composition root this mirrors (App.OnFrameworkInitialization-
    // Completed threads a single instance of each into both). Building them twice gave the previewer two stores over
    // the same path, so the shell's recent list and the workflow's were unrelated objects.
    private DesignMainWindowViewModel(NullDialogService dialogs, RecentProjectsStore recent)
        : base(DesignWorkflow(dialogs, recent), dialogs, recent, new NullThemeService())
    {
        StatusText = "Tryk F1 for hjælp";
    }

    private static ProjectWorkflow DesignWorkflow(IDialogService dialogs, RecentProjectsStore recent)
    {
        // NO fault port and no fault sink, deliberately — the omission is the design decision, not an
        // oversight of the real root's wiring. The previewer's process has no logging pipeline, no Problemer
        // panel and no user to report to, so a port here would have nowhere to report and a sink would collect
        // rows nothing can render. Both doors are optional for exactly this case.
        var service = new ProjectAppService(new IhcSettings());
        var workflow = new ProjectWorkflow(service, recent, dialogs, catalogDir: DesignPath("catalog"));
        // The standard empty project, so the previewer shows the shell with its ten localities rather than two
        // blank panes. In memory only — no file is read or written — and synchronous in practice: nothing is dirty,
        // so the save prompt this would otherwise raise is never reached.
        _ = workflow.NewAsync();
        return workflow;
    }

    // A directory that is never created. The stores read through a missing path as "nothing stored yet", which is
    // exactly the state a preview should show.
    private static string DesignPath(string name) =>
        Path.Combine(Path.GetTempPath(), "ihc-openvisual-design", name);
}
