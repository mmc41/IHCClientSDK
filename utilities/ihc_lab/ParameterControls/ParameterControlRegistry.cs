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
/// Strategies are evaluated in registration order and the first whose <see cref="IParameterControlStrategy.CanHandle"/>
/// returns true wins. The concrete strategies are mutually exclusive (each handles a disjoint set of types), so their
/// relative order is irrelevant; the only ordering constraint is that the catch-all
/// <see cref="Strategies.ComplexTypeParameterStrategy"/> matches any type and must therefore be registered last.
///
/// The registry is built once (the singleton via <see cref="Lazy{T}"/>, or directly with <c>new</c> in tests)
/// and then only read, all on the Avalonia UI thread, so no synchronization is needed.
/// </remarks>
public class ParameterControlRegistry
{
    private static readonly Lazy<ParameterControlRegistry> _instance = new(() => CreateDefaultRegistry());
    private readonly List<IParameterControlStrategy> _strategies = new();

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

        _strategies.Add(strategy);
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

        foreach (var strategy in _strategies)
        {
            if (strategy.CanHandle(field))
                return strategy;
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

        return _strategies.Any(s => s.CanHandle(field));
    }

    /// <summary>
    /// Gets the first strategy that can handle the specified field, or null when none can. Unlike
    /// <see cref="GetStrategy"/> this does not throw, so callers (e.g. the operation filter) can decide
    /// renderability without exception handling.
    /// </summary>
    /// <param name="field">The field metadata to find a strategy for.</param>
    /// <returns>The first matching strategy, or null when no strategy can handle the field.</returns>
    public IParameterControlStrategy? TryGetStrategy(FieldMetaData field)
    {
        if (field == null)
            return null;

        return _strategies.FirstOrDefault(s => s.CanHandle(field));
    }

    /// <summary>
    /// Gets the count of registered strategies.
    /// </summary>
    public int StrategyCount => _strategies.Count;

    /// <summary>
    /// Creates a new registry with default strategies registered.
    /// Registration order only matters for the catch-all, which must be registered last.
    /// </summary>
    /// <returns>A new registry instance with default strategies</returns>
    private static ParameterControlRegistry CreateDefaultRegistry()
    {
        var registry = new ParameterControlRegistry();

        // Concrete strategies handle disjoint type sets, so their relative order does not matter; only the
        // catch-all must come last (see the class remarks).

        // Scalar types
        registry.Register(new Strategies.StringParameterStrategy());
        registry.Register(new Strategies.BoolParameterStrategy());
        registry.Register(new Strategies.NumericParameterStrategy());

        // Specialized types
        registry.Register(new Strategies.FileParameterStrategy());
        registry.Register(new Strategies.ResourceValueParameterStrategy());
        registry.Register(new Strategies.EnumParameterStrategy());
        registry.Register(new Strategies.DateTimeParameterStrategy());
        registry.Register(new Strategies.TimeSpanParameterStrategy());
        registry.Register(new Strategies.ArrayParameterStrategy());

        // Catch-all (must be registered last)
        registry.Register(new Strategies.ComplexTypeParameterStrategy());

        return registry;
    }
}
