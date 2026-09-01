using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Ihc;
using Ihc.App;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for <see cref="LabAppService.OperationItem"/>'s argument vector, using CsCheck.
    ///
    /// <para>Two laws, both stated over <b>any</b> operation whose parameters this fixture can generate
    /// values for, rather than over the single one-int operation the example tests used:</para>
    /// <list type="number">
    ///   <item><b>Round-trip</b> - <c>GetMethodArgumentsAsArray()</c> after
    ///   <c>SetMethodArgumentsFromArray(a)</c> is element-wise equal to <c>a</c>.</item>
    ///   <item><b>Defensive copy</b> - the returned array is never the internal one, successive calls
    ///   return distinct instances, and mutating a returned copy does not change the operation.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class LabAppServicePropertyTests
    {
        #pragma warning disable NUnit1032 // Fakes from FakeItEasy don't need disposal
        private IAuthenticationService fakeAuthService;
        private IResourceInteractionService fakeResourceService;
        private IConfigurationService fakeConfigService;
        #pragma warning restore NUnit1032
        private IhcSettings settings;

        [SetUp]
        public void Setup()
        {
            fakeAuthService = A.Fake<IAuthenticationService>();
            fakeResourceService = A.Fake<IResourceInteractionService>();
            fakeConfigService = A.Fake<IConfigurationService>();

            settings = new IhcSettings
            {
                Endpoint = "http://test",
                UserName = "testuser",
                Password = "testpass",
                Application = Application.administrator,
                LogSensitiveData = false,
                AsyncContinueOnCapturedContext = false
            };
        }

        // The scalar types this fixture can produce a type-correct value for. SetMethodArgument validates
        // by strict assignability (no numeric coercion), so an operation is only usable here when EVERY
        // parameter is one of these - anything else would fail for a reason unrelated to the laws (D03).
        private static readonly HashSet<Type> GeneratableTypes =
        [
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(bool), typeof(char), typeof(string),
        ];

        private static bool IsGeneratable(Type type) =>
            GeneratableTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);

        /// <summary>
        /// A deterministic, type-correct value for a parameter, derived from one generated seed. Null is
        /// produced for one seed in seven where the parameter accepts it, so the "null is allowed for
        /// reference types and Nullable&lt;T&gt;" branch is exercised too.
        /// </summary>
        private static object? ValueFor(Type type, int seed)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            bool acceptsNull = !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
            if (acceptsNull && seed % 7 == 0)
                return null;

            int magnitude = Math.Abs(seed % 1000);

            return Type.GetTypeCode(underlying) switch
            {
                TypeCode.Byte => (byte)(magnitude % 256),
                TypeCode.SByte => (sbyte)(magnitude % 128),
                TypeCode.Int16 => (short)(magnitude % 32000),
                TypeCode.UInt16 => (ushort)(magnitude % 32000),
                TypeCode.Int32 => seed,
                TypeCode.UInt32 => (uint)magnitude,
                TypeCode.Int64 => (long)seed,
                TypeCode.UInt64 => (ulong)magnitude,
                TypeCode.Single => magnitude / 2.0f,
                TypeCode.Double => magnitude / 2.0,
                TypeCode.Decimal => magnitude / 2.0m,
                TypeCode.Boolean => magnitude % 2 == 0,
                TypeCode.Char => (char)('a' + magnitude % 26),
                // Whitespace-bearing and non-ASCII strings included on purpose (see Discoveries [T013]).
                TypeCode.String => (magnitude % 4) switch
                {
                    0 => string.Empty,
                    1 => " padded ",
                    2 => "æøå " + magnitude,
                    _ => "v" + magnitude,
                },
                _ => throw new NotSupportedException($"No generator for {underlying.Name}"),
            };
        }

        private LabAppService ConfiguredService()
        {
            var labService = new LabAppService(null, null);
            labService.Configure(settings,
                new IIHCApiService[] { fakeAuthService, fakeResourceService, fakeConfigService });
            return labService;
        }

        /// <summary>
        /// Every operation across the configured services whose parameters are all generatable.
        /// </summary>
        private List<LabAppService.OperationItem> UsableOperations(LabAppService labService) =>
            labService.Services
                .SelectMany(service => service.OperationItems)
                .Where(operation => operation.OperationMetadata.Parameters.All(p => IsGeneratable(p.Type)))
                .ToList();

        [Test]
        public void SetArgumentsFromArray_ThenGetArgumentsAsArray_RoundTripsAnyArgumentVector()
        {
            var labService = ConfiguredService();
            var operations = UsableOperations(labService);

            // Guard against a vacuous property: if the filter matched nothing (or nothing with
            // parameters), the law below would hold trivially and prove nothing.
            Assert.That(operations, Is.Not.Empty, "no usable operations - the property would be vacuous");
            Assert.That(operations.Any(o => o.MethodParameterCount > 0), Is.True,
                "no usable operation takes parameters - the round-trip would be vacuous");

            (from index in Gen.Int[0, operations.Count - 1]
             from seeds in Gen.Int.Array[8, 8]
             select (Operation: operations[index], Seeds: seeds))
            .Sample(testCase =>
            {
                var parameters = testCase.Operation.OperationMetadata.Parameters;
                object[] arguments = parameters
                    .Select((p, i) => ValueFor(p.Type, testCase.Seeds[i % testCase.Seeds.Length])!)
                    .ToArray();

                testCase.Operation.SetMethodArgumentsFromArray(arguments);
                object?[] read = testCase.Operation.GetMethodArgumentsAsArray();

                Assert.That(read, Is.EqualTo(arguments), testCase.Operation.DisplayName);
            }, threads: 1);
        }

        /// <summary>
        /// The "changed" relation the event contract is defined against, mirroring the service's own
        /// rule: reference-equal counts as unchanged (so null-to-null does too), exactly one side being
        /// null counts as changed, otherwise <c>Equals</c> decides.
        /// </summary>
        private static bool AreValuesEqual(object? a, object? b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;
            return a.Equals(b);
        }

        /// <summary>
        /// One step of a generated edit sequence: either a single indexed set, or a whole-vector set.
        /// </summary>
        private readonly record struct EditStep(bool WholeVector, int Index, int Seed);

        /// <summary>
        /// Model-based law for <c>MethodArgumentChanged</c> over a SEQUENCE of edits: an event fires
        /// exactly when the stored value changes, in argument order, carrying the right index, old value
        /// and new value. The model is a plain <c>object[]</c> replaying the same edits - so the test
        /// states the contract independently of how the service tracks its arguments.
        ///
        /// <para>Replaces eight single-edit examples. A sequence is strictly stronger: it also catches a
        /// service that reports the correct old value only on the first edit, or that compares against
        /// the parameter default rather than the current value.</para>
        /// </summary>
        [Test]
        public void MethodArgumentChanged_FiresExactlyWhenAValueChanges_OverAnyEditSequence()
        {
            var labService = ConfiguredService();
            var operations = UsableOperations(labService)
                .Where(operation => operation.MethodParameterCount > 0)
                .ToList();

            Assert.That(operations, Is.Not.Empty, "no usable parameterized operations - property would be vacuous");

            (from index in Gen.Int[0, operations.Count - 1]
             from steps in (from wholeVector in Gen.Bool
                            from stepIndex in Gen.Int[0, 7]
                            from seed in Gen.Int[0, 40]
                            select new EditStep(wholeVector, stepIndex, seed)).Array[1, 6]
             select (Operation: operations[index], Steps: steps))
            .Sample(testCase =>
            {
                var operation = testCase.Operation;
                var parameters = operation.OperationMetadata.Parameters;

                // Start from a known state so the model and the service agree before the first edit.
                operation.ResetMethodArguments();
                object?[] model = operation.GetMethodArgumentsAsArray();

                var actual = new List<(int Index, object? Old, object? New)>();
                void Record(object? sender, LabAppService.MethodArgumentChangedEventArgs e)
                    => actual.Add((e.Index, e.OldValue, e.NewValue));

                operation.MethodArgumentChanged += Record;
                try
                {
                    var expected = new List<(int Index, object? Old, object? New)>();

                    foreach (var step in testCase.Steps)
                    {
                        if (step.WholeVector)
                        {
                            object[] vector = parameters
                                .Select((p, i) => ValueFor(p.Type, step.Seed + i)!)
                                .ToArray();

                            for (int i = 0; i < vector.Length; i++)
                            {
                                if (!AreValuesEqual(model[i], vector[i]))
                                    expected.Add((i, model[i], vector[i]));
                                model[i] = vector[i];
                            }

                            operation.SetMethodArgumentsFromArray(vector);
                        }
                        else
                        {
                            int target = step.Index % parameters.Length;
                            object? value = ValueFor(parameters[target].Type, step.Seed);

                            if (!AreValuesEqual(model[target], value))
                                expected.Add((target, model[target], value));
                            model[target] = value;

                            operation.SetMethodArgument(target, value);
                        }
                    }

                    Assert.That(actual, Is.EqualTo(expected), operation.DisplayName + ": event sequence");
                    Assert.That(operation.GetMethodArgumentsAsArray(), Is.EqualTo(model),
                        operation.DisplayName + ": final state");
                }
                finally
                {
                    operation.MethodArgumentChanged -= Record;
                }
            }, threads: 1);
        }

        [Test]
        public void GetArgumentsAsArray_ReturnsAnIndependentCopy_ForAnyOperation()
        {
            var labService = ConfiguredService();

            // Zero-parameter operations are EXCLUDED on purpose: GetMethodArgumentsAsArray returns
            // Array.Empty<object>() for them, and that is a shared singleton - so "successive calls
            // return distinct instances" is genuinely false there. The empty case is pinned by the
            // kept example GetArgumentsAsArray_ForNoParameterOperation_ReturnsEmptyArray instead.
            var operations = UsableOperations(labService)
                .Where(operation => operation.MethodParameterCount > 0)
                .ToList();

            Assert.That(operations, Is.Not.Empty, "no usable parameterized operations - property would be vacuous");

            (from index in Gen.Int[0, operations.Count - 1]
             from seeds in Gen.Int.Array[8, 8]
             select (Operation: operations[index], Seeds: seeds))
            .Sample(testCase =>
            {
                var parameters = testCase.Operation.OperationMetadata.Parameters;
                object[] arguments = parameters
                    .Select((p, i) => ValueFor(p.Type, testCase.Seeds[i % testCase.Seeds.Length])!)
                    .ToArray();
                testCase.Operation.SetMethodArgumentsFromArray(arguments);

                object?[] first = testCase.Operation.GetMethodArgumentsAsArray();
                object?[] second = testCase.Operation.GetMethodArgumentsAsArray();
                string what = testCase.Operation.DisplayName;

                Assert.That(first, Is.Not.SameAs(testCase.Operation.MethodArguments), what + ": not the internal array");
                Assert.That(first, Is.Not.SameAs(second), what + ": successive calls are distinct instances");
                Assert.That(first, Is.EqualTo(second), what + ": successive calls have equal contents");

                // Mutating the copy must not reach the operation.
                object? before = testCase.Operation.MethodArguments[0];
                first[0] = new object();
                Assert.That(testCase.Operation.MethodArguments[0], Is.EqualTo(before), what + ": copy is not aliased");
            }, threads: 1);
        }
    }
}
