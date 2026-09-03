using System.Threading.Tasks;
using Avalonia.Threading;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Opening this application's own main window, which <see cref="AvaloniaTestBase"/> cannot offer: the
    /// shared base is compiled into every Avalonia suite and so must name no one application's types.
    ///
    /// <para>The suite imports this statically (see safe_lab_tests.csproj), so a fixture calls
    /// <see cref="SetupMainWindowAsync"/> unqualified exactly as it did when the method was inherited.</para>
    /// </summary>
    internal static class LabWindows
    {
        /// <summary>
        /// Creates, initializes, shows and returns a <see cref="MainWindow"/>, registering it as
        /// <see cref="AvaloniaTestBase.CurrentTestWindow"/> so a failure is captured automatically.
        /// </summary>
        /// <returns>The initialized and shown MainWindow instance.</returns>
        internal static async Task<MainWindow> SetupMainWindowAsync()
        {
            AvaloniaTestBase.CurrentTestWindow = await new MainWindow().Start();
            AvaloniaTestBase.CurrentTestWindow.Show();
            Dispatcher.UIThread.RunJobs();
            return (MainWindow)AvaloniaTestBase.CurrentTestWindow;
        }
    }
}
