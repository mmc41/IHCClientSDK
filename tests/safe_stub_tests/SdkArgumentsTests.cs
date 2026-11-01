using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The shared argument builder gives every parameter of every public SDK service method a value the real
    /// setter accepts: never null for a complex parameter, the default stub's value where it holds one, and a value
    /// that passes the data-annotation validation each setter runs before it sends.
    /// </summary>
    [TestFixture]
    public class SdkArgumentsTests
    {
        /// <summary>Every parameter type of every public service interface, read from the SDK rather than listed,
        /// so a parameter type added later is visited too.</summary>
        public static IEnumerable<TestCaseData> EveryParameterType() =>
            typeof(IIHCApiService).Assembly.GetExportedTypes()
                .Where(type => type.IsInterface && type != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(type))
                .SelectMany(service => service.GetMethods())
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => new TestCaseData(type).SetArgDisplayNames(type.Name));

        [TestCaseSource(nameof(EveryParameterType))]
        public void EveryParameter_GetsAValue_ThatPassesTheSettersValidation(Type type)
        {
            object argument = SdkArguments.For(type);

            Assert.That(argument, Is.InstanceOf(type));
            var failures = new List<ValidationResult>();
            Assert.That(Validator.TryValidateObject(argument, new ValidationContext(argument), failures, validateAllProperties: true), Is.True,
                string.Join("; ", failures.Select(failure => failure.ErrorMessage)));
        }

        public static IEnumerable<TestCaseData> ValuesTheStubHolds()
        {
            yield return Held(typeof(NetworkSettings), stub => stub.Configuration.NetworkSettings);
            yield return Held(typeof(DNSServers), stub => stub.Configuration.DNSServers);
            yield return Held(typeof(SMTPSettings), stub => stub.Configuration.SMTPSettings);
            yield return Held(typeof(EmailControlSettings), stub => stub.Configuration.EmailControlSettings);
            yield return Held(typeof(WebAccessControl), stub => stub.Configuration.WebAccessControl);
            yield return Held(typeof(SmsModemSettings), stub => stub.SmsModem.Settings);
            yield return Held(typeof(IhcUser), stub => stub.Authentication.User);
            yield return Held(typeof(ProjectFile), stub => stub.Controller.Project);
            yield return Held(typeof(ControllerState), stub => stub.Controller.State);
        }

        /// <summary>A parameter whose type the default stub holds a value of gets that value back, so the setter's
        /// validation passing over it is also proof that the stub's default is one a client could send.</summary>
        [TestCaseSource(nameof(ValuesTheStubHolds))]
        public void AParameterTheStubHoldsAValueOf_GetsThatValue(Type type, Func<ControllerStub, object?> held)
        {
            object? value = held(new ControllerStub());

            Assert.That(value, Is.Not.Null, "the default stub holds a value of this type");
            Assert.That(SdkArguments.For(type), Is.EqualTo(value));
        }

        /// <summary>
        /// The captured controller holds a time-server name longer than the SDK allows and a disabled wireless setup
        /// without the addresses it requires, so neither default can be sent back as it is. When the SDK's validation
        /// accepts one, its substitute here is no longer needed and this fails to say so.
        /// </summary>
        [Test]
        public void TheDefaultsTheValidationRefuses_AreExactlyTheSubstitutedOnes()
        {
            var stub = new ControllerStub();
            object[] refused =
            [
                stub.Time.Settings,
                stub.Configuration.WLanSettings ?? throw new InvalidOperationException("the default stub holds a wireless setup"),
            ];

            Assert.That(refused.Select(value => value.GetType()), Is.EquivalentTo(SdkArguments.RefusedDefaults.Keys));
            Assert.That(refused.Where(value => !Validator.TryValidateObject(value, new ValidationContext(value), [], validateAllProperties: true)),
                Is.EqualTo(refused), "each substituted default is still refused");
        }

        private static TestCaseData Held(Type type, Func<ControllerStub, object?> held) =>
            new TestCaseData(type, held).SetArgDisplayNames(type.Name);
    }
}
