namespace FSharp.Data.MiniDumpProvider

open Microsoft.Diagnostics.Runtime
open System.Collections.Generic

module TypeHelper =

    let isPrimitive (t:ClrType) =
        (t.IsPrimitive || t.IsString) && not t.IsEnum

    let getTypes<'T> (heap:ClrHeap) (t:ClrType) (creator:ClrObject->'T) =
        heap.EnumerateObjects()
        |> Seq.filter (fun o -> o.Type.MethodTable = t.MethodTable)
        |> Seq.map creator


module ValueProvider =

    let getValue (clrObject:ClrObject) (name:string) =
        let field = clrObject.Type.GetFieldByName(name)
        let t = field.Type

        if (TypeHelper.isPrimitive t) then
            field.GetValue(clrObject.Address, clrObject.Type.IsValueClass)
        elif (t.IsValueClass) then
            let address = field.GetAddress(clrObject.Address, clrObject.Type.IsValueClass)
            ClrObject(address, field.Type) :> obj
        else
            let address = field.GetValue(clrObject.Address, clrObject.Type.IsValueClass) :?> uint64
            if (address = 0UL) then 
                null
            else 
                ClrObject(address, field.Type) :> obj
            

        //if (TypeHelper.isPrimitive t) then
        // if (clrObject.Type.IsValueClass) then
        //     field.GetValue(clrObject.Address, true)
        // elif ()

        //     let resultObject = clrObject.GetObjectField(name)
        //     if resultObject.IsNull then null
        //     else resultObject :> obj

            // let v = field.GetValue(clrObject.Address, false)
            // match v with 
            // | :? uint64 as address when address = 0UL -> null
            // | _ -> v

        // elif (t.IsObjectReference) then
        //     let resultObject = clrObject.GetObjectField(name)
        //     if resultObject.IsNull then null
        //     else resultObject :> obj
        // elif (t.IsValueClass) then
        //     let address = field.GetAddress(clrObject.Address);
        //     ClrObject(address, field.Type) :> obj
        // else
        //     failwith (sprintf "Cannot get value. Object: %O, field: %O" clrObject field)

    let getStaticValue (clrType:ClrType) (fieldName:string) =
        clrType.GetStaticFieldByName(fieldName).GetValue(clrType.Heap.Runtime.AppDomains |> Seq.head)

    let getArrayElementAt (clrObject:ClrObject) i =
        let t = clrObject.Type

        // if (TypeHelper.isPrimitive t.ComponentType) then
        //     t.GetArrayElementValue(clrObject.Address, i)
        // else 
        //     let address = t.GetArrayElementAddress(clrObject.Address, i)
        //     ClrObject(address, t.ComponentType) :> obj


        if (TypeHelper.isPrimitive t.ComponentType) then
            t.GetArrayElementValue(clrObject.Address, i)
        elif (t.ComponentType.IsValueClass) then
            let address = t.GetArrayElementAddress(clrObject.Address, i)
            ClrObject(address, t.ComponentType) :> obj
        else
            let address = t.GetArrayElementValue(clrObject.Address, i) :?> uint64
            if (address = 0UL) then 
                null
            else 
                ClrObject(address, t.ComponentType) :> obj

    type ClrArrayEnumerator<'T>(clrObject:ClrObject) =
        let mutable _current:'T = Unchecked.defaultof<'T>
        let mutable _index = 0
        interface IEnumerator<'T> with
            member __.MoveNext() =
                if (_index < clrObject.Length) then
                    _current <- getArrayElementAt clrObject _index :?> 'T
                    _index <- _index + 1
                    true
                else false
            member __.Reset() =
                _index <- 0
            member __.Current = _current
            member __.Current = _current :> obj
            member __.Dispose() = ()

    type ClrArrayEnumerable<'T>(clrObject:ClrObject) =
        interface IEnumerable<'T> with
            member __.GetEnumerator() =
                new ClrArrayEnumerator<'T>(clrObject) :> IEnumerator<'T>
            member __.GetEnumerator() =
                new ClrArrayEnumerator<'T>(clrObject) :> System.Collections.IEnumerator

[<AutoOpen>]
module Extensions =
    
    type ClrHeap with
        member inline this.EnumerateObjectsOfType< ^T when ^T : (static member __MethodTable : uint64 ) and ^T : (static member __CreateInstance : ClrObject -> ^T ) > () =
            let mt = (^T:(static member __MethodTable : uint64) () )
            this.EnumerateObjects()
            |> Seq.filter (fun p -> p.Type.MethodTable = mt)
            |> Seq.map (fun p -> (^T:(static member __CreateInstance : ClrObject -> ^T) p ))

        member inline this.GetClrType< ^T when ^T : (static member __MethodTable : uint64 ) > () =
            let mt = (^T:(static member __MethodTable : uint64) () )
            this.GetTypeByMethodTable(mt)

    type ClrObject with 
        member inline this.ToTypedObject< ^T when ^T : (static member __CreateInstance : ClrObject -> ^T) > () =
            (^T:(static member __CreateInstance : ClrObject -> ^T) this)

    let inline cast o =
        let t = (^Tn:(member __ClrObject : ClrObject) o )        
        (^TOut:(static member __CreateInstance : ClrObject -> ^TOut) t)


// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("MiniDumpProvider.DesignTime.dll")>]
do ()
