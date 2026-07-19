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
            var session = new ProjectWorkflow(projectService, backup, recent, dialogs, loggerFactory);
            var themeService = new ThemeService();
            var viewModel = new MainWindowViewModel(session, dialogs, recent, themeService, config, loggerFactory);

            var window = new MainWindow { DataContext = viewModel };
            dialogs.Owner = window;
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => session.Dispose();

            // Open the standard empty project (or offer crash recovery) once the window is shown, so any
            // recovery prompt has a visible owner.
            window.Opened += async (_, _) => await viewModel.InitializeAsync(Program.SkipRecovery);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
