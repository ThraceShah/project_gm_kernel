#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=CurvedSheetPartitionOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

var name=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
if(name.Length==0||!name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("one new output name");
var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","native-spcurve-oracle",name));
if(Directory.Exists(directory))throw new InvalidOperationException("Existing evidence is immutable");Directory.CreateDirectory(directory);
if(!ParasolidScriptHost.TryStartSession("Finite native SP-curve queries",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session");
    try
    {
        PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"PK version");Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
        Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));var passed=0;
        PK_VECTOR_t* aJet=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* bJet=stackalloc PK_VECTOR_t[2];
        foreach(var rationalUv in new[]{false,true})foreach(var rationalSurface in new[]{false,true})foreach(var reverse in new[]{false,true})
        {
            var label=$"uv-{rationalUv}-surface-{rationalSurface}-reverse-{reverse}";
            var reference=NativeWire(rationalUv,rationalSurface,reverse);var input=Transmit(false,reference);File.WriteAllBytes(Path.Combine(directory,label+"-native-input.x_t"),input);
            var managed=Receive(true,input);Require(KernelRuntime.TryResolveBodySlot(managed,out var slot)&&!KernelRuntime.Xt.IsOpaque(slot),"editable native SP wire input");
            var ownCurve=WireCurve(true,managed,out var lower,out var upper);var referenceCurve=WireCurve(false,reference,out var nativeLower,out var nativeUpper);
            Require(Math.Abs(lower-nativeLower)<1e-9&&Math.Abs(upper-nativeUpper)<1e-9,"same finite native endpoint parameters");
            const double station=.375;var u=rationalUv?station/(2-station):station;var z=rationalSurface?4*u*station/(1+2*u+station):u*station;
            var plane=new PK_PLANE_sf_t();plane.basis_set.location=new(0,0,z);plane.basis_set.axis=new(0,0,1);plane.basis_set.ref_direction=new(1,0,0);int ownPlane,nativePlane;
            Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&plane,&ownPlane),"own query plane");Check(PK_PLANE_create(&plane,&nativePlane),"native query plane");
            foreach(var negative in new[]{false,true})
            {
                var last=negative?nativeLower+(nativeUpper-nativeLower)*.1:nativeUpper;var a=Query(true,ownPlane,ownCurve,nativeLower,last);var b=Query(false,nativePlane,referenceCurve,nativeLower,last);
                Require(a.Length==b.Length&&a.Length==(negative?0:1),"finite native SP root count");
                for(var i=0;i<a.Length;i++)for(var d=0;d<4;d++)Require(Math.Abs(a[i][d]-b[i][d])<1e-9,"native parameter and position correspondence");
                Console.WriteLine($"FINITE_SP_QUERY_PASS case={label} negative={negative} roots={a.Length}");passed++;
            }
            var ownBytes=Transmit(true,managed);File.WriteAllBytes(Path.Combine(directory,label+"-gm-fresh.x_t"),ownBytes);var fresh=Receive(false,ownBytes);
            CheckBody(reference);CheckBody(fresh);ParasolidBodyCompare.AssertEquivalent(reference,fresh,label);
            var freshCurve=WireCurve(false,fresh,out var first,out var lastParameter);Require(Math.Abs(first-nativeLower)<1e-9&&Math.Abs(lastParameter-nativeUpper)<1e-9,"fresh finite SP endpoint parameters");
            Console.WriteLine($"FINITE_SP_TRIM case={label} native={nativeLower:R}/{nativeUpper:R} managed={lower:R}/{upper:R} fresh={first:R}/{lastParameter:R}");
            for(var sample=0;sample<=16;sample++)
            {
                var t=first+(lastParameter-first)*sample/16;Check(PK_CURVE_eval(referenceCurve,t,1,aJet),"reference native SP derivative");Check(PK_CURVE_eval(freshCurve,t,1,bJet),"fresh native SP derivative");
                for(var derivative=0;derivative<2;derivative++)for(var d=0;d<3;d++)Require(Math.Abs(aJet[derivative].coord[d]-bJet[derivative].coord[d])<1e-10,"finite SP native gauge D0/D1");
            }
            Console.WriteLine($"FINITE_SP_SOURCE_WIRE_PASS case={label} author=real-PK-input samples=17 continuum_proof=False sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input))}");
            Check(KernelRuntime.EntityDelete(1,&managed),"delete imported SP source");Check(KernelRuntime.EntityDelete(1,&ownPlane),"delete own plane");Check(PK_ENTITY_delete(1,&reference),"delete native source");Check(PK_ENTITY_delete(1,&fresh),"delete native fresh source");Check(PK_ENTITY_delete(1,&nativePlane),"delete native plane");
        }
        Console.WriteLine($"PASS finite native SP queries={passed}");
    }
    finally{Check(KernelRuntime.SessionStop(),"managed stop");}
}

static unsafe int NativeWire(bool rationalUv,bool rationalSurface,bool reverse)
{
    double* p=stackalloc double[16];double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{2,2};var dim=rationalSurface?4:3;
    for(var u=0;u<2;u++)for(var v=0;v<2;v++){var w=rationalSurface?1+2*u+v:1;var at=(2*u+v)*dim;p[at]=u*w;p[at+1]=v*w;p[at+2]=u*v*w;if(rationalSurface)p[at+3]=w;}
    var sf=new PK_BSURF_sf_t{u_degree=1,v_degree=1,n_u_vertices=2,n_v_vertices=2,vertex_dim=dim,is_rational=rationalSurface?PK_LOGICAL_true:PK_LOGICAL_false,vertex=p,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
    int surface,curve,sp,wire;Check(PK_BSURF_create(&sf,&surface),"native SP support");double* uv=stackalloc double[9];
    if(rationalUv){uv[0]=uv[1]=0;uv[2]=2;uv[3]=.5;uv[4]=1;uv[5]=1.5;uv[6]=uv[7]=uv[8]=1;}
    else{uv[0]=uv[1]=0;uv[2]=uv[3]=1;}
    var uvCount=rationalUv?3:2;var uvDimension=rationalUv?3:2;
    if(reverse)for(var i=0;i<uvCount/2;i++)for(var d=0;d<uvDimension;d++)(uv[i*uvDimension+d],uv[(uvCount-1-i)*uvDimension+d])=(uv[(uvCount-1-i)*uvDimension+d],uv[i*uvDimension+d]);
    mult[0]=mult[1]=rationalUv?3:2;
    var cf=new PK_BCURVE_sf_t{degree=rationalUv?2:1,n_vertices=rationalUv?3:2,vertex_dim=rationalUv?3:2,is_rational=rationalUv?PK_LOGICAL_true:PK_LOGICAL_false,vertex=uv,n_knots=2,knot=knots,knot_mult=mult,form=PK_BCURVE_form_arbitrary_c,knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c};
    Check(PK_BCURVE_create(&cf,&curve),"native parameter BCurve");var spf=new PK_SPCURVE_sf_t(surface,curve);Check(PK_SPCURVE_create(&spf,&sp),"native SP curve");
    Check(PK_CURVE_make_wire_body(sp,new(.1,.9),&wire),"native finite source wire");return wire;
}
static unsafe double[][] Query(bool own,int plane,int curve,double first,double last)
{
    var options=new PK_SURF_intersect_curve_o_t();var bounds=new PK_INTERVAL_t(first,last);int n;PK_VECTOR_t* points;PK_UV_t* uv;double* parameters;int* kinds;
    Check(own?KernelRuntime.SurfIntersectCurve(plane,curve,*(M.PK_INTERVAL_s*)&bounds,(M.PK_SURF_intersect_curve_o_s*)&options,&n,(M.PK_VECTOR_s**)&points,(M.PK_UV_s**)&uv,&parameters,&kinds)
        :PK_SURF_intersect_curve(plane,curve,bounds,&options,&n,&points,&uv,&parameters,&kinds),"finite SP surface/curve query");
    try{var output=new double[n][];for(var i=0;i<n;i++)output[i]=[parameters[i],points[i].coord[0],points[i].coord[1],points[i].coord[2]];return output;}
    finally{foreach(var pointer in new nint[]{(nint)points,(nint)uv,(nint)parameters,(nint)kinds})if(pointer!=0)Check(own?KernelRuntime.MemoryFree((void*)pointer):PK_MEMORY_free((void*)pointer),"query output release");}
}
static unsafe int WireCurve(bool own,int body,out double first,out double last)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"wire edge");
    try
    {
        Require(n==1,"one finite source edge");int curve;
        if(own){var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edges[0]).Slot];curve=edge.CurveTag;Require(KernelRuntime.TryProfileEdgeRange(edge,out first,out last),"finite imported SP edge");}
        else{int cls;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[0],PK_LOGICAL_true,&curve,&cls,ends,&range,&sense),"native finite edge geometry");first=range.value[0];last=range.value[1];}
        return curve;
    }
    finally{if(edges!=null)Check(own?KernelRuntime.MemoryFree(edges):PK_MEMORY_free(edges),"wire edge list release");}
}
static unsafe byte[] Transmit(bool own,int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=380,transmit_user_fields=PK_LOGICAL_true};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"source XT transmit");
    try{using var stream=new MemoryStream();for(var b=&block;b!=null;b=b->next)stream.Write(new ReadOnlySpan<byte>(b->bytes,checked((int)b->n_bytes)));return stream.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block),"source XT release");}
}
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"source receive");try{Require(n==1,"one source part");return parts[0];}finally{if(parts!=null)Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts),"source list release");}}
}
static unsafe void CheckBody(int body)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full source BODY_check");try{for(var i=0;i<n;i++)Console.WriteLine($"BODY_FAULT state={faults[i].state} entities={faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,"valid finite source wire");}finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static void Require(bool value,string name){if(!value)throw new Exception(name);}
static string ScriptPath([CallerFilePath]string path="")=>path;
