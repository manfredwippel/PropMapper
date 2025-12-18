# PropMapper - No Reflection Version

This document describes the **no-reflection** version of PropMapper, an alternative implementation that provides property mapping without any runtime reflection overhead.

## Overview

While the standard PropMapper uses reflection during initialization to discover properties (then uses compiled Expression trees for fast mapping), this no-reflection version requires **manual registration** of mappers but provides:

- ✅ **Zero reflection** - No `PropertyInfo.GetProperties()` calls at all
- ✅ **Predictable performance** - No initialization overhead, even on first use
- ✅ **AOT-friendly** - Works with Native AOT and trimming
- ✅ **Full control** - You define exactly what gets mapped
- ✅ **Same API** - Uses similar extension methods as the reflection version

## When to Use This Version

### Use the No-Reflection Version When:
- ✅ You're using Native AOT compilation
- ✅ You're concerned about reflection security or compliance
- ✅ You need predictable first-call performance
- ✅ You want explicit control over mapping logic
- ✅ You're working with trimmed applications

### Use the Standard Reflection Version When:
- ✅ You want automatic property discovery
- ✅ You don't mind small first-call overhead
- ✅ You're mapping many different type pairs
- ✅ You prefer less boilerplate code

## Installation

Copy `PropMapperNoReflection.cs` into your project, or use it alongside the standard `PropMapper.cs`.

## Usage

### 1. Register Mappers (One-Time Setup)

Before using any mapping operations, you must register mappers for each type pair:

```csharp
using DX.Shared.NoReflection;

// Typically in your application startup code
public static class MapperConfiguration
{
    public static void Configure()
    {
        // Register Person -> PersonDTO mapper
        MapperRegistry.Register<Person, PersonDTO>(
            // CreateCopy function: creates new instance
            createFunc: p => new PersonDTO
            {
                FirstName = p.FirstName,
                LastName = p.LastName,
                Age = p.Age,
                Email = p.Email
            },
            // CopyTo action: copies to existing instance
            copyAction: (p, dto) =>
            {
                dto.FirstName = p.FirstName;
                dto.LastName = p.LastName;
                dto.Age = p.Age;
                dto.Email = p.Email;
            }
        );

        // Register more mappers as needed...
    }
}

// Call at application startup
MapperConfiguration.Configure();
```

### 2. Use the Extension Methods

Once registered, use the extension methods just like the standard version:

```csharp
using DX.Shared.NoReflection;

// Create a new object and copy properties
var dto = person.CreateCopyNoReflection<Person, PersonDTO>();

// Copy to an existing object
person.CopyToNoReflection(existingDto);

// Or use CopyFrom
existingDto.CopyFromNoReflection(person);

// Copy collections
IEnumerable<PersonDTO> dtos = people.CopyAllNoReflection<Person, PersonDTO>();
```

## API Reference

### MapperRegistry.Register<TInput, TOutput>

Registers mapping functions for a type pair.

**Parameters:**
- `createFunc`: `Func<TInput, TOutput>` - Function that creates a new `TOutput` from `TInput`
- `copyAction`: `Action<TInput, TOutput>` - Action that copies properties from `TInput` to an existing `TOutput`

**Example:**
```csharp
MapperRegistry.Register<Source, Dest>(
    createFunc: s => new Dest { Prop1 = s.Prop1, Prop2 = s.Prop2 },
    copyAction: (s, d) => { d.Prop1 = s.Prop1; d.Prop2 = s.Prop2; }
);
```

### Extension Methods

All methods work on any object once mappers are registered:

#### `CreateCopyNoReflection<TInput, TOutput>()`
Creates a new instance of `TOutput` and copies properties.

- **Returns**: New instance of `TOutput`
- **Throws**: `ArgumentNullException` if input is null
- **Throws**: `InvalidOperationException` if no mapper is registered

#### `CopyToNoReflection<TInput, TOutput>(TOutput output)`
Copies properties to an existing output object.

- **Returns**: `bool` - `true` if successful, `false` if either input or output is null
- **Throws**: `InvalidOperationException` if no mapper is registered

#### `CopyFromNoReflection<TInput, TOutput>(TInput input)`
Copies properties from input to the current object.

- **Returns**: `bool` - `true` if successful, `false` if either input or output is null
- **Throws**: `InvalidOperationException` if no mapper is registered

#### `CopyAllNoReflection<TInput, TOutput>(IEnumerable<TInput>)`
Copies all objects in a collection.

- **Returns**: `IEnumerable<TOutput>` - Lazily evaluated sequence
- **Note**: Null items are skipped
- **Throws**: `InvalidOperationException` if no mapper is registered

## Complete Example

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using DX.Shared.NoReflection;

// Define your classes
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

public class Program
{
    public static void Main()
    {
        // 1. Register mapper (do this once at startup)
        MapperRegistry.Register<Person, PersonDTO>(
            createFunc: p => new PersonDTO
            {
                FirstName = p.FirstName,
                LastName = p.LastName,
                Age = p.Age
            },
            copyAction: (p, dto) =>
            {
                dto.FirstName = p.FirstName;
                dto.LastName = p.LastName;
                dto.Age = p.Age;
            }
        );

        // 2. Use the mappers
        var person = new Person 
        { 
            FirstName = "John", 
            LastName = "Doe", 
            Age = 30 
        };

        // Create new DTO
        var dto = person.CreateCopyNoReflection<Person, PersonDTO>();
        Console.WriteLine($"{dto.FirstName} {dto.LastName}, {dto.Age}");

        // Copy to existing object
        var anotherDto = new PersonDTO();
        person.CopyToNoReflection(anotherDto);

        // Copy collection
        var people = new List<Person>
        {
            new Person { FirstName = "Jane", LastName = "Smith", Age = 25 },
            new Person { FirstName = "Bob", LastName = "Johnson", Age = 35 }
        };
        
        var dtos = people.CopyAllNoReflection<Person, PersonDTO>().ToList();
        Console.WriteLine($"Copied {dtos.Count} people");
    }
}
```

## Performance Comparison

| Version | First Call | Subsequent Calls | Memory | Reflection |
|---------|-----------|------------------|---------|-----------|
| **No-Reflection** | Fast | Fast | Minimal | None |
| Standard (Reflection + Expression) | Slow (compile) | Fast | Static cache | On init only |
| Manual code | Fast | Fast | None | None |

The no-reflection version has consistent performance on both first and subsequent calls, with no compilation overhead.

## Advanced Usage

### Conditional Mapping

You can include conditional logic in your mappers:

```csharp
MapperRegistry.Register<Person, PersonDTO>(
    createFunc: p => new PersonDTO
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        Age = p.Age > 0 ? p.Age : 0  // Ensure non-negative age
    },
    copyAction: (p, dto) =>
    {
        dto.FirstName = p.FirstName;
        dto.LastName = p.LastName;
        dto.Age = p.Age > 0 ? p.Age : 0;
    }
);
```

### Type Conversion

Handle type conversions explicitly:

```csharp
MapperRegistry.Register<PersonEntity, PersonViewModel>(
    createFunc: e => new PersonViewModel
    {
        Name = $"{e.FirstName} {e.LastName}",
        AgeString = e.Age.ToString(),
        CreatedDate = e.CreatedTimestamp.ToString("yyyy-MM-dd")
    },
    copyAction: (e, vm) =>
    {
        vm.Name = $"{e.FirstName} {e.LastName}";
        vm.AgeString = e.Age.ToString();
        vm.CreatedDate = e.CreatedTimestamp.ToString("yyyy-MM-dd");
    }
);
```

### Nested Objects

Handle nested object mapping:

```csharp
MapperRegistry.Register<Order, OrderDTO>(
    createFunc: o => new OrderDTO
    {
        OrderId = o.OrderId,
        // Map nested customer (assumes Customer -> CustomerDTO is registered)
        Customer = o.Customer?.CreateCopyNoReflection<Customer, CustomerDTO>()
    },
    copyAction: (o, dto) =>
    {
        dto.OrderId = o.OrderId;
        if (o.Customer != null)
        {
            if (dto.Customer == null)
                dto.Customer = o.Customer.CreateCopyNoReflection<Customer, CustomerDTO>();
            else
                o.Customer.CopyToNoReflection(dto.Customer);
        }
    }
);
```

## Error Handling

### Missing Mapper Registration

If you try to use a mapper that hasn't been registered, you'll get a helpful error:

```csharp
var dto = person.CreateCopyNoReflection<Person, PersonDTO>();
// InvalidOperationException: No mapper registered for Person -> PersonDTO.
// Call MapperRegistry.Register<Person, PersonDTO>() first.
```

### Null Handling

Like the standard version, the no-reflection version handles nulls gracefully:

```csharp
Person? person = null;
PersonDTO dto = new PersonDTO();

// Returns false, doesn't throw
bool result = person.CopyToNoReflection(dto);  // false

// Throws ArgumentNullException
var newDto = person.CreateCopyNoReflection<Person, PersonDTO>();  // throws
```

## Thread Safety

- ✅ **Registration**: Should be done at application startup before concurrent access
- ✅ **Usage**: All mapping operations are thread-safe after registration
- ✅ **Static caching**: Uses static variables per type pair, safe for concurrent access

## Limitations

- ❌ **Manual registration required** - Must register each type pair explicitly
- ❌ **More boilerplate** - Requires writing mapping code for each type pair
- ❌ **No automatic discovery** - Won't automatically map matching properties

## Choosing Between Versions

### Use Cases for No-Reflection Version

**Native AOT Applications:**
```csharp
// Works perfectly with Native AOT
MapperRegistry.Register<T, U>(...);
var result = source.CreateCopyNoReflection<T, U>();
```

**Microservices with Limited Type Pairs:**
```csharp
// Only mapping a few well-known types
MapperRegistry.Register<Entity, DTO>(...);
MapperRegistry.Register<DTO, Entity>(...);
```

**Security-Conscious Applications:**
```csharp
// No reflection = reduced attack surface
// Explicit mapping = better code review
```

### Use Cases for Standard Reflection Version

**Rapid Development:**
```csharp
// No registration needed
var dto = person.CreateCopy<Person, PersonDTO>();
```

**Many Type Pairs:**
```csharp
// Automatic discovery scales better when mapping 100+ type pairs
```

## Namespace

The no-reflection version is in the `DX.Shared.NoReflection` namespace:

```csharp
using DX.Shared.NoReflection;
```

This is separate from the standard version (`DX.Shared`) so you can use both in the same project if needed.

## Compatibility

- .NET Standard 2.0+
- .NET Framework 4.6.1+
- .NET Core 2.0+
- .NET 5.0+
- .NET 6.0+
- .NET 7.0+
- .NET 8.0+
- ✅ **Native AOT compatible**
- ✅ **Trimming-friendly**
- Nullable reference types enabled

## FAQ

**Q: Can I use both versions in the same project?**

A: Yes! They use different namespaces (`DX.Shared` vs `DX.Shared.NoReflection`) and different method names (`CreateCopy` vs `CreateCopyNoReflection`), so there's no conflict.

**Q: Which version is faster?**

A: After the initial compilation, both versions have similar runtime performance. The no-reflection version has faster first-call performance (no expression compilation).

**Q: Do I have to register both createFunc and copyAction?**

A: Yes, both are required when registering a mapper. This provides flexibility for different use cases.

**Q: What if I only want CreateCopy and never use CopyTo?**

A: You still need to provide both functions during registration, but the copyAction will only be compiled/called if you actually use CopyTo or CopyFrom.

**Q: Can I update a mapper after registration?**

A: Yes, calling `MapperRegistry.Register<T, U>()` again will replace the existing mapper.

## License

MIT License - see main LICENSE file for details.
