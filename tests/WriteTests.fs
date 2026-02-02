module Json.Ok.WriteTests

open System
open System.Text.Json
open Expecto
open Expecto.Flip
open Json.Ok.Write

let serialize json =
    json |> toJsonElement |> JsonSerializer.Serialize

[<Tests>]
let tests =
    [ test "simple object with primitives" {
          let result =
              jsonObject {
                  "id" => 101
                  "active" => true
                  "name" => "Test User"
                  "price" => 99.99m
              }
              |> serialize

          let expected = """{"id":101,"active":true,"name":"Test User","price":99.99}"""

          result |> Expect.equal "" expected
      }

      test "nested object using => operator" {
          let result =
              jsonObject {
                  "id" => 101
                  "active" => true

                  "metadata"
                  => jsonObject {
                      "created_by" => "FSharp"
                      "version" => 2
                  }
              }
              |> serialize

          let doc = JsonDocument.Parse(result)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 101
          doc.RootElement.GetProperty("active").GetBoolean() |> Expect.equal "active" true

          doc.RootElement.GetProperty("metadata").GetProperty("created_by").GetString()
          |> Expect.equal "created_by" "FSharp"

          doc.RootElement.GetProperty("metadata").GetProperty("version").GetInt32()
          |> Expect.equal "version" 2
      }

      test "array of objects using => operator" {
          let result =
              jsonObject {
                  "items"
                  => [ for sku, price in [ "ABC", 10; "XYZ", 25 ] do
                           jsonObject {
                               "sku" => sku
                               "price" => price
                           } ]
              }
              |> serialize

          let doc = JsonDocument.Parse(result)
          let items = doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 2
          items[0].GetProperty("sku").GetString() |> Expect.equal "first sku" "ABC"
          items[0].GetProperty("price").GetInt32() |> Expect.equal "first price" 10
          items[1].GetProperty("sku").GetString() |> Expect.equal "second sku" "XYZ"
          items[1].GetProperty("price").GetInt32() |> Expect.equal "second price" 25
      }

      test "array of primitives using => operator" {
          let result =
              jsonObject { "tags" => [ "functional"; "dotnet"; "fsharp" ] } |> serialize

          let doc = JsonDocument.Parse(result)

          let tags =
              doc.RootElement.GetProperty("tags").EnumerateArray()
              |> Seq.map _.GetString()
              |> Seq.toList

          tags |> Expect.equal "" [ "functional"; "dotnet"; "fsharp" ]
      }

      test "array with literal primitives" {
          let result = jsonObject { "scores" => [ 95; 87; 92 ] } |> serialize

          let doc = JsonDocument.Parse(result)

          let scores =
              doc.RootElement.GetProperty("scores").EnumerateArray()
              |> Seq.map _.GetInt32()
              |> Seq.toList

          scores |> Expect.equal "" [ 95; 87; 92 ]
      }

      test "optional properties - Some values are written" {
          let result =
              jsonObject {
                  "id" => 101
                  "description" => Some "A description"
                  "count" => Some 42
              }
              |> serialize

          let doc = JsonDocument.Parse(result)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 101

          doc.RootElement.GetProperty("description").GetString()
          |> Expect.equal "description" "A description"

          doc.RootElement.GetProperty("count").GetInt32() |> Expect.equal "count" 42
      }

      test "optional properties - None values are omitted" {
          let result =
              jsonObject {
                  "id" => 101
                  "notes" => (None: string option)
                  "enabled" => (None: bool option)
              }
              |> serialize

          let doc = JsonDocument.Parse(result)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 101

          doc.RootElement.TryGetProperty("notes")
          |> fst
          |> Expect.isFalse "notes should not exist"

          doc.RootElement.TryGetProperty("enabled")
          |> fst
          |> Expect.isFalse "enabled should not exist"
      }

      test "top-level array" {
          let arr: JsonWriter<Item> =
              jsonArray {
                  for i in [ 1; 2; 3 ] do
                      i
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize

          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 3
          items[0].GetInt32() |> Expect.equal "first" 1
          items[1].GetInt32() |> Expect.equal "second" 2
          items[2].GetInt32() |> Expect.equal "third" 3
      }

      test "complex nested structure" {
          let result =
              jsonObject {
                  "id" => 123
                  "name" => "Charlie"

                  "profile"
                  => jsonObject {
                      "age" => 28
                      "active" => true
                  }

                  "scores"
                  => jsonArray {
                      for score in [ 95; 87; 92 ] do
                          score
                  }

                  "addresses"
                  => jsonArray {
                      for city in [ "NYC"; "LA"; "Chicago" ] do
                          jsonObject {
                              "city" => city
                              "country" => "USA"
                          }
                  }
              }
              |> serialize

          let doc = JsonDocument.Parse(result)

          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 123
          doc.RootElement.GetProperty("name").GetString() |> Expect.equal "name" "Charlie"

          doc.RootElement.GetProperty("profile").GetProperty("age").GetInt32()
          |> Expect.equal "age" 28

          doc.RootElement.GetProperty("profile").GetProperty("active").GetBoolean()
          |> Expect.equal "active" true

          let scores =
              doc.RootElement.GetProperty("scores").EnumerateArray()
              |> Seq.map _.GetInt32()
              |> Seq.toList

          scores |> Expect.equal "scores" [ 95; 87; 92 ]

          let addresses =
              doc.RootElement.GetProperty("addresses").EnumerateArray() |> Seq.toList

          addresses.Length |> Expect.equal "address count" 3
          addresses[0].GetProperty("city").GetString() |> Expect.equal "first city" "NYC"
          addresses[1].GetProperty("city").GetString() |> Expect.equal "second city" "LA"

          addresses[2].GetProperty("city").GetString()
          |> Expect.equal "third city" "Chicago"
      }

      test "standalone value - int" {
          let result = JsonWriter.valueInt 45 |> toJsonElement |> JsonSerializer.Serialize

          result |> Expect.equal "" "45"
      }

      test "standalone value - string" {
          let result = JsonWriter.value "hello" |> toJsonElement |> JsonSerializer.Serialize

          result |> Expect.equal "" "\"hello\""
      }

      test "standalone value - bool" {
          let result = JsonWriter.valueBool true |> toJsonElement |> JsonSerializer.Serialize

          result |> Expect.equal "" "true"
      }

      test "standalone value - null" {
          let result = JsonWriter.valueNull |> toJsonElement |> JsonSerializer.Serialize

          result |> Expect.equal "" "null"
      }

      test "all primitive types in object" {
          let guid = Guid.Parse("12345678-1234-1234-1234-123456789abc")
          let dateTime = DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
          let dateTimeOffset = DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)

          let result =
              jsonObject {
                  "stringVal" => "test"
                  "intVal" => 42
                  "int64Val" => 9223372036854775807L
                  "boolVal" => true
                  "doubleVal" => 3.14159
                  "decimalVal" => 99.99m
                  "guidVal" => guid
                  "dateTimeVal" => dateTime
                  "dateTimeOffsetVal" => dateTimeOffset
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          doc.RootElement.GetProperty("stringVal").GetString()
          |> Expect.equal "string" "test"

          doc.RootElement.GetProperty("intVal").GetInt32() |> Expect.equal "int" 42

          doc.RootElement.GetProperty("int64Val").GetInt64()
          |> Expect.equal "int64" 9223372036854775807L

          doc.RootElement.GetProperty("boolVal").GetBoolean() |> Expect.equal "bool" true

          doc.RootElement.GetProperty("doubleVal").GetDouble()
          |> Expect.equal "double" 3.14159

          doc.RootElement.GetProperty("decimalVal").GetDecimal()
          |> Expect.equal "decimal" 99.99m

          doc.RootElement.GetProperty("guidVal").GetGuid() |> Expect.equal "guid" guid

          doc.RootElement.GetProperty("dateTimeVal").GetDateTime()
          |> Expect.equal "dateTime" dateTime

          doc.RootElement.GetProperty("dateTimeOffsetVal").GetDateTimeOffset()
          |> Expect.equal "dateTimeOffset" dateTimeOffset
      }

      test "mixed primitive types in array" {
          let arr: JsonWriter<Item> =
              jsonArray {
                  for item in [ "test"; "hello"; "world" ] do
                      item
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize

          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 3
          items[0].GetString() |> Expect.equal "first" "test"
          items[1].GetString() |> Expect.equal "second" "hello"
          items[2].GetString() |> Expect.equal "third" "world"
      }

      test "empty object" {
          let result = jsonObject { () } |> serialize

          result |> Expect.equal "" "{}"
      }

      test "empty array" {
          let arr: JsonWriter<Item> = jsonArray { () }
          let result = arr |> toJsonElement |> JsonSerializer.Serialize

          result |> Expect.equal "" "[]"
      }

      test "base64 string property" {
          let bytes = [| 72uy; 101uy; 108uy; 108uy; 111uy |] // "Hello"
          let expected = Convert.ToBase64String(bytes)

          let result =
              jsonObject {
                  "id" => 1
                  "data" => bytes
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 1
          doc.RootElement.GetProperty("data").GetString() |> Expect.equal "data" expected
      }

      test "base64 string in array" {
          let bytes1 = [| 1uy; 2uy; 3uy |]
          let bytes2 = [| 4uy; 5uy; 6uy |]
          let expected1 = Convert.ToBase64String(bytes1)
          let expected2 = Convert.ToBase64String(bytes2)

          let arr: JsonWriter<Item> =
              jsonArray {
                  for b in [ bytes1; bytes2 ] do
                      b
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize

          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 2
          items[0].GetString() |> Expect.equal "first" expected1
          items[1].GetString() |> Expect.equal "second" expected2
      }

      test "optional base64 string property - Some" {
          let bytes = [| 10uy; 20uy; 30uy |]
          let expected = Convert.ToBase64String(bytes)

          let result =
              jsonObject {
                  "id" => 1
                  "data" => Some bytes
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          doc.RootElement.GetProperty("data").GetString() |> Expect.equal "data" expected
      }

      test "optional base64 string property - None" {
          let result =
              jsonObject {
                  "id" => 1
                  "data" => (None: byte[] option)
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          doc.RootElement.TryGetProperty("data")
          |> fst
          |> Expect.isFalse "data should not exist"
      }

      test "heterogeneous array - mixed primitive types" {
          let guid = Guid.Parse("12345678-1234-1234-1234-123456789abc")
          let dateTime = DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)

          let arr: JsonWriter<Item> =
              jsonArray {
                  42
                  "hello"
                  true
                  3.14
                  99.99m
                  guid
                  dateTime
                  [| 1uy; 2uy; 3uy |]
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize
          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 8
          items[0].GetInt32() |> Expect.equal "int" 42
          items[1].GetString() |> Expect.equal "string" "hello"
          items[2].GetBoolean() |> Expect.equal "bool" true
          items[3].GetDouble() |> Expect.equal "double" 3.14
          items[4].GetDecimal() |> Expect.equal "decimal" 99.99m
          items[5].GetGuid() |> Expect.equal "guid" guid
          items[6].GetDateTime() |> Expect.equal "dateTime" dateTime

          items[7].GetString()
          |> Expect.equal "base64" (Convert.ToBase64String([| 1uy; 2uy; 3uy |]))
      }

      test "heterogeneous array - mixed objects and primitives" {
          let arr: JsonWriter<Item> =
              jsonArray {
                  1
                  "text"

                  jsonObject {
                      "name" => "Alice"
                      "age" => 30
                  }

                  true

                  jsonObject {
                      "city" => "NYC"
                      "country" => "USA"
                  }
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize
          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 5
          items[0].GetInt32() |> Expect.equal "first" 1
          items[1].GetString() |> Expect.equal "second" "text"
          items[2].ValueKind |> Expect.equal "third is object" JsonValueKind.Object
          items[2].GetProperty("name").GetString() |> Expect.equal "name" "Alice"
          items[2].GetProperty("age").GetInt32() |> Expect.equal "age" 30
          items[3].GetBoolean() |> Expect.equal "fourth" true
          items[4].ValueKind |> Expect.equal "fifth is object" JsonValueKind.Object
          items[4].GetProperty("city").GetString() |> Expect.equal "city" "NYC"
      }

      test "heterogeneous array - nested arrays and objects" {
          let arr: JsonWriter<Item> =
              jsonArray {
                  jsonArray {
                      1
                      2
                      3
                  }

                  "separator"

                  jsonObject {
                      "nested"
                      => jsonArray {
                          "a"
                          "b"
                          "c"
                      }
                  }
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize
          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 3
          items[0].ValueKind |> Expect.equal "first is array" JsonValueKind.Array
          items[0].EnumerateArray() |> Seq.length |> Expect.equal "nested array length" 3
          items[1].GetString() |> Expect.equal "separator" "separator"
          items[2].ValueKind |> Expect.equal "third is object" JsonValueKind.Object

          let nestedArray = items[2].GetProperty("nested").EnumerateArray() |> Seq.toList
          nestedArray.Length |> Expect.equal "nested array in object" 3
          nestedArray[0].GetString() |> Expect.equal "first nested" "a"
      }

      test "JsonElement property" {
          let existingElement =
              JsonSerializer.SerializeToElement {| foo = "bar"; count = 42 |}

          let result =
              jsonObject {
                  "id" => 1
                  "embedded" => existingElement
                  "after" => "value"
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 1
          doc.RootElement.GetProperty("after").GetString() |> Expect.equal "after" "value"

          let embedded = doc.RootElement.GetProperty("embedded")
          embedded.GetProperty("foo").GetString() |> Expect.equal "foo" "bar"
          embedded.GetProperty("count").GetInt32() |> Expect.equal "count" 42
      }

      test "JsonElement in array" {
          let elem1 = JsonSerializer.SerializeToElement {| type' = "A" |}
          let elem2 = JsonSerializer.SerializeToElement {| type' = "B" |}

          let arr: JsonWriter<Item> =
              jsonArray {
                  elem1
                  "separator"
                  elem2
              }

          let result = arr |> toJsonElement |> JsonSerializer.Serialize
          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 3
          items[0].ValueKind |> Expect.equal "first is object" JsonValueKind.Object
          items[0].GetProperty("type'").GetString() |> Expect.equal "type" "A"
          items[1].GetString() |> Expect.equal "separator" "separator"
          items[2].ValueKind |> Expect.equal "third is object" JsonValueKind.Object
          items[2].GetProperty("type'").GetString() |> Expect.equal "type" "B"
      }

      test "optional JsonElement property - Some" {
          let elem = JsonSerializer.SerializeToElement {| nested = true |}

          let result =
              jsonObject {
                  "id" => 1
                  "data" => Some elem
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          doc.RootElement.GetProperty("data").GetProperty("nested").GetBoolean()
          |> Expect.isTrue "nested"
      }

      test "optional JsonElement property - None" {
          let result =
              jsonObject {
                  "id" => 1
                  "data" => (None: JsonElement option)
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          doc.RootElement.TryGetProperty("data")
          |> fst
          |> Expect.isFalse "data should not exist"
      }

      test "list of primitives - string" {
          let result =
              jsonObject {
                  "id" => 1
                  "tags" => [ "a"; "b"; "c" ]
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 1

          let tags =
              doc.RootElement.GetProperty("tags").EnumerateArray()
              |> Seq.map _.GetString()
              |> Seq.toList

          tags |> Expect.equal "tags" [ "a"; "b"; "c" ]
      }

      test "list of primitives - int" {
          let result =
              jsonObject {
                  "id" => 1
                  "scores" => [ 95; 87; 92 ]
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          let scores =
              doc.RootElement.GetProperty("scores").EnumerateArray()
              |> Seq.map _.GetInt32()
              |> Seq.toList

          scores |> Expect.equal "scores" [ 95; 87; 92 ]
      }

      test "list of objects" {
          let result =
              jsonObject {
                  "items"
                  => [ jsonObject {
                           "id" => 1
                           "name" => "Alice"
                       }
                       jsonObject {
                           "id" => 2
                           "name" => "Bob"
                       } ]
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 2
          items[0].GetProperty("id").GetInt32() |> Expect.equal "first id" 1
          items[0].GetProperty("name").GetString() |> Expect.equal "first name" "Alice"
          items[1].GetProperty("id").GetInt32() |> Expect.equal "second id" 2
          items[1].GetProperty("name").GetString() |> Expect.equal "second name" "Bob"
      }

      test "array of primitives - int" {
          let result =
              jsonObject {
                  "id" => 1
                  "scores" => [| 95; 87; 92 |]
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          let scores =
              doc.RootElement.GetProperty("scores").EnumerateArray()
              |> Seq.map _.GetInt32()
              |> Seq.toList

          scores |> Expect.equal "scores" [ 95; 87; 92 ]
      }

      test "seq of primitives" {
          let tags =
              seq {
                  "functional"
                  "dotnet"
                  "fsharp"
              }

          let result =
              jsonObject {
                  "id" => 1
                  "tags" => tags
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)

          let resultTags =
              doc.RootElement.GetProperty("tags").EnumerateArray()
              |> Seq.map _.GetString()
              |> Seq.toList

          resultTags |> Expect.equal "tags" [ "functional"; "dotnet"; "fsharp" ]
      }

      test "empty list" {
          let result =
              jsonObject {
                  "id" => 1
                  "items" => ([]: int list)
              }
              |> serialize

          let doc = JsonDocument.Parse(result: string)
          doc.RootElement.GetProperty("id").GetInt32() |> Expect.equal "id" 1

          let items = doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList
          items.Length |> Expect.equal "count" 0
      }

      test "list of JsonElements" {
          let elem1 = JsonSerializer.SerializeToElement {| type' = "X" |}
          let elem2 = JsonSerializer.SerializeToElement {| type' = "Y" |}

          let result = jsonObject { "items" => [ elem1; elem2 ] } |> serialize

          let doc = JsonDocument.Parse(result: string)
          let items = doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

          items.Length |> Expect.equal "count" 2
          items[0].GetProperty("type'").GetString() |> Expect.equal "first" "X"
          items[1].GetProperty("type'").GetString() |> Expect.equal "second" "Y"
      }

      // ============================================================================
      // Conditional Branching Tests - Showcase CE Benefits
      // ============================================================================

      test "conditional if-then property inclusion" {
          let createUser includeEmail =
              jsonObject {
                  "id" => 123
                  "name" => "John Doe"

                  if includeEmail then
                      "email" => "john@example.com"
              }
              |> serialize

          let withEmail = createUser true |> JsonDocument.Parse

          withEmail.RootElement.GetProperty("email").GetString()
          |> Expect.equal "has email" "john@example.com"

          let withoutEmail = createUser false |> JsonDocument.Parse

          withoutEmail.RootElement.TryGetProperty("email")
          |> fst
          |> Expect.isFalse "should not have email"
      }

      test "if-then-else conditional branches" {
          let createUser includeEmail isPremium =
              jsonObject {
                  "id" => 123
                  "name" => "John Doe"

                  if includeEmail then
                      "email" => "john@example.com"

                  if isPremium then
                      "tier" => "premium"
                      "credits" => 1000
                  else
                      "tier" => "free"
                      "credits" => 0
              }
              |> serialize

          // Test with email and premium
          let result1 = createUser true true
          let doc1 = JsonDocument.Parse(result1)

          doc1.RootElement.GetProperty("email").GetString()
          |> Expect.equal "has email" "john@example.com"

          doc1.RootElement.GetProperty("tier").GetString()
          |> Expect.equal "tier" "premium"

          doc1.RootElement.GetProperty("credits").GetInt32()
          |> Expect.equal "credits" 1000

          // Test without email, not premium
          let result2 = createUser false false
          let doc2 = JsonDocument.Parse(result2)

          doc2.RootElement.TryGetProperty("email")
          |> fst
          |> Expect.isFalse "should not have email"

          doc2.RootElement.GetProperty("tier").GetString() |> Expect.equal "tier" "free"
          doc2.RootElement.GetProperty("credits").GetInt32() |> Expect.equal "credits" 0
      }

      test "conditional nesting based on runtime values" {
          let createProduct hasDetails hasReviews =
              jsonObject {
                  "id" => 42
                  "name" => "Product"

                  if hasDetails then
                      "details"
                      => jsonObject {
                          "weight" => 2.5

                          "dimensions"
                          => jsonObject {
                              "width" => 10
                              "height" => 20
                              "depth" => 5
                          }
                      }

                  if hasReviews then
                      "reviews"
                      => [ jsonObject {
                               "rating" => 5
                               "comment" => "Excellent!"
                           }
                           jsonObject {
                               "rating" => 4
                               "comment" => "Good"
                           } ]
              }
              |> serialize

          // With both
          let full = createProduct true true |> JsonDocument.Parse

          full.RootElement.GetProperty("details").GetProperty("weight").GetDouble()
          |> Expect.equal "weight" 2.5

          full.RootElement.GetProperty("reviews").EnumerateArray()
          |> Seq.length
          |> Expect.equal "review count" 2

          // With neither
          let minimal = createProduct false false |> JsonDocument.Parse

          minimal.RootElement.TryGetProperty("details")
          |> fst
          |> Expect.isFalse "no details"

          minimal.RootElement.TryGetProperty("reviews")
          |> fst
          |> Expect.isFalse "no reviews"
      }

      test "dynamic property inclusion with loop" {
          let createConfig (features: (string * bool) list) =
              jsonObject {
                  "appName" => "MyApp"
                  "version" => "1.0"

                  for (featureName, enabled) in features do
                      if enabled then
                          featureName => true
              }
              |> serialize

          let result =
              createConfig
                  [ "darkMode", true
                    "analytics", false
                    "notifications", true
                    "debugMode", false ]

          let doc = JsonDocument.Parse(result)

          doc.RootElement.GetProperty("darkMode").GetBoolean()
          |> Expect.isTrue "darkMode enabled"

          doc.RootElement.GetProperty("notifications").GetBoolean()
          |> Expect.isTrue "notifications enabled"

          doc.RootElement.TryGetProperty("analytics")
          |> fst
          |> Expect.isFalse "analytics not enabled"

          doc.RootElement.TryGetProperty("debugMode")
          |> fst
          |> Expect.isFalse "debugMode not enabled"
      }

      test "conditional arrays with guards" {
          let createReport (items: (string * int * bool) list) includeInactive =
              jsonObject {
                  "reportName" => "Status Report"
                  "timestamp" => DateTime(2024, 1, 1)

                  "items"
                  => [ for (name, count, isActive) in items do
                           if includeInactive || isActive then
                               jsonObject {
                                   "name" => name
                                   "count" => count
                                   "active" => isActive
                               } ]
              }
              |> serialize

          let testData =
              [ "Service A", 10, true
                "Service B", 5, false
                "Service C", 3, true
                "Service D", 7, true ]

          // Exclude inactive
          let filtered = createReport testData false |> JsonDocument.Parse

          let filteredItems =
              filtered.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

          filteredItems.Length |> Expect.equal "filtered count" 3

          // Include inactive
          let all = createReport testData true |> JsonDocument.Parse
          let allItems = all.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList
          allItems.Length |> Expect.equal "all count" 4
      }

      test "conditional response structure" {
          // Demonstrate that conditionals can change the structure dynamically
          let createResponse success errorMessage userId =
              jsonObject {
                  "timestamp" => DateTime.UtcNow.ToString("o")

                  if success then
                      "status" => "success"

                      "data"
                      => jsonObject {
                          "userId" => userId
                          "username" => "john"
                      }
                  else
                      "status" => "error"

                      "error"
                      => jsonObject {
                          "message" => errorMessage
                          "code" => 500
                      }
              }
              |> serialize

          // Success case
          let successResp = createResponse true "" 123 |> JsonDocument.Parse

          successResp.RootElement.GetProperty("status").GetString()
          |> Expect.equal "status" "success"

          successResp.RootElement.GetProperty("data").GetProperty("userId").GetInt32()
          |> Expect.equal "userId" 123

          successResp.RootElement.TryGetProperty("error")
          |> fst
          |> Expect.isFalse "no error"

          // Error case
          let errorResp = createResponse false "User not found" 0 |> JsonDocument.Parse

          errorResp.RootElement.GetProperty("status").GetString()
          |> Expect.equal "status" "error"

          errorResp.RootElement.GetProperty("error").GetProperty("message").GetString()
          |> Expect.equal "error msg" "User not found"

          errorResp.RootElement.TryGetProperty("data") |> fst |> Expect.isFalse "no data"
      } ]

    |> testList "JsonWriter"
