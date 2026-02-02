module Json.Ok.ReadTests

open System
open System.Text.Json
open Expecto
open Expecto.Flip
open Json.Ok.Read

let parse reader =
    JsonSerializer.SerializeToElement >> JsonReader.readJsonElement reader

// Types for oneOf tests
type JsonValue =
    | StringValue of string
    | NumberValue of int
    | BoolValue of bool

type Contact =
    | Email of string
    | Phone of string
    | SocialMedia of platform: string * handle: string

type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float

[<Tests>]
let tests =
    [ test "read required string property" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  return name
              }

          parse reader {| name = "Alice" |} |> Expect.equal "" (Ok "Alice")

          parse reader {| name = 123 |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"

          parse reader {| other = "Alice" |}
          |> Expect.wantError "missing property"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }
      test "read required string property - null value rejected" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  return name
              }

          let jsonWithNull = """{"name": null}"""

          let result =
              jsonWithNull
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement reader

          result
          |> Expect.wantError "null string should be rejected"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read optional string property - null value rejected" {
          let reader =
              jsonReader {
                  let! name = Opt.Prop.string "name"
                  return name
              }

          let jsonWithNull = """{"name": null}"""

          let result =
              jsonWithNull
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement reader

          result |> Expect.equal "" (Ok None)
      }

      test "required value string - null value rejected" {
          let jsonWithNull = """null"""

          let result =
              jsonWithNull
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement Req.Value.string

          result
          |> Expect.wantError "null value string should be rejected"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "optional value string - null value rejected" {
          let jsonWithNull = """null"""

          let result =
              jsonWithNull
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement Opt.Value.string

          result |> Expect.equal "" (Ok None)
      }

      test "read null value - succeeds on null" {
          let jsonWithNull = "null"

          let result =
              jsonWithNull
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement Req.Value.nullValue

          result |> Expect.equal "" (Ok())
      }

      test "read null value - fails on non-null" {
          let result =
              JsonDocument.Parse("\"hello\"").RootElement
              |> JsonReader.readJsonElement Req.Value.nullValue

          result
          |> Expect.wantError "non-null value should be rejected"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read null property - succeeds when property is null" {
          let reader = Req.Prop.nullValue "status"

          let result =
              JsonDocument.Parse("""{"status": null}""").RootElement
              |> JsonReader.readJsonElement reader

          result |> Expect.equal "" (Ok())
      }

      test "read null property - fails when property is missing" {
          let reader = Req.Prop.nullValue "status"

          parse reader {| other = "value" |}
          |> Expect.wantError "missing property"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read null property - fails when property is not null" {
          let reader = Req.Prop.nullValue "status"

          parse reader {| status = "active" |}
          |> Expect.wantError "non-null property"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read optional null property - returns Some when null" {
          let reader = Opt.Prop.nullValue "status"

          let result =
              JsonDocument.Parse("""{"status": null}""").RootElement
              |> JsonReader.readJsonElement reader

          result |> Expect.equal "" (Ok(Some()))
      }

      test "read optional null property - returns None when missing" {
          let reader = Opt.Prop.nullValue "status"

          parse reader {| other = "value" |} |> Expect.equal "" (Ok None)
      }

      test "read optional null property - returns None when not null" {
          let reader = Opt.Prop.nullValue "status"

          parse reader {| status = "active" |} |> Expect.equal "" (Ok None)
      }

      test "read required guid property" {
          let id = Guid.NewGuid()

          let reader = jsonReader { return! Req.Prop.guid "id" }
          parse reader {| id = id |} |> Expect.equal "" (Ok id)

          parse (Req.Prop.guid "id") {| id = id |} |> Expect.equal "inlined" (Ok id)

          parse reader {| id = "not-a-guid" |}
          |> Expect.wantError "invalid guid"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required bool property" {
          let reader =
              jsonReader {
                  let! flag = Req.Prop.bool "flag"
                  return flag
              }

          parse reader {| flag = true |} |> Expect.equal "" (Ok true)

          parse reader {| flag = false |} |> Expect.equal "" (Ok false)

          parse reader {| flag = "true" |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required int16 property" {
          let reader =
              jsonReader {
                  let! value = Req.Prop.int16 "value"
                  return value
              }

          parse reader {| value = 32767s |} |> Expect.equal "" (Ok 32767s)

          parse reader {| value = "100" |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required int32 property" {
          let reader =
              jsonReader {
                  let! age = Req.Prop.int32 "age"
                  return age
              }

          parse reader {| age = 42 |} |> Expect.equal "" (Ok 42)

          parse reader {| age = "42" |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required int64 property" {
          let reader =
              jsonReader {
                  let! value = Req.Prop.int64 "value"
                  return value
              }

          parse reader {| value = 9223372036854775807L |}
          |> Expect.equal "" (Ok 9223372036854775807L)

          parse reader {| value = "9999" |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required double property" {
          let reader =
              jsonReader {
                  let! value = Req.Prop.double "value"
                  return value
              }

          parse reader {| value = 3.14159 |} |> Expect.equal "" (Ok 3.14159)

          parse reader {| value = "3.14" |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required decimal property" {
          let reader =
              jsonReader {
                  let! price = Req.Prop.decimal "price"
                  return price
              }

          parse reader {| price = 19.99 |} |> Expect.equal "" (Ok 19.99m)
      }

      test "read required bytesFromBase64 property" {
          let reader =
              jsonReader {
                  let! data = Req.Prop.bytesFromBase64 "data"
                  return data
              }

          let bytes = [| 72uy; 101uy; 108uy; 108uy; 111uy |] // "Hello" in bytes
          let base64 = Convert.ToBase64String(bytes)

          parse reader {| data = base64 |} |> Expect.equal "" (Ok bytes)

          parse reader {| data = 123 |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"

          parse reader {| data = "not-valid-base64!!!" |}
          |> Expect.wantError "invalid base64"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "read required DateTime property" {
          let dt = DateTime(2024, 1, 15, 10, 30, 0)

          let reader =
              jsonReader {
                  let! timestamp = Req.Prop.dateTime "timestamp"
                  return timestamp
              }

          parse reader {| timestamp = dt |} |> Expect.equal "" (Ok dt)
      }

      test "read optional string property - present" {
          let reader =
              jsonReader {
                  let! email = Opt.Prop.string "email"
                  return email
              }

          parse reader {| email = "test@example.com" |}
          |> Expect.equal "" (Ok(Some "test@example.com"))
      }

      test "read optional string property - missing" {
          let reader =
              jsonReader {
                  let! email = Opt.Prop.string "email"
                  return email
              }

          parse reader {| name = "Alice" |} |> Expect.equal "" (Ok None)
      }

      test "read optional string property - wrong type" {
          let reader =
              jsonReader {
                  let! email = Opt.Prop.string "email"
                  return email
              }

          parse reader {| email = 123 |} |> Expect.equal "" (Ok None)
      }

      test "read optional int16 property - present" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.int16 "value"
                  return value
              }

          parse reader {| value = 1000s |} |> Expect.equal "" (Ok(Some 1000s))
      }

      test "read optional int16 property - missing" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.int16 "value"
                  return value
              }

          parse reader {| other = 123 |} |> Expect.equal "" (Ok None)
      }

      test "read optional int64 property - present" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.int64 "value"
                  return value
              }

          parse reader {| value = 1234567890L |} |> Expect.equal "" (Ok(Some 1234567890L))
      }

      test "read optional int64 property - missing" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.int64 "value"
                  return value
              }

          parse reader {| other = 123 |} |> Expect.equal "" (Ok None)
      }

      test "read optional double property - present" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.double "value"
                  return value
              }

          parse reader {| value = 2.71828 |} |> Expect.equal "" (Ok(Some 2.71828))
      }

      test "read optional double property - missing" {
          let reader =
              jsonReader {
                  let! value = Opt.Prop.double "value"
                  return value
              }

          parse reader {| other = 123 |} |> Expect.equal "" (Ok None)
      }

      test "read optional bytesFromBase64 property - present" {
          let reader =
              jsonReader {
                  let! data = Opt.Prop.bytesFromBase64 "data"
                  return data
              }

          let bytes = [| 1uy; 2uy; 3uy |]
          let base64 = Convert.ToBase64String(bytes)

          parse reader {| data = base64 |} |> Expect.equal "" (Ok(Some bytes))
      }

      test "read optional bytesFromBase64 property - missing" {
          let reader =
              jsonReader {
                  let! data = Opt.Prop.bytesFromBase64 "data"
                  return data
              }

          parse reader {| other = "test" |} |> Expect.equal "" (Ok None)
      }

      test "read optional bytesFromBase64 property - invalid base64" {
          let reader =
              jsonReader {
                  let! data = Opt.Prop.bytesFromBase64 "data"
                  return data
              }

          parse reader {| data = "not-valid-base64!!!" |} |> Expect.equal "" (Ok None)
      }

      test "sequential binding with let! - short circuits on error" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  let! age = Req.Prop.int32 "age"
                  let! id = Req.Prop.guid "id"
                  return {| Name = name; Age = age; Id = id |}
              }

          parse reader {| name = "Alice" |}
          |> Expect.wantError "should fail on missing age"
          |> List.length
          |> Expect.equal "" 1
      }

      test "applicative binding with and! - collects all errors" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  and! age = Req.Prop.int32 "age"
                  and! id = Req.Prop.guid "id"
                  return {| Name = name; Age = age; Id = id |}
              }

          parse reader {| |}
          |> Expect.wantError "should fail on missing all properties"
          |> List.length
          |> Expect.equal "" 3
      }

      test "read nested object" {
          let addressReader =
              jsonReader {
                  let! street = Req.Prop.string "street"
                  and! city = Req.Prop.string "city"
                  return sprintf "%s, %s" street city
              }

          let personReader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  and! address = Req.Prop.object addressReader "address"
                  return {| Name = name; Address = address |}
              }

          parse
              personReader
              {| name = "Alice"
                 address = {| street = "Main St"; city = "NYC" |} |}
          |> Expect.equal
              ""
              (Ok
                  {| Name = "Alice"
                     Address = "Main St, NYC" |})
      }

      test "read optional nested object - present" {
          let addressReader =
              jsonReader {
                  let! city = Req.Prop.string "city"
                  return city
              }

          let personReader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  and! city = Opt.Prop.object addressReader "address"
                  return {| Name = name; City = city |}
              }

          parse
              personReader
              {| name = "Alice"
                 address = {| city = "NYC" |} |}
          |> Expect.equal "" (Ok {| Name = "Alice"; City = Some "NYC" |})
      }

      test "read optional nested object - missing" {
          let addressReader =
              jsonReader {
                  let! city = Req.Prop.string "city"
                  return city
              }

          let personReader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  and! city = Opt.Prop.object addressReader "address"
                  return {| Name = name; City = city |}
              }

          parse personReader {| name = "Alice" |}
          |> Expect.equal "" (Ok {| Name = "Alice"; City = None |})
      }

      test "read optional nested object - invalid" {
          let addressReader =
              jsonReader {
                  let! city = Req.Prop.string "city"
                  return city
              }

          let personReader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  and! city = Opt.Prop.object addressReader "address"
                  return {| Name = name; City = city |}
              }

          parse personReader {| name = "Alice"; address = {| |} |}
          |> Expect.equal "" (Ok {| Name = "Alice"; City = None |})
      }

      test "nested optional with Option.flatten" {
          let reader =
              jsonReader {
                  let! maybeName = Opt.Prop.object (Opt.Prop.string "name") "user"
                  return Option.flatten maybeName
              }

          parse reader {| |} |> Expect.equal "missing user" (Ok None)

          parse reader {| user = {| |} |} |> Expect.equal "user missing name" (Ok None)

          parse reader {| user = {| name = "Alice" |} |}
          |> Expect.equal "user with name" (Ok(Some "Alice"))
      }

      test "mapError transforms errors" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  return name
              }
              |> JsonReader.mapError (List.map (sprintf "User validation: %s"))

          parse reader {| |}
          |> Expect.wantError "Expected error"
          |> List.head
          |> Expect.equal "" "User validation: Property 'name' not found"
      }

      test "case insensitive property names" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  return name
              }

          parse reader {| NaMe = "Alice" |} |> Expect.equal "" (Ok "Alice")

          parse reader {| NAME = "Bob" |} |> Expect.equal "" (Ok "Bob")
      }

      test "inline nested object reader" {
          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"

                  and! address =
                      Req.Prop.object
                          (jsonReader {
                              let! street = Req.Prop.string "street"
                              and! city = Req.Prop.string "city"
                              return street, city
                          })
                          "address"

                  return {| Name = name; Address = address |}
              }

          parse
              reader
              {| name = "Alice"
                 address = {| street = "Main St"; city = "NYC" |} |}
          |> Expect.equal
              ""
              (Ok
                  {| Name = "Alice"
                     Address = "Main St", "NYC" |})
      }

      test "JsonObjectReader.map" {
          let reader = Req.Prop.string "name" |> JsonReader.map String.length

          parse reader {| name = "Alice" |} |> Expect.equal "" (Ok 5)
      }

      test "JsonObjectReader.bind" {
          let reader =
              Req.Prop.string "type"
              |> JsonReader.bind (fun t ->
                  match t with
                  | "email" -> Req.Prop.string "emailAddress"
                  | "phone" -> Req.Prop.string "phoneNumber"
                  | _ -> JsonReader.retn "unknown")

          parse
              reader
              {| ``type`` = "email"
                 emailAddress = "test@example.com" |}
          |> Expect.equal "" (Ok "test@example.com")

          parse
              reader
              {| ``type`` = "phone"
                 phoneNumber = "123-456" |}
          |> Expect.equal "" (Ok "123-456")
      }

      // ========== Conditional Reading Tests ==========

      test "conditional - validation changes based on another field" {
          let reader =
              jsonReader {
                  let! age = Req.Prop.int32 "age"
                  let! consent = Opt.Prop.bool "parentalConsent"

                  let isValid =
                      if age < 18 then
                          match consent with
                          | Some true -> true
                          | _ -> false
                      else
                          true

                  return {| Age = age; IsValid = isValid |}
              }

          parse reader {| age = 25 |}
          |> Expect.equal "" (Ok {| Age = 25; IsValid = true |})

          parse reader {| age = 16; parentalConsent = true |}
          |> Expect.equal "" (Ok {| Age = 16; IsValid = true |})

          parse reader {| age = 16 |}
          |> Expect.equal "" (Ok {| Age = 16; IsValid = false |})
      }

      test "conditional - read different property based on type field" {
          let makeReader contactType =
              match contactType with
              | "email" -> Req.Prop.string "email"
              | "phone" -> Req.Prop.string "phone"
              | _ -> JsonReader.retn "unknown"

          let reader = Req.Prop.string "type" |> JsonReader.bind makeReader

          parse
              reader
              {| ``type`` = "email"
                 email = "test@example.com" |}
          |> Expect.equal "" (Ok "test@example.com")

          parse
              reader
              {| ``type`` = "phone"
                 phone = "+1234567" |}
          |> Expect.equal "" (Ok "+1234567")

          parse reader {| ``type`` = "unknown" |} |> Expect.equal "" (Ok "unknown")
      }

      test "conditional - optional field determines if more reading required" {
          let makeReader name isAdmin =
              match isAdmin with
              | Some true ->
                  Req.Prop.string "permissions"
                  |> JsonReader.map (fun perms ->
                      {| Name = name
                         Permissions = Some perms |})
              | _ -> JsonReader.retn {| Name = name; Permissions = None |}

          let reader =
              jsonReader {
                  let! name = Req.Prop.string "name"
                  let! isAdmin = Opt.Prop.bool "isAdmin"
                  return! makeReader name isAdmin
              }

          parse
              reader
              {| name = "Alice"
                 isAdmin = true
                 permissions = "admin" |}
          |> Expect.equal
              ""
              (Ok
                  {| Name = "Alice"
                     Permissions = Some "admin" |})

          parse reader {| name = "Bob" |}
          |> Expect.equal "" (Ok {| Name = "Bob"; Permissions = None |})

          parse reader {| name = "Charlie"; isAdmin = true |}
          |> Expect.wantError "should fail without permissions"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "conditional - choose nested object based on discriminator" {
          let cardDetailsReader =
              Req.Prop.string "last4" |> JsonReader.map (sprintf "Card ending in %s")

          let bankDetailsReader =
              Req.Prop.string "accountNumber" |> JsonReader.map (sprintf "Bank account %s")

          let makeReader paymentType =
              match paymentType with
              | "card" -> Req.Prop.object cardDetailsReader "details"
              | "bank" -> Req.Prop.object bankDetailsReader "details"
              | _ -> JsonReader.retn "Unknown payment method"

          let reader = Req.Prop.string "paymentType" |> JsonReader.bind makeReader

          parse
              reader
              {| paymentType = "card"
                 details = {| last4 = "1234" |} |}
          |> Expect.equal "" (Ok "Card ending in 1234")

          parse
              reader
              {| paymentType = "bank"
                 details = {| accountNumber = "ACC123" |} |}
          |> Expect.equal "" (Ok "Bank account ACC123")

          parse reader {| paymentType = "crypto" |}
          |> Expect.equal "" (Ok "Unknown payment method")
      }

      // ========== JsonElement Reading Tests ==========

      test "read required JsonElement value - string" {
          parse Req.Value.jsonElement "hello world"
          |> Expect.wantOk "should succeed"
          |> (fun elem -> elem.GetString())
          |> Expect.equal "" "hello world"
      }

      test "read required JsonElement property - string" {
          parse (Req.Prop.jsonElement "data") {| data = "test value" |}
          |> Expect.wantOk "should succeed"
          |> (fun elem -> elem.GetString())
          |> Expect.equal "" "test value"
      }

      test "read required JsonElement property - object" {
          parse (Req.Prop.jsonElement "metadata") {| metadata = {| key = "value"; count = 5 |} |}
          |> Expect.wantOk "should succeed"
          |> (fun elem ->
              elem.ValueKind = JsonValueKind.Object
              && elem.GetProperty("key").GetString() = "value"
              && elem.GetProperty("count").GetInt32() = 5)
          |> Expect.isTrue "should be object with correct properties"
      }

      test "read optional JsonElement property - present string" {
          parse (Opt.Prop.jsonElement "data") {| data = "test" |}
          |> Expect.wantOk "should succeed"
          |> Option.map (fun elem -> elem.GetString())
          |> Expect.equal "" (Some "test")
      }

      test "read optional JsonElement property - missing" {
          parse (Opt.Prop.jsonElement "missing") {| other = "value" |}
          |> Expect.equal "" (Ok None)
      }

      test "JsonElement preserves nested structure" {
          parse (Req.Prop.jsonElement "data") {| data = {| level1 = {| level2 = {| level3 = "deep value" |} |} |} |}
          |> Expect.wantOk "should succeed"
          |> (fun elem -> elem.GetProperty("level1").GetProperty("level2").GetProperty("level3").GetString())
          |> Expect.equal "" "deep value"
      }

      // ========== arrayAny Tests ==========

      test "Req.Value.arrayAny - successfully reads valid items and skips invalid ones" {
          let json = """[1, 2, "not a number", 3, null, 4]"""

          let result =
              json
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement (Req.Value.arrayAny Req.Value.int32)

          result |> Expect.equal "" (Ok [ 1; 2; 3; 4 ])
      }

      test "Req.Prop.arrayAny - successfully reads valid array items and skips invalid ones" {
          let reader = Req.Prop.arrayAny Req.Value.int32 "numbers"

          parse reader {| numbers = [| 10; 20; 30 |] |}
          |> Expect.equal "all valid items" (Ok [ 10; 20; 30 ])

          let json = """{"numbers": [1, "invalid", 2, false, 3]}"""

          let result =
              json |> JsonDocument.Parse |> _.RootElement |> JsonReader.readJsonElement reader

          result |> Expect.equal "mixed valid/invalid items" (Ok [ 1; 2; 3 ])
      }

      test "Opt.Value.arrayAny - returns Some list with valid items" {
          let json = """[1, 2, "not a number", 3]"""

          let result =
              json
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement (Opt.Value.arrayAny Req.Value.int32)

          result |> Expect.equal "" (Ok(Some [ 1; 2; 3 ]))

          // Test with non-array returns None
          let nonArrayResult =
              JsonDocument.Parse("\"not an array\"").RootElement
              |> JsonReader.readJsonElement (Opt.Value.arrayAny Req.Value.int32)

          nonArrayResult |> Expect.equal "non-array" (Ok None)
      }

      test "Opt.Prop.arrayAny - returns Some list with valid items when property exists" {
          let reader = Opt.Prop.arrayAny Req.Value.string "values"

          let json = """{"values": ["hello", 123, "world", null, "test"]}"""

          let result =
              json |> JsonDocument.Parse |> _.RootElement |> JsonReader.readJsonElement reader

          result |> Expect.equal "mixed items" (Ok(Some [ "hello"; "world"; "test" ]))

          // Test with missing property returns None
          parse reader {| other = "data" |} |> Expect.equal "missing property" (Ok None)
      }

      // ========== oneOf Tests ==========

      test "Req.Value.oneOf - tries multiple readers until one succeeds" {
          let reader =
              Req.Value.oneOf
                  [ Req.Value.string |> JsonReader.map (fun s -> sprintf "String: %s" s)
                    Req.Value.int32 |> JsonReader.map (fun i -> sprintf "Number: %d" i) ]

          parse reader "hello" |> Expect.equal "string value" (Ok "String: hello")
          parse reader 42 |> Expect.equal "int value" (Ok "Number: 42")

          parse reader true
          |> Expect.wantError "no readers succeed"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "Req.Value.oneOf - returns first successful reader" {
          let reader =
              Req.Value.oneOf
                  [ Req.Value.string |> JsonReader.map (fun _ -> "first")
                    Req.Value.string |> JsonReader.map (fun _ -> "second") ]

          parse reader "test" |> Expect.equal "" (Ok "first")
      }

      test "Req.Prop.oneOf - tries multiple readers for a property" {
          let reader =
              Req.Prop.oneOf
                  "value"
                  [ Req.Value.string |> JsonReader.map (fun s -> sprintf "String: %s" s)
                    Req.Value.int32 |> JsonReader.map (fun i -> sprintf "Int: %d" i)
                    Req.Value.bool |> JsonReader.map (fun b -> sprintf "Bool: %b" b) ]

          parse reader {| value = "text" |}
          |> Expect.equal "string property" (Ok "String: text")

          parse reader {| value = 123 |} |> Expect.equal "int property" (Ok "Int: 123")

          parse reader {| value = true |}
          |> Expect.equal "bool property" (Ok "Bool: true")
      }

      test "Req.Prop.oneOf - fails when property is missing" {
          let reader =
              Req.Prop.oneOf
                  "value"
                  [ Req.Value.string |> JsonReader.map box
                    Req.Value.int32 |> JsonReader.map box ]

          parse reader {| other = "data" |}
          |> Expect.wantError "missing property"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "Opt.Value.oneOf - returns Some when a reader succeeds" {
          let reader =
              Opt.Value.oneOf
                  [ Req.Value.string |> JsonReader.map box
                    Req.Value.int32 |> JsonReader.map box ]

          parse reader "hello" |> Expect.equal "string" (Ok(Some(box "hello")))

          parse reader 42 |> Expect.equal "number" (Ok(Some(box 42)))

          parse reader true |> Expect.equal "no match" (Ok None)
      }

      test "Opt.Prop.oneOf - returns Some when property exists and a reader succeeds" {
          let reader =
              Opt.Prop.oneOf
                  "data"
                  [ Req.Value.string |> JsonReader.map box
                    Req.Value.int32 |> JsonReader.map box ]

          parse reader {| data = "test" |}
          |> Expect.equal "string property" (Ok(Some(box "test")))

          parse reader {| data = 999 |} |> Expect.equal "int property" (Ok(Some(box 999)))

          parse reader {| data = true |} |> Expect.equal "wrong type" (Ok None)

          parse reader {| other = "value" |} |> Expect.equal "missing property" (Ok None)
      }

      test "oneOf with discriminated union - heterogeneous array example" {
          let itemReader =
              Req.Value.oneOf
                  [ Req.Value.string |> JsonReader.map StringValue
                    Req.Value.int32 |> JsonReader.map NumberValue
                    Req.Value.bool |> JsonReader.map BoolValue ]

          let arrayReader = Req.Value.arrayAny itemReader

          let json =
              """[
                    "hello",
                    42,
                    true,
                    "world",
                    123,
                    false,
                    null,
                    { "nested": "object" }
                ]"""

          let result =
              json
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement arrayReader

          let expected =
              [ StringValue "hello"
                NumberValue 42
                BoolValue true
                StringValue "world"
                NumberValue 123
                BoolValue false ]

          result |> Expect.equal "" (Ok expected)
      }

      test "oneOf with discriminated union - heterogeneous object properties" {
          let emailReader = Req.Prop.string "email" |> JsonReader.map Email
          let phoneReader = Req.Prop.string "phone" |> JsonReader.map Phone

          let socialMediaReader =
              jsonReader {
                  let! platform = Req.Prop.string "platform"
                  and! handle = Req.Prop.string "handle"
                  return SocialMedia(platform, handle)
              }

          let contactReader =
              Req.Prop.oneOf "contact" [ emailReader; phoneReader; socialMediaReader ]

          parse contactReader {| contact = {| email = "test@example.com" |} |}
          |> Expect.equal "email contact" (Ok(Email "test@example.com"))

          parse contactReader {| contact = {| phone = "+1-555-1234" |} |}
          |> Expect.equal "phone contact" (Ok(Phone "+1-555-1234"))

          parse
              contactReader
              {| contact =
                  {| platform = "twitter"
                     handle = "@user" |} |}
          |> Expect.equal "social media contact" (Ok(SocialMedia("twitter", "@user")))
      }

      test "oneOf with nested objects in array" {
          let circleReader =
              jsonReader {
                  let! kind = Req.Prop.string "kind"
                  let! radius = Req.Prop.double "radius"

                  match kind with
                  | "circle" -> return Circle radius
                  | _ -> return! JsonReader.err [ "Not a circle" ]
              }

          let rectangleReader =
              jsonReader {
                  let! kind = Req.Prop.string "kind"
                  let! width = Req.Prop.double "width"
                  let! height = Req.Prop.double "height"

                  match kind with
                  | "rectangle" -> return Rectangle(width, height)
                  | _ -> return! JsonReader.err [ "Not a rectangle" ]
              }

          let shapesReader =
              Req.Prop.arrayAny (Req.Value.oneOf [ circleReader; rectangleReader ]) "shapes"

          let json =
              """{
                  "shapes": [
                      { "kind": "circle", "radius": 5.0 },
                      { "kind": "rectangle", "width": 10.0, "height": 20.0 },
                      { "kind": "circle", "radius": 3.5 },
                      { "kind": "unknown", "data": "ignored" }
                  ]
              }"""

          let result =
              json
              |> JsonDocument.Parse
              |> _.RootElement
              |> JsonReader.readJsonElement shapesReader

          let expected = [ Circle 5.0; Rectangle(10.0, 20.0); Circle 3.5 ]

          result |> Expect.equal "" (Ok expected)
      }

      test "nested object error messages include property context" {
          let reader = Req.Prop.object (Req.Prop.int32 "userId") "user"

          parse reader {| user = {| userId = "not-a-number" |} |}
          |> Expect.wantError "Expected error"
          |> List.exists (fun e -> e.Contains "user." && e.Contains "userId")
          |> Expect.isTrue "Error should include nested property path"
      }

      test "array error messages include property and index context" {
          let reader = Req.Prop.array (Req.Prop.int32 "id") "items"

          """{"items": [{"id": 1}, {"id": "bad"}, {"id": 3}]}"""
          |> JsonDocument.Parse
          |> _.RootElement
          |> JsonReader.readJsonElement reader
          |> Expect.wantError "Expected error"
          |> List.exists (fun e -> e.Contains "items[" && e.Contains "id")
          |> Expect.isTrue "Error should include array property and index"
      }

      // SRTP API tests
      test "prop - type-driven required string" {
          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  return name
              }

          parse reader {| name = "Alice" |} |> Expect.equal "" (Ok "Alice")

          parse reader {| name = 123 |}
          |> Expect.wantError "wrong type"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "prop - type-driven required primitives" {
          let reader =
              jsonReader {
                  let! age: int = prop "age"
                  let! active: bool = prop "active"
                  let! price: decimal = prop "price"
                  return {| Age = age; Active = active; Price = price |}
              }

          parse reader {| age = 42; active = true; price = 99.99 |}
          |> Expect.equal "" (Ok {| Age = 42; Active = true; Price = 99.99m |})
      }

      test "prop - type-driven required Guid" {
          let guid = Guid.Parse("12345678-1234-1234-1234-123456789abc")

          let reader =
              jsonReader {
                  let! id: Guid = prop "id"
                  return id
              }

          parse reader {| id = guid |} |> Expect.equal "" (Ok guid)
      }

      test "prop - type-driven optional string" {
          let reader =
              jsonReader {
                  let! name: string option = prop "name"
                  return name
              }

          parse reader {| name = "Alice" |} |> Expect.equal "" (Ok(Some "Alice"))
          parse reader {| other = "value" |} |> Expect.equal "" (Ok None)
          parse reader {| name = 123 |} |> Expect.equal "" (Ok None)
      }

      test "prop - type-driven optional primitives" {
          let reader =
              jsonReader {
                  let! age: int option = prop "age"
                  let! active: bool option = prop "active"
                  let! price: decimal option = prop "price"
                  return {| Age = age; Active = active; Price = price |}
              }

          parse reader {| age = 42; active = true; price = 99.99 |}
          |> Expect.equal "" (Ok {| Age = Some 42; Active = Some true; Price = Some 99.99m |})

          parse reader {| other = "value" |}
          |> Expect.equal "" (Ok {| Age = None; Active = None; Price = None |})
      }

      test "prop - type-driven optional Guid" {
          let guid = Guid.Parse("12345678-1234-1234-1234-123456789abc")

          let reader =
              jsonReader {
                  let! id: Guid option = prop "id"
                  return id
              }

          parse reader {| id = guid |} |> Expect.equal "" (Ok(Some guid))
          parse reader {| other = "value" |} |> Expect.equal "" (Ok None)
      }

      test "prop - mixed required and optional in single reader" {
          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! age: int = prop "age"
                  let! email: string option = prop "email"
                  let! phone: string option = prop "phone"
                  return {| Name = name; Age = age; Email = email; Phone = phone |}
              }

          parse reader {| name = "Alice"; age = 30; email = "alice@example.com" |}
          |> Expect.equal
              ""
              (Ok
                  {|
                      Name = "Alice"
                      Age = 30
                      Email = Some "alice@example.com"
                      Phone = None
                  |})
      }

      test "mixed SRTP and explicit API" {
          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! age = Req.Prop.int32 "age"
                  let! email: string option = prop "email"
                  let! phone = Opt.Prop.string "phone"
                  return {| Name = name; Age = age; Email = email; Phone = phone |}
              }

          parse reader {| name = "Alice"; age = 30; email = "alice@example.com" |}
          |> Expect.equal
              ""
              (Ok
                  {|
                      Name = "Alice"
                      Age = 30
                      Email = Some "alice@example.com"
                      Phone = None
                  |})
      }

      test "Prop.read - type-driven required object" {
          let addressReader =
              jsonReader {
                  let! street: string = prop "street"
                  let! city: string = prop "city"
                  return {| Street = street; City = city |}
              }

          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! address: {| Street: string; City: string |} = Prop.read (addressReader, "address")
                  return {| Name = name; Address = address |}
              }

          parse
              reader
              {|
                  name = "Alice"
                  address = {| street = "123 Main St"; city = "Boston" |}
              |}
          |> Expect.equal
              ""
              (Ok
                  {|
                      Name = "Alice"
                      Address = {| Street = "123 Main St"; City = "Boston" |}
                  |})

          parse reader {| name = "Alice" |}
          |> Expect.wantError "missing object"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "Prop.read - type-driven optional object" {
          let addressReader =
              jsonReader {
                  let! street: string = prop "street"
                  let! city: string = prop "city"
                  return {| Street = street; City = city |}
              }

          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! address: {| Street: string; City: string |} option = Prop.read (addressReader, "address")
                  return {| Name = name; Address = address |}
              }

          parse
              reader
              {|
                  name = "Alice"
                  address = {| street = "123 Main St"; city = "Boston" |}
              |}
          |> Expect.equal
              ""
              (Ok
                  {|
                      Name = "Alice"
                      Address = Some {| Street = "123 Main St"; City = "Boston" |}
                  |})

          parse reader {| name = "Alice" |}
          |> Expect.equal "" (Ok {| Name = "Alice"; Address = None |})
      }

      test "Prop.read - type-driven required array" {
          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! tags: string list = Prop.read (Req.Value.string, "tags")
                  return {| Name = name; Tags = tags |}
              }

          parse reader {| name = "Alice"; tags = [ "a"; "b"; "c" ] |}
          |> Expect.equal "" (Ok {| Name = "Alice"; Tags = [ "a"; "b"; "c" ] |})

          parse reader {| name = "Alice" |}
          |> Expect.wantError "missing array"
          |> List.isEmpty
          |> Expect.isFalse "should have error messages"
      }

      test "Prop.read - type-driven optional array" {
          let reader =
              jsonReader {
                  let! name: string = prop "name"
                  let! tags: string list option = Prop.read (Req.Value.string, "tags")
                  return {| Name = name; Tags = tags |}
              }

          parse reader {| name = "Alice"; tags = [ "a"; "b"; "c" ] |}
          |> Expect.equal "" (Ok {| Name = "Alice"; Tags = Some [ "a"; "b"; "c" ] |})

          parse reader {| name = "Alice" |}
          |> Expect.equal "" (Ok {| Name = "Alice"; Tags = None |})
      }

      test "Prop.read - with object item reader for array" {
          let itemReader =
              jsonReader {
                  let! id: int = prop "id"
                  let! name: string = prop "name"
                  return {| Id = id; Name = name |}
              }

          let reader =
              jsonReader {
                  let! items: {| Id: int; Name: string |} list = Prop.read (itemReader, "items")
                  return items
              }

          parse reader {| items = [ {| id = 1; name = "A" |}; {| id = 2; name = "B" |} ] |}
          |> Expect.equal "" (Ok [ {| Id = 1; Name = "A" |}; {| Id = 2; Name = "B" |} ])
      }

      ]

    |> testList "JsonReader"
