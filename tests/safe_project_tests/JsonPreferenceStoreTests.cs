using System;
using System.Collections.Generic;
using System.IO;
using ihc_openvisual.Services;
using Microsoft.Extensions.Logging;

namespace Ihc.Vis.Tests;

/// <summary>
/// The three JSON preference stores share ONE load/save body, so a preference file that cannot be read or
/// written is greppable instead of invisible. Each store previously carried its own silent catch, which is why a
/// recent-list or installer-identity write that always failed left no trace anywhere: no dialog, no log record,
/// no span. The shape that replaces them logs exactly one warning naming both the FILE and the OPERATION.
/// <para>
/// The stores' own behaviour — the MRU cap, the round-trip, the new-project stamp — belongs to
/// <see cref="RecentProjectsStoreTests"/>, <see cref="DataTableStoreTests"/> and
/// <see cref="NewProjectTemplateParityTests"/>; this fixture pins only the failure path they share.
/// </para>
/// </summary>
public class JsonPreferenceStoreTests
{
    /// <summary>One store's two failure entry points, named so a failing case reads as the store that failed.</summary>
    public sealed record StoreCase(
        string Name,
        string FileName,
        Action<string, ILoggerFactory> Load,
        Action<string, ILoggerFactory> Save)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<StoreCase> Cases()
    {
        yield return new StoreCase("RecentProjectsStore", "recent.json",
            (path, logs) => _ = new RecentProjectsStore(path, logs),
            (path, logs) => new RecentProjectsStore(path, logs).Add(Path.Combine(Path.GetTempPath(), "some.vis")));

        yield return new StoreCase("DataTableStore", "datatables.json",
            (path, logs) => _ = new DataTableStore(path, logs),
            (path, logs) => new DataTableStore(path, logs).Commit(
                new Dictionary<string, IReadOnlyList<string>> { ["customer"] = new[] { "Kunde" } }));

        yield return new StoreCase("InstallerIdentityStore", "installer.json",
            (path, logs) => _ = new InstallerIdentityStore(path, loggerFactory: logs),
            (path, logs) => new InstallerIdentityStore(path, loggerFactory: logs)
                .Update(new InstallerIdentity { Name = "Elektriker" }));
    }

    /// <summary>A corrupt file still degrades to defaults — but now says so once, naming the file it could not read.</summary>
    [TestCaseSource(nameof(Cases))]
    public void FailedLoad_LogsOneWarningNamingTheFileAndTheOperation(StoreCase store)
    {
        using ScratchDir dir = new("ihc_ov_pref_");
        string path = dir.File(store.FileName);
        File.WriteAllText(path, "{ this is not json");
        var logs = new CapturingLoggerFactory();

        store.Load(path, logs);

        string warning = TheOneWarning(logs);
        Assert.Multiple(() =>
        {
            Assert.That(warning, Does.Contain(path), "the warning names the file that could not be read");
            Assert.That(warning, Does.Contain("load"), "the warning names the operation that failed");
        });
    }

    /// <summary>A write that cannot land is best-effort as before — but no longer silent.</summary>
    [TestCaseSource(nameof(Cases))]
    public void FailedSave_LogsOneWarningNamingTheFileAndTheOperation(StoreCase store)
    {
        using ScratchDir dir = new("ihc_ov_pref_");
        // A DIRECTORY where the file belongs: the write cannot land, on every platform, without depending on
        // permissions a test runner may or may not have.
        string path = dir.File(store.FileName);
        Directory.CreateDirectory(path);
        var logs = new CapturingLoggerFactory();

        store.Save(path, logs);

        string warning = TheOneWarning(logs);
        Assert.Multiple(() =>
        {
            Assert.That(warning, Does.Contain(path), "the warning names the file that could not be written");
            Assert.That(warning, Does.Contain("save"), "the warning names the operation that failed");
        });
    }

    /// <summary>The healthy path stays quiet: a store that loads and saves cleanly logs nothing at all.</summary>
    [TestCaseSource(nameof(Cases))]
    public void SucceedingStore_LogsNothing(StoreCase store)
    {
        using ScratchDir dir = new("ihc_ov_pref_");
        string path = dir.File(store.FileName);
        var logs = new CapturingLoggerFactory();

        store.Save(path, logs);
        store.Load(path, logs);

        Assert.That(logs.Messages, Is.Empty, "a working preference file is not worth a log record");
    }

    /// <summary>The logged message alone. <see cref="CapturingLogger"/> appends the exception's own text after a
    /// pipe, and that text names the path too, so asserting on the whole line would pass without the store having
    /// named anything.</summary>
    private static string TheOneWarning(CapturingLoggerFactory logs)
    {
        Assert.That(logs.Messages, Has.Exactly(1).Items, "one warning per failed preference operation");
        Assert.That(logs.Messages[0], Does.StartWith("Warning:"), "a lost preference is a warning, not an error");
        return logs.Messages[0].Split(" | ", 2)[0];
    }
}
