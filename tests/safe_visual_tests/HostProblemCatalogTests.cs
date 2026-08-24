using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T041: the reserved host family <c>app.openvisual.*</c> — its own code space under the SAME governance as the
/// SDK's families (D7, D8 REV 6, R13 REV 6, R4 REV 4).
///
/// <para>What is proven here: every host code is in the reserved family and well formed; ids are unique across
/// EVERY family rather than merely within this one; every declared code has an entry and every entry a declared
/// code; the family is restricted to operation OUTCOMES and a host-authored FINDING is rejected; the user-facing
/// text is Danish with the English engine sentence kept in the diagnostic slot; and the published row table is
/// rendered from the declarations rather than maintained by hand.</para>
///
/// <para>The finding restriction needs its own check and cannot borrow the SDK's: a host entry declaring a project
/// finding is schema-LEGAL — <see cref="CatalogInvariants"/> would pass it — which is exactly why the family's
/// "outcomes only" rule is asserted rather than assumed.</para>
/// </summary>
public class HostProblemCatalogTests
{
    private const string ReservedPrefix = "app.openvisual.";

    private const string BeginMarker =
        "<!-- GENERATED: host rows — rendered from the declarations; do not edit by hand -->";

    private const string EndMarker = "<!-- END GENERATED -->";

    /// <summary>The appendix as the gate sees it: the copy the build drops beside the test binaries.</summary>
    private static string AppendixCopyPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "appdocs", "error-list.md");

    [Test]
    public void EveryHostCodeIsInTheReservedFamilyAndWellFormed()
    {
        Assert.That(HostProblemCatalog.Current.Entries, Is.Not.Empty,
            "a family with no rows would make every pin below vacuous");

        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostProblemCatalog.Current.Entries)
            {
                string code = entry.Code.Value;
                Assert.That(code, Does.StartWith(ReservedPrefix), "this app mints into app.openvisual.* only");
                Assert.That(entry.Code.Family, Is.EqualTo(ProblemFamily.App), code);
                Assert.That(entry.Code.IsHostOwned, Is.True, code);
                Assert.That(ProblemCode.TryParse(code, out _), Is.True, $"{code} is a well-formed code");
            }
        });
    }

    /// <summary>
    /// Uniqueness spans the whole vocabulary. Checked by handing the SDK's and this app's declarations to the SDK's
    /// own invariant checker as ONE catalogue — the same gate, not a host-side re-implementation of it.
    /// </summary>
    [Test]
    public void IdsAreUniqueAcrossEveryFamilyNotMerelyWithinThisOne()
    {
        ProblemCatalog combined = ProblemCatalog.From(EquatableArray.CreateRange(
            ProblemCatalog.Current.Entries.Concat(HostProblemCatalog.Current.Entries)));

        Assert.Multiple(() =>
        {
            Assert.That(combined.Total, Is.EqualTo(ProblemCatalog.Current.Total + HostProblemCatalog.Current.Total),
                "sanity: the two sets were really combined");
            Assert.That(CatalogInvariants.Check(combined, []), Is.Empty,
                "an id collision or a misplaced category across the SDK and host declarations");
        });
    }

    /// <summary>
    /// Every code the app declares has an entry, and every entry a declared code — the SDK's own
    /// entry-without-rule / rule-without-entry pair, applied to the host family with its declared codes as the
    /// implemented set.
    /// </summary>
    [Test]
    public void EveryDeclaredHostCodeHasAnEntryAndEveryEntryADeclaredCode()
    {
        IReadOnlyCollection<ProblemCode> declared = DeclaredHostCodes();

        Assert.Multiple(() =>
        {
            Assert.That(declared, Is.Not.Empty, "sanity: the reflection found the declared codes");
            Assert.That(CatalogInvariants.Check(HostProblemCatalog.Current, declared), Is.Empty,
                "a code minted with nothing behind it, or an entry no code names");
        });
    }

    [Test]
    public void EveryHostEntryIsAnOperationOutcomeAndNeverAFinding()
    {
        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostProblemCatalog.Current.Entries)
            {
                Assert.That(FindingOffences(entry), Is.Empty, entry.Code.Value);
            }
        });
    }

    /// <summary>
    /// The restriction, armed: a host entry that authors a project finding is REJECTED. Note what the seeded entry
    /// is — a perfectly well-formed catalogue row, which the SDK's invariants accept without complaint. The rule
    /// this family lives under is about OWNERSHIP, so it needs its own assertion or the appendix's promise ("a host
    /// never authors a finding about a project") is prose nothing checks.
    /// </summary>
    [Test]
    public void AHostAuthoredFindingIsRejected()
    {
        ProblemCatalogEntry seeded = new(
            new ProblemCode("app.openvisual.link-input-unconnected"),
            ProblemCatalogSection.ProjectFindings,
            ValidationCategory.Wiring,
            CatalogDisposition.Warning,
            RuleKind.UserContentRule,
            RuleFaces.WholeProject,
            default,
            FindingShape.OnePerOccurrence,
            default,
            "Ikke forbundet");

        ProblemCatalog seededCatalog = ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>([seeded]));

        Assert.Multiple(() =>
        {
            Assert.That(FindingOffences(seeded), Is.Not.Empty, "the host restriction must report it");
            Assert.That(FindingOffences(seeded), Has.Some.Contains("section"), "and say what is wrong with it");
            Assert.That(CatalogInvariants.Check(seededCatalog, []), Is.Empty,
                "and this is why the check is needed: the SDK's invariants find nothing wrong — the entry is "
                + "schema-legal, it is merely not the host's to author");
        });
    }

    /// <summary>
    /// The host language pin. Danish user-facing text, following the SDK's own convention of a fixed label
    /// authored once, with the English engine sentence in the diagnostic slot where the log reads it and no user
    /// ever does (ARCHITECTURE.md invariant 10).
    /// </summary>
    [Test]
    public void TheUserFacingTextIsDanishAndTheEnglishStaysInTheDiagnostic()
    {
        string[] englishOpeners = ["The ", "A ", "An ", "No ", "Not ", "This ", "That ", "Could not ", "Failed "];
        string[] englishFragments =
            [" the ", " is not ", " does not ", " cannot ", " could not ", " has no ", " must be ", " was not "];

        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostProblemCatalog.Current.Entries)
            {
                string code = entry.Code.Value;
                Assert.That(entry.MessageTemplate, Is.Not.Empty,
                    $"{code} has no user-facing text — an unauthored label is a code that cannot be shown");
                foreach (string opener in englishOpeners)
                {
                    Assert.That(entry.MessageTemplate, Does.Not.StartWith(opener), code);
                }

                foreach (string fragment in englishFragments)
                {
                    Assert.That(entry.MessageTemplate, Does.Not.Contain(fragment), code);
                }

                Assert.That((entry.Diagnostic ?? string.Empty).All(char.IsAscii), Is.True,
                    $"{code}: the diagnostic slot is the ENGLISH engine sentence, not a second Danish one");
            }
        });
    }

    /// <summary>The one row's exact Danish sentence, pinned so it cannot drift silently.</summary>
    [Test]
    public void TheCatchAllsDanishSentenceIsPinned()
    {
        Assert.That(HostProblemCatalog.Unexpected.MessageTemplate,
            Is.EqualTo("Handlingen kunne ikke gennemføres på grund af en intern fejl. "
                       + "Detaljerne er skrevet til loggen."));
    }

    /// <summary>
    /// ONE OWNER for the catch-all sentence. It used to be written twice — on the catalogue entry that governs
    /// <c>app.openvisual.unexpected</c> and again as a view-model constant — with a test standing between the
    /// copies to keep them equal. A test that compares two copies is what you write when you cannot remove one;
    /// the shell now READS the entry, so there is nothing to drift and nothing to compare.
    /// <para>
    /// Asserted as a source scan rather than by comparing the two members, because comparing them is exactly the
    /// assertion this replaces: it would pass just as well with the duplicate restored.
    /// </para>
    /// </summary>
    [Test]
    public void TheCatchAllSentenceIsWrittenOnceInTheApplication()
    {
        string root = Path.Combine(RepositoryRoot(), "applications", "ihc_openvisual");
        string[] carrying =
        [
            .. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                    && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                .Where(f => File.ReadAllText(f, Encoding.UTF8)
                    .Contains("Handlingen kunne ikke gennemføres på grund af en intern fejl",
                        StringComparison.Ordinal))
                .Select(f => Path.GetFileName(f))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(carrying, Is.EqualTo(new[] { "HostProblemCatalog.cs" }).AsCollection,
                "the sentence belongs to the entry that governs its code, and to nothing else");
            Assert.That(MainWindowViewModel.UnexpectedErrorMessage,
                Is.EqualTo(HostProblemCatalog.Unexpected.MessageTemplate),
                "and the shell shows exactly what that entry says");
        });
    }

    /// <summary>
    /// The typed factory produces a problem carrying the bound Danish message and the English detail SEPARATELY —
    /// the split that lets the shell render one and log the other.
    /// </summary>
    [Test]
    public void TheFactoryProducesTheDanishMessageAndKeepsTheEnglishDetailApart()
    {
        InvalidOperationException cause = new("Sequence contains no elements");
        Problem problem = HostProblems.Unexpected(cause);

        Assert.Multiple(() =>
        {
            Assert.That(problem.Code, Is.EqualTo(HostProblemCodes.Unexpected));
            Assert.That(problem.Message, Is.EqualTo(HostProblemCatalog.Unexpected.MessageTemplate),
                "bound at the producer, so the presentation path renders it as it stands");
            Assert.That(problem.Diagnostic, Is.EqualTo(cause.Message), "the engine text moves to the diagnostic slot");
            Assert.That(problem.Cause, Is.SameAs(cause));
            Assert.That(ProblemPresenter.Text(problem), Does.EndWith(" [app.openvisual.unexpected]"),
                "and it renders through the shell's ONE path, same as any SDK problem");
        });
    }

    /// <summary>
    /// A markdown table is LINE-ORIENTED: a newline inside a cell ends the row, and everything after it stops
    /// being a table at all. One host label spans three lines by design — the report-not-openable sentence puts
    /// the file path on its own line — so the rendered appendix silently lost every row after it.
    /// <para>
    /// Asserted as a COUNT rather than by looking for that one label: the defect is a property of the renderer,
    /// and the next multi-line label would reintroduce it.
    /// </para>
    /// </summary>
    [Test]
    public void TheRenderedTableHasExactlyOneLinePerHostCode()
    {
        string[] lines = Render(HostProblemCatalog.Current).Split('\n');
        string[] rows = [.. lines.Where(line => line.StartsWith("| `", StringComparison.Ordinal))];
        string[] continuations =
        [
            .. lines.Where(line => line.Contains(" |", StringComparison.Ordinal)
                && !line.StartsWith('|')),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Length.EqualTo(HostProblemCatalog.Current.Total),
                "one table row per host code — a label carrying a newline renders as several");
            Assert.That(continuations, Is.Empty,
                "a cell's text escaped its row and is no longer in the table: " + string.Join(" / ", continuations));
        });
    }

    /// <summary>
    /// The published appendix's row table is RENDERED from the declarations and compared here, so the document
    /// cannot fall behind the code (D24). Same shape as the SDK catalogue's generated index, pointed at the host's
    /// own artifact — governance follows the owner.
    /// </summary>
    [Test]
    public void TheRenderedRowTableMatchesTheCheckedInAppendix()
    {
        string expected = Render(HostProblemCatalog.Current);
        string actual = ReadGeneratedRegion(File.ReadAllText(AppendixCopyPath));

        Assert.That(actual, Is.EqualTo(expected),
            "the checked-in host appendix is stale — run the [Explicit] Regenerate_TheHostRowTable test and "
            + "review the diff");
    }

    /// <summary>
    /// Regenerates the checked-in appendix region. <see cref="ExplicitAttribute"/> for the reason the SDK's
    /// counterpart is: a test that rewrites the artifact it compares against passes unconditionally. Being
    /// explicit is also what lets it write to the SOURCE tree — the gate tests read the copied appendix beside the
    /// binaries, never the checkout.
    /// </summary>
    [Test]
    [Explicit("Rewrites the checked-in host appendix; run deliberately and review the diff.")]
    public void Regenerate_TheHostRowTable()
    {
        string path = Path.Combine(RepositoryRoot(), "applications", "ihc_openvisual", "docs", "error-list.md");
        string document = File.ReadAllText(path);
        string rendered = Render(HostProblemCatalog.Current);

        int begin = document.IndexOf(BeginMarker, StringComparison.Ordinal);
        string updated = begin < 0
            ? document.TrimEnd('\n') + "\n\n" + BeginMarker + "\n" + rendered + "\n" + EndMarker + "\n"
            : document[..begin] + BeginMarker + "\n" + rendered + "\n"
              + document[document.IndexOf(EndMarker, begin, StringComparison.Ordinal)..];

        File.WriteAllText(path, updated, new UTF8Encoding(false));
        TestContext.Out.WriteLine($"rewrote the host row table in {path}");
    }

    /// <summary>
    /// Why an entry is not something this family may author. A list rather than a bool so the failure names the
    /// offending axis, and shared by the pin and its armed counterpart so both judge by one rule.
    /// </summary>
    private static IReadOnlyList<string> FindingOffences(ProblemCatalogEntry entry)
    {
        List<string> offences = [];
        if (entry.Section != ProblemCatalogSection.OperationOutcomes)
        {
            offences.Add($"section is {entry.Section}, and a host may only author operation outcomes");
        }

        if (entry.Kind != RuleKind.OperationOutcome)
        {
            offences.Add($"kind is {entry.Kind}");
        }

        if (entry.Disposition != CatalogDisposition.Refusal)
        {
            offences.Add($"disposition is {entry.Disposition}, which reports a finding");
        }

        if (entry.Severity is not null)
        {
            offences.Add($"it carries the finding severity {entry.Severity}");
        }

        if (entry.Category is not null)
        {
            offences.Add($"it classifies project content as {entry.Category}");
        }

        if (entry.Faces != RuleFaces.None)
        {
            offences.Add($"it declares the faces {entry.Faces}, so an executor would run it");
        }

        return offences;
    }

    /// <summary>Every code the app declares, read off <see cref="HostProblemCodes"/> rather than listed twice.</summary>
    private static IReadOnlyCollection<ProblemCode> DeclaredHostCodes() =>
        [.. typeof(HostProblemCodes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(ProblemCode))
            .Select(p => (ProblemCode)p.GetValue(null)!)];

    private static string ReadGeneratedRegion(string document)
    {
        int begin = document.IndexOf(BeginMarker, StringComparison.Ordinal);
        Assert.That(begin, Is.GreaterThanOrEqualTo(0),
            $"the host appendix has no generated region; expected the marker {BeginMarker}");

        int body = begin + BeginMarker.Length;
        int end = document.IndexOf(EndMarker, body, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(body), "the generated region has no end marker");

        return document[body..end].Trim('\n', '\r');
    }

    private static string Render(ProblemCatalog catalog)
    {
        StringBuilder page = new();
        page.Append("\nEvery code this app mints, as the code itself declares it. This table is RENDERED from\n");
        page.Append("`applications/ihc_openvisual/Services/HostProblemCatalog.cs` and compared by a test, so it\n");
        page.Append("cannot fall behind the declarations. Edit the declarations, not this table.\n\n");
        page.Append("There is no category column: this family authors operation outcomes, and the eight categories\n");
        page.Append("classify project content the SDK owns.\n\n");
        page.Append("| Id | Costs | Kind | Status | Danish label |\n");
        page.Append("| --- | --- | --- | --- | --- |\n");
        foreach (ProblemCatalogEntry row in catalog.Entries.OrderBy(e => e.Code.Value, StringComparer.Ordinal))
        {
            page.Append($"| `{row.Code.Value}` | {row.Disposition} | {row.Kind} | {row.Status} ");
            page.Append($"| {Cell(row.MessageTemplate)} |\n");
        }

        page.Append($"\n**Total: {catalog.Total} host code{(catalog.Total == 1 ? string.Empty : "s")}.**\n");
        return page.ToString().Trim('\n');
    }

    /// <summary>
    /// One template as ONE table cell. A markdown table is line-oriented, so a newline inside a cell ends the row
    /// and everything after it stops being a table — and one host label spans three lines by design, putting the
    /// report's file path on its own line. The break is PRESERVED as a table-cell line break rather than dropped:
    /// the sentence was written with it for a reason.
    /// </summary>
    private static string Cell(string template) =>
        template.Length == 0 ? "*(to author)*" : template.Replace("\n", "<br>", StringComparison.Ordinal);

    /// <summary>The checkout root, for the explicit regeneration only.</summary>
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repo root (IHCClientSDK.sln) not found above the test directory");
    }
}
