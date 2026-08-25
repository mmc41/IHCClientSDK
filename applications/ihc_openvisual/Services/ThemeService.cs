using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace ihc_openvisual.Services;

/// <summary>
/// Avalonia implementation of <see cref="IThemeService"/>: the theme variant, the workspace text scale, and the
/// high-contrast palette.
/// <para>Text scale and contrast are both here because Avalonia supplies neither. It ignores the platform's
/// text-scaling setting outright, and although it REPORTS the platform's contrast preference
/// (<see cref="PlatformColorValues.ContrastPreference"/>) it ships no high-contrast theme to switch to — so the
/// app owns both palettes (accessibility review BP-12/BP-13).</para>
/// <para>Both work by overwriting entries in <c>Application.Resources</c>, which is why every consumer of a
/// scalable or contrast-sensitive token binds it with <c>DynamicResource</c>: a <c>StaticResource</c> is resolved
/// once at load and would never see the change.</para>
/// </summary>
public sealed class ThemeService : IThemeService
{
    // The design values the scale multiplies. Kept here rather than read back from the resource dictionary,
    // because after the first scale the dictionary holds scaled values — compounding them would drift.
    private const double WorkspaceFontSize = 14;   // the inherited base; Fluent's own default
    private const double TitleFontSize = 22;
    private const double BodyFontSize = 12;
    private const double CaptionFontSize = 11;
    private const double MonoFontSize = 11;        // the Problemer list's dense monospace readout

    // The high-contrast palette. Pure black/white ink on the maximum-contrast surface, which is the point: these
    // are not "a bit darker" versions of the ordinary tokens but a deliberately maximal-contrast set.
    private static readonly Color HighContrastIcon = Colors.White;
    // The one deliberate exception to "maximal": an unavailable command's ink must still read as unavailable next
    // to that white, and disabled controls are exempt from the contrast minimum. Telling available from
    // unavailable is precisely what a high-contrast user would otherwise lose on an icon-only toolbar.
    private static readonly Color HighContrastDisabledIcon = Color.FromRgb(0x8C, 0x8C, 0x8C);
    private static readonly Color HighContrastWarning = Color.FromRgb(0xFF, 0xFF, 0x00);
    private static readonly Color HighContrastSecondaryText = Colors.White;
    private static readonly Color HighContrastLink = Color.FromRgb(0x00, 0xFF, 0xFF);

    public AppTheme Current { get; private set; } = AppTheme.System;

    public TextScale TextScale { get; private set; } = TextScale.Normal;

    public bool IsHighContrast { get; private set; }

    /// <summary>Starts following the platform's contrast preference: applies it now, and keeps following it while
    /// the app runs (the user can turn high contrast on or off without restarting). Called once at start-up;
    /// separate from the constructor so the service can be built before a platform exists.</summary>
    public void FollowPlatformContrast()
    {
        if (Application.Current?.PlatformSettings is not { } settings)
            return;

        ApplyContrast(settings.GetColorValues().ContrastPreference == ColorContrastPreference.High);
        settings.ColorValuesChanged += (_, values) =>
            ApplyContrast(values.ContrastPreference == ColorContrastPreference.High);
    }

    public void Apply(AppTheme theme)
    {
        Current = theme;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }

    public void ApplyTextScale(TextScale scale)
    {
        TextScale = scale;
        if (Application.Current is not { } app)
            return;

        double factor = scale.Factor();
        // All FIVE together: scaling the pane headers but not the tree labels — or vice versa — would break the
        // size hierarchy the design encodes, which is the whole point of a text-size setting (US-001). The
        // workspace token is the base every control that states no size of its own inherits, so it is what makes
        // the tree labels (the bulk of the app's text) scale at all. The Problemer list sets its own size and so
        // would sit out a scale entirely if its token were left behind here.
        app.Resources["WorkspaceFontSize"] = WorkspaceFontSize * factor;
        app.Resources["TitleFontSize"] = TitleFontSize * factor;
        app.Resources["BodyFontSize"] = BodyFontSize * factor;
        app.Resources["CaptionFontSize"] = CaptionFontSize * factor;
        app.Resources["MonoFontSize"] = MonoFontSize * factor;
    }

    public void ApplyContrast(bool isHighContrast)
    {
        IsHighContrast = isHighContrast;
        if (Application.Current is not { } app)
            return;

        if (isHighContrast)
        {
            app.Resources["IconColor"] = HighContrastIcon;
            app.Resources["DisabledIconColor"] = HighContrastDisabledIcon;
            app.Resources["WarningBrush"] = new SolidColorBrush(HighContrastWarning);
            app.Resources["SecondaryTextBrush"] = new SolidColorBrush(HighContrastSecondaryText);
            app.Resources["LinkBrush"] = new SolidColorBrush(HighContrastLink);
        }
        else
        {
            // Removing the overrides — rather than writing the ordinary values back — hands the tokens back to
            // App.axaml's ThemeDictionaries, so the light/dark pair keeps working. Writing literals here would
            // pin one variant's colours over both.
            foreach (string token in new[] { "IconColor", "DisabledIconColor", "WarningBrush", "SecondaryTextBrush", "LinkBrush" })
                app.Resources.Remove(token);
        }
    }
}
