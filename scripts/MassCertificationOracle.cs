#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=2||args.Any(Path.IsPathRooted)||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))
    throw new ArgumentException("new output name and project-relative verified native XT input required");
var output=Path.Combine(root,"temp_docs","mass-certification",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Execution evidence is immutable");
Directory.CreateDirectory(output);var input=File.ReadAllBytes(Path.Combine(root,args[1]));
File.WriteAllBytes(Path.Combine(output,"input.x_t"),input);
var reports=new JsonArray();ParasolidMassProperties.Observer=report=>reports.Add((JsonNode)report);
var mathematicalControls=new JsonArray();
ParasolidMassProperties.BeginEvidence(output);
foreach(var scale in new[]{.125,1.0,8.0})foreach(var offset in new[]{5e-8,8e-8})
{
    Span<double> a=stackalloc double[20];Span<double> b=stackalloc double[20];a.Clear();b.Clear();
    a[0]=a[2]=8*Math.Pow(scale,3);a[18]=24*scale*scale;a[8]=a[12]=a[16]=16.0/3*Math.Pow(scale,5);a.CopyTo(b);
    b[4]=b[5]=b[6]=offset*scale;
    var report=new JsonObject{["label"]=$"manufactured-cog-norm-{scale:R}-{offset:R}",["properties"]=new JsonArray(),["kind"]="mathematical manufactured control; no native API or model"};
    var result=ParasolidMassProperties.Assess(a,b,report["label"]!.GetValue<string>(),1,report);
    var expected=offset==5e-8?MassAgreement.Verified:MassAgreement.Different;
    if(result!=expected)throw new InvalidOperationException("Euclidean norm or unit scaling contract failed");
    report["expected"]=expected.ToString();report["actual"]=result.ToString();mathematicalControls.Add((JsonNode)report);
}
if(!ParasolidScriptHost.TryStartSession("physical mass certification controls",out var host,out var reason))throw new Exception(reason);
try
{
    using(host)unsafe
    {
        foreach(var mode in new[]{"identity","scale-0.125","scale-8","rigid-translated"})
        foreach(var partition in new[]{false,true})
        {
            var a=Receive(input);var b=Receive(input);var label=mode+(partition?"-partition":"-same-xt");
            try
            {
                if(partition)Partition(b);
                Transform(a,mode);Transform(b,mode);Full(a);Full(b);
                var bytes=Transmit(b);File.WriteAllBytes(Path.Combine(output,label+".x_t"),bytes);
                var fresh=Receive(bytes);
                try
                {
                    Full(fresh);MatchSupports(a,fresh);
                    var result=ParasolidMassProperties.Compare(a,fresh,label);
                    if(result==MassAgreement.Different)throw new InvalidOperationException(label+" certified a difference after a geometry-preserving native operation");
                }
                finally{Check(PK_ENTITY_delete(1,&fresh));}
            }
            finally{Check(PK_ENTITY_delete(1,&b));Check(PK_ENTITY_delete(1,&a));}
        }
        PK_BODY_t correct,wrong;Check(PK_BODY_create_solid_block(2,2,2,null,&correct));Check(PK_BODY_create_solid_block(2.1,2,2,null,&wrong));
        try
        {
            foreach(var mode in new[]{"identity","scale-0.125","scale-8"})
            {
                var a=Receive(Transmit(correct));var b=Receive(Transmit(wrong));
                try
                {
                    Transform(a,mode);Transform(b,mode);
                    if(ParasolidMassProperties.Compare(a,b,"wrong-size-"+mode)!=MassAgreement.Different)
                        throw new InvalidOperationException("Significant wrong size must be certified Different in every unit scale");
                }
                finally{Check(PK_ENTITY_delete(1,&b));Check(PK_ENTITY_delete(1,&a));}
            }
        }
        finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    }
    Console.WriteLine($"CONTROLS_EXECUTED unresolved={ParasolidMassProperties.UnresolvedCount}; native geometry-preserving controls are separate from GM construction acceptance");
    if(ParasolidMassProperties.UnresolvedCount>0)Environment.ExitCode=2;
}
finally
{
    File.WriteAllText(Path.Combine(output,"results.json"),new JsonObject{["inputPath"]=args[1],["inputBytes"]=input.Length,
        ["inputSha256"]=Convert.ToHexStringLower(SHA256.HashData(input)),["scriptSha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Source()))),
        ["massHelperSha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"scripts","ParasolidMassProperties.cs")))),
        ["mathematicalControls"]=mathematicalControls,["reports"]=reports}.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
}

static string Source([CallerFilePath]string path="")=>path;
static void Check(int error)=>ParasolidScriptHost.Check(error,"mass certification control");
static unsafe PK_BODY_t Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;
        Check(PK_PART_receive_b(block,&options,&count,&parts));
        try{if(count!=1)throw new InvalidOperationException("one body required");return parts[0];}finally{Check(PK_MEMORY_free(parts));}
    }
}
static unsafe byte[] Transmit(PK_BODY_t body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block));
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return bytes.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block));}
}
static unsafe void Full(PK_BODY_t body)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults));
    try
    {
        for(var i=0;i<count;i++)Console.WriteLine($"BODY_FAULT state={faults[i].state} entities={faults[i].entity_1}/{faults[i].entity_2}");
        if(count!=0)throw new InvalidOperationException($"Full BODY_check faults={count}");
    }
    finally{if(faults!=null)Check(PK_MEMORY_free(faults));}
}
static unsafe void Partition(PK_BODY_t body)
{
    PK_BOX_t box;Check(PK_TOPOL_find_box(body,&box));
    var basis=new PK_AXIS2_sf_t(new((box.coord[0]+box.coord[3])*.5,(box.coord[1]+box.coord[4])*.5,(box.coord[2]+box.coord[5])*.5),new(1,0,0),new(0,1,0));
    var definition=new PK_PLANE_sf_t(basis);PK_PLANE_t plane;Check(PK_PLANE_create(&definition,&plane));
    try
    {
        int count;PK_EDGE_t* edges;Check(PK_BODY_imprint_plane(body,plane,1e-8,&count,&edges));
        if(edges!=null)Check(PK_MEMORY_free(edges));if(count==0)throw new InvalidOperationException("partition must create a finite division");
    }
    finally{Check(PK_ENTITY_delete(1,&plane));}
}
static unsafe void Transform(PK_BODY_t body,string mode)
{
    if(mode=="identity")return;
    var scale=mode=="scale-0.125"?.125:mode=="scale-8"?8:1;
    var matrix=new PK_TRANSF_sf_t{matrixI0J0=scale,matrixI1J1=scale,matrixI2J2=scale,matrixI3J3=1};
    if(mode=="rigid-translated")matrix=new(){matrixI0J2=1,matrixI1J0=1,matrixI2J1=1,matrixI0J3=3,matrixI1J3=5,matrixI2J3=7,matrixI3J3=1};
    PK_TRANSF_t transform;Check(PK_TRANSF_create(&matrix,&transform));
    try{int count;PK_TOPOL_t* replaced;PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&count,&replaced,&exact));if(replaced!=null)Check(PK_MEMORY_free(replaced));if(exact!=null)Check(PK_MEMORY_free(exact));}
    finally{Check(PK_ENTITY_delete(1,&transform));}
}
static unsafe void MatchSupports(PK_BODY_t a,PK_BODY_t b)
{
    int na,nb;PK_FACE_t* fa;PK_FACE_t* fb;Check(PK_BODY_ask_faces(a,&na,&fa));Check(PK_BODY_ask_faces(b,&nb,&fb));
    try
    {
        for(var i=0;i<nb;i++)
        {
            PK_SURF_t sb;PK_LOGICAL_t senseB;Check(PK_FACE_ask_oriented_surf(fb[i],&sb,&senseB));var matched=false;
            for(var j=0;j<na;j++){PK_SURF_t sa;PK_LOGICAL_t senseA,coincident;Check(PK_FACE_ask_oriented_surf(fa[j],&sa,&senseA));Check(PK_GEOM_is_coincident(sa,sb,&coincident));matched|=coincident==PK_LOGICAL_true&&senseA==senseB;}
            if(!matched)throw new InvalidOperationException("native control changed an oriented support");
        }
    }
    finally{Check(PK_MEMORY_free(fb));Check(PK_MEMORY_free(fa));}
}
