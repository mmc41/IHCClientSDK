#nullable enable
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// What every rule module needs to MINT a rule, stated once.
    /// <para>
    /// <see cref="RuleBuilder"/> takes an ENTRY, and <see cref="IProjectInspection.Report"/> takes a bound
    /// argument array, so each module used to re-implement the two steps between a code and a registered rule:
    /// look the entry up and throw <see cref="RuleRegistrationFault.NoCatalogueEntry"/> when it is missing, and
    /// turn a tuple list into <see cref="ProblemArgument"/>s. Twenty-one and eighteen copies of those, plus
    /// thirteen of the display-name fallback and five of the threshold read, are one edit here instead.
    /// </para>
    /// <para>
    /// Imported with <c>using static</c> by the rule modules, so a rule site still reads <c>Rule(catalog, …)</c>
    /// and <c>Arguments(…)</c> — the authoring vocabulary is shared, not relocated.
    /// </para>
    /// </summary>
    internal static class RuleAuthoring
    {
        /// <summary>Mints the traversal rule for one code, refusing a code the catalogue does not declare.</summary>
        /// <param name="catalog">The catalogue the entry is declared in.</param>
        /// <param name="code">The code the rule implements.</param>
        /// <param name="body">The traversal that reports what it finds.</param>
        /// <exception cref="RuleRegistrationException">The catalogue declares no such code.</exception>
        internal static RuleDefinition Rule(ProblemCatalog catalog, ProblemCode code, ProjectInspection body) =>
            catalog.TryGet(code, out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Inspect(body).Build()
                : throw new RuleRegistrationException(code, RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>The same, for a module that spells its codes as literals.</summary>
        /// <param name="catalog">The catalogue the entry is declared in.</param>
        /// <param name="code">The code the rule implements.</param>
        /// <param name="body">The traversal that reports what it finds.</param>
        internal static RuleDefinition Rule(ProblemCatalog catalog, string code, ProjectInspection body) =>
            Rule(catalog, new ProblemCode(code), body);

        /// <summary>Binds a rule's declared argument slots for one finding.</summary>
        /// <param name="bindings">The slot name and its value, in the entry's declared order.</param>
        internal static EquatableArray<ProblemArgument> Arguments(params (string Name, object Value)[] bindings) =>
            [.. bindings.Select(b => new ProblemArgument(b.Name, b.Value))];

        /// <summary>
        /// The label a finding shows for an element: its name, else its tag. The one answer, so a bound argument
        /// and the site the executor describes cannot name the same element two ways.
        /// </summary>
        /// <param name="element">The element to label.</param>
        internal static string Name(ProjectElement element) =>
            element.GetAttribute("name") is { Length: > 0 } name ? name : element.Tag;

        /// <summary>
        /// One declared threshold off a catalogue entry — the numbers a rule compares against are DATA on the
        /// entry, never a literal at the rule site.
        /// </summary>
        /// <param name="catalog">The catalogue the entry is declared in.</param>
        /// <param name="code">The code whose entry declares the threshold.</param>
        /// <param name="name">The threshold's declared name.</param>
        /// <exception cref="RuleRegistrationException">No such code, or no threshold of that name on it.</exception>
        internal static double Threshold(ProblemCatalog catalog, string code, string name) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
            && entry.Thresholds.FirstOrDefault(t => t.Name == name) is { } threshold
                ? threshold.Value
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>Every product in the project, in document order, through the shared classifier.</summary>
        /// <param name="analyses">The run's shared analyses.</param>
        internal static IEnumerable<ProjectElement> AllProducts(IProjectAnalyses analyses) =>
            analyses.Elements.Where(e => ProductClassifier.IsProduct(e.Tag));

        /// <summary>Every function block in the project, in document order.</summary>
        /// <param name="analyses">The run's shared analyses.</param>
        internal static IEnumerable<ProjectElement> Blocks(IProjectAnalyses analyses) =>
            analyses.WithTag("functionblock");
    }
}
