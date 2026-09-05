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

    /// <summary>
    /// The composition root's entry point into the instrumentation core. A span from here is named
    /// <c>App.&lt;operation&gt;</c>, and this layer opens exactly one of them: the launch.
    /// </summary>
    private static readonly Ihc.OperationTelemetry Telemetry =
        new(AppTelemetryRegistry.Surface, nameof(App));

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // THE LAUNCH, as one operation. Everything below — building the SDK service, the workflow (which
            // loads the persisted catalog imports), the shell (which builds the catalog menus and both trees)
            // — and then the start-up document load in the Opened handler at the end are one thing a person
            // waits for. Each used to open a trace of its own, so "why was the window slow to appear" had no
            // operation to open, only fragments.
            //
            // Held in a local rather than a `using`: the last phase runs in the Opened handler below, which is
            // where this is disposed. The cost of that shape, stated: a launch that throws before reaching the
            // handler never disposes the scope and exports no span — which is what happens today anyway, and
            // Main's catch is what reports such a failure.
            System.Diagnostics.Activity? ambient = System.Diagnostics.Activity.Current;
            Ihc.OperationScope startup = Telemetry.Start("Startup");
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
            window.Opened += async (_, _) =>
            {
                // The launch span is made ambient again before the load starts, because this handler runs off the
                // window message loop under whatever execution context the dispatcher restores rather than the
                // one the scope was opened in; without it the load would open a trace of its own and the launch
                // would split in two exactly where the wait is longest. InitializeAsync opens its span in its
                // synchronous prefix, so it is already parented by the time the task comes back.
                //
                // NOT restored afterwards, and that is safe for a reason belonging to this lambda rather than to
                // discipline: it is `async`, and an async method's kickoff restores the execution context once
                // the synchronous prefix yields — so nothing assigned here reaches the message loop that raised
                // the event. The composition root below needs its own restore precisely because it is NOT async.
                if (startup.Activity is { } launch)
                {
                    System.Diagnostics.Activity.Current = launch;
                }
                Task load = viewModel.InitializeAsync(Program.StartupProjectPath);

                // The launch ENDS when there is a document to look at. Awaited here rather than fired at a
                // supervisor, so this stays the async void hook whose fault reaches the view-model's own error
                // boundary — the containment shape G1 anchors on. Disposal is idempotent, so a window shown
                // twice records the first launch and nothing further.
                try
                {
                    await load;
                }
                finally
                {
                    startup.Dispose();
                }
            };

            // The launch stops being AMBIENT here, though it stays OPEN until the handler above closes it.
            // Starting a span makes it current, and this method is a PLAIN method running straight off the
            // message loop — no async kickoff to restore the context on the way out, so what it leaves current
            // is what the message loop keeps, for the life of the process. Measured before this line existed:
            // the quit prompt arrived minutes later as a 3.1 s child of a 1.5 s launch that had closed long
            // before, and any window-lifecycle callback would have done the same. The `async` handler above
            // needs no such line, which is the whole of the difference between the two.
            //
            // Placed here rather than in a finally around the whole composition, deliberately: the only way past
            // it is an exception during composition, and that path does not reach a usable application at all —
            // Main's catch logs it and the process exits, so there is no later work left to mis-parent.
            System.Diagnostics.Activity.Current = ambient;
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
