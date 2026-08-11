namespace ihc_openvisual.ViewModels;

/// <summary>
/// The caption of a scene MEMBER's value dialog, which the reference application takes from the member's TYPE
/// (alignment F-49, measured live 2026-08-11 by wiring a block's scene pin to two products):
/// <code>
///   relay  (Lampeudtag ▸ Scenarier)                    Relæ scenarie egenskaber
///   dimmer (Lampeudtag dimmer ▸ Scenarier/regulering)   Lysdæmper scenarie egenskaber
/// </code>
/// <para>Shared because the dialog has TWO call sites — once when a link is being made, once when an existing
/// membership is edited — and the original captions both the same way. Correcting one and not the other is
/// exactly what happened: the edit-time title was fixed first and the link-time one went on reading
/// <c>Scenarie værdi</c>, which is only visible by actually making a link.</para>
/// </summary>
internal static class SceneValueTitles
{
    /// <summary>The dialog caption for a member of the given kind.</summary>
    public static string For(bool isDimmer) => $"{(isDimmer ? "Lysdæmper" : "Relæ")} scenarie egenskaber";
}
