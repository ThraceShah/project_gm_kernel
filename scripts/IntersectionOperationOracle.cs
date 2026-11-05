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
            SS("transposed-ruled-two-components",CubicRuled(false,false,true),Plane(0,0,0,0,0,1));
            SS("transposed-rational-ruled-two-components",CubicRuled(true,false,true),Plane(0,0,0,0,0,1));
            TransposedRuledBoundary();
            SS("tensor-polynomial-three-isocurves",TensorPatch(0,false,false),Plane(0,0,0,0,0,1));
            SS("tensor-rational-three-isocurves",TensorPatch(0,true,false),Plane(0,0,0,0,0,1));
            SS("tensor-rational-homogeneous-scale-1e-4",TensorPatch(0,true,false,1e-4),Plane(0,0,0,0,0,1));
            SS("tensor-rational-homogeneous-scale-1e4",TensorPatch(0,true,false,1e4),Plane(0,0,0,0,0,1));
            SS("tensor-transposed-three-isocurves",TensorPatch(0,false,true),Plane(0,0,0,0,0,1));
            SS("tensor-rational-transposed-isocurves",TensorPatch(0,true,true),Plane(0,0,0,0,0,1));
            SS("general-near-isoparametric-three-branches",TensorPatch(5,false,false),Plane(0,0,0,0,0,1));
            SS("general-skew-cylinders",Cylinder(1),Cylinder(.6,0,.1,.2,1,0,0));
            SS("general-eccentric-sphere-cylinder",Sphere(.2,.1,.3,1),Cylinder(.6));
            SS("general-sphere-torus",Torus(1,.4),Sphere(.2,0,0,1));
            SS("general-eccentric-torus-cylinder",Torus(2,.6),Cylinder(.5,2));
            SS("general-translated-tori",Torus(2,.6),Torus(2,.6,.2));
            SS("coaxial-sphere-torus-tangent-circle",Sphere(0,0,0,2),Torus(3,1));
            SS("tensor-quadratic-crossing",TensorPatch(1,false,false),Plane(0,0,0,0,0,1));
            SS("tensor-tangent-isocurve",TensorPatch(2,false,false),Plane(0,0,0,0,0,1));
            SS("tensor-disjoint",TensorPatch(3,false,false),Plane(0,0,0,0,0,1));
            SS("tensor-coincident-area",TensorPatch(4,false,false),Plane(0,0,0,0,0,1));
            SS("tensor-collapsed-pole",TensorPatch(6,false,false),Plane(0,0,0,1,0,0,0,1,0));
            SS("general-paraboloid-internal-loop",GeneralPatch(0),Plane(0,0,0,0,0,1));
            SS("general-nurbs-nurbs-internal-loop",GeneralPatch(0),GeneralPatch(1));
            SS("general-rational-internal-loop",GeneralPatch(4),Plane(0,0,0,0,0,1));
            SS("general-disjoint-patches",GeneralPatch(3),GeneralPatch(2));
            SS("general-cross-span-closed-loop",GeneralPatch(5),Plane(0,0,0,0,0,1));
            SS("general-convex-isolated-plane-contact",GeneralPatch(0),Plane(0,0,-.0625,0,0,1));
            SS("general-nurbs-sphere",GeneralPatch(2),Sphere(.5,.5,.1,.3));
            SS("general-nurbs-sphere-equator",GeneralPatch(2),Sphere(.5,.5,0,.3));
            SS("general-nurbs-cylinder",GeneralPatch(0),Cylinder(.5));
            SS("general-nurbs-cone",GeneralPatch(0),Cone(.5,.3));
            SS("general-nurbs-torus",GeneralPatch(2),Torus(.6,.2));
            SC("general-nurbs-line",GeneralPatch(0),Line(.5,.5,0,0,0,1),-1,1);
            CC("general-bcurve-bcurve-three-crossings",Cubic(false),0,1,StraightBCurve(),0,1);
            ProjectionContracts();
            DistanceContracts();
            BlendContracts();
            SplineEditingContracts();
            ExtremeTensorScaleBoundary("tensor-rational-extreme-small-scale",1e-200,PK_ERROR_weight_le_0);
            ExtremeTensorScaleBoundary("tensor-rational-extreme-large-scale",1e200,0);
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
unsafe void ExtremeTensorScaleBoundary(string label,double homogeneousScale,int expectedNativeCreateError)
{
    var surface=CreateTensorPatch(0,true,false,homogeneousScale,out var nativeCreateError);var plane=Plane(0,0,0,0,0,1);
    Log($"BOUNDARY {label}: homogeneous scale={homogeneousScale:R}, managed create=0, native PK_BSURF_create error={nativeCreateError}; excluded from positive compatibility count.");
    Require(nativeCreateError==expectedNativeCreateError,$"{label}: native creation behavior changed, expected {expectedNativeCreateError}, actual {nativeCreateError}");
    var mo=new M.PK_SURF_intersect_surf_o_s{o_t_version=3,mixed_curve_category=M.ParasolidConstants.PK_mixed_intersection_pline_c,tolerance=1e-5};
    var po=new PK_SURF_intersect_surf_o_t();int mnp=0,pnp=0,mnc=0,pnc=0;
    M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;int* mc=null;int* pc=null;M.PK_INTERVAL_s* mr=null;PK_INTERVAL_t* pr=null;int* mk=null;int* pk=null;
    try
    {
        var managedError=KernelRuntime.SurfIntersectSurf(surface.Managed,plane.Managed,&mo,&mnp,&mp,&mnc,&mc,&mr,&mk);
        Log($"BOUNDARY {label}: managed PK_SURF_intersect_surf error={managedError}, points={mnp}, curves={mnc}");
        Check(managedError,label+" managed intersection");Require(mnp==0&&mnc==3,label+" managed complete branch set");
        var seen=new bool[3];
        for(var i=0;i<mnc;i++)
        {
            Equal(mr[i].value[0],0,label+" managed interval start");Equal(mr[i].value[1],1,label+" managed interval end");
            Require(mk[i]==M.ParasolidConstants.PK_intersect_curve_simple_c,label+" managed classification");
            var startPoint=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(mc[i],0,0,&startPoint),label+" managed start");
            var rootIndex=-1;var baseY=0.0;
            for(var root=0;root<3;root++)
            {
                var v=(root+1)*.25;var y=(1.5*v+v*v)/(1+1.5*v);
                if(Math.Abs(startPoint.coord[1]-y)<2e-8){rootIndex=root;baseY=y;break;}
            }
            Require(rootIndex>=0&&!seen[rootIndex],label+" managed missing/duplicate isocurve");seen[rootIndex]=true;
            for(var sample=0;sample<=4;sample++)
            {
                var u=sample/4.0;var point=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(mc[i],u,0,&point),label+" managed sample");
                Equal(point.coord[0],u,label+" managed x");Equal(point.coord[1],baseY+.25*u*u,label+" managed y");Equal(point.coord[2],0,label+" managed z");
            }
            int body=0;Check(KernelRuntime.CurveMakeWireBody(mc[i],mr[i],&body),label+" managed wire");
            var bytes=Transmit(body);File.WriteAllBytes(Path.Combine(output,label+"-self-branch"+i+".x_t"),bytes);var received=Receive(bytes);
            CheckBody(received,label+" self-XT branch "+i);
            Require(Count(received,0)==0&&Count(received,1)==1&&Count(received,2)==2,label+" self-XT wire topology");
            Log($"BOUNDARY {label}: self-XT branch={i}, real-PK complete BODY_check faults=0, faces=0, edges=1, vertices=2");
        }
        if(nativeCreateError!=0)
        {
            // No valid native surface exists: do not fabricate a surface for
            // the remaining native calls or treat rejection as parity.
            Require(nativeCreateError==PK_ERROR_weight_le_0,label+" unexpected native create rejection");
            Log($"BOUNDARY {label}: native error {PK_ERROR_weight_le_0} (PK_ERROR_weight_le_0) prevents native intersection/wire construction; self-only geometry and all three received wires were checked.");
            return;
        }
        var nativeError=PK_SURF_intersect_surf(surface.Oracle,plane.Oracle,&po,&pnp,&pp,&pnc,&pc,&pr,&pk);
        Log($"BOUNDARY {label}: native PK_SURF_intersect_surf error={nativeError}, points={pnp}, curves={pnc}");
        Check(nativeError,label+" native intersection");Require(pnp==0&&pnc==3,label+" native complete branch count");
        for(var i=0;i<pnc;i++)
        {
            Require(pk[i]==PK_intersect_curve_simple_c,label+" native classification");
            Log($"BOUNDARY {label}: native branch={i}, interval=[{pr[i].value[0]:R},{pr[i].value[1]:R}], kind={pk[i]}");
            int body=0;var wireError=PK_CURVE_make_wire_body(pc[i],pr[i],&body);
            Log($"BOUNDARY {label}: native branch={i} PK_CURVE_make_wire_body error={wireError}, body={body}");Check(wireError,label+" native wire");
            var checkOptions=new PK_BODY_check_o_t{max_faults=100};int count=0;PK_check_fault_t* faults=null;
            var checkError=PK_BODY_check(body,&checkOptions,&count,&faults);
            Log($"BOUNDARY {label}: native branch={i} complete PK_BODY_check error={checkError}, faults={count}");
            try
            {
                Check(checkError,label+" native full check");
                if(i==0)Require(count==1,label+" original first-wire corrupt-body boundary changed");
                for(var fault=0;fault<count;fault++)
                {
                    Log($"BOUNDARY {label}: native branch={i} BODY_check fault={faults[fault].state}, entities={faults[fault].entity_1}/{faults[fault].entity_2}");
                    Require(faults[fault].state==PK_BODY_state_corrupt_c&&faults[fault].entity_1!=0&&faults[fault].entity_2==0,label+" unexpected native wire fault");
                }
            }
            finally{FreeOracle(faults);}
        }
        Log($"BOUNDARY {label}: native first wire retains PK_BODY_state_corrupt_c ({PK_BODY_state_corrupt_c}); all three self-XT wires have zero full-check faults. This remains a native limitation, not a passed compatibility fixture.");
    }
    finally{FreeManaged(mp);FreeManaged(mc);FreeManaged(mr);FreeManaged(mk);FreeOracle(pp);FreeOracle(pc);FreeOracle(pr);FreeOracle(pk);}
}
unsafe void TransposedRuledBoundary()
{
    const string label="transposed-rational-ruled-multiple-span-boundary";
    try
    {
        Log($"BOUNDARY {label}: original strict oracle failed with managed/native curves 4/2; this configuration is explicitly unsupported and is not included in the positive compatibility count.");
        for(var mode=0;mode<3;mode++)
        {
            var a=CubicRuled(true,true,true);var b=Plane(0,0,0,0,0,1);
            var mo=new M.PK_SURF_intersect_surf_o_s{o_t_version=3,mixed_curve_category=M.ParasolidConstants.PK_mixed_intersection_pline_c,tolerance=1e-5};
            var po=new PK_SURF_intersect_surf_o_t();
            if(mode!=0)
            {
                po.have_uvbox_1=PK_LOGICAL_true;po.uvbox_1=mode==1?new PK_UVBOX_t(0,0,1,2):new PK_UVBOX_t(0,1,1,2);
                mo.have_uvbox_1=1;for(var i=0;i<4;i++)mo.uvbox_1.param[i]=po.uvbox_1.param[i];
            }
            int mnp=-1,pnp=0,mnc=-1,pnc=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;int* mc=null;int* pc=null;
            M.PK_INTERVAL_s* mr=null;PK_INTERVAL_t* pr=null;int* mk=null;int* pk=null;
            var alive=KernelRuntime.Curves.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
            try
            {
                var error=KernelRuntime.SurfIntersectSurf(a.Managed,b.Managed,&mo,&mnp,&mp,&mnc,&mc,&mr,&mk);
                Require(error==M.ParasolidConstants.PK_ERROR_not_implemented,$"{label}: managed error {error}");
                Require(mnp==0&&mnc==0&&mp==null&&mc==null&&mr==null&&mk==null,label+": managed partial outputs");
                Require(alive==KernelRuntime.Curves.AliveCount&&blocks==KernelRuntime.ReturnOutstandingBlocks,label+": managed entity/buffer leak");
                Check(PK_SURF_intersect_surf(a.Oracle,b.Oracle,&po,&pnp,&pp,&pnc,&pc,&pr,&pk),label+" native diagnostic");
                Log($"BOUNDARY {label}: hint={mode} (0=default, 1=full domain, 2=second span), managed=not_implemented, native points={pnp}, curves={pnc}");
                Require(pnp==0&&pnc==2,label+": native behavior changed; revisit the recorded compatibility boundary");
                for(var i=0;i<pnc;i++)for(var sample=0;sample<=2;sample++)
                {
                    var t=pr[i].value[0]+(pr[i].value[1]-pr[i].value[0])*sample/2;
                    var point=new PK_VECTOR_t();var uv=new PK_UV_t();Check(PK_CURVE_eval(pc[i],t,0,&point),label+" native curve");
                    Check(PK_SURF_parameterise_vector(a.Oracle,point,&uv),label+" native UV");
                    Log($"  branch={i} t={t:R}, xyz=({point.coord[0]:R},{point.coord[1]:R},{point.coord[2]:R}), UV=({uv.param[0]:R},{uv.param[1]:R}), kind={pk[i]}");
                }
            }
            finally{FreeManaged(mp);FreeManaged(mc);FreeManaged(mr);FreeManaged(mk);FreeOracle(pp);FreeOracle(pc);FreeOracle(pr);FreeOracle(pk);}
        }
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
}
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
        for(var i=0;i<mnp;i++)
        {
            var found=-1;for(var j=0;j<pnp;j++)if(Distance(mp[i],pp[j])<2e-8)found=j;Require(found>=0,label+" isolated point");
            var ma=mp[i];var pa=pp[found];var mb=ma;var pb=pa;mb.coord[2]+=1;pb.coord[2]+=1;
            DistanceWitnessWire(label+"-isolated-point"+i,ma,pa,mb,pb);
        }
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
    CheckBody(referenceBody,label+" native");CheckBody(received,label+" received");
    for(var kind=0;kind<5;kind++)Require(Count(referenceBody,kind)==Count(received,kind),label+" topology count kind "+kind);
    var options=new PK_DEBUG_BODY_compare_o_t{all_tests=PK_LOGICAL_false,max_diffs=64,num_tests=PK_LOGICAL_true,
        tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,
        acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};
    var result=new PK_DEBUG_BODY_compare_r_t();Check(PK_DEBUG_BODY_compare(referenceBody,received,&options,&result),label+" compare");
    try
    {
        Log($"{label}: body compare global={result.global_result}, local={result.local_result}, entries={result.n_global_diffs}");
        for(var i=0;i<result.n_global_diffs;i++)Log($"  diff={result.global_diffs[i].diff}, masters={result.global_diffs[i].n_masters}, similars={result.global_diffs[i].n_similars}");
        var chartOnly=true;var chartDifference=false;
        for(var f=0;f<result.n_face_pairs;f++)for(var d=0;d<result.face_pairs[f].n_local_diffs;d++)
        {
            var diff=result.face_pairs[f].local_diffs[d];
            Log($"  local diff={diff.diff}, entities={diff.master_entity}/{diff.similar_entity}, class={diff.master_class}/{diff.similar_class}, values={diff.master_int}/{diff.similar_int}");
            chartOnly&=diff.diff==PK_DEBUG_diff_n_cht_pts_c;chartDifference|=diff.diff==PK_DEBUG_diff_n_cht_pts_c;
        }
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&(result.local_result==PK_DEBUG_local_res_no_diffs_c||chartOnly&&chartDifference),label+" body comparison failed");
    }
    finally{Check(PK_DEBUG_BODY_compare_r_f(&result),label+" free compare");}
}
unsafe void ProjectionContracts()
{
    var surface=GeneralPatch(0);var uv=new M.PK_UV_s();uv.param[0]=.17;uv.param[1]=.82;var point=new M.PK_VECTOR_s();
    Check(KernelRuntime.SurfEval(surface.Managed,uv,0,0,0,&point),"projection source");
    var ours=new M.PK_UV_s();var native=new PK_UV_t();var position=new PK_VECTOR_t(point.coord[0],point.coord[1],point.coord[2]);
    Check(KernelRuntime.SurfParameteriseVector(surface.Managed,point,&ours),"self surface inverse");Check(PK_SURF_parameterise_vector(surface.Oracle,position,&native),"native surface inverse");
    Equal(ours.param[0],native.param[0],"surface inverse U");Equal(ours.param[1],native.param[1],"surface inverse V");
    int mc=0,pc=0;Check(KernelRuntime.BSurfMakeBCurveVIsoparam(surface.Managed,ours.param[1],&mc),"self inverse isocurve");
    Check(PK_BSURF_make_bcurve_v_isoparam(surface.Oracle,native.param[1],&pc),"native inverse isocurve");
    var mr=Interval(0,1);PK_INTERVAL_t pr=new();pr.value[1]=1;CompareWire("surface-inverse-isocurve",mc,mr,pc,pr);cases++;
    point.coord[2]-=2;position.coord[2]-=2;
    var me=KernelRuntime.SurfParameteriseVector(surface.Managed,point,&ours);var pe=PK_SURF_parameterise_vector(surface.Oracle,position,&native);
    Log($"surface inverse off-surface self={me}, native={pe}");Require(me==pe,"surface inverse off-surface contract");
    var curve=Cubic(false);var sample=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(curve.Managed,.237,0,&sample),"curve inverse source");
    double mt=0,pt=0;Check(KernelRuntime.CurveParameteriseVector(curve.Managed,sample,&mt),"self curve inverse");
    Check(PK_CURVE_parameterise_vector(curve.Oracle,new(sample.coord[0],sample.coord[1],sample.coord[2]),&pt),"native curve inverse");Equal(mt,pt,"curve inverse parameter");
    mr=Interval(0,mt);pr.value[0]=0;pr.value[1]=pt;CompareWire("curve-inverse-bounded-wire",curve.Managed,mr,curve.Oracle,pr);cases++;
    Log("PASS public surface/curve inverse, bounded global projection and independent reconstructed-wire comparisons");
}

unsafe void DistanceContracts()
{
    VectorDistance("distance-point-paraboloid",GeneralPatch(0),.5,.5,-1);
    VectorDistance("distance-point-rational-surface",GeneralPatch(4),.2,.7,-1);
    VectorDistance("distance-point-sphere",Sphere(0,0,0,2),3,.2,.1);
    VectorDistance("distance-point-cylinder",Cylinder(2),3,.2,4);
    VectorDistance("distance-point-cone",Cone(2,.3),3,.2,4);
    VectorDistance("distance-point-torus",Torus(3,1),4.2,.2,.1);
    VectorDistance("distance-point-bcurve",Cubic(false),.4,.7,.1);
    PairDistance("distance-line-paraboloid",Line(.5,.5,-1,1,0,0),GeneralPatch(0));
    PairDistance("distance-nurbs-plane",GeneralPatch(0),Plane(0,0,-1,0,0,1));
    PairDistance("distance-skew-infinite-lines",Line(0,0,0,1,0,0),Line(0,1,1,0,1,0));
    PairDistance("distance-separated-spheres",Sphere(0,0,0,2),Sphere(6,1,.2,1));
}

unsafe void BlendContracts()
{
    BlendCase("blend-positive-planes",false,false,.2);
    BlendCase("blend-negative-planes",false,false,-.2);
    BlendCase("blend-plane-sphere",true,false,.2);
    BlendCase("blend-sphere-plane",true,true,.2);
}

unsafe void SplineEditingContracts()
{
    foreach(var rational in new[]{false,true})for(var operation=0;operation<3;operation++)
    {
        var label="edit-"+(rational?"rational":"polynomial")+"-curve-"+operation;
        try
        {
            var curve=Cubic(rational);
            Check(KernelRuntime.BCurveAddKnot(curve.Managed,.37),label+" self insert");Check(PK_BCURVE_add_knot(curve.Oracle,.37),label+" native insert");
            if(operation==1)
            {
                var mo=new M.PK_BCURVE_raise_degree_o_s{o_t_version=1};var po=new PK_BCURVE_raise_degree_o_t();
                Check(KernelRuntime.BCurveRaiseDegree(curve.Managed,2,&mo),label+" self elevate");Check(PK_BCURVE_raise_degree(curve.Oracle,2,&po),label+" native elevate");
            }
            if(operation==2)
            {
                var mr=Interval(-2,3);PK_INTERVAL_t pr=new();pr.value[0]=-2;pr.value[1]=3;var mo=new M.PK_BCURVE_reparameterise_o_s{o_t_version=1};var po=new PK_BCURVE_reparameterise_o_t();
                Check(KernelRuntime.BCurveReparameterise(curve.Managed,mr,&mo),label+" self reparameterise");Check(PK_BCURVE_reparameterise(curve.Oracle,pr,&po),label+" native reparameterise");
            }
            var mrange=new M.PK_INTERVAL_s();PK_INTERVAL_t prange;Check(KernelRuntime.CurveAskInterval(curve.Managed,&mrange),label+" self domain");Check(PK_CURVE_ask_interval(curve.Oracle,&prange),label+" native domain");
            Equal(mrange.value[0],prange.value[0],label+" domain start");Equal(mrange.value[1],prange.value[1],label+" domain end");
            M.PK_VECTOR_s* mj=stackalloc M.PK_VECTOR_s[3];PK_VECTOR_t* pj=stackalloc PK_VECTOR_t[3];
            for(var i=0;i<=12;i++)
            {
                var t=i==12?mrange.value[1]:mrange.value[0]+(mrange.value[1]-mrange.value[0])*i/12;
                Check(KernelRuntime.CurveEval(curve.Managed,t,2,mj),label+" self jets");Check(PK_CURVE_eval(curve.Oracle,t,2,pj),label+" native jets");
                for(var j=0;j<3;j++)for(var d=0;d<3;d++)Equal(mj[j].coord[d],pj[j].coord[d],label+" D0/D1/D2");
            }
            CompareWire(label,curve.Managed,mrange,curve.Oracle,prange);cases++;Log("PASS "+label+": same editing API, native jets and independent XT wire");
        }
        catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
    }
}

unsafe void BlendCase(string label,bool spherePair,bool sphereFirst,double radius)
{
    try
    {
        var plane=Plane(0,0,0,0,0,1);var other=spherePair?Sphere(0,0,1,1):Plane(0,0,0,1,0,0,0,1,0);
        var a=sphereFirst?other:plane;var b=sphereFirst?plane:other;
        var start=new M.PK_VECTOR_s();var end=new M.PK_VECTOR_s();start.coord[0]=spherePair?1:0;end.coord[1]=sphereFirst?-1:1;
        var mo=new M.PK_SURF_create_blend_o_s{o_t_version=1};var po=new PK_SURF_create_blend_o_t();int m=0,p=0;
        Check(KernelRuntime.SurfCreateBlend(a.Managed,radius,b.Managed,radius,start,end,&mo,&m),label+" managed create");
        Check(PK_SURF_create_blend(a.Oracle,radius,b.Oracle,radius,new PK_VECTOR_t(start.coord[0],0,0),new PK_VECTOR_t(0,end.coord[1],0),&po,&p),label+" native create");
        int mc=0,pc=0;Check(KernelRuntime.EntityAskClass(m,&mc),label+" managed class");Check(PK_ENTITY_ask_class(p,&pc),label+" native class");Require(mc==pc,label+" exact geometry class");
        M.PK_VECTOR_s* mj=stackalloc M.PK_VECTOR_s[9];PK_VECTOR_t* pj=stackalloc PK_VECTOR_t[9];
        for(var i=0;i<5;i++)
        {
            var uv=new M.PK_UV_s();uv.param[0]=.23+i*.17;uv.param[1]=.19+i*.13;var puv=new PK_UV_t(uv.param[0],uv.param[1]);
            Check(KernelRuntime.SurfEval(m,uv,2,2,0,mj),label+" managed jets");Check(PK_SURF_eval(p,puv,2,2,PK_LOGICAL_false,pj),label+" native jets");
            for(var j=0;j<9;j++)for(var d=0;d<3;d++)Equal(mj[j].coord[d],pj[j].coord[d],label+" D0/D1/D2");
        }
        var ma=a.Managed;var mb=b.Managed;Check(KernelRuntime.EntityDelete(1,&ma),label+" delete first support");Check(KernelRuntime.EntityDelete(1,&mb),label+" delete second support");
        var tool=spherePair?Plane(0,0,.2,0,0,1):Plane(0,.37,0,0,1,0,1,0,0);
        SSCore(label+"-intersection",new(m,p),tool);Log("PASS "+label+": same public create API, native jets, independent support lifetime and intersection wire");
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
}

unsafe void VectorDistance(string label,GeometryPair geometry,double x,double y,double z)
{
    try
    {
        var mo=new M.PK_GEOM_range_vector_o_s{o_t_version=2,have_tolerance=1,tolerance=1e-8,guess=new(){type=M.ParasolidConstants.PK_range_guess_no_c},opt_level=M.ParasolidConstants.PK_range_opt_accuracy_c};
        var po=new PK_GEOM_range_vector_o_t{have_tolerance=PK_LOGICAL_true,tolerance=1e-8,opt_level=PK_range_opt_accuracy_c};
        var query=new M.PK_VECTOR_s();query.coord[0]=x;query.coord[1]=y;query.coord[2]=z;int ms=0,ps=0;
        M.PK_range_1_r_s mr=default;PK_range_1_r_t pr=default;
        Check(KernelRuntime.GeomRangeVector(geometry.Managed,query,&mo,&ms,&mr),label+" managed");
        Check(PK_GEOM_range_vector(geometry.Oracle,new PK_VECTOR_t(x,y,z),&po,&ps,&pr),label+" native");
        Require(ms==ps&&ms==PK_range_result_found_c,label+" status");Equal(mr.distance,pr.distance,label+" minimum");
        Require(mr.end.entity==geometry.Managed&&pr.end.entity==geometry.Oracle&&mr.end.sub_entity==pr.end.sub_entity,label+" endpoint entities");
        Require(mr.end.region==(byte)pr.end.region&&mr.end.negative==(byte)pr.end.negative,label+" endpoint flags");
        for(var d=0;d<3;d++)Equal(mr.end.vector.coord[d],pr.end.vector.coord[d],label+" witness");
        for(var d=0;d<2;d++)Equal(mr.end.parameters[d],pr.end.parameters[d],label+" parameters");
        DistanceWitnessWire(label,mr.end.vector,pr.end.vector,query,new PK_VECTOR_t(x,y,z));
        cases++;Log("PASS "+label+": global minimum, native parameters/flags and independent XT wire");
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
}

unsafe void PairDistance(string label,GeometryPair a,GeometryPair b)
{
    try
    {
        var mo=new M.PK_GEOM_range_o_s{o_t_version=3,have_tolerance=1,tolerance=1e-8,range_type=M.ParasolidConstants.PK_range_type_minimum_c,opt_level=M.ParasolidConstants.PK_range_opt_accuracy_c};
        var guesses=(M.PK_range_guess_s*)mo.guesses;guesses[0].type=guesses[1].type=M.ParasolidConstants.PK_range_guess_no_c;
        var po=new PK_GEOM_range_o_t{have_tolerance=PK_LOGICAL_true,tolerance=1e-8,opt_level=PK_range_opt_accuracy_c};int ms=0,ps=0;M.PK_range_2_r_s mr=default;PK_range_2_r_t pr=default;
        Check(KernelRuntime.GeomRange(a.Managed,b.Managed,&mo,&ms,&mr),label+" managed");Check(PK_GEOM_range(a.Oracle,b.Oracle,&po,&ps,&pr),label+" native");
        Require(ms==ps&&ms==PK_range_result_found_c,label+" status");Equal(mr.distance,pr.distance,label+" minimum");
        var me=(M.PK_range_end_s*)mr.ends;var pe=&pr.endsI0;
        for(var i=0;i<2;i++)
        {
            Require(me[i].entity==(i==0?a.Managed:b.Managed)&&pe[i].entity==(i==0?a.Oracle:b.Oracle)&&me[i].sub_entity==pe[i].sub_entity,label+" endpoint entity");
            Require(me[i].region==(byte)pe[i].region&&me[i].negative==(byte)pe[i].negative,label+" endpoint flags");
            for(var d=0;d<2;d++)Equal(me[i].parameters[d],pe[i].parameters[d],label+" endpoint parameters");
            for(var d=0;d<3;d++)Equal(me[i].vector.coord[d],pe[i].vector.coord[d],label+" endpoint witness");
        }
        DistanceWitnessWire(label,me[0].vector,pe[0].vector,me[1].vector,pe[1].vector);cases++;Log("PASS "+label+": global pair minimum and independent XT wire");
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
}

unsafe void DistanceWitnessWire(string label,M.PK_VECTOR_s ma,PK_VECTOR_t pa,M.PK_VECTOR_s mb,PK_VECTOR_t pb)
{
    var ml=new M.PK_LINE_sf_s();ml.basis_set.location=ma;var pl=new PK_LINE_sf_t();pl.basis_set.location=pa;
    var md=0.0;var pd=0.0;
    for(var d=0;d<3;d++){var m=mb.coord[d]-ma.coord[d];var p=pb.coord[d]-pa.coord[d];ml.basis_set.axis.coord[d]=m;pl.basis_set.axis.coord[d]=p;md+=m*m;pd+=p*p;}
    for(var d=0;d<3;d++){ml.basis_set.axis.coord[d]/=Math.Sqrt(md);pl.basis_set.axis.coord[d]/=Math.Sqrt(pd);}
    int mc=0,pc=0;Check(KernelRuntime.LineCreate(&ml,&mc),label+" managed witness line");Check(PK_LINE_create(&pl,&pc),label+" native witness line");
    var mr=Interval(0,Math.Sqrt(md));PK_INTERVAL_t pr=new();pr.value[0]=0;pr.value[1]=Math.Sqrt(pd);CompareWire(label,mc,mr,pc,pr);
}
unsafe void CheckBody(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count=0;PK_check_fault_t* faults=null;
    Check(PK_BODY_check(body,&options,&count,&faults),label+" complete BODY_check");
    try
    {
        for(var i=0;i<count;i++)Log($"{label}: BODY_check fault={faults[i].state}, entities={faults[i].entity_1}/{faults[i].entity_2}");
        Require(count==0,label+" BODY_check faults="+count);
    }
    finally{FreeOracle(faults);}
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
static unsafe GeometryPair Cylinder(double radius,double x=0,double y=0,double z=0,double nx=0,double ny=0,double nz=1)
{var sf=new M.PK_CYL_sf_s{basis_set=Axis(x,y,z,nx,ny,nz,nx==1?0:1,nx==1?1:0,0),radius=radius};int m=0,p=0;Check(KernelRuntime.CylCreate(&sf,&m),"self cylinder");Check(PK_CYL_create((PK_CYL_sf_t*)&sf,&p),"PK cylinder");return new(m,p);}
static unsafe GeometryPair Cone(double radius,double angle)
{var sf=new M.PK_CONE_sf_s{basis_set=Axis(),radius=radius,semi_angle=angle};int m=0,p=0;Check(KernelRuntime.ConeCreate(&sf,&m),"self cone");Check(PK_CONE_create((PK_CONE_sf_t*)&sf,&p),"PK cone");return new(m,p);}
static unsafe GeometryPair Torus(double a,double b,double z=0)
{var sf=new M.PK_TORUS_sf_s{basis_set=Axis(0,0,z),major_radius=a,minor_radius=b};int m=0,p=0;Check(KernelRuntime.TorusCreate(&sf,&m),"self torus");Check(PK_TORUS_create((PK_TORUS_sf_t*)&sf,&p),"PK torus");return new(m,p);}
static unsafe GeometryPair Cubic(bool rational)
{
    var dim=rational?4:3;double* vertices=stackalloc double[4*dim];double* zs=stackalloc double[4]{-.08,.14,-.14,.08};
    for(var i=0;i<4;i++){var w=rational?i%3+1:1;vertices[i*dim]=i/3.0*w;vertices[i*dim+1]=0;vertices[i*dim+2]=zs[i];if(rational)vertices[i*dim+3]=w;}
    double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{4,4};
    var sf=new M.PK_BCURVE_sf_s{degree=3,n_vertices=4,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=vertices,n_knots=2,knot=knots,knot_mult=mult,
        form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
    int m=0,p=0;Check(KernelRuntime.BCurveCreate(&sf,&m),"self cubic");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&sf,&p),"PK cubic");return new(m,p);
}
static unsafe GeometryPair StraightBCurve()
{
    double* poles=stackalloc double[6]{0,0,0,1,0,0};double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{2,2};
    var sf=new M.PK_BCURVE_sf_s{degree=1,n_vertices=2,vertex_dim=3,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
        form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
    int m=0,p=0;Check(KernelRuntime.BCurveCreate(&sf,&m),"self straight B-curve");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&sf,&p),"PK straight B-curve");return new(m,p);
}
static unsafe GeometryPair GeneralPatch(int kind)
{
    var rational=kind==4;var dim=rational?4:3;double* poles=stackalloc double[36];double* z=stackalloc double[3]{.25,-.25,.25};
    var nu=kind==5?4:3;double* xs=stackalloc double[4]{0,.25,.75,1};double* zs=stackalloc double[4]{.25,0,0,.25};
    for(var u=0;u<nu;u++)for(var v=0;v<3;v++)
    {
        var at=(u*3+v)*dim;var w=rational?1+.2*u+.3*v:1;
        poles[at]=(kind==5?xs[u]:u*.5)*w;poles[at+1]=v*.5*w;poles[at+2]=(kind==1?.08*u*.5*v*.5:kind==2?0:(kind==5?zs[u]:z[u])+z[v]-.0625+(kind==3?10:0))*w;if(rational)poles[at+3]=w;
    }
    double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{3,3};
    double* uk=stackalloc double[3]{0,.5,1};int* um=stackalloc int[3]{3,1,3};
    var sf=new M.PK_BSURF_sf_s{u_degree=2,v_degree=2,n_u_vertices=nu,n_v_vertices=3,vertex_dim=dim,is_rational=(byte)(rational?1:0),
        vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
        form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
    if(kind==5){sf.n_u_knots=3;sf.u_knot=uk;sf.u_knot_mult=um;}
    int m=0,p=0;Check(KernelRuntime.BSurfCreate(&sf,&m),"self general patch");Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&p),"PK general patch");return new(m,p);
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
static unsafe GeometryPair CubicRuled(bool rational,bool twoSpans,bool transpose=false)
{
    var count=twoSpans?7:4;var dim=rational?4:3;double* vertices=stackalloc double[count*2*dim];double* zs=stackalloc double[7]{-.08,.14,-.14,.08,-.14,.14,-.08};
    for(var u=0;u<count;u++)for(var v=0;v<2;v++){var at=(transpose?v*count+u:u*2+v)*dim;var w=rational?u%3+1:1;vertices[at]=u/3.0*w;vertices[at+1]=v*w;vertices[at+2]=zs[u]+v*w;if(rational)vertices[at+3]=w;}
    double* uk=stackalloc double[3]{0,1,2};double* vk=stackalloc double[2]{0,1};int* um=stackalloc int[3]{4,twoSpans?3:4,4};int* vm=stackalloc int[2]{2,2};
    var sf=new M.PK_BSURF_sf_s{u_degree=3,v_degree=1,n_u_vertices=count,n_v_vertices=2,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=vertices,n_u_knots=twoSpans?3:2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=um,v_knot_mult=vm,
        form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
    if(transpose){sf.u_degree=1;sf.v_degree=3;sf.n_u_vertices=2;sf.n_v_vertices=count;sf.n_u_knots=2;sf.n_v_knots=twoSpans?3:2;sf.u_knot=vk;sf.v_knot=uk;sf.u_knot_mult=vm;sf.v_knot_mult=um;}
    int m=0,p=0;Check(KernelRuntime.BSurfCreate(&sf,&m),"self cubic ruled");Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&p),"PK cubic ruled");return new(m,p);
}
static unsafe GeometryPair TensorPatch(int mode,bool rational,bool transpose,double homogeneousScale=1)
{
    var pair=CreateTensorPatch(mode,rational,transpose,homogeneousScale,out var nativeError);Check(nativeError,"PK tensor patch");return pair;
}
static unsafe GeometryPair CreateTensorPatch(int mode,bool rational,bool transpose,double homogeneousScale,out int nativeError)
{
    var pu=2;var pv=mode is 0 or 5?3:2;var dim=rational?4:3;double* poles=stackalloc double[(pu+1)*(pv+1)*dim];
    double* zc=stackalloc double[4]{-.09375,.13541666666666666,-.13541666666666666,.09375};
    for(var u=0;u<=pu;u++)for(var v=0;v<=pv;v++)
    {
        var w=rational?1+v*.5:1;var at=(transpose?v*(pu+1)+u:u*(pv+1)+v)*dim;
        var x=u/(double)pu+(mode==1?.25*v*(v-1)/(pv*(pv-1)):0);var y=v/(double)pv+.25*u*(u-1)/(pu*(pu-1));
        var z=mode switch{0 or 5=>zc[v],1=>(u/(double)pu-.5)*(v/(double)pv-.5),2=>v==1?-.25:.25,3=>.25+v*(v-1)/(double)(pv*(pv-1)),_=>0};
        if(mode==5&&u==1&&v==1)z+=1e-9;
        if(mode==6){y=u*v/4.0;z=u==2?1:0;}
        poles[at]=x*w*homogeneousScale;poles[at+1]=y*w*homogeneousScale;poles[at+2]=z*homogeneousScale;if(rational)poles[at+3]=w*homogeneousScale;
    }
    double* uk=stackalloc double[2]{0,1};double* vk=stackalloc double[2]{0,1};
    int* um=stackalloc int[2]{(transpose?pv:pu)+1,(transpose?pv:pu)+1};int* vm=stackalloc int[2]{(transpose?pu:pv)+1,(transpose?pu:pv)+1};
    var sf=new M.PK_BSURF_sf_s{u_degree=transpose?pv:pu,v_degree=transpose?pu:pv,n_u_vertices=(transpose?pv:pu)+1,n_v_vertices=(transpose?pu:pv)+1,
        vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=um,v_knot_mult=vm,
        form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
    int m=0,p=0;Check(KernelRuntime.BSurfCreate(&sf,&m),"self tensor patch");nativeError=PK_BSURF_create((PK_BSURF_sf_t*)&sf,&p);return new(m,p);
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
