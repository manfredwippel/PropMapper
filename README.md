# PropMapper

Property mapper for .NET. Flat and basic, but **VERY FAST**.

Just [one cs-file](PropMapper.cs), approximately 193 lines of code with comprehensive XML documentation.

## Installation

Drop the cs-file into your project OR install via [Nuget](https://www.nuget.org/packages/PropMapper/)

`Install-Package PropMapper`

(this will simply add the .cs file to your project, not a DLL-reference, so you're free of dependencies)

## Usage

The library provides extension methods through the `ClassClonator` static class. There are several ways to use it:

### Creating a new object (Clone)

```cs
// Create a new object and copy all matching properties
var destination = sourceObject.CreateCopy<SourceType, DestType>();
```

### Copying to an existing object

```cs
// Copy properties from source to existing destination object
sourceObject.CopyTo(destinationObject);

// Or use CopyFrom on the destination object
destinationObject.CopyFrom(sourceObject);
```

### Copying collections

```cs
// Copy all objects in a collection
IEnumerable<DestType> destinations = sourceCollection.CopyAll<SourceType, DestType>();
```

## API Reference

### Extension Methods

All methods are available as extension methods on any object:

#### `CreateCopy<TInput, TOutput>()`
Creates a new instance of `TOutput` and copies all matching properties from the input object.
- **Returns**: New instance of `TOutput` with copied properties
- **Constraints**: `TOutput` must have a parameterless constructor
- **Throws**: `ArgumentNullException` if input is null

#### `CopyTo<TInput, TOutput>(TOutput output)`
Copies all matching properties from the input object to an existing output object.
- **Returns**: `bool` - `true` if successful, `false` if either input or output is null

#### `CopyFrom<TInput, TOutput>(TInput input)`
Copies all matching properties from the input object to the current object (inverse of `CopyTo`).
- **Returns**: `bool` - `true` if successful, `false` if either input or output is null

#### `CopyAll<TInput, TOutput>(IEnumerable<TInput>)`
Copies all objects in a collection to new instances.
- **Returns**: `IEnumerable<TOutput>` - Lazily evaluated sequence of copied objects
- **Constraints**: `TOutput` must have a parameterless constructor
- **Note**: Null items in the input collection are skipped

### Static Methods

#### `GenerateMappingImage<TInput, TOutput>(string outputPath, int width = 800, int height = 600)`
Generates a visual diagram showing property mappings between types.
- **Parameters**:
  - `outputPath`: File path where the image will be saved (supports .png, .jpg, .bmp)
  - `width`: Image width in pixels (default: 800)
  - `height`: Image height in pixels (default: 600)
- **Throws**: 
  - `ArgumentNullException` if outputPath is null
  - `ArgumentException` if outputPath is empty or whitespace
- **Requires**: SkiaSharp NuGet package
- **Example**: `ClassClonator.GenerateMappingImage<Person, PersonDTO>("mapping.png");`

## Benchmarks

Mapping a simple object with 50 properties, over 100k iterations.

Results:

| Mapper  | Results |
| ------------- | ------------- |
| Automapper   | 32490ms  |
| Automapper with cached `config` object | 335ms  |
| **PropMapper**   | **25ms**  |
| Manual code  | 10ms  |

PropMapper is more than 13 times faster than cached Automapper. Here's the class we tested on:

```cs
public class Tester
{
	public string prop1 { get; set; }
	public string prop2 { get; set; }
	public string prop3 { get; set; }
	public int iprop1 { get; set; }
	//etc. 50 times
}
```

## How It Works

PropMapper uses **compiled Expression trees** for optimal performance:

1. **Static Compilation**: When you first use a specific type pair (e.g., `Person` to `Employee`), the mapper creates compiled Expression trees in a static constructor
2. **Caching**: These compiled expressions are cached in static variables, so subsequent calls are extremely fast
3. **Property Matching**: Properties are matched by name (case-sensitive) between source and destination types
4. **Type Safety**: Only properties with matching names and compatible types are copied

This approach provides near-manual-code performance while maintaining the convenience of automatic mapping.

## Usage Examples

### Basic Object Mapping

```cs
public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

public class PersonDTO
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

// Create a new DTO from a Person
var person = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
var dto = person.CreateCopy<Person, PersonDTO>();
```

### Casting Base Class to Derived Class

```cs
public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class Employee : Person
{
    public string Title { get; set; }
    
    public Employee(Person person)
    {
        // Copy all properties from Person to this Employee instance
        person.CopyTo(this);
    }
}
```

### Mapping Collections

```cs
List<Person> people = GetPeople();
IEnumerable<PersonDTO> dtos = people.CopyAll<Person, PersonDTO>();

// Use with LINQ
var adultDtos = people
    .Where(p => p.Age >= 18)
    .CopyAll<Person, PersonDTO>()
    .ToList();
```

### Updating Existing Objects

```cs
public void UpdatePerson(Person existingPerson, PersonDTO updatedData)
{
    // Copy properties from DTO to existing entity
    updatedData.CopyTo(existingPerson);
    // or
    existingPerson.CopyFrom(updatedData);
}
```

### Generating Mapping Diagrams

**New Feature**: Generate visual diagrams showing property mappings between types.

```cs
// Generate a mapping diagram image
ClassClonator.GenerateMappingImage<Person, PersonDTO>("mapping.png");

// Customize image dimensions
ClassClonator.GenerateMappingImage<Person, PersonDTO>("mapping.png", width: 1000, height: 800);
```

The generated image shows:
- Source type properties on the left
- Destination type properties on the right
- Green highlights for matched properties
- Green lines connecting matching properties
- Statistics showing match counts

**Note**: Image generation requires the `SkiaSharp` NuGet package. The core mapping functionality works without any dependencies.

## Features

✅ **Fast**: Uses compiled expressions, not reflection  
✅ **Simple**: Single C# file, no dependencies for core mapping (SkiaSharp required for image generation)  
✅ **Type-Safe**: Generic methods with compile-time type checking  
✅ **Null-Safe**: Built-in null checking for safety  
✅ **Flexible**: Works with any POCO objects  
✅ **Collection Support**: Bulk copy operations with LINQ integration  
✅ **Extension Methods**: Natural, fluent API  
✅ **Visual Mapping**: Generate diagram images showing property mappings  

## Limitations

- Only public properties with matching names are copied
- Property name matching is case-sensitive
- Source property must be readable (`CanRead`)
- Destination property must be writable (`CanWrite`)
- No support for nested object mapping (only shallow copy)
- No custom mapping configuration
- Destination type must have a parameterless constructor (for `CreateCopy` and `CopyAll` methods)

## Performance Considerations

- **First Call Overhead**: The first mapping between two specific types has a small overhead due to expression compilation
- **Subsequent Calls**: Extremely fast due to cached compiled expressions
- **Memory**: Each unique type pair creates a static cached expression
- **Thread-Safe**: Static constructors are thread-safe, making the mapper safe for concurrent use

## Troubleshooting

### Properties Not Copying

**Problem**: Some properties aren't being copied.

**Solutions**:
- Ensure property names match exactly (case-sensitive)
- Verify source property has a getter (`get;`)
- Verify destination property has a setter (`set;`)
- Check that property types are compatible

### NullReferenceException

**Problem**: Getting null reference exceptions.

**Solutions**:
- Use `CopyTo()` or `CopyFrom()` which return `false` for null inputs
- Check the return value: `if (source.CopyTo(dest)) { /* success */ }`
- Use `CreateCopy()` with null checking: `var result = source?.CreateCopy<TIn, TOut>();`

### Performance Not as Expected

**Problem**: Mapping seems slow.

**Solutions**:
- First call per type pair has compilation overhead - this is normal
- Ensure you're not creating new type pairs unnecessarily
- For best performance, reuse the same type pairs throughout your application

## Namespace

The library is in the `DX.Shared` namespace. Add this to your using statements:

```cs
using DX.Shared;
```

## Compatibility

- .NET Standard 2.0+
- .NET Framework 4.6.1+
- .NET Core 2.0+
- .NET 5.0+
- .NET 6.0+
- Nullable reference types enabled

## Contributing

This is a simple, focused library. If you find bugs or have suggestions:

1. Open an issue on GitHub
2. Submit a pull request with tests
3. Keep changes minimal and focused

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Credits

Originally created by [Jitbit](https://www.jitbit.com/). The tool is in heavy use in production environments and is regularly tested.
