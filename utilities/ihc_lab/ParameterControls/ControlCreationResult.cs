using System.Collections.Generic;
using Avalonia.Controls;

namespace IhcLab.ParameterControls;

/// <summary>
/// Result container for parameter control creation operations.
/// Contains the created control and optional metadata for composite controls.
/// </summary>
public record ControlCreationResult
{
    /// <summary>
    /// The Avalonia control created for the parameter.
    /// </summary>
    public required Control Control { get; init; }

    /// <summary>
    /// Indicates whether this control is a composite (contains sub-controls).
    /// True for complex types, arrays, or nested structures.
    /// </summary>
    public bool IsComposite { get; init; } = false;

    /// <summary>
    /// For composite controls, maps sub-control names to their strategies.
    /// Used to extract values from nested controls.
    /// </summary>
    public Dictionary<string, IParameterControlStrategy> SubStrategies { get; init; } = new();
}
