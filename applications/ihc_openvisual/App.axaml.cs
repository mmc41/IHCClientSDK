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

            var projectService = new ProjectAppService(settings);
            var backup = BackupService.CreateDefault();
            var recent = RecentProjectsStore.CreateDefault();
            var dialogs = new AvaloniaDialogService(loggerFactory);
            var session = new ProjectWorkflow(projectService, backup, recent, dialogs, loggerFactory,
                installerIdentity: InstallerIdentityStore.CreateDefault(),
                dataTables: DataTableStore.CreateDefault());
            var themeService = new ThemeService();
            // Adopt the platform's high-contrast preference now and keep following it (US-001): Avalonia reports
            // the preference but ships no high-contrast theme, so the palette is ours to supply (BP-13).
            themeService.FollowPlatformContrast();
            var viewModel = new MainWindowViewModel(session, dialogs, recent, themeService, config, loggerFactory);

            var window = new MainWindow { DataContext = viewModel };
            dialogs.Owner = window;
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) =>
            {
                viewModel.Dispose();   // detach the VM's session/recent event handlers first
                session.Dispose();
            };

            // Open the start-up document — the file named on the command line ("Open with…" / a double-clicked
            // .vis), or the standard empty project — once the window is shown, so any recovery prompt or
            // open-failure dialog has a visible owner.
            window.Opened += async (_, _) =>
                await viewModel.InitializeAsync(Program.SkipRecovery, Program.StartupProjectPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
