using System.Collections.Generic;
using System.Linq;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// THE CROSS-LAYER SWEEP. A catalogue row that declares an attribute is claiming the finding is about a FIELD;
/// this asks the planner whether that field is actually reachable, and fails when it is not.
///
/// <para><b>A <c>Dialog</c> plan does not justify itself.</b> The planner degrades an unreachable field to
/// dialog-level on purpose — that is what keeps a row from lying — but if degradation counted as a pass, the
/// sweep would absolve exactly the rows it exists to catch. So the pass condition is <c>Field</c>, or an entry on
/// the list below stating WHY that row does not reach one.</para>
///
/// <para><b>The list lives here, in the test assembly.</b> It reads the catalogue, which the GUI may not, and
/// every entry carries its reason. It is reviewed whenever a target is declared: a new declaration either reaches
/// a field or earns a line here.</para>
/// </summary>
public class RouteCompletenessTests
{
    /// <summary>
    /// Why a declared attribute does not reach a field. Keyed by ATTRIBUTE where one reason covers a family, per
    /// the accepted vocabulary: the attribute is not rendered as a field, the field is read-only, the row declares
    /// no attribute, the attribute is dynamic per occurrence, or the corpus offers no witnessing site.
    /// </summary>
    private static readonly Dictionary<string, string> NonFieldAttributes = new()
    {
        ["product_identifier"] =
            "rendered, but READ-ONLY: the product type is chosen when the product is placed and is not "
            + "re-typeable afterwards, so the route honestly ends at the dialog.",
        ["master_type"] =
            "library PROVENANCE, not user content: it records where a block came from and no dialog offers it "
            + "for editing.",
        ["master_version"] = "library provenance, as master_type.",
        ["master_name"] = "library provenance, as master_type.",
        ["master_note"] = "library provenance, as master_type.",
    };

    /// <summary>Per-CODE exemptions, for a row whose reason is not shared by its attribute.</summary>
    private static readonly Dictionary<string, string> NonFieldCodes = new();

    /// <summary>
    /// The corpus the sweep witnesses against: the AUTHORED ERROR corpus, chosen because it is the one file
    /// built to contain the shapes the rules fire on. A clean project witnesses almost nothing.
    /// </summary>
    private const string WitnessCorpus = "Project6-Errors.vis";

    private sealed record Row(string Code, string? Tag, string Attribute, ValidationCategory Category);

    private static IReadOnlyList<Row> Declaring() =>
        [.. ProblemCatalog.Current.Entries
            .Where(e => e.Section == ProblemCatalogSection.ProjectFindings
                && e.Status == ProblemCodeStatus.Active
                && e.Target.Attribute is not null
                // Every project-finding row carries a category; the filter is here so the projection below can
                // take it without inventing one for a row that had none.
                && e.Category is not null)
            .Select(e => new Row(e.Code.Value, e.Target.Tag, e.Target.Attribute!, e.Category!.Value))
            .OrderBy(r => r.Code, System.StringComparer.Ordinal)];

    /// <summary>What the sweep concluded about one row.</summary>
    private enum Verdict { Reached, Excused, Unreached }

    /// <summary>
    /// THE SWEEP ITSELF, as a pure function over the row set — so the arming check below can run the very same
    /// body over a declaration that is deliberately unroutable.
    /// <para>Extracted for the reason the SDK emission sweep extracted its own: a checker that can only be
    /// invoked with the real catalogue cannot be shown to have teeth.</para>
    /// </summary>
    private static IReadOnlyList<(Row Row, Verdict Verdict, string Line)> Judge(
        IEnumerable<Row> rows, Project project, ProblemNavigationPlanner planner)
    {
        List<(Row, Verdict, string)> judged = [];
        foreach (Row row in rows)
        {
            if (NonFieldAttributes.TryGetValue(row.Attribute, out string? attributeReason))
            {
                judged.Add((row, Verdict.Excused, $"{row.Code} ({row.Attribute}) — {attributeReason}"));
                continue;
            }
            if (NonFieldCodes.TryGetValue(row.Code, out string? codeReason))
            {
                judged.Add((row, Verdict.Excused, $"{row.Code} — {codeReason}"));
                continue;
            }
            if (WitnessFor(project, row) is not { } witness)
            {
                judged.Add((row, Verdict.Unreached,
                    $"{row.Code} declares '{row.Attribute}' on <{row.Tag ?? "*"}>, and the corpus offers no "
                    + "element to witness it — either widen the corpus or give it a reason"));
                continue;
            }

            NavigationPlan plan = planner.Plan(
                project, witness.Id!.Value, row.Attribute, new ProblemCode(row.Code));
            judged.Add(plan.Kind is NavigationKind.Field
                ? (row, Verdict.Reached, $"{row.Code} ({row.Attribute}) → {plan.Kind}")
                : (row, Verdict.Unreached,
                    $"{row.Code} declares '{row.Attribute}' but its route reaches {plan.Kind}, not Field, "
                    + $"witnessed on <{witness.Tag}>"));
        }
        return judged;
    }

    /// <summary>
    /// An element the row could report — the same corpus the SDK's emission-consistency sweep runs over, so the
    /// two agree about which rows are witnessable at all.
    /// </summary>
    private static ProjectElement? WitnessFor(Project project, Row row)
    {
        List<ProjectElement> candidates = [.. project.Root.DescendantsAndSelf().Where(e =>
            e.Id is not null
            && (row.Tag is { } tag
                ? e.Tag == tag
                // A WILDCARD row is about the attribute wherever it appears, so an element carrying it is a
                // witness. Carrying rather than declaring: the schema view that would answer "declares" is
                // internal to the SDK and this assembly may not read it, and an element that actually holds the
                // attribute is in any case the stronger witness — it is a site the rule could really report.
                : e.GetAttribute(row.Attribute) is not null))];

        // An AUTHORED subject first. Structural containers carry attributes too — `enum_definitions` has a
        // `name` — and no rule reports one, so judging the route for such a site measured a journey nobody can
        // take and called a reachable attribute unreachable. The list below is the kinds a person authors and
        // reads back, written out here rather than read from the rules: the sweep is an independent check, and
        // borrowing a rule's own subject test would make it agree with the rule by construction.
        return candidates.FirstOrDefault(IsAuthoredSubject) ?? candidates.FirstOrDefault();
    }

    /// <summary>The element kinds a person names, documents and addresses — see <see cref="WitnessFor"/>.</summary>
    private static bool IsAuthoredSubject(ProjectElement element) =>
        ProductClassifier.IsProduct(element.Tag)
        || element.IsLocalityGroup
        || element.Kind is ElementKind.FunctionBlock or ElementKind.Resource or ElementKind.EnumResource
            or ElementKind.DatalinePin;

    [Test]
    public void EveryRowDeclaringAnAttributeReachesAFieldOrStatesWhyItCannot()
    {
        using ShellHarness harness = ShellHarness.Create();
        Project project = harness.ProjectService
            .Load(ProblemsTestData.FixturePath(WitnessCorpus)).GetAwaiter().GetResult();
        ProblemNavigationPlanner planner =
            ProblemsTestData.Planner(harness.ProjectService);

        IReadOnlyList<(Row Row, Verdict Verdict, string Line)> judged = Judge(Declaring(), project, planner);
        var unreached = judged.Where(j => j.Verdict is Verdict.Unreached).Select(j => j.Line).ToList();

        TestContext.Out.WriteLine("REACHES A FIELD:");
        foreach ((Row _, Verdict _, string line) in judged.Where(j => j.Verdict is Verdict.Reached))
        {
            TestContext.Out.WriteLine("  " + line);
        }
        TestContext.Out.WriteLine("EXCUSED, with its reason:");
        foreach ((Row _, Verdict _, string line) in judged.Where(j => j.Verdict is Verdict.Excused))
        {
            TestContext.Out.WriteLine("  " + line);
        }

        Assert.Multiple(() =>
        {
            Assert.That(unreached, Is.Empty, string.Join(System.Environment.NewLine, unreached));
            Assert.That(judged.Any(j => j.Verdict is Verdict.Reached), Is.True,
                "the sweep must have judged something — a green run over an all-excused population proves "
                + "nothing about the routes");
        });
    }

    /// <summary>
    /// EVERY FAMILY, not merely every row that happens to be witnessable. The declarations arrived family by
    /// family — naming, addressing, documentation, device settings, scenes, logic — and a sweep that quietly
    /// stopped judging one of them would stay green while that family routes rotted.
    ///
    /// <para>So a category that declares an attribute must either reach a field somewhere, or have every one of
    /// its rows excused with a stated reason. A family falling out of the judged set fails here, naming
    /// itself.</para>
    /// </summary>
    [Test]
    public void EveryCategoryThatDeclaresAnAttributeIsJudged()
    {
        using ShellHarness harness = ShellHarness.Create();
        Project project = harness.ProjectService
            .Load(ProblemsTestData.FixturePath(WitnessCorpus)).GetAwaiter().GetResult();
        ProblemNavigationPlanner planner =
            ProblemsTestData.Planner(harness.ProjectService);

        IReadOnlyList<(Row Row, Verdict Verdict, string Line)> judged = Judge(Declaring(), project, planner);
        var declaring = judged.Select(j => j.Row.Category).Distinct().ToList();
        var reached = judged.Where(j => j.Verdict is Verdict.Reached)
            .Select(j => j.Row.Category).Distinct().ToHashSet();
        var allExcused = declaring
            .Where(c => judged.Where(j => j.Row.Category == c).All(j => j.Verdict is Verdict.Excused))
            .ToHashSet();

        foreach (ValidationCategory category in declaring.OrderBy(c => c.ToString(), System.StringComparer.Ordinal))
        {
            TestContext.Out.WriteLine(
                $"{category}: {judged.Count(j => j.Row.Category == category)} declaring, "
                + $"{judged.Count(j => j.Row.Category == category && j.Verdict is Verdict.Reached)} reaching a field");
        }

        Assert.Multiple(() =>
        {
            Assert.That(declaring, Has.Count.GreaterThan(1),
                "sanity: the declarations span more than one family, or this test asserts nothing");
            foreach (ValidationCategory category in declaring)
            {
                Assert.That(reached.Contains(category) || allExcused.Contains(category), Is.True,
                    $"{category} declares an attribute but no row of it reaches a field, and not every row of "
                    + "it is excused with a reason — the family has fallen out of the sweep");
            }
        });
    }

    /// <summary>
    /// THE ARMING CHECK. The sweep above passes; this proves it would not pass a row that declares an attribute
    /// nothing can reach — the failure mode a Dialog-accepting pass condition would have hidden.
    /// </summary>
    [Test]
    public void ARowWhoseAttributeReachesNoFieldIsReported()
    {
        using ShellHarness harness = ShellHarness.Create();
        Project project = harness.ProjectService
            .Load(ProblemsTestData.FixturePath(WitnessCorpus)).GetAwaiter().GetResult();
        ProblemNavigationPlanner planner =
            ProblemsTestData.Planner(harness.ProjectService);

        // The SAME body the sweep above runs, over a row set holding one deliberately unroutable declaration:
        // `udf` is a real attribute of a real element that no dialog renders as a field. Judging it through
        // Judge() rather than reasoning about the planner is what makes this an arming check on the SWEEP
        // rather than a second test of the planner.
        Row unroutable = new("invented-row", "product_dataline", "udf", ValidationCategory.Documentation);

        IReadOnlyList<(Row Row, Verdict Verdict, string Line)> judged = Judge([unroutable], project, planner);

        Assert.Multiple(() =>
        {
            Assert.That(judged.Single().Verdict, Is.EqualTo(Verdict.Unreached),
                "a declaration that reaches no field must be reported, or the sweep above is decorative");
            Assert.That(NonFieldAttributes.ContainsKey(unroutable.Attribute), Is.False,
                "and it is not excused, so it really did travel the judging path");
            Assert.That(Judge(Declaring(), project, planner).Any(j => j.Verdict is Verdict.Unreached), Is.False,
                "while the real population stays clean — the arming lies about one row, not about the sweep");
        });
    }

    /// <summary>Every excuse says something. A blank reason is a row nobody decided about.</summary>
    [Test]
    public void EveryAllowlistEntryCarriesItsReason()
    {
        Assert.Multiple(() =>
        {
            foreach ((string key, string reason) in
                NonFieldAttributes.Concat(NonFieldCodes))
            {
                Assert.That(reason, Is.Not.Empty, key);
                Assert.That(reason.Length, Is.GreaterThan(20),
                    $"'{key}' needs a reason, not a label — say why the row cannot reach a field");
            }
        });
    }
}
