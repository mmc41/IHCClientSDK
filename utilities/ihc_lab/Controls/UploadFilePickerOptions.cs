using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Platform.Storage;

namespace IhcLab;

/// <summary>
/// Builds the open-file-dialog options shared by <see cref="BinaryFilePicker"/> and <see cref="TextFilePicker"/>,
/// so the two pickers default their upload dialog to a type's canonical extension(s) in exactly the same way.
/// </summary>
internal static class UploadFilePickerOptions
{
    /// <summary>
    /// Builds the upload dialog options. When one or more canonical extensions are supplied (without leading dot,
    /// e.g. "vis" or "icw"/"icz"), the dialog defaults its filter to them - while still allowing all files - and
    /// reflects them in the title (e.g. "Select *.icw/*.icz File to Upload"). Whitespace/empty entries are ignored;
    /// when none remain it falls back to a generic "Select {fallbackNoun} File to Upload" with no filter.
    /// </summary>
    /// <param name="extensions">Canonical extensions without leading dot (or null/empty for the generic dialog).</param>
    /// <param name="fallbackNoun">Noun used in the generic title when no extension applies (e.g. "Binary", "Text").</param>
    public static FilePickerOpenOptions Build(IEnumerable<string>? extensions, string fallbackNoun)
    {
        var patterns = (extensions ?? Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => $"*.{e.Trim()}")
            .ToArray();

        if (patterns.Length == 0)
            return new FilePickerOpenOptions
            {
                Title = $"Select {fallbackNoun} File to Upload",
                AllowMultiple = false
            };

        string joined = string.Join("/", patterns);
        return new FilePickerOpenOptions
        {
            Title = $"Select {joined} File to Upload",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(joined) { Patterns = patterns },
                FilePickerFileTypes.All
            }
        };
    }
}
