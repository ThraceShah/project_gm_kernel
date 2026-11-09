#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseShortEdgeNativeTrimIntegral=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;

const double PointLimit=1e-7;
const double StabilityLimit=PointLimit/100;
const string WrongGeometry="native finite wrong-size control";
var finiteControls=args.Length==2&&args[1]=="--finite-controls";
var finitePair=args.Length==3&&args[1]=="--finite-pair"&&Artifact(args[2]);
if(!(args.Length==1||finiteControls||finitePair)||!Artifact(args[0])||!args[0].EndsWith("-native.x_t",StringComparison.Ordinal))
    throw new ArgumentException("pass a native oracle artifact filename [--finite-controls | --finite-pair other.x_t] from temp_docs/short-edge-blend-oracle");
if(!ParasolidScriptHost.TryStartSession("native CHART refinement control",out var host,out var message))
    throw new InvalidOperationException(message);
using(host)unsafe
{
    var directory=Path.GetDirectoryName(Source())!;
    var schema=XtSchemaCatalog.OpenDirectory(Path.Combine(directory,"..","third_party","parasolid","schema"));
    var artifacts=Path.Combine(directory,"..","temp_docs","short-edge-blend-oracle");
    var bytes=File.ReadAllBytes(Path.Combine(artifacts,args[0]));
    if(finitePair)
    {
        CompareFinitePair(schema,bytes,File.ReadAllBytes(Path.Combine(artifacts,args[2])),"native finite artifact pair");
        return;
    }
    foreach(var sections in new[]{4,8})
    {
        var refined=Refine(schema,bytes,sections);
        var reference=Read(bytes);var actual=0;
        try
        {
            actual=Read(refined);CheckBody(reference);CheckBody(actual);
            ParasolidBodyCompare.AssertEquivalent(reference,actual,"native CHART refinement");
            var raw=CompareRawMass(reference,actual);
            if(finiteControls)
            {
                if(!(raw>=PointLimit))throw new InvalidOperationException("this CHART control does not reproduce the raw failure");
                RequireAgreement(CompareFinite(schema,reference,bytes,actual,refined,$"CHART x{sections}"));
            }
        }
        finally{if(actual!=0)Check(PK_ENTITY_delete(1,&actual));Check(PK_ENTITY_delete(1,&reference));}
    }
    if(finiteControls)foreach(var power in new[]{-22,-25,-27})CheckWrongSize(schema,bytes,power);
}

static bool Artifact(string filename)=>Path.GetFileName(filename)==filename&&filename.EndsWith(".x_t",StringComparison.Ordinal);

static unsafe void CompareFinitePair(XtSchemaCatalog schema,byte[] a,byte[] b,string label)
{
    var reference=Read(a);PK_BODY_t actual=0;
    try
    {
        actual=Read(b);CheckBody(reference);CheckBody(actual);
        ParasolidBodyCompare.AssertEquivalent(reference,actual,label);
        CompareRawMass(reference,actual);
        RequireAgreement(CompareFinite(schema,reference,a,actual,b,label));
    }
    finally{if(actual!=0)Check(PK_ENTITY_delete(1,&actual));Check(PK_ENTITY_delete(1,&reference));}
}

static unsafe void CheckWrongSize(XtSchemaCatalog schema,byte[] bytes,int power)
{
    var scale=1+Math.ScaleB(1,power);
    var reference=Read(bytes);PK_BODY_t copy=0,actual=0;
    try
    {
        copy=Read(bytes);var changed=ScaleAndTransmit(copy,scale);actual=Read(changed);
        CheckBody(reference);CheckBody(actual);CheckEqualCounts(reference,actual);
        var rejected=false;
        try{ParasolidBodyCompare.AssertEquivalent(reference,actual,WrongGeometry);}
        catch(InvalidOperationException e) when(e.Message==WrongGeometry+" body geometry/structure/sense/tolerance/matching differs")
        {rejected=true;Console.WriteLine("DIAGNOSTIC ONLY strict comparator rejected the wrong size");}
        if(!rejected)throw new InvalidOperationException("strict comparator accepted wrong geometry");
        CompareRawMass(reference,actual);
        var result=CompareFinite(schema,reference,bytes,actual,changed,$"scale 1+2^{power}",scale);
        var expectedDifference=power>=-25;
        if(!result.Stable||!(result.ScaleLawError<StabilityLimit)
            ||(expectedDifference?!(result.MinResidual>=PointLimit):!(result.MaxResidual<PointLimit)))
            throw new InvalidOperationException("finite wrong-size control did not show the expected discrimination");
        Console.WriteLine($"DIAGNOSTIC ONLY wrong-size control: finite={(expectedDifference?"difference":"below moment threshold")}; strict=rejected");
    }
    finally
    {
        if(actual!=0)Check(PK_ENTITY_delete(1,&actual));if(copy!=0)Check(PK_ENTITY_delete(1,&copy));Check(PK_ENTITY_delete(1,&reference));
    }
}

static void RequireAgreement((bool Stable,double MinResidual,double MaxResidual,double ScaleLawError) result)
{
    if(!result.Stable||!(result.MaxResidual<PointLimit))
        throw new InvalidOperationException("finite trim diagnostic did not establish stable agreement");
    Console.WriteLine("DIAGNOSTIC ONLY stable finite trim agreement; original gate unchanged");
}

static (bool Stable,double MinResidual,double MaxResidual,double ScaleLawError) CompareFinite(XtSchemaCatalog schema,
    PK_BODY_t a,byte[] bytesA,PK_BODY_t b,byte[] bytesB,string label,double scale=1)
{
    var chartA=ShortEdgeNativeTrimIntegral.ReadCharts(schema,bytesA);var chartB=ShortEdgeNativeTrimIntegral.ReadCharts(schema,bytesB);
    var origin=ShortEdgeNativeTrimIntegral.ReferenceOrigin(a);
    var previousA=ShortEdgeNativeTrimIntegral.Measure(a,chartA,origin,2);var previousB=ShortEdgeNativeTrimIntegral.Measure(b,chartB,origin,2);
    var stable=true;var minimum=double.PositiveInfinity;var maximum=0d;var scaleLaw=0d;
    Span<double> expected=stackalloc double[previousA.Length];
    foreach(var slices in new[]{4,8})
    {
        var x=ShortEdgeNativeTrimIntegral.Measure(a,chartA,origin,slices);var y=ShortEdgeNativeTrimIntegral.Measure(b,chartB,origin,slices);
        var changeA=ShortEdgeNativeTrimIntegral.Residual(previousA,x);var changeB=ShortEdgeNativeTrimIntegral.Residual(previousB,y);
        var residual=ShortEdgeNativeTrimIntegral.Residual(x,y);
        stable&=changeA<StabilityLimit&&changeB<StabilityLimit&&double.IsFinite(residual);
        minimum=Math.Min(minimum,residual);maximum=Math.Max(maximum,residual);
        if(scale!=1)
        {
            for(var i=0;i<x.Length;i++)expected[i]=x[i]*Math.Pow(scale,i==0?3:i<4?1:i==10?2:5);
            scaleLaw=Math.Max(scaleLaw,ShortEdgeNativeTrimIntegral.Residual(expected,y));
        }
        Console.WriteLine($"DIAGNOSTIC ONLY {label}: slices={slices} finite={residual:R} changes={changeA:R}/{changeB:R} scale_law={scaleLaw:R}");
        previousA=x;previousB=y;
    }
    Console.WriteLine($"DIAGNOSTIC ONLY {label}: native modulus overlap={Overlap(a,b)}/15, stable={stable}");
    return (stable,minimum,maximum,scaleLaw);
}

static unsafe byte[] ScaleAndTransmit(PK_BODY_t body,double scale)
{
    var matrix=new PK_TRANSF_sf_t();matrix.matrixI0J0=matrix.matrixI1J1=matrix.matrixI2J2=scale;matrix.matrixI3J3=1;
    PK_TRANSF_t transform;Check(PK_TRANSF_create(&matrix,&transform));
    try
    {
        int count;PK_ENTITY_t* entities;PK_LOGICAL_t* exact;
        Check(PK_BODY_transform(body,transform,0,&count,&entities,&exact));
        if(entities!=null)Check(PK_MEMORY_free(entities));if(exact!=null)Check(PK_MEMORY_free(exact));
    }
    finally{Check(PK_ENTITY_delete(1,&transform));}
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block));
    try
    {
        using var bytes=new MemoryStream();
        for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));
        return bytes.ToArray();
    }
    finally{Check(PK_MEMORY_block_f(&block));}
}

static unsafe void CheckEqualCounts(PK_BODY_t a,PK_BODY_t b)
{
    foreach(var kind in new[]{PK_CLASS_face,PK_CLASS_edge,PK_CLASS_vertex})
    {
        var x=Count(a,kind);var y=Count(b,kind);
        if(x!=y)throw new InvalidOperationException("wrong-size control changed topology counts");
        Console.WriteLine($"DIAGNOSTIC ONLY topology class={kind} counts={x}/{y}");
    }
    static int Count(PK_BODY_t body,PK_CLASS_t kind)
    {
        int n;PK_ENTITY_t* entities;
        Check(kind==PK_CLASS_face?PK_BODY_ask_faces(body,&n,&entities):kind==PK_CLASS_edge
            ?PK_BODY_ask_edges(body,&n,&entities):PK_BODY_ask_vertices(body,&n,&entities));
        if(entities!=null)Check(PK_MEMORY_free(entities));return n;
    }
}

static unsafe int Overlap(PK_BODY_t reference,PK_BODY_t actual)
{
    var options=new PK_TOPOL_eval_mass_props_o_t{bound=PK_mass_bound_modulus_c};
    double* a=stackalloc double[20];double* b=stackalloc double[20];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999999,&options,a,a+2,a+4,a+8,a+18));
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999999,&options,b,b+2,b+4,b+8,b+18));
    ReadOnlySpan<int> values=[0,2,4,5,6,8,9,10,11,12,13,14,15,16,18];
    ReadOnlySpan<int> errors=[1,3,7,7,7,17,17,17,17,17,17,17,17,17,19];
    var count=0;
    for(var i=0;i<values.Length;i++)
    {
        var e=errors[i];var v=values[i];
        if(!(a[e]>=0&&b[e]>=0)||!double.IsFinite(a[e]+b[e])||!double.IsFinite(a[v]-b[v]))
            throw new InvalidOperationException("nonfinite native mass estimate or invalid modulus");
        if(Math.Abs(a[v]-b[v])<=a[e]+b[e])count++;
    }
    return count;
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

static unsafe double CompareRawMass(PK_BODY_t reference,PK_BODY_t actual)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    ReadOnlySpan<string> names=["volume","mass","centroid.x","centroid.y","centroid.z","inertia.xx","inertia.xy","inertia.xz",
        "inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
    var maximum=0.0;
    foreach(var accuracy in new[]{.99999999,.99999999999,1d})
    {
        Check(PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+1,a+2,a+5,a+14));
        Check(PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+1,b+2,b+5,b+14));
        maximum=0;var index=0;
        for(var i=0;i<names.Length;i++)
        {
            var residual=Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i]));
            if(!double.IsFinite(residual))throw new InvalidOperationException("nonfinite native mass result");
            if(residual>maximum){index=i;maximum=residual;}
        }
        Console.WriteLine($"DIAGNOSTIC ONLY raw accuracy={accuracy:R} normalized15={maximum:R} worst={names[index]} values={a[index]:R}/{b[index]:R} original_1e-7_point_gate={(maximum<1e-7?"pass":"fail")}");
    }
    return maximum;
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
