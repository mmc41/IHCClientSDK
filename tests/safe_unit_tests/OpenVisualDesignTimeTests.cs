using System.Linq;
using System.Reflection;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_unit_tests;

/// <summary>
/// The shell view-model has no parameterless "design-time" constructor. It once did, and it was doing real work:
/// two <c>Path.GetTempFileName()</c> calls (each CREATING a never-deleted file on disk) plus a whole
/// <c>ProjectAppService</c>, in a constructor whose only legitimate caller would be the XAML previewer — which
/// never called it, because no view declares a <c>Design.DataContext</c>. Heavy work in a view-model constructor
/// is an Avalonia architecture anti-pattern (review AP-18/A-13) because the previewer runs it on every preview.
/// <para>This is the guard, not a formality: the constructor was dead AND side-effecting, so nothing would have
/// caught it silently returning. If design-time data is ever wanted, the review's preferred shape is a separate
/// side-effect-free <c>DesignMainWindowViewModel</c> subclass wired to <c>Design.DataContext</c> — not a second
/// production constructor that drifts from the real one.</para>
/// </summary>
public class OpenVisualDesignTimeTests
{
    [Test]
    public void MainWindowViewModel_HasNoParameterlessConstructor()
    {
        ConstructorInfo[] constructors = typeof(MainWindowViewModel)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(constructors, Is.Not.Empty, "the real, dependency-injected constructor is still there");
        Assert.That(constructors.Where(c => c.GetParameters().Length == 0), Is.Empty,
            "no parameterless constructor: it would only be reachable from the previewer, and the one that "
            + "existed created temp files on every instantiation");
    }
}
