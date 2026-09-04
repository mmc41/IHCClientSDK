using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ihc_openvisual.Configuration;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // The one token App.axaml cannot state: the monospace family is chosen per platform (none is embedded),
        // and XAML has no platform switch. Written here, beside the sizes it pairs with, rather than in
        // ThemeService — the family never changes at run time, so nothing re-writes it.
        Resources["MonoFontFamily"] = new Avalonia.Media.FontFamily(Program.MonoFontFamily);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Composition root: one shared logging/telemetry pipeline, one ProjectAppService and one
            // ProjectWorkflow for the whole window (US-063). The app is file-only: no controller is passed, so
            // the SDK service runs against its embedded catalog with no network/install needed.
            ILoggerFactory loggerFactory = Program.LoggerFactory ?? NullLoggerFactory.Instance;
            AppConfiguration? config = Program.Config;
            Ihc.IhcSettings settings = config?.IhcSettings ?? new Ihc.IhcSettings();

            // The marshal for every background result the shell binds. It is declared HERE, at the composition
            // root, because this is the only layer allowed to name Avalonia — the fault sink, the workflow, its
            // validation monitor and the worker all take it as a delegate. Background priority so binding a
            // findings list never competes with input or render.
            //
            // ONE delegate, shared. Written out per consumer it was the same three lines asserting in a comment
            // that they matched; a priority changed in one copy would silently give two background results two
            // different orderings.
            Action<Action> post = action => Avalonia.Threading.Dispatcher.UIThread.Post(
                action, Avalonia.Threading.DispatcherPriority.Background);

            // FIRST, before anything that can fault into it. The application's own fault sink: it outlives
            // every document, is shared by every layer that can fault, and is what makes an internal error
            // durable rather than a log line nobody reads. It marshals through the same post the workflow uses,
            // so a fault arriving off the dispatcher reaches the UI thread the way findings do.
            var internalErrors = new InternalErrorLog(post);
            // The SDK's fault port (D16). An exception escaping an app-service operation is reported here before
            // it continues to its caller, so a failure that some catch further up turns into a dialog the user
            // dismisses still leaves a row behind. The SDK names no sink type and no logger: it takes a
            // delegate, and the composition root is the only layer that knows what is on the other end of it.
            var projectService = new ProjectAppService(settings, internalErrors.Append);
            var recent = RecentProjectsStore.CreateDefault(loggerFactory);
            var dialogs = new AvaloniaDialogService(loggerFactory);
            var session = new ProjectWorkflow(projectService, recent, dialogs, loggerFactory,
                installerIdentity: InstallerIdentityStore.CreateDefault(loggerFactory),
                dataTables: DataTableStore.CreateDefault(loggerFactory),
                post: post,
                faultSink: internalErrors.Append);
            // Cleared where the findings list resets (D02). Subscribed to the monitor's own announcement rather
            // than to a new event: the sink compares the generation itself, so nothing below has to know it exists.
            session.Validation.Changed += (_, _) => internalErrors.FollowGeneration(session.Validation.Generation);
            // The supervisor's port. Set HERE, once, because the supervisor is static — its callers are view
            // code-behind and a worker, layers with no constructor a port could be injected through. Unset, it
            // still observes; it simply has nowhere to report to.
            TaskSupervisor.ReportTo(internalErrors.Append);

            var themeService = new ThemeService();
            // Adopt the platform's high-contrast preference now and keep following it (US-001): Avalonia reports
            // the preference but ships no high-contrast theme, so the palette is ours to supply (BP-13).
            themeService.FollowPlatformContrast();
            var viewModel = new MainWindowViewModel(session, dialogs, recent, themeService, config, loggerFactory,
                internalErrors);

            var window = new MainWindow { DataContext = viewModel };
            dialogs.Owner = window;
            desktop.MainWindow = window;

            // The read-only test surface, and the ONE place the flag is read. It arrives as a value, so nothing
            // below can branch on it, and where the string goes is decided here because this is the only layer
            // allowed to name Avalonia. Off — which is every session a person starts — it subscribes to nothing
            // and writes nothing.
            var automation = new AutomationSnapshotPublisher(
                Program.TestSurfaceEnabled,
                snapshot => Avalonia.Automation.AutomationProperties.SetItemStatus(window, snapshot),
                session,
                internalErrors);

            // IN ORDER: the publisher lets go of the workflow's events, then the view-model detaches its
            // session/recent handlers, and only then is the session itself torn down.
            DisposeOnShutdown(desktop, automation, viewModel, session);

            // Open the start-up document — the file named on the command line ("Open with…" / a double-clicked
            // .vis), or the standard empty project — once the window is shown, so an open-failure dialog has a
            // visible owner.
            window.Opened += async (_, _) => await viewModel.InitializeAsync(Program.StartupProjectPath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Releases what the composition root built, in the order given, when the desktop lifetime shuts down.
    /// </summary>
    /// <remarks>
    /// One handler rather than a disposal line per object: the ORDER matters — a subscriber has to let go of an
    /// event before the object raising it is torn down — and an order stated once in a list cannot fall out of
    /// step the way three separate registrations can.
    /// </remarks>
    private static void DisposeOnShutdown(
        IClassicDesktopStyleApplicationLifetime desktop, params IDisposable[] owned) =>
        desktop.ShutdownRequested += (_, _) =>
        {
            foreach (IDisposable item in owned)
            {
                item.Dispose();
            }
        };
}
