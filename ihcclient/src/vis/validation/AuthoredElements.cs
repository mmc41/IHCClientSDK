using System;
using System.Collections.Frozen;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The elements a PERSON authors and reads back — a locality, a product, a terminal, a function block, a block
    /// variable. Shared by the rows that ask something about "an element the user sees": <c>name-empty</c> asks
    /// whether it has a name, <c>struct-icon-default</c> whether it has an icon.
    ///
    /// <para><b>Both exclusions are measured, and both are load-bearing.</b> The format's structural containers
    /// carry names AND icons that nobody authors — the module rack ships unnamed in all 45 corpus occurrences, and a
    /// <c>settings</c> container's icon is furniture. And a <c>resource_*</c> element is a declared variable only
    /// inside one of the four <see cref="FunctionBlockSections"/> containers: the same tags appear inside
    /// <c>action</c>/<c>case_action</c>/<c>condition</c> as the operand a command works on, where they are literal
    /// VALUES with neither a name nor an icon.</para>
    /// </summary>
    internal static class AuthoredElements
    {
        /// <summary>Whether this element is one a person authors and reads back.</summary>
        /// <param name="element">The element to classify.</param>
        /// <param name="topology">The topology analysis, for the declared-variable test.</param>
        internal static bool IsAuthored(ProjectElement element, ITopologyAnalysis topology)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(topology);
            return element.Tag switch
            {
                "group" or "functionblock" => true,
                _ when ProductClassifier.IsProduct(element.Tag) => true,
                _ when IsTerminal(element.Tag) => true,
                _ when element.Tag.StartsWith("resource_", StringComparison.Ordinal) =>
                    IsBlockVariable(element, topology),
                _ => false,
            };
        }

        /// <summary>A terminal on a product, as opposed to the module rack the terminals are grouped into.</summary>
        internal static bool IsTerminal(string tag) =>
            (tag.StartsWith("dataline_", StringComparison.Ordinal)
                || tag.StartsWith("airlink_", StringComparison.Ordinal))
            && !tag.EndsWith("_module", StringComparison.Ordinal)
            && !tag.EndsWith("_modules", StringComparison.Ordinal);

        /// <summary>
        /// The four section tags a block declares its variables in, from the one list that names them. A set
        /// rather than a scan of <see cref="FunctionBlockSections.All"/>: this is asked once per element by three
        /// rules, and <c>resource_*</c> is the most numerous kind in a <c>.vis</c> file.
        /// </summary>
        private static readonly FrozenSet<string> DeclaringContainers =
            FunctionBlockSections.All.Select(section => section.Container).ToFrozenSet(StringComparer.Ordinal);

        /// <summary>A variable DECLARED by a block, as opposed to one used as a program command's operand.</summary>
        internal static bool IsBlockVariable(ProjectElement variable, ITopologyAnalysis topology) =>
            topology.Parent(variable) is { } parent && DeclaringContainers.Contains(parent.Tag);
    }
}
