using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#nullable enable

namespace DX.Shared
{
    /// <summary>
    /// Static class with Extension Methods for cloning objects
    /// </summary>
    public static class ClassClonator
    {
        /// <summary>
        /// Copy readable Properties to the output Instances writable properties 
        /// matched by equal Name
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="input">must not be null</param>
        /// <param name="output">must not be null</param>
        /// <returns></returns>
        public static bool CopyTo<TInput, TOutput>(this TInput input, TOutput output)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Copy readable Properties from the input Instances to the writable properties 
        /// matched by equal Name
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        public static bool CopyFrom<TInput, TOutput>(this TOutput output, TInput input)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Create a new Object and Copy all readable Properties to the returned Instances writable properties 
        /// matched by equal Name
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static TOutput CreateCopy<TInput, TOutput>(this TInput input) where TOutput : new()
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return CloneMapper<TInput, TOutput>.From(input);
        }

        /// <summary>
        /// returns a Copy for each Object in the input stream
        /// Copy all readable Properties to the returned Instances writable properties 
        /// matched by equal Name
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput">must have a parameterless Constructor : new() </typeparam>
        /// <param name="inputArr">must not be null</param>
        /// <returns></returns>
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

        public static IEnumerable<PropertyInfo> ReadProps => readProps;

        public static IEnumerable<PropertyInfo> WriteProps => writeProps;

    }

    /// <summary>
    /// clones object public properties to a new object
    /// uses Expressions (compiled and saved to static) - faster than Reflection
    /// (compilation happens with every new generic type call cause it's a new static class each time)
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>

    internal class CloneMapper<TInput, TOutput> where TOutput : new()
    {
        private static readonly Func<TInput, TOutput> cloner;
        
        static CloneMapper()
        {
            cloner = CreateCloner();
        }

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

        public static TOutput From(TInput input)
        {
            return cloner(input);
        }
    }


    /// <summary>
    /// clones object public properties to an existing object
    /// uses Expressions (compiled and saved to static) - faster than Reflection
    /// (compilation happens with every new generic type call cause it's a new static class each time)
    /// </summary>
    internal static class CopyMapper<TInput, TOutput>
    {
        private static readonly Action<TInput, TOutput> copier;

        static CopyMapper()
        {
            copier = CreateCopier();
        }

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