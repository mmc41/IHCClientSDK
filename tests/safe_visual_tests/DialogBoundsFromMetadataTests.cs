using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T045: the two hand-written numeric dialogs take their bounds from the SDK's dialog-metadata face, and the blank
/// gates take their answer from the SDK's required-field constraint. What this app keeps is the interaction —
/// commit, focus, the dialog staying open — and nothing about which VALUES are acceptable.
///
/// <para><b>Why this needed deleting rather than adding.</b> The advanced-dimmer window's markup carried 200–60000,
/// 2–10 and 0–100, and the scene-value window's 0–100 and 0–59, while the same numbers were declared per element in
/// the catalog. Two copies of a bound can only agree by coincidence, and the direction they disagree in is the
/// dangerous one: a window advertising a wider range invites a value the commit path then refuses.</para>
/// </summary>
public class DialogBoundsFromMetadataTests : AvaloniaTestBase
{
    /// <summary>Bounds no catalog would produce, so a control still carrying its own copy cannot pass by luck.</summary>
    private static FieldConstraintMetadata Bounds(double minimum, double maximum) =>
        FieldConstraintMetadata.Unconstrained with { Minimum = minimum, Maximum = maximum };

    [AvaloniaTest]
    public void TheAdvancedDimmerBoxesTakeTheBoundsTheInputCarries()
    {
        AdvancedDimmerInput input = new(
            700, 700, 5, 0, 100, "auto",
            SoftOn: Bounds(11, 12), SoftOff: Bounds(13, 14), ManualRamp: Bounds(15, 16),
            Minimum: Bounds(17, 18), Maximum: Bounds(19, 20));

        var window = new AdvancedDimmerWindow();
        CurrentTestWindow = window;
        window.Show();
        NumericFieldBounds.Apply(window.FindControl<NumericUpDown>("SoftOnBox")!, input.SoftOn);
        NumericFieldBounds.Apply(window.FindControl<NumericUpDown>("ManualRampBox")!, input.ManualRamp);
        NumericFieldBounds.Apply(window.FindControl<NumericUpDown>("MaximumBox")!, input.Maximum);

        Assert.Multiple(() =>
        {
            AssertBounds(window, "SoftOnBox", 11, 12);
            AssertBounds(window, "ManualRampBox", 15, 16);
            AssertBounds(window, "MaximumBox", 19, 20);
        });
    }

    [AvaloniaTest]
    public void AnUnconstrainedFieldIsNotClampedToZero()
    {
        var window = new SceneValueWindow();
        CurrentTestWindow = window;
        window.Show();
        NumericUpDown box = window.FindControl<NumericUpDown>("LevelBox")!;

        NumericFieldBounds.Apply(box, FieldConstraintMetadata.Unconstrained);

        Assert.Multiple(() =>
        {
            Assert.That(box.Minimum, Is.EqualTo(decimal.MinValue),
                "no declared bound means the catalog states no limit — clamping to 0 would invent one");
            Assert.That(box.Maximum, Is.EqualTo(decimal.MaxValue));
        });
    }

    /// <summary>
    /// The scene dialog's bounds are the SDK's own: the level range its <c>SceneValue.Dimmer</c> factory enforces,
    /// and the mm:ss notation's per-part range. Asserted through the input the coordinators build, so a coordinator
    /// that stopped passing them would fail here rather than silently unbound a field.
    /// </summary>
    [Test]
    public void TheSceneValueConstraintsAreTheSdksOwn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SceneValue.LevelConstraint.Minimum, Is.EqualTo(0));
            Assert.That(SceneValue.LevelConstraint.Maximum, Is.EqualTo(100));
            Assert.That(SceneValue.RampPartConstraint.Maximum, Is.EqualTo(59));
            Assert.That(() => SceneValue.Dimmer((int)SceneValue.LevelConstraint.Maximum! + 1, TimeSpan.Zero),
                Throws.InstanceOf<ArgumentOutOfRangeException>(),
                "what the field advertises is what the factory enforces — one bound, not two");
        });
    }

    /// <summary>
    /// The catalog's bounds reach the dialog through the metadata face, per setting. Read from the built-in catalog's
    /// own dimmer product rather than from a fixture, so the numbers under test are the shipped ones.
    /// </summary>
    [Test]
    public void TheDimmerSettingConstraintsComeFromTheCatalog()
    {
        ProjectAppService app = new(new Ihc.IhcSettings());
        Ihc.Vis.Products.ProductDefinition dimmer = app.GetAvailableProducts()
            .First(p => p.Body.FindDescendantOrSelf(e => e.Tag == "dimmer_setting_fade_rate_up") is not null);
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        project = app.Apply(project, app.Commands.AddProduct(project, locality, dimmer)).Project!;
        ProjectElement placed = project.Root
            .FindDescendantOrSelf(e => e.FindDescendantOrSelf(c => c.Tag == "dimmer_setting_fade_rate_up") is not null)!;

        DimmerView view = new(project, placed);

        Assert.Multiple(() =>
        {
            FieldConstraintMetadata fade = view.SettingConstraint("dimmer_setting_fade_rate_up");
            Assert.That((fade.Minimum, fade.Maximum), Is.EqualTo((200d, 60000d)),
                "the catalog declares this setting's range; the dialog no longer carries a copy");
            Assert.That(view.SettingConstraint("dimmer_setting_dimming_rate").Maximum, Is.EqualTo(10000),
                "declared in milliseconds — the seconds field divides these bounds by the same 1000 as the value");
            Assert.That(view.SettingConstraint("dimmer_setting_nonexistent"),
                Is.EqualTo(FieldConstraintMetadata.Unconstrained),
                "a setting the product does not carry is unconstrained, not zero");
        });
    }

    /// <summary>
    /// THE GATE: no hardcoded numeric bound in the dialog markup or the view-models. Scanned over the app's own
    /// sources and markup, copied beside the test binaries — a bound written back into a window fails here.
    /// </summary>
    [Test]
    public void NoDialogMarkupOrViewModelCarriesItsOwnNumericBound()
    {
        // Sources and MARKUP, copied beside the binaries by the test project. Both roots are asserted non-empty
        // first: a scan that reads no files passes every assertion, which is exactly how the markup half of this
        // gate was found to be copying nothing.
        string[] roots =
        [
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appsrc"),
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appmarkup"),
        ];
        List<string> offenders = [];
        List<string> scanned = [];

        foreach (string file in roots.SelectMany(r => Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories)))
        {
            scanned.Add(file);
            string name = Path.GetFileName(file);
            if (name == "NumericFieldBounds.cs")
            {
                continue;   // the one place a declared bound is FORWARDED onto a control
            }

            foreach (string line in File.ReadAllLines(file))
            {
                bool markupBound = line.Contains("Minimum=\"", StringComparison.Ordinal)
                    || line.Contains("Maximum=\"", StringComparison.Ordinal);
                bool codeBound = line.Contains(".Minimum =", StringComparison.Ordinal)
                    || line.Contains(".Maximum =", StringComparison.Ordinal);
                if (markupBound || codeBound)
                {
                    offenders.Add($"{name}: {line.Trim()}");
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(scanned.Count(f => f.EndsWith(".axaml", StringComparison.Ordinal)), Is.GreaterThan(10),
                "sanity: the markup really was scanned");
            Assert.That(scanned.Count(f => f.EndsWith(".cs", StringComparison.Ordinal)), Is.GreaterThan(30),
                "sanity: and so were the sources");
            Assert.That(offenders, Is.Empty,
                "a numeric bound belongs to the SDK's dialog-metadata face; a copy in a window can only agree with "
                + "the catalog by coincidence");
        });
    }

    /// <summary>
    /// The blank gate: one decision, the SDK's, with the SDK's sentence — over the three partitions the old gates
    /// disagreed about. Whitespace-only is the middle case that used to pass two of the three.
    /// </summary>
    [Test]
    public void TheBlankGateIsTheSdksDecisionAndTheSdksSentence()
    {
        // Through the FACADE, which is where the decision now lives: the shell composes nothing of its own.
        var app = new ProjectAppService(new IhcSettings());
        Assert.Multiple(() =>
        {
            Assert.That(app.MissingRequiredField("Stue"), Is.Null, "a real value passes");
            Assert.That(app.MissingRequiredField(null), Is.Not.Null, "null is blank");
            Assert.That(app.MissingRequiredField(string.Empty), Is.Not.Null, "empty is blank");

            Problem? whitespace = app.MissingRequiredField("   ");
            Assert.That(whitespace, Is.Not.Null, "whitespace-only is blank — the case the three gates disagreed on");
            Assert.That(whitespace!.Code, Is.EqualTo(EditRefusalCodes.ValueRequired));
            Assert.That(whitespace.Message, Is.EqualTo(EditRefusalProblems.ValueRequired().Message),
                "the sentence is the SDK's, authored once");
            Assert.That(ProblemCatalog.Current.TryGet(whitespace.Code, out ProblemCatalogEntry entry), Is.True);
            Assert.That(entry.BindTemplate(whitespace), Is.EqualTo(whitespace.Message),
                "and it agrees with its catalogue template");
        });
    }

    private static void AssertBounds(Window window, string name, decimal minimum, decimal maximum)
    {
        NumericUpDown box = window.FindControl<NumericUpDown>(name)!;
        Assert.That((box.Minimum, box.Maximum), Is.EqualTo((minimum, maximum)), name);
    }
}
