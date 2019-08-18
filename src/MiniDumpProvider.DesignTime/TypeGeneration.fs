namespace FSharp.Data.MiniDumpProvider

module internal TypeGeneration =

    open System
    open Microsoft.Diagnostics.Runtime
    open ProviderImplementation.ProvidedTypes
    open FSharp.Quotations
    open System.Reflection
    open System.Collections.Generic

    type GenerationContext = {Assembly:Assembly; Namespace:string; Runtime: ClrRuntime}

    let splitName (s:string) = 
        s.Split([|'.'; '+'|])

    let parseName (n:string) =
        let arr = n |> splitName
        match arr with 
        | [|name|] -> (None, name)
        | [|ns; name|] -> (Some ns, name)
        | _ -> let name = arr |> Array.last in (Some (n.Substring(0, n.Length - 1 - name.Length)), name)

    let populateType (ptd:ProvidedTypeDefinition) (getOrCreateType:ClrType->ProvidedTypeDefinition) (t:ClrType) : unit =
        
        let mt = t.MethodTable
        
        fun () -> 
            ProvidedConstructor(
                parameters = [ ProvidedParameter("clrObject",typeof<ClrObject>) ],
                invokeCode = 
                    (fun args -> <@@ (%%(args.[0]) : ClrObject) :> obj @@>))
        |> ptd.AddMemberDelayed

        let getMembetType (t:ClrType) =
            if TypeHelper.isPrimitive t 
            then Type.GetType(t.Name) 
            else getOrCreateType t :> Type
        
        fun () -> 
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
                                <@@ let clrObject = ((%%(args.[0]) : obj) :?> ClrObject) in ValueProvider.getValue clrObject name @@> )))
        |> ptd.AddMembersDelayed

        fun () -> ProvidedProperty("__ClrObject", typeof<ClrObject>, getterCode = fun args -> <@@ ((%%(args.[0]) : obj) :?> ClrObject) @@>)
        |> ptd.AddMemberDelayed

        fun () -> ProvidedProperty("__MethodTable", typeof<uint64>, getterCode = (fun _ -> <@@ mt @@>), isStatic = true)
        |> ptd.AddMemberDelayed

        fun () -> ProvidedMethod("__CreateInstance", [ProvidedParameter("clrObject",typeof<ClrObject>)], ptd, 
                        invokeCode = (fun args -> let clrObject = args.[0]
                                                  clrObject),
                        isStatic = true)
        |> ptd.AddMemberDelayed

        fun () -> ProvidedMethod("__GetClrType", [ProvidedParameter("clrHeap",typeof<ClrHeap>)], typeof<ClrType>, 
                        invokeCode = (fun args -> <@@ (%%(args.[0]) : ClrHeap).GetTypeByMethodTable(mt) @@>),
                        isStatic = true)
        |> ptd.AddMemberDelayed

        if t.IsArray then
            fun () -> ProvidedProperty("__Length", typeof<int>, getterCode = (fun args -> <@@ ((%%(args.[0]) : obj) :?> ClrObject).Length @@>))
            |> ptd.AddMemberDelayed

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
            fun () -> ProvidedMethod("__EnumerateItems", [],  returnType, 
                        invokeCode = (fun args -> let enumerable = Expr.NewObject(enumeratorConstructorInfo, [Expr.Coerce(args.[0], typeof<ClrObject>)])
                                                  Expr.Coerce(enumerable, returnType)))
            |> ptd.AddMemberDelayed
        ()


    let rec getOrCreateNamespace (cache:IDictionary<string,ProvidedTypeDefinition>) context (fullName:string) : ProvidedTypeDefinition = 
        match fullName with 
        | null 
        | "" -> 
            getOrCreateNamespace cache context ""
        | _ -> 
            match (cache.TryGetValue fullName) with
            | (true, nsd) -> nsd
            | _ -> 
                let (nso, name) = parseName fullName
                let nsType = ProvidedTypeDefinition(context.Assembly, context.Namespace, name, Some typeof<obj>, hideObjectMethods = true)
                cache.Add (fullName, nsType)
                
                if (nso |> Option.isSome) then  
                    let parentNamespaceType = getOrCreateNamespace cache context nso.Value
                    parentNamespaceType.AddMember nsType
                nsType

    let rec getOrCreateType (cache:IDictionary<string,ProvidedTypeDefinition>) context (t:ClrType) : ProvidedTypeDefinition =
        match (cache.TryGetValue t.Name) with
        | (true, d) -> d
        | _ ->
            let (nso, name) = parseName t.Name        
            let parentTypeOption = Some typeof<obj>
            match (cache.TryGetValue t.Name) with
            | (true, d) -> d
            | _ ->
                let ptd = ProvidedTypeDefinition(context.Assembly, context.Namespace, name, parentTypeOption, hideObjectMethods = true)
                cache.Add(t.Name, ptd)

                populateType ptd (getOrCreateType cache context) t

                if (nso |> Option.isSome) then
                    let nsType = getOrCreateNamespace cache context nso.Value
                    nsType.AddMember ptd

                ptd