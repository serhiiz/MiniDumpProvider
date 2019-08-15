namespace FSharp.Data.MiniDumpProvider

open ProviderImplementation.ProvidedTypes
open FSharp.Core.CompilerServices
open System
open System.Reflection
open System.Diagnostics
open TypeGeneration
open DumpFile
open FSharp.Data.MiniDumpProvider

open System.Collections.Generic


[<TypeProvider>]
type public MiniDumpProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("MiniDumpProvider.DesignTime", "MiniDumpProvider.Runtime")], addDefaultProbingLocation=true)

    let ns = "FSharp.Data.MiniDumpProvider"
    let asm = Assembly.GetExecutingAssembly()

    let checkRootType (t:ProvidedTypeDefinition) =
        t.FullName.Replace("Generated.", String.Empty)
        |> splitName |> (fun s -> s.Length <= 2)

    let createType typeName (path:string) attachDebugger =

        if attachDebugger then Debugger.Launch() |> ignore

        let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, hideObjectMethods = true)
        let runtime = initRuntime path None
        let ctx = {Assembly = asm; Namespace = "Generated"; Runtime = runtime}

        runtime.Heap.EnumerateTypes()
            |> Seq.filter (TypeHelper.isPrimitive >> not)
            |> Seq.fold (fun s t -> getOrCreateType s ctx t |> ignore; s) (Dictionary())
            |> Seq.map (fun kvp -> kvp.Value)
            |> Seq.filter checkRootType
            |> Seq.iter rootType.AddMember

        ProvidedMethod(
            "CreateRuntime", 
            [], 
            typeof<Microsoft.Diagnostics.Runtime.ClrRuntime>, 
            invokeCode = (fun _ -> let capturedPath = path
                                   <@@ initRuntime capturedPath None @@>), 
            isStatic = true)
        |> rootType.AddMember

        rootType

    let provider = ProvidedTypeDefinition(asm, ns, "MiniDump", Some typeof<obj>, hideObjectMethods = true)

    do provider.DefineStaticParameters(
        [ProvidedStaticParameter("DumpFile", typeof<string>); ProvidedStaticParameter("AttachDebugger", typeof<bool>, false)], 
        fun typeName args -> createType typeName (args.[0] :?> string) (args.[1] :?> bool)   
    )

    do this.AddNamespace(ns, [provider])

[<TypeProviderAssembly>]
do ()
