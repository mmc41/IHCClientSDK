using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
    public record SeviceOperationMetadata(string Name, Type ReturnType, Type[] ParameterTypes, ServiceOperationKind OperationKind, MethodInfo OperationDetails)
    {
        public string Name { get; init; } = Name;
        public Type ReturnType { get; init; } = ReturnType;
        public Type[] ParameterTypes { get; init; } = ParameterTypes;
        public ServiceOperationKind Kind { get; init; } = OperationKind;
        public MethodInfo MethodInfo { get; init; } = OperationDetails;

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

            return new SeviceOperationMetadata(method.Name, returnType, parameterTypes, operationType, method);
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
    }

}