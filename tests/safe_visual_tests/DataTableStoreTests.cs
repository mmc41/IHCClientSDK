using System.Collections.Generic;
using System.IO;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// The installer's reusable documentation texts, which survive as the SUGGESTION MEMORY behind the project-info
/// dialog's sixteen contact combos (US-039) now that editing the data tables is a declared product exclusion.
/// Nothing in the app lists or edits these tables any more — they fill up only from values typed into a
/// documentation field, so the one behaviour left worth pinning is that a committed text outlives the process.
/// <para>
/// The offering and absorbing halves are covered by <see cref="ProjectInfoDialogParityTests"/>.
/// </para>
/// </summary>
public class DataTableStoreTests
{
    /// <summary>The store round-trips through its own file, so the texts survive a restart.</summary>
    [Test]
    public void Store_RoundTripsThroughItsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "datatables.json");
        var store = new DataTableStore(path);

        store.Commit(new Dictionary<string, IReadOnlyList<string>>
        {
            ["customer"] = new[] { "Kunde Bo Bæk", "Morten" },
            ["street"] = new[] { "Virum gyde 2" },
        });
        var reloaded = new DataTableStore(path);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.TextsFor("customer"), Is.EqualTo(new[] { "Kunde Bo Bæk", "Morten" }));
            Assert.That(reloaded.TextsFor("street"), Is.EqualTo(new[] { "Virum gyde 2" }));
            Assert.That(reloaded.TextsFor("country"), Is.Empty, "a table never added to reads empty, not missing");
        });
    }
}
