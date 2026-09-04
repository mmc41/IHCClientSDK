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
