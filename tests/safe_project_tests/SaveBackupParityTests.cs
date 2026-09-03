using System.IO;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests;

/// <summary>
/// Overwriting a project file must leave the previous version behind as a <c>.BAK</c> side-file, the way
/// IHC Visual does (uxparity S-04) — the installer's safety net when a save turns out to be a mistake.
/// It applies to any write over an existing file (plain Save as well as Save As), and to no other case:
/// saving to a name that does not exist yet has nothing to back up.
/// </summary>
public class SaveBackupParityTests
{
    private static string Bak(string visPath) => Path.ChangeExtension(visPath, ".BAK");

    [Test]
    public async Task SaveAs_OverAnExistingFile_KeepsThePreviousVersionAsBak()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        string path = harness.TempPath("target.vis");

        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();
        byte[] firstVersion = await File.ReadAllBytesAsync(path);
        Assert.That(File.Exists(Bak(path)), Is.False, "a first save has no previous version to back up");

        await harness.Session.AddLocalityAsync();
        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Bak(path)), Is.True, "the overwrite leaves a .BAK behind");
            Assert.That(File.ReadAllBytes(Bak(path)), Is.EqualTo(firstVersion), "and it holds the version just replaced");
        });
    }

    [Test]
    public async Task Save_OverTheAlreadyNamedFile_AlsoKeepsABak()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();
        string path = harness.TempPath("named.vis");
        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();
        byte[] firstVersion = await File.ReadAllBytesAsync(path);

        await harness.Session.AddLocalityAsync();
        await harness.Session.SaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Bak(path)), Is.True, "a plain Save over the existing file backs it up too");
            Assert.That(File.ReadAllBytes(Bak(path)), Is.EqualTo(firstVersion));
        });
    }
}
