using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace Ihc
{
    /// <summary>
    /// Utilities to help to copy and transform complex objects.
    /// </summary>
    public static class CopyUtil
    {
        /// <summary>
        /// An exception will be thrown if the max number of recursive steps is reached.
        /// </summary>
        private const int MaxRecursionDepth = 100;

        /// <summary>
        /// Determines if a type is known to be immutable and doesn't need deep copying.
        /// </summary>
        private static bool IsKnownImmutable(Type type)
        {
            return type.IsPrimitive || type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type == typeof(decimal);
        }

        /// <summary>
        /// Computes a deep copy of the src object, applying a property value transformer on each property value and collection element.
        /// Useful for redacting passwords or encrypting certain properties.
        /// </summary>
        /// <param name="src">Source object to copy. Can be null.</param>
        /// <param name="propertyValueTransformer">
        /// Transformer function applied to each property value and collection element during copy.
        /// First parameter is PropertyInfo of the property containing the value (null only for root-level collections).
        /// For collection elements (arrays, lists, sets, dictionary values), receives the parent property's PropertyInfo.
        /// Dictionary keys are deep-copied but NOT passed through the transformer function.
        /// </param>
        /// <returns>A deep copy of the source object with transformations applied, or null if src is null.</returns>
        /// <exception cref="InvalidOperationException">Thrown when recursion depth exceeds 100 or type has no suitable constructor.</exception>
        /// <exception cref="NotSupportedException">Thrown when attempting to copy multi-dimensional arrays.</exception>
        /// <remarks>
        /// <para>IMPORTANT: Circular references are not supported. Object graphs containing circular references
        /// will throw an InvalidOperationException when the recursion depth limit is exceeded.</para>
        /// <para>Read-only properties (properties with only a getter and no setter) will only be preserved if
        /// the type has a constructor that accepts all properties as parameters. Otherwise, read-only properties
        /// and computed properties will be lost in the copy.</para>
        /// <para>Comparers for Dictionary and HashSet collections are preserved when copying exact Dictionary/HashSet types.</para>
        /// <para><b>OpenTelemetry Integration:</b> This method automatically creates an Activity for tracing and emits warning events
        /// for non-fatal issues during copying. Warning events include:</para>
        /// <list type="bullet">
        /// <item><description><b>TypeFidelityLoss:</b> Interface type copied as concrete type (e.g., IList→List).
        /// This warning is emitted when a property has an interface type (IList, ISet, IDictionary) but contains a
        /// concrete implementation. The copy will use a concrete type (List, HashSet, Dictionary) rather than the
        /// interface. Note: This warning is only emitted for properties, not for root-level objects or collection
        /// elements, as the actual runtime type is preserved in those cases.</description></item>
        /// <item><description><b>ComparerFallback:</b> Dictionary/HashSet comparer couldn't be preserved</description></item>
        /// <item><description><b>ReadOnlyPropertyLost:</b> Read-only property without matching constructor</description></item>
        /// <item><description><b>IndexedPropertySkipped:</b> Indexed properties cannot be copied</description></item>
        /// </list>
        /// <para>To capture warnings, configure an ActivityListener that listens to the "ihcclient" ActivitySource.</para>
        /// <para><b>Type Fidelity and Interface Collections:</b> When copying interface-typed collection properties
        /// (IList&lt;T&gt;, ISet&lt;T&gt;, IDictionary&lt;TKey,TValue&gt;), the method creates concrete implementations
        /// (List&lt;T&gt;, HashSet&lt;T&gt;, Dictionary&lt;TKey,TValue&gt;) because the original concrete type information
        /// is not always accessible or reconstructible. This is a known limitation and a TypeFidelityLoss warning will be
        /// emitted. To avoid this warning, use concrete types in your property declarations when possible.</para>
        /// </remarks>
        /// <example>
        /// Identity transformer example (no modifications):
        /// <code><![CDATA[
        /// var copy = CopyUtil.DeepCopyAndApply(original, (prop, value) => value);
        /// ]]></code>
        ///
        /// Capturing warnings via ActivityListener:
        /// <code><![CDATA[
        /// var warnings = new List<ActivityEvent>();
        /// using var listener = new ActivityListener
        /// {
        ///     ShouldListenTo = source => source.Name == "ihcclient",
        ///     Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ///     ActivityStopped = activity =>
        ///     {
        ///         foreach (var evt in activity.Events)
        ///         {
        ///             if (evt.Name == "Warning")
        ///             {
        ///                 warnings.Add(evt);
        ///                 // Access tags: evt.Tags["warning.type"], evt.Tags["warning.path"], etc.
        ///             }
        ///         }
        ///     }
        /// };
        /// ActivitySource.AddActivityListener(listener);
        ///
        /// IList<int> original = new List<int> { 1, 2, 3 };
        /// var copy = CopyUtil.DeepCopyAndApply(original, (prop, value) => value);
        ///
        /// // Check warnings by type
        /// foreach (var warning in warnings)
        /// {
        ///     var type = warning.Tags.FirstOrDefault(t => t.Key == "type").Value;
        ///     var message = warning.Tags.FirstOrDefault(t => t.Key == "warning.message").Value;
        ///     Console.WriteLine($"{type}: {message}");
        /// }
        /// ]]></code>
        /// </example>
        public static object DeepCopyAndApply(object src, Func<PropertyInfo, object, object> propertyValueTransformer)
        {
            using (var activity = Telemetry.ActivitySource.StartActivity(nameof(CopyUtil) + "." + nameof(DeepCopyAndApply), ActivityKind.Internal))
            {
                try
                {
                    if (propertyValueTransformer == null)
                        throw new ArgumentNullException(nameof(propertyValueTransformer));

                    if (src == null)
                        return null;

                    activity?.SetTag($"{Telemetry.argsTagPrefix}{src}.Type", src.GetType());

                    return DoDeepCopyAndApply(src, propertyValueTransformer, 0, null, "root", Activity.Current);
                } catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        private static object DoDeepCopyAndApply(object src, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            // Enforce max recursion depth (this also prevents stack overflow from circular references,
            // though circular references are not properly preserved - they will cause an exception)
            if (depth >= MaxRecursionDepth)
            {
                throw new InvalidOperationException(
                    $"Maximum recursion depth of {MaxRecursionDepth} exceeded during deep copy at path: {path}");
            }

            // Handle null
            if (src == null)
                return null;

            var type = src.GetType();

            // Type checking order is important:
            // 1. Check simple/immutable types first (fastest path, most common)
            // 2. Check arrays (must come before IEnumerable since arrays implement IEnumerable)
            // 3. Check non-generic collections (must come before generic type checking)
            // 4. Check generic collections (Dictionary, List, HashSet, etc.)
            // 5. Finally handle complex objects/records

            // Handle Nullable<T> value types (e.g., int?, DateTime?) - these are effectively immutable
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return src;
            }

            // Handle simple/immutable types that don't need deep copying
            if (IsKnownImmutable(type))
            {
                return src;
            }

            // Handle arrays (must come before non-generic IEnumerable check since arrays also implement IEnumerable)
            if (type.IsArray)
            {
                return DoCopyArray((Array)src, propertyValueTransformer, depth, parentProperty, path, activity);
            }

            // Handle non-generic IEnumerable (like ArrayList)
            if (!type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
                type != typeof(string))
            {
                if (typeof(System.Collections.IList).IsAssignableFrom(type))
                {
                    return DoCopyNonGenericList(src, type, propertyValueTransformer, depth, parentProperty, path, activity);
                }
            }

            // Handle generic collections
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // Handle Dictionary<TKey, TValue> and IDictionary<TKey, TValue>
                if (genericTypeDef == typeof(Dictionary<,>) ||
                    type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                {
                    return DoCopyDictionary(src, type, genericArgs, propertyValueTransformer, depth, parentProperty, path, activity);
                }

                // Handle HashSet<T> and ISet<T>
                if (genericTypeDef == typeof(HashSet<>) ||
                    type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>)))
                {
                    return DoCopyHashSet(src, type, genericArgs, propertyValueTransformer, depth, parentProperty, path, activity);
                }

                // Handle List<T>, IList<T>, ICollection<T>, and IEnumerable<T>
                if (genericTypeDef == typeof(List<>) ||
                    type.GetInterfaces().Any(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                         i.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                         i.GetGenericTypeDefinition() == typeof(IList<>))))
                {
                    return DoCopyList(src, type, genericArgs, propertyValueTransformer, depth, parentProperty, path, activity);
                }
            }

            // Handle records and classes
            return DoCopyObject(src, type, propertyValueTransformer, depth, path, activity);
        }

        private static object DoCopyArray(Array sourceArray, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            var elementType = sourceArray.GetType().GetElementType();
            var rank = sourceArray.Rank;

            if (rank == 1)
            {
                // Handle single-dimensional arrays
                var length = sourceArray.Length;
                var copiedArray = Array.CreateInstance(elementType, length);

                for (int i = 0; i < length; i++)
                {
                    var element = sourceArray.GetValue(i);
                    var elementPath = $"{path}[{i}]";
                    var copiedElement = DoDeepCopyAndApply(element, propertyValueTransformer, depth + 1, parentProperty, elementPath, activity);

                    object transformedElement;
                    try
                    {
                        transformedElement = propertyValueTransformer(parentProperty, copiedElement);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Transformer function threw an exception while processing array element at path: {elementPath}. " +
                            $"Element type: {elementType?.FullName ?? "unknown"}. See inner exception for details.",
                            ex);
                    }

                    copiedArray.SetValue(transformedElement, i);
                }

                return copiedArray;
            }
            else
            {
                // Multi-dimensional arrays are not supported
                throw new NotSupportedException(
                    $"Multi-dimensional arrays are not supported at path: {path}. Array has rank {rank}.");
            }
        }

        private static object DoCopyNonGenericList(object src, Type type, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            System.Collections.IList copiedList;
            try
            {
                // Attempt to create an instance of the original type
                copiedList = (System.Collections.IList)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(
                    $"Cannot create instance of non-generic IList type '{type.FullName}' at path: {path}. " +
                    $"The type must have a public parameterless constructor. Inner exception: {ex.Message}",
                    ex);
            }

            int index = 0;
            foreach (var item in (System.Collections.IEnumerable)src)
            {
                var itemPath = $"{path}[{index}]";
                var copiedItem = DoDeepCopyAndApply(item, propertyValueTransformer, depth + 1, parentProperty, itemPath, activity);

                object transformedItem;
                try
                {
                    transformedItem = propertyValueTransformer(parentProperty, copiedItem);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Transformer function threw an exception while processing non-generic list element at path: {itemPath}. " +
                        $"See inner exception for details.",
                        ex);
                }

                copiedList.Add(transformedItem);
                index++;
            }

            return copiedList;
        }

        private static object DoCopyDictionary(object src, Type sourceType, Type[] genericArgs, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            // Validate that dictionary keys are immutable types
            var keyType = genericArgs[0];
            if (!IsKnownImmutable(keyType))
            {
                throw new NotSupportedException(
                    $"Dictionary key type '{keyType.FullName}' is not supported at path: {path}. " +
                    $"Only immutable types (primitives, enums, string, DateTime, DateTimeOffset, TimeSpan, Guid, decimal) " +
                    $"are allowed as dictionary keys to ensure correct equality semantics after deep copy.");
            }

            // Emit warning if source type is IDictionary interface (type fidelity loss)
            if (sourceType.IsInterface && sourceType.IsGenericType)
            {
                var genericTypeDef = sourceType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(IDictionary<,>))
                {
                    activity?.AddWarning(
                        $"Property at path: {path} has interface type IDictionary<{genericArgs[0].Name},{genericArgs[1].Name}> but will be copied as Dictionary<{genericArgs[0].Name},{genericArgs[1].Name}>",
                        ("type", "TypeFidelityLoss"),
                        ("path", path),
                        ("declaredType", sourceType.FullName),
                        ("runtimeType", $"Dictionary<{genericArgs[0].FullName},{genericArgs[1].FullName}>"));
                }
            }

            var dictType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
            object copiedDict;
            bool comparerFailed = false;

            // Try to preserve the comparer if the source is a Dictionary<,>
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var comparerProp = sourceType.GetProperty("Comparer");
                if (comparerProp != null)
                {
                    var comparer = comparerProp.GetValue(src);
                    try
                    {
                        // Try to create with comparer
                        copiedDict = Activator.CreateInstance(dictType, comparer);
                    }
                    catch
                    {
                        // Fall back to parameterless constructor if comparer constructor fails
                        copiedDict = Activator.CreateInstance(dictType);
                        comparerFailed = true;
                    }
                }
                else
                {
                    copiedDict = Activator.CreateInstance(dictType);
                }
            }
            else
            {
                copiedDict = Activator.CreateInstance(dictType);
            }

            if (comparerFailed)
            {
                activity?.AddWarning(
                    $"Dictionary comparer could not be preserved at path: {path}, using default comparer",
                    ("type", "ComparerFallback"),
                    ("path", path),
                    ("sourceType", sourceType.FullName),
                    ("keyType", genericArgs[0].FullName));
            }

            var addMethod = dictType.GetMethod("Add");

            foreach (var kvp in (System.Collections.IEnumerable)src)
            {
                var kvpType = kvp.GetType();
                var key = kvpType.GetProperty("Key").GetValue(kvp);
                var value = kvpType.GetProperty("Value").GetValue(kvp);

                // Keys are immutable (enforced by validation above), so no need to deep-copy them
                var valuePath = $"{path}[{key}]";
                var copiedValue = DoDeepCopyAndApply(value, propertyValueTransformer, depth + 1, parentProperty, valuePath, activity);

                object transformedValue;
                try
                {
                    transformedValue = propertyValueTransformer(parentProperty, copiedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Transformer function threw an exception while processing dictionary value at path: {valuePath}. " +
                        $"Key: {key}, Value type: {genericArgs[1].FullName}. See inner exception for details.",
                        ex);
                }

                addMethod.Invoke(copiedDict, new[] { key, transformedValue });
            }

            return copiedDict;
        }

        private static object DoCopyHashSet(object src, Type sourceType, Type[] genericArgs, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            // Validate that HashSet element type is safe for transformation
            var elementType = genericArgs[0];
            bool isElementImmutable = IsKnownImmutable(elementType);

            // Emit warning if source type is ISet interface (type fidelity loss)
            if (sourceType.IsInterface && sourceType.IsGenericType)
            {
                var genericTypeDef = sourceType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(ISet<>))
                {
                    activity?.AddWarning(
                        $"Property at path: {path} has interface type ISet<{genericArgs[0].Name}> but will be copied as HashSet<{genericArgs[0].Name}>",
                        ("type", "TypeFidelityLoss"),
                        ("path", path),
                        ("declaredType", sourceType.FullName),
                        ("runtimeType", $"HashSet<{genericArgs[0].FullName}>"));
                }
            }

            var setType = typeof(HashSet<>).MakeGenericType(genericArgs);
            object copiedSet;
            bool comparerFailed = false;

            // Try to preserve the comparer if the source is a HashSet<>
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var comparerProp = sourceType.GetProperty("Comparer");
                if (comparerProp != null)
                {
                    var comparer = comparerProp.GetValue(src);
                    try
                    {
                        // Try to create with comparer
                        copiedSet = Activator.CreateInstance(setType, comparer);
                    }
                    catch
                    {
                        // Fall back to parameterless constructor if comparer constructor fails
                        copiedSet = Activator.CreateInstance(setType);
                        comparerFailed = true;
                    }
                }
                else
                {
                    copiedSet = Activator.CreateInstance(setType);
                }
            }
            else
            {
                copiedSet = Activator.CreateInstance(setType);
            }

            if (comparerFailed)
            {
                activity?.AddWarning(
                    $"HashSet comparer could not be preserved at path: {path}, using default comparer",
                    ("type", "ComparerFallback"),
                    ("path", path),
                    ("sourceType", sourceType.FullName),
                    ("elementType", genericArgs[0].FullName));
            }

            var addMethod = setType.GetMethod("Add");

            int index = 0;
            foreach (var item in (System.Collections.IEnumerable)src)
            {
                var itemPath = $"{path}[{index}]";
                var copiedItem = DoDeepCopyAndApply(item, propertyValueTransformer, depth + 1, parentProperty, itemPath, activity);

                // Compute hash code before transformation to detect mutations
                int? hashCodeBefore = null;
                if (!isElementImmutable && copiedItem != null)
                {
                    try
                    {
                        hashCodeBefore = copiedItem.GetHashCode();
                    }
                    catch
                    {
                        // If GetHashCode() throws, we can't safely detect mutations
                        // Continue anyway - the element might still work correctly
                    }
                }

                object transformedItem;
                try
                {
                    transformedItem = propertyValueTransformer(parentProperty, copiedItem);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Transformer function threw an exception while processing HashSet element at path: {itemPath}. " +
                        $"Element type: {elementType.FullName}. See inner exception for details.",
                        ex);
                }

                // Detect mutations by checking both reference equality and hash code changes
                if (!isElementImmutable && copiedItem != null && transformedItem != null)
                {
                    bool referenceChanged = !ReferenceEquals(copiedItem, transformedItem);
                    bool hashCodeChanged = false;

                    if (hashCodeBefore.HasValue && ReferenceEquals(copiedItem, transformedItem))
                    {
                        // Same reference - check if it was mutated in place
                        try
                        {
                            int hashCodeAfter = transformedItem.GetHashCode();
                            hashCodeChanged = hashCodeBefore.Value != hashCodeAfter;
                        }
                        catch
                        {
                            // If GetHashCode() throws, assume no change
                        }
                    }

                    if (referenceChanged || hashCodeChanged)
                    {
                        throw new InvalidOperationException(
                            $"HashSet element transformation at path: {itemPath} is potentially unsafe. " +
                            $"Element type '{elementType.FullName}' is not immutable, and the transformer " +
                            $"{(referenceChanged ? "returned a different object" : "mutated the element in place")}. " +
                            $"This may break HashSet equality semantics if the transformation affects properties used in " +
                            $"GetHashCode() or Equals(), potentially causing duplicate elements or loss of uniqueness. " +
                            $"Consider using immutable element types or an identity transformer for HashSet elements.");
                    }
                }

                addMethod.Invoke(copiedSet, new[] { transformedItem });
                index++;
            }

            return copiedSet;
        }

        private static object DoCopyList(object src, Type sourceType, Type[] genericArgs, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, PropertyInfo parentProperty, string path, Activity activity)
        {
            // Emit warning if source type is IList/ICollection/IEnumerable interface (type fidelity loss)
            if (sourceType.IsInterface && sourceType.IsGenericType)
            {
                var genericTypeDef = sourceType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(IList<>) ||
                    genericTypeDef == typeof(ICollection<>) ||
                    genericTypeDef == typeof(IEnumerable<>))
                {
                    var interfaceName = genericTypeDef.Name.Replace("`1", "");
                    activity?.AddWarning(
                        $"Property at path: {path} has interface type {interfaceName}<{genericArgs[0].Name}> but will be copied as List<{genericArgs[0].Name}>",
                        ("type", "TypeFidelityLoss"),
                        ("path", path),
                        ("declaredType", sourceType.FullName),
                        ("runtimeType", $"List<{genericArgs[0].FullName}>"));
                }
            }

            var listType = typeof(List<>).MakeGenericType(genericArgs);
            var copiedList = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");

            int index = 0;
            foreach (var item in (System.Collections.IEnumerable)src)
            {
                var itemPath = $"{path}[{index}]";
                var copiedItem = DoDeepCopyAndApply(item, propertyValueTransformer, depth + 1, parentProperty, itemPath, activity);

                object transformedItem;
                try
                {
                    transformedItem = propertyValueTransformer(parentProperty, copiedItem);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Transformer function threw an exception while processing list element at path: {itemPath}. " +
                        $"Element type: {genericArgs[0].FullName}. See inner exception for details.",
                        ex);
                }

                addMethod.Invoke(copiedList, new[] { transformedItem });
                index++;
            }

            return copiedList;
        }

        private static object DoCopyObject(object src, Type type, Func<PropertyInfo, object, object> propertyValueTransformer, int depth, string path, Activity activity)
        {
            var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray();

            // Track indexed properties for warning
            var indexedProperties = allProperties.Where(p => p.GetIndexParameters().Length > 0).ToArray();
            if (indexedProperties.Length > 0)
            {
                foreach (var indexedProp in indexedProperties)
                {
                    activity?.AddWarning(
                        $"Indexed property '{indexedProp.Name}' cannot be copied at path: {path}",
                        ("type", "IndexedPropertySkipped"),
                        ("path", path),
                        ("propertyName", indexedProp.Name),
                        ("propertyType", indexedProp.PropertyType.FullName));
                }
            }

            var properties = allProperties.Where(p => p.GetIndexParameters().Length == 0).ToArray();

            // Copy all property values recursively with transformation
            var copiedPropertyValues = new Dictionary<PropertyInfo, object>();
            foreach (var prop in properties)
            {
                var originalValue = prop.GetValue(src);
                var propertyPath = $"{path}.{prop.Name}";

                // Detect type fidelity loss when property type is an interface but runtime type is concrete
                var propertyType = prop.PropertyType;
                if (originalValue != null && propertyType.IsInterface)
                {
                    var runtimeType = originalValue.GetType();
                    if (!runtimeType.IsInterface)
                    {
                        activity?.AddWarning(
                            $"Property '{prop.Name}' at path: {propertyPath} has interface type {propertyType.Name} but contains {runtimeType.Name}",
                            ("type", "TypeFidelityLoss"),
                            ("path", propertyPath),
                            ("propertyName", prop.Name),
                            ("declaredType", propertyType.FullName),
                            ("runtimeType", runtimeType.FullName));
                    }
                }

                var copiedValue = DoDeepCopyAndApply(originalValue, propertyValueTransformer, depth + 1, prop, propertyPath, activity);

                object transformedValue;
                try
                {
                    transformedValue = propertyValueTransformer(prop, copiedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Transformer function threw an exception while processing property at path: {propertyPath}. " +
                        $"Property name: {prop.Name}, Property type: {prop.PropertyType.FullName}. See inner exception for details.",
                        ex);
                }

                copiedPropertyValues[prop] = transformedValue;
            }

            // Try to find a constructor with parameters matching all properties
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            var matchingConstructor = constructors.FirstOrDefault(ctor =>
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != properties.Length)
                    return false;

                // Check if all property names match constructor parameter names (case-insensitive)
                return properties.All(prop =>
                    parameters.Any(p => string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase)));
            });

            if (matchingConstructor != null)
            {
                // Use constructor-based initialization
                var parameters = matchingConstructor.GetParameters();
                var parameterValues = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var matchingProp = properties.First(p =>
                        string.Equals(p.Name, param.Name, StringComparison.OrdinalIgnoreCase));
                    parameterValues[i] = copiedPropertyValues[matchingProp];
                }

                return matchingConstructor.Invoke(parameterValues);
            }
            else
            {
                // Use parameterless constructor and property initialization
                // This works for both regular properties and init-only properties
                var parameterlessConstructor = type.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                if (parameterlessConstructor == null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' at path: {path} has no parameterless constructor and no constructor matching all properties");
                }

                var instance = parameterlessConstructor.Invoke(null);

                // Track read-only properties that will be lost
                var readOnlyProperties = properties.Where(p => !p.CanWrite).ToArray();
                if (readOnlyProperties.Length > 0)
                {
                    foreach (var readOnlyProp in readOnlyProperties)
                    {
                        activity?.AddWarning(
                            $"Read-only property '{readOnlyProp.Name}' at path: {path} cannot be set (no setter and no matching constructor)",
                            ("type", "ReadOnlyPropertyLost"),
                            ("path", path),
                            ("propertyName", readOnlyProp.Name),
                            ("propertyType", readOnlyProp.PropertyType.FullName));
                    }
                }

                // Set all writable properties
                foreach (var prop in properties.Where(p => p.CanWrite))
                {
                    prop.SetValue(instance, copiedPropertyValues[prop]);
                }

                return instance;
            }
        }
    }

}