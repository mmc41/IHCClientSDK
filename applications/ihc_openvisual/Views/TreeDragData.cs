using System;
using Avalonia.Input;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace ihc_openvisual.Views;

/// <summary>
/// The drag payload for tree-node drag-and-drop (Wave 9; introduced by the A-P0 feasibility spike). A dragged node
/// carries its <see cref="ElementId"/> inside an <see cref="IDataTransfer"/> under a private format key, so a drop
/// reads the id back <b>from the data transfer</b> — never from a source field captured at <c>PointerPressed</c>.
/// Keying off the data transfer is what lets Avalonia's headless <c>window.DragDrop</c> (which supplies its own
/// <see cref="IDataTransfer"/>) exercise the real drop path, and it is also what makes a genuine external drop work.
/// Factoring the payload here makes "the drag carries the right id" a plain, headless unit test (§0.3).
/// </summary>
public static class TreeDragData
{
    /// <summary>The private drag format carrying the dragged element id. A string <b>application</b> format holds the
    /// id encoded as <c>"Counter:TypeCode"</c>. A string (serialisable) format is used rather than an in-process one
    /// because <see cref="DataFormat.CreateInProcessFormat{T}"/> is constrained to reference types and
    /// <see cref="ElementId"/> is a value type — and a serialisable id is also what a real cross-application drop
    /// needs. One shared instance so the format used to write the payload equals the one used to read it back.</summary>
    // An application-format identifier accepts only ASCII letters/digits, '.' and '-' (no slash), so a MIME-style
    // key is rejected — a dotted, reverse-namespaced identifier is used instead.
    public static readonly DataFormat<string> ElementIdFormat =
        DataFormat.CreateStringApplicationFormat("dk.edora.ihc.openvisual.element-id");

    /// <summary>Builds the drag payload for a node, or <c>null</c> when the node addresses no element (nothing to
    /// drag — e.g. the synthetic <c>Localities</c> root).</summary>
    public static DataTransfer? BuildDragData(TreeNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.ElementId is not { } id)
            return null;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ElementIdFormat, Encode(id)));
        return data;
    }

    /// <summary>Reads the dragged element id back from a drop's data transfer, or <c>null</c> when it carries no
    /// (valid) id under this format.</summary>
    public static ElementId? TryGetElementId(IDataTransfer? data) =>
        data?.TryGetValue(ElementIdFormat) is { } encoded && TryDecode(encoded, out ElementId id) ? id : null;

    private static string Encode(ElementId id) => $"{id.Counter}:{id.TypeCode}";

    private static bool TryDecode(string encoded, out ElementId id)
    {
        int counter = 0, typeCode = 0;
        string[] parts = encoded.Split(':');
        bool ok = parts.Length == 2 && int.TryParse(parts[0], out counter) && int.TryParse(parts[1], out typeCode);
        id = new ElementId(counter, typeCode);   // meaningful only when ok; the caller discards id on false
        return ok;
    }
}
