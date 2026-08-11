using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Every entry in <c>product.md</c>'s <i>Differences from the Original IHC Visual</i> register must name the test
/// that would fail if the behaviour drifted back, or state why no test is possible.
///
/// <para><b>Why this rule exists.</b> A registered difference is the one thing an alignment comparison is told to
/// treat as correct rather than as a finding. An unpinned one is therefore invisible in both directions: nothing
/// fails when the code quietly reverts to the vendor's behaviour, and the next comparison finds the vendor's
/// behaviour matching and records a pass — so the register accumulates entries describing behaviour the app no
/// longer has. The register also survives longer than any campaign record, which is exactly why its entries, not
/// the campaign's findings, are what must carry pins.</para>
///
/// <para><b>What this can and cannot check.</b> It is a completeness gate over the DOCUMENT: every entry carries a
/// marker, and every test named in a <i>Pinned by</i> marker that belongs to THIS assembly really exists. It cannot
/// tell whether a named test actually pins the behaviour described — that judgement stays with whoever registers
/// the difference. Names resolving to the other suites (<c>safe_project_tests</c>, <c>safe_unit_tests</c>) are not
/// visible from here and are accepted as written.</para>
/// </summary>
public class RegisterPinTests
{
    private const string RegisterHeading = "## Differences from the Original IHC Visual";
    private const string NextHeading = "## What This Product Is Not";

    /// <summary>A marker line: the last line of an entry, stating how the entry is pinned.</summary>
    private static readonly Regex Marker = new(@"^\s+\*(Pinned by|No test|Withdrawn):\*", RegexOptions.Compiled);

    /// <summary>The identifiers inside a <i>Pinned by</i> marker — <c>ClassName</c> or <c>ClassName.MethodName</c>,
    /// written in backticks.</summary>
    private static readonly Regex PinnedName = new(@"`([A-Za-z_][A-Za-z0-9_]*)(?:\.([A-Za-z_][A-Za-z0-9_]*))?`",
        RegexOptions.Compiled);

    private static string[] RegisterLines()
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "appdocs", "product.md");
        string[] all = File.ReadAllLines(path);
        int start = Array.FindIndex(all, l => l.StartsWith(RegisterHeading, StringComparison.Ordinal));
        int end = Array.FindIndex(all, l => l.StartsWith(NextHeading, StringComparison.Ordinal));
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "the register heading moved — this test is reading the wrong section");
        Assert.That(end, Is.GreaterThan(start), "the register's end heading moved");
        return all[start..end];
    }

    private static List<(string Head, string? MarkerLine)> Entries() => ParseEntries(RegisterLines());

    [Test]
    public void EveryRegisteredDifference_StatesHowItIsPinned()
    {
        List<(string Head, string? MarkerLine)> entries = Entries();

        Assert.That(entries, Is.Not.Empty, "the register parsed as empty — the document's shape changed");
        Assert.Multiple(() =>
        {
            foreach ((string head, string? marker) in entries)
            {
                Assert.That(marker, Is.Not.Null,
                    $"registered difference \"{Summarize(head)}\" names no test and no reason for having none");
            }
        });
    }

    [Test]
    public void EveryNamedTest_InThisAssembly_Exists()
    {
        // Public top-level types only: the assembly is also full of compiler-generated closures, whose names
        // (<>c and friends) repeat across every type that declares a lambda.
        Dictionary<string, Type> classes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsPublic && !t.IsNested)
            .ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach ((string head, string? marker) in Entries())
            {
                if (marker is null || !marker.Contains("*Pinned by:*", StringComparison.Ordinal))
                    continue;
                foreach (Match m in PinnedName.Matches(marker))
                {
                    string className = m.Groups[1].Value;
                    // Names from the other suites are invisible from here; only what this assembly owns is checked.
                    if (!classes.TryGetValue(className, out Type? type))
                        continue;
                    if (!m.Groups[2].Success)
                        continue;
                    string method = m.Groups[2].Value;
                    Assert.That(type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
                        Is.Not.Null,
                        $"\"{Summarize(head)}\" is pinned by {className}.{method}, which no longer exists");
                }
            }
        });
    }

    /// <summary>
    /// The detector's own armed check: a seeded entry with no marker must be caught. Without this, a parser that
    /// silently matched nothing — a moved heading, a changed bullet character — would report every entry as pinned
    /// and the gate would pass vacuously forever.
    /// </summary>
    [Test]
    public void TheGate_CatchesAnUnpinnedEntry()
    {
        string[] seeded =
        [
            RegisterHeading,
            string.Empty,
            "- A difference nobody pinned.",
            "- A difference someone pinned.",
            "  *Pinned by:* `RegisterPinTests`.",
            string.Empty,
            NextHeading,
        ];

        List<(string Head, string? MarkerLine)> parsed = ParseEntries(seeded);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Has.Count.EqualTo(2), "both seeded entries are seen");
            Assert.That(parsed[0].MarkerLine, Is.Null, "the unpinned one is detected as unpinned");
            Assert.That(parsed[1].MarkerLine, Is.Not.Null, "and the pinned one is not a false positive");
        });
    }

    /// <summary>Each entry as (first line, the marker line that closes it or null). An entry starts at a top-level
    /// "- " bullet and runs to the next one. The ONE parse — <see cref="Entries"/> runs it over the real document and
    /// the armed check runs it over a seeded slice, so the check exercises the logic the gate uses.</summary>
    private static List<(string Head, string? MarkerLine)> ParseEntries(IEnumerable<string> lines)
    {
        var entries = new List<(string, string?)>();
        string? head = null;
        string? marker = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (head is not null)
                    entries.Add((head, marker));
                head = line[2..].Trim();
                marker = null;
            }
            else if (head is not null && Marker.IsMatch(line))
            {
                marker = line.Trim();
            }
        }
        if (head is not null)
            entries.Add((head, marker));
        return entries;
    }

    private static string Summarize(string head) => head.Length <= 70 ? head : head[..70] + "…";
}
