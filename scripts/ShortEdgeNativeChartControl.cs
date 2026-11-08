#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;

if(args.Length!=1||Path.GetFileName(args[0])!=args[0]||!args[0].EndsWith("-native.x_t",StringComparison.Ordinal))
    throw new ArgumentException("pass a native short-edge oracle artifact filename from temp_docs/short-edge-blend-oracle");
if(!ParasolidScriptHost.TryStartSession("native CHART refinement control",out var host,out var message))
    throw new InvalidOperationException(message);
using(host)unsafe
{
    var directory=Path.GetDirectoryName(Source())!;
    var schema=XtSchemaCatalog.OpenDirectory(Path.Combine(directory,"..","third_party","parasolid","schema"));
    var bytes=File.ReadAllBytes(Path.Combine(directory,"..","temp_docs","short-edge-blend-oracle",args[0]));
    foreach(var sections in new[]{4,8})
    {
        var refined=Refine(schema,bytes,sections);
        var reference=Read(bytes);var actual=0;
        try
        {
            actual=Read(refined);CheckBody(reference);CheckBody(actual);
            ParasolidBodyCompare.AssertEquivalent(reference,actual,"native CHART refinement");
            CompareRawMass(reference,actual);
        }
        finally{if(actual!=0)Check(PK_ENTITY_delete(1,&actual));Check(PK_ENTITY_delete(1,&reference));}
    }
}

static unsafe byte[] Refine(XtSchemaCatalog schema,byte[] bytes,int sections)
{
    var doc=XtCodec.Read(schema,bytes);
    var byId=doc.Nodes.ToDictionary(n=>n.Index);
    var body=Read(bytes);
    try
    {
        var node=doc.Nodes.Single(n=>doc.Schema.GetNode(n.Type).Name=="INTERSECTION"&&byId[n.Fields[9].Pointer].VariableLength>=8);
        var chart=byId[node.Fields[9].Pointer];var data=byId[node.Fields[^1].Pointer];
        var old=chart.VariableLength;
        if(chart.Fields.Length!=7+old||data.Fields.Length!=1+4*old||data.Fields[0].Integer!=4)
            throw new InvalidOperationException("control needs a native chart with two UV pairs per point");
        var ident=(int)node.Fields[0].Integer;int curve;
        Check(PK_PART_find_entity_by_ident(body,PK_CLASS_curve,ident,&curve));
        if(curve==0)throw new InvalidOperationException("native curve identifier not found");
        var surfaceA=FaceSupport(body,(int)byId[node.Fields[7].Pointer].Fields[0].Integer);
        var surfaceB=FaceSupport(body,(int)byId[node.Fields[8].Pointer].Fields[0].Integer);
        var t=new double[old];
        for(var i=0;i<old;i++)
        {
            var p=chart.Fields[7+i].Vector;double station;
            Check(PK_CURVE_parameterise_vector(curve,new(p.X,p.Y,p.Z),&station));
            if(!double.IsFinite(station)||i>0&&station<=t[i-1])throw new InvalidOperationException("control needs increasing native CHART stations");
            t[i]=station;
        }
        var newCount=sections*(old-1)+1;
        var points=new XtFieldValue[7+newCount];chart.Fields.AsSpan(0,7).CopyTo(points);points[2]=XtFieldValue.Int(newCount);
        var uv=new XtFieldValue[1+4*newCount];uv[0]=data.Fields[0];
        for(var i=0;i<old;i++)
        {
            var index=sections*i;
            points[7+index]=chart.Fields[7+i];data.Fields.AsSpan(1+4*i,4).CopyTo(uv.AsSpan(1+4*index,4));
            if(i==old-1)break;
            for(var part=1;part<sections;part++)
            {
                var station=t[i]+(t[i+1]-t[i])*(part/(double)sections);PK_VECTOR_t p;
                Check(PK_CURVE_eval(curve,station,0,&p));points[7+index+part]=XtFieldValue.Vec(p.coord[0],p.coord[1],p.coord[2]);
                PK_UV_t a,b;Check(PK_SURF_parameterise_vector(surfaceA,p,&a));Check(PK_SURF_parameterise_vector(surfaceB,p,&b));
                Put(uv,1+4*(index+part),a,surfaceA,data.Fields[1+4*i].Real);
                Put(uv,3+4*(index+part),b,surfaceB,data.Fields[3+4*i].Real);
            }
        }
        chart.VariableLength=newCount;chart.Fields=points;data.VariableLength=4*newCount;data.Fields=uv;
        Console.WriteLine($"DIAGNOSTIC ONLY native curve {ident}, CHART anchors {old}->{newCount}; input XT untouched");
        return XtCodec.Write(schema,doc);
    }
    finally{Check(PK_ENTITY_delete(1,&body));}
}

static unsafe void CheckBody(int body)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;
    Check(PK_BODY_check(body,&options,&count,&faults));
    try
    {
        for(var i=0;i<count;i++)Console.WriteLine($"BODY_check fault={faults[i].state} entities={faults[i].entity_1}/{faults[i].entity_2}");
        if(count!=0)throw new InvalidOperationException("native CHART control body failed full BODY_check");
        Console.WriteLine("native control full BODY_check: 0 faults");
    }
    finally{if(faults!=null)Check(PK_MEMORY_free(faults));}
}

static unsafe void CompareRawMass(int reference,int actual)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    ReadOnlySpan<string> names=["volume","mass","centroid.x","centroid.y","centroid.z","inertia.xx","inertia.xy","inertia.xz",
        "inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
    foreach(var accuracy in new[]{.99999999,.99999999999,1d})
    {
        Check(PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+1,a+2,a+5,a+14));
        Check(PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+1,b+2,b+5,b+14));
        var maximum=0.0;var index=0;
        for(var i=0;i<names.Length;i++)
        {
            var residual=Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i]));
            if(!double.IsFinite(residual))throw new InvalidOperationException("nonfinite native mass result");
            if(residual>maximum){index=i;maximum=residual;}
        }
        Console.WriteLine($"DIAGNOSTIC ONLY raw accuracy={accuracy:R} normalized15={maximum:R} worst={names[index]} values={a[index]:R}/{b[index]:R} original_1e-7_point_gate={(maximum<1e-7?"pass":"fail")}");
    }
}

static unsafe void Put(XtFieldValue[] uv,int at,PK_UV_t value,int surface,double preceding)
{
    int kind;Check(PK_ENTITY_ask_class(surface,&kind));var u=value.param[0];
    if(kind==PK_CLASS_cyl)u+=Math.Tau*Math.Round((preceding-u)/Math.Tau);
    uv[at]=XtFieldValue.RealValue(u);uv[at+1]=XtFieldValue.RealValue(value.param[1]);
}
static unsafe int FaceSupport(int body,int ident)
{
    int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces));
    try
    {
        for(var i=0;i<count;i++)
        {
            int surface,id;Check(PK_FACE_ask_surf(faces[i],&surface));Check(PK_ENTITY_ask_identifier(surface,&id));
            if(id==ident)return surface;
        }
    }
    finally{Check(PK_MEMORY_free(faces));}
    throw new InvalidOperationException($"native face support {ident} not found");
}
static unsafe int Read(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);
        var opts=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};
        int count;int* parts;Check(PK_PART_receive_b(memory,&opts,&count,&parts));
        try{if(count!=1)throw new InvalidOperationException("expected one native diagnostic body");return parts[0];}
        finally{Check(PK_MEMORY_free(parts));}
    }
}
static string Source([CallerFilePath]string path="")=>path;
static void Check(int error)=>ParasolidScriptHost.Check(error,"native CHART refinement control");
