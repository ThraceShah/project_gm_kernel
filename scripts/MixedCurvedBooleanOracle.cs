#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using M=ProjectGmKernel.Native.Generated;

var names=new[]{"symmetric-unite","symmetric-intersect","symmetric-subtract","asymmetric-unite","asymmetric-intersect","asymmetric-subtract",
    "continuous-x-subtract","continuous-xy-subtract","continuous-asymmetric-contact-subtract","cavity-subtract","closed-loop-subtract","two-crossings-subtract",
    "two-tools-unite","two-tools-intersect","two-tools-subtract","nurbs-unite","nurbs-intersect","nurbs-subtract",
    "blend-unite","blend-intersect","blend-subtract","tilted-cylinder-subtract","refillet-new-edge"};
var profile=args.Contains("--profile",StringComparer.Ordinal);var noBoundCache=args.Contains("--no-bound-cache",StringComparer.Ordinal);var noSplineReuse=args.Contains("--no-spline-reuse",StringComparer.Ordinal);var repeat=ReadRepeat(args);
var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","mixed-curved-boolean-oracle");
if(profile)directory=Path.Combine(directory,(noBoundCache?"profile-uncached":"profile-cached")+(noSplineReuse?"-spline-uncached":""));Directory.CreateDirectory(directory);
if(args.Length>0&&args[0]=="--worker")return RunWorker(args[1],args.Contains("--native-only",StringComparer.Ordinal),directory,profile,noBoundCache,repeat,noSplineReuse);
if(args.Contains("list",StringComparer.Ordinal)||args.Contains("--list",StringComparer.Ordinal))
{
    foreach(var name in names)Console.WriteLine(name+": "+Description(name));return 0;
}
var seconds=600;var nativeOnly=args.Contains("--native-only",StringComparer.Ordinal);var selected=new List<string>();
for(var i=0;i<args.Length;i++)
{
    if(args[i]=="--native-only")continue;
    if(args[i] is "--profile" or "--no-bound-cache" or "--no-spline-reuse")continue;
    if(args[i]=="--repeat"){i++;continue;}
    if(args[i]=="--seconds"){Require(++i<args.Length&&int.TryParse(args[i],out seconds)&&seconds>0&&seconds<=3600,"--seconds must be in [1,3600]");continue;}
    Require(names.Contains(args[i],StringComparer.Ordinal),"unknown case "+args[i]);selected.Add(args[i]);
}
if(selected.Count==0)selected.AddRange(names);
Console.WriteLine($"MODE {(nativeOnly?"native-reference-only":"strict-independent")}, timeout={seconds}s per owned worker process");
var failures=0;var timeouts=0;var passed=0;
foreach(var name in selected)
{
    var logPath=Path.Combine(directory,name+(nativeOnly?"-native":"")+".log");
    using var writer=new StreamWriter(logPath,false){AutoFlush=true};var writeLock=new object();
    var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    info.ArgumentList.Add("--worker");info.ArgumentList.Add(name);if(nativeOnly)info.ArgumentList.Add("--native-only");
    if(profile)info.ArgumentList.Add("--profile");if(noBoundCache)info.ArgumentList.Add("--no-bound-cache");info.ArgumentList.Add("--repeat");info.ArgumentList.Add(repeat.ToString());
    if(noSplineReuse)info.ArgumentList.Add("--no-spline-reuse");
    using var process=new Process{StartInfo=info};
    process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)lock(writeLock)writer.WriteLine(e.Data);};
    process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)lock(writeLock)writer.WriteLine("STDERR "+e.Data);};
    var started=Stopwatch.GetTimestamp();Console.WriteLine("RUN "+name+": "+Description(name));
    Require(process.Start(),"start owned case worker");process.BeginOutputReadLine();process.BeginErrorReadLine();var timedOut=false;
    using(var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
    {
        try{await process.WaitForExitAsync(cancellation.Token);}
        catch(OperationCanceledException){timedOut=true;if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
    }
    process.WaitForExit();
    var elapsed=Stopwatch.GetElapsedTime(started).TotalSeconds;
    if(timedOut){timeouts++;writer.WriteLine($"TIMEOUT owned worker after {elapsed:F3}s; unresolved, not a passing result");Console.WriteLine($"TIMEOUT {name} {elapsed:F3}s");}
    else if(process.ExitCode==0){passed++;Console.WriteLine($"PASS {name} {elapsed:F3}s");}
    else{failures++;Console.WriteLine($"FAIL {name} exit={process.ExitCode} {elapsed:F3}s");}
}
Console.WriteLine($"SUMMARY {(nativeOnly?"native-only":"strict")} passed={passed}, failed={failures}, timed-out={timeouts}");return failures+timeouts==0?0:1;

static string Description(string name)=>name switch
{
    var n when n.StartsWith("symmetric-",StringComparison.Ordinal)=>"M1 fillet versus x>=.5 block (known manufacturing section)",
    var n when n.StartsWith("asymmetric-",StringComparison.Ordinal)=>"M1 fillet versus x>=.55 block (general Blend/plane section)",
    "continuous-x-subtract"=>"subtract x>=.5, then x>=.3 from the same filleted solid",
    "continuous-xy-subtract"=>"subtract x>=.5, then y>=.65; split the previous roof/plane ICurve",
    "continuous-asymmetric-contact-subtract"=>"subtract x>=.55, then y>=.05; split the previous non-iso Blend/plane ICurve",
    "cavity-subtract"=>"internal block [.4,.6]x[.45,.65]x[.2,.4], retain the exterior Blend shell",
    "closed-loop-subtract"=>"wide block with bottom z=1.045, make an interior closed roof intersection",
    "two-crossings-subtract"=>"slot x in [.3,.7], y in [-.075,.125], z in [.85,1.25], split the Blend twice",
    var n when n.StartsWith("two-tools-",StringComparison.Ordinal)=>"overlapping x>=.5 and y>=.65 tools with distinct finite planes",
    var n when n.StartsWith("nurbs-",StringComparison.Ordinal)=>"NURBS roof tool x[-.1,1.1], y[-.15,.25], zbase .45, roof .93 + .6 B2(u)B1(v)",
    var n when n.StartsWith("blend-",StringComparison.Ordinal)=>"second independently filleted roof translated by (.07,.03,.02), true Blend/Blend crossing",
    "tilted-cylinder-subtract"=>"radius .12 cylinder from (.2,.25,-.3), axis normalize(.3,.2,1), length1.8",
    "refillet-new-edge"=>"M1 fillet -> back notch y>=.6,z>=.7 -> new roof/y=.6 edge fillet radius .05",
    _=>throw new Exception("unknown scenario "+name),
};

static int ReadRepeat(string[] args)
{
    var index=Array.IndexOf(args,"--repeat");if(index<0)return 1;
    Require(index+1<args.Length&&int.TryParse(args[index+1],out var count)&&count is >=1 and <=3,"--repeat must be in [1,3]");return int.Parse(args[index+1]);
}

static unsafe int RunWorker(string name,bool nativeOnly,string directory,bool profile,bool noBoundCache,int repeat,bool noSplineReuse)
{
#if DEBUG
    Console.WriteLine("CONFIG Debug managed");
#else
    Console.WriteLine("CONFIG Release managed");
#endif
    Console.WriteLine("CASE "+name+": "+Description(name));var started=Stopwatch.GetTimestamp();
    if(!ParasolidScriptHost.TryStartSession("Independent mixed curved Boolean "+name,out var host,out var message)){Console.WriteLine(message);return 1;}
    using(host)
    {
        ModelingPerformance.Enabled=profile;ModelingPerformance.CacheEnabled=!noBoundCache;
        JointNestedBlendEvaluation.ReuseSplineContactBounds=!noSplineReuse;
        var options=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&options),"self session");
        try
        {
            for(var iteration=0;iteration<repeat;iteration++)
            {
            Console.WriteLine($"ITERATION {iteration+1}/{repeat}, {(iteration==0?"cold":"warm")}, bound-cache={!noBoundCache}");
            var reference=Produce(false,name,directory);Require(reference.Length>0,"manufactured scenario must produce material");for(var i=0;i<reference.Length;i++){CheckBody(reference[i],name+" native result "+i);File.WriteAllBytes(Path.Combine(directory,name+"-reference-"+i+".x_t"),TransmitNative(reference[i]));VerifyScenario(reference[i],name);}
            Console.WriteLine("NATIVE READY "+name+" results="+reference.Length);if(nativeOnly){Console.WriteLine("PASS native reference "+name);return 0;}
            var own=Produce(true,name,directory);Require(own.Length==reference.Length,name+" result body count");
            var matched=new bool[reference.Length];
            for(var i=0;i<own.Length;i++)
            {
                var bytes=Transmit(own[i]);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+".x_t"),bytes);var received=Receive(bytes);
                try
                {
                    CheckBody(received,name+" received "+i);var actual=Center(received);var index=-1;
                    for(var j=0;j<reference.Length;j++)if(!matched[j]&&Distance(actual,Center(reference[j]))<1e-7){Require(index<0,"unambiguous result correspondence");index=j;}
                    Require(index>=0,"result correspondence by independent PK centroid");matched[index]=true;VerifyScenario(received,name);Compare(reference[index],received,name+" result "+i);
                    if(name.StartsWith("continuous-",StringComparison.Ordinal)||name is "cavity-subtract" or "refillet-new-edge")CheckReloadClassification(own[i],reference[index],name);
                }
                finally{Check(PK_ENTITY_delete(1,&received),"delete received result");}
            }
            Console.WriteLine("PASS strict "+name+" iteration="+(iteration+1));
            }
            return 0;
        }
        catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);return 1;}
        finally{Console.WriteLine($"TIME case {name} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");Check(KernelRuntime.SessionStop(),"self session stop");}
    }
}

static unsafe void VerifyScenario(int body,string name)
{
    if(name.StartsWith("nurbs-",StringComparison.Ordinal)||name.StartsWith("blend-",StringComparison.Ordinal)||name=="two-crossings-subtract")
    {
        var document=XtText.DecodeDocument(System.Text.Encoding.ASCII.GetString(TransmitNative(body)));var found=0;
        foreach(var intersection in document.Nodes.Where(n=>document.Schema.GetNode(n.Type).Name=="INTERSECTION"))
        {
            var owner=document.Nodes.FirstOrDefault(n=>n.Index==intersection.Fields[2].Pointer);
            if(owner==null||document.Schema.GetNode(owner.Type).Name!="EDGE")continue;
            var first=document.Nodes.First(n=>n.Index==intersection.Fields[7].Pointer);var second=document.Nodes.First(n=>n.Index==intersection.Fields[8].Pointer);
            var a=document.Schema.GetNode(first.Type).Name;var b=document.Schema.GetNode(second.Type).Name;
            var requested=name.StartsWith("nurbs-",StringComparison.Ordinal)?"B_SURFACE":name.StartsWith("blend-",StringComparison.Ordinal)?"BLENDED_EDGE":"PLANE";
            if((a=="BLENDED_EDGE"&&b==requested)||(b=="BLENDED_EDGE"&&a==requested)){found++;Console.WriteLine($"CROSSING {name} curve=#{intersection.Index}, edge=#{owner.Index}, supports={a}/{b}");}
        }
        Require(found>=(name=="two-crossings-subtract"?2:1),name+" must actually intersect the requested finite Blend face pair");
    }
    if(name=="cavity-subtract")Require(RegionSignature(body).Split(',').Contains("1:2",StringComparer.Ordinal),"internal cavity material region has exterior and interior shells");
    if(name=="closed-loop-subtract")
    {
        int n;int* faces;Check(PK_BODY_ask_faces(body,&n,&faces),"closed loop faces");var found=false;
        try{for(var i=0;i<n;i++){int surface,type;Check(PK_FACE_ask_surf(faces[i],&surface),"closed loop support");Check(PK_ENTITY_ask_class(surface,&type),"closed loop support class");if(type!=PK_CLASS_bsurf)continue;int loops;int* tags;Check(PK_FACE_ask_loops(faces[i],&loops,&tags),"closed roof loops");try{found|=loops>=2;}finally{Free(false,tags);}}}finally{Free(false,faces);}
        Require(found,"closed interior roof intersection must leave a hole loop");
    }
}

static unsafe void CheckReloadClassification(int own,int reference,string name)
{
    var bytes=Transmit(own);int imported;
    fixed(byte* pointer=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=pointer,n_bytes=(nuint)bytes.Length};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&n,&parts),"self reload Boolean result");
        try{Require(n==1,"one self reloaded result");imported=parts[0];}finally{Free(true,parts);}
    }
    Check(KernelRuntime.EntityDelete(1,&own),"delete original before self reload consumers");
    try
    {
        var points=new[]{new PK_VECTOR_t(.1,.02,.25),new PK_VECTOR_t(.15,.25,.25),new PK_VECTOR_t(-.2,.5,.5),new PK_VECTOR_t(.51,.55,.3),new PK_VECTOR_t(.15,.9,.9),new PK_VECTOR_t(.15,.02,.98)};
        var inside=0;var outside=0;
        for(var p=0;p<points.Length;p++)
        {
            var point=points[p];
            int actual,expected,topology;Check(KernelRuntime.BodyContainsVector(imported,*(M.PK_VECTOR_s*)&point,&actual,&topology),"self reloaded point classification");Check(PK_BODY_contains_vector(reference,point,&expected,&topology),"independent PK point classification");
            Console.WriteLine($"CLASSIFY {name} ({point.coord[0]:R},{point.coord[1]:R},{point.coord[2]:R}) self={actual}, PK={expected}");Require(actual==expected,name+" reloaded material classification");
            Require(expected==PK_enclosure_inside_c||expected==PK_enclosure_outside_c,"query point must have a defined non-boundary classification");
            if(p==0)Require(expected==PK_enclosure_inside_c,"manufactured material point");
            if(p==2||(name=="cavity-subtract"&&p==3))Require(expected==PK_enclosure_outside_c,"manufactured exterior or cavity point");
            if(actual==PK_enclosure_inside_c)inside++;if(actual==PK_enclosure_outside_c)outside++;
        }
        Require(inside>0&&outside>0,"reloaded result includes verified material and void query points");
    }
    finally{Check(KernelRuntime.EntityDelete(1,&imported),"delete self reloaded consumer body");}
}

static unsafe int[] Produce(bool own,string name,string directory)
{
    var target=Roof(own);Fillet(own,target,.1);if(!own)CheckBody(target,"native M1 source");
    var function=name.EndsWith("-unite",StringComparison.Ordinal)?PK_boolean_unite_c:name.EndsWith("-intersect",StringComparison.Ordinal)?PK_boolean_intersect_c:PK_boolean_subtract_c;
    var kind=name[..name.LastIndexOf('-')];int tool;
    if(name is "continuous-x-subtract" or "continuous-xy-subtract" or "continuous-asymmetric-contact-subtract")
    {
        var asymmetric=name=="continuous-asymmetric-contact-subtract";
        var first=Boolean(own,target,new[]{Block(own,asymmetric?1.05:1,.5,-.1,1,2,2)},PK_boolean_subtract_c,name+" first");Require(first.Length==1,"one first-cut body");target=first[0];
        tool=name=="continuous-x-subtract"?Block(own,.8,.5,-.1,1,2,2):Block(own,.2,1,-.15,2.4,asymmetric?1.9:.7,2.1);
    }
    else if(name=="refillet-new-edge")
    {
        var cut=Boolean(own,target,new[]{Block(own,.5,1.1,.7,2,1,1)},PK_boolean_subtract_c,"back-notch Boolean");Require(cut.Length==1,"one back-notch body");target=cut[0];
        if(!own)CheckBody(target,"native before re-fillet");else File.WriteAllBytes(Path.Combine(directory,name+"-before-refillet.x_t"),Transmit(target));
        Fillet(own,target,.05,.6,1);Require(CountBlendFaces(own,target)>=2,"original and new Blend faces survive");return new[]{target};
    }
    else if(kind=="symmetric")tool=Block(own,1,.5,-.1,1,2,2);
    else if(kind=="asymmetric")tool=Block(own,1.05,.5,-.1,1,2,2);
    else if(kind=="cavity")tool=Block(own,.5,.55,.2,.2,.2,.2);
    else if(kind=="closed-loop")tool=Block(own,.5,.5,1.045,2,2,1);
    else if(kind=="two-crossings")tool=Block(own,.5,.025,.85,.4,.2,.4);
    else if(kind=="two-tools")
        return Boolean(own,target,new[]{Block(own,1,.4,-.2,1,2.2,2),Block(own,.2,1,-.15,2.4,.7,2.1)},function,"overlapping multi-tool Boolean");
    else if(kind=="nurbs")tool=RoofTool(own);
    else if(kind=="blend")
    {
        tool=Roof(own);Fillet(own,tool,.1);Translate(own,tool,.07,.03,.02);
    }
    else if(kind=="tilted-cylinder")tool=TiltedCylinder(own);
    else throw new Exception("missing tool "+kind);
    return Boolean(own,target,new[]{tool},function,name);
}

static unsafe int[] Boolean(bool own,int target,int[] tools,PK_boolean_function_t function,string label)
{
    if(own)
    {
        var output=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","mixed-curved-boolean-oracle");var stem=label.Replace(' ','-');
        File.WriteAllBytes(Path.Combine(output,stem+"-self-target.x_t"),Transmit(target));
        for(var i=0;i<tools.Length;i++)File.WriteAllBytes(Path.Combine(output,stem+"-self-tool-"+i+".x_t"),Transmit(tools[i]));
    }
    DumpSupports(own,target,tools);if(!own)foreach(var tool in tools)CheckBody(tool,"native tool");
    var options=new PK_BODY_boolean_o_t{function=function,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    WebProgressCapture.Before(own,target,[],"PK_BODY_boolean",tools);WebProgressCapture.BooleanParameters(own,options);
    Console.WriteLine($"BEGIN {(own?"self":"PK")} {label}, function={function}, tools={tools.Length}");var started=Stopwatch.GetTimestamp();int error;double webMs=0;
    fixed(KernelRuntime.RegularBooleanStage* stage=&KernelRuntime.LastRegularBooleanStage)
    fixed(int* face=&KernelRuntime.LastRegularBooleanFace)
    fixed(int* other=&KernelRuntime.LastRegularBooleanOtherFace)
    {
        var stageAddress=(nint)stage;var faceAddress=(nint)face;var otherAddress=(nint)other;
        var progress=new Timer(_=>Console.WriteLine($"PROGRESS {(own?"self":"PK")} {label}, elapsed={Stopwatch.GetElapsedTime(started).TotalSeconds:F1}s, stage={*(KernelRuntime.RegularBooleanStage*)stageAddress}, face={Volatile.Read(ref *(int*)faceAddress)}, other={Volatile.Read(ref *(int*)otherAddress)}"),null,TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(10));
        try{var apiStarted=Stopwatch.GetTimestamp();fixed(int* input=tools)error=own?KernelRuntime.BodyBoolean(target,tools.Length,input,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(target,tools.Length,input,&options,&n,&bodies);webMs=Stopwatch.GetElapsedTime(apiStarted).TotalMilliseconds;}
        finally{progress.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    }
    WebProgressCapture.BooleanAfter(own,error==0?new ReadOnlySpan<int>(bodies,n).ToArray():[],webMs,error,0,null);
    Console.WriteLine($"TIME {(own?"self":"PK")} {label} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}, error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, other={KernelRuntime.LastRegularBooleanOtherFace}, sheet={KernelRuntime.LastSheetPartitionStage}");
    if(own&&ModelingPerformance.Enabled)PrintProfile();
    Check(error,label);
    try{var result=new int[n];new ReadOnlySpan<int>(bodies,n).CopyTo(result);return result;}finally{Free(own,bodies);}
}
static void PrintProfile()
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/Stopwatch.Frequency;
    Console.WriteLine($"PROFILE elapsed-ms={p.ElapsedTicks*milliseconds:F3}, pairs={p.SurfacePairs}, ssi-cells={p.SsiCells}, root-cells={p.RootCells}, roots={p.Roots}, budget-failures={p.BudgetFailures}, winding={p.WindingCalls}, bound-requests={p.BoundRequests}, bound-evaluations={p.BoundEvaluations}, cache-hits={p.BoundCacheHits}, bound-ms={p.BoundTicks*milliseconds:F3}");
    foreach(var stage in Enum.GetValues<KernelRuntime.RegularBooleanStage>())if(p.Stages[(int)stage]!=0)Console.WriteLine($"PROFILE stage={stage} ms={p.Stages[(int)stage]*milliseconds:F3}");
}
static unsafe void DumpSupports(bool own,int target,ReadOnlySpan<int> tools)
{
    var global=0;for(var body=0;body<=tools.Length;body++)
    {
        var tag=body==0?target:tools[body-1];int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(tag,&n,&faces):PK_BODY_ask_faces(tag,&n,&faces),"operand faces");
        try{for(var f=0;f<n;f++)
        {
            int surf,type;Check(own?KernelRuntime.FaceAskSurf(faces[f],&surf):PK_FACE_ask_surf(faces[f],&surf),"operand surface");Check(own?KernelRuntime.EntityAskClass(surf,&type):PK_ENTITY_ask_class(surf,&type),"operand surface class");Console.WriteLine($"SUPPORT {(own?"self":"PK")} global={global++}, owner={body}, face={faces[f]}, surface={surf}, class={type}");
            if(type==PK_CLASS_plane){PK_PLANE_sf_t p;Check(own?KernelRuntime.PlaneAsk(surf,(M.PK_PLANE_sf_s*)&p):PK_PLANE_ask(surf,&p),"operand plane");Console.WriteLine($" PLANE origin=({p.basis_set.location.coord[0]:R},{p.basis_set.location.coord[1]:R},{p.basis_set.location.coord[2]:R}) axis=({p.basis_set.axis.coord[0]:R},{p.basis_set.axis.coord[1]:R},{p.basis_set.axis.coord[2]:R})");}
        }}
        finally{Free(own,faces);}
    }
}
static unsafe int CountBlendFaces(bool own,int body)
{
    int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"result faces");var count=0;
    try{for(var i=0;i<n;i++){int surface,type;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"result support");Check(own?KernelRuntime.EntityAskClass(surface,&type):PK_ENTITY_ask_class(surface,&type),"result support class");if(type==PK_CLASS_blendsf)count++;}return count;}
    finally{Free(own,faces);}
}
static unsafe int RoofTool(bool own)
{
    var body=Block(own,.5,.05,.45,1.2,.4,.48);int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"tool faces");var top=0;
    try{for(var i=0;i<n;i++){int surf;PK_PLANE_sf_t p;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surf):PK_FACE_ask_surf(faces[i],&surf),"tool support");Check(own?KernelRuntime.PlaneAsk(surf,(M.PK_PLANE_sf_s*)&p):PK_PLANE_ask(surf,&p),"tool plane");if(p.basis_set.axis.coord[2]>.9)top=faces[i];}}
    finally{Free(own,faces);}Require(top!=0,"tool roof face");
    double* poles=stackalloc double[45];for(var u=0;u<5;u++)for(var v=0;v<3;v++){var at=3*(3*u+v);poles[at]=-.1+1.2*u/4;poles[at+1]=-.15+.4*v/2;poles[at+2]=.93+(u==2&&v==1?.6:0);}
    double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{3,3};
    var sf=new PK_BSURF_sf_t{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,
        form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int roof;byte sense=1;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&roof):PK_BSURF_create(&sf,&roof),"NURBS tool roof");Check(own?KernelRuntime.TopologyDetachGeometry(top):PK_TOPOL_detach_geom(top),"detach tool roof");Check(own?KernelRuntime.FaceAttachSurfaces(1,&top,&roof,&sense):PK_FACE_attach_surfs(1,&top,&roof,(PK_LOGICAL_t*)&sense),"attach NURBS tool roof");return body;
}
static unsafe int TiltedCylinder(bool own)
{
    var a=Math.Sqrt(1.13);var r=Math.Sqrt(1.09);var basis=new PK_AXIS2_sf_t{location=new(.2,.25,-.3),axis=new(.3/a,.2/a,1/a),ref_direction=new(1/r,0,-.3/r)};int body;
    Check(own?KernelRuntime.BodyCreateSolidCyl(.12,1.8,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_cyl(.12,1.8,&basis,&body),"tilted cylinder");return body;
}
static unsafe void Translate(bool own,int body,double x,double y,double z)
{
    var form=new M.PK_TRANSF_sf_s();form.matrix[0]=form.matrix[5]=form.matrix[10]=form.matrix[15]=1;form.matrix[3]=x;form.matrix[7]=y;form.matrix[11]=z;int transform;
    Check(own?KernelRuntime.TransfCreate(&form,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&transform),"tool translation");int n;int* replaced;
    if(own){byte* exact;Check(KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,&exact),"self tool translation");Free(true,replaced);Free(true,exact);}
    else{PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"PK tool translation");Free(false,replaced);Free(false,exact);}
    Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete translation");
}
static unsafe byte[] TransmitNative(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&options,&block),"native text XT");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}finally{Check(PK_MEMORY_block_f(&block),"native XT memory");}
}
static unsafe PK_VECTOR_t Center(int body)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* m=stackalloc double[15];Check(PK_TOPOL_eval_mass_props(1,&body,.99999999,&options,m,m+1,m+2,m+5,m+14),"result centroid");return new(m[2],m[3],m[4]);
}
// M1 independent fixture and strict comparison helpers follow.

static unsafe int Roof(bool own)
{
    var body=Block(own,.5,.5,0,1,1,1);int n;int* faces;
    Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input faces");var top=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"face support");PK_PLANE_sf_t plane;
            Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"input plane");
            if(Math.Abs(plane.basis_set.axis.coord[2])>.9&&Math.Abs(plane.basis_set.location.coord[2]-1)<1e-10)top=faces[i];
        }
    }
    finally{Free(own,faces);}
    Require(top!=0,"top plane");double* poles=stackalloc double[45];double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{3,3};
    for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=j/2.0;poles[at+2]=1+(i==2&&j==1?.4:0);}
    var sf=new PK_BSURF_sf_t{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
        u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
        self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int roof;byte sense=1;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&roof):PK_BSURF_create(&sf,&roof),"roof surface");
    Check(own?KernelRuntime.TopologyDetachGeometry(top):PK_TOPOL_detach_geom(top),"detach top support");
    Check(own?KernelRuntime.FaceAttachSurfaces(1,&top,&roof,&sense):PK_FACE_attach_surfs(1,&top,&roof,(PK_LOGICAL_t*)&sense),"attach freeform roof");
    return body;
}

static unsafe int Fillet(bool own,int body,double radius,double selectedY=0,double selectedZ=1)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"input edges");var selected=0;
    int* vertices=stackalloc int[2];PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var e=0;e<n;e++)
        {
            Check(own?KernelRuntime.EdgeAskVertices(edges[e],vertices):PK_EDGE_ask_vertices(edges[e],vertices),"edge vertices");
            for(var v=0;v<2;v++)
            {
                int point;Check(own?KernelRuntime.VertexAskPoint(vertices[v],&point):PK_VERTEX_ask_point(vertices[v],&point),"vertex point");PK_POINT_sf_t sf;
                Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&sf):PK_POINT_ask(point,&sf),"point definition");ends[v]=sf.position;
            }
            if(Math.Abs(ends[0].coord[1]-selectedY)+Math.Abs(ends[1].coord[1]-selectedY)+Math.Abs(ends[0].coord[2]-selectedZ)+Math.Abs(ends[1].coord[2]-selectedZ)<1e-10)
            {Require(selected==0,"unambiguous selected edge");selected=edges[e];}
        }
    }
    finally{Free(own,edges);}
    Require(selected!=0,"selected freeform edge");var options=new PK_EDGE_set_blend_constant_o_t();options.properties.tolerance=1e-7;int count;int* marked;
    Check(own?KernelRuntime.EdgeSetBlendConstant(1,&selected,radius,(M.PK_EDGE_set_blend_constant_o_s*)&options,&count,&marked):PK_EDGE_set_blend_constant(1,&selected,radius,&options,&count,&marked),"set constant blend");
    try{Require(count==1,"one marked edge");}finally{Free(own,marked);}
    // Pin a comparable procedural representation. Modern default fixing fits
    // this case to a B-surface; v261 preserves the dependent rolling Blend.
    var fix=new PK_BODY_fix_blends_o_t{tolerance=1e-7,local_check=PK_LOGICAL_true,update=PK_blend_edge_update_v261_c};int* blends;int fault,fe,ft;
    var started=Stopwatch.GetTimestamp();
    var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&count,&blends,null,null,&fault,&fe,&ft):PK_BODY_fix_blends(body,&fix,&count,&blends,null,null,&fault,&fe,&ft);
    Console.WriteLine($"TIME {(own?"self":"PK")} BODY_fix_blends radius={radius:R} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}, error={error}");
    Console.WriteLine($"{(own?"own":"native")} fix error={error}, count={count}, fault={fault}, edge={fe}, topol={ft}");Check(error,"fix constant blend");
    try
    {
        Require(fault==PK_blend_fault_no_fault_c&&count==1,"one fixed blend without fault");int surface;
        Check(own?KernelRuntime.FaceAskSurf(blends[0],&surface):PK_FACE_ask_surf(blends[0],&surface),"result Blend support");return surface;
    }
    finally{Free(own,blends);}
}

static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t{location=new(x,y,z),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(width,depth,height,&basis,&body),"block");return body;
}

static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"XT buffers");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive");try{Require(n==1,"one received solid");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}

static unsafe void CheckBody(int body,string name)
{
    int type;Check(PK_BODY_ask_type(body,&type),"body type");Require(type==PK_BODY_type_solid_c,name+" solid type");
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full BODY_check");
    try
    {
        for(var i=0;i<n;i++)
        {
            int faultClass=0;var classError=PK_ENTITY_ask_class(faults[i].entity_1,&faultClass);
            Console.WriteLine($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}, class={faultClass}, classError={classError}");
        }
        Require(n==0,name+" body faults="+n);Console.WriteLine($"{name} full BODY_check: 0 faults");
    }
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}

static unsafe void Compare(int reference,int actual,string name)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} own={b}");Require(a==b,name+" topology count "+kind);}
    var nativeRegions=RegionSignature(reference);var ownRegions=RegionSignature(actual);Console.WriteLine($"{name} material regions: native={nativeRegions}, own={ownRegions}");
    Require(nativeRegions==ownRegions,name+" material and shell semantics");
    var options=new PK_DEBUG_BODY_compare_o_t{max_diffs=1024,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,
        class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_true};PK_DEBUG_BODY_compare_r_t result;
    Check(PK_DEBUG_BODY_compare(reference,actual,&options,&result),"structural compare");var structuralOk=false;
    try
    {
        var structural=0;Console.WriteLine($"{name} compare={result.global_result}/{result.local_result}, face_pairs={result.n_face_pairs}");
        for(var i=0;i<result.n_global_diffs;i++)Console.WriteLine($"global diff={result.global_diffs[i].diff}, entities={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
        for(var f=0;f<result.n_face_pairs;f++)for(var i=0;i<result.face_pairs[f].n_local_diffs;i++)
        {
            var d=result.face_pairs[f].local_diffs[i];var chart=d.diff==PK_DEBUG_diff_n_cht_pts_c;
            Console.WriteLine($"{(chart?"CHART":"STRUCTURE")} face={f}, diff={d.diff}, entity={d.master_entity}/{d.similar_entity}, class={d.master_class}/{d.similar_class}, int={d.master_int}/{d.similar_int}, value={d.master_double:R}/{d.similar_double:R}");if(!chart)structural++;
        }
        structuralOk=result.global_result==PK_DEBUG_global_res_no_diffs_c&&structural==0;
    }
    finally{PK_DEBUG_BODY_compare_r_f(&result);}
    var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* aMass=stackalloc double[15];double* bMass=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&massOptions,aMass,aMass+1,aMass+2,aMass+5,aMass+14),"native mass properties");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&massOptions,bMass,bMass+1,bMass+2,bMass+5,bMass+14),"received mass properties");
    var maximum=0.0;
    for(var i=0;i<15;i++)maximum=Math.Max(maximum,Math.Abs(aMass[i]-bMass[i])/(1+Math.Abs(aMass[i])));
    Console.WriteLine($"{name} PK mass: volume={aMass[0]:R}/{bMass[0]:R}, area={aMass[14]:R}/{bMass[14]:R}, max normalized residual={maximum:R}");
    Require(maximum<1e-7,name+" mass properties");Require(structuralOk,name+" structural differences");
}

static unsafe int Count(int body,int kind)
{
    int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
    try{return n;}finally{if(tags!=null)PK_MEMORY_free(tags);}
}
static unsafe string RegionSignature(int body)
{
    int n;int* regions;Check(PK_BODY_ask_regions(body,&n,&regions),"region semantics");
    try
    {
        var records=new string[n];
        for(var i=0;i<n;i++)
        {
            PK_LOGICAL_t solid;int shells;int* tags;Check(PK_REGION_is_solid(regions[i],&solid),"material region flag");
            Check(PK_REGION_ask_shells(regions[i],&shells,&tags),"region shell membership");
            try{records[i]=$"{(solid==PK_LOGICAL_true?1:0)}:{shells}";}finally{if(tags!=null)PK_MEMORY_free(tags);}
        }
        Array.Sort(records,StringComparer.Ordinal);return string.Join(",",records);
    }
    finally{PK_MEMORY_free(regions);}
}
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var sum=0.0;for(var d=0;d<3;d++)sum+=Math.Pow(a.coord[d]-b.coord[d],2);return Math.Sqrt(sum);}
static unsafe void Free(bool own,void* memory){if(memory!=null)Check(own?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"return buffer");}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, blend={KernelRuntime.LastRegularBlendStage}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, parameter={KernelRuntime.LastSheetParameter:R}, algorithm={KernelRuntime.LastSheetAlgorithmStatus}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;
