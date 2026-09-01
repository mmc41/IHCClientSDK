using System;
using System.Xml;

namespace Ihc.Vis.Model
{
    /// <summary>
    /// The two input refusals every recursive element walk over an IHC XML file owes its caller: a nesting
    /// ceiling, and a refusal of character data in an attribute-only model.
    /// <para>
    /// Shared because the walk is: <c>ProjectReader.ReadElement</c> (<c>.vis</c>) and
    /// <c>CatalogReader.ReadElement</c> (<c>.def</c>/<c>.ifb</c>) are the same descent over two file families,
    /// and hardening only one of them is how the catalog side came to be missing both. What is NOT shared is
    /// the throw: the two readers refuse into different families — a coded load refusal on the project side, an
    /// <see cref="XmlException"/> the import path wraps on the catalog side — so this type DETECTS and phrases,
    /// and each reader raises its own.
    /// </para>
    /// </summary>
    internal static class ElementWalkGuards
    {
        /// <summary>Real projects and component files nest ~12 deep; far past that is corrupt input.</summary>
        internal const int MaxElementDepth = 128;

        /// <summary>
        /// The sentence for a walk that must refuse rather than recurse, or null while within the ceiling.
        /// The ceiling is a stack-exhaustion guard first and a validity check second: the depth that overflows
        /// raises <see cref="StackOverflowException"/>, which .NET cannot catch and which takes the process with
        /// it, so the refusal has to happen while there is still a stack to refuse on.
        /// </summary>
        /// <param name="depth">The depth the walk is about to descend to.</param>
        /// <param name="subject">Names the file family, for the message.</param>
        internal static string? DepthMessage(int depth, string subject) =>
            depth > MaxElementDepth
                ? $"Element nesting exceeds {MaxElementDepth} levels; the file is corrupt or not {subject}."
                : null;

        /// <summary>
        /// The sentence for character data the caller must refuse, or null when the reader is not positioned on
        /// any. Whitespace-only nodes return null: indentation carries nothing the model would lose, and a guard
        /// that treated it as content would refuse every hand-formatted file in existence.
        /// </summary>
        /// <param name="reader">The reader, positioned on the node under inspection.</param>
        /// <param name="parentTag">The element the text sits inside, for the message.</param>
        /// <param name="consequence">
        /// What loading it would cost, phrased for the file family — the half of the message that differs
        /// between a project and a catalog component.
        /// </param>
        internal static string? CharacterDataMessage(XmlReader reader, string parentTag, string consequence)
        {
            if (reader.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                || string.IsNullOrWhiteSpace(reader.Value))
            {
                return null;   // whitespace-only nodes carry no information the model would lose
            }

            string excerpt = reader.Value.Trim();
            if (excerpt.Length > 40)
            {
                excerpt = excerpt.Substring(0, 40) + "...";
            }

            string at = reader is IXmlLineInfo info && info.HasLineInfo()
                ? $" (line {info.LineNumber}, position {info.LinePosition})"
                : string.Empty;

            return $"Element <{parentTag}> contains character data (\"{excerpt}\"){at}; {consequence}";
        }
    }
}
