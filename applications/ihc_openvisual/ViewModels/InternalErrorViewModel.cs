using System;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Ihc.Vis.Problems;
using ihc_openvisual.Services;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// What the internal-error dialog SHOWS and what its copy button COPIES — the same facts, assembled once.
///
/// <para><b>Why the payload lives here and not in the code-behind.</b> The copy button's whole purpose is that
/// the text it hands over is what the reader was looking at; assembling it beside the clipboard call would put
/// that guarantee in a layer no headless test can reach, and the two would be free to drift the first time
/// either changed. Here the dialog's fields and the copied text are read off ONE object, so a test can hold
/// them against each other.</para>
///
/// <para><b>It names no Avalonia type.</b> The clipboard is an Avalonia concept, so the code-behind keeps the
/// <c>TopLevel.GetTopLevel(this)?.Clipboard</c> call and nothing else — it asks this for the text, hands it over,
/// and reports back which of the two things happened.</para>
/// </summary>
public sealed partial class InternalErrorViewModel : ObservableObject
{
    /// <summary>The copy button at rest.</summary>
    public const string CopyLabel = "Kopiér";

    /// <summary>The copy button after a successful copy. Transient feedback ON the button, because the dialog is
    /// modal over the status bar and the status bar is therefore the one surface the reader cannot see.</summary>
    public const string CopiedLabel = "Kopieret";

    /// <summary>How a timestamp reads: sortable, unambiguous, and the same in every locale a report is read in.</summary>
    private const string ObservedFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly InternalError _error;

    public InternalErrorViewModel(InternalError error, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(error);
        _error = error;
        AppVersion = appVersion;
    }

    /// <summary>The Danish sentence, WHOLE, as the catalogue bound it.</summary>
    public string Sentence => _error.Message;

    /// <summary>Which fault, from where, and when — the line a support case quotes.</summary>
    public string Identity => $"{_error.Code.Value} · {OriginText(_error.Origin)} · {Observed}";

    /// <summary>The technical readout: the English diagnostic first, then the captured detail.</summary>
    public string Detail => _error.Diagnostic is { Length: > 0 } diagnostic
        ? $"{diagnostic}{Environment.NewLine}{Environment.NewLine}{_error.Detail}"
        : _error.Detail;

    /// <summary>The label the dialog puts above <see cref="Detail"/>.</summary>
    public const string DetailLabel = "Teknisk detalje";

    /// <summary>The app build the fault was observed in. Supplied rather than read here so this stays a pure
    /// projection of its inputs and a test can pin the assembled text exactly.</summary>
    public string AppVersion { get; }

    [ObservableProperty] private string _copyText = CopyLabel;

    /// <summary>
    /// Everything a bug report needs, in a fixed order: identity first so a reader can tell at a glance WHICH
    /// fault this is, the Danish sentence next because it is what the user saw, then the build, then the
    /// technical readout.
    /// </summary>
    /// <remarks>
    /// Labelled, and the labels are Danish like the rest of the chrome — a payload pasted into an email is read
    /// by a person before it is read by a developer. Only the diagnostic and the captured detail are verbatim
    /// technical text, which is the same split the dialog itself draws.
    /// </remarks>
    public string Payload
    {
        get
        {
            StringBuilder text = new();
            text.Append("Kode: ").AppendLine(_error.Code.Value);
            text.Append("Oprindelse: ").AppendLine(OriginText(_error.Origin));
            text.Append("Tidspunkt: ").AppendLine(Observed);
            text.Append("Programversion: ").AppendLine(AppVersion);
            text.AppendLine();
            text.AppendLine(Sentence);
            text.AppendLine();
            text.Append(DetailLabel).AppendLine(":");
            text.Append(Detail);
            return text.ToString();
        }
    }

    /// <summary>The copy succeeded; say so on the button.</summary>
    public void MarkCopied() => CopyText = CopiedLabel;

    /// <summary>
    /// There was no clipboard to copy to. Reported IN PLACE, on the button, with the coded refusal's own Danish
    /// sentence — a second modal over a modal would be worse than the failure, and the status bar is hidden
    /// behind this dialog.
    /// </summary>
    public void MarkCopyUnavailable() => CopyText = HostProblems.ClipboardUnavailable().Message;

    /// <summary>What the origin says in Danish. A reader has to be able to tell OUR bug from the platform's.</summary>
    public static string OriginText(InternalErrorOrigin origin) => origin switch
    {
        InternalErrorOrigin.Sdk => "SDK",
        InternalErrorOrigin.Host => "Program",
        InternalErrorOrigin.Platform => "Platform",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown internal error origin"),
    };

    private string Observed =>
        _error.Observed.ToLocalTime().ToString(ObservedFormat, CultureInfo.InvariantCulture);
}
