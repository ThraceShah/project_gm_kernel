#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=3||args.Any(Path.IsPathRooted)||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))
    throw new ArgumentException("new output name, project-relative fixture directory and original material log required");
var output=Path.Combine(root,"temp_docs","native-query-replay",args[0]);if(Directory.Exists(output))throw new InvalidOperationException("Evidence is immutable");
Directory.CreateDirectory(output);var sourceLog=File.ReadAllBytes(Path.Combine(root,args[2]));File.WriteAllBytes(Path.Combine(output,"original-material.log"),sourceLog);
var rows=new JsonArray();var sources=new JsonArray();var failures=0;
var pattern=new Regex(@"^MANAGED_MATERIAL_QUERY (?<label>\S+) .*point=\((?<x>[^,]+),(?<y>[^,]+),(?<z>[^)]+)\) GM_error=(?<gm>\d+) GM_class=(?<gc>\d+) PK_error=(?<pk>\d+) PK_class=(?<pc>\d+)",RegexOptions.Multiline|RegexOptions.CultureInvariant);
var probes=pattern.Matches(System.Text.Encoding.UTF8.GetString(sourceLog));if(probes.Count==0)throw new InvalidOperationException("No original probe coordinates found");
if(!ParasolidScriptHost.TryStartSession("same-byte native query replay",out var host,out var reason))throw new Exception(reason);
try
{
    using(host)unsafe
    {
        var options=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&options));
        try
        {
            foreach(var group in probes.Cast<Match>().GroupBy(x=>x.Groups["label"].Value))
            {
                var gm=Load(group.Key+"-managed.x_t");var pk=Load(group.Key+"-reference.x_t");
                var own=Receive(true,gm);var reference=Receive(false,pk);var roundtrip=Receive(false,Transmit(false,reference));var fresh=Receive(false,Transmit(true,own));
                try
                {
                    Full(reference);Full(roundtrip);Full(fresh);ParasolidBodyCompare.AssertEquivalent(reference,fresh,group.Key+" fixed original products");
                    foreach(var item in group)
                    {
                        double Number(string name)=>double.Parse(item.Groups[name].Value,System.Globalization.CultureInfo.InvariantCulture);
                        var p=new PK_VECTOR_t(Number("x"),Number("y"),Number("z"));int* values=stackalloc int[4];int* errors=stackalloc int[4];PK_TOPOL_t* topols=stackalloc PK_TOPOL_t[4];
                        errors[0]=PK_BODY_contains_vector(reference,p,values,topols);errors[1]=PK_BODY_contains_vector(roundtrip,p,values+1,topols+1);
                        errors[2]=KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&p,values+2,topols+2);errors[3]=PK_BODY_contains_vector(fresh,p,values+3,topols+3);
                        var equal=true;for(var i=0;i<4;i++)equal&=errors[i]==0&&values[i]==values[0];if(!equal)failures++;
                        var codes=new JsonArray();var returnCodes=new JsonArray();var topology=new JsonArray();var classes=new JsonArray();
                        for(var i=0;i<4;i++)
                        {
                            codes.Add(JsonValue.Create(values[i]));returnCodes.Add(JsonValue.Create(errors[i]));topology.Add(JsonValue.Create(topols[i]));int kind=0;
                            var error=topols[i]==0?-1:i==2?KernelRuntime.EntityAskClass(topols[i],&kind):PK_ENTITY_ask_class(topols[i],&kind);
                            classes.Add(error==0?JsonValue.Create(kind):null);
                        }
                        rows.Add((JsonNode)new JsonObject{["label"]=group.Key,["x"]=p.coord[0],["y"]=p.coord[1],["z"]=p.coord[2],
                            ["originalGmError"]=Number("gm"),["originalGmEnclosure"]=Number("gc"),["originalPkError"]=Number("pk"),["originalPkEnclosure"]=Number("pc"),
                            ["order"]="PK_reference, PK_reference_roundtrip, GM_fixed_input_reload, PK_GM_fresh",
                            ["enclosures"]=codes,["errors"]=returnCodes,["topology"]=topology,["topologyClasses"]=classes,["agreement"]=equal?"Verified":"Different"});
                    }
                    File.WriteAllBytes(Path.Combine(output,group.Key+"-gm-query-output.x_t"),Transmit(true,own));
                }
                finally{Check(KernelRuntime.EntityDelete(1,&own));Check(PK_ENTITY_delete(1,&fresh));Check(PK_ENTITY_delete(1,&roundtrip));Check(PK_ENTITY_delete(1,&reference));}
            }
        }
        finally{Check(KernelRuntime.SessionStop());}
    }
    Console.WriteLine($"FIXED_QUERY_REPLAY total={rows.Count} different={failures}");if(failures!=0)Environment.ExitCode=1;
}
finally
{
    var assembly=typeof(KernelRuntime).Assembly;var dll=File.ReadAllBytes(assembly.Location);var pdb=Path.ChangeExtension(assembly.Location,".pdb");
    using var process=System.Diagnostics.Process.GetCurrentProcess();var libraries=new JsonArray();
    foreach(var module in process.Modules.Cast<System.Diagnostics.ProcessModule>().Where(x=>x.ModuleName.Contains("pskernel",StringComparison.Ordinal)).DistinctBy(x=>x.FileName))
    {
        var bytes=File.ReadAllBytes(module.FileName);libraries.Add((JsonNode)new JsonObject{["module"]=module.ModuleName,["path"]=Path.GetRelativePath(root,module.FileName),["bytes"]=bytes.Length,["sha256"]=Hash(bytes)});
    }
    if(libraries.Count==0)throw new InvalidOperationException("Actual mapped Parasolid library must be recorded");
    File.WriteAllBytes(Path.Combine(output,"ProjectGmKernel.Native.dll"),dll);if(File.Exists(pdb))File.Copy(pdb,Path.Combine(output,"ProjectGmKernel.Native.pdb"));
    File.WriteAllText(Path.Combine(output,"results.json"),new JsonObject{["originalLogPath"]=args[2],["originalLogBytes"]=sourceLog.Length,["originalLogSha256"]=Hash(sourceLog),
        ["scriptSha256"]=Hash(File.ReadAllBytes(Source())),["nativeDllSha256"]=Hash(dll),["nativeMvid"]=assembly.ManifestModule.ModuleVersionId.ToString(),
        ["nativePdbSha256"]=File.Exists(pdb)?Hash(File.ReadAllBytes(pdb)):null,["actualMappedParasolidLibraries"]=libraries,["sources"]=sources,["points"]=rows}.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
}
byte[] Load(string name)
{
    var path=Path.Combine(args[1],name);var bytes=File.ReadAllBytes(Path.Combine(root,path));File.WriteAllBytes(Path.Combine(output,name),bytes);
    sources.Add((JsonNode)new JsonObject{["path"]=path,["bytes"]=bytes.Length,["sha256"]=Hash(bytes)});return bytes;
}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Check(int error)=>ParasolidScriptHost.Check(error,"fixed query replay");
static unsafe PK_BODY_t Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&count,&parts):PK_PART_receive_b(block,&options,&count,&parts));
        try{if(count!=1)throw new InvalidOperationException("one fixed product required");return parts[0];}finally{Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts));}
    }
}
static unsafe byte[] Transmit(bool own,PK_BODY_t body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block));
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return bytes.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block));}
}
static unsafe void Full(PK_BODY_t body)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults));
    try{if(count!=0)throw new InvalidOperationException($"Full BODY_check faults={count}");}finally{if(faults!=null)Check(PK_MEMORY_free(faults));}
}
