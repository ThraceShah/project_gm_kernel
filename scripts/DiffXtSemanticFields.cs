#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;

if(args.Length!=2)throw new ArgumentException("usage: DiffXtSemanticFields.cs ORIGINAL_XT REBUILT_XT");
var schemaDirectory=Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")??Environment.GetEnvironmentVariable("P_SCHEMA")??throw new InvalidOperationException("PARASOLID_SCHEMA_DIR or P_SCHEMA is required.");
var catalog=XtSchemaCatalog.OpenDirectory(schemaDirectory);var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;
var left=XtCodec.Read(catalog,File.ReadAllBytes(Path.GetFullPath(Path.Combine(scriptDirectory,args[0]))));var right=XtCodec.Read(catalog,File.ReadAllBytes(Path.GetFullPath(Path.Combine(scriptDirectory,args[1]))));
var leftGroups=left.Nodes.GroupBy(n=>left.Schema.GetNode(n.Type).Name).ToDictionary(g=>g.Key,g=>g.ToArray());var rightGroups=right.Nodes.GroupBy(n=>right.Schema.GetNode(n.Type).Name).ToDictionary(g=>g.Key,g=>g.ToArray());
var leftKeys=Keys(left,leftGroups);var rightKeys=Keys(right,rightGroups);
if(Environment.GetEnvironmentVariable("PGM_XT_DUMP_NODES")=="1"){Console.WriteLine("LEFT "+string.Join(' ',left.Nodes.Select(node=>$"{node.Index}:{left.Schema.GetNode(node.Type).Name}")));Console.WriteLine("RIGHT "+string.Join(' ',right.Nodes.Select(node=>$"{node.Index}:{right.Schema.GetNode(node.Type).Name}")));}
if(Environment.GetEnvironmentVariable("PGM_XT_DUMP_TYPE") is {Length:>0} dumpType&&leftGroups.TryGetValue(dumpType,out var dumpNodes))foreach(var node in dumpNodes)Dump(left,node);
foreach(var pair in leftGroups.OrderBy(static pair=>pair.Key,StringComparer.Ordinal))
{
    if(!rightGroups.TryGetValue(pair.Key,out var rightNodes)){Console.WriteLine($"{pair.Key}: missing all {pair.Value.Length} nodes");continue;}
    var count=Math.Min(pair.Value.Length,rightNodes.Length);for(var i=0;i<count;i++)Compare(pair.Key,i,pair.Value[i],rightNodes[i]);
}
foreach(var pair in rightGroups)if(!leftGroups.TryGetValue(pair.Key,out var leftNodes)||leftNodes.Length!=pair.Value.Length)Console.WriteLine($"RIGHT {pair.Key}: {pair.Value.Length} nodes vs {leftNodes?.Length??0}");
return;

void Compare(string name,int ordinal,XtNode a,XtNode b)
{
    var descriptor=left.Schema.GetNode(a.Type);var ai=0;var bi=0;
    foreach(var field in left.Schema.GetFields(descriptor))
    {
        if(!field.Transmit)continue;var ac=Count(descriptor,field,a);var bd=right.Schema.GetNode(b.Type);var rf=default(XtFieldDescriptor);foreach(var candidate in right.Schema.GetFields(bd))if(candidate.Name==field.Name&&candidate.Transmit){rf=candidate;break;}var bc=rf.Name is null?0:Count(bd,rf,b);
        for(var j=0;j<ac;j++){var av=a.Fields[ai+j];var bv=j<bc&&bi+j<b.Fields.Length?b.Fields[bi+j]:default;if(!Default(av)&&Default(bv)||DifferentValue(av,bv)||DifferentPointer(av,bv))Console.WriteLine($"{name}[{ordinal}].{field.Name}[{j}]: {DescribeWithTarget(av,leftKeys)} -> {DescribeWithTarget(bv,rightKeys)}");}
        ai+=ac;bi+=bc;
    }
}
static int Count(XtNodeDescriptor d,XtFieldDescriptor f,XtNode n)=>f.ElementCount>1?f.ElementCount:d.Variable&&f.ElementCount==1?Math.Max(0,n.VariableLength):1;
static bool Default(XtFieldValue v)=>v.Kind==XtFieldKind.Empty||v.Integer==0&&v.Real==0&&v.Pointer==0&&v.Character is '\0' or '?'&&v.Vector.X==0&&v.Vector.Y==0&&v.Vector.Z==0&&v.Fourth==0&&v.Fifth==0&&v.Sixth==0;
static bool DifferentValue(XtFieldValue a,XtFieldValue b)=>a.Kind!=XtFieldKind.Pointer&&(a.Kind!=b.Kind||a.Integer!=b.Integer||a.Real!=b.Real||a.Character!=b.Character||a.Vector.X!=b.Vector.X||a.Vector.Y!=b.Vector.Y||a.Vector.Z!=b.Vector.Z||a.Fourth!=b.Fourth||a.Fifth!=b.Fifth||a.Sixth!=b.Sixth);
bool DifferentPointer(XtFieldValue a,XtFieldValue b)=>a.Kind==XtFieldKind.Pointer&&(b.Kind!=XtFieldKind.Pointer||Key(a.Pointer,leftKeys)!=Key(b.Pointer,rightKeys));
static string Describe(XtFieldValue v)=>v.Kind switch{XtFieldKind.Pointer=>$"ptr:{v.Pointer}",XtFieldKind.Real=>$"real:{v.Real:R}",XtFieldKind.Character=>$"char:{v.Character}",XtFieldKind.Vector=>$"vec:{v.Vector.X:R},{v.Vector.Y:R},{v.Vector.Z:R}",_=>$"{v.Kind}:{v.Integer}"};
static Dictionary<int,string> Keys(XtDocument document,Dictionary<string,XtNode[]> groups){var result=new Dictionary<int,string>();foreach(var pair in groups)for(var i=0;i<pair.Value.Length;i++)result[pair.Value[i].Index]=$"{pair.Key}[{i}]";return result;}
static string Key(int pointer,Dictionary<int,string> keys)=>pointer==0?"null":keys.TryGetValue(pointer,out var key)?key:$"missing:{pointer}";
static string DescribeWithTarget(XtFieldValue value,Dictionary<int,string> keys)=>value.Kind==XtFieldKind.Pointer?$"{Describe(value)}({Key(value.Pointer,keys)})":Describe(value);
static void Dump(XtDocument document,XtNode node){var descriptor=document.Schema.GetNode(node.Type);var offset=0;foreach(var field in document.Schema.GetFields(descriptor)){if(!field.Transmit)continue;var count=Count(descriptor,field,node);Console.WriteLine($"DUMP {descriptor.Name}.{field.Name} = {string.Join(',',node.Fields.AsSpan(offset,count).ToArray().Select(Describe))}");offset+=count;}}
static string GetScriptPath([CallerFilePath]string path="")=>path;
