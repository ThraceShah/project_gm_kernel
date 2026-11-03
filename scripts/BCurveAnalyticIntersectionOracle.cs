#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=BCurveAnalyticIntersectionOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using ManagedGeometryTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","bcurve-analytic-intersection-oracle"));Directory.CreateDirectory(output);
var report=new StringBuilder();var passed=0;var failed=0;var wireBoundaryCases=0;
void Log(string message){Console.WriteLine(message);report.AppendLine(message);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent rational B-curve analytic intersection oracle",out var host,out var message))throw new Exception(message);
    using(host)unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
        try
        {
            for(var kind=0;kind<4;kind++)for(var rational=0;rational<2;rational++)
                Run($"{Name(kind)}-{(rational!=0?"rational":"polynomial")}-cubic-all-roots",Surface(kind),Oscillating(kind,rational!=0),0,1);
            for(var kind=0;kind<4;kind++)
                Run($"{Name(kind)}-linear-spline-tangent",Surface(kind,true),Curve(1,[-3,kind==3?4:2,0,3,kind==3?4:2,0],[0,1],[2,2]),0,1);
            for(var kind=0;kind<2;kind++)
            {
                var w=Math.Sqrt(.5);Run($"{Name(kind)}-rational-circle-coincidence",Surface(kind,true),
                    Curve(2,[2,0,0,1,2*w,2*w,0,w,0,2,0,1,-2*w,2*w,0,w,-2,0,0,1],[0,1,2],[3,2,3],true),.25,1.75);
            }
            Run("sphere-all-knot-spans",Surface(0,true),Curve(1,[-3,0,0,-2,0,0,0,0,0,3,0,0],[0,1,2,3],[2,1,1,2]),0,3);
            Run("sphere-clipped-knot-spans",Surface(0,true),Curve(1,[-3,0,0,-2,0,0,0,0,0,3,0,0],[0,1,2,3],[2,1,1,2]),.5,2.5);
            Run("sphere-nonuniform-c1-quadratic-spans",Surface(0),Curve(2,[-3,0,0,5,.125,0,-5,.25,0,3,.375,0],[0,1,3],[3,1,3]),0,3);
            Run("sphere-rational-nonuniform-c1-quadratic-spans",Surface(0),Curve(2,[-3,0,0,1,10,.25,0,2,-5,.25,0,1,6,.75,0,2],[0,1,3],[3,1,3],true),0,3);
            Run("cone-far-nappe-rejected",Cone(1),Curve(1,[-3,0,-4,3,0,-4],[0,1],[2,2]),0,1);
            Run("cone-coincident-regular-generator",Cone(1),Curve(1,[1,0,0,2,0,2],[0,1],[2,2]),0,1);
            RejectApex();
            Run("sphere-near-tangent-root-pair",Surface(0,true),Curve(1,[-3,2-1e-6,0,3,2-1e-6,0],[0,1],[2,2]),0,1);
            var degree8=new double[27];for(var i=0;i<9;i++)degree8[3*i]=i-4;
            Run("sphere-degree-eight-bound",Surface(0,true),Curve(8,degree8,[0,1],[9,9]),0,1);
            Run("torus-degree-four-bound",Surface(3,true),Curve(4,[-5,0,0,-2.5,0,0,0,0,0,2.5,0,0,5,0,0],[0,1],[5,5]),0,1);
            Run("sphere-translated-rational",Surface(0,false,true),Oscillating(0,true,true),0,1);
            Run("torus-translated-rational",Surface(3,false,true),Oscillating(3,true,true),0,1);
            var baseCurve=Curve(1,[-3,0,0,3,0,0],[0,1],[2,2]);var trim=new M.PK_TRCURVE_sf_s{basis_curve=baseCurve.Managed,t_int=Range(.1,.9)};int mtrim=0,ptrim=0;
            Check(KernelRuntime.TrCurveCreate(&trim,&mtrim),"self trim");var pkBasis=baseCurve.Oracle;var trimStart=.1;var trimEnd=.9;int trimError=0;
            CRTRCU(&pkBasis,&trimStart,&trimEnd,&ptrim,&trimError);Check(trimError,"PK CRTRCU trim");
            Run("sphere-trimmed-parameter-remap",Surface(0,true),new(mtrim,ptrim),0,.8);
            CheckWireBoundary("near-folded-torus-wire",Surface(3),Oscillating(3,false,false,.01),0,1,12,PK_EDGE_state_bad_vertex_c);
            Run("sphere-c0-knots-and-root-partition",Surface(0,true),Curve(1,[-3,0,-.2,2,0,0,3,0,.1,0,0,.3],[0,1,2,3],[2,1,1,2]),0,3,[1,2]);
            RunWireContract("wire-full-c0-auto-split",Curve(1,[-3,0,-.2,2,0,0,3,0,.1,0,0,.3],[0,1,2,3],[2,1,1,2]),0,3,3);
            RunWireContract("wire-degree1-g1-speed-change",Curve(1,[0,0,0,1,0,0,3,0,0],[0,1,2],[2,1,2]),0,2,1);
            RunWireContract("wire-degree2-g1-speed-change",Curve(2,[0,0,0,.5,0,0,1,1,0,2,3,0,3,4,0],[0,1,2],[3,2,3]),0,2,1);
            Require(failed==0,$"{failed} case(s) failed; {passed} passed");
            Log($"PASS {passed} independent B-curve/analytic cases (including one explicit cone-apex rejection), plus {wireBoundaryCases} separately diagnosed native wire-fault boundary regression(s). All positive cases include complete point/parameter/UV/classification sets and real-PK full-check/strict-compare partition wires.");
        }
        finally{Check(KernelRuntime.SessionStop(),"self stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}

unsafe void Run(string label,GeometryPair surface,GeometryPair curve,double lo,double hi,double[]? additionalBreaks=null)
{
    try{RunCore(label,surface,curve,lo,hi,additionalBreaks);passed++;}
    catch(Exception ex){failed++;Log("FAIL "+label+": "+ex.Message);}
}
unsafe void RunCore(string label,GeometryPair surface,GeometryPair curve,double lo,double hi,double[]? additionalBreaks)
{
    var mo=new M.PK_SURF_intersect_curve_o_s{o_t_version=1};var po=new PK_SURF_intersect_curve_o_t();
    int mn=0,pn=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;M.PK_UV_s* muv=null;PK_UV_t* puv=null;double* mt=null;double* pt=null;int* mk=null;int* pk=null;
    var me=KernelRuntime.SurfIntersectCurve(surface.Managed,curve.Managed,Range(lo,hi),&mo,&mn,&mp,&muv,&mt,&mk);
    var pe=PK_SURF_intersect_curve(surface.Oracle,curve.Oracle,new(lo,hi),&po,&pn,&pp,&puv,&pt,&pk);
    try
    {
        Require(me==0&&pe==0,$"{label}: API errors self={me}, PK={pe}");Require(mn==pn,$"{label}: result count self={mn}, PK={pn}");
        var matched=new bool[pn];var oracleParameters=new double[mn];
        for(var i=0;i<mn;i++)
        {
            var match=-1;for(var j=0;j<pn;j++)if(!matched[j]&&Math.Abs(mt[i]-pt[j])<2e-8&&Distance(mp[i],pp[j])<2e-8){match=j;break;}
            Require(match>=0,$"{label}: unmatched root t={mt[i]:R} ({mp[i].coord[0]:R},{mp[i].coord[1]:R},{mp[i].coord[2]:R})");
            matched[match]=true;oracleParameters[i]=pt[match];Require(mk[i]==pk[match],$"{label}: kind self={mk[i]}, PK={pk[match]} at t={mt[i]:R}");
            for(var d=0;d<2;d++)
            {
                var delta=Math.Abs(muv[i].param[d]-puv[match].param[d]);if(d==0||label.StartsWith("torus",StringComparison.Ordinal))delta=Math.Min(delta,Math.Abs(delta-Math.Tau));
                Require(double.IsFinite(delta)&&delta<2e-8,$"{label}: UV{d} self={muv[i].param[d]:R}, PK={puv[match].param[d]:R}");
            }
            var cp=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(curve.Managed,mt[i],0,&cp),label+" curve root");
            for(var d=0;d<3;d++)Require(Math.Abs(cp.coord[d]-mp[i].coord[d])<2e-8,label+" curve residual");
            if(mk[i] is 14613 or 14614){Require(muv[i].param[0]==-1&&muv[i].param[1]==-1,label+" coincidence sentinel");continue;}
            var sp=new M.PK_VECTOR_s();Check(KernelRuntime.SurfEval(surface.Managed,muv[i],0,0,0,&sp),label+" surface root");
            for(var d=0;d<3;d++)Require(Math.Abs(sp.coord[d]-mp[i].coord[d])<2e-8,label+" surface residual");
        }
        // The intersection parameters partition two independently constructed
        // curves. Every nonempty partition is independently made into a wire;
        // our wire crosses the XT boundary before strict real-PK comparison.
        var wires=0;var breaks=new List<(double Self,double Oracle)>{(lo,lo),(hi,hi)};
        for(var i=0;i<mn;i++)breaks.Add((mt[i],oracleParameters[i]));
        if(additionalBreaks!=null)foreach(var knot in additionalBreaks){Require(knot>lo&&knot<hi,label+" extra knot is internal");breaks.Add((knot,knot));}
        breaks.Sort((a,b)=>a.Self.CompareTo(b.Self));
        var wireCurve=curve;var selfOffset=0.0;var oracleOffset=0.0;
        int curveClass=0;Check(KernelRuntime.EntityAskClass(curve.Managed,&curveClass),label+" curve class");
        if(curveClass==M.ParasolidConstants.PK_CLASS_trcurve)
        {
            var msf=new M.PK_TRCURVE_sf_s();var psf=new PK_TRCURVE_sf_t();Check(KernelRuntime.TrCurveAsk(curve.Managed,&msf),"self trim ask");Check(PK_TRCURVE_ask(curve.Oracle,&psf),"PK trim ask");
            wireCurve=new(msf.basis_curve,psf.basis_curve);selfOffset=msf.t_int.value[0];oracleOffset=psf.t_int.value[0];
            Log(label+": wire construction uses each trim's own basis and mapped parameter interval; intersection parameters above remain native trimmed parameters.");
        }
        for(var i=1;i<breaks.Count;i++)
        {
            var previous=breaks[i-1].Self;var oraclePrevious=breaks[i-1].Oracle;var next=breaks[i].Self;var oracleNext=breaks[i].Oracle;
            if(next-previous>1e-9&&oracleNext-oraclePrevious>1e-9)
            {CompareWire(label+"-segment"+wires++,wireCurve,previous+selfOffset,next+selfOffset,oraclePrevious+oracleOffset,oracleNext+oracleOffset);}
        }
        Log($"PASS {label}: roots={mn}, intersection-partition wires={wires}, complete parameters/UV/types, full BODY_check and strict XT structural comparison");
    }
    finally{FreeSelf(mp);FreeSelf(muv);FreeSelf(mt);FreeSelf(mk);FreePk(pp);FreePk(puv);FreePk(pt);FreePk(pk);}
}
unsafe void CompareWire(string label,GeometryPair curve,double lo,double hi,double plo,double phi,bool modern=false,int expectedEdges=-1)
{
    int mc=0,pc=0,mb=0,pb=0;Check(KernelRuntime.EntityCopy(curve.Managed,&mc),label+" self copy");Check(PK_ENTITY_copy(curve.Oracle,&pc),label+" PK copy");
    if(!modern)
    {Check(KernelRuntime.CurveMakeWireBody(mc,Range(lo,hi),&mb),label+" self wire");Check(PK_CURVE_make_wire_body(pc,new(plo,phi),&pb),label+" PK wire");}
    else
    {
        var wireOptions=new PK_CURVE_make_wire_body_o_t{want_edges=PK_LOGICAL_true,want_indices=PK_LOGICAL_true};var mr=Range(lo,hi);var pr=new PK_INTERVAL_t(plo,phi);
        int mn=0,pn=0;int* me=null;int* pe=null;int* mi=null;int* pi=null;
        Check(KernelRuntime.CurveMakeWireBody2(1,&mc,&mr,(M.PK_CURVE_make_wire_body_o_s*)&wireOptions,&mb,&mn,&me,&mi),label+" self wire2");
        Check(PK_CURVE_make_wire_body_2(1,&pc,&pr,&wireOptions,&pb,&pn,&pe,&pi),label+" PK wire2");
        try{Require(mn==expectedEdges&&pn==expectedEdges,$"{label}: returned edge counts {mn}/{pn}, expected {expectedEdges}");for(var i=0;i<mn;i++)Require(mi[i]==0&&pi[i]==0,label+" split edge indices retain original input index");}
        finally{FreeSelf(me);FreeSelf(mi);FreePk(pe);FreePk(pi);}
        Log($"{label}: both APIs return {mn} edges with original-input indices all zero");
    }
    var bytes=Transmit(mb);File.WriteAllBytes(Path.Combine(output,label+".x_t"),bytes);var imported=Receive(bytes);
    BodyCheck(pb,label+" independent reference");BodyCheck(imported,label+" self import");
    for(var kind=0;kind<5;kind++)Require(Count(imported,kind)==Count(pb,kind),label+" topology kind "+kind);
    if(expectedEdges>=0)Require(Count(imported,1)==expectedEdges,label+" actual expected edge count");
    var options=new PK_DEBUG_BODY_compare_o_t{all_tests=PK_LOGICAL_false,max_diffs=64,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,
        class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};
    var result=new PK_DEBUG_BODY_compare_r_t();Check(PK_DEBUG_BODY_compare(pb,imported,&options,&result),label+" compare");
    try
    {
        Log($"{label}: global={result.global_result}, local={result.local_result}, diff entries={result.n_global_diffs}");
        for(var i=0;i<result.n_global_diffs;i++)Log($"  diff={result.global_diffs[i].diff}, masters={result.global_diffs[i].n_masters}, similars={result.global_diffs[i].n_similars}");
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&result.local_result==PK_DEBUG_local_res_no_diffs_c,label+" strict comparison");
    }
    finally{Check(PK_DEBUG_BODY_compare_r_f(&result),"free comparison");Check(KernelRuntime.EntityDelete(1,&mb),"delete self wire");Check(PK_ENTITY_delete(1,&pb),"delete reference wire");Check(PK_ENTITY_delete(1,&imported),"delete imported wire");}
}
unsafe void RunWireContract(string label,GeometryPair curve,double lo,double hi,int expectedEdges)
{
    try
    {
        CompareWire(label+"-legacy",curve,lo,hi,lo,hi,false,expectedEdges);CompareWire(label+"-modern",curve,lo,hi,lo,hi,true,expectedEdges);
        Log($"PASS {label}: legacy and modern API both produce {expectedEdges} valid edges, matching input-index tracking, full BODY_check and strict XT comparison");passed++;
    }
    catch(Exception ex){failed++;Log("FAIL "+label+": "+ex.Message);}
}
unsafe void BodyCheck(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&options,&n,&faults),label+" BODY_check");
    try
    {
        for(var i=0;i<n;i++)
        {
            Log($"{label}: fault={faults[i].state}, entity={faults[i].entity_1}, other={faults[i].entity_2}");
            if(faults[i].state==PK_EDGE_state_bad_vertex_c)
            {
                int curve=0,point=0;var sf=new PK_POINT_sf_t();var value=new PK_VECTOR_t();double t=0;
                if(PK_EDGE_ask_curve(faults[i].entity_1,&curve)==0&&PK_VERTEX_ask_point(faults[i].entity_2,&point)==0&&PK_POINT_ask(point,&sf)==0
                    &&PK_CURVE_parameterise_vector(curve,sf.position,&t)==0&&PK_CURVE_eval(curve,t,0,&value)==0)
                    Log($"  vertex=({sf.position.coord[0]:R},{sf.position.coord[1]:R},{sf.position.coord[2]:R}), nearest t={t:R}, curve=({value.coord[0]:R},{value.coord[1]:R},{value.coord[2]:R})");
            }
        }
        Require(n==0,$"{label}: BODY_check faults={n}");
    }
    finally{FreePk(faults);}
}
unsafe void RejectApex()
{
    var surface=Cone(1);var curve=Curve(1,[-1,0,-4,1,0,0],[0,1],[2,2]);var mo=new M.PK_SURF_intersect_curve_o_s{o_t_version=1};var po=new PK_SURF_intersect_curve_o_t();
    int mn=0,pn=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;M.PK_UV_s* mu=null;PK_UV_t* pu=null;double* mt=null;double* pt=null;int* mk=null;int* pk=null;
    var me=KernelRuntime.SurfIntersectCurve(surface.Managed,curve.Managed,Range(0,1),&mo,&mn,&mp,&mu,&mt,&mk);
    var pe=PK_SURF_intersect_curve(surface.Oracle,curve.Oracle,new(0,1),&po,&pn,&pp,&pu,&pt,&pk);
    try
    {
        Require(me==M.ParasolidConstants.PK_ERROR_cant_do_intersect&&pe==PK_ERROR_cant_do_intersect,$"cone apex rejection self={me}, PK={pe}");
        Require(mn==0&&mp==null&&mu==null&&mt==null&&mk==null,"cone apex self outputs are cleared");Log("PASS negative cone apex coincidence: both APIs reject with cant_do_intersect; no self output buffers");passed++;
    }
    catch(Exception ex){failed++;Log("FAIL negative cone apex: "+ex.Message);}
    finally{FreeSelf(mp);FreeSelf(mu);FreeSelf(mt);FreeSelf(mk);FreePk(pp);FreePk(pu);FreePk(pt);FreePk(pk);}
}
unsafe void CheckWireBoundary(string label,GeometryPair surface,GeometryPair curve,double lo,double hi,int expectedRoots,int expectedFault)
{
    var mo=new M.PK_SURF_intersect_curve_o_s{o_t_version=1};var po=new PK_SURF_intersect_curve_o_t();
    int mn=0,pn=0;M.PK_VECTOR_s* mp=null;PK_VECTOR_t* pp=null;M.PK_UV_s* mu=null;PK_UV_t* pu=null;double* mt=null;double* pt=null;int* mk=null;int* pk=null;
    int mc=0,pc=0,mb=0,pb=0,received=0;
    try
    {
        Check(KernelRuntime.SurfIntersectCurve(surface.Managed,curve.Managed,Range(lo,hi),&mo,&mn,&mp,&mu,&mt,&mk),label+" self query");
        Check(PK_SURF_intersect_curve(surface.Oracle,curve.Oracle,new(lo,hi),&po,&pn,&pp,&pu,&pt,&pk),label+" PK query");
        Require(mn==expectedRoots&&pn==expectedRoots,$"{label}: all original roots retained self={mn}, PK={pn}, expected={expectedRoots}");
        var pkEnd=hi;
        for(var i=0;i<mn;i++)
        {
            var match=-1;for(var j=0;j<pn;j++)if(Math.Abs(mt[i]-pt[j])<2e-8&&Distance(mp[i],pp[j])<2e-8)match=j;
            Require(match>=0&&mk[i]==pk[match],label+" root positions/parameters/types");
            if(pt[i]>lo)pkEnd=Math.Min(pkEnd,pt[i]);
        }
        Check(KernelRuntime.EntityCopy(curve.Managed,&mc),label+" self copy");Check(PK_ENTITY_copy(curve.Oracle,&pc),label+" PK copy");
        Check(KernelRuntime.CurveMakeWireBody(mc,Range(lo,mt[0]),&mb),label+" self raw wire");Check(PK_CURVE_make_wire_body(pc,new(lo,pkEnd),&pb),label+" PK raw wire");
        var bytes=Transmit(mb);File.WriteAllBytes(Path.Combine(output,label+".x_t"),bytes);received=Receive(bytes);
        foreach(var body in new[]{received,pb})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&options,&n,&faults),label+" complete check");
            try
            {
                if(body==pb)Require(n>0,label+" expected documented native wire limitation");
                if(n==0)Log(label+": self-XT bounded wire has no BODY_check faults; native boundary remains separately reported.");
                for(var i=0;i<n;i++){Log($"{label}: {(body==received?"self-XT":"independent-PK")} body fault={faults[i].state}, entity={faults[i].entity_1}, other={faults[i].entity_2}");Require(faults[i].state==expectedFault,label+" unexpected wire fault");}
            }
            finally{FreePk(faults);}
        }
        Log($"PASS expected native wire boundary {label}: both intersection APIs return all {mn} roots; native direct wire has full BODY_check fault {expectedFault}; self wire has either no fault or the same diagnosed fault. This is not counted as a valid native-parity modeling wire.");wireBoundaryCases++;
    }
    catch(Exception ex){failed++;Log("FAIL wire boundary "+label+": "+ex.Message);}
    finally
    {
        FreeSelf(mp);FreeSelf(mu);FreeSelf(mt);FreeSelf(mk);FreePk(pp);FreePk(pu);FreePk(pt);FreePk(pk);
        if(mb!=0)Check(KernelRuntime.EntityDelete(1,&mb),"delete boundary self wire");if(pb!=0)Check(PK_ENTITY_delete(1,&pb),"delete boundary PK wire");if(received!=0)Check(PK_ENTITY_delete(1,&received),"delete boundary imported wire");
    }
}
static unsafe int Count(int body,int kind)
{
    int n=0;int* list=null;var error=kind switch{0=>PK_BODY_ask_faces(body,&n,&list),1=>PK_BODY_ask_edges(body,&n,&list),2=>PK_BODY_ask_vertices(body,&n,&list),3=>PK_BODY_ask_shells(body,&n,&list),_=>PK_BODY_ask_regions(body,&n,&list)};
    Check(error,"PK topology count");FreePk(list);return n;
}
static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=M.ParasolidConstants.PK_transmit_meshes_separate_c};
    var block=new M.PK_MEMORY_block_s();Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"self text transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"self text free");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n=0;int* parts=null;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive self XT");try{Require(n==1,"received body count");return parts[0];}finally{FreePk(parts);}
    }
}
static unsafe GeometryPair Oscillating(int kind,bool rational,bool moved=false,double separation=.125)
{
    var dim=rational?4:3;var poles=new double[4*dim];double[] wave=[-8,14,-14,8];
    for(var i=0;i<4;i++)
    {
        var w=rational?i%3+1:1;var t=i/3.0;
        if(kind==1){poles[dim*i]=t*w;poles[dim*i+1]=wave[i];}else{poles[dim*i]=wave[i];poles[dim*i+1]=separation*t*w;}
        if(moved){var x=poles[dim*i];poles[dim*i]=10*w;poles[dim*i+1]+=20*w;poles[dim*i+2]=30*w-x;}
        if(rational)poles[dim*i+3]=w;
    }
    return Curve(3,poles,[0,1],[4,4],rational);
}
static unsafe GeometryPair Surface(int kind,bool large=false,bool moved=false)
{
    int m=0,p=0;var axis=Axis();if(moved){axis.location.coord[0]=10;axis.location.coord[1]=20;axis.location.coord[2]=30;axis.axis.coord[0]=1;axis.axis.coord[2]=0;axis.ref_direction.coord[0]=0;axis.ref_direction.coord[2]=-1;}
    if(kind==0){var sf=new M.PK_SPHERE_sf_s{basis_set=axis,radius=large?2:.5};Check(KernelRuntime.SphereCreate(&sf,&m),"self sphere");Check(PK_SPHERE_create((PK_SPHERE_sf_t*)&sf,&p),"PK sphere");}
    if(kind==1){if(!large){axis.axis.coord[0]=1;axis.axis.coord[2]=0;axis.ref_direction.coord[0]=0;axis.ref_direction.coord[1]=1;}var sf=new M.PK_CYL_sf_s{basis_set=axis,radius=large?2:.5};Check(KernelRuntime.CylCreate(&sf,&m),"self cylinder");Check(PK_CYL_create((PK_CYL_sf_t*)&sf,&p),"PK cylinder");}
    if(kind==2)return Cone(large?2:.5);
    if(kind==3){var sf=new M.PK_TORUS_sf_s{basis_set=axis,major_radius=large?3:.6,minor_radius=large?1:.2};Check(KernelRuntime.TorusCreate(&sf,&m),"self torus");Check(PK_TORUS_create((PK_TORUS_sf_t*)&sf,&p),"PK torus");}
    return new(m,p);
}
static unsafe GeometryPair Cone(double radius)
{var sf=new M.PK_CONE_sf_s{basis_set=Axis(),radius=radius,semi_angle=Math.Atan(.5)};int m=0,p=0;Check(KernelRuntime.ConeCreate(&sf,&m),"self cone");Check(PK_CONE_create((PK_CONE_sf_t*)&sf,&p),"PK cone");return new(m,p);}
static unsafe GeometryPair Curve(int degree,double[] poles,double[] knots,int[] multiplicities,bool rational=false)
{
    fixed(double* vertices=poles,knot=knots)fixed(int* mult=multiplicities)
    {
        var sf=new M.PK_BCURVE_sf_s{degree=degree,n_vertices=poles.Length/(rational?4:3),vertex_dim=rational?4:3,is_rational=(byte)(rational?1:0),vertex=vertices,
            n_knots=knots.Length,knot=knot,knot_mult=mult,form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_unset_c};
        int m=0,p=0;Check(KernelRuntime.BCurveCreate(&sf,&m),"self B-curve");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&sf,&p),"PK B-curve");return new(m,p);
    }
}
static string Name(int kind)=>kind switch{0=>"sphere",1=>"cylinder",2=>"cone",_=>"torus"};
static unsafe M.PK_AXIS2_sf_s Axis(){var a=new M.PK_AXIS2_sf_s();a.axis.coord[2]=1;a.ref_direction.coord[0]=1;return a;}
static unsafe M.PK_INTERVAL_s Range(double a,double b){var r=new M.PK_INTERVAL_s();r.value[0]=a;r.value[1]=b;return r;}
static unsafe double Distance(M.PK_VECTOR_s a,PK_VECTOR_t b){var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);}
static unsafe void FreeSelf(void* p){if(p!=null)Check(KernelRuntime.MemoryFree(p),"free self");}
static unsafe void FreePk(void* p){if(p!=null)Check(PK_MEMORY_free(p),"free PK");}
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException($"{label}: error {error}");}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static string ScriptPath([CallerFilePath]string path="")=>path;
readonly record struct GeometryPair(ManagedGeometryTag Managed,PK_ENTITY_t Oracle);
