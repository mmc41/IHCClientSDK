using System;
using System.Collections.Generic;
using System.Linq;
using Ihc;

namespace IhcLab.ParameterControls;

/// <summary>
/// Central registry for parameter control strategies.
/// Implements chain of responsibility pattern to select the appropriate strategy for a given field type.
/// </summary>
/// <remarks>
/// Strategies are evaluated in registration order, so register more specific strategies before general ones.
/// Example order: Array → Enum → Numeric → String → ComplexType
///
/// The registry is typically used as a singleton, but can be instantiated for testing purposes.
/// </remarks>
public class ParameterControlRegistry
{
    private static readonly Lazy<ParameterControlRegistry> _instance = new(() => CreateDefaultRegistry());
    private readonly List<IParameterControlStrategy> _strategies = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of the registry with default strategies registered.
    /// </summary>
    public static ParameterControlRegistry Instance => _instance.Value;

    /// <summary>
    /// Registers a strategy in the registry.
    /// Strategies are evaluated in registration order during GetStrategy() calls.
    /// </summary>
    /// <param name="strategy">The strategy to register</param>
    /// <exception cref="ArgumentNullException">Thrown if strategy is null</exception>
    public void Register(IParameterControlStrategy strategy)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));

        lock (_lock)
        {
            _strategies.Add(strategy);
        }
    }

    /// <summary>
    /// Gets the first strategy that can handle the specified field.
    /// Uses chain of responsibility pattern to find a suitable strategy.
    /// </summary>
    /// <param name="field">The field metadata to find a strategy for</param>
    /// <returns>The first strategy that returns true from CanHandle()</returns>
    /// <exception cref="ArgumentNullException">Thrown if field is null</exception>
    /// <exception cref="NotSupportedException">Thrown if no strategy can handle the field type</exception>
    public IParameterControlStrategy GetStrategy(FieldMetaData field)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));

        lock (_lock)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.CanHandle(field))
                    return strategy;
            }
        }

        throw new NotSupportedException(
            $"No strategy found for field '{field.Name}' of type '{field.Type.FullName}'. " +
            $"Please register a strategy that can handle this type.");
    }

    /// <summary>
    /// Checks if any registered strategy can handle the specified field.
    /// </summary>
    /// <param name="field">The field metadata to check</param>
    /// <returns>True if a strategy exists that can handle the field; otherwise false</returns>
    public bool CanHandle(FieldMetaData field)
    {
        if (field == null)
            return false;

        lock (_lock)
        {
            return _strategies.Any(s => s.CanHandle(field));
        }
    }

    /// <summary>
    /// Gets the count of registered strategies.
    /// </summary>
    public int StrategyCount
    {
        get
        {
            lock (_lock)
            {
                return _strategies.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new registry with default strategies registered.
    /// Registration order matters - more specific types first, general types last.
    /// </summary>
    /// <returns>A new registry instance with default strategies</returns>
    private static ParameterControlRegistry CreateDefaultRegistry()
    {
        var registry = new ParameterControlRegistry();

        // Register strategies in order of specificity (most specific first)
        // IMPORTANT: Order matters! More specific strategies must be registered before general ones.

        // Phase 1 strategies (basic types)
        registry.Register(new Strategies.StringParameterStrategy());
        registry.Register(new Strategies.BoolParameterStrategy());
        registry.Register(new Strategies.NumericParameterStrategy());

        // Phase 2 strategies (existing types - specific to general)
        registry.Register(new Strategies.FileParameterStrategy());           // Check before complex types
        registry.Register(new Strategies.ResourceValueParameterStrategy());  // Specific type
        registry.Register(new Strategies.EnumParameterStrategy());           // Enums before complex
        registry.Register(new Strategies.DateTimeParameterStrategy());       // DateTime/DateTimeOffset

        // Phase 3 strategies (new functionality)
        registry.Register(new Strategies.ArrayParameterStrategy());          // Arrays (NEW - before complex!)

        // Catch-all strategy (MUST be last!)
        registry.Register(new Strategies.ComplexTypeParameterStrategy());    // Catch-all for SubTypes (last!)

        return registry;
    }

    /// <summary>
    /// Creates an empty registry for testing purposes.
    /// </summary>
    /// <returns>A new empty registry</returns>
    public static ParameterControlRegistry CreateEmpty()
    {
        return new ParameterControlRegistry();
    }
}
