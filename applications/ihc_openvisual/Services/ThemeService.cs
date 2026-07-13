using Avalonia;
using Avalonia.Styling;

namespace ihc_openvisual.Services;

/// <summary>Applies the chosen workspace theme by setting <see cref="Application.RequestedThemeVariant"/>.</summary>
public sealed class ThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;

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
}
