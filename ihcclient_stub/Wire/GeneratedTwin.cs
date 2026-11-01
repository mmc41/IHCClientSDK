using System.Reflection;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Copies a generated wire value into the same-named type of another generated namespace. Each SOAP service
    /// generates its own copy of the shared value types, alike member for member, so a value the stub reads or
    /// writes in one service's terms crosses to another by name, and one mapper serves both.
    /// </summary>
    internal static class GeneratedTwin
    {
        /// <summary>
        /// <paramref name="source"/> as the same-named type in <typeparamref name="T"/>'s namespace, with every
        /// public property copied, translating each generated member the same way.
        /// </summary>
        internal static T? Translate<T>(object? source) where T : class =>
            (T?)Translate(source, typeof(T).Namespace ?? throw new ArgumentException("a generated type has a namespace", nameof(T)));

        private static object? Translate(object? source, string targetNamespace)
        {
            if (source is null)
            {
                return null;
            }
            Type from = source.GetType();
            Type to = from.Assembly.GetType($"{targetNamespace}.{from.Name}")
                ?? throw new InvalidOperationException($"{targetNamespace} generates no twin of {from.FullName}");
            object copy = Activator.CreateInstance(to)
                ?? throw new InvalidOperationException($"{to.FullName} cannot be constructed");
            foreach (PropertyInfo target in GeneratedTypes.SettableProperties(to))
            {
                PropertyInfo origin = from.GetProperty(target.Name, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"{from.FullName} has no {target.Name} for its twin in {targetNamespace}");
                object? value = origin.GetValue(source);
                target.SetValue(copy, GeneratedTypes.IsGenerated(target.PropertyType) ? Translate(value, targetNamespace) : value);
            }
            return copy;
        }
    }
}
