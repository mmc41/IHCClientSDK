using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc {
    public record SeviceOperationMetadata(string Name, Type ReturnType, Type[] ParameterTypes)
    {
        public string Name { get; init; } = Name;
        public Type ReturnType { get; init; } = ReturnType;
        public Type[] ParameterTypes { get; init; } = ParameterTypes;
    }

    public static class ServiceMetadata
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
            var returnType = UnwrapAsyncReturnType(method.ReturnType);
            var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

            return new SeviceOperationMetadata(method.Name, returnType, parameterTypes);
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
                return returnType.GetGenericArguments()[0];
            }

            return returnType;
        }
    }

}