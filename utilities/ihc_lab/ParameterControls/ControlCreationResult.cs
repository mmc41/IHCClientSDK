using Avalonia.Controls;

namespace IhcLab.ParameterControls;

/// <summary>
/// Result container for parameter control creation operations.
/// Wraps the created Avalonia control.
/// </summary>
public record ControlCreationResult
{
    /// <summary>
    /// The Avalonia control created for the parameter.
    /// </summary>
    public required Control Control { get; init; }
}
