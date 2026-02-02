module Json.Ok.Read

open System
open System.Text.Json
open System.Collections.Generic


[<RequireQualifiedAccess>]
module private Result =

    let inline ofOption e =
        function
        | Some x -> Ok x
        | None -> Error e

    /// Apply a Result-wrapped function to a Result-wrapped value, collecting all errors applicatively
    let inline apply fResult aResult =
        match fResult, aResult with
        | Ok f, Ok a -> Ok(f a)
        | Error e1, Error e2 -> Error(e1 @ e2)
        | Error e, _ -> Error e
        | _, Error e -> Error e

    /// Traverse a list with a function that returns a Result, accumulating errors monadically (stops at first error)
    let inline traverseM f list =
        let folder head tail =
            f head |> Result.bind (fun h -> tail |> Result.bind (fun t -> Ok(h :: t)))

        List.foldBack folder list (Ok [])

    /// Traverse a list with a function that returns a Result, accumulating errors applicatively (collects all errors)
    let inline traverseA f list =
        let cons h t = h :: t
        let folder head tail = apply (Result.map cons (f head)) tail
        List.foldBack folder list (Ok [])


[<RequireQualifiedAccess>]
type ReadableJson =
    private
    | Value of JsonElement
    | Object of JsonElement * JsonObject Lazy
    | Array of JsonElement * ReadableJson list Lazy

and private JsonObject = Dictionary<string, ReadableJson>

[<RequireQualifiedAccess>]
module private JsonObject =

    let private stringComparer = StringComparer.OrdinalIgnoreCase

    let inline build kvps : JsonObject =
        Dictionary<string, ReadableJson>(dict kvps, stringComparer)

    let inline tryFind name (jsonObj: JsonObject) : ReadableJson option =
        match jsonObj.TryGetValue name with
        | true, value -> Some value
        | _ -> None


[<RequireQualifiedAccess>]
module private ReadableJson =

    let rec ofJsonElement (elem: JsonElement) : ReadableJson =
        match elem.ValueKind with
        | JsonValueKind.Object ->
            ReadableJson.Object(
                elem,
                lazy
                    (elem.EnumerateObject()
                     |> Seq.map (fun p -> p.Name, ofJsonElement p.Value)
                     |> JsonObject.build)
            )
        | JsonValueKind.Array ->
            ReadableJson.Array(elem, lazy (elem.EnumerateArray() |> Seq.map ofJsonElement |> List.ofSeq))
        | _ -> ReadableJson.Value elem

    let inline snd<'a> (_, v: Lazy<'a>) = v.Value


type JsonReader<'a> = private JsonReader of (ReadableJson -> Result<'a, string list>)

[<RequireQualifiedAccess>]
module JsonReader =

    let internal run (JsonReader reader) json = reader json

    let readJsonElement reader =
        ReadableJson.ofJsonElement >> run reader

    let err e = JsonReader(fun _ -> Error e)

    let retn a = JsonReader(fun _ -> Ok a)

    let map f (JsonReader reader) = JsonReader(reader >> Result.map f)

    let mapError f (JsonReader reader) = JsonReader(reader >> Result.mapError f)

    let bind f (JsonReader reader) =
        JsonReader(fun json -> reader json |> Result.bind (fun a -> run (f a) json))

    let apply (JsonReader fReader) (JsonReader aReader) =
        JsonReader(fun json -> Result.apply (fReader json) (aReader json))

type JsonReaderBuilder() =
    member _.Return x = JsonReader.retn x
    member _.ReturnFrom(x: JsonReader<'a>) = x
    member _.Bind(x: JsonReader<'a>, f: 'a -> JsonReader<'b>) = JsonReader.bind f x
    member _.Zero() = JsonReader.retn ()
    member _.Delay(f: unit -> JsonReader<'a>) = JsonReader(JsonReader.run (f ()))
    member _.BindReturn(x: JsonReader<'a>, f: 'a -> 'b) = JsonReader.map f x

    member _.MergeSources(x: JsonReader<'a>, y: JsonReader<'b>) =
        JsonReader.apply (JsonReader.map (fun a b -> a, b) x) y

let jsonReader = JsonReaderBuilder()


[<RequireQualifiedAccess>]
module private Read =

    let inline private toOpt (b, value) =
        match b with
        | true -> Some value
        | _ -> None

    let inline jsonElement elem =
        match elem with
        | ReadableJson.Value e
        | ReadableJson.Object(e, _)
        | ReadableJson.Array(e, _) -> e

    let inline object (elem: ReadableJson) =
        match elem with
        | ReadableJson.Object(elem, jsonObj) -> Some(elem, jsonObj)
        | _ -> None

    let inline array (elem: ReadableJson) =
        match elem with
        | ReadableJson.Array(elem, items) -> Some(elem, items)
        | _ -> None

    let inline string (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.String -> Some(e.GetString())
        | _ -> None

    let inline guid (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.String -> toOpt (e.TryGetGuid())
        | _ -> None

    let inline dateTime (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.String -> toOpt (e.TryGetDateTime())
        | _ -> None

    let inline dateTimeOffset (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.String -> toOpt (e.TryGetDateTimeOffset())
        | _ -> None

    let inline nullValue (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Null -> Some()
        | _ -> None

    let inline bool (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e ->
            match e.ValueKind with
            | JsonValueKind.True
            | JsonValueKind.False -> Some(e.GetBoolean())
            | _ -> None
        | _ -> None

    let inline bytesFromBase64 (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.String -> toOpt (e.TryGetBytesFromBase64())
        | _ -> None

    let inline int16 (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Number -> toOpt (e.TryGetInt16())
        | _ -> None

    let inline int32 (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Number -> toOpt (e.TryGetInt32())
        | _ -> None

    let inline int64 (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Number -> toOpt (e.TryGetInt64())
        | _ -> None

    let inline double (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Number -> toOpt (e.TryGetDouble())
        | _ -> None

    let inline decimal (elem: ReadableJson) =
        match elem with
        | ReadableJson.Value e when e.ValueKind = JsonValueKind.Number -> toOpt (e.TryGetDecimal())
        | _ -> None


[<RequireQualifiedAccess>]
module private JsonArray =

    let inline readAny reader =
        ReadableJson.snd >> List.choose (JsonReader.run reader >> Result.toOption)

    let inline readReq reader =
        ReadableJson.snd
        >> List.mapi (fun i -> JsonReader.run reader >> Result.mapError (List.map (fun s -> $"[{i}]: {s}")))
        >> Result.traverseA id

    let inline readOpt reader =
        ReadableJson.snd >> Result.traverseM (JsonReader.run reader) >> Result.toOption


[<RequireQualifiedAccess>]
module Req =

    let rec private tryReaders errs readers json =
        match readers with
        | reader :: remaining ->
            match JsonReader.run reader json with
            | Ok v -> Ok v
            | Error e -> tryReaders (errs @ e) remaining json
        | [] -> Error("No readers succeeded" :: errs)

    /// Required value readers - return `Error` if value is wrong type
    module Value =

        let jsonElement: JsonReader<JsonElement> = JsonReader(Read.jsonElement >> Ok)

        let string: JsonReader<string> =
            JsonReader(Read.string >> Result.ofOption [ "Expected a string value" ])

        let guid: JsonReader<Guid> =
            JsonReader(Read.guid >> Result.ofOption [ "Expected a GUID value" ])

        let bool: JsonReader<bool> =
            JsonReader(Read.bool >> Result.ofOption [ "Expected a boolean value" ])

        let bytesFromBase64: JsonReader<byte[]> =
            JsonReader(Read.bytesFromBase64 >> Result.ofOption [ "Expected a base64 string value" ])

        let int16: JsonReader<int16> =
            JsonReader(Read.int16 >> Result.ofOption [ "Expected an int16 value" ])

        let int32: JsonReader<int> =
            JsonReader(Read.int32 >> Result.ofOption [ "Expected an int32 value" ])

        let int64: JsonReader<int64> =
            JsonReader(Read.int64 >> Result.ofOption [ "Expected an int64 value" ])

        let double: JsonReader<double> =
            JsonReader(Read.double >> Result.ofOption [ "Expected a double value" ])

        let decimal: JsonReader<decimal> =
            JsonReader(Read.decimal >> Result.ofOption [ "Expected a decimal value" ])

        let dateTime: JsonReader<DateTime> =
            JsonReader(Read.dateTime >> Result.ofOption [ "Expected a DateTime value" ])

        let dateTimeOffset: JsonReader<DateTimeOffset> =
            JsonReader(Read.dateTimeOffset >> Result.ofOption [ "Expected a DateTimeOffset value" ])

        let nullValue: JsonReader<unit> =
            JsonReader(Read.nullValue >> Result.ofOption [ "Expected a null value" ])

        /// returns Ok list if is an array and all items are read successfully
        let array (reader: JsonReader<'a>) : JsonReader<'a list> =
            JsonReader(
                Read.array
                >> Result.ofOption [ "Expected an array value" ]
                >> Result.bind (JsonArray.readReq reader)
            )

        /// returns Ok list if is an array with any successfully read items (tolerates item read errors)
        let arrayAny (reader: JsonReader<'a>) : JsonReader<'a list> =
            JsonReader(
                Read.array
                >> Result.ofOption [ "Expected an array value" ]
                >> Result.map (JsonArray.readAny reader)
            )

        /// Try multiple readers in sequence until one succeeds
        let oneOf (readers: JsonReader<'a> list) : JsonReader<'a> = JsonReader(tryReaders [] readers)

    /// Required object property readers - return `Error` if property is missing or wrong type
    module Prop =

        let inline private tryGet name =
            Read.object
            >> Result.ofOption [ "Expected a JSON object" ]
            >> Result.bind (
                ReadableJson.snd
                >> JsonObject.tryFind name
                >> Result.ofOption [ $"Property '{name}' not found" ]
            )

        let jsonElement name : JsonReader<JsonElement> =
            JsonReader(tryGet name >> Result.map Read.jsonElement)

        let string name : JsonReader<string> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.string >> Result.ofOption [ $"Property '{name}' is not a string" ]))

        let guid name : JsonReader<Guid> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.guid >> Result.ofOption [ $"Property '{name}' is not a GUID" ]))

        let bool name : JsonReader<bool> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.bool >> Result.ofOption [ $"Property '{name}' is not a boolean" ]))

        let bytesFromBase64 name : JsonReader<byte[]> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (
                    Read.bytesFromBase64
                    >> Result.ofOption [ $"Property '{name}' is not a base64 string" ]
                ))

        let int16 name : JsonReader<int16> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.int16 >> Result.ofOption [ $"Property '{name}' is not an int16" ]))

        let int32 name : JsonReader<int> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.int32 >> Result.ofOption [ $"Property '{name}' is not an int32" ]))

        let int64 name : JsonReader<int64> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.int64 >> Result.ofOption [ $"Property '{name}' is not an int64" ]))

        let double name : JsonReader<double> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.double >> Result.ofOption [ $"Property '{name}' is not a double" ]))

        let decimal name : JsonReader<decimal> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.decimal >> Result.ofOption [ $"Property '{name}' is not a decimal" ]))

        let dateTime name : JsonReader<DateTime> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (fun j -> Read.dateTime j |> Result.ofOption [ $"Property '{name}' is not a DateTime" ]))

        let dateTimeOffset name : JsonReader<DateTimeOffset> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (fun j ->
                    Read.dateTimeOffset j
                    |> Result.ofOption [ $"Property '{name}' is not a DateTimeOffset" ]))

        let nullValue name : JsonReader<unit> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.nullValue >> Result.ofOption [ $"Property '{name}' is not null" ]))

        /// returns Ok object if property exists and is an object and is read successfully
        let object (reader: JsonReader<'a>) name : JsonReader<'a> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (fun jsonObj ->
                    Read.object jsonObj |> Result.ofOption [ $"Property '{name}' is not an object" ])
                |> Result.bind (
                    ReadableJson.Object
                    >> JsonReader.run reader
                    >> Result.mapError (List.map (fun e -> $"{name}.{e}"))
                ))

        /// returns Ok list if property exists and is an array and all items are read successfully
        let array (reader: JsonReader<'a>) name : JsonReader<'a list> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.array >> Result.ofOption [ $"Property '{name}' is not an array" ])
                |> Result.bind (JsonArray.readReq reader >> Result.mapError (List.map (fun e -> $"{name}{e}"))))

        /// returns Ok list if property exists and is an array with any successfully read items (tolerates item read errors)
        let arrayAny (reader: JsonReader<'a>) (name: string) : JsonReader<'a list> =
            JsonReader(fun j ->
                tryGet name j
                |> Result.bind (Read.array >> Result.ofOption [ $"Property '{name}' is not an array" ])
                |> Result.map (JsonArray.readAny reader))

        /// Try multiple readers in sequence until one succeeds
        let oneOf name (readers: JsonReader<'a> list) : JsonReader<'a> =
            JsonReader(tryGet name >> Result.bind (tryReaders [] readers))

[<RequireQualifiedAccess>]
module Opt =

    let rec private tryReaders readers json =
        match readers with
        | reader :: remaining ->
            match JsonReader.run reader json with
            | Ok v -> Some v
            | Error _ -> tryReaders remaining json
        | [] -> None

    /// Optional value property readers - return `Ok(None)` if value is wrong type
    module Value =

        let string: JsonReader<string option> = JsonReader(Read.string >> Ok)

        let guid: JsonReader<Guid option> = JsonReader(Read.guid >> Ok)

        let bool: JsonReader<bool option> = JsonReader(Read.bool >> Ok)

        let bytesFromBase64: JsonReader<byte[] option> =
            JsonReader(Read.bytesFromBase64 >> Ok)

        let int16: JsonReader<int16 option> = JsonReader(Read.int16 >> Ok)

        let int32: JsonReader<int option> = JsonReader(Read.int32 >> Ok)

        let int64: JsonReader<int64 option> = JsonReader(Read.int64 >> Ok)

        let double: JsonReader<double option> = JsonReader(Read.double >> Ok)

        let decimal: JsonReader<decimal option> = JsonReader(Read.decimal >> Ok)

        let dateTime: JsonReader<DateTime option> = JsonReader(Read.dateTime >> Ok)

        let dateTimeOffset: JsonReader<DateTimeOffset option> =
            JsonReader(Read.dateTimeOffset >> Ok)

        let nullValue: JsonReader<unit option> = JsonReader(Read.nullValue >> Ok)

        /// returns Some list if is an array and all items are read successfully
        let array (reader: JsonReader<'a>) : JsonReader<'a list option> =
            JsonReader(Read.array >> Option.bind (JsonArray.readOpt reader) >> Ok)

        /// returns Some list if is an array with any successfully read items (tolerates item read errors)
        let arrayAny (reader: JsonReader<'a>) : JsonReader<'a list option> =
            JsonReader(Read.array >> Option.map (JsonArray.readAny reader) >> Ok)

        /// Try multiple readers in sequence until one succeeds
        let oneOf (readers: JsonReader<'a> list) : JsonReader<'a option> = JsonReader(tryReaders readers >> Ok)

    /// Optional object property readers - return `Ok(None)` if property is missing or wrong type
    module Prop =

        let inline private tryGet name =
            Read.object >> Option.bind (ReadableJson.snd >> JsonObject.tryFind name)

        let jsonElement name : JsonReader<JsonElement option> =
            JsonReader(tryGet name >> Option.map Read.jsonElement >> Ok)

        let string name : JsonReader<string option> =
            JsonReader(tryGet name >> Option.bind Read.string >> Ok)

        let guid name : JsonReader<Guid option> =
            JsonReader(tryGet name >> Option.bind Read.guid >> Ok)

        let bool name : JsonReader<bool option> =
            JsonReader(tryGet name >> Option.bind Read.bool >> Ok)

        let bytesFromBase64 name : JsonReader<byte[] option> =
            JsonReader(tryGet name >> Option.bind Read.bytesFromBase64 >> Ok)

        let int16 name : JsonReader<int16 option> =
            JsonReader(tryGet name >> Option.bind Read.int16 >> Ok)

        let int32 name : JsonReader<int option> =
            JsonReader(tryGet name >> Option.bind Read.int32 >> Ok)

        let int64 name : JsonReader<int64 option> =
            JsonReader(tryGet name >> Option.bind Read.int64 >> Ok)

        let double name : JsonReader<double option> =
            JsonReader(tryGet name >> Option.bind Read.double >> Ok)

        let decimal name : JsonReader<decimal option> =
            JsonReader(tryGet name >> Option.bind Read.decimal >> Ok)

        let dateTime name : JsonReader<DateTime option> =
            JsonReader(tryGet name >> Option.bind Read.dateTime >> Ok)

        let dateTimeOffset name : JsonReader<DateTimeOffset option> =
            JsonReader(tryGet name >> Option.bind Read.dateTimeOffset >> Ok)

        let nullValue name : JsonReader<unit option> =
            JsonReader(tryGet name >> Option.bind Read.nullValue >> Ok)

        /// returns Some object if property exists and is an object and is read successfully
        let object (reader: JsonReader<'a>) (name: string) : JsonReader<'a option> =
            JsonReader(
                tryGet name
                >> Option.bind Read.object
                >> Option.bind (ReadableJson.Object >> JsonReader.run reader >> Result.toOption)
                >> Ok
            )

        /// returns Some list if property exists and is an array and all items are read successfully
        let array (reader: JsonReader<'a>) (name: string) : JsonReader<'a list option> =
            JsonReader(
                tryGet name
                >> Option.bind Read.array
                >> Option.bind (JsonArray.readOpt reader)
                >> Ok
            )

        /// returns Some list if property exists and is an array with any successfully read items (tolerates item read errors)
        let arrayAny (reader: JsonReader<'a>) (name: string) : JsonReader<'a list option> =
            JsonReader(
                tryGet name
                >> Option.bind Read.array
                >> Option.map (JsonArray.readAny reader)
                >> Ok
            )

        /// Try multiple readers in sequence until one succeeds
        let oneOf name (readers: JsonReader<'a> list) : JsonReader<'a option> =
            JsonReader(tryGet name >> Option.bind (tryReaders readers) >> Ok)


/// SRTP-based property reader (type-driven API)
/// Supports both required and optional properties - the type annotation determines behavior.
type PropReader =
    // Required property readers
    static member inline Read(_: PropReader, name: string, _: string) : JsonReader<string> = Req.Prop.string name

    static member inline Read(_: PropReader, name: string, _: int) : JsonReader<int> = Req.Prop.int32 name

    static member inline Read(_: PropReader, name: string, _: int16) : JsonReader<int16> = Req.Prop.int16 name

    static member inline Read(_: PropReader, name: string, _: int64) : JsonReader<int64> = Req.Prop.int64 name

    static member inline Read(_: PropReader, name: string, _: bool) : JsonReader<bool> = Req.Prop.bool name

    static member inline Read(_: PropReader, name: string, _: Guid) : JsonReader<Guid> = Req.Prop.guid name

    static member inline Read(_: PropReader, name: string, _: double) : JsonReader<double> = Req.Prop.double name

    static member inline Read(_: PropReader, name: string, _: decimal) : JsonReader<decimal> = Req.Prop.decimal name

    static member inline Read(_: PropReader, name: string, _: DateTime) : JsonReader<DateTime> = Req.Prop.dateTime name

    static member inline Read(_: PropReader, name: string, _: DateTimeOffset) : JsonReader<DateTimeOffset> =
        Req.Prop.dateTimeOffset name

    static member inline Read(_: PropReader, name: string, _: byte[]) : JsonReader<byte[]> =
        Req.Prop.bytesFromBase64 name

    static member inline Read(_: PropReader, name: string, _: JsonElement) : JsonReader<JsonElement> =
        Req.Prop.jsonElement name

    static member inline Read(_: PropReader, name: string, _: unit) : JsonReader<unit> = Req.Prop.nullValue name

    // Optional property readers
    static member inline Read(_: PropReader, name: string, _: string option) : JsonReader<string option> =
        Opt.Prop.string name

    static member inline Read(_: PropReader, name: string, _: int option) : JsonReader<int option> = Opt.Prop.int32 name

    static member inline Read(_: PropReader, name: string, _: int16 option) : JsonReader<int16 option> =
        Opt.Prop.int16 name

    static member inline Read(_: PropReader, name: string, _: int64 option) : JsonReader<int64 option> =
        Opt.Prop.int64 name

    static member inline Read(_: PropReader, name: string, _: bool option) : JsonReader<bool option> =
        Opt.Prop.bool name

    static member inline Read(_: PropReader, name: string, _: Guid option) : JsonReader<Guid option> =
        Opt.Prop.guid name

    static member inline Read(_: PropReader, name: string, _: double option) : JsonReader<double option> =
        Opt.Prop.double name

    static member inline Read(_: PropReader, name: string, _: decimal option) : JsonReader<decimal option> =
        Opt.Prop.decimal name

    static member inline Read(_: PropReader, name: string, _: DateTime option) : JsonReader<DateTime option> =
        Opt.Prop.dateTime name

    static member inline Read
        (_: PropReader, name: string, _: DateTimeOffset option)
        : JsonReader<DateTimeOffset option> =
        Opt.Prop.dateTimeOffset name

    static member inline Read(_: PropReader, name: string, _: byte[] option) : JsonReader<byte[] option> =
        Opt.Prop.bytesFromBase64 name

    static member inline Read(_: PropReader, name: string, _: JsonElement option) : JsonReader<JsonElement option> =
        Opt.Prop.jsonElement name

    static member inline Read(_: PropReader, name: string, _: unit option) : JsonReader<unit option> =
        Opt.Prop.nullValue name

    // Object/array property readers - unified under ReadWith
    // SRTP picks the right one based on whether return type is a list
    static member inline ReadWith(_: PropReader, name: string, reader: JsonReader<'a>, _: 'a) : JsonReader<'a> =
        Req.Prop.object reader name

    static member inline ReadWith
        (_: PropReader, name: string, reader: JsonReader<'a>, _: 'a option)
        : JsonReader<'a option> =
        Opt.Prop.object reader name

    static member inline ReadWith
        (_: PropReader, name: string, reader: JsonReader<'a>, _: 'a list)
        : JsonReader<'a list> =
        Req.Prop.array reader name

    static member inline ReadWith
        (_: PropReader, name: string, reader: JsonReader<'a>, _: 'a list option)
        : JsonReader<'a list option> =
        Opt.Prop.array reader name

#nowarn 64

/// Type-driven property reader with overloaded methods.
/// Use type annotation to select required vs optional behavior.
/// <example>
/// <code>
/// jsonReader {
///     // Primitives - single argument (property name)
///     let! name: string = Prop.read "name"              // Required primitive
///     let! age: int option = Prop.read "age"            // Optional primitive
///
///     // Objects/arrays - two arguments (reader, property name)
///     let! address: Address = Prop.read (addressReader, "address")        // Required object
///     let! items: Item list = Prop.read (itemReader, "items")             // Required array (detected by list type)
///     let! extras: Item list option = Prop.read (itemReader, "extras")    // Optional array
///     return {| Name = name; Age = age; Address = address; Items = items; Extras = extras |}
/// }
/// </code>
/// </example>
type Prop =
    /// Read a primitive property. Type annotation determines required vs optional.
    static member inline read(name: string) : JsonReader< ^T > =
        ((^R or ^T): (static member Read: ^R * string * ^T -> JsonReader< ^T >) (Unchecked.defaultof<PropReader>,
                                                                                 name,
                                                                                 Unchecked.defaultof< ^T>))

    /// Read an object or array property. Type annotation determines:
    /// - Required vs optional (T vs T option)
    /// - Object vs array (T vs T list)
    static member inline read(reader: JsonReader<'a>, name: string) : JsonReader< ^T > =
        ((^R or ^T): (static member ReadWith: ^R * string * JsonReader<'a> * ^T -> JsonReader< ^T >) (Unchecked.defaultof<
                                                                                                          PropReader
                                                                                                       >,
                                                                                                      name,
                                                                                                      reader,
                                                                                                      Unchecked.defaultof<
                                                                                                          ^T
                                                                                                       >))

/// Shorthand for Prop.read with single argument (primitives only)
let inline prop (name: string) : JsonReader< ^T > = Prop.read name
