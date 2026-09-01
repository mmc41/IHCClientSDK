using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

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
        var patterns = NormalizePatterns(extensions);

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

    /// <summary>
    /// Builds the upload button's caption for a picker accepting the given canonical extensions, mirroring the
    /// dialog title: it names the concrete type when one or more extensions are known (e.g. "Upload *.vis File" or
    /// "Upload *.icw/*.icz File") and otherwise falls back to a generic "Upload {fallbackNoun} File". This keeps the
    /// button honest about what the picker accepts instead of always claiming a generic "Text"/"Binary" file.
    /// </summary>
    /// <param name="extensions">Canonical extensions without leading dot (or null/empty for the generic caption).</param>
    /// <param name="fallbackNoun">Noun used in the generic caption when no extension applies (e.g. "Binary", "Text").</param>
    public static string BuildUploadButtonCaption(IEnumerable<string>? extensions, string fallbackNoun)
    {
        var patterns = NormalizePatterns(extensions);
        string descriptor = patterns.Length == 0 ? fallbackNoun : string.Join("/", patterns);
        return $"Upload {descriptor} File";
    }

    /// <summary>
    /// Records a failed upload, and answers the one line the picker puts on its status label.
    /// </summary>
    /// <remarks>
    /// <para>Both pickers reduced the exception to <c>$"Error: {ex.Message}"</c> on a transient label and kept
    /// nothing else. Neither control has a logger of its own, and the caller is an <c>async void</c> click
    /// handler, so no boundary above saw the fault either: the type and the stack were gone the moment the label
    /// was written, on the path that reads the file the user just picked.</para>
    /// <para>Here rather than twice, for the reason <see cref="Build"/> is here: these two controls are siblings
    /// that already share their dialog wording, and a copy in each is a copy free to drift. The logger comes
    /// from <c>Program.loggerFactory</c>, which is how everything else in this application reaches the
    /// pipeline.</para>
    /// </remarks>
    /// <param name="failure">What went wrong.</param>
    /// <param name="picker">The control reporting it, for the log category.</param>
    /// <returns>The status-label text — unchanged, so the user sees exactly what they saw before.</returns>
    public static string ReportUploadFailure(Exception failure, string picker)
    {
        ArgumentNullException.ThrowIfNull(failure);
        (Program.loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateLogger(picker)
            .LogError(failure, "Uploading a picked file failed in {Picker}", picker);
        return $"Error: {failure.Message}";
    }

    private static string[] NormalizePatterns(IEnumerable<string>? extensions)
    {
        return (extensions ?? Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => $"*.{e.Trim()}")
            .ToArray();
    }
}
