namespace FSharp.Data.MiniDumpProvider

open ProviderImplementation.ProvidedTypes
open FSharp.Core.CompilerServices
open System
open System.Reflection
open System.Diagnostics
open TypeGeneration
open FSharp.Data.MiniDumpProvider
open System.Collections.Generic


[<TypeProvider>]
type public MiniDumpProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("MiniDumpProvider.DesignTime", "MiniDumpProvider.Runtime")], addDefaultProbingLocation=true)

    let ns = "FSharp.Data.MiniDumpProvider"
    let asm = Assembly.GetExecutingAssembly()

    let createType typeName (path:string) attachDebugger =
        if attachDebugger then Debugger.Launch() |> ignore

        let cache = Dictionary()
        let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, hideObjectMethods = true)
        rootType.AddMembersDelayed(fun () -> 
                       let runtime = DumpFile.initRuntime path None
                       let typeTree = TypeTree.createTypeTree runtime.Heap
                       let ctx = {Assembly = asm; Namespace = ns; Runtime = runtime}
                       typeTree.Children 
                       |> Seq.toList
                       |> List.map (fun p -> getOrCreateType typeTree cache ctx p.Name))
        
        ProvidedMethod(
            "CreateRuntime", 
            [], 
            typeof<Microsoft.Diagnostics.Runtime.ClrRuntime>, 
            invokeCode = (fun _ -> let capturedPath = path
                                   <@@ DumpFile.initRuntime capturedPath None @@>), 
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
