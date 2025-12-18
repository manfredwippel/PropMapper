using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SkiaSharp;

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

        /// <summary>
        /// Generates a visual diagram image showing the property mappings between source and destination types.
        /// The image displays source properties on the left, destination properties on the right, 
        /// and lines connecting matching properties.
        /// </summary>
        /// <typeparam name="TInput">The source type</typeparam>
        /// <typeparam name="TOutput">The destination type</typeparam>
        /// <param name="outputPath">The file path where the image will be saved (supports .png, .jpg, .bmp formats)</param>
        /// <param name="width">The width of the generated image in pixels (default: 800)</param>
        /// <param name="height">The height of the generated image in pixels (default: 600)</param>
        /// <exception cref="ArgumentNullException">Thrown when outputPath is null</exception>
        /// <exception cref="ArgumentException">Thrown when outputPath is empty or whitespace</exception>
        /// <example>
        /// <code>
        /// ClassClonator.GenerateMappingImage&lt;Person, PersonDTO&gt;("mapping.png");
        /// </code>
        /// </example>
        public static void GenerateMappingImage<TInput, TOutput>(string outputPath, int width = 800, int height = 600)
        {
            if (outputPath is null)
            {
                throw new ArgumentNullException(nameof(outputPath));
            }
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty or whitespace.", nameof(outputPath));
            }

            var sourceProps = PropertyCache<TInput>.ReadProps.ToList();
            var destProps = PropertyCache<TOutput>.WriteProps.ToList();
            
            // Find matching properties
            var matchingProps = sourceProps.Join(destProps,
                sp => sp.Name,
                dp => dp.Name,
                (sp, dp) => new { Source = sp, Dest = dp }).ToList();

            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.White);

                // Draw title
                using (var titlePaint = new SKPaint())
                {
                    titlePaint.Color = SKColors.Black;
                    titlePaint.TextSize = 24;
                    titlePaint.IsAntialias = true;
                    titlePaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);

                    string title = $"Property Mapping: {typeof(TInput).Name} → {typeof(TOutput).Name}";
                    float titleWidth = titlePaint.MeasureText(title);
                    canvas.DrawText(title, (width - titleWidth) / 2, 30, titlePaint);
                }

                // Calculate layout
                int leftX = 50;
                int rightX = width - 250;
                int startY = 80;
                int lineHeight = 25;
                
                using (var textPaint = new SKPaint())
                using (var matchPaint = new SKPaint())
                using (var highlightPaint = new SKPaint())
                using (var headerPaint = new SKPaint())
                {
                    // Configure paints
                    textPaint.Color = SKColors.Black;
                    textPaint.TextSize = 14;
                    textPaint.IsAntialias = true;
                    textPaint.Typeface = SKTypeface.FromFamilyName("Arial");

                    matchPaint.Color = SKColors.Green;
                    matchPaint.StrokeWidth = 2;
                    matchPaint.IsAntialias = true;
                    matchPaint.Style = SKPaintStyle.Stroke;

                    highlightPaint.Color = new SKColor(144, 238, 144, 128); // Light green with transparency
                    highlightPaint.Style = SKPaintStyle.Fill;

                    headerPaint.Color = SKColors.Black;
                    headerPaint.TextSize = 16;
                    headerPaint.IsAntialias = true;
                    headerPaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);

                    // Draw type headers
                    canvas.DrawText($"Source: {typeof(TInput).Name}", leftX, startY - 30, headerPaint);
                    canvas.DrawText($"Destination: {typeof(TOutput).Name}", rightX, startY - 30, headerPaint);

                    // Draw source properties
                    for (int i = 0; i < sourceProps.Count; i++)
                    {
                        var prop = sourceProps[i];
                        string propText = $"{prop.Name} : {GetSimpleTypeName(prop.PropertyType)}";
                        float y = startY + i * lineHeight;
                        
                        // Highlight matched properties
                        bool isMatched = matchingProps.Any(m => m.Source.Name == prop.Name);
                        if (isMatched)
                        {
                            canvas.DrawRect(leftX - 5, y - 15, 200, lineHeight - 5, highlightPaint);
                        }
                        
                        canvas.DrawText(propText, leftX, y, textPaint);
                    }

                    // Draw destination properties
                    for (int i = 0; i < destProps.Count; i++)
                    {
                        var prop = destProps[i];
                        string propText = $"{prop.Name} : {GetSimpleTypeName(prop.PropertyType)}";
                        float y = startY + i * lineHeight;
                        
                        // Highlight matched properties
                        bool isMatched = matchingProps.Any(m => m.Dest.Name == prop.Name);
                        if (isMatched)
                        {
                            canvas.DrawRect(rightX - 5, y - 15, 200, lineHeight - 5, highlightPaint);
                        }
                        
                        canvas.DrawText(propText, rightX, y, textPaint);
                    }

                    // Draw connection lines for matched properties
                    foreach (var match in matchingProps)
                    {
                        int sourceIndex = sourceProps.IndexOf(match.Source);
                        int destIndex = destProps.IndexOf(match.Dest);
                        
                        float y1 = startY + sourceIndex * lineHeight - 5;
                        float y2 = startY + destIndex * lineHeight - 5;
                        
                        canvas.DrawLine(leftX + 190, y1, rightX - 10, y2, matchPaint);
                    }
                }

                // Draw statistics
                using (var statsPaint = new SKPaint())
                {
                    statsPaint.Color = SKColors.DarkGray;
                    statsPaint.TextSize = 12;
                    statsPaint.IsAntialias = true;
                    statsPaint.Typeface = SKTypeface.FromFamilyName("Arial");

                    string stats = $"Matched: {matchingProps.Count} | Source: {sourceProps.Count} | Destination: {destProps.Count}";
                    canvas.DrawText(stats, 50, height - 30, statsPaint);
                }

                // Save the image
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                using (var image = surface.Snapshot())
                using (var data = image.Encode(GetSkiaImageFormat(outputPath), 100))
                using (var stream = File.OpenWrite(outputPath))
                {
                    data.SaveTo(stream);
                }
            }
        }

        /// <summary>
        /// Gets a simplified type name for display purposes.
        /// </summary>
        private static string GetSimpleTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(long)) return "long";
            if (type == typeof(DateTime)) return "DateTime";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return GetSimpleTypeName(type.GetGenericArguments()[0]) + "?";
            }
            return type.Name;
        }

        /// <summary>
        /// Determines the image format based on file extension.
        /// </summary>
        private static SKEncodedImageFormat GetSkiaImageFormat(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".gif" => SKEncodedImageFormat.Gif,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };
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