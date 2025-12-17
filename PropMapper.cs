using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#nullable enable

namespace DX.Shared
{
    /// <summary>
    /// Static class with extension methods for fast property mapping between objects.
    /// Uses compiled Expression trees for optimal performance.
    /// </summary>
    /// <remarks>
    /// This class provides high-performance object-to-object mapping through extension methods.
    /// The first mapping between two specific types has a small overhead due to expression compilation,
    /// but subsequent calls are extremely fast due to cached compiled expressions.
    /// All operations are thread-safe.
    /// </remarks>
    public static class ClassClonator
    {
        /// <summary>
        /// Copies all readable properties from the input object to the output object's writable properties,
        /// matched by property name (case-sensitive).
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object</typeparam>
        /// <param name="input">The source object to copy properties from</param>
        /// <param name="output">The destination object to copy properties to</param>
        /// <returns>True if the copy was successful; false if either input or output is null</returns>
        /// <example>
        /// <code>
        /// var person = new Person { FirstName = "John", LastName = "Doe" };
        /// var dto = new PersonDTO();
        /// person.CopyTo(dto); // Copies matching properties
        /// </code>
        /// </example>
        public static bool CopyTo<TInput, TOutput>(this TInput input, TOutput output)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Copies all readable properties from the input object to the current object's writable properties,
        /// matched by property name (case-sensitive). This is the inverse of <see cref="CopyTo{TInput,TOutput}"/>.
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object</typeparam>
        /// <param name="output">The destination object (current instance) to copy properties to</param>
        /// <param name="input">The source object to copy properties from</param>
        /// <returns>True if the copy was successful; false if either input or output is null</returns>
        /// <example>
        /// <code>
        /// var dto = new PersonDTO();
        /// var person = new Person { FirstName = "John", LastName = "Doe" };
        /// dto.CopyFrom(person); // Copies matching properties
        /// </code>
        /// </example>
        public static bool CopyFrom<TInput, TOutput>(this TOutput output, TInput input)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Creates a new instance of <typeparamref name="TOutput"/> and copies all readable properties 
        /// from the input object to the new instance's writable properties, matched by property name (case-sensitive).
        /// </summary>
        /// <typeparam name="TInput">The type of the source object</typeparam>
        /// <typeparam name="TOutput">The type of the destination object (must have a parameterless constructor)</typeparam>
        /// <param name="input">The source object to copy properties from</param>
        /// <returns>A new instance of <typeparamref name="TOutput"/> with properties copied from the input</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null</exception>
        /// <example>
        /// <code>
        /// var person = new Person { FirstName = "John", LastName = "Doe" };
        /// var dto = person.CreateCopy&lt;Person, PersonDTO&gt;();
        /// </code>
        /// </example>
        public static TOutput CreateCopy<TInput, TOutput>(this TInput input) where TOutput : new()
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return CloneMapper<TInput, TOutput>.From(input);
        }

        /// <summary>
        /// Creates a copy of each object in the input collection. For each input object, creates a new instance
        /// of <typeparamref name="TOutput"/> and copies all readable properties to the new instance's writable properties,
        /// matched by property name (case-sensitive).
        /// </summary>
        /// <typeparam name="TInput">The type of the source objects</typeparam>
        /// <typeparam name="TOutput">The type of the destination objects (must have a parameterless constructor)</typeparam>
        /// <param name="inputArr">The collection of source objects to copy</param>
        /// <returns>A lazily-evaluated sequence of new instances with copied properties. Null items in the input are skipped.</returns>
        /// <example>
        /// <code>
        /// List&lt;Person&gt; people = GetPeople();
        /// IEnumerable&lt;PersonDTO&gt; dtos = people.CopyAll&lt;Person, PersonDTO&gt;();
        /// </code>
        /// </example>
        public static IEnumerable<TOutput> CopyAll<TInput, TOutput>(this IEnumerable<TInput> inputArr) where TOutput : new()
        {
            foreach (TInput input in inputArr)
            {
                if (input is null)
                {
                    //System.Diagnostics.Debug.Fail($"Input was null {typeof(TInput)}");
                }
                else
                {
                    yield return CloneMapper<TInput, TOutput>.From(input);
                }
            }
        }
    }

    /// <summary>
    /// Internal cache for property information of a type. 
    /// Stores readable and writable properties separately to optimize property matching.
    /// </summary>
    /// <typeparam name="T">The type to cache property information for</typeparam>
    internal static class PropertyCache<T>
    {
        static readonly IEnumerable<PropertyInfo> writeProps;

        static readonly IEnumerable<PropertyInfo> readProps;

        static PropertyCache()
        {
            readProps = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanRead);
            writeProps = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanWrite);
        }

        /// <summary>
        /// Gets all public instance properties that can be read from type <typeparamref name="T"/>.
        /// </summary>
        public static IEnumerable<PropertyInfo> ReadProps => readProps;

        /// <summary>
        /// Gets all public instance properties that can be written to on type <typeparamref name="T"/>.
        /// </summary>
        public static IEnumerable<PropertyInfo> WriteProps => writeProps;

    }

    /// <summary>
    /// Internal mapper that creates new instances and copies properties.
    /// Uses compiled Expression trees for high performance.
    /// </summary>
    /// <typeparam name="TInput">The source type</typeparam>
    /// <typeparam name="TOutput">The destination type (must have a parameterless constructor)</typeparam>
    /// <remarks>
    /// This class compiles and caches an expression tree in its static constructor.
    /// Each unique combination of TInput and TOutput creates a separate static instance.
    /// The compiled expression is cached for extremely fast subsequent calls.
    /// </remarks>
    internal class CloneMapper<TInput, TOutput> where TOutput : new()
    {
        private static readonly Func<TInput, TOutput> cloner;
        
        static CloneMapper()
        {
            cloner = CreateCloner();
        }

        /// <summary>
        /// Creates and compiles an expression tree that instantiates a new TOutput object
        /// and assigns all matching properties from TInput.
        /// </summary>
        /// <returns>A compiled function that performs the mapping</returns>
        private static Func<TInput, TOutput> CreateCloner()
        {
            ParameterExpression input = Expression.Parameter(typeof(TInput), "input");
            // For each property that exists in the destination object, is there a property with the same name in the source object?
            IEnumerable<MemberBinding> memberBindings = PropertyCache<TInput>.ReadProps.Join(PropertyCache<TOutput>.WriteProps,
                sourceProperty => sourceProperty.Name,
                destinationProperty => destinationProperty.Name,
                (sourceProperty, destinationProperty) =>
                    (MemberBinding)Expression.Bind(destinationProperty,
                        Expression.Property(input, sourceProperty)));

            MemberInitExpression body = Expression.MemberInit(Expression.New(typeof(TOutput)), memberBindings);
            Expression<Func<TInput, TOutput>> lambda = Expression.Lambda<Func<TInput, TOutput>>(body, input);
            return lambda.Compile();
        }

        /// <summary>
        /// Creates a new instance of <typeparamref name="TOutput"/> and copies all matching properties from the input.
        /// </summary>
        /// <param name="input">The source object</param>
        /// <returns>A new instance with copied properties</returns>
        public static TOutput From(TInput input)
        {
            return cloner(input);
        }
    }


    /// <summary>
    /// Internal mapper that copies properties between existing objects.
    /// Uses compiled Expression trees for high performance.
    /// </summary>
    /// <typeparam name="TInput">The source type</typeparam>
    /// <typeparam name="TOutput">The destination type</typeparam>
    /// <remarks>
    /// This class compiles and caches an expression tree in its static constructor.
    /// Each unique combination of TInput and TOutput creates a separate static instance.
    /// The compiled expression is cached for extremely fast subsequent calls.
    /// </remarks>
    internal static class CopyMapper<TInput, TOutput>
    {
        private static readonly Action<TInput, TOutput> copier;

        static CopyMapper()
        {
            copier = CreateCopier();
        }

        /// <summary>
        /// Creates and compiles an expression tree that assigns all matching properties 
        /// from the input object to the output object.
        /// </summary>
        /// <returns>A compiled action that performs the property copying</returns>
        private static Action<TInput, TOutput> CreateCopier()
        {
            ParameterExpression input = Expression.Parameter(typeof(TInput), "input");
            ParameterExpression output = Expression.Parameter(typeof(TOutput), "output");

            // For each property that exists in the destination object, is there a property with the same name in the source object?
            IEnumerable<BinaryExpression> memberAssignments = PropertyCache<TInput>.ReadProps.Join(PropertyCache<TOutput>.WriteProps, 
                    sourceProperty => sourceProperty.Name,
                    destinationProperty => destinationProperty.Name,
                    (sourceProperty, destinationProperty) =>
                        Expression.Assign(
                                Expression.Property(output, destinationProperty), 
                                Expression.Property(input, sourceProperty)
                        )
                );

            BlockExpression body = Expression.Block(memberAssignments);
            Expression<Action<TInput, TOutput>> lambda = Expression.Lambda<Action<TInput, TOutput>>(body, input, output);
            return lambda.Compile();
        }

        /// <summary>
        /// Copies all matching properties from the input object to the output object.
        /// </summary>
        /// <param name="input">The source object to copy from</param>
        /// <param name="output">The destination object to copy to</param>
        /// <returns>True if successful; false if either parameter is null</returns>
        public static bool CopyTo(TInput input, TOutput output)
        {
            if(input is null || output is null)
            {
                return false;
            }
            copier(input, output);
            return true;
        }
    }
}