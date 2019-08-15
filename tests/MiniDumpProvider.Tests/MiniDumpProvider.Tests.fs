module MiniDumpProviderTests

open NUnit.Framework
open System
open FsUnit
open FSharp.Data.MiniDumpProvider

module Test =

    [<Literal>]
    let public dumpFile = __SOURCE_DIRECTORY__ + @"\..\..\test-data\TestApp.dmp"

type T = MiniDump< DumpFile=Test.dumpFile, AttachDebugger=false >

type Root = T.TestApp.Root
let runtime = T.CreateRuntime()

[<Test>]
let ``Access a value field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ValueField |> should equal 10 

[<Test>]
let ``Access a reference field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ReferenceField |> should equal "10" 

[<Test>]
let ``Access a value field within an object on a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ObjectField.ValueField2 |> should equal 11 

[<Test>]
let ``Access a reference field within an object on a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ObjectField.ReferenceField2 |> should equal "11" 

[<Test>]
let ``Access a value field within a struct on a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.StructField.ValueField2 |> should equal 12 

[<Test>]
let ``Access a reference field within a struct on a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.StructField.ReferenceField2 |> should equal "12" 

[<Test>]
let ``Access length of an array of a value type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfValueField.__Length |> should equal 3 

[<Test>]
let ``Access an array of a value type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfValueField.__EnumerateItems() |> Seq.toArray |> should equal [|13;14;15|]

[<Test>]
let ``Access length of an array of a reference type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfReferenceField.__Length |> should equal 3 

[<Test>]
let ``Access an array of a reference type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfReferenceField.__EnumerateItems() |> Seq.toArray |> should equal [|"13";"14";"15"|]

[<Test>]
let ``Access an array of a complex reference type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfObjectField.__EnumerateItems() 
    |> Seq.map (fun p -> p.ValueField2, p.ReferenceField2) 
    |> Seq.toArray 
    |> should equal [|(16,"16");(17,"17");(18,"18");(19,"19")|]

[<Test>]
let ``Access an array of a complex value type field within a reference type`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.ArrayOfStructField.__EnumerateItems() 
    |> Seq.map (fun p -> p.ValueField2, p.ReferenceField2) 
    |> Seq.toArray 
    |> should equal [|(16,"16");(17,"17");(18,"18");(19,"19")|]

[<Test>]
let ``Access a null field`` () =
    let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
    root.ReferenceType.NullField |> should equal null

// [<Test>]
// let ``Access a boxed value type field`` () =
//     let root = runtime.Heap.EnumerateObjectsOfType<Root>() |> Seq.head
//     root.ReferenceType.BoxedInt |> should equal null    



// TODO:
// null values in array
// enums