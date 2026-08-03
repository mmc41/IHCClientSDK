namespace ihc_openvisual.Services;

/// <summary>The workspace theme the installer can pick from the <i>View</i> menu (US-001 SHOULD).</summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// The workspace text-size steps the installer can pick from <i>Vis</i> (US-001 SHOULD). This exists because
/// Avalonia does not honour the operating system's own text-scaling setting at all, so an in-app control is the
/// only route to WCAG 1.4.4 (Resize Text) — see the accessibility review, BP-12.
/// </summary>
public enum TextScale
{
    Small,
    Normal,
    Large,
    Largest
}

public static class TextScaleExtensions
{
    /// <summary>The multiplier applied to every workspace font token. <see cref="TextScale.Normal"/> is exactly
    /// 1.0 so the unscaled design stays reachable, and the steps are ordered smallest to largest — the story
    /// leaves the exact factors unspecified, only their order and the Normal default.</summary>
    public static double Factor(this TextScale scale) => scale switch
    {
        TextScale.Small => 0.85,
        TextScale.Large => 1.25,
        TextScale.Largest => 1.5,
        _ => 1.0,
    };
}

/// <summary>
/// Applies the workspace appearance choices — theme, text size, and the high-contrast palette — to the running
/// application. Implemented in the view layer so view-models stay free of Avalonia types (architecture review
/// A-03); the view-models see only this port.
/// </summary>
public interface IThemeService
{
    AppTheme Current { get; }

    /// <summary>The active text-size step (US-001).</summary>
    TextScale TextScale { get; }

    /// <summary>Whether the high-contrast palette is active. Driven by the platform's own contrast preference
    /// rather than by a menu: Avalonia reports that preference but ships no high-contrast theme, so supplying the
    /// palette is the application's job (accessibility review BP-13).</summary>
    bool IsHighContrast { get; }

    void Apply(AppTheme theme);

    /// <summary>Applies a text-size step; takes effect immediately, with no restart or reopen.</summary>
    void ApplyTextScale(TextScale scale);

    /// <summary>Switches the high-contrast palette on or off. Live in both directions, since the platform
    /// preference can change while the app runs.</summary>
    void ApplyContrast(bool isHighContrast);
}
