using System;
using System.Collections.Generic;

#nullable enable

namespace DX.Shared.NoReflection
{
    /// <summary>
    /// Static class with extension methods for fast property mapping between objects WITHOUT using reflection.
    /// This version requires manual registration of mappers for each type pair.
    /// </summary>
    /// <remarks>
    /// Unlike the reflection-based version, this implementation requires explicit mapper registration
    /// for each type pair you want to map. This provides maximum performance with zero reflection overhead,
    /// but requires more code.
    /// 
    /// Usage:
    /// 1. Register mappers using MapperRegistry.Register methods
    /// 2. Use the same CopyTo, CopyFrom, CreateCopy, and CopyAll extension methods
    /// 
    /// Example:
    /// <code>
    /// // One-time registration (typically at app startup)
    /// MapperRegistry.Register&lt;Person, PersonDTO&gt;(
    ///     createFunc: p => new PersonDTO { FirstName = p.FirstName, LastName = p.LastName },
    ///     copyAction: (p, dto) => { dto.FirstName = p.FirstName; dto.LastName = p.LastName; }
    /// );
    /// 
    /// // Then use normally
    /// var dto = person.CreateCopyNoReflection&lt;Person, PersonDTO&gt;();
    /// </code>
    /// </remarks>
    public static class ClassClonatorNoReflection
    {
        /// <summary>
        /// Copies all properties from the input object to the output object using registered mapper.
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object</typeparam>
        /// <param name="input">The source object to copy properties from</param>
        /// <param name="output">The destination object to copy properties to</param>
        /// <returns>True if the copy was successful; false if either input or output is null</returns>
        /// <exception cref="InvalidOperationException">Thrown if no mapper is registered for the type pair</exception>
        public static bool CopyToNoReflection<TInput, TOutput>(this TInput input, TOutput output)
        {
            return MapperRegistry<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Copies all properties from the input object to the current object using registered mapper.
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object</typeparam>
        /// <param name="output">The destination object (current instance) to copy properties to</param>
        /// <param name="input">The source object to copy properties from</param>
        /// <returns>True if the copy was successful; false if either input or output is null</returns>
        /// <exception cref="InvalidOperationException">Thrown if no mapper is registered for the type pair</exception>
        public static bool CopyFromNoReflection<TInput, TOutput>(this TOutput output, TInput input)
        {
            return MapperRegistry<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Creates a new instance of <typeparamref name="TOutput"/> and copies all properties using registered mapper.
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object</typeparam>
        /// <param name="input">The source object to copy properties from</param>
        /// <returns>A new instance of <typeparamref name="TOutput"/> with properties copied from the input</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        /// <exception cref="InvalidOperationException">Thrown if no mapper is registered for the type pair</exception>
        public static TOutput CreateCopyNoReflection<TInput, TOutput>(this TInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return MapperRegistry<TInput, TOutput>.CreateCopy(input);
        }

        /// <summary>
        /// Creates a copy of each object in the input collection using registered mapper.
        /// </summary>
        /// <typeparam name="TInput">The type of the source objects</typeparam>
        /// <typeparam name="TOutput">The type of the destination objects</typeparam>
        /// <param name="inputArr">The collection of source objects to copy</param>
        /// <returns>A lazily-evaluated sequence of new instances with copied properties. Null items are skipped.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no mapper is registered for the type pair</exception>
        public static IEnumerable<TOutput> CopyAllNoReflection<TInput, TOutput>(this IEnumerable<TInput> inputArr)
        {
            foreach (TInput input in inputArr)
            {
                if (input is not null)
                {
                    yield return MapperRegistry<TInput, TOutput>.CreateCopy(input);
                }
            }
        }
    }

    /// <summary>
    /// Registry for storing and retrieving mappers between type pairs.
    /// All mappers must be registered before use.
    /// </summary>
    /// <remarks>
    /// This class is thread-safe after registration. Register all mappers during application startup.
    /// </remarks>
    public static class MapperRegistry
    {
        /// <summary>
        /// Registers a mapper between two types.
        /// </summary>
        /// <typeparam name="TInput">The source type</typeparam>
        /// <typeparam name="TOutput">The destination type</typeparam>
        /// <param name="createFunc">Function that creates a new TOutput from TInput</param>
        /// <param name="copyAction">Action that copies properties from TInput to existing TOutput</param>
        /// <example>
        /// <code>
        /// MapperRegistry.Register&lt;Person, PersonDTO&gt;(
        ///     createFunc: p => new PersonDTO 
        ///     { 
        ///         FirstName = p.FirstName, 
        ///         LastName = p.LastName,
        ///         Age = p.Age
        ///     },
        ///     copyAction: (p, dto) => 
        ///     { 
        ///         dto.FirstName = p.FirstName; 
        ///         dto.LastName = p.LastName;
        ///         dto.Age = p.Age;
        ///     }
        /// );
        /// </code>
        /// </example>
        public static void Register<TInput, TOutput>(
            Func<TInput, TOutput> createFunc,
            Action<TInput, TOutput> copyAction)
        {
            MapperRegistry<TInput, TOutput>.Register(createFunc, copyAction);
        }
    }

    /// <summary>
    /// Internal registry for a specific type pair.
    /// </summary>
    /// <typeparam name="TInput">The source type</typeparam>
    /// <typeparam name="TOutput">The destination type</typeparam>
    internal static class MapperRegistry<TInput, TOutput>
    {
        private static Func<TInput, TOutput>? _createFunc;
        private static Action<TInput, TOutput>? _copyAction;
        private static bool _isRegistered = false;

        /// <summary>
        /// Registers the mapper functions for this type pair.
        /// </summary>
        public static void Register(Func<TInput, TOutput> createFunc, Action<TInput, TOutput> copyAction)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _copyAction = copyAction ?? throw new ArgumentNullException(nameof(copyAction));
            _isRegistered = true;
        }

        /// <summary>
        /// Creates a new instance of TOutput from TInput.
        /// </summary>
        public static TOutput CreateCopy(TInput input)
        {
            if (!_isRegistered || _createFunc is null)
            {
                throw new InvalidOperationException(
                    $"No mapper registered for {typeof(TInput).Name} -> {typeof(TOutput).Name}. " +
                    $"Call MapperRegistry.Register<{typeof(TInput).Name}, {typeof(TOutput).Name}>() first.");
            }
            return _createFunc(input);
        }

        /// <summary>
        /// Copies properties from input to output.
        /// </summary>
        public static bool CopyTo(TInput input, TOutput output)
        {
            if (input is null || output is null)
            {
                return false;
            }

            if (!_isRegistered || _copyAction is null)
            {
                throw new InvalidOperationException(
                    $"No mapper registered for {typeof(TInput).Name} -> {typeof(TOutput).Name}. " +
                    $"Call MapperRegistry.Register<{typeof(TInput).Name}, {typeof(TOutput).Name}>() first.");
            }

            _copyAction(input, output);
            return true;
        }
    }
}
