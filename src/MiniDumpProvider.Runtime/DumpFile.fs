namespace FSharp.Data.MiniDumpProvider

module DumpFile =

    open System
    open Microsoft.Diagnostics.Runtime
    open System.IO

    let getDacLocation (dataTarget:DataTarget) hintPath = 
        let version = dataTarget.ClrVersions.[0];
        match hintPath with
        | Some f when (f |> isNull |> not) && File.Exists f -> f
        | Some d when (d |> isNull |> not) && Directory.Exists d -> Path.Combine(d, version.DacInfo.FileName)
        | _ -> dataTarget.SymbolLocator.FindBinary(version.DacInfo)

    let createRuntime dumpFileName dacPath = 
        let dataTarget = DataTarget.LoadCrashDump(dumpFileName);

        let ternary test a b = 
            if test then a else b

        let isTarget64Bit = dataTarget.PointerSize = 8u
        if Environment.Is64BitProcess <> isTarget64Bit then
            failwith (sprintf "Architecture mismatch: Process is %s but target is %s" (ternary Environment.Is64BitProcess "64 bit" "32 bit") (ternary isTarget64Bit "64 bit" "32 bit"))

        let version = dataTarget.ClrVersions.[0];

        let dac = getDacLocation dataTarget dacPath

        if (dac |> isNull) || (File.Exists(dac) |> not) then
            failwith (sprintf "Could not find the specified dac at '%s'" dac)

        version.CreateRuntime(dac);

    let initRuntime path dac = 
        let rt = createRuntime path dac
        rt.Heap.CacheHeap(Threading.CancellationToken.None)
        rt