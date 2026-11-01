using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Serialization;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Builds a plausible value of a generated type from its shape alone, so an operation with no hand-written
    /// answer still answers, and a regenerated operation answers on arrival.
    /// </summary>
    /// <remarks>
    /// <para>Nothing is left <c>null</c>: many SDK mappers read a null inner element, or a null nullable, as "no
    /// answer" or as a refusal. A string is <c>"stub"</c> so it is visibly fake on a screen; a bool is
    /// <c>true</c>, since most bool answers mean "succeeded"; an array is empty, so no device is invented.</para>
    /// <para>There is no depth cap, because a cap would null a live answer deeper down. A type that would contain
    /// itself, or one that carries <see cref="XmlIncludeAttribute"/> and has no semantic factory, is refused
    /// instead: reflection does not guess a subtype.</para>
    /// </remarks>
    internal static class ShapeDefaults
    {
        /// <summary>The fixed instant every shape-built date carries.</summary>
        internal static readonly DateTime Instant = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Values the generic rules would get wrong, keyed by generated type name because each SOAP namespace
        /// generates its own copy of these types. A zeroed date is one the SDK reads as unreadable, and the
        /// non-abstract resource-value base class is a value no SDK mapper reads.
        /// </summary>
        private static readonly Dictionary<string, Func<Type, object>> SemanticFactories = new(StringComparer.Ordinal)
        {
            ["WSDate"] = type => WithMembers(type, WireDates.Parts(new DateTimeOffset(Instant))),
            ["WSResourceValue"] = type => WithMembers(Sibling(type, "WSBooleanValue"), ("value", false)),
        };

        /// <summary>A value of <paramref name="type"/>, or the reason none can be built.</summary>
        internal static bool TryBuild(Type type, [NotNullWhen(true)] out object? value, [NotNullWhen(false)] out string? refusal)
        {
            ArgumentNullException.ThrowIfNull(type);
            refusal = null;
            value = Build(type, [], ref refusal);
            return value is not null;
        }

        private static object? Build(Type type, HashSet<Type> enclosing, ref string? refusal)
        {
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                return Build(underlying, enclosing, ref refusal);
            }
            if (SemanticFactories.TryGetValue(type.Name, out var factory) && GeneratedTypes.IsGenerated(type))
            {
                return factory(type);
            }
            if (type == typeof(string))
            {
                return "stub";
            }
            if (type == typeof(bool))
            {
                return true;
            }
            if (type.IsEnum)
            {
                // The first declared member, which reflection over the enum's fields returns in declaration order.
                return type.GetFields(BindingFlags.Public | BindingFlags.Static)[0].GetValue(null);
            }
            if (type == typeof(DateTime))
            {
                return Instant;
            }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                return Convert.ChangeType(0, type, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType()!, 0);
            }
            return BuildComplex(type, enclosing, ref refusal);
        }

        private static object? BuildComplex(Type type, HashSet<Type> enclosing, ref string? refusal)
        {
            if (type.IsDefined(typeof(XmlIncludeAttribute), inherit: false))
            {
                refusal = $"{type.FullName} names subtypes and has no semantic factory";
                return null;
            }
            if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) is null)
            {
                refusal = $"{type.FullName} has no public parameterless constructor";
                return null;
            }
            if (!enclosing.Add(type))
            {
                refusal = $"{type.FullName} would contain itself";
                return null;
            }

            object instance = Activator.CreateInstance(type)!;
            foreach ((Type memberType, Action<object, object?> set) in WritableMembers(type))
            {
                object? member = Build(memberType, enclosing, ref refusal);
                if (member is null)
                {
                    return null;
                }
                set(instance, member);
            }
            enclosing.Remove(type);
            return instance;
        }

        /// <summary>The public members a value is written through: the message wrappers carry fields, the data
        /// types properties.</summary>
        private static IEnumerable<(Type Type, Action<object, object?> Set)> WritableMembers(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !field.IsInitOnly)
                .Select(field => (field.FieldType, (Action<object, object?>)field.SetValue))
                .Concat(GeneratedTypes.SettableProperties(type)
                    .Select(property => (property.PropertyType, (Action<object, object?>)property.SetValue)));

        /// <summary>The type called <paramref name="name"/> in the same generated namespace as <paramref name="type"/>.</summary>
        private static Type Sibling(Type type, string name) =>
            type.Assembly.GetType($"{type.Namespace}.{name}")
            ?? throw new InvalidOperationException($"{type.Namespace} generates no {name} for the {type.Name} factory");

        /// <summary>A new <paramref name="type"/> with the named members set, every other member shape-built.</summary>
        private static object WithMembers(Type type, params (string Name, object Value)[] members)
        {
            string? refusal = null;
            object instance = BuildComplex(type, [], ref refusal)
                ?? throw new InvalidOperationException($"the {type.Name} factory cannot build its type: {refusal}");
            return SetMembers(instance, members);
        }

        /// <summary>Sets the named properties of <paramref name="instance"/>, which must have them all: a generated
        /// type that renamed one is a stub defect, never a member quietly left unset.</summary>
        internal static object SetMembers(object instance, params (string Name, object Value)[] members)
        {
            Type type = instance.GetType();
            foreach ((string name, object value) in members)
            {
                PropertyInfo property = type.GetProperty(name)
                    ?? throw new InvalidOperationException($"{type.FullName} has no member {name}");
                property.SetValue(instance, value);
            }
            return instance;
        }
    }
}
