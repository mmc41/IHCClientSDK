using NUnit.Framework;
using System;
using System.Linq;
using Ihc;
using FakeItEasy;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit tests for ReflectionUtil that verify it works correctly with FakeItEasy mocked services.
    /// </summary>
    [TestFixture]
    public class ReflectionUtilMockedTest
    {
        [Test]
        public void GetServiceType_FakeAuthenticationService_ReturnsInterfaceType()
        {
            // Arrange
            IAuthenticationService service = A.Fake<IAuthenticationService>();
            string description = "FakeItEasy IAuthenticationService";

            // Act
            Type serviceType = ReflectionUtil.GetServiceType(service);

            // Assert
            Assert.That(serviceType, Is.Not.Null, $"Service type should not be null for {description}");
            Assert.That(serviceType.IsInterface, Is.True, $"Service type should be an interface for {description}");
            Assert.That(typeof(IIHCApiService).IsAssignableFrom(serviceType), Is.True,
                $"Service type should be assignable to IIHCService for {description}");

            // Verify it's not a proxy type (e.g., "ObjectProxy" or similar)
            Assert.That(serviceType.Name, Does.Not.Contain("Proxy"), $"Service type should not contain 'Proxy' in name for {description}");
            Assert.That(serviceType.Name, Does.Not.Contain("Castle"), $"Service type should not contain 'Castle' in name for {description}");

            // Verify the interface name follows expected pattern (starts with 'I')
            Assert.That(serviceType.Name, Does.StartWith("I"), $"Interface name should start with 'I' for {description}");
        }

        [Test]
        public void GetMethods_FakeService_ReturnsInterfaceMethods()
        {
            // Arrange
            IAuthenticationService service = A.Fake<IAuthenticationService>();
            string description = "FakeItEasy IAuthenticationService";

            // Act
            MethodInfo[] methods = ReflectionUtil.GetMethods(service);

            // Assert
            Assert.That(methods, Is.Not.Null, $"Methods array should not be null for {description}");
            Assert.That(methods.Length, Is.GreaterThan(0), $"Should have at least one method for {description}");

            // Verify methods are not internal proxy methods
            foreach (var method in methods)
            {
                // Check method names don't contain proxy-specific patterns
                Assert.That(method.Name, Does.Not.Contain("__"), $"Method name should not contain '__' for {description}");
                Assert.That(method.Name, Does.Not.Contain("Proxy"), $"Method name should not contain 'Proxy' for {description}");
            }
        }
    }
}
