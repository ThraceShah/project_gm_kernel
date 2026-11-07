#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("short-edge mass integration diagnostics",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var prefix=args.FirstOrDefault(x=>!x.StartsWith("--",StringComparison.Ordinal))??"a1-r0.5-t1E-07-reverseFalse-default";
    if(args.Contains("--receive"))
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start));
        try
        {
            var bytes=File.ReadAllBytes(ProductPath(prefix+"-self.x_t"));
            fixed(byte* p=bytes)
            {
                var memory=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int count;int* parts;
                var error=KernelRuntime.PartReceiveB(memory,&options,&count,&parts);Console.WriteLine($"managed self-product receive={error} parts={count}");Check(error);
                try{for(var i=0;i<count;i++)Check(KernelRuntime.EntityDelete(1,parts+i));}finally{Check(KernelRuntime.MemoryFree(parts));}
            }
        }
        finally{Check(KernelRuntime.SessionStop());}
        return;
    }
    var reference=Read(prefix+"-native.x_t");var actual=Read(prefix+"-self.x_t");
    try
    {
        if(args.Contains("--definitions")){Definitions(reference,"reference");Definitions(actual,"actual");}
        var scaleOption=args.FirstOrDefault(x=>x.StartsWith("--scale-power=",StringComparison.Ordinal));var scale=scaleOption==null?1:Math.ScaleB(1,int.Parse(scaleOption[14..],System.Globalization.CultureInfo.InvariantCulture));
        if(scale!=1){ScaleBody(reference,scale);ScaleBody(actual,scale);}
        if(args.Contains("--tight-precision"))Check(PK_SESSION_set_precision(1e-12));
        var options=new PK_TOPOL_eval_mass_props_o_t{bound=PK_mass_bound_modulus_c};double* a=stackalloc double[20];double* b=stackalloc double[20];
        var accuracy=args.Contains("--full-accuracy")?1:.99999999999;
        if(args.Contains("--faces")){FaceMass(reference,accuracy,options,a);FaceMass(actual,accuracy,options,b);}
        else
        {
            Check(PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+2,a+4,a+8,a+18));
            Check(PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+2,b+4,b+8,b+18));
        }
        if(scale!=1){UnscaleMass(a,scale);UnscaleMass(b,scale);}
        var maximum=0.0;
        for(var property=0;property<15;property++)
        {
            var value=property==0?0:property==1?2:property<5?property+2:property<14?property+3:18;
            var bound=property==0?1:property==1?3:property<5?7:property<14?17:19;
            var residual=Math.Abs(a[value]-b[value])/(1+Math.Abs(a[value]));maximum=Math.Max(maximum,residual);
            Console.WriteLine($"property={property} reference={a[value]:R} actual={b[value]:R} error_moduli={a[bound]:R}/{b[bound]:R} normalized={residual:R} intervals_overlap={Math.Abs(a[value]-b[value])<=a[bound]+b[bound]}");
        }
        Console.WriteLine($"DIAGNOSTIC ONLY: point residual={maximum:R}; unchanged acceptance threshold=1e-7");
        if(maximum>=1e-7)Environment.ExitCode=1;
    }
    finally{Check(PK_ENTITY_delete(1,&reference));Check(PK_ENTITY_delete(1,&actual));}
}
static unsafe int Read(string name)
{
    var bytes=File.ReadAllBytes(ProductPath(name));
    fixed(byte* p=bytes)
    {
        var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(memory,&options,&n,&parts));try{if(n!=1)throw new Exception("one diagnostic body required");return parts[0];}finally{Check(PK_MEMORY_free(parts));}
    }
}
static unsafe void FaceMass(int body,double accuracy,PK_TOPOL_eval_mass_props_o_t options,double* output)
{
    int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces));options.single=PK_LOGICAL_true;
    try{Check(PK_TOPOL_eval_mass_props(count,faces,accuracy,&options,output,output+2,output+4,output+8,output+18));}
    finally{Check(PK_MEMORY_free(faces));}
}
static unsafe void ScaleBody(int body,double scale)
{
    var matrix=new PK_TRANSF_sf_t{matrixI0J0=scale,matrixI1J1=scale,matrixI2J2=scale,matrixI3J3=1};int transform;
    Check(PK_TRANSF_create(&matrix,&transform));
    try
    {int n;int* replaced;PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&n,&replaced,&exact));if(replaced!=null)Check(PK_MEMORY_free(replaced));if(exact!=null)Check(PK_MEMORY_free(exact));}
    finally{Check(PK_ENTITY_delete(1,&transform));}
}
static unsafe void UnscaleMass(double* values,double scale)
{
    for(var i=0;i<4;i++)values[i]/=scale*scale*scale;
    for(var i=4;i<8;i++)values[i]/=scale;
    for(var i=8;i<18;i++)values[i]/=scale*scale*scale*scale*scale;
    values[18]/=scale*scale;values[19]/=scale*scale;
}
static unsafe void Definitions(int body,string label)
{
    int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces));
    try{for(var i=0;i<count;i++)
    {
        int surface,type;Check(PK_FACE_ask_surf(faces[i],&surface));Check(PK_ENTITY_ask_class(surface,&type));if(type!=PK_CLASS_blendsf)continue;
        PK_BLENDSF_sf_t definition;Check(PK_BLENDSF_ask(surface,&definition));PK_ELLIPSE_sf_t ellipse;Check(PK_ELLIPSE_ask(definition.spine,&ellipse));
        var box=new PK_UVBOX_t();var error=PK_FACE_find_uvbox(faces[i],&box);
        Console.WriteLine($"DEFINITION {label} face={faces[i]} blend={surface} supports={definition.geom_1}/{definition.geom_2} radii={definition.radii[0]:R}/{definition.radii[1]:R} spine={definition.spine} extent=[{definition.spine_extent.value[0]:R},{definition.spine_extent.value[1]:R}] ellipse={ellipse.R1:R}/{ellipse.R2:R} uvbox_error={error} uvbox=[{box.param[0]:R},{box.param[1]:R},{box.param[2]:R},{box.param[3]:R}]");
        var point=new PK_VECTOR_t();
        for(var end=0;end<2;end++){Check(PK_CURVE_eval(definition.spine,definition.spine_extent.value[end],0,&point));Console.WriteLine($"SPINE END {label} end={end} point=({point.coord[0]:R},{point.coord[1]:R},{point.coord[2]:R})");}
    }}finally{Check(PK_MEMORY_free(faces));}
}
static void Check(int error)=>ParasolidScriptHost.Check(error,"mass diagnostic");
static string ProductPath(string name)=>Path.Combine(Path.GetDirectoryName(Source())!,"..","temp_docs","short-edge-blend-oracle",name);
static string Source([CallerFilePath]string path="")=>path;
