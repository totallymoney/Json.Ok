# Json.Ok

A type-safe, functional JSON library for F# with powerful computation expression support for both reading and writing JSON.

## Installation

Install from NuGet:

```bash
dotnet add package totallymoney/Json.Ok
```

Or using Paket:

```
paket add totallymoney/Json.Ok
```

## Features

- **Type-safe JSON reading and writing** with computation expressions
- **Compile-time safety** using phantom types to prevent invalid JSON structures
- **Flexible error handling** with both monadic (fail-fast) and applicative (collect-all-errors) approaches
- **Rich type support**: primitives, Guid, DateTime, DateTimeOffset, byte arrays (base64), nested objects, arrays
- **Type-driven API** using SRTP for concise, type-inferred property reading
- **Conditional JSON generation** with native F# control flow (if/then/else, loops)
- **Optional property handling** with F# option types
- **Case-insensitive property names** for robust JSON parsing

## Quick Start

### Reading JSON

```fsharp
open System.Text.Json
open Json.Ok.Read

// Define your reader using computation expressions
let userReader = jsonReader {
    let! id: int = prop "id"
    let! name: string = prop "name"
    let! email: string option = prop "email"  // Optional property
    return {| Id = id; Name = name; Email = email |}
}

// Parse JSON
let json = """{"id": 123, "name": "Alice", "email": "alice@example.com"}"""
let element = JsonDocument.Parse(json).RootElement
let result = JsonReader.readJsonElement userReader element

match result with
| Ok user -> printfn $"User: {user.Name}"
| Error errors -> printfn $"Errors: {errors}"
```

### Writing JSON

```fsharp
open System.Text.Json
open Json.Ok.Write

// Create JSON using computation expressions
let userJson = jsonObject {
    "id" => 123
    "name" => "Alice"
    "email" => "alice@example.com"
    "isActive" => true
}

// Convert to JsonElement or serialize
let element = toJsonElement userJson
let jsonString = JsonSerializer.Serialize(element)
// Output: {"id":123,"name":"Alice","email":"alice@example.com","isActive":true}
```

## Reading JSON

### Basic Property Reading

The library supports both explicit and type-driven property reading:

```fsharp
open Json.Ok.Read

// Explicit API with required properties
let explicitReader = jsonReader {
    let! name = Req.Prop.string "name"
    let! age = Req.Prop.int32 "age"
    let! active = Req.Prop.bool "active"
    return {| Name = name; Age = age; Active = active |}
}

// Type-driven API (more concise)
let typeDrivenReader = jsonReader {
    let! name: string = prop "name"
    let! age: int = prop "age"
    let! active: bool = prop "active"
    return {| Name = name; Age = age; Active = active |}
}
```

### Optional Properties

Properties can be optional using F# option types:

```fsharp
let userReader = jsonReader {
    let! name: string = prop "name"               // Required
    let! email: string option = prop "email"      // Optional
    let! phone: string option = prop "phone"      // Optional
    return {| Name = name; Email = email; Phone = phone |}
}

// Missing optional properties return None
// Parse: {"name": "Bob"}
// Result: Ok {| Name = "Bob"; Email = None; Phone = None |}
```

### Nested Objects

Read nested objects by composing readers:

```fsharp
let addressReader = jsonReader {
    let! street: string = prop "street"
    let! city: string = prop "city"
    let! zip: string = prop "zip"
    return {| Street = street; City = city; Zip = zip |}
}

let personReader = jsonReader {
    let! name: string = prop "name"
    let! address = Prop.read (addressReader, "address")
    return {| Name = name; Address = address |}
}

// Parse: {"name": "Alice", "address": {"street": "123 Main", "city": "NYC", "zip": "10001"}}
```

### Arrays

Read arrays of primitives or objects:

```fsharp
// Array of primitives
let tagsReader = jsonReader {
    let! tags: string list = Prop.read (Req.Value.string, "tags")
    return tags
}

// Array of objects
let itemReader = jsonReader {
    let! id: int = prop "id"
    let! name: string = prop "name"
    return {| Id = id; Name = name |}
}

let listReader = jsonReader {
    let! items = Prop.read (itemReader, "items")
    return items
}

// Parse: {"items": [{"id": 1, "name": "Item 1"}, {"id": 2, "name": "Item 2"}]}
```

### Error Handling

Choose between fail-fast (monadic) or collect-all-errors (applicative):

```fsharp
// Monadic (fail-fast) - stops at first error
let monadicReader = jsonReader {
    let! name = Req.Prop.string "name"
    let! age = Req.Prop.int32 "age"
    let! email = Req.Prop.string "email"
    return {| Name = name; Age = age; Email = email |}
}

// Applicative (collect all errors) - using and!
let applicativeReader = jsonReader {
    let! name = Req.Prop.string "name"
    and! age = Req.Prop.int32 "age"
    and! email = Req.Prop.string "email"
    return {| Name = name; Age = age; Email = email |}
}

// Parse: {"name": "Alice"}
// Monadic returns: Error ["Property 'age' not found"]
// Applicative returns: Error ["Property 'age' not found"; "Property 'email' not found"]
```

### Polymorphic JSON (oneOf)

Handle polymorphic JSON with `oneOf`:

```fsharp
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float

let circleReader = jsonReader {
    let! kind: string = prop "kind"
    let! radius: float = prop "radius"
    match kind with
    | "circle" -> return Circle radius
    | _ -> return! JsonReader.err ["Not a circle"]
}

let rectangleReader = jsonReader {
    let! kind: string = prop "kind"
    let! width: float = prop "width"
    let! height: float = prop "height"
    match kind with
    | "rectangle" -> return Rectangle(width, height)
    | _ -> return! JsonReader.err ["Not a rectangle"]
}

let shapeReader = Req.Value.oneOf [circleReader; rectangleReader]

// Parse: {"kind": "circle", "radius": 5.0}
// Result: Ok (Circle 5.0)
```

### Tolerant Array Reading

Use `arrayAny` to skip invalid items instead of failing:

```fsharp
// Skip invalid items in arrays
let tolerantReader = Req.Prop.arrayAny Req.Value.int32 "numbers"

// Parse: {"numbers": [1, "invalid", 2, null, 3]}
// Result: Ok [1; 2; 3]  // Skips "invalid" and null
```

## Writing JSON

### Basic Objects

Create JSON objects using the `jsonObject` computation expression and `=>` operator:

```fsharp
open Json.Ok.Write

let user = jsonObject {
    "id" => 123
    "name" => "Alice"
    "email" => "alice@example.com"
    "age" => 30
    "isActive" => true
}

let json = user |> toJsonElement |> JsonSerializer.Serialize
// Output: {"id":123,"name":"Alice","email":"alice@example.com","age":30,"isActive":true}
```

### Nested Objects

Nest objects within objects:

```fsharp
let person = jsonObject {
    "id" => 101
    "name" => "Bob"
    "address" => jsonObject {
        "street" => "123 Main St"
        "city" => "NYC"
        "zip" => "10001"
    }
}
```

### Arrays

Create arrays using `jsonArray` or inline lists:

```fsharp
// Using jsonArray
let scores = jsonArray {
    95
    87
    92
}

// Using inline list of primitives
let user = jsonObject {
    "name" => "Alice"
    "tags" => ["developer"; "fsharp"; "dotnet"]
}

// Using inline list of objects
let team = jsonObject {
    "name" => "Engineering"
    "members" => [
        jsonObject { "id" => 1; "name" => "Alice" }
        jsonObject { "id" => 2; "name" => "Bob" }
    ]
}
```

### Optional Properties

Optional properties are omitted when `None`:

```fsharp
let user = jsonObject {
    "id" => 123
    "name" => "Alice"
    "email" => Some "alice@example.com"  // Included
    "phone" => None                       // Omitted
}

// Output: {"id":123,"name":"Alice","email":"alice@example.com"}
```

### Conditional JSON Generation

Use F# control flow for dynamic JSON:

```fsharp
let createUser includeEmail isPremium = jsonObject {
    "id" => 123
    "name" => "Alice"

    if includeEmail then
        "email" => "alice@example.com"

    if isPremium then
        "tier" => "premium"
        "credits" => 1000
    else
        "tier" => "free"
        "credits" => 0
}

let freeUser = createUser false false
// {"id":123,"name":"Alice","tier":"free","credits":0}

let premiumUser = createUser true true
// {"id":123,"name":"Alice","email":"alice@example.com","tier":"premium","credits":1000}
```

### Dynamic Properties with Loops

Generate properties dynamically:

```fsharp
let features = [
    "darkMode", true
    "analytics", false
    "notifications", true
]

let config = jsonObject {
    "appName" => "MyApp"
    "version" => "1.0"

    for (feature, enabled) in features do
        if enabled then
            feature => true
}

// Output: {"appName":"MyApp","version":"1.0","darkMode":true,"notifications":true}
```

### Supported Types

The library supports all JSON-compatible types:

```fsharp
open System

let allTypes = jsonObject {
    // Primitives
    "string" => "hello"
    "int" => 42
    "int64" => 9223372036854775807L
    "bool" => true
    "double" => 3.14159
    "decimal" => 99.99m

    // .NET types
    "guid" => Guid.NewGuid()
    "dateTime" => DateTime.UtcNow
    "dateTimeOffset" => DateTimeOffset.Now
    "bytes" => [| 1uy; 2uy; 3uy |]  // Encoded as base64

    // Existing JsonElement
    "existing" => someJsonElement
}
```

## Advanced Examples

### Complex Nested Structure

```fsharp
type OrderItem = { Sku: string; Price: decimal; Quantity: int }
type Order = {
    Id: int
    Customer: string
    Items: OrderItem list
    Discount: decimal option
}

let orderReader = jsonReader {
    let! id: int = prop "id"
    let! customer: string = prop "customer"
    let! discount: decimal option = prop "discount"

    let itemReader = jsonReader {
        let! sku: string = prop "sku"
        let! price: decimal = prop "price"
        let! quantity: int = prop "quantity"
        return { Sku = sku; Price = price; Quantity = quantity }
    }

    let! items = Prop.read (itemReader, "items")

    return {
        Id = id
        Customer = customer
        Items = items
        Discount = discount
    }
}

let writeOrder (order: Order) = jsonObject {
    "id" => order.Id
    "customer" => order.Customer
    "discount" => order.Discount
    "items" => [
        for item in order.Items do
            jsonObject {
                "sku" => item.Sku
                "price" => item.Price
                "quantity" => item.Quantity
            }
    ]
}
```

## API Reference

### Reading

- `jsonReader { ... }` - Computation expression for building JSON readers
- `prop "name"` - Type-driven property reader (shorthand)
- `Prop.read (reader, "name")` - Type-driven reader for objects/arrays
- `Req.Prop.*` - Required property readers
- `Opt.Prop.*` - Optional property readers
- `Req.Value.*` - Required value readers
- `Opt.Value.*` - Optional value readers
- `Req.Value.oneOf` / `Req.Prop.oneOf` - Polymorphic JSON support
- `Req.Prop.arrayAny` / `Opt.Prop.arrayAny` - Tolerant array readers

### Writing

- `jsonObject { ... }` - Computation expression for building JSON objects
- `jsonArray { ... }` - Computation expression for building JSON arrays
- `"name" => value` - Property assignment operator
- `toJsonElement` - Convert JsonWriter to JsonElement
- `JsonWriter.value*` - Standalone value writers
- `JsonWriter.prop*` - Property writers

## License

See [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please open an issue or pull request.
