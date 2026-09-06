using System;
using System.Linq;

using ihc_openvisual.ViewModels;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The rule both drivers answer <c>problems state</c> by.
/// </summary>
/// <remarks>
/// It runs in either mode and needs no application: what is under test is the vocabulary itself, which is the
/// one thing a scenario relies on without being able to see which driver produced it.
/// </remarks>
public class ProblemsStatesTests
{
    /// <summary>
    /// The states whose counts describe a document that has already moved on, and are therefore NOT bound.
    /// </summary>
    /// <remarks>
    /// <c>stale</c> is the one that matters here. A wait for a bound result gives up silently when its timeout
    /// elapses, and what it leaves on screen is precisely a stale panel — so a rule that called that bound would
    /// hand the scenario after it a set of real, superseded numbers and no way to tell.
    /// </remarks>
    [TestCase(ProblemsStates.Validating)]
    [TestCase(ProblemsStates.Stale)]
    public void APanelBetweenResultsIsNotBound(string state) =>
        Assert.That(ProblemsStates.IsBound(state), Is.False,
            $"'{state}' means the panel's counts are not about the current document");

    [TestCase(ProblemsStates.Clean)]
    [TestCase(ProblemsStates.Findings)]
    public void AnUpToDatePanelIsBound(string state) =>
        Assert.That(ProblemsStates.IsBound(state), Is.True,
            $"'{state}' is a result for the document as it now stands");

    /// <summary>
    /// Every tier has a count key, and no two share one.
    /// </summary>
    /// <remarks>
    /// The keys are derived from the tier names, which is what stops a driver binding a count to the wrong
    /// one — but derivation is only safe while it stays INJECTIVE. Two tiers whose names differed by a trailing
    /// <c>s</c> would collapse onto one key, and the block would then publish whichever the walk wrote last
    /// while a scenario read it as the other. That is not a hypothetical property of the rule; it is the one
    /// assumption the rule makes, so it is checked rather than assumed.
    /// </remarks>
    [Test]
    public void EveryTierHasItsOwnCountKey()
    {
        var keys = Enum.GetValues<ProblemsTier>().Select(ProblemsStates.CountKey).ToList();

        Assert.That(keys, Is.Unique,
            "two tiers share a count key, so one of them is unreadable and the other reports the wrong number: "
            + string.Join(", ", keys));
    }

    /// <summary>
    /// The keys scenarios actually read are among them.
    /// </summary>
    /// <remarks>
    /// The two the corpus sweep asserts on, named as literals here on purpose. Derived at both ends, the rule
    /// and its readers would agree with each other no matter what either said — a tier renamed in the
    /// view-model would silently rename the key, and the sweep would fail on a missing count rather than say
    /// which name moved. These two literals are what makes that a sentence.
    /// </remarks>
    [TestCase(ProblemsTier.Internal, "internals")]
    [TestCase(ProblemsTier.Fatal, "fatals")]
    public void TheKeysTheSweepReadsAreTheOnesTheDriversPublish(ProblemsTier tier, string key) =>
        Assert.That(ProblemsStates.CountKey(tier), Is.EqualTo(key),
            $"the corpus sweep reads '{key}' from a problems state envelope; the drivers now publish "
            + $"{tier} under '{ProblemsStates.CountKey(tier)}'");

    /// <summary>
    /// The headless driver names a state by lower-casing the view-model's own enum, so a member added there
    /// would silently become a word no driver, scenario or rule above knows.
    /// </summary>
    [Test]
    public void EveryViewModelStateHasADeclaredWord()
    {
        string[] declared = [ProblemsStates.Validating, ProblemsStates.Stale, ProblemsStates.Clean, ProblemsStates.Findings];

        var undeclared = Enum.GetValues<ProblemsState>()
            .Select(state => (state, word: ProblemsStates.Of(state)))
            .Where(named => !declared.Contains(named.word))
            .Select(named => $"{named.state} reads as '{named.word}'")
            .ToList();

        Assert.That(undeclared, Is.Empty,
            "a ProblemsState the driver vocabulary does not declare reaches a scenario as an unknown word, and "
            + "the bound rule then decides it by omission: " + string.Join("; ", undeclared));
    }
}
