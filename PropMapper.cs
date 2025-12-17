using System;
using System.Collections.Generic;

#nullable enable

namespace DX.Shared
{
    /// <summary>
    /// Static class with Extension Methods for cloning objects.
    /// This version uses compile-time code generation via source generators.
    /// No reflection is used - all mapping code is generated at compile time.
    /// </summary>
    public static class ClassClonator
    {
        /// <summary>
        /// Copy readable Properties to the output Instances writable properties 
        /// matched by equal Name
        /// </summary>
        public static bool CopyTo<TInput, TOutput>(this TInput input, TOutput output)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Copy readable Properties from the input Instances to the writable properties 
        /// matched by equal Name
        /// </summary>
        public static bool CopyFrom<TInput, TOutput>(this TOutput output, TInput input)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Create a new Object and Copy all readable Properties to the returned Instances writable properties 
        /// matched by equal Name
        /// </summary>
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
        public static IEnumerable<TOutput> CopyAll<TInput, TOutput>(this IEnumerable<TInput> inputArr) where TOutput : new()
        {
            foreach (TInput input in inputArr)
            {
                if (input is not null)
                {
                    yield return CloneMapper<TInput, TOutput>.From(input);
                }
            }
        }
    }

    /// <summary>
    /// Maps properties from TInput to a new TOutput instance.
    /// Uses compile-time generated mappers - no reflection at runtime.
    /// </summary>
    internal static class CloneMapper<TInput, TOutput> where TOutput : new()
    {
        private static readonly IMapper<TInput, TOutput>? mapper = MapperRegistry.GetCloneMapper<TInput, TOutput>();
        
        public static TOutput From(TInput input)
        {
            if (mapper != null)
            {
                return mapper.Map(input);
            }
            
            // If no mapper was generated, it means no properties matched or the source generator didn't run
            // This is likely a compile-time configuration issue
            throw new InvalidOperationException(
                $"No mapper was generated for {typeof(TInput).Name} -> {typeof(TOutput).Name}. " +
                "Ensure the source generator is properly configured and the types have matching public properties.");
        }
    }

    /// <summary>
    /// Maps properties from TInput to an existing TOutput instance.
    /// Uses compile-time generated mappers - no reflection at runtime.
    /// </summary>
    internal static class CopyMapper<TInput, TOutput>
    {
        private static readonly ICopyMapper<TInput, TOutput>? mapper = MapperRegistry.GetCopyMapper<TInput, TOutput>();
        
        public static bool CopyTo(TInput input, TOutput output)
        {
            if (input is null || output is null)
            {
                return false;
            }
            
            if (mapper != null)
            {
                mapper.CopyTo(input, output);
                return true;
            }
            
            // If no mapper was generated, it means no properties matched or the source generator didn't run
            throw new InvalidOperationException(
                $"No mapper was generated for {typeof(TInput).Name} -> {typeof(TOutput).Name}. " +
                "Ensure the source generator is properly configured and the types have matching public properties.");
        }
    }

    /// <summary>
    /// Interface for mappers that create new instances
    /// </summary>
    internal interface IMapper<TInput, TOutput>
    {
        TOutput Map(TInput input);
    }

    /// <summary>
    /// Interface for mappers that copy to existing instances
    /// </summary>
    internal interface ICopyMapper<TInput, TOutput>
    {
        void CopyTo(TInput input, TOutput output);
    }

    /// <summary>
    /// Registry of generated mappers. Populated by source generator.
    /// </summary>
    internal static partial class MapperRegistry
    {
        // These will be implemented by the source generator
        public static partial IMapper<TInput, TOutput>? GetCloneMapper<TInput, TOutput>() where TOutput : new();
        public static partial ICopyMapper<TInput, TOutput>? GetCopyMapper<TInput, TOutput>();
    }
}
