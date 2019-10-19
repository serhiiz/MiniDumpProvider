namespace FSharp.Data.MiniDumpProvider

module TypeTree = 

    open Microsoft.Diagnostics.Runtime
    open System.Collections.Generic

    type Mutable<'a>() =
        member val Value:Option<'a> = None with get, set
       
    type Node = {Name: string; FullName: string; Children: IList<Node>; Type:Mutable<ClrType>}

    let splitName (s:string) = 
        match s with 
        | "" -> [||]
        | _ ->
            let index = s.IndexOf('<');
            let (toSplit,generic) =
                if (index < 0) then (s,"")
                else (s.Substring(0, index), s.Substring(index))
            
            let split = toSplit.Split([|'.'; '+'|])
            split.[split.Length - 1] <- split.[split.Length - 1] + generic
            split
        
    let rec getOrCreateNode ns parts =
        match parts with 
        | [] -> ns
        | head::tail -> 
            match ns.Children |> Seq.tryFind (fun n -> n.Name = head) with 
            | Some nextNs -> getOrCreateNode nextNs tail
            | None -> 
                let fullName = 
                    match (ns.Type.Value, ns.FullName) with 
                    | Some _, _ -> ns.FullName + "+" + head
                    | _, "" -> head
                    | _ -> ns.FullName + "." + head
                let nextns = {Name = head; FullName = fullName; Children = List(); Type = Mutable() }
                ns.Children.Add nextns
                getOrCreateNode nextns tail

    let rec getNode ns parts =
        match parts with 
        | [] -> ns
        | head::tail -> 
            let child = ns.Children |> Seq.find (fun n -> n.Name = head) 
            getNode child tail
            
    let addType rootNs (t:ClrType) =
        let parts = t.Name |> splitName |> Array.toList
        let ns = getOrCreateNode rootNs parts
        ns.Type.Value <- Some t
        rootNs

    let createTypeTree (heap:ClrHeap) =
        heap.EnumerateTypes()
        |> Seq.fold addType {Name=""; FullName = ""; Children = List(); Type = Mutable()}