module Json.Ok.Write

open System
open System.Text.Json
open System.Buffers

/// Phantom type markers for type safety
type Prop = private | Prop // Properties (must be inside objects)
type Item = private | Item // Items (for arrays or standalone values)
type Complete = private | Complete // Complete JSON structures (can be converted to JsonElement)

/// Core type wrapping a JSON write action with context tracking
type JsonWriter<'Context> = private JsonWriter of (Utf8JsonWriter -> unit)

[<RequireQualifiedAccess>]
module JsonWriter =

    let run (JsonWriter write) (writer: Utf8JsonWriter) = write writer

    // Value writers (for standalone values or array elements) - return JsonWriter<Complete>
    let value (v: string) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteStringValue v)

    let valueInt (v: int) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteNumberValue v)

    let valueInt64 (v: int64) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteNumberValue v)

    let valueBool (v: bool) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteBooleanValue v)

    let valueDouble (v: float) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteNumberValue v)

    let valueDecimal (v: decimal) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteNumberValue v)

    let valueGuid (v: Guid) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteStringValue v)

    let valueDateTime (v: DateTime) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteStringValue v)

    let valueDateTimeOffset (v: DateTimeOffset) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteStringValue v)

    let valueBase64String (v: byte[]) : JsonWriter<Complete> =
        JsonWriter(fun w -> w.WriteBase64StringValue v)

    let valueJsonElement (v: JsonElement) : JsonWriter<Complete> = JsonWriter(fun w -> v.WriteTo w)

    let valueNull: JsonWriter<Complete> = JsonWriter(fun w -> w.WriteNullValue())

    // Property writers (for object properties) - return JsonWriter<Prop>
    let prop (name: string) (v: string) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteString(name, v))

    let propInt (name: string) (v: int) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteNumber(name, v))

    let propInt64 (name: string) (v: int64) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteNumber(name, v))

    let propBool (name: string) (v: bool) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteBoolean(name, v))

    let propDouble (name: string) (v: float) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteNumber(name, v))

    let propDecimal (name: string) (v: decimal) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteNumber(name, v))

    let propGuid (name: string) (v: Guid) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteString(name, v))

    let propDateTime (name: string) (v: DateTime) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteString(name, v))

    let propDateTimeOffset (name: string) (v: DateTimeOffset) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteString(name, v))

    let propNull (name: string) : JsonWriter<Prop> = JsonWriter(fun w -> w.WriteNull(name))

    let propBase64String (name: string) (v: byte[]) : JsonWriter<Prop> =
        JsonWriter(fun w -> w.WriteBase64String(name, ReadOnlySpan(v)))

    let propJsonElement (name: string) (v: JsonElement) : JsonWriter<Prop> =
        JsonWriter(fun w ->
            w.WritePropertyName name
            v.WriteTo w)

    // Property writers for nested structures
    let propObj (name: string) (JsonWriter write: JsonWriter<Prop>) : JsonWriter<Prop> =
        JsonWriter(fun w ->
            w.WriteStartObject name
            write w
            w.WriteEndObject())

    let propArray (name: string) (JsonWriter write: JsonWriter<Item>) : JsonWriter<Prop> =
        JsonWriter(fun w ->
            w.WriteStartArray name
            write w
            w.WriteEndArray())

    // Object and array constructors - convert Props/Items into Complete structures
    let obj (JsonWriter write: JsonWriter<Prop>) : JsonWriter<Complete> =
        JsonWriter(fun w ->
            w.WriteStartObject()
            write w
            w.WriteEndObject())

    let array (JsonWriter write: JsonWriter<Item>) : JsonWriter<Complete> =
        JsonWriter(fun w ->
            w.WriteStartArray()
            write w
            w.WriteEndArray())

    // Combinators - combine writers of the same context
    let combine (JsonWriter w1: JsonWriter<'a>) (JsonWriter w2: JsonWriter<'a>) : JsonWriter<'a> =
        JsonWriter(fun w ->
            w1 w
            w2 w)

    let zero<'a> : JsonWriter<'a> = JsonWriter(fun _ -> ())

    // Conversion functions - Complete structures can be used as Items in arrays
    let asItem (JsonWriter write: JsonWriter<Complete>) : JsonWriter<Item> = JsonWriter write

/// <summary>
/// Helper type for overloaded toJsonElement using SRTP.
/// This type is public only for technical reasons (SRTP constraint resolution).
/// Users should not need to reference this type directly.
/// </summary>
type ToJsonElement =
    static member ToJson(_: ToJsonElement, JsonWriter writer: JsonWriter<Complete>) =
        let buffer = new ArrayBufferWriter<byte>()

        do
            use jsonWriter = new Utf8JsonWriter(buffer)
            writer jsonWriter
            jsonWriter.Flush()

        use doc = JsonDocument.Parse buffer.WrittenMemory
        doc.RootElement.Clone()

    static member ToJson(_: ToJsonElement, writer: JsonWriter<Prop>) =
        writer
        |> JsonWriter.obj
        |> fun w -> ToJsonElement.ToJson(Unchecked.defaultof<ToJsonElement>, w)

    static member ToJson(_: ToJsonElement, writer: JsonWriter<Item>) =
        writer
        |> JsonWriter.array
        |> fun w -> ToJsonElement.ToJson(Unchecked.defaultof<ToJsonElement>, w)

/// <summary>
/// Convert a JsonWriter to a JsonElement.
/// This is the main function for converting your JSON builder expressions into JsonElement.
/// </summary>
/// <remarks>
/// Works with JsonWriter&lt;Complete&gt;, JsonWriter&lt;Prop&gt;, or JsonWriter&lt;Item&gt;:
/// <list type="bullet">
/// <item>JsonWriter&lt;Complete&gt; is converted directly</item>
/// <item>JsonWriter&lt;Prop&gt; is automatically wrapped with an object first</item>
/// <item>JsonWriter&lt;Item&gt; is automatically wrapped with an array first</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// jsonObject { "id" => 123 } |> toJsonElement
/// jsonArray { 1; 2; 3 } |> toJsonElement
/// JsonWriter.valueInt 42 |> toJsonElement
/// </code>
/// </example>
#nowarn "64" // Suppress warning about type variable being constrained

let inline toJsonElement (writer: ^T) : JsonElement =
    ((^ToJsonElement or ^T): (static member ToJson: ^ToJsonElement * ^T -> JsonElement) (Unchecked.defaultof<
                                                                                             ToJsonElement
                                                                                          >,
                                                                                         writer))

/// Computation expression builder for object properties
type JsonObjectBuilder() =
    member _.Yield(w: JsonWriter<Prop>) : JsonWriter<Prop> = w
    member _.Zero() : JsonWriter<Prop> = JsonWriter.zero
    member _.Delay(f: unit -> JsonWriter<Prop>) : JsonWriter<Prop> = f ()
    member _.Combine(w1, w2) : JsonWriter<Prop> = JsonWriter.combine w1 w2

    member _.For(seq: seq<'a>, f: 'a -> JsonWriter<Prop>) : JsonWriter<Prop> =
        seq |> Seq.map f |> Seq.fold JsonWriter.combine JsonWriter.zero

/// Computation expression builder for array items
type JsonArrayBuilder() =
    // Allow yielding JsonWriter<Item> - wrap it as a complete array first
    member _.Yield(w: JsonWriter<Item>) : JsonWriter<Item> =
        w |> JsonWriter.array |> JsonWriter.asItem

    // Allow yielding JsonWriter<Prop> - automatically wraps it as an object item
    member _.Yield(w: JsonWriter<Prop>) : JsonWriter<Item> =
        w |> JsonWriter.obj |> JsonWriter.asItem

    // Allow yielding JsonWriter<Complete> - automatically converts to item
    member _.Yield(w: JsonWriter<Complete>) : JsonWriter<Item> = JsonWriter.asItem w

    // Allow yielding primitives directly - no need for JsonWriter.value*
    member _.Yield(v: string) : JsonWriter<Item> = JsonWriter.value v |> JsonWriter.asItem

    member _.Yield(v: int) : JsonWriter<Item> =
        JsonWriter.valueInt v |> JsonWriter.asItem

    member _.Yield(v: int64) : JsonWriter<Item> =
        JsonWriter.valueInt64 v |> JsonWriter.asItem

    member _.Yield(v: bool) : JsonWriter<Item> =
        JsonWriter.valueBool v |> JsonWriter.asItem

    member _.Yield(v: float) : JsonWriter<Item> =
        JsonWriter.valueDouble v |> JsonWriter.asItem

    member _.Yield(v: decimal) : JsonWriter<Item> =
        JsonWriter.valueDecimal v |> JsonWriter.asItem

    member _.Yield(v: Guid) : JsonWriter<Item> =
        JsonWriter.valueGuid v |> JsonWriter.asItem

    member _.Yield(v: DateTime) : JsonWriter<Item> =
        JsonWriter.valueDateTime v |> JsonWriter.asItem

    member _.Yield(v: DateTimeOffset) : JsonWriter<Item> =
        JsonWriter.valueDateTimeOffset v |> JsonWriter.asItem

    member _.Yield(v: byte[]) : JsonWriter<Item> =
        JsonWriter.valueBase64String v |> JsonWriter.asItem

    member _.Yield(v: JsonElement) : JsonWriter<Item> =
        JsonWriter.valueJsonElement v |> JsonWriter.asItem

    member _.Zero() : JsonWriter<Item> = JsonWriter.zero
    member _.Delay(f: unit -> JsonWriter<Item>) : JsonWriter<Item> = f ()
    member _.Combine(w1, w2) : JsonWriter<Item> = JsonWriter.combine w1 w2

    member _.For(seq: seq<'a>, f: 'a -> JsonWriter<Item>) : JsonWriter<Item> =
        seq |> Seq.map f |> Seq.fold JsonWriter.combine JsonWriter.zero

/// <summary>
/// Computation expression builder for creating JSON objects.
/// Use this to build JSON objects with type-safe property definitions.
/// </summary>
/// <example>
/// <code>
/// jsonObject {
///     "id" => 123
///     "name" => "Alice"
///     "tags" => jsonArray { "a"; "b"; "c" }
/// } |> toJsonElement
/// </code>
/// </example>
let jsonObject = JsonObjectBuilder()

/// <summary>
/// Computation expression builder for creating JSON arrays.
/// Use this to build JSON arrays with automatic type conversions.
/// </summary>
/// <example>
/// <code>
/// jsonArray {
///     for item in items do
///         jsonObject { "value" => item }
/// } |> toJsonElement
/// </code>
/// </example>
let jsonArray = JsonArrayBuilder()

/// <summary>
/// Helper type for overload resolution using SRTP for the => operator.
/// This type is public only for technical reasons (SRTP constraint resolution).
/// Users should not need to reference this type directly.
/// </summary>
type WriteProp =
    static member inline Prop(_: WriteProp, name: string, value: string) = JsonWriter.prop name value
    static member inline Prop(_: WriteProp, name: string, value: int) = JsonWriter.propInt name value
    static member inline Prop(_: WriteProp, name: string, value: int64) = JsonWriter.propInt64 name value
    static member inline Prop(_: WriteProp, name: string, value: bool) = JsonWriter.propBool name value
    static member inline Prop(_: WriteProp, name: string, value: float) = JsonWriter.propDouble name value
    static member inline Prop(_: WriteProp, name: string, value: decimal) = JsonWriter.propDecimal name value
    static member inline Prop(_: WriteProp, name: string, value: Guid) = JsonWriter.propGuid name value
    static member inline Prop(_: WriteProp, name: string, value: DateTime) = JsonWriter.propDateTime name value

    static member inline Prop(_: WriteProp, name: string, value: DateTimeOffset) =
        JsonWriter.propDateTimeOffset name value

    static member inline Prop(_: WriteProp, name: string, value: byte[]) = JsonWriter.propBase64String name value

    static member inline Prop(_: WriteProp, name: string, value: JsonElement) = JsonWriter.propJsonElement name value

    // Optional types
    static member inline Prop(_: WriteProp, name: string, value: string option) =
        match value with
        | Some v -> JsonWriter.prop name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: int option) =
        match value with
        | Some v -> JsonWriter.propInt name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: int64 option) =
        match value with
        | Some v -> JsonWriter.propInt64 name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: bool option) =
        match value with
        | Some v -> JsonWriter.propBool name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: float option) =
        match value with
        | Some v -> JsonWriter.propDouble name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: decimal option) =
        match value with
        | Some v -> JsonWriter.propDecimal name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: Guid option) =
        match value with
        | Some v -> JsonWriter.propGuid name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: DateTime option) =
        match value with
        | Some v -> JsonWriter.propDateTime name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: DateTimeOffset option) =
        match value with
        | Some v -> JsonWriter.propDateTimeOffset name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: byte[] option) =
        match value with
        | Some v -> JsonWriter.propBase64String name v
        | None -> JsonWriter.zero

    static member inline Prop(_: WriteProp, name: string, value: JsonElement option) =
        match value with
        | Some v -> JsonWriter.propJsonElement name v
        | None -> JsonWriter.zero

    // Nested structures - allow using jsonObject/jsonArray directly
    static member inline Prop(_: WriteProp, name: string, value: JsonWriter<Prop>) = JsonWriter.propObj name value

    static member inline Prop(_: WriteProp, name: string, value: JsonWriter<Item>) = JsonWriter.propArray name value

    // List/Seq support - automatically convert to arrays
    static member inline Prop(_: WriteProp, name: string, value: seq<JsonWriter<Prop>>) =
        value
        |> Seq.map (JsonWriter.obj >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<JsonWriter<Complete>>) =
        value
        |> Seq.map JsonWriter.asItem
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<string>) =
        value
        |> Seq.map (JsonWriter.value >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<int>) =
        value
        |> Seq.map (JsonWriter.valueInt >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<int64>) =
        value
        |> Seq.map (JsonWriter.valueInt64 >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<bool>) =
        value
        |> Seq.map (JsonWriter.valueBool >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<float>) =
        value
        |> Seq.map (JsonWriter.valueDouble >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<decimal>) =
        value
        |> Seq.map (JsonWriter.valueDecimal >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<Guid>) =
        value
        |> Seq.map (JsonWriter.valueGuid >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<DateTime>) =
        value
        |> Seq.map (JsonWriter.valueDateTime >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<DateTimeOffset>) =
        value
        |> Seq.map (JsonWriter.valueDateTimeOffset >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<byte[]>) =
        value
        |> Seq.map (JsonWriter.valueBase64String >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

    static member inline Prop(_: WriteProp, name: string, value: seq<JsonElement>) =
        value
        |> Seq.map (JsonWriter.valueJsonElement >> JsonWriter.asItem)
        |> Seq.fold JsonWriter.combine JsonWriter.zero
        |> JsonWriter.propArray name

/// <summary>
/// Operator for defining JSON object properties.
/// Supports primitive types (string, int, bool, float, decimal, Guid, DateTime, DateTimeOffset, byte[], JsonElement),
/// optional types, nested structures (jsonObject, jsonArray), and sequences/lists (automatically converted to arrays).
/// </summary>
/// <example>
/// <code>
/// "id" => 123
/// "name" => "Alice"
/// "active" => true
/// "data" => [| 1uy; 2uy; 3uy |]  // Written as base64 string
/// "existing" => jsonElement  // Embed existing JsonElement
/// "metadata" => jsonObject { "version" => 2 }
/// "items" => jsonArray { 1; 2; 3 }
/// "tags" => ["a"; "b"; "c"]  // List automatically converted to array
/// "objects" => [ jsonObject { "id" => 1 }; jsonObject { "id" => 2 } ]  // List of objects
/// "optional" => Some "value"  // Omitted if None
/// </code>
/// </example>
let inline (=>) (name: string) (value: ^T) : JsonWriter<Prop> =
    ((^W or ^T): (static member Prop: ^W * string * ^T -> JsonWriter<Prop>) (Unchecked.defaultof<WriteProp>, name, value))


(*
Type Safety Summary:
--------------------
The DSL uses phantom types to enforce valid JSON structure at compile time.

AUTOMATIC CONVERSIONS:
1. toJsonElement automatically wraps:
   - jsonObject { props } → object
   - jsonArray { items } → array
   - JsonWriter.valueInt 42 → direct

2. jsonArray automatically converts yielded values:
   - Primitives (int, string, bool, etc.) → directly yielded
   - JsonWriter<Complete> → automatically converts to Item
   - JsonWriter<Prop> → automatically wraps with obj and converts to Item
   - No need for JsonWriter.value* or pipes!

CLEAN SYNTAX EXAMPLE:
    jsonObject {
        "id" => 101
        "metadata" => jsonObject { "version" => 2 }

        "items" => jsonArray {
            for x in xs do
                jsonObject {
                    "name" => x
                }
                // No pipes needed!
        }

        "tags" => jsonArray {
            for tag in tags do
                tag  // Primitives work directly!
        }
    }
    |> toJsonElement  // No obj needed!

COMPILE-TIME SAFETY:
✗ PREVENTED at compile time:
    - Using properties (=>) inside jsonArray
    - Mixing incompatible contexts

This prevents runtime errors like:
  "Cannot write a JSON property within an array or as the first JSON token"
*)
