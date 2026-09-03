using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests;

/// <summary>
/// T040: the shell's ONE presentation path for the coded problem contract (R16 REV 4/5, R18(a), D06, D7).
/// <para>
/// What it must prove: a bare problem, a cause/detail chain and a set of aggregate items each render by the rule
/// stated on the SDK types themselves (<see cref="Problem"/>, <see cref="ProblemChain"/>,
/// <see cref="ProblemAggregate"/>), identity appears as a SUBORDINATE bracketed suffix and never as a prefix that
/// displaces the Danish message, and the rendered form carries nothing else — which is what "the owner is visible
/// only in the code's family" means in practice: there is no owner marker for a site to get wrong.
/// </para>
/// <para>
/// Only SDK-family problems are exercised here, which is all this task can do: the host family has no code yet
/// (T041 mints it) and the three bare-bool GUI sites have no problem to narrate yet (T043). Migrating the 24
/// existing message sites onto this path is T042's deliverable. Per D06 the report appendix renders no identity,
/// so nothing here touches a report oracle.
/// </para>
/// </summary>
public class ProblemPresentationTests
{
    private static Problem P(string code, string message, string? diagnostic = null) =>
        new(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty, diagnostic);

    [Test]
    public void ABareProblemRendersItsMessageWholeWithIdentityAsABracketedSuffix()
    {
        Problem problem = P("load-empty", "Filen er tom");

        string rendered = ProblemPresenter.Text(problem);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Is.EqualTo("Filen er tom [load-empty]"),
                "the message leads whole; identity follows it, bracketed");
            Assert.That(rendered, Does.Not.StartWith("load-empty"),
                "R18(a): a prefix would displace the message, which is the published counter-argument to codes");
        });
    }

    /// <summary>
    /// The rendered form is the message plus the identity and NOTHING else — no family word, no owner label, no
    /// severity prefix. That is what makes a host family an extension of one vocabulary rather than a second
    /// error-reporting system in the shell (R16 REV 4): the owner is readable from the code's own first segment,
    /// so the path has nothing to render about it and nothing to get wrong.
    /// </summary>
    [Test]
    public void TheRenderedFormAddsNothingButIdentity()
    {
        (string Code, string Message)[] problems =
        [
            ("load-empty", "Filen er tom"),                       // validation family: a bare published id
            ("io.save", "Projektet kunne ikke gemmes"),            // io family
            ("edit.target-locked", "Målet er låst"),               // edit family
            ("internal.unexpected", "Uventet fejl"),               // the SDK catch-all
        ];

        Assert.Multiple(() =>
        {
            foreach ((string code, string message) in problems)
            {
                string rendered = ProblemPresenter.Text(P(code, message));
                Assert.That(rendered, Is.EqualTo($"{message} [{code}]"), code);
                Assert.That(new ProblemCode(code).Family, Is.Not.EqualTo(ProblemFamily.Unknown),
                    "sanity: every case here is a real SDK family, so ownership is already legible in the code");
            }
        });
    }

    /// <summary>
    /// A code the shell cannot classify still renders: message, identity, no exception. The unknown-code rule is
    /// the SDK's (<see cref="ProblemFamily.Unknown"/> degrades rather than throws) and the presentation path must
    /// not reintroduce the failure one level up.
    /// </summary>
    [Test]
    public void AnUnknownFamilyStillRenders()
    {
        Problem problem = P("future.something-new", "En fremtidig fejl");

        Assert.Multiple(() =>
        {
            Assert.That(problem.Code.Family, Is.EqualTo(ProblemFamily.Unknown), "sanity: this SDK knows no such family");
            Assert.That(ProblemPresenter.Text(problem), Is.EqualTo("En fremtidig fejl [future.something-new]"));
        });
    }

    /// <summary>
    /// A problem carrying no code at all — <c>default(ProblemCode)</c> reaches here from a host that built one
    /// carelessly — renders its message alone. Empty brackets would be identity theatre: a suffix pointing at
    /// nothing, which a user would quote in a support question.
    /// </summary>
    [Test]
    public void AProblemWithNoIdentityRendersItsMessageAlone()
    {
        Problem problem = new(default, "Uventet fejl", EquatableArray<ProblemArgument>.Empty);

        Assert.That(ProblemPresenter.Text(problem), Is.EqualTo("Uventet fejl"));
    }

    /// <summary>
    /// The ARGUMENTS half of the contract, end to end: a producer binds the catalogue entry's Danish template with
    /// the problem's declared arguments (<see cref="ProblemCatalogEntry.BindTemplate"/> — the one text assembly the
    /// design permits, and it is the ENTRY's job, not this path's), and the value then reaches the user through
    /// this path with no placeholder left behind.
    /// </summary>
    [Test]
    public void ArgumentsReachTheUserThroughThisPath()
    {
        ProblemCode code = EditRefusalCodes.EnumTypeMissing;
        Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
            "sanity: the code is catalogued");
        Assert.That(entry.MessageTemplate, Does.Contain("{name}"), "sanity: this row's template declares a slot");

        Problem unbound = new(code, entry.MessageTemplate,
            EquatableArray.Create<ProblemArgument>([new ProblemArgument("name", "Dage")]));
        Problem produced = unbound with { Message = entry.BindTemplate(unbound) };

        string rendered = ProblemPresenter.Text(produced);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Does.Contain("Dage"), "the argument's value reaches the user");
            Assert.That(rendered, Does.Not.Contain("{"), "no slot survives into the rendered form");
            Assert.That(rendered, Does.EndWith($" [{code.Value}]"), "identity is still the suffix");
        });
    }

    [Test]
    public void TheEnglishDiagnosticIsNeverRendered()
    {
        Problem problem = P("io.load", "Projektet kunne ikke åbnes",
            "The stream ended before the root element was closed.");

        Assert.That(ProblemPresenter.Text(problem), Does.Not.Contain(problem.Diagnostic!),
            "invariant 10: the engine sentence goes to the log, never beside the Danish message");
    }

    /// <summary>
    /// Case 2 of the rendering rule, and T006's traversal rule: of the two levels of a chain, the CAUSE is what
    /// reaches the user — once. The operation's own sentence restates the same failure less precisely, so showing
    /// it too would show the user one failure twice; its CODE stays available for the log and for grouping.
    /// </summary>
    [Test]
    public void AChainRendersTheCauseOnceAndNeverTheOperationsSentence()
    {
        ProblemChain chain = new(P("io.load", "Projektet kunne ikke åbnes"), P("load-empty", "Filen er tom"));

        string rendered = ProblemPresenter.Text(chain);

        Assert.Multiple(() =>
        {
            Assert.That(rendered, Is.EqualTo("Filen er tom [load-empty]"), "the cause, and the cause's identity");
            Assert.That(rendered, Does.Not.Contain(chain.Operation.Message));
            Assert.That(rendered, Does.Not.Contain(chain.Operation.Code.Value),
                "the shown identity is the cause's — the traversal rule decides which code, not the site");
        });
    }

    /// <summary>
    /// Case 3: the head frames the failure and EVERY item is its own complete entry, in the producer's order, each
    /// with its own identity. Nothing is collapsed into the head and nothing is elided behind the count.
    /// </summary>
    [Test]
    public void AnAggregateRendersTheHeadAndEveryItemInOrder()
    {
        Problem[] items =
        [
            P("doc-cabletype", "Mangler Kabeltype"),
            P("doc-position", "Mangler Placering"),
            P("doc-not-linked", "Ikke forbundet"),
        ];
        ProblemAggregate aggregate = new(P("io.save", "Projektet har fejl"), EquatableArray.Create(items));

        IReadOnlyList<string> entries = ProblemPresenter.Entries(aggregate);

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(items.Length + 1), "the head plus every item");
            Assert.That(entries[0], Is.EqualTo("Projektet har fejl [io.save]"));
            Assert.That(entries.Skip(1), Is.EqualTo(items.Select(ProblemPresenter.Text)).AsCollection,
                "items keep the producer's order — document-scan order, for a validation aggregate");
        });
    }

    /// <summary>
    /// The dialog body for an aggregate: the same entries, one per line. It lives on this path rather than at each
    /// call site because R16 is explicit that the shell does not get to choose per site — a site that joined them
    /// its own way would be a second presentation path with a different answer.
    /// </summary>
    [Test]
    public void AnAggregateBodyIsItsEntriesOnePerLine()
    {
        ProblemAggregate aggregate = new(
            P("io.save", "Projektet har fejl"),
            EquatableArray.Create<Problem>([P("doc-cabletype", "Mangler Kabeltype"), P("doc-position", "Mangler Placering")]));

        string body = ProblemPresenter.Text(aggregate);

        Assert.Multiple(() =>
        {
            Assert.That(body.Split('\n'), Is.EqualTo(ProblemPresenter.Entries(aggregate)).AsCollection);
            Assert.That(body, Does.StartWith("Projektet har fejl [io.save]"));
        });
    }

    /// <summary>
    /// The two composition types must not render alike, or the distinction they exist for has been lost somewhere
    /// between the model and the screen: a chain rendered as a list shows one failure twice, and an aggregate
    /// reduced to one member loses N−1 findings.
    /// </summary>
    [Test]
    public void TheTwoCompositionsRenderDifferently()
    {
        Problem operation = P("io.load", "Projektet kunne ikke åbnes");
        Problem cause = P("load-empty", "Filen er tom");

        string asChain = ProblemPresenter.Text(new ProblemChain(operation, cause));
        IReadOnlyList<string> asAggregate =
            ProblemPresenter.Entries(new ProblemAggregate(operation, EquatableArray.Create<Problem>([cause])));

        Assert.Multiple(() =>
        {
            Assert.That(asAggregate, Has.Count.EqualTo(2), "an aggregate keeps both entries");
            Assert.That(asChain, Is.EqualTo(asAggregate[1]), "the chain renders only what the aggregate lists second");
            Assert.That(asAggregate[0], Does.Contain(operation.Message),
                "an aggregate head IS shown — it frames independent items rather than restating one failure");
        });
    }

    /// <summary>
    /// The rendered form agrees with the rule as the SDK states it — same suffix shape, same traversal — so the
    /// text pinned by <c>ProblemRenderingRuleTests</c> in the engine suite and the text this shell shows cannot
    /// drift apart. Stated as a test rather than as a comment because the two live in different assemblies.
    /// </summary>
    [Test]
    public void TheShellsFormMatchesTheRuleStatedOnTheSdkTypes()
    {
        Problem problem = P("doc-cabletype", "Mangler Kabeltype");

        Assert.That(ProblemPresenter.Text(problem), Is.EqualTo($"{problem.Message} [{problem.Code.Value}]"));
    }
}
