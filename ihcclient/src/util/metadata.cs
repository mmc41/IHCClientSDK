using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ihc {
    /// <summary>
    /// Type of service operation (method) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    public enum ServiceOperationKind { AsyncFunction, AsyncEnumerable };

    /// <summary>
    /// High level metadata about a service operation (method) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    /// <param name="Name">The name of the method</param>
    /// <param name="ReturnType">The return type unwrapped from Task or IAsyncEnumerable wrappers (refer to OperationKind)</param>
    /// <param name="ParameterTypes">Method parameter types</param>
    /// <param name="OperationKind">The type of operation</param>
    /// <param name="OperationDetails">The underlying MethodInfo describing the operation in details</param>
    /// <param name="Description">The XML documentation summary for this method</param>
    public record SeviceOperationMetadata(string Name, Type ReturnType, Type[] ParameterTypes, ServiceOperationKind OperationKind, MethodInfo OperationDetails, string Description)
    {
        public string Name { get; init; } = Name;
        public Type ReturnType { get; init; } = ReturnType;
        public Type[] ParameterTypes { get; init; } = ParameterTypes;
        public ServiceOperationKind Kind { get; init; } = OperationKind;
        public MethodInfo MethodInfo { get; init; } = OperationDetails;
        public string Description { get; init; } = Description;

        public override string ToString()
        {
            var parameters = string.Join(", ", ParameterTypes.Select(t => t.Name));
            return $"{ReturnType.Name} {Name}({parameters}) [{Kind}]";
        }
    }

    /// <summary>
    /// Produce high level metadata about service operations (methods) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    internal static class ServiceMetadata
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<SeviceOperationMetadata>> _cache = new();
        private static XDocument _xmlDoc;
        private static readonly object _xmlLock = new();

        public static IReadOnlyList<SeviceOperationMetadata> GetOperations(IIHCService service)
        {
            var serviceType = service.GetType();

            return _cache.GetOrAdd(serviceType, type =>
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                var operations = new List<SeviceOperationMetadata>();

                foreach (var method in methods)
                {
                    // Skip Dispose and DisposeAsync methods
                    if (method.Name == "Dispose" || method.Name == "DisposeAsync")
                        continue;

                    // Skip property getters/setters
                    if (method.IsSpecialName)
                        continue;

                    operations.Add(CreateOperationInfo(method));
                }

                return operations.AsReadOnly();
            });
        }

        private static SeviceOperationMetadata CreateOperationInfo(System.Reflection.MethodInfo method)
        {
            var operationType = DetermineOperationKind(method.ReturnType);
            var returnType = UnwrapAsyncReturnType(method.ReturnType);
            var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            var description = GetMethodDescription(method);

            return new SeviceOperationMetadata(method.Name, returnType, parameterTypes, operationType, method, description);
        }

        private static ServiceOperationKind DetermineOperationKind(Type returnType)
        {
            // Check if it's an IAsyncEnumerable<T>
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IAsyncEnumerable<>))
            {
                return ServiceOperationKind.AsyncEnumerable;
            }

            // Check if it's a Task<IAsyncEnumerable<T>>
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = returnType.GetGenericArguments()[0];
                if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IAsyncEnumerable<>))
                {
                    return ServiceOperationKind.AsyncEnumerable;
                }
            }

            // Default to AsyncFunction for Task or Task<T>
            return ServiceOperationKind.AsyncFunction;
        }

        private static Type UnwrapAsyncReturnType(Type returnType)
        {
            // If the return type is Task or Task<T>, unwrap to void or T
            if (returnType == typeof(Task))
            {
                return typeof(void);
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = returnType.GetGenericArguments()[0];

                // If Task<IAsyncEnumerable<T>>, unwrap to T
                if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IAsyncEnumerable<>))
                {
                    return innerType.GetGenericArguments()[0];
                }

                return innerType;
            }

            // If the return type is IAsyncEnumerable<T>, unwrap to T
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IAsyncEnumerable<>))
            {
                return returnType.GetGenericArguments()[0];
            }

            return returnType;
        }

        /// <summary>
        /// Load the XML documentation file generated for the current assembly.
        /// </summary>
        private static XDocument LoadXmlDocumentation()
        {
            lock (_xmlLock)
            {
                if (_xmlDoc != null)
                    return _xmlDoc;

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var assemblyLocation = assembly.Location;

                    if (string.IsNullOrEmpty(assemblyLocation))
                        return null;

                    var xmlPath = System.IO.Path.ChangeExtension(assemblyLocation, ".xml");

                    if (!System.IO.File.Exists(xmlPath))
                        return null;

                    _xmlDoc = XDocument.Load(xmlPath);
                    return _xmlDoc;
                }
                catch
                {
                    // If we can't load the XML, just return null
                    return null;
                }
            }
        }

        private static string GetMethodDescription(MethodInfo method)
        {
            var xmlDoc = LoadXmlDocumentation();
            if (xmlDoc == null)
                return string.Empty;

            // Try to find documentation from the declaring type first
            var description = TryGetDescriptionForType(xmlDoc, method, method.DeclaringType);
            if (description != null)
                return description;

            // If not found, try all implemented interfaces
            if (method.DeclaringType != null)
            {
                var interfaces = method.DeclaringType.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    // Get the interface map to find the corresponding interface method
                    var interfaceMap = method.DeclaringType.GetInterfaceMap(iface);
                    var index = Array.IndexOf(interfaceMap.TargetMethods, method);

                    if (index >= 0)
                    {
                        var interfaceMethod = interfaceMap.InterfaceMethods[index];
                        description = TryGetDescriptionForType(xmlDoc, interfaceMethod, iface);
                        if (description != null)
                            return description;
                    }
                }
            }

            return string.Empty;
        }

        private static string TryGetDescriptionForType(XDocument xmlDoc, MethodInfo method, Type type)
        {
            if (type == null)
                return null;

            var sb = new StringBuilder();
            sb.Append("M:");
            sb.Append(type.FullName);
            sb.Append('.');
            sb.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                sb.Append('(');
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    var paramType = parameters[i].ParameterType;
                    sb.Append(GetXmlTypeName(paramType));
                }
                sb.Append(')');
            }

            var memberName = sb.ToString();

            // Find the member element in the XML
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

            if (memberElement == null)
                return null;

            // Try to get the summary element first
            var summaryElement = memberElement.Element("summary");
            if (summaryElement != null)
                return summaryElement.Value.Trim();

            // Fall back to the member element's direct text content
            return memberElement.Value.Trim();
        }

        private static string GetXmlTypeName(Type type)
        {
            // Handle generic types
            if (type.IsGenericType)
            {
                var genericTypeName = type.GetGenericTypeDefinition().FullName;
                // Remove the `1, `2, etc. from generic type names
                var tickIndex = genericTypeName!.IndexOf('`');
                if (tickIndex > 0)
                    genericTypeName = genericTypeName.Substring(0, tickIndex);

                var genericArgs = type.GetGenericArguments();
                var sb = new StringBuilder();
                sb.Append(genericTypeName);
                sb.Append('{');
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(GetXmlTypeName(genericArgs[i]));
                }
                sb.Append('}');
                return sb.ToString();
            }

            // Handle arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return GetXmlTypeName(elementType!) + "[]";
            }

            // Regular types
            return type.FullName ?? type.Name;
        }
    }

}