namespace ihc_openvisual.ViewModels;

/// <summary>
/// How the trees render a wire — the <i>Vis ▸ Links</i> choice (US-089).
/// <para>Session-only, like the theme and the text scale beside it in the same menu: nothing about it is
/// persisted, so a fresh shell always starts at <see cref="Labels"/>.</para>
/// </summary>
public enum LinkDisplay
{
    /// <summary>The default, and the reference application's rendering: each half of a wire is a child row under
    /// its pin, labelled with the bare path of the opposite end (US-022).</summary>
    Labels,

    /// <summary>One directed arrow drawn between the two pin rows instead, in the same pane or across the two.
    /// A wire whose opposite end has no row in the current view keeps its label — see
    /// <see cref="LinkArrowPlanner.DrawsAsArrow"/>.</summary>
    Arrows,
}
