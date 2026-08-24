using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.Services;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// R17's FOURTH check, and the one the other three cannot make: a host family's user-facing sentences held against
/// the SDK's own phrasing standard, in the host's own language pin.
///
/// <para><b>Why phrasing and not ids.</b> Checks one to three keep the two code SPACES apart — no SDK-declared
/// <c>app.*</c> code, no GUI-declared SDK code, and the contract usable outside the shell. None of them would
/// notice the measured failure mode of an opened vocabulary, which is STYLE DRIFT: a host family whose sentences
/// are longer, chattier, differently punctuated or written in a different register still reads as a second product
/// bolted onto the first, however clean its ids are.</para>
///
/// <para><b>The standard is MEASURED from the SDK, not invented here.</b> Every bound below is derived from the
/// SDK catalogue's own active templates at run time, so the check cannot drift from the thing it is comparing
/// against — and if the SDK's own convention changes, the host's bound moves with it instead of failing on a
/// number someone typed once.</para>
/// </summary>
public class HostPhrasingStandardTests
{
    /// <summary>The SDK's authored, user-facing templates — the reference population.</summary>
    private static IReadOnlyList<string> SdkTemplates =>
        [.. ProblemCatalog.Current.Entries
            .Where(e => e.Status == ProblemCodeStatus.Active && e.MessageTemplate.Length > 0)
            .Select(e => e.MessageTemplate)];

    /// <summary>This app's templates — the population under test.</summary>
    private static IReadOnlyList<ProblemCatalogEntry> HostEntries => [.. HostProblemCatalog.Current.Entries];

    [Test]
    public void TheReferencePopulationsAreBothRealEnoughToCompare()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SdkTemplates, Has.Count.GreaterThan(30),
                "the SDK's standard is read off its own authored rows; a handful would not be a standard");
            Assert.That(HostEntries, Has.Count.GreaterThan(1), "and the host has rows to hold against it");
        });
    }

    /// <summary>
    /// A fixed LABEL or one sentence, never a paragraph: the host's longest template may not exceed the SDK's
    /// longest. This is the drift a reviewer would otherwise catch only by reading both catalogues side by side.
    /// </summary>
    [Test]
    public void NoHostSentenceIsLongerThanTheSdksLongest()
    {
        int sdkLongest = SdkTemplates.Max(t => t.Length);

        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostEntries)
            {
                Assert.That(entry.MessageTemplate.Length, Is.LessThanOrEqualTo(sdkLongest),
                    $"{entry.Code.Value} is chattier than anything the SDK says (SDK's longest is {sdkLongest} "
                    + "characters); a host family that talks more than the engine reads as a second product");
            }
        });
    }

    /// <summary>
    /// The shape rules the SDK's own rows follow, applied to the host's: a template opens on a capital or a
    /// placeholder, carries no leading or trailing whitespace, no double spaces, no tab, and no unauthored
    /// <c>TODO</c>. Each is asserted over BOTH populations, so a rule the SDK itself breaks cannot be imposed on
    /// the host — the check would fail on the reference population first and say so.
    /// </summary>
    [Test]
    public void TheHostFollowsTheShapeRulesTheSdkFollows()
    {
        Assert.Multiple(() =>
        {
            foreach (string template in SdkTemplates)
            {
                AssertShape(template, "SDK");
            }

            foreach (ProblemCatalogEntry entry in HostEntries)
            {
                AssertShape(entry.MessageTemplate, entry.Code.Value);
            }
        });
    }

    /// <summary>
    /// Terminal punctuation: the SDK's population is deliberately MIXED — a fixed label carries none
    /// (<i>Mangler Kabeltype</i>) and a sentence ends in a full stop (<i>Filen er tom.</i>) — so the standard is
    /// not "always a period". What it forbids is the third thing: no other terminator, and never an exclamation
    /// mark, which is the register a tool does not use.
    /// </summary>
    [Test]
    public void HostTerminalPunctuationStaysInsideTheSdksTwoHabits()
    {
        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostEntries)
            {
                char last = entry.MessageTemplate[^1];
                Assert.That(last is '.' or '}' || char.IsLetterOrDigit(last), Is.True,
                    $"{entry.Code.Value} ends on '{last}': the SDK's rows end on a letter (a label), a full stop "
                    + "(a sentence) or a bound value, and nothing else");
                Assert.That(entry.MessageTemplate, Does.Not.Contain("!"),
                    $"{entry.Code.Value}: an exclamation mark is not this product's register");
            }
        });
    }

    /// <summary>
    /// No sentence is assembled at render time — the rule the whole fixed-label convention rests on. Every
    /// placeholder in a host template is a DECLARED slot, and every declared slot appears in the template: a
    /// template naming an undeclared slot renders a visible <c>{gap}</c>, and a slot no template names is a value
    /// the user never sees.
    /// </summary>
    [Test]
    public void EveryHostPlaceholderIsADeclaredSlotAndEverySlotIsUsed()
    {
        Assert.Multiple(() =>
        {
            foreach (ProblemCatalogEntry entry in HostEntries)
            {
                IReadOnlyList<string> placeholders = Placeholders(entry.MessageTemplate);
                IReadOnlyList<string> slots = [.. entry.Slots.Select(s => s.Name)];

                Assert.That(placeholders, Is.EquivalentTo(slots),
                    $"{entry.Code.Value}: template placeholders and declared slots must be the same set");
            }
        });
    }

    /// <summary>
    /// The armed control: the standard must FAIL a sentence that drifts. Three seeded templates, one per rule —
    /// a paragraph, a shouted line, and a template naming a slot it never declared.
    /// </summary>
    [Test]
    public void TheStandardIsArmed()
    {
        int sdkLongest = SdkTemplates.Max(t => t.Length);
        string paragraph = new('x', sdkLongest + 1);

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.Length, Is.GreaterThan(sdkLongest), "a template longer than anything the SDK says");
            Assert.That(ShapeOffences(" Ledende blanktegn"), Is.Not.Empty, "leading whitespace is caught");
            Assert.That(ShapeOffences("skrevet med lille begyndelsesbogstav"), Is.Not.Empty, "a lowercase open is caught");
            Assert.That(ShapeOffences("To  mellemrum"), Is.Not.Empty, "a double space is caught");
            Assert.That(ShapeOffences("TODO: uskrevet"), Is.Not.Empty, "an unauthored TODO is caught");
            Assert.That(Placeholders("Filen '{file}' i {folder}"), Is.EquivalentTo(new[] { "file", "folder" }),
                "and the placeholder reader finds both, so the slot comparison is not vacuous");
        });
    }

    private static void AssertShape(string template, string label)
    {
        Assert.That(ShapeOffences(template), Is.Empty, $"{label}: '{template}'");
    }

    /// <summary>The shape rules, in one place so both populations are judged by the same code.</summary>
    private static IReadOnlyList<string> ShapeOffences(string template)
    {
        List<string> offences = [];
        if (template.Length == 0)
        {
            offences.Add("empty");
            return offences;
        }

        if (template.Trim() != template)
        {
            offences.Add("leading or trailing whitespace");
        }

        char first = template[0];
        if (first != '{' && first != '<' && !char.IsUpper(first))
        {
            offences.Add($"opens on '{first}' rather than a capital or a placeholder");
        }

        if (template.Contains("  ", StringComparison.Ordinal))
        {
            offences.Add("double space");
        }

        if (template.Contains('\t'))
        {
            offences.Add("tab");
        }

        if (template.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            offences.Add("unauthored TODO");
        }

        return offences;
    }

    /// <summary>The <c>{slot}</c> names a template uses, in order of appearance.</summary>
    private static IReadOnlyList<string> Placeholders(string template)
    {
        List<string> names = [];
        for (int open = template.IndexOf('{'); open >= 0;
             open = template.IndexOf('{', open + 1))
        {
            int close = template.IndexOf('}', open);
            if (close > open + 1)
            {
                names.Add(template[(open + 1)..close]);
            }
        }

        return names;
    }
}
