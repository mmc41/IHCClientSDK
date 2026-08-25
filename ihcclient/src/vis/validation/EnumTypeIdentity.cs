#nullable enable
using System;

using Ihc.Vis.Model;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// Which enumerator types the AUTHOR owns — the population every enum row is about. Shared by
    /// <see cref="EnumDefinitionRules"/> (the type's own shape) and <see cref="VariableUsageRules"/> (its values'
    /// use), so the two can never disagree about what counts as an authored type.
    /// </summary>
    internal static class EnumTypeIdentity
    {
        /// <summary>The attribute marking a definition as one of the format's own read-only system tables.</summary>
        private const string SystemTypeAttribute = "typeid";

        /// <summary>
        /// A definition the author owns.
        /// <para>
        /// NOT a <c>typeid</c>-bearing SYSTEM table: 40 of the corpus's 109 definitions are shipped with the format
        /// and read-only in the application, and most projects reference none of them, so including them would make
        /// the shape rows fire on nearly every authentic file and the value row report 11 unused values in every
        /// project — the EMPTY one included.
        /// </para>
        /// <para>
        /// NOT the data-tables definition either: <see cref="ProjectProjections.UserTextsTableName"/> holds the
        /// project's user-defined TEXTS (US-049) rather than a type's values, so no variable is ever declared of it
        /// and none of its rows is ever referenced as a value.
        /// </para>
        /// </summary>
        internal static bool IsAuthored(ProjectElement definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.GetAttribute(SystemTypeAttribute) is not { Length: > 0 }
                && Name(definition) != ProjectProjections.UserTextsTableName;
        }
    }
}
