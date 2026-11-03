using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ihc.Stubbing;

namespace Ihc.Tests
{
    /// <summary>
    /// A valid argument for any parameter of a public SDK service method, for tests that call every operation.
    /// A complex parameter takes the value a default <see cref="ControllerStub"/> holds of its type, which also
    /// proves that default passes the setter's validation, except where the default is one the validation
    /// refuses (<see cref="RefusedDefaults"/>); a type the stub holds no value of takes an explicit literal that
    /// passes it. No complex parameter is given null: the real setters refuse null before they send.
    /// </summary>
    internal static class SdkArguments
    {
        /// <summary>Only read, never registered, so it answers nothing; it is where the typed defaults live.</summary>
        private static readonly ControllerStub Defaults = new();

        /// <summary>
        /// For this type the stub's default is one the SDK's own validation refuses to send back: a disabled wireless
        /// setup whose addresses are empty where it requires them. That default is a stand-in, not a captured answer,
        /// since no capture holds the wireless settings. The argument keeps it and changes only what the validation
        /// refuses.
        /// </summary>
        internal static readonly IReadOnlyDictionary<Type, Func<object, object>> RefusedDefaults = new Dictionary<Type, Func<object, object>>
        {
            [typeof(WLanSettings)] = held => (WLanSettings)held with { IpAddress = "192.168.77.104", Netmask = "255.255.255.0", Gateway = "192.168.77.1" },
        };

        internal static object For(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (Simple(type) is { } simple)
            {
                return simple;
            }
            if (StubDefault(type) is { } typed)
            {
                return RefusedDefaults.TryGetValue(type, out Func<object, object>? accepted) ? accepted(typed) : typed;
            }
            if (type.IsEnum)
            {
                return Enum.GetValues(type).GetValue(0)!;
            }
            if (type == typeof(ResourceValue))
            {
                return SampleResourceValue();
            }
            if (type == typeof(SceneProject))
            {
                return new SceneProject("stub.icw", [1, 2, 3, 4]);
            }
            if (type == typeof(ProjectSegment))
            {
                return new ProjectSegment { Data = [1, 2, 3, 4], Filename = "stub.vis" };
            }
            if (type.IsAssignableFrom(typeof(List<int>)))
            {
                return new List<int> { 1, 2, 3 };
            }
            if (type.IsAssignableFrom(typeof(List<ResourceValue>)))
            {
                return new List<ResourceValue> { SampleResourceValue() };
            }
            throw new NotSupportedException(
                $"No argument for {type}: give the stub a typed default of it, or add an explicit literal here.");
        }

        private static object? Simple(Type type) => type switch
        {
            _ when type == typeof(string) => "stub",
            _ when type == typeof(bool) => true,
            _ when type == typeof(int) => 1,
            _ when type == typeof(long) => 1L,
            _ when type == typeof(short) => (short)1,
            _ when type == typeof(byte) => (byte)1,
            _ when type == typeof(sbyte) => (sbyte)1,
            _ when type == typeof(uint) => 1u,
            _ when type == typeof(ulong) => 1ul,
            _ when type == typeof(ushort) => (ushort)1,
            _ when type == typeof(float) => 1f,
            _ when type == typeof(double) => 1d,
            _ when type == typeof(decimal) => 1m,
            _ when type == typeof(DateTime) => DateTime.Now,
            _ when type == typeof(DateTimeOffset) => DateTimeOffset.Now,
            _ when type == typeof(TimeSpan) => TimeSpan.Zero,
            _ when type == typeof(CancellationToken) => default(CancellationToken),
            _ => null,
        };

        /// <summary>
        /// The one value of <paramref name="type"/> the default stub's scenario groups hold. Two properties of the
        /// same type would make the choice depend on declaration order, which reflection does not promise, so that
        /// fails here and the type takes an explicit literal instead.
        /// </summary>
        private static object? StubDefault(Type type)
        {
            object[] held =
            [
                .. typeof(ControllerStub).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(group => group.GetValue(Defaults))
                    .Where(group => group is not null and not StubJournal)
                    .SelectMany(group => group!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(property => property.PropertyType == type && property.GetIndexParameters().Length == 0)
                        .Select(property => property.GetValue(group)))
                    .OfType<object>(),
            ];
            return held.Length switch
            {
                0 => null,
                1 => held[0],
                _ => throw new InvalidOperationException($"The default stub holds {held.Length} values of {type}; give it an explicit literal."),
            };
        }

        private static ResourceValue SampleResourceValue() => new()
        {
            ResourceID = 1,
            Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 1 },
        };
    }
}
