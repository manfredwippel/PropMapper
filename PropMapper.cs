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
    /// 静态类，提供对象间快速属性映射的扩展方法。
    /// 使用编译的表达式树以获得最佳性能。
    /// </summary>
    /// <remarks>
    /// This class provides high-performance object-to-object mapping through extension methods.
    /// The first mapping between two specific types has a small overhead due to expression compilation,
    /// but subsequent calls are extremely fast due to cached compiled expressions.
    /// All operations are thread-safe.
    /// 此类通过扩展方法提供高性能的对象到对象映射。
    /// 两个特定类型之间的首次映射由于表达式编译会有少量开销，
    /// 但由于缓存了编译的表达式，后续调用非常快。
    /// 所有操作都是线程安全的。
    /// </remarks>
    public static class ClassClonator
    {
        /// <summary>
        /// Copies all readable properties from the input object to the output object's writable properties,
        /// matched by property name (case-sensitive).
        /// 将输入对象的所有可读属性复制到输出对象的可写属性，
        /// 按属性名称匹配（区分大小写）。
        /// </summary>
        /// <typeparam name="TInput">The type of the source object / 源对象的类型</typeparam>
        /// <typeparam name="TOutput">The type of the destination object / 目标对象的类型</typeparam>
        /// <param name="input">The source object to copy properties from / 要复制属性的源对象</param>
        /// <param name="output">The destination object to copy properties to / 要复制属性到的目标对象</param>
        /// <returns>True if the copy was successful; false if either input or output is null / 如果复制成功则返回 true；如果输入或输出为 null 则返回 false</returns>
        /// <example>
        /// <code>
        /// var person = new Person { FirstName = "John", LastName = "Doe" };
        /// var dto = new PersonDTO();
        /// person.CopyTo(dto); // Copies matching properties / 复制匹配的属性
        /// </code>
        /// </example>
        public static bool CopyTo<TInput, TOutput>(this TInput input, TOutput output)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Copies all readable properties from the input object to the current object's writable properties,
        /// matched by property name (case-sensitive). This is the inverse of <see cref="CopyTo{TInput,TOutput}"/>.
        /// 将输入对象的所有可读属性复制到当前对象的可写属性，
        /// 按属性名称匹配（区分大小写）。这是 <see cref="CopyTo{TInput,TOutput}"/> 的反向操作。
        /// </summary>
        /// <typeparam name="TInput">The type of the source object / 源对象的类型</typeparam>
        /// <typeparam name="TOutput">The type of the destination object / 目标对象的类型</typeparam>
        /// <param name="output">The destination object (current instance) to copy properties to / 要复制属性到的目标对象（当前实例）</param>
        /// <param name="input">The source object to copy properties from / 要复制属性的源对象</param>
        /// <returns>True if the copy was successful; false if either input or output is null / 如果复制成功则返回 true；如果输入或输出为 null 则返回 false</returns>
        /// <example>
        /// <code>
        /// var dto = new PersonDTO();
        /// var person = new Person { FirstName = "John", LastName = "Doe" };
        /// dto.CopyFrom(person); // Copies matching properties / 复制匹配的属性
        /// </code>
        /// </example>
        public static bool CopyFrom<TInput, TOutput>(this TOutput output, TInput input)
        {
            return CopyMapper<TInput, TOutput>.CopyTo(input, output);
        }

        /// <summary>
        /// Creates a new instance of <typeparamref name="TOutput"/> and copies all readable properties 
        /// from the input object to the new instance's writable properties, matched by property name (case-sensitive).
        /// 创建 <typeparamref name="TOutput"/> 的新实例，并将输入对象的所有可读属性
        /// 复制到新实例的可写属性，按属性名称匹配（区分大小写）。
        /// </summary>
        /// <typeparam name="TInput">The type of the source object / 源对象的类型</typeparam>
        /// <typeparam name="TOutput">The type of the destination object (must have a parameterless constructor) / 目标对象的类型（必须有无参数构造函数）</typeparam>
        /// <param name="input">The source object to copy properties from / 要复制属性的源对象</param>
        /// <returns>A new instance of <typeparamref name="TOutput"/> with properties copied from the input / 包含从输入复制的属性的 <typeparamref name="TOutput"/> 新实例</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null / 当输入为 null 时抛出</exception>
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
        /// 为输入集合中的每个对象创建副本。对于每个输入对象，创建 <typeparamref name="TOutput"/> 的新实例，
        /// 并将所有可读属性复制到新实例的可写属性，按属性名称匹配（区分大小写）。
        /// </summary>
        /// <typeparam name="TInput">The type of the source objects / 源对象的类型</typeparam>
        /// <typeparam name="TOutput">The type of the destination objects (must have a parameterless constructor) / 目标对象的类型（必须有无参数构造函数）</typeparam>
        /// <param name="inputArr">The collection of source objects to copy / 要复制的源对象集合</param>
        /// <returns>A lazily-evaluated sequence of new instances with copied properties. Null items in the input are skipped. / 延迟计算的新实例序列，包含复制的属性。输入中的 null 项将被跳过。</returns>
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
                    // Skip null items / 跳过 null 项
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
    /// 类型的属性信息内部缓存。
    /// 分别存储可读和可写属性以优化属性匹配。
    /// </summary>
    /// <typeparam name="T">The type to cache property information for / 要缓存属性信息的类型</typeparam>
    internal static class PropertyCache<T>
    {
        static readonly IEnumerable<PropertyInfo> writeProps;

        static readonly IEnumerable<PropertyInfo> readProps;

        static PropertyCache()
        {
            // Get all readable public instance properties / 获取所有公共实例属性中可读的属性
            readProps = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanRead);
            // Get all writable public instance properties / 获取所有公共实例属性中可写的属性
            writeProps = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanWrite);
        }

        /// <summary>
        /// Gets all public instance properties that can be read from type <typeparamref name="T"/>.
        /// 获取类型 <typeparamref name="T"/> 的所有可读公共实例属性。
        /// </summary>
        public static IEnumerable<PropertyInfo> ReadProps => readProps;

        /// <summary>
        /// Gets all public instance properties that can be written to on type <typeparamref name="T"/>.
        /// 获取类型 <typeparamref name="T"/> 的所有可写公共实例属性。
        /// </summary>
        public static IEnumerable<PropertyInfo> WriteProps => writeProps;

    }

    /// <summary>
    /// Internal mapper that creates new instances and copies properties.
    /// Uses compiled Expression trees for high performance.
    /// 创建新实例并复制属性的内部映射器。
    /// 使用编译的表达式树以获得高性能。
    /// </summary>
    /// <typeparam name="TInput">The source type / 源类型</typeparam>
    /// <typeparam name="TOutput">The destination type (must have a parameterless constructor) / 目标类型（必须有无参数构造函数）</typeparam>
    /// <remarks>
    /// This class compiles and caches an expression tree in its static constructor.
    /// Each unique combination of TInput and TOutput creates a separate static instance.
    /// The compiled expression is cached for extremely fast subsequent calls.
    /// 此类在静态构造函数中编译并缓存表达式树。
    /// TInput 和 TOutput 的每个唯一组合都会创建一个单独的静态实例。
    /// 编译的表达式被缓存，使后续调用极快。
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
        /// 创建并编译一个表达式树，该树实例化一个新的 TOutput 对象
        /// 并从 TInput 分配所有匹配的属性。
        /// </summary>
        /// <returns>A compiled function that performs the mapping / 执行映射的编译函数</returns>
        private static Func<TInput, TOutput> CreateCloner()
        {
            ParameterExpression input = Expression.Parameter(typeof(TInput), "input");
            // Match properties by name - for each destination property, find a source property with the same name
            // 按名称匹配属性 - 对于每个目标属性，查找具有相同名称的源属性
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
        /// 创建 <typeparamref name="TOutput"/> 的新实例并从输入复制所有匹配的属性。
        /// </summary>
        /// <param name="input">The source object / 源对象</param>
        /// <returns>A new instance with copied properties / 包含复制属性的新实例</returns>
        public static TOutput From(TInput input)
        {
            return cloner(input);
        }
    }


    /// <summary>
    /// Internal mapper that copies properties between existing objects.
    /// Uses compiled Expression trees for high performance.
    /// 在现有对象之间复制属性的内部映射器。
    /// 使用编译的表达式树以获得高性能。
    /// </summary>
    /// <typeparam name="TInput">The source type / 源类型</typeparam>
    /// <typeparam name="TOutput">The destination type / 目标类型</typeparam>
    /// <remarks>
    /// This class compiles and caches an expression tree in its static constructor.
    /// Each unique combination of TInput and TOutput creates a separate static instance.
    /// The compiled expression is cached for extremely fast subsequent calls.
    /// 此类在静态构造函数中编译并缓存表达式树。
    /// TInput 和 TOutput 的每个唯一组合都会创建一个单独的静态实例。
    /// 编译的表达式被缓存，使后续调用极快。
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
        /// 创建并编译一个表达式树，将输入对象的所有匹配属性
        /// 分配给输出对象。
        /// </summary>
        /// <returns>A compiled action that performs the property copying / 执行属性复制的编译操作</returns>
        private static Action<TInput, TOutput> CreateCopier()
        {
            ParameterExpression input = Expression.Parameter(typeof(TInput), "input");
            ParameterExpression output = Expression.Parameter(typeof(TOutput), "output");

            // Match properties by name - for each destination property, find a source property with the same name
            // 按名称匹配属性 - 对于每个目标属性，查找具有相同名称的源属性
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
        /// 将输入对象的所有匹配属性复制到输出对象。
        /// </summary>
        /// <param name="input">The source object to copy from / 要从中复制的源对象</param>
        /// <param name="output">The destination object to copy to / 要复制到的目标对象</param>
        /// <returns>True if successful; false if either parameter is null / 如果成功则返回 true；如果任一参数为 null 则返回 false</returns>
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