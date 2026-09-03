using System;
using System.IO;
using System.Linq;
using ihc_openvisual.Services;

namespace Ihc.Vis.Tests;

/// <summary>The recent-projects MRU list (US-004): capped at four, most-recent-first, de-duplicated, persisted.</summary>
public class RecentProjectsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "ihc_ov_recent_" + Guid.NewGuid().ToString("N") + ".json");

    private static string P(string name) => Path.Combine(Path.GetTempPath(), name);

    [Test]
    public void Add_CapsAtFour_MostRecentFirst()
    {
        string file = TempFile();
        var store = new RecentProjectsStore(file);

        foreach (string name in new[] { "a.vis", "b.vis", "c.vis", "d.vis", "e.vis" })
            store.Add(P(name));

        Assert.Multiple(() =>
        {
            Assert.That(store.Items, Has.Count.EqualTo(4));
            Assert.That(store.Items[0], Is.EqualTo(Path.GetFullPath(P("e.vis"))), "newest first");
            Assert.That(store.Items, Does.Not.Contain(Path.GetFullPath(P("a.vis"))), "oldest dropped past the cap of four");
        });

        File.Delete(file);
    }

    [Test]
    public void Add_ExistingPath_MovesToFront_WithoutGrowing()
    {
        string file = TempFile();
        var store = new RecentProjectsStore(file);
        store.Add(P("a.vis"));
        store.Add(P("b.vis"));

        store.Add(P("a.vis"));

        Assert.Multiple(() =>
        {
            Assert.That(store.Items, Has.Count.EqualTo(2));
            Assert.That(store.Items[0], Is.EqualTo(Path.GetFullPath(P("a.vis"))));
        });

        File.Delete(file);
    }

    [Test]
    public void Add_TracksLastDirectory()
    {
        string file = TempFile();
        var store = new RecentProjectsStore(file);

        store.Add(P("proj.vis"));

        Assert.That(store.LastDirectory, Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(P("proj.vis")))));
        File.Delete(file);
    }

    [Test]
    public void Items_PersistAcrossReload()
    {
        string file = TempFile();
        new RecentProjectsStore(file).Add(P("kept.vis"));

        var reloaded = new RecentProjectsStore(file);

        Assert.That(reloaded.Items, Does.Contain(Path.GetFullPath(P("kept.vis"))));
        File.Delete(file);
    }
}
