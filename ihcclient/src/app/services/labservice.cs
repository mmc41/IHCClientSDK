using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text;
using System.Reflection;

namespace Ihc.App
{
    /// <summary>
    /// High-level application service for IHC laboratory types of test applications, where the user can experiment with individual IHC services.
    /// When used by test applications this service should act as source of truth for all state and the gui should use databinding and events
    /// to synchroinize against that state. This makes the complete solution modular and highly testable.
    /// 
    /// This application service is intended as a tech-agnostic backend for a GUI or console application with the following workflow:
    /// 1. The user select a service (usually in a dropdown in a GUI)
    /// 2. The user select an operation for the service (usually in a dropdown GUI)
    /// 3. The application supply default arguments (parameters of differnt types)
    /// 4. The user specify exact arguments
    /// 5. The user issues command to call the operation
    /// 6. The application show result or error.
    /// 7. The user looks at the result/error, optionally saving the it or a part of it to the clipboard or to a file. 
    /// 8. The user selects another operation or service.
    /// </summary>
    public class LabAppService : AppServiceBase
    {
        /// <summary>
        /// Hold information about a service and associated operations.
        /// </summary>
        public class ServiceItem
        {
            private int _selectedOperationIndex;

            /// <summary>
            /// The IHC API service instance.
            /// </summary>
            public IIHCApiService Service { get; init; }

            /// <summary>
            /// Name of the service that should be displayed to the user.
            /// </summary>
            public string DisplayName { get; init; }

            /// <summary>
            /// Array of operations available for this service (filtered according to operationFilter).
            /// </summary>
            public OperationItem[] OperationItems { get; init; }

            /// <summary>
            /// Gets or sets the currently selected operation index for this service.
            /// Used to restore the selection when switching between services.
            /// Validates that the index is within bounds of OperationItems array.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative or beyond the operations array bounds.</exception>
            public int SelectedOperationIndex
            {
                get => _selectedOperationIndex;
                set
                {
                    if (OperationItems == null || OperationItems.Length == 0)
                    {
                        // Allow setting to 0 when there are no operations (for initialization)
                        if (value != 0)
                            throw new ArgumentOutOfRangeException(nameof(value), $"Service has no operations, can only set to 0, but was {value}");
                        _selectedOperationIndex = 0;
                        return;
                    }

                    if (value < 0 || value >= OperationItems.Length)
                        throw new ArgumentOutOfRangeException(nameof(value), $"SelectedOperationIndex must be between 0 and {OperationItems.Length - 1}, but was {value}");

                    _selectedOperationIndex = value;
                }
            }

            /// <summary>
            /// Creates a new ServiceItem instance for the specified service.
            /// </summary>
            /// <param name="service">The IHC API service instance.</param>
            /// <param name="operationFilter">Filter function to determine which operations should be included. Returns true to include an operation.</param>
            public ServiceItem(IIHCApiService service, Func<ServiceOperationMetadata, bool> operationFilter)
            {
                Service = service;

                // Find the IHC service interface (works for both real services and fakes)
                var serviceInterfaces = service.GetType().GetInterfaces()
                    .Where(static i => i != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(i))
                    .ToList();

                // Use the interface name, stripping the leading 'I' for display
                if (serviceInterfaces.Count > 0)
                {
                    var interfaceName = serviceInterfaces[0].Name;
                    DisplayName = interfaceName.StartsWith("I") ? interfaceName.Substring(1) : interfaceName;
                }
                else
                {
                    // Fallback to type name if no interface found
                    DisplayName = service.GetType().Name;
                }

                OperationItems = ServiceMetadata.GetOperations(Service)
                    .Where(operation => operationFilter(operation))
                    .Select(method => new OperationItem(this, method))
                    .ToArray();

                // First operation selected initially.
                SelectedOperationIndex = 0;
            }

            /// <summary>
            /// Return index of first operation matching specified display name, prefix or subset. Returns 0 if non conditions are given.
            /// </summary>
            /// <param name="fullDisplayName">Full display name</param>
            /// <param name="prefixDisplayName">Prefix display name</param>
            /// <param name="substringDisplayName">Substring of display name</param>
            /// <returns>Index or -1 if not found</returns>
            public int LookupFirstOperationIndexByDisplayName(string fullDisplayName = null, string prefixDisplayName = null, string substringDisplayName = null)
            {
                // If no conditions specified, return 0
                if (fullDisplayName == null && prefixDisplayName == null && substringDisplayName == null)
                    return 0;

                // Search through operations for first match
                for (int i = 0; i < OperationItems.Length; i++)
                {
                    var displayName = OperationItems[i].DisplayName;

                    // Check full match first (highest priority)
                    if (fullDisplayName != null && displayName.Equals(fullDisplayName, StringComparison.Ordinal))
                        return i;

                    // Check prefix match second
                    if (prefixDisplayName != null && displayName.StartsWith(prefixDisplayName, StringComparison.Ordinal))
                        return i;

                    // Check substring match last (lowest priority)
                    if (substringDisplayName != null && displayName.Contains(substringDisplayName, StringComparison.Ordinal))
                        return i;
                }

                // No match found
                return -1;
            }

            /// <summary>
            /// Select operation by OperationItem instead of index.
            /// </summary>
            /// <param name="operation">The specific operation to selected (must belong to OperationItems)</param>
            /// <exception cref="ArgumentException"></exception>
            public void SelectOperation(OperationItem operation)
            {
                if (operation.Service != this)
                    throw new ArgumentException("Operation does not belong to service instance");

                for (int opIndex = 0; opIndex < OperationItems.Length; opIndex++)
                {
                    if (OperationItems[opIndex] == operation)
                    {
                        SelectedOperationIndex = opIndex;
                        return;
                    }
                }

                throw new ArgumentException("Operation unexpectedly not part of service instance");
            }
        }

        /// <summary>
        /// Provides data for the ParameterChanged event.
        /// Contains information about which method argument changed, including its index and old/new values.
        /// </summary>
        public class MethodArgumentChangedEventArgs : EventArgs
        {
            /// <summary>
            /// Gets the zero-based index of the argument that changed.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets the previous value of the argument before the change.
            /// May be null if the argument was previously unset or was explicitly null.
            /// </summary>
            public object OldValue { get; }

            /// <summary>
            /// Gets the new value of the argument after the change.
            /// </summary>
            public object NewValue { get; }

            /// <summary>
            /// Initializes a new instance of the ParameterChangedEventArgs class.
            /// </summary>
            /// <param name="index">The zero-based index of the argument that changed.</param>
            /// <param name="oldValue">The previous value of the argument.</param>
            /// <param name="newValue">The new value of the argument.</param>
            public MethodArgumentChangedEventArgs(int index, object oldValue, object newValue)
            {
                Index = index;
                OldValue = oldValue;
                NewValue = newValue;
            }
        }

        /// <summary>
        /// Provides data for the CurrentOperationChanged event.
        /// Contains information about service and operation selection changes.
        /// </summary>
        public class CurrentOperationChangedEventArgs : EventArgs
        {
            /// <summary>
            /// Gets the zero-based index of the previously selected service.
            /// </summary>
            public int OldServiceIndex { get; }

            /// <summary>
            /// Gets the zero-based index of the newly selected service.
            /// </summary>
            public int NewServiceIndex { get; }

            /// <summary>
            /// Gets the previously selected service item.
            /// May be null if no service was previously selected.
            /// </summary>
            public ServiceItem OldService { get; }

            /// <summary>
            /// Gets the newly selected service item.
            /// </summary>
            public ServiceItem NewService { get; }

            /// <summary>
            /// Gets the zero-based index of the previously selected operation within the old service.
            /// </summary>
            public int OldOperationIndex { get; }

            /// <summary>
            /// Gets the zero-based index of the newly selected operation within the new service.
            /// </summary>
            public int NewOperationIndex { get; }

            /// <summary>
            /// Gets the previously selected operation item.
            /// May be null if no operation was previously selected.
            /// </summary>
            public OperationItem OldOperation { get; }

            /// <summary>
            /// Gets the newly selected operation item.
            /// </summary>
            public OperationItem NewOperation { get; }

            /// <summary>
            /// Initializes a new instance of the CurrentOperationChangedEventArgs class.
            /// </summary>
            /// <param name="oldServiceIndex">The zero-based index of the previously selected service.</param>
            /// <param name="newServiceIndex">The zero-based index of the newly selected service.</param>
            /// <param name="oldService">The previously selected service item.</param>
            /// <param name="newService">The newly selected service item.</param>
            /// <param name="oldOperationIndex">The zero-based index of the previously selected operation.</param>
            /// <param name="newOperationIndex">The zero-based index of the newly selected operation.</param>
            /// <param name="oldOperation">The previously selected operation item.</param>
            /// <param name="newOperation">The newly selected operation item.</param>
            public CurrentOperationChangedEventArgs(
                int oldServiceIndex, int newServiceIndex,
                ServiceItem oldService, ServiceItem newService,
                int oldOperationIndex, int newOperationIndex,
                OperationItem oldOperation, OperationItem newOperation)
            {
                OldServiceIndex = oldServiceIndex;
                NewServiceIndex = newServiceIndex;
                OldService = oldService;
                NewService = newService;
                OldOperationIndex = oldOperationIndex;
                NewOperationIndex = newOperationIndex;
                OldOperation = oldOperation;
                NewOperation = newOperation;
            }
        }

        /// <summary>
        /// Hold information about a service operation and the arguments.
        /// </summary>
        public class OperationItem
        {
            /// <summary>
            /// The parent service item that owns this operation.
            /// </summary>
            public ServiceItem Service { get; init; }

            /// <summary>
            /// Metadata describing the operation including parameters, return type, and method information.
            /// </summary>
            public ServiceOperationMetadata OperationMetadata { get; }

            /// <summary>
            /// Name of the operation that should be displayed to the user.
            /// </summary>
            public string DisplayName { get; }

            /// <summary>
            /// Array of method argument values to pass when invoking the operation.
            /// Each element corresponds to a parameter defined in OperationMetadata.
            /// Use SetArgument() method for type-safe argument modification.
            /// </summary>
            public object[] MethodArguments { get; private set; }

            /// <summary>
            /// Gets the number of parameters for this operation.
            /// Equivalent to Arguments.Length.
            /// </summary>
            public int MethodParameterCount => MethodArguments?.Length ?? 0;

            /// <summary>
            /// Occurs when an argument value is changed via SetArgument() or SetArgumentsFromArray().
            /// Provides the index of the changed argument along with its old and new values.
            /// This event enables two-way synchronization between the backend and GUI layers.
            /// </summary>
            public event EventHandler<MethodArgumentChangedEventArgs> ArgumentChanged;

            /// <summary>
            /// Creates a new OperationItem instance with default arguments.
            /// </summary>
            /// <param name="service">The parent service item.</param>
            /// <param name="operationMetadata">Metadata describing the operation.</param>
            public OperationItem(ServiceItem service, ServiceOperationMetadata operationMetadata)
            {
                Service = service;
                OperationMetadata = operationMetadata;
                DisplayName = operationMetadata.Name;
                MethodArguments = GenerateDefaultArguments();
            }

            /// <summary>
            /// Sets the value of a specific argument with type validation.
            /// Validates that the provided value is compatible with the expected parameter type.
            /// </summary>
            /// <param name="index">Zero-based index of the argument to set (must be within [0, Parameters.Length - 1]).</param>
            /// <param name="value">The value to set. Must be assignable to the parameter type at the specified index. Null is allowed for reference types and Nullable&lt;T&gt;.</param>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when index is outside the valid range.</exception>
            /// <exception cref="ArgumentException">Thrown when the value type is incompatible with the expected parameter type.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the operation has no parameters.</exception>
            public void SetMethodArgument(int index, object value)
            {
                if (OperationMetadata.Parameters == null || OperationMetadata.Parameters.Length == 0)
                    throw new InvalidOperationException($"Operation '{DisplayName}' has no parameters");

                if (index < 0 || index >= OperationMetadata.Parameters.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Argument index must be between 0 and {OperationMetadata.Parameters.Length - 1}, but was {index}");

                var parameter = OperationMetadata.Parameters[index];
                var expectedType = parameter.Type;

                // Validate type compatibility
                if (value == null)
                {
                    // Null is allowed for reference types and Nullable<T>
                    if (expectedType.IsValueType && Nullable.GetUnderlyingType(expectedType) == null)
                    {
                        throw new ArgumentException(
                            $"Cannot set argument '{parameter.Name}' at index {index} to null. Expected type '{expectedType.Name}' is a non-nullable value type.",
                            nameof(value));
                    }
                }
                else
                {
                    var actualType = value.GetType();

                    // Check if value is assignable to expected type
                    if (!expectedType.IsAssignableFrom(actualType))
                    {
                        // Special handling for numeric conversions and nullable types
                        var underlyingType = Nullable.GetUnderlyingType(expectedType);
                        if (underlyingType != null)
                        {
                            // For Nullable<T>, check if value is assignable to T
                            if (!underlyingType.IsAssignableFrom(actualType))
                            {
                                throw new ArgumentException(
                                    $"Cannot set argument '{parameter.Name}' at index {index}. Expected type '{expectedType.Name}' but got '{actualType.Name}'.",
                                    nameof(value));
                            }
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"Cannot set argument '{parameter.Name}' at index {index}. Expected type '{expectedType.Name}' but got '{actualType.Name}'.",
                                nameof(value));
                        }
                    }
                }

                // Capture old value before modification
                object oldValue = MethodArguments[index];

                // Set the new value
                MethodArguments[index] = value;

                // Fire event only if value actually changed
                if (!AreValuesEqual(oldValue, value))
                {
                    OnMethodArgumentChanged(new MethodArgumentChangedEventArgs(index, oldValue, value));
                }
            }

            /// <summary>
            /// Sets all arguments from the provided array in a single operation.
            /// Validates that the array length matches the number of parameters and performs type validation for each argument.
            /// This is a convenience method for bulk argument setting, particularly useful when populating from GUI controls.
            /// </summary>
            /// <param name="values">Array of argument values. Length must match ParameterCount. Each value is validated via SetArgument().</param>
            /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
            /// <exception cref="ArgumentException">Thrown when array length doesn't match parameter count, or when any value has incompatible type.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the operation has no parameters but a non-empty array is provided.</exception>
            public void SetMethodArgumentsFromArray(object[] values)
            {
                if (values == null)
                    throw new ArgumentNullException(nameof(values));

                var expectedCount = MethodParameterCount;

                if (values.Length != expectedCount)
                    throw new ArgumentException(
                        $"Argument array length ({values.Length}) does not match parameter count ({expectedCount}) for operation '{DisplayName}'.",
                        nameof(values));

                // Validate and set each argument using SetArgument for type safety
                for (int i = 0; i < values.Length; i++)
                {
                    SetMethodArgument(i, values[i]);
                }
            }

            /// <summary>
            /// Returns a defensive copy of the current argument array.
            /// Modifications to the returned array will not affect the internal Arguments array.
            /// Use SetArgument() or SetArgumentsFromArray() to modify arguments.
            /// </summary>
            /// <returns>A new array containing copies of the current argument values.</returns>
            public object[] GetMethodArgumentsAsArray()
            {
                if (MethodArguments == null || MethodArguments.Length == 0)
                    return Array.Empty<object>();

                // Create defensive copy to prevent external mutation
                var copy = new object[MethodArguments.Length];
                Array.Copy(MethodArguments, copy, MethodArguments.Length);
                return copy;
            }

            /// <summary>
            /// Resets all arguments to their default values.
            /// Fires MethodArgumentChanged events for each argument that actually changes.
            /// </summary>
            public void ResetMethodArguments()
            {
                var newDefaults = GenerateDefaultArguments();

                // Fire events for each changed argument
                for (int i = 0; i < newDefaults.Length; i++)
                {
                    if (!AreValuesEqual(MethodArguments[i], newDefaults[i]))
                    {
                        object oldValue = MethodArguments[i];
                        MethodArguments[i] = newDefaults[i];
                        OnMethodArgumentChanged(new MethodArgumentChangedEventArgs(i, oldValue, newDefaults[i]));
                    }
                }
            }

            /// <summary>
            /// Raises the ArgumentChanged event with the specified event data.
            /// </summary>
            /// <param name="e">Event data containing the index, old value, and new value.</param>
            protected virtual void OnMethodArgumentChanged(MethodArgumentChangedEventArgs e)
            {
                ArgumentChanged?.Invoke(this, e);
            }

            /// <summary>
            /// Determines whether two values are equal, handling null values appropriately.
            /// Uses reference equality for identical references, Equals() for value comparison otherwise.
            /// </summary>
            /// <param name="a">First value to compare.</param>
            /// <param name="b">Second value to compare.</param>
            /// <returns>True if values are equal; otherwise, false.</returns>
            private static bool AreValuesEqual(object a, object b)
            {
                if (ReferenceEquals(a, b))
                    return true;

                if (a == null || b == null)
                    return false;

                return a.Equals(b);
            }

            /// <summary>
            /// Generates default argument values for all parameters of the operation.
            /// </summary>
            /// <returns>Array of default values matching the operation's parameter types.</returns>
            private object[] GenerateDefaultArguments()
            {
                if (OperationMetadata.Parameters == null || OperationMetadata.Parameters.Length == 0)
                    return Array.Empty<object>();

                return OperationMetadata.Parameters
                    .Select(param => GetDefaultValue(param.Type))
                    .ToArray();
            }

            /// <summary>
            /// Gets the default value for the specified type.
            /// Handles common types including primitives, strings, DateTime, Guid, TimeSpan, enums, and arrays.
            /// For reference types (except string), returns null.
            /// </summary>
            /// <param name="type">The type to get the default value for.</param>
            /// <returns>The default value for the type. Common defaults include: empty string for string, DateTime.Now for DateTime,
            /// Guid.Empty for Guid, TimeSpan.Zero for TimeSpan, empty arrays for array types, first enum value for enums,
            /// default(T) for value types, and null for other reference types.</returns>
            /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
            /// <exception cref="InvalidOperationException">Thrown when a default value cannot be created (e.g., enum with no values, array with indeterminate element type, or value type instantiation fails).</exception>
            public static object GetDefaultValue(Type type)
            {
                if (type == null)
                    throw new ArgumentNullException(nameof(type));

                // Handle string explicitly
                if (type == typeof(string))
                    return string.Empty;

                // Handle DateTime
                if (type == typeof(DateTime))
                    return DateTime.Now;

                // Handle byte arrays
                if (type == typeof(byte[]))
                    return Array.Empty<byte>();

                // Handle Guid
                if (type == typeof(Guid))
                    return Guid.Empty;

                // Handle TimeSpan
                if (type == typeof(TimeSpan))
                    return TimeSpan.Zero;

                // Handle DateTimeOffset
                if (type == typeof(DateTimeOffset))
                    return DateTimeOffset.Now;

                // Handle enums
                if (type.IsEnum)
                {
                    var enumValues = Enum.GetValues(type);
                    return enumValues.Length > 0 ? enumValues.GetValue(0) : throw new InvalidOperationException($"Enum {type.Name} has no values");
                }

                // Handle arrays (except byte[] which is handled above)
                if (type.IsArray)
                {
                    return Array.CreateInstance(type.GetElementType() ?? throw new InvalidOperationException($"Cannot determine element type for array {type.Name}"), 0);
                }

                // Handle value types (structs, primitives)
                if (type.IsValueType)
                    return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to create instance of {type.Name}");

                // For reference types, return null (caller must handle appropriately)
                return null;
            }
        }

        /// <summary>
        /// Holds information about the result of executing a service operation.
        /// </summary>
        public record OperationResult
        {
            /// <summary>
            /// The raw result object returned from the operation (typically a Task or Task&lt;T&gt;).
            /// </summary>
            public object Result { get; init; }

            /// <summary>
            /// A formatted string representation of the result suitable for display to the user.
            /// For void operations, this is "OK". For operations returning values, this contains
            /// the formatted result with special handling for DateTime, byte arrays, and collections.
            /// </summary>
            public string DisplayResult { get; init; }

            /// <summary>
            /// The return type of the operation method (e.g., Task, Task&lt;string&gt;, Task&lt;int&gt;).
            /// </summary>
            public Type ReturnType { get; init; }
        };

        private readonly object _lock = new object();
        private ServiceItem[] _services;
        private int _selectedServiceIndex;

        /// <summary>
        /// Occurs when the services array is replaced via Configure() or Services setter.
        /// GUI should rebind service/operation dropdowns when this event fires.
        /// </summary>
        public event EventHandler<EventArgs> ServicesChanged;

        /// <summary>
        /// Occurs when selected service or operation changes.
        /// Provides old and new service/operation indices and items for comparison.
        /// GUI should update dropdown selections and parameter controls when this event fires.
        /// </summary>
        public event EventHandler<CurrentOperationChangedEventArgs> CurrentOperationChanged;

        /// <summary>
        /// Gets or sets the array of available services.
        /// When setting, automatically resets SelectedServiceIndex to 0 to maintain consistency.
        /// Access is thread-safe.
        /// </summary>
        public ServiceItem[] Services
        {
            get
            {
                lock (_lock)
                {
                    return _services;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (value == null)
                        throw new ArgumentNullException(nameof(value), "Services array cannot be null");

                    _services = value;

                    // Reset selection to maintain consistency
                    // If the new array is smaller than the current index, this prevents out-of-range issues
                    _selectedServiceIndex = 0;
                }

                // Fire event outside lock to avoid potential deadlocks
                ServicesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises the CurrentOperationChanged event with captured old/new state.
        /// </summary>
        private void OnCurrentOperationChanged(
            int oldServiceIndex, int newServiceIndex,
            ServiceItem oldService, ServiceItem newService,
            int oldOperationIndex, int newOperationIndex,
            OperationItem oldOperation, OperationItem newOperation)
        {
            CurrentOperationChanged?.Invoke(this, new CurrentOperationChangedEventArgs(
                oldServiceIndex, newServiceIndex,
                oldService, newService,
                oldOperationIndex, newOperationIndex,
                oldOperation, newOperation));
        }

        /// <summary>
        /// Gets or sets the index of the currently selected service.
        /// When switched, the operation selection automatically reflects the previously selected operation for that service.
        /// Access is thread-safe.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when services are not configured.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the valid range [0, Services.Length - 1].</exception>
        public int SelectedServiceIndex
        {
            get
            {
                lock (_lock)
                {
                    return _selectedServiceIndex;
                }
            }
            set
            {
                int oldServiceIndex;
                int newServiceIndex;
                ServiceItem oldService;
                ServiceItem newService;
                int oldOperationIndex;
                int newOperationIndex;
                OperationItem oldOperation;
                OperationItem newOperation;

                lock (_lock)
                {
                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before setting SelectedServiceIndex");

                    if (value < 0 || value >= _services.Length)
                        throw new ArgumentOutOfRangeException(nameof(value), $"SelectedServiceIndex must be between 0 and {_services.Length - 1}, but was {value}");

                    // Capture old state
                    oldServiceIndex = _selectedServiceIndex;
                    oldService = (_services.Length > oldServiceIndex) ? _services[oldServiceIndex] : null;
                    oldOperationIndex = oldService?.SelectedOperationIndex ?? 0;
                    oldOperation = (oldService != null && oldService.OperationItems != null && oldService.OperationItems.Length > oldOperationIndex)
                        ? oldService.OperationItems[oldOperationIndex]
                        : null;

                    // Set new value
                    _selectedServiceIndex = value;

                    // Capture new state
                    newServiceIndex = _selectedServiceIndex;
                    newService = _services[newServiceIndex];
                    newOperationIndex = newService.SelectedOperationIndex;
                    newOperation = (newService.OperationItems != null && newService.OperationItems.Length > newOperationIndex)
                        ? newService.OperationItems[newOperationIndex]
                        : null;
                }

                // Fire event outside lock if state changed
                if (oldServiceIndex != newServiceIndex)
                {
                    OnCurrentOperationChanged(
                        oldServiceIndex, newServiceIndex,
                        oldService, newService,
                        oldOperationIndex, newOperationIndex,
                        oldOperation, newOperation);
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the currently selected operation within the current service.
        /// Each service maintains its own operation selection, which is preserved when switching between services.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when services are not configured or the selected service has no operations.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the valid range [0, OperationItems.Length - 1].</exception>
        public int SelectedOperationIndex
        {
            get
            {
                lock (_lock)
                {
                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before getting SelectedOperationIndex");

                    if (_selectedServiceIndex < 0 || _selectedServiceIndex >= _services.Length)
                        throw new InvalidOperationException($"SelectedServiceIndex {_selectedServiceIndex} is out of range");

                    return _services[_selectedServiceIndex].SelectedOperationIndex;
                }
            }
            set
            {
                int oldServiceIndex;
                int newServiceIndex;
                ServiceItem oldService;
                ServiceItem newService;
                int oldOperationIndex;
                int newOperationIndex;
                OperationItem oldOperation;
                OperationItem newOperation;

                lock (_lock)
                {
                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before setting SelectedOperationIndex");

                    if (_selectedServiceIndex < 0 || _selectedServiceIndex >= _services.Length)
                        throw new InvalidOperationException($"SelectedServiceIndex {_selectedServiceIndex} is out of range");

                    var operationItems = _services[_selectedServiceIndex].OperationItems;

                    if (operationItems == null || operationItems.Length == 0)
                        throw new InvalidOperationException($"Selected service has no operations");

                    if (value < 0 || value >= operationItems.Length)
                        throw new ArgumentOutOfRangeException(nameof(value), $"SelectedOperationIndex must be between 0 and {operationItems.Length - 1}, but was {value}");

                    // Capture old state
                    oldServiceIndex = _selectedServiceIndex;
                    oldService = _services[oldServiceIndex];
                    oldOperationIndex = oldService.SelectedOperationIndex;
                    oldOperation = (oldService.OperationItems != null && oldService.OperationItems.Length > oldOperationIndex)
                        ? oldService.OperationItems[oldOperationIndex]
                        : null;

                    // Set new value
                    _services[_selectedServiceIndex].SelectedOperationIndex = value;

                    // Capture new state (service stays the same, operation changed)
                    newServiceIndex = _selectedServiceIndex;
                    newService = _services[newServiceIndex];
                    newOperationIndex = newService.SelectedOperationIndex;
                    newOperation = (newService.OperationItems != null && newService.OperationItems.Length > newOperationIndex)
                        ? newService.OperationItems[newOperationIndex]
                        : null;
                }

                // Fire event outside lock if operation changed
                if (oldOperationIndex != newOperationIndex)
                {
                    OnCurrentOperationChanged(
                        oldServiceIndex, newServiceIndex,
                        oldService, newService,
                        oldOperationIndex, newOperationIndex,
                        oldOperation, newOperation);
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected operation item. When setting, automatically selects the service that owns the operation.
        /// NB: The setter is mostly intended for testing. SelectedOperationIndex is the prefered approach to setting current operation.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when services are not configured, indices are out of range, or the selected service has no operations.</exception>
        /// <exception cref="ArgumentNullException">Thrown when setting to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the operation's service is not part of the configured services.</exception>
        public OperationItem SelectedOperation
        {
            get
            {
                lock (_lock)
                {
                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before accessing SelectedOperation");

                    if (_selectedServiceIndex < 0 || _selectedServiceIndex >= _services.Length)
                        throw new InvalidOperationException($"SelectedServiceIndex {_selectedServiceIndex} is out of range");

                    var serviceItem = _services[_selectedServiceIndex];

                    if (serviceItem.OperationItems == null || serviceItem.OperationItems.Length == 0)
                        throw new InvalidOperationException($"Selected service has no operations");

                    var selectedOpIndex = serviceItem.SelectedOperationIndex;

                    if (selectedOpIndex < 0 || selectedOpIndex >= serviceItem.OperationItems.Length)
                        throw new InvalidOperationException($"SelectedOperationIndex {selectedOpIndex} is out of range");

                    return serviceItem.OperationItems[selectedOpIndex];
                }
            }
            set
            {
                int oldServiceIndex;
                int newServiceIndex;
                ServiceItem oldService;
                ServiceItem newService;
                int oldOperationIndex;
                int newOperationIndex;
                OperationItem oldOperation;
                OperationItem newOperation;
                bool changed = false;

                lock (_lock)
                {
                    if (value == null)
                        throw new ArgumentNullException(nameof(value), "Operation must be non-null");

                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before setting SelectedOperation");

                    // Find the service that owns this operation
                    var owningService = value.Service;
                    int serviceIndex = Array.IndexOf(_services, owningService);

                    if (serviceIndex == -1)
                        throw new ArgumentException("The operation's service is not part of the configured services", nameof(value));

                    // Capture old state
                    oldServiceIndex = _selectedServiceIndex;
                    oldService = _services[oldServiceIndex];
                    oldOperationIndex = oldService.SelectedOperationIndex;
                    oldOperation = (oldService.OperationItems != null && oldService.OperationItems.Length > oldOperationIndex)
                        ? oldService.OperationItems[oldOperationIndex]
                        : null;

                    // Select the service and operation
                    _selectedServiceIndex = serviceIndex;
                    owningService.SelectOperation(value);

                    // Capture new state
                    newServiceIndex = _selectedServiceIndex;
                    newService = _services[newServiceIndex];
                    newOperationIndex = newService.SelectedOperationIndex;
                    newOperation = (newService.OperationItems != null && newService.OperationItems.Length > newOperationIndex)
                        ? newService.OperationItems[newOperationIndex]
                        : null;

                    // Check if anything changed
                    changed = (oldServiceIndex != newServiceIndex) || (oldOperationIndex != newOperationIndex);
                }

                // Fire event outside lock if state changed
                if (changed)
                {
                    OnCurrentOperationChanged(
                        oldServiceIndex, newServiceIndex,
                        oldService, newService,
                        oldOperationIndex, newOperationIndex,
                        oldOperation, newOperation);
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected service item. If set, the provided service must be one of the instances of Services array.
        /// NB: The setter is mostly intended for testing. SelectedServiceIndex is the prefered approach to setting current service.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when services are not configured or the selected service index is out of range.</exception>
        public ServiceItem SelectedService
        {
            get
            {
                lock (_lock)
                {
                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before accessing SelectedService");

                    if (_selectedServiceIndex < 0 || _selectedServiceIndex >= _services.Length)
                        throw new InvalidOperationException($"SelectedServiceIndex {_selectedServiceIndex} is out of range");

                    return _services[_selectedServiceIndex];
                }
            }
            set
            {
                int oldServiceIndex;
                int newServiceIndex;
                ServiceItem oldService;
                ServiceItem newService;
                int oldOperationIndex;
                int newOperationIndex;
                OperationItem oldOperation;
                OperationItem newOperation;

                lock (_lock)
                {
                    if (value == null)
                        throw new ArgumentNullException(nameof(value), "Service must be non-null");

                    if (_services == null || _services.Length == 0)
                        throw new InvalidOperationException("Services must be configured before setting SelectedService");

                    int index = Array.IndexOf(_services, value);
                    if (index == -1)
                        throw new ArgumentException("The provided service is not part of the configured services", nameof(value));

                    // Capture old state
                    oldServiceIndex = _selectedServiceIndex;
                    oldService = _services[oldServiceIndex];
                    oldOperationIndex = oldService.SelectedOperationIndex;
                    oldOperation = (oldService.OperationItems != null && oldService.OperationItems.Length > oldOperationIndex)
                        ? oldService.OperationItems[oldOperationIndex]
                        : null;

                    // Set new value
                    _selectedServiceIndex = index;

                    // Capture new state
                    newServiceIndex = _selectedServiceIndex;
                    newService = _services[newServiceIndex];
                    newOperationIndex = newService.SelectedOperationIndex;
                    newOperation = (newService.OperationItems != null && newService.OperationItems.Length > newOperationIndex)
                        ? newService.OperationItems[newOperationIndex]
                        : null;
                }

                // Fire event outside lock if service changed
                if (oldServiceIndex != newServiceIndex)
                {
                    OnCurrentOperationChanged(
                        oldServiceIndex, newServiceIndex,
                        oldService, newService,
                        oldOperationIndex, newOperationIndex,
                        oldOperation, newOperation);
                }
            }
        }

        private readonly Func<ServiceOperationMetadata, bool> globalSupportedOperationFilter;
        private readonly Func<IIHCApiService, bool> globalSupportedServiceFilter;

        /// <summary>
        /// Create an LabService with no services.
        /// Call configure post-construction to initialize.
        /// The provided filters can be used to specify a subset of services/operations that should be listed and selectable.
        /// </summary>
        /// <param name="globalSupportedServiceFilter">Specifies which services are supported for lab application. Null (default) means all</param>
        /// <param name="globalSupportedOperationFilter">Specifies which operations are supported for lab application. Null (default) means all</param>
        public LabAppService(Func<IIHCApiService, bool> globalSupportedServiceFilter, Func<ServiceOperationMetadata, bool> globalSupportedOperationFilter = null)
        {
            this.globalSupportedServiceFilter = globalSupportedServiceFilter ?? (s => true);
            this.globalSupportedOperationFilter = globalSupportedOperationFilter ?? (s => true);

            _services = Array.Empty<ServiceItem>();
            _selectedServiceIndex = 0;
            // No need to initialize _selectedOperationIndex - it's stored in each ServiceItem
        }

        /// <summary>
        /// (Re)Configure LabService with specified settings and services.
        /// Applies service and operation filters to determine which services and operations are available.
        /// Resets selection to the first service and first operation.
        /// Thread-safe.
        /// </summary>
        /// <param name="settings">IHC settings (currently unused but reserved for future use).</param>
        /// <param name="serviceInterfaces">Array of IHC API service instances to make available in the lab.</param>
        public void Configure(IhcSettings settings, IIHCApiService[] serviceInterfaces)
        {
            using (var activity = StartActivity(nameof(Configure)))
            {
                try
                {
                    if (serviceInterfaces == null)
                        throw new ArgumentNullException(nameof(serviceInterfaces), "Service interfaces array cannot be null");

                    activity?.SetParameters(
                        (nameof(settings), settings?.ToString() ?? "null"),
                        (nameof(serviceInterfaces), serviceInterfaces.Length.ToString()));

                    lock (_lock)
                    {
                        _services = serviceInterfaces
                            .Where(service => globalSupportedServiceFilter(service))
                            .Select(service => new ServiceItem(service, this.globalSupportedOperationFilter))
                            .ToArray();

                        // Reset to first service (each ServiceItem tracks its own operation index, defaulting to 0)
                        _selectedServiceIndex = 0;
                    }

                    // Fire event outside lock to avoid potential deadlocks
                    ServicesChanged?.Invoke(this, EventArgs.Empty);

                    activity?.SetTag("ServiceCount", _services?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Dynamically invokes the currently selected operation with its configured arguments.
        /// Supports only AsyncFunction operations (Task and Task&lt;T&gt;), not AsyncEnumerable.
        /// Thread-safe: captures operation context under lock, then releases lock before async execution.
        /// </summary>
        /// <returns>An OperationResult containing the raw result, formatted display string, and return type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when services/operations are not configured, indices are invalid, or invocation fails.</exception>
        /// <exception cref="NotSupportedException">Thrown when the operation kind is not AsyncFunction or the return type is unsupported.</exception>
        public async Task<OperationResult> DynCallSelectedOperation()
        {
            using var activity = StartActivity(nameof(DynCallSelectedOperation));

            try
            {
                IIHCApiService service;
                ServiceOperationMetadata operationMetadata;
                object[] parameterValues;

                // Capture operation context inside lock
                lock (_lock)
                {
                    service = SelectedOperation.Service.Service;
                    operationMetadata = SelectedOperation.OperationMetadata;
                    parameterValues = SelectedOperation.MethodArguments;
                }

                var serviceName = service?.GetType()?.Name ?? "Unknown";
                var operationName = operationMetadata?.Name ?? "Unknown";

                activity?.SetParameters(
                    ("ServiceName", serviceName),
                    ("OperationName", operationName),
                    ("ParameterCount", parameterValues?.Length.ToString() ?? "0"));

                activity?.SetTag(Ihc.Telemetry.argsTagPrefix + "parameterValues", String.Join(",", parameterValues.Select(p => p?.ToString() ?? "null")));

                if (operationMetadata.Kind != ServiceOperationKind.AsyncFunction)
                    throw new NotSupportedException($"Only normal async operations currently supported (service: {serviceName}, operation: {operationName}, kind: {operationMetadata.Kind})");

                object taskObject;
                try
                {
                    taskObject = operationMetadata.Invoke(parameterValues);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to invoke operation '{operationName}' on service '{serviceName}': {ex.Message}", ex);
                }

                if (taskObject == null)
                    throw new InvalidOperationException($"Method invocation returned null for operation '{operationName}' on service '{serviceName}'");

                // Get the declared return type from the method (not the runtime type which can be AsyncStateMachineBox)
                Type taskType = operationMetadata.MethodInfo.ReturnType;

                // Check if it's a Task (void) or Task<T>
                if (taskType == typeof(Task))
                {
                    // It's a Task (void async method)
                    try
                    {
                        await (Task)taskObject;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Operation '{operationName}' on service '{serviceName}' failed during execution: {ex.Message}", ex);
                    }

                    var result = new OperationResult()
                    {
                        Result = taskObject,
                        DisplayResult = "OK",
                        ReturnType = taskType
                    };

                    activity?.SetReturnValue($"Success=true, ReturnType={taskType?.Name ?? "null"}");
                    return result;
                }
                else if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // It's a Task<T> - we need to await it and get the result
                    // Use dynamic to await the task
                    dynamic dynamicTask = taskObject;
                    try
                    {
                        await dynamicTask;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Operation '{operationName}' on service '{serviceName}' failed during execution: {ex.Message}", ex);
                    }

                    // Get the Result property
                    var resultProperty = taskType.GetProperty("Result");
                    if (resultProperty == null)
                        throw new InvalidOperationException($"Task<T> does not have a Result property for operation '{operationName}' on service '{serviceName}'");

                    object result = resultProperty.GetValue(taskObject);

                    // Format the result as a string
                    string strResult = FormatResult(result, operationMetadata.ReturnType);

                    var operationResult = new OperationResult()
                    {
                        Result = taskObject,
                        DisplayResult = strResult,
                        ReturnType = taskType
                    };

                    activity?.SetReturnValue($"Success=true, ReturnType={taskType?.Name ?? "null"}, DisplayResult={strResult ?? "null"}");
                    return operationResult;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported return type: {taskType.Name} for operation '{operationName}' on service '{serviceName}'");
                }
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Formats a result object into a readable string representation.
        /// Provides special formatting for DateTime (yyyy-MM-dd HH:mm:ss.fff), DateTimeOffset (with timezone),
        /// TimeSpan, Guid, byte arrays (hex preview), numeric types (with precision), booleans (lowercase),
        /// and collections (limited to 100 items). Falls back to ToString() for other types.
        /// </summary>
        /// <param name="result">The result object to format. Can be null.</param>
        /// <param name="returnType">The return type metadata (currently unused but available for future enhancements).</param>
        /// <returns>Formatted string representation suitable for user display. Returns "(null)" for null values.</returns>
        private string FormatResult(object result, Type returnType)
        {
            if (result == null)
                return "(null)";

            // Handle DateTime types
            if (result is DateTime dt)
                return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");

            if (result is DateTimeOffset dto)
                return dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

            // Handle TimeSpan
            if (result is TimeSpan ts)
                return ts.ToString();

            // Handle Guid
            if (result is Guid guid)
                return guid.ToString();

            // Handle byte arrays specially
            if (result is byte[] byteArray)
            {
                if (byteArray.Length == 0)
                    return "byte[0] []";

                if (byteArray.Length <= 16)
                {
                    // For small arrays, show hex values
                    return $"byte[{byteArray.Length}] [{BitConverter.ToString(byteArray).Replace("-", " ")}]";
                }
                else
                {
                    // For large arrays, show length and first 16 bytes
                    var preview = BitConverter.ToString(byteArray, 0, 16).Replace("-", " ");
                    return $"byte[{byteArray.Length}] [{preview} ...]";
                }
            }

            // Handle primitive numeric types with their type names
            if (result is int || result is long || result is short || result is byte)
                return $"{result}";

            if (result is uint || result is ulong || result is ushort || result is sbyte)
                return $"{result}";

            if (result is float f)
                return f.ToString("G9");

            if (result is double d)
                return d.ToString("G17");

            if (result is decimal dec)
                return dec.ToString("G");

            // Handle boolean
            if (result is bool b)
                return b.ToString().ToLowerInvariant();

            // Handle arrays and enumerables
            if (result is System.Collections.IEnumerable enumerable && result is not string)
            {
                var items = enumerable.Cast<object>().ToArray();
                if (items.Length == 0)
                    return "[]";

                // Limit display to first 100 items to prevent excessive output
                const int maxItems = 100;
                var displayItems = items.Take(maxItems).ToArray();

                // Format array elements
                var formattedItems = displayItems.Select(item =>
                {
                    if (item == null) return "null";
                    if (item is string s) return $"\"{s}\"";
                    if (item is DateTime itemDt) return itemDt.ToString("yyyy-MM-dd HH:mm:ss");
                    if (item is byte[] itemBytes) return $"byte[{itemBytes.Length}]";
                    return item.ToString() ?? "null";
                });

                var result_str = $"[{string.Join(", ", formattedItems)}";
                if (items.Length > maxItems)
                    result_str += $", ... ({items.Length - maxItems} more)";
                result_str += "]";

                return result_str;
            }

            return result.ToString() ?? "(empty)";
        }

    }
}