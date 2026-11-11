#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
var here=Path.GetDirectoryName(Here())!;
var evidence=args.Length>0?Path.GetFullPath(args[0],here):Path.GetFullPath("../../../../temp_docs/zero-attribution-0ba",here);
var dll=Path.Combine(evidence,"original","ProjectGmKernel.Native.dll");
using var file=File.OpenRead(dll);using var pe=new PEReader(file);var reader=pe.GetMetadataReader();
using var pdbFile=File.OpenRead(Path.ChangeExtension(dll,".pdb"));using var provider=MetadataReaderProvider.FromPortablePdbStream(pdbFile);var pdb=provider.GetMetadataReader();
Console.WriteLine("DLL sha="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dll)))+" mvid="+reader.GetGuid(reader.GetModuleDefinition().Mvid));
var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
string TypeName(EntityHandle h)=>h.Kind switch{HandleKind.TypeDefinition=>reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)h).Name),HandleKind.TypeReference=>reader.GetString(reader.GetTypeReference((TypeReferenceHandle)h).Name),HandleKind.TypeSpecification=>"TypeSpec["+Convert.ToHexString(reader.GetBlobBytes(reader.GetTypeSpecification((TypeSpecificationHandle)h).Signature))+"]",_=>h.Kind.ToString()};
string Name(EntityHandle h)=>h.Kind switch
{
    HandleKind.MethodDefinition=>TypeName(reader.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType())+"::"+reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)h).Name),
    HandleKind.MemberReference=>TypeName(reader.GetMemberReference((MemberReferenceHandle)h).Parent)+"::"+reader.GetString(reader.GetMemberReference((MemberReferenceHandle)h).Name),
    HandleKind.MethodSpecification=>Name(reader.GetMethodSpecification((MethodSpecificationHandle)h).Method),
    _=>TypeName(h)
};
foreach(var handle in reader.MethodDefinitions)
{
    var method=reader.GetMethodDefinition(handle);var name=reader.GetString(method.Name);
    if(name is not ("IntervalJacobian" or "IntervalResidual" or "CacheSplineContact" or "IntervalNormal" or "IntervalSpline" or "SplineContactMatches"))continue;
    var body=pe.GetMethodBody(method.RelativeVirtualAddress);var il=body.GetILBytes()!;var points=pdb.GetMethodDebugInformation(handle.ToDebugInformationHandle()).GetSequencePoints().Where(p=>!p.IsHidden).ToArray();
    Console.WriteLine($"METHOD {Name(handle)} InitLocals={body.LocalVariablesInitialized} ILBytes={il.Length}");
    var calls=0;var init=0;
    for(var at=0;at<il.Length;)
    {
        var start=at;ushort code=il[at++];if(code==0xfe)code=(ushort)(0xfe00|il[at++]);var op=codes[code];var size=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,at),_=>4};
        if(op.Name is "call" or "callvirt" or "initobj" or "initblk" or "cpblk" or "localloc" or "sizeof")
        {
            var target=size==4?Name(MetadataTokens.EntityHandle(BitConverter.ToInt32(il,at))):"";
            var point=points.LastOrDefault(p=>p.Offset<=start);Console.WriteLine($" IL_{start:X4} {op.Name} {target} sourceLine={point.StartLine}");if(op.Name!.StartsWith("call"))calls++;else init++;
        }
        at+=size;
    }
    Console.WriteLine($"COUNTS calls={calls} initOrBlockOps={init}");
}
foreach(var h in pdb.Documents)
{
    var doc=pdb.GetDocument(h);var path=pdb.GetString(doc.Name);var name=Path.GetFileName(path);if(name is not ("NestedBlendBounds.cs" or "NestedBlendIntervalSupport.cs"))continue;
    Console.WriteLine($"PDB_DOCUMENT {name} hash={Convert.ToHexStringLower(pdb.GetBlobBytes(doc.Hash))}");
}
static string Here([CallerFilePath]string p="")=>p;
