using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// What it means for an assembly to open a network connection of its own, judged for the assemblies that may
    /// open none: the controller stub (L7), and the executables with the bootstrap code they share (L12). One
    /// predicate for both, so the stub and an executable cannot come to be held to different ideas of "outbound".
    /// </summary>
    /// <remarks>
    /// Stricter than the controller-reach guard's live-transport rule, on purpose. A test suite legitimately builds
    /// an <c>HttpClient</c> over a handler it declares, so that rule judges how far a transport reaches. Neither
    /// population here has any reason to build or hold a client, a handler, a socket or a WCF channel at all, so
    /// every edge into one counts, whoever declared the type: a call, a construction, a field, a signature or a
    /// base type.
    /// </remarks>
    internal static class OutboundConnectionScan
    {
        /// <summary>Namespaces any edge into which is an outbound network capability.</summary>
        private static readonly string[] OutboundNamespaces =
        [
            "System.Net.Sockets",
            "System.Net.WebSockets",
            "System.Net.Quic",
            "System.Net.Mail",
            "System.Net.NetworkInformation",
            "System.ServiceModel",
        ];

        /// <summary>
        /// Types outside those namespaces that send, or are the chain a sender is built over. The HTTP types are
        /// listed by their bases, so <c>HttpClient</c> and every handler count, and a subclass declared to look
        /// harmless counts as what it derives from.
        /// </summary>
        private static readonly string[] OutboundTypes =
        [
            "System.Net.WebRequest",
            "System.Net.WebClient",
            "System.Net.Http.HttpMessageInvoker",
            "System.Net.Http.HttpMessageHandler",
        ];

        /// <summary>
        /// Every outbound capability <paramref name="assembly"/>'s own code reaches: any call, construction, field,
        /// signature or base-type edge into an outbound namespace or type.
        /// </summary>
        internal static IEnumerable<(ContainmentSite Site, string Target)> Reaches(Assembly assembly)
        {
            foreach (MethodBase member in AuthoredMembers.Of(assembly))
            {
                ContainmentSite site = ContainmentSite.For(member);
                foreach (IlBody.Instruction instruction in IlBody.Instructions(member))
                {
                    if (instruction.Called?.DeclaringType is { } declaring && IsOutbound(declaring))
                    {
                        yield return (site, declaring.FullName ?? declaring.Name);
                    }
                }
                foreach (Type signatureType in SignatureTypes(member).Where(IsOutbound))
                {
                    yield return (site, signatureType.FullName ?? signatureType.Name);
                }
            }

            foreach (Type type in assembly.GetTypes())
            {
                var site = new ContainmentSite(ArchRuleHelpers.OutermostTypeName(type.FullName!), "(declaration)");
                IEnumerable<Type> declared = type.GetFields(BindingFlags.Instance | BindingFlags.Static
                                                            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(field => field.FieldType)
                    .Concat(type.GetInterfaces())
                    .Concat(type.BaseType is { } baseType ? [baseType] : []);
                foreach (Type edge in declared.Where(IsOutbound))
                {
                    yield return (site, edge.FullName ?? edge.Name);
                }
            }
        }

        private static IEnumerable<Type> SignatureTypes(MethodBase member) =>
            member.GetParameters().Select(parameter => parameter.ParameterType)
                .Concat(member is MethodInfo method ? [method.ReturnType] : []);

        /// <summary>True for a type in an outbound namespace, one of the outbound types, a type deriving from
        /// either, or a generic, array or by-ref type built over such a type.</summary>
        private static bool IsOutbound(Type? type)
        {
            if (type is null)
            {
                return false;
            }
            if (type.HasElementType)
            {
                return IsOutbound(type.GetElementType());
            }
            if (type.IsGenericType && type.GetGenericArguments().Any(IsOutbound))
            {
                return true;
            }
            for (Type? at = type; at is not null; at = at.BaseType)
            {
                if (at.Namespace is { } ns
                    && OutboundNamespaces.Any(root => ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal)))
                {
                    return true;
                }
                if (at.FullName is { } name && OutboundTypes.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
