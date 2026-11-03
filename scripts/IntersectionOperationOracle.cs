#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=IntersectionOperationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using ManagedGeometryTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","intersection-operation-oracle"));Directory.CreateDirectory(output);
var report=new StringBuilder();var cases=0;var failures=0;
void Log(string value){Console.WriteLine(value);report.AppendLine(value);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent intersection operation oracle",out var host,out var message))throw new Exception(message);
    using(host)unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");
        try
        {
            var x=Line(0,0,0,1,0,0);var y=Line(1,-2,0,0,1,0);var circle=Circle(0,0,0,2);
            CC("crossing-lines",x,-3,3,y,0,4);CC("coincident-opposite-lines",x,-2,2,Line(0,0,0,-1,0,0),-1,1);
            CC("skew-lines",x,-3,3,Line(0,0,1,0,1,0),-3,3);CC("line-circle",x,-3,3,circle,0,Math.Tau);
            CC("line-arc",x,-3,3,circle,0,Math.PI/2);CC("tangent-line-circle",Line(0,2,0,1,0,0),-3,3,circle,0,Math.Tau);
            CC("two-circles",circle,0,Math.Tau,Circle(2,0,0,2),0,Math.Tau);
            CC("noncoplanar-circles",circle,0,Math.Tau,Circle(0,0,0,2,0,1,0),0,Math.Tau);
            var xy=Plane(0,0,0,0,0,1);var sphere=Sphere(0,0,0,2);var torus=Torus(3,1);
            SC("plane-line",xy,Line(0,0,0,0,0,1),-3,3);SC("plane-line-overlap",xy,x,-3,3);
            SC("sphere-line",sphere,x,-3,3);SC("sphere-line-tangent",sphere,Line(0,2,0,1,0,0),-3,3);
            SC("torus-four-roots",torus,x,-5,5);SC("sphere-ellipse-four-roots",sphere,Ellipse(3,1),0,Math.Tau);
            SC("conic-chart-pi-tangency",Sphere(1,0,0,3),circle,0,Math.Tau);
            SC("nurbs-plane-three-roots",xy,Cubic(false),0,1);SC("rational-nurbs-plane-three-roots",xy,Cubic(true),0,1);
            CC("line-nurbs-three-roots",x,0,1,Cubic(false),0,1);
            SS("plane-plane",xy,Plane(0,0,0,1,0,0,0,1,0));SS("plane-sphere",xy,sphere);
            SS("sphere-sphere",sphere,Sphere(2,0,0,2));SS("sphere-tangent",sphere,Sphere(4,0,0,2));SS("coincident-surfaces",sphere,sphere);
            SS("oblique-plane-cylinder",Plane(0,0,0,Math.Sqrt(.5),0,Math.Sqrt(.5),0,1,0),Cylinder(2));
            SS("plane-torus-two-circles",xy,torus);SS("plane-torus-tangent-circle",Plane(0,0,1,0,0,1),torus);
            SS("bilinear-nurbs-hyperbola",Bilinear(false),Plane(0,0,.25,0,0,1));
            SS("bilinear-nurbs-crossing",Bilinear(true),xy);
            // Native ICURVE wire creation adopts its support geometry. Each
            // subsequent case therefore needs fresh orphan supports.
            SS("cubic-ruled-two-components",CubicRuled(false,false),Plane(0,0,0,0,0,1));
            SS("rational-cubic-ruled-two-components",CubicRuled(true,false),Plane(0,0,0,0,0,1));
            SS("rational-multiple-spans-four-components",CubicRuled(true,true),Plane(0,0,0,0,0,1));
            Require(failures==0,$"{failures} independent intersection cases failed; {cases} passed.");
            Log($"PASS {cases} independent intersection contract cases, checked parameters/types/branches and self-XT → real-PK wire-body comparisons.");
        }
        finally{Check(KernelRuntime.SessionStop(),"managed stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}

unsafe void CC(string label,GeometryPair a,double al,double ah,GeometryPair b,double bl,double bh)
{try{CCCore(label,a,al,ah,b,bl,bh);}catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}}
unsafe void SC(string label,GeometryPair surface,GeometryPair curve,double lo,double hi)
{try{SCCore(label,surface,curve,lo,hi);}catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}}
unsafe void SS(string label,GeometryPair a,GeometryPair b)
{try{SSCore(label,a,b);}catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}}
unsafe void CCCore(string label,GeometryPair a,double al,double ah,GeometryPair b,double bl,double bh)
{
    var mo=new M.PK_CURVE_intersect_curve_o_s{o_t_version=1};var po=new PK_CURVE_intersect_curve_o_t();
    int mn=0,pn=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;double* mt=null;double* mu=null;double* pt=null;double* pu=null;int* mk=null;int* pk=null;
    Check(KernelRuntime.CurveIntersectCurve(a.Managed,Interval(al,ah),b.Managed,Interval(bl,bh),&mo,&mn,&mp,&mt,&mu,&mk),label+" managed");
    Check(PK_CURVE_intersect_curve(a.Oracle,new(al,ah),b.Oracle,new(bl,bh),&po,&pn,&pp,&pt,&pu,&pk),label+" oracle");
    try
    {
        Require(mn==pn,$"{label}: count {mn}/{pn}");
        for(var i=0;i<mn;i++)
        {
            var matched=-1;for(var j=0;j<pn;j++)if(Distance(mp[i],pp[j])<2e-8&&Math.Abs(mt[i]-pt[j])<2e-8){matched=j;break;}
            Require(matched>=0,$"{label}: unmatched point {i}");Equal(mt[i],pt[matched],label+" first t");Equal(mu[i],pu[matched],label+" second t");Require(mk[i]==pk[matched],$"{label} point kind {mk[i]}/{pk[matched]}");
        }
        Log($"PASS {label}: {mn} point(s), complete first/second parameters and classifications");cases++;
    }
    finally{FreeManaged(mp);FreeManaged(mt);FreeManaged(mu);FreeManaged(mk);FreeOracle(pp);FreeOracle(pt);FreeOracle(pu);FreeOracle(pk);}
}
unsafe void SCCore(string label,GeometryPair surface,GeometryPair curve,double lo,double hi)
{
    var mo=new M.PK_SURF_intersect_curve_o_s{o_t_version=1};var po=new PK_SURF_intersect_curve_o_t();
    int mn=0,pn=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;M.PK_UV_s* muv=null;PK_UV_t* puv=null;double* mt=null;double* pt=null;int* mk=null;int* pk=null;
    Check(KernelRuntime.SurfIntersectCurve(surface.Managed,curve.Managed,Interval(lo,hi),&mo,&mn,&mp,&muv,&mt,&mk),label+" managed");
    Check(PK_SURF_intersect_curve(surface.Oracle,curve.Oracle,new(lo,hi),&po,&pn,&pp,&puv,&pt,&pk),label+" oracle");
    try
    {
        Require(mn==pn,$"{label}: count {mn}/{pn}");
        for(var i=0;i<mn;i++)
        {
            var matched=-1;for(var j=0;j<pn;j++)if(Math.Abs(mt[i]-pt[j])<2e-8&&Distance(mp[i],pp[j])<2e-8){matched=j;break;}
            Require(matched>=0,$"{label}: unmatched point {i}");Require(mk[i]==pk[matched],$"{label}: kind {mk[i]}/{pk[matched]}");
            for(var k=0;k<2;k++)Equal(muv[i].param[k],puv[matched].param[k],$"{label}: UV{k}");
        }
        Log($"PASS {label}: {mn} point(s), surface UV and curve parameters/classifications");cases++;
    }
    finally{FreeManaged(mp);FreeManaged(mt);FreeManaged(muv);FreeManaged(mk);FreeOracle(pp);FreeOracle(pt);FreeOracle(puv);FreeOracle(pk);}
}
unsafe void SSCore(string label,GeometryPair a,GeometryPair b)
{
    var mo=new M.PK_SURF_intersect_surf_o_s{o_t_version=3,mixed_curve_category=M.ParasolidConstants.PK_mixed_intersection_pline_c,tolerance=1e-5};var po=new PK_SURF_intersect_surf_o_t();
    int mnp=0,pnp=0,mnc=0,pnc=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;int* mc=null;int* pc=null;M.PK_INTERVAL_s* mr=null;PK_INTERVAL_t* pr=null;int* mk=null;int* pk=null;
    Check(KernelRuntime.SurfIntersectSurf(a.Managed,b.Managed,&mo,&mnp,&mp,&mnc,&mc,&mr,&mk),label+" managed");
    Check(PK_SURF_intersect_surf(a.Oracle,b.Oracle,&po,&pnp,&pp,&pnc,&pc,&pr,&pk),label+" oracle");
    try
    {
        Require(mnp==pnp&&mnc==pnc,$"{label}: isolated {mnp}/{pnp}, curves {mnc}/{pnc}");
        for(var i=0;i<mnp;i++){var found=false;for(var j=0;j<pnp;j++)if(Distance(mp[i],pp[j])<2e-8)found=true;Require(found,label+" isolated point");}
        var matched=new bool[pnc];
        for(var i=0;i<mnc;i++)
        {
            var mid=mr[i].value[0]/2+mr[i].value[1]/2;var point=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(mc[i],mid,0,&point),label+" curve midpoint");
            var index=-1;
            for(var j=0;j<pnc;j++)
            {
                if(matched[j])continue;double t=0;var position=new PK_VECTOR_t(point.coord[0],point.coord[1],point.coord[2]);
                if(PK_CURVE_parameterise_vector(pc[j],position,&t)!=0)continue;var evaluated=new PK_VECTOR_t();
                if(PK_CURVE_eval(pc[j],t,0,&evaluated)==0&&Distance(point,evaluated)<2e-8){index=j;break;}
            }
            Require(index>=0,label+" unmatched branch");matched[index]=true;Require(mk[i]==pk[index],label+" curve kind");
            var deviation=0.0;
            for(var sample=0;sample<=32;sample++)
            {
                var t=mr[i].value[0]+(mr[i].value[1]-mr[i].value[0])*sample/32;
                Check(KernelRuntime.CurveEval(mc[i],t,0,&point),label+" managed sample");double parameter=0;
                Check(PK_CURVE_parameterise_vector(pc[index],new(point.coord[0],point.coord[1],point.coord[2]),&parameter),label+" PK parameterization");
                var evaluated=new PK_VECTOR_t();Check(PK_CURVE_eval(pc[index],parameter,0,&evaluated),label+" PK sample");deviation=Math.Max(deviation,Distance(point,evaluated));
                var kp=new KernelVector3{X=point.coord[0],Y=point.coord[1],Z=point.coord[2]};
                Require(KernelRuntime.TryIntersectionPairedParameters(a.Managed,b.Managed,kp,out _,out _)==ProjectGmKernel.Native.Computation.AlgorithmStatus.Success,label+" independent support UV mapping");
            }
            Require(deviation<3e-8,$"{label}: branch deviation {deviation:R}");
            CompareWire(label+"-branch"+i,mc[i],mr[i],pc[index],pr[index]);Log($"{label}: branch {i} samples max deviation={deviation:R}");
        }
        Log($"PASS {label}: isolated={mnp}, curves={mnc}, complete branch set and independent self-XT wire comparisons");cases++;
    }
    finally{FreeManaged(mp);FreeManaged(mc);FreeManaged(mr);FreeManaged(mk);FreeOracle(pp);FreeOracle(pc);FreeOracle(pr);FreeOracle(pk);}
}
unsafe void CompareWire(string label,int managedCurve,M.PK_INTERVAL_s managedRange,int pkCurve,PK_INTERVAL_t pkRange)
{
    int managedBody=0,referenceBody=0;
    Check(KernelRuntime.CurveMakeWireBody(managedCurve,managedRange,&managedBody),label+" managed wire");
    Check(PK_CURVE_make_wire_body(pkCurve,pkRange,&referenceBody),label+" reference wire");
    var bytes=Transmit(managedBody);File.WriteAllBytes(Path.Combine(output,label+".x_t"),bytes);var received=Receive(bytes);
    for(var kind=0;kind<5;kind++)Require(Count(referenceBody,kind)==Count(received,kind),label+" topology count kind "+kind);
    var options=new PK_DEBUG_BODY_compare_o_t{all_tests=PK_LOGICAL_false,max_diffs=64,num_tests=PK_LOGICAL_true,
        tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,
        acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};
    var result=new PK_DEBUG_BODY_compare_r_t();Check(PK_DEBUG_BODY_compare(referenceBody,received,&options,&result),label+" compare");
    try
    {
        Log($"{label}: body compare global={result.global_result}, local={result.local_result}, entries={result.n_global_diffs}");
        for(var i=0;i<result.n_global_diffs;i++)Log($"  diff={result.global_diffs[i].diff}, masters={result.global_diffs[i].n_masters}, similars={result.global_diffs[i].n_similars}");
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&result.local_result==PK_DEBUG_local_res_no_diffs_c,label+" body comparison failed");
    }
    finally{Check(PK_DEBUG_BODY_compare_r_f(&result),label+" free compare");}
}
static unsafe int Count(int body,int kind)
{
    int n=0;int* list=null;var error=kind switch{0=>PK_BODY_ask_faces(body,&n,&list),1=>PK_BODY_ask_edges(body,&n,&list),2=>PK_BODY_ask_vertices(body,&n,&list),3=>PK_BODY_ask_shells(body,&n,&list),_=>PK_BODY_ask_regions(body,&n,&list)};
    Check(error,"PK topology count");FreeOracle(list);return n;
}
static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=M.ParasolidConstants.PK_transmit_meshes_separate_c};
    var block=new M.PK_MEMORY_block_s();Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"self text transmit");
    try{using var stream=new MemoryStream();for(var current=&block;current!=null;current=current->next)stream.Write(new ReadOnlySpan<byte>(current->bytes,checked((int)current->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"self text free");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n=0;int* parts=null;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive self XT");try{Require(n==1,"received body count");return parts[0];}finally{FreeOracle(parts);}
    }
}
static unsafe GeometryPair Line(double x,double y,double z,double dx,double dy,double dz)
{
    var sf=new M.PK_LINE_sf_s();sf.basis_set.location.coord[0]=x;sf.basis_set.location.coord[1]=y;sf.basis_set.location.coord[2]=z;
    sf.basis_set.axis.coord[0]=dx;sf.basis_set.axis.coord[1]=dy;sf.basis_set.axis.coord[2]=dz;
    int m=0,p=0;Check(KernelRuntime.LineCreate(&sf,&m),"self line");Check(PK_LINE_create((PK_LINE_sf_t*)&sf,&p),"PK line");return new(m,p);
}
static unsafe GeometryPair Circle(double x,double y,double z,double radius,double nx=0,double ny=0,double nz=1)
{var sf=new M.PK_CIRCLE_sf_s{basis_set=Axis(x,y,z,nx,ny,nz),radius=radius};int m=0,p=0;Check(KernelRuntime.CircleCreate(&sf,&m),"self circle");Check(PK_CIRCLE_create((PK_CIRCLE_sf_t*)&sf,&p),"PK circle");return new(m,p);}
static unsafe GeometryPair Ellipse(double a,double b)
{var sf=new M.PK_ELLIPSE_sf_s{basis_set=Axis(),R1=a,R2=b};int m=0,p=0;Check(KernelRuntime.EllipseCreate(&sf,&m),"self ellipse");Check(PK_ELLIPSE_create((PK_ELLIPSE_sf_t*)&sf,&p),"PK ellipse");return new(m,p);}
static unsafe GeometryPair Plane(double x,double y,double z,double nx,double ny,double nz,double rx=1,double ry=0,double rz=0)
{var sf=new M.PK_PLANE_sf_s{basis_set=Axis(x,y,z,nx,ny,nz,rx,ry,rz)};int m=0,p=0;Check(KernelRuntime.PlaneCreate(&sf,&m),"self plane");Check(PK_PLANE_create((PK_PLANE_sf_t*)&sf,&p),"PK plane");return new(m,p);}
static unsafe GeometryPair Sphere(double x,double y,double z,double radius)
{var sf=new M.PK_SPHERE_sf_s{basis_set=Axis(x,y,z),radius=radius};int m=0,p=0;Check(KernelRuntime.SphereCreate(&sf,&m),"self sphere");Check(PK_SPHERE_create((PK_SPHERE_sf_t*)&sf,&p),"PK sphere");return new(m,p);}
static unsafe GeometryPair Cylinder(double radius)
{var sf=new M.PK_CYL_sf_s{basis_set=Axis(),radius=radius};int m=0,p=0;Check(KernelRuntime.CylCreate(&sf,&m),"self cylinder");Check(PK_CYL_create((PK_CYL_sf_t*)&sf,&p),"PK cylinder");return new(m,p);}
static unsafe GeometryPair Torus(double a,double b)
{var sf=new M.PK_TORUS_sf_s{basis_set=Axis(),major_radius=a,minor_radius=b};int m=0,p=0;Check(KernelRuntime.TorusCreate(&sf,&m),"self torus");Check(PK_TORUS_create((PK_TORUS_sf_t*)&sf,&p),"PK torus");return new(m,p);}
static unsafe GeometryPair Cubic(bool rational)
{
    var dim=rational?4:3;double* vertices=stackalloc double[4*dim];double* zs=stackalloc double[4]{-.08,.14,-.14,.08};
    for(var i=0;i<4;i++){var w=rational?i%3+1:1;vertices[i*dim]=i/3.0*w;vertices[i*dim+1]=0;vertices[i*dim+2]=zs[i];if(rational)vertices[i*dim+3]=w;}
    double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{4,4};
    var sf=new M.PK_BCURVE_sf_s{degree=3,n_vertices=4,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=vertices,n_knots=2,knot=knots,knot_mult=mult,
        form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
    int m=0,p=0;Check(KernelRuntime.BCurveCreate(&sf,&m),"self cubic");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&sf,&p),"PK cubic");return new(m,p);
}
static unsafe GeometryPair Bilinear(bool saddle)
{
    double* vertices=stackalloc double[12];for(var u=0;u<2;u++)for(var v=0;v<2;v++){var at=(u*2+v)*3;vertices[at]=saddle?2*u-1:u;vertices[at+1]=saddle?2*v-1:v;vertices[at+2]=vertices[at]*vertices[at+1];}
    double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{2,2};
    var sf=new M.PK_BSURF_sf_s{u_degree=1,v_degree=1,n_u_vertices=2,n_v_vertices=2,vertex_dim=3,vertex=vertices,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
        form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
    int m=0,p=0;Check(KernelRuntime.BSurfCreate(&sf,&m),"self bilinear");Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&p),"PK bilinear");return new(m,p);
}
static unsafe GeometryPair CubicRuled(bool rational,bool twoSpans)
{
    var count=twoSpans?7:4;var dim=rational?4:3;double* vertices=stackalloc double[count*2*dim];double* zs=stackalloc double[7]{-.08,.14,-.14,.08,-.14,.14,-.08};
    for(var u=0;u<count;u++)for(var v=0;v<2;v++){var at=(u*2+v)*dim;var w=rational?u%3+1:1;vertices[at]=u/3.0*w;vertices[at+1]=v*w;vertices[at+2]=zs[u]+v*w;if(rational)vertices[at+3]=w;}
    double* uk=stackalloc double[3]{0,1,2};double* vk=stackalloc double[2]{0,1};int* um=stackalloc int[3]{4,twoSpans?3:4,4};int* vm=stackalloc int[2]{2,2};
    var sf=new M.PK_BSURF_sf_s{u_degree=3,v_degree=1,n_u_vertices=count,n_v_vertices=2,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=vertices,n_u_knots=twoSpans?3:2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=um,v_knot_mult=vm,
        form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
    int m=0,p=0;Check(KernelRuntime.BSurfCreate(&sf,&m),"self cubic ruled");Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&p),"PK cubic ruled");return new(m,p);
}
static unsafe M.PK_AXIS2_sf_s Axis(double x=0,double y=0,double z=0,double nx=0,double ny=0,double nz=1,double rx=1,double ry=0,double rz=0)
{var a=new M.PK_AXIS2_sf_s();a.location.coord[0]=x;a.location.coord[1]=y;a.location.coord[2]=z;a.axis.coord[0]=nx;a.axis.coord[1]=ny;a.axis.coord[2]=nz;a.ref_direction.coord[0]=rx;a.ref_direction.coord[1]=ry;a.ref_direction.coord[2]=rz;return a;}
static unsafe M.PK_INTERVAL_s Interval(double lo,double hi){var i=new M.PK_INTERVAL_s();i.value[0]=lo;i.value[1]=hi;return i;}
static unsafe double Distance(M.PK_VECTOR_s a,PK_VECTOR_t b){var dx=a.coord[0]-b.coord[0];var dy=a.coord[1]-b.coord[1];var dz=a.coord[2]-b.coord[2];return Math.Sqrt(dx*dx+dy*dy+dz*dz);}
static unsafe void FreeManaged(void* p){if(p!=null)Check(KernelRuntime.MemoryFree(p),"free managed result");}
static unsafe void FreeOracle(void* p){if(p!=null)Check(PK_MEMORY_free(p),"free oracle result");}
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException($"{label}: error {error}");}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static void Equal(double a,double b,string label)=>Require(double.IsFinite(a)&&double.IsFinite(b)&&Math.Abs(a-b)<=2e-8*Math.Max(1,Math.Abs(b)),$"{label}: {a:R}/{b:R}");
static string ScriptPath([CallerFilePath]string path="")=>path;
readonly record struct GeometryPair(ManagedGeometryTag Managed,PK_ENTITY_t Oracle);
