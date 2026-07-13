namespace ihc_openvisual.Services;

/// <summary>The workspace theme the installer can pick from the <i>View</i> menu (US-001 SHOULD).</summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>Applies the chosen <see cref="AppTheme"/> to the running application. Implemented in the view
/// layer so view-models stay free of Avalonia types.</summary>
public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}
