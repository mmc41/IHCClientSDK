using System;
using System.Reflection;

using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// What <see cref="ReflectionUtil"/> owes for any service instance: it reports the service's INTERFACE and the
    /// interface's methods, never whatever concrete or proxy type the instance happens to be.
    /// </summary>
    /// <remarks>
    /// One contract, asserted here once and run by two fixtures over two implementations — a real service in the
    /// controller-backed suite and a FakeItEasy proxy in the unit suite. A dynamic proxy is exactly the shape that
    /// would break these assertions, so neither implementation passing says anything about the other, and the
    /// assertions themselves must not drift apart.
    /// </remarks>
    internal static class ReflectionUtilContract
    {
        /// <summary>The resolved service type is the interface, not the implementation or a proxy class.</summary>
        /// <param name="service">The service instance to resolve.</param>
        /// <param name="description">Names the implementation under test, so a failure says which one failed.</param>
        public static void AssertServiceTypeIsTheInterface(IIHCApiService service, string description)
        {
            Type serviceType = ReflectionUtil.GetServiceType(service);

            Assert.That(serviceType, Is.Not.Null, $"Service type should not be null for {description}");
            Assert.That(serviceType.IsInterface, Is.True, $"Service type should be an interface for {description}");
            Assert.That(typeof(IIHCApiService).IsAssignableFrom(serviceType), Is.True,
                $"Service type should be assignable to IIHCService for {description}");

            Assert.That(serviceType.Name, Does.Not.Contain("Proxy"), $"Service type should not contain 'Proxy' in name for {description}");
            Assert.That(serviceType.Name, Does.Not.Contain("Castle"), $"Service type should not contain 'Castle' in name for {description}");

            Assert.That(serviceType.Name, Does.StartWith("I"), $"Interface name should start with 'I' for {description}");
        }

        /// <summary>The reported methods are the interface's own, with no proxy plumbing among them.</summary>
        /// <param name="service">The service instance to resolve.</param>
        /// <param name="description">Names the implementation under test, so a failure says which one failed.</param>
        public static void AssertMethodsAreTheInterfaceMethods(IIHCApiService service, string description)
        {
            MethodInfo[] methods = ReflectionUtil.GetMethods(service);

            Assert.That(methods, Is.Not.Null, $"Methods array should not be null for {description}");
            Assert.That(methods.Length, Is.GreaterThan(0), $"Should have at least one method for {description}");

            foreach (var method in methods)
            {
                Assert.That(method.Name, Does.Not.Contain("__"), $"Method name should not contain '__' for {description}");
                Assert.That(method.Name, Does.Not.Contain("Proxy"), $"Method name should not contain 'Proxy' for {description}");
            }
        }
    }
}
