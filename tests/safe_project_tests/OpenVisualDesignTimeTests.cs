using System.IO;
using System.Linq;
using System.Reflection;
using ihc_openvisual.DesignTime;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

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

    /// <summary>The preferred shape, now built: a separate design-time subclass the previewer can construct, so
    /// the designer shows the real shell instead of an empty frame.</summary>
    [Test]
    public void DesignMainWindowViewModel_IsConstructableAndShowsAProject()
    {
        var vm = new DesignMainWindowViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(vm, Is.InstanceOf<MainWindowViewModel>(), "so it satisfies the window's x:DataType");
            Assert.That(vm.InstallationNodes, Is.Not.Empty, "the previewer has a populated tree to lay out");
            Assert.That(vm.Title, Is.Not.Empty);
        });
    }

    /// <summary>
    /// The whole reason design-time data gets its own type: the previewer re-runs this constructor on every markup
    /// change, so it must not touch the installer's real state. The predecessor did — two <c>GetTempFileName()</c>
    /// calls, each leaving a file behind, on every instantiation.
    /// </summary>
    [Test]
    public void DesignMainWindowViewModel_WritesNothingToDisk()
    {
        // The constructor is given a temp directory of its OWN, and the assertion is that the directory is still
        // empty afterwards. The obvious version of this test — snapshot the real temp directory, construct, and
        // compare the listing — was neither fast nor sound. It enumerated a directory the test does not own and
        // set-compared two listings of it, which on a developer machine means millions of entries and seconds of
        // wall clock; and because the test host, the build and the coverage collector all write there while it
        // runs, any of them landing a file between the two listings failed this test for a reason that has
        // nothing to do with the view-model.
        //
        // Redirecting is what makes the claim exact rather than approximate: an empty directory afterwards means
        // NOTHING was written anywhere under temp, where the listing diff could only ever say that nothing
        // survived to the second listing. Both variables are set because the platforms disagree on which one
        // Path.GetTempPath reads, and they are restored so a suite that runs after this one is handed the
        // environment it expects.
        string? tmpBefore = Environment.GetEnvironmentVariable("TMP");
        string? tempBefore = Environment.GetEnvironmentVariable("TEMP");
        string? tmpdirBefore = Environment.GetEnvironmentVariable("TMPDIR");
        string sandbox = Path.Combine(Path.GetTempPath(), "ihc-designtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            Environment.SetEnvironmentVariable("TMP", sandbox);
            Environment.SetEnvironmentVariable("TEMP", sandbox);
            Environment.SetEnvironmentVariable("TMPDIR", sandbox);

            Assert.That(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(sandbox.TrimEnd(Path.DirectorySeparatorChar)),
                "precondition: the redirect took, so what follows is about the view-model and not about the "
                + "platform ignoring the variables");

            // Several times over, as the previewer would.
            for (int i = 0; i < 3; i++)
                _ = new DesignMainWindowViewModel();

            Assert.That(Directory.EnumerateFileSystemEntries(sandbox, "*", SearchOption.AllDirectories),
                Is.Empty,
                "the design-time stores read through missing paths, so construction creates neither the "
                + "design directory nor any stray temp file");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMP", tmpBefore);
            Environment.SetEnvironmentVariable("TEMP", tempBefore);
            Environment.SetEnvironmentVariable("TMPDIR", tmpdirBefore);
            Directory.Delete(sandbox, recursive: true);
        }
    }
}
