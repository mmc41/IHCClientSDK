#nullable enable
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Selects a mode's view of a full-tagged <see cref="ReportShapeDocument"/> (spec R4): Full passes the
    /// document through untouched; Standard drops <see cref="ReportMembership.FullOnly"/> shapes, strips the
    /// Full-only row FIELDS (the <c>(ID …)</c> chip and the note's locality suffix), and re-tags the layout
    /// a stripped shape switches to. This is the single home of mode membership at render time — the writers
    /// never see the mode.
    /// </summary>
    internal static class ReportModeFilter
    {
        public static ReportShapeDocument Select(ReportShapeDocument document, ReportMode mode)
        {
            ReportShapeDocument result = document;
            if (mode == ReportMode.Standard)
            {
                result = document with
                {
                    Shapes = document.Shapes
                        .Where(s => s.Membership == ReportMembership.Common)
                        .Select(StripFullOnlyFields)
                        .ToImmutableArray(),
                };
            }
            return result;
        }

        private static ReportShape StripFullOnlyFields(ReportShape shape) => shape switch
        {
            TreeShape tree => tree with { Rows = tree.Rows.Select(StripRow).ToImmutableArray() },
            KeyValueBlockShape block => block with { Rows = StripKeyValues(block.Rows) },
            TableShape table => StripTable(table),
            ComponentBlockShape component => component with
            {
                Fields = StripKeyValues(component.Fields),
                Terminals = component.Terminals is { } terminals ? StripTable(terminals) : null,
            },
            FbBlockShape block => block with
            {
                IdToken = null,
                Identity = ImmutableArray<KeyValueRow>.Empty,
                // Without the identity grid the section joins the single-line run: the mode decision becomes
                // an explicit layout property here, so the writers never infer it from the stripped content.
                Standalone = false,
                Rows = block.Rows.Select(StripRow).ToImmutableArray(),
            },
            _ => shape,
        };

        private static ReportTreeRow StripRow(ReportTreeRow row) => row switch
        {
            NamedTreeRow named => named with { IdToken = null },
            PlainTreeRow plain => plain with { IdToken = null },
            NoteTreeRow note => note with { LocalitySuffix = null },
            IconTreeRow icon => icon with { IdToken = null },
            _ => row,
        };

        private static ImmutableArray<KeyValueRow> StripKeyValues(ImmutableArray<KeyValueRow> rows) =>
            rows.Select(r => r with { IdToken = null }).ToImmutableArray();

        private static TableShape StripTable(TableShape table) => table with
        {
            Rows = table.Rows
                .Select(row => row.Select(c => c with { IdToken = null }).ToImmutableArray())
                .ToImmutableArray(),
        };
    }
}
