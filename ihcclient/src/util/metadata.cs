using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// Type of service operation (method) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    public enum ServiceOperationKind { AsyncFunction, AsyncEnumerable };

    /// <summary>
    /// High level metadata about a field/parameter used in a high level IHC service operation type. For use by test and documentation tools.
    /// </summary>
    // TODO: Add parameter types.
    public record FieldMetaData(string name, Type type, FieldMetaData[] subtypes, string description)
    {
        public string Name { get; init; } = name;
        public Type Type { get; init; } = type;
        public string Description { get; init; } = description;
        public FieldMetaData[] SubTypes { get; init; } = subtypes;
        public bool IsSimple { get { return type.IsPrimitive || type == typeof(String); } }
        public bool IsArray { get { return type.IsArray; } }

        public override string ToString()
        {
            var subTypes = string.Join(", ", SubTypes.Select(p => p.Type.Name));
            return $"FieldMetaData(Name={Name}, Type={Type.Name}, SubTypes={subTypes})";
        }
    }

    /// <summary>
    /// High level metadata about a service operation (method) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    /// <param name="service">IHC service</param>
    /// <param name="Name">The name of the method</param>
    /// <param name="ReturnType">The return type unwrapped from Task or IAsyncEnumerable wrappers (refer to OperationKind)</param>
    /// <param name="Parameters">Method parameter metadata</param>
    /// <param name="OperationKind">The type of operation</param>
    /// <param name="OperationDetails">The underlying MethodInfo describing the operation in details</param>
    /// <param name="Description">The XML documentation summary for this method</param>
    public class ServiceOperationMetadata(IIHCService service, string Name, Type ReturnType, FieldMetaData[] Parameters, ServiceOperationKind OperationKind, MethodInfo OperationDetails, string Description)
    {
        public string Name { get; init; } = Name;
        public Type ReturnType { get; init; } = ReturnType;
        public FieldMetaData[] Parameters { get; init; } = Parameters;
        public ServiceOperationKind Kind { get; init; } = OperationKind;
        public MethodInfo MethodInfo { get; init; } = OperationDetails;
        public string Description { get; init; } = Description;
        public IIHCService service { get; init; } = service;

        public override string ToString()
        {
            var parameters = string.Join(", ", Parameters.Select(p => p.Type.Name));
            return $"{ReturnType.Name} {Name}({parameters}) [{Kind}]";
        }

        /// <summary>
        /// Invoke the operation on the specified service instance with the provided arguments.
        /// Returns a Task, Task&lt;T&gt;, or IAsyncEnumerable&lt;T&gt; depending on the operation kind.
        /// The caller is responsible for awaiting/enumerating the returned value.
        /// </summary>
        /// <param name="arguments">The arguments to pass to the method</param>
        /// <returns>Task, Task&lt;T&gt;, or IAsyncEnumerable&lt;T&gt; representing the async operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when serviceInstance is null</exception>
        /// <exception cref="ArgumentException">Thrown when arguments don't match expected parameter types</exception>
        public object Invoke(object[] arguments)
        {
            // Validate arguments length matches expected parameters
            if (arguments == null && Parameters.Length > 0)
                throw new ArgumentException($"Method {Name} expects {Parameters.Length} arguments but null was provided");

            if (arguments != null && arguments.Length != Parameters.Length)
                throw new ArgumentException($"Method {Name} expects {Parameters.Length} arguments but {arguments.Length} were provided");

            // Use reflection to invoke the method on the service instance
            // This returns Task, Task<T>, or IAsyncEnumerable<T> depending on the operation
            var result = MethodInfo.Invoke(service, arguments ?? Array.Empty<object>());

            return result;
        }
    }

    /// <summary>
    /// Produce high level metadata about service operations (methods) on a high level IHC service. For use by test and documentation tools.
    /// </summary>
    public static class ServiceMetadata
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<ServiceOperationMetadata>> _cache = new();
        private static XDocument _xmlDoc;
        private static readonly object _xmlLock = new();

        /// <summary>
        /// Get metadata about the operations supported by this service.
        /// For use by test and documentation tools. Not for normal application code.
        /// </summary>
        /// <returns>List of metadata for service operations</returns>
        public static IReadOnlyList<ServiceOperationMetadata> GetOperations(IIHCService service)
        {
            using Activity activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            
            var serviceType = service.GetType();
            activity?.SetTag("service.name", typeof(ServiceMetadata).Name);
            activity?.SetParameters((nameof(service), service.GetType().Name));
            activity?.SetTag("input.cachedResult", true); // Assume cached by default

            var retv = _cache.GetOrAdd(serviceType, type =>
            {
                activity?.SetTag("cachedResult", false); // Override if not cached.

                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                var operations = new List<ServiceOperationMetadata>();

                foreach (var method in methods)
                {
                    // Skip property getters/setters
                    if (method.IsSpecialName)
                        continue;

                    // Skip methods inherited from System.Object
                    if (method.DeclaringType == typeof(object))
                        continue;

                    // Skip methods from ICookieHandlerService, IDisposable, and IAsyncDisposable
                    if (IsMethodFromExcludedInterface(method))
                        continue;

                    operations.Add(CreateOperationInfo(service, method));
                }

                return operations.AsReadOnly();
            });

            activity?.SetReturnValue(retv);

            return retv;
        }

        private static bool IsMethodFromExcludedInterface(MethodInfo method)
        {
            if (method.DeclaringType == null)
                return false;

            var excludedInterfaces = new[] { typeof(IDisposable), typeof(IAsyncDisposable), typeof(ICookieHandlerService) };

            // Check if method is declared directly on excluded interfaces
            if (excludedInterfaces.Contains(method.DeclaringType))
                return true;

            // Check if the declaring type implements any excluded interfaces and this method implements one of their methods
            var declaringType = method.DeclaringType;

            foreach (var excludedInterface in excludedInterfaces)
            {
                if (excludedInterface.IsAssignableFrom(declaringType))
                {
                    // Get the interface map to see if this method implements an interface method
                    var interfaceMap = declaringType.GetInterfaceMap(excludedInterface);
                    if (interfaceMap.TargetMethods.Contains(method))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ServiceOperationMetadata CreateOperationInfo(IIHCService service, System.Reflection.MethodInfo method)
        {
            var operationType = DetermineOperationKind(method.ReturnType);
            var returnType = UnwrapAsyncReturnType(method.ReturnType);
            var parameters = method.GetParameters()
                .Select(p => new FieldMetaData(
                    name: p.Name ?? string.Empty,
                    type: p.ParameterType,
                    subtypes: CreateSubTypes(p.ParameterType),
                    description: GetParameterDescription(method, p.Name ?? string.Empty)))
                .ToArray();
            var description = GetMethodDescription(method);

            return new ServiceOperationMetadata(service, method.Name, returnType, parameters, operationType, method, description);
        }

        private static FieldMetaData[] CreateSubTypes(Type parameterType)
        {
            // For arrays, return element type with blank name
            if (parameterType.IsArray)
            {
                var elementType = parameterType.GetElementType();
                if (elementType != null)
                {
                    return new[] { new FieldMetaData(name: string.Empty, type: elementType, subtypes: [], description: "") }; // TODO: Read description from XML
                }
                return Array.Empty<FieldMetaData>();
            }

            // For primitives and strings, return empty array (no subtypes)
            if (parameterType.IsPrimitive || parameterType == typeof(string))
            {
                return Array.Empty<FieldMetaData>();
            }

            // For classes/records, return properties
            if (parameterType.IsClass || parameterType.IsValueType)
            {
                var properties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                return properties
                    .Select(p => new FieldMetaData(name: p.Name, type: p.PropertyType, subtypes: [], description: "")) // TODO: Read description from XML
                    .ToArray();
            }

            // Default: return empty array
            return Array.Empty<FieldMetaData>();
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

        private static string GetParameterDescription(MethodInfo method, string parameterName)
        {
            var xmlDoc = LoadXmlDocumentation();
            if (xmlDoc == null)
                return string.Empty;

            // Try to find documentation from the declaring type first
            var description = TryGetParameterDescriptionForType(xmlDoc, method, method.DeclaringType, parameterName);
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
                        description = TryGetParameterDescriptionForType(xmlDoc, interfaceMethod, iface, parameterName);
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

        private static string TryGetParameterDescriptionForType(XDocument xmlDoc, MethodInfo method, Type type, string parameterName)
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

            // Find the param element with matching name attribute
            var paramElement = memberElement.Elements("param")
                .FirstOrDefault(p => p.Attribute("name")?.Value == parameterName);

            if (paramElement == null)
                return null;

            return paramElement.Value.Trim();
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