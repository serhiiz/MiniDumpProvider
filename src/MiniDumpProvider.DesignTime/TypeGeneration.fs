﻿namespace FSharp.Data.MiniDumpProvider

module internal TypeGeneration =

    open System
    open Microsoft.Diagnostics.Runtime
    open ProviderImplementation.ProvidedTypes
    open FSharp.Quotations
    open System.Reflection
    open System.Collections.Generic
    open TypeTree

    type GenerationContext = {Assembly:Assembly; Namespace:string; Runtime: ClrRuntime}

    let parseName (n:string) =
        let arr = n |> splitName
        match arr with 
        | [||] -> (None, "")
        | [|name|] -> (None, name)
        | [|ns; name|] -> (Some ns, name)
        | _ -> let name = arr |> Array.last in (Some (n.Substring(0, n.Length - 1 - name.Length)), name)

    let getTypeMembers (ptd:ProvidedTypeDefinition) (getOrCreateType:string->Type) (t:ClrType) : MemberInfo seq =
        seq {
            let mt = t.MethodTable
        
            yield ProvidedConstructor(
                    parameters = [ ProvidedParameter("clrObject",typeof<ClrObject>) ],
                    invokeCode = 
                        (fun args -> <@@ (%%(args.[0]) : ClrObject) :> obj @@>))

            let getMembetType (t:ClrType) =
                if TypeHelper.isPrimitive t then 
                    Type.GetType(t.Name) 
                elif t.Name = "System.__Canon[]" then
                    getOrCreateType "System.Object[]"
                else getOrCreateType t.Name
        
            yield!
                t.Fields
                |> Seq.toList
                |> List.groupBy (fun f -> f.Name) // E.g. System.Reflection.Emit.LocalBuilder has duplicated field names
                |> List.collect (fun (_,vs) -> match vs with 
                                               | [single] -> [(single.Name, single)]
                                               | _ -> vs |> List.map (fun f -> (sprintf "%s_at_%s" f.Name (f.Offset.ToString("X")), f)))
                |> List.map 
                    (fun (n, f) -> 
                        ProvidedProperty(n, getMembetType f.Type, 
                            getterCode= (
                                fun args -> 
                                    let name = f.Name
                                    <@@ let clrObject = ((%%(args.[0]) : obj) :?> ClrObject) in ValueProvider.getValue clrObject name @@> )) :> MemberInfo)

            yield ProvidedProperty("__ClrObject", typeof<ClrObject>, getterCode = fun args -> <@@ ((%%(args.[0]) : obj) :?> ClrObject) @@>)

            yield ProvidedProperty("__MethodTable", typeof<uint64>, getterCode = (fun _ -> <@@ mt @@>), isStatic = true)

            yield ProvidedMethod("__CreateInstance", [ProvidedParameter("clrObject",typeof<ClrObject>)], ptd, 
                            invokeCode = (fun args -> let clrObject = args.[0]
                                                      clrObject),
                            isStatic = true)

            yield ProvidedMethod("__GetClrType", [ProvidedParameter("clrHeap",typeof<ClrHeap>)], typeof<ClrType>, 
                            invokeCode = (fun args -> <@@ (%%(args.[0]) : ClrHeap).GetTypeByMethodTable(mt) @@>),
                            isStatic = true)

            if t.IsArray then
                yield ProvidedProperty("__Length", typeof<int>, getterCode = (fun args -> <@@ ((%%(args.[0]) : obj) :?> ClrObject).Length @@>))

                let arrayElementType = 
                    if t.ComponentType.ElementType = ClrElementType.Unknown // Defect in clrmd https://github.com/microsoft/clrmd/issues/115
                    then typeof<ClrObject>
                    else getMembetType t.ComponentType
            
                let tn = typedefof<ValueProvider.ClrArrayEnumerable<_>>
                let gtn = tn.MakeGenericType([|arrayElementType|])
                let enumeratorConstructorInfo =
                    match arrayElementType with 
                    | :? ProvidedTypeDefinition -> System.Reflection.Emit.TypeBuilder.GetConstructor(gtn, tn.GetConstructor([|typeof<ClrObject>|]))
                    | _ -> gtn.GetConstructor([|typeof<ClrObject>|])
            

                let returnType = typedefof<IEnumerable<_>>.MakeGenericType([|arrayElementType|])
                yield ProvidedMethod("__EnumerateItems", [],  returnType, 
                            invokeCode = (fun args -> let enumerable = Expr.NewObject(enumeratorConstructorInfo, [Expr.Coerce(args.[0], typeof<ClrObject>)])
                                                      Expr.Coerce(enumerable, returnType)))
            }


    let rec getEvaluatedType cache fullName =

        let findEvaluatedType fullName cache =
            let rec findEvaluatedTypeCore fullName missingParts (cache:IDictionary<string,ProvidedTypeDefinition>) =
                match (cache.TryGetValue fullName) with
                | (true, d) -> d, missingParts
                | _ ->
                    let (nso, name) = parseName fullName
                    findEvaluatedTypeCore (nso |> Option.defaultValue "") (name :: missingParts) cache

            findEvaluatedTypeCore fullName [] cache

        let rec evalTypes (rootType:Type) (names:string list) =
            match names with 
            | [] -> rootType
            | head :: tail ->
                let newlyEvaluatedType = rootType.GetNestedType(head.Replace('.', '_'))
                evalTypes newlyEvaluatedType tail
                
        findEvaluatedType fullName cache ||> evalTypes
        

    let rec getOrCreateType (typeHierarchy:Node) (cache:IDictionary<string,ProvidedTypeDefinition>) context fullName : ProvidedTypeDefinition =
        match (cache.TryGetValue fullName) with
        | (true, d) -> d
        | _ ->
            let (nso, name) = parseName fullName 
            
            let parentTypeOption = Some typeof<obj>
            let ptd = ProvidedTypeDefinition(context.Assembly, nso |> Option.defaultValue null, name.Replace('.', '_'), parentTypeOption, hideObjectMethods = true)
            cache.Add(fullName, ptd)

            let parts = fullName |> splitName |> Array.toList
            let node = getNode typeHierarchy parts 

            ptd.AddMembersDelayed(fun () -> 
                node.Children 
                |> Seq.toList
                |> List.map (fun p -> getOrCreateType typeHierarchy cache context p.FullName))

            if (node.Type.Value.IsSome) then
                ptd.AddMembersDelayed(fun () -> getTypeMembers ptd (getEvaluatedType cache) node.Type.Value.Value |> Seq.toList)

            ptd