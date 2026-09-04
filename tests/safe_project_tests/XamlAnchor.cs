using System;

namespace Ihc.Vis.Tests;

/// <summary>
/// Finding a control in <c>MainWindow.axaml</c> BY TEXT, for the parity fixtures that read the markup as a
/// document rather than building the window.
/// </summary>
/// <remarks>
/// Those fixtures slice the markup between two automation ids to isolate one menu, which means the id has to be
/// findable as text. It is spelled two ways: as a literal, and — for the ids the end-to-end driver targets —
/// bound through <c>{x:Static}</c> to the constant of the same name, so that the application and the driver
/// declare it once between them. Both publish the identical id, and which spelling a control uses is not what
/// any of these fixtures is about, so resolving it belongs here rather than in each of them.
/// </remarks>
internal static class XamlAnchor
{
    /// <summary>
    /// Where <paramref name="automationId"/> is published in <paramref name="xaml"/>, in either spelling, or a
    /// negative value if it is not. Callers assert on the result; a bare offset keeps their own message.
    /// </summary>
    internal static int IndexOfAutomationId(string xaml, string automationId)
    {
        ArgumentNullException.ThrowIfNull(xaml);

        int literal = xaml.IndexOf($"AutomationId=\"{automationId}\"", StringComparison.Ordinal);
        return literal >= 0
            ? literal
            : xaml.IndexOf($"AutomationId=\"{{x:Static cfg:AutomationIds.{automationId}}}\"", StringComparison.Ordinal);
    }
}
