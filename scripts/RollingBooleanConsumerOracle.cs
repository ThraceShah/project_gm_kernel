#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

unsafe
{
    if(args.Contains("--check-result-gate"))
    {
        RequireFixtureResult(PK_boolean_result_success_c,1,true,"positive");
        foreach(var sample in new[]{(PK_boolean_result_failed_c,1,true),(PK_boolean_result_success_c,0,false),
            (PK_boolean_result_success_c,0,true),(PK_boolean_result_success_c,1,false),(PK_boolean_result_success_c,2,true)})
        {
            var rejected=false;
            try{RequireFixtureResult(sample.Item1,sample.Item2,sample.Item3,"negative");}
            catch(InvalidOperationException){rejected=true;}
            Require(rejected,"result gate accepted an invalid fixture output");
            Console.WriteLine($"RESULT GATE status={sample.Item1} count={sample.Item2} has_bodies={sample.Item3} rejected={rejected}");
        }
        Console.WriteLine("RESULT GATE positive and five negatives pass; no modeling API executed");return;
    }
    var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,".."));
    var output=Path.Combine(root,"temp_docs","contract-sol",args.FirstOrDefault(a=>!a.StartsWith("--"))??"consumer");
    Require(!Directory.Exists(output),"Use a new output name; existing evidence is immutable");
    Directory.CreateDirectory(output);
    if(!ParasolidScriptHost.TryStartSession("rolling Boolean consumer",out var host,out var reason))throw new Exception(reason);
    using(host)
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");
        KernelRuntime.ForbidLegacyRegularBlendAssembly=true;
        try
        {
            var target=ReceiveManaged(File.ReadAllBytes(Path.Combine(root,"tests","KernelTests","Fixtures","generated-short-edge-input.x_t")));
            Span<int> selected=stackalloc int[2];Selection(target,selected);
            if(args.Contains("--reverse"))selected.Reverse();
            var mark=new M.PK_EDGE_set_blend_constant_o_s{o_t_version=2};mark.properties.tolerance=1e-7;int count;
            fixed(int* edges=selected)Check(KernelRuntime.EdgeSetBlendConstant(2,edges,.5,&mark,&count,null),"GM mark");
            var fix=new M.PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,edgeFault,topolFault;
            KernelRuntime.TraceFiniteBoundary=t=>
            {
                if(t.Stage==KernelRuntime.BoundaryTraceStage.CertifiedRoot)
                    Console.WriteLine($"ROOT event={t.StartEvent} curve={t.Curve} cut={t.ProductCurve} source=[{t.First:R},{t.Last:R}] error={t.SpatialError:R} witness={t.Witness}");
            };
            var error=KernelRuntime.BodyFixBlends(target,&fix,&count,null,null,null,&fault,&edgeFault,&topolFault);
            Console.WriteLine($"BLEND error={error} count={count} stage={KernelRuntime.LastRollingNetworkStage}");Check(error,"GM fix");
            var q=1/Math.Sqrt(2);var center=(-7-3.8e-5)*q-.075;
            var oblique=new M.PK_AXIS2_sf_s();oblique.location.coord[0]=-1.5;
            oblique.location.coord[1]=(1+center)*q;oblique.location.coord[2]=(1-center)*q;
            oblique.axis.coord[1]=oblique.axis.coord[2]=q;oblique.ref_direction.coord[1]=q;oblique.ref_direction.coord[2]=-q;
            var meridian=new M.PK_AXIS2_sf_s();meridian.location.coord[0]=-1.55;meridian.location.coord[1]=-2.03;meridian.location.coord[2]=4.8;
            meridian.axis.coord[2]=meridian.ref_direction.coord[0]=1;
            int* tools=stackalloc int[2];
            Check(KernelRuntime.BodyCreateSolidBlock(.15,.4,2.5,&oblique,tools),"GM oblique tool");
            Check(KernelRuntime.BodyCreateSolidBlock(.10,.16,1.3,&meridian,tools+1),"GM meridian tool");
            var inputs=new[]{target,tools[0],tools[1]};var before=inputs.Select(TransmitManaged).ToArray();
            var native=new int[3];
            for(var i=0;i<3;i++)
            {
                var name=i==0?"target":$"tool-{i}";File.WriteAllBytes(Path.Combine(output,name+".x_t"),before[i]);
                Console.WriteLine($"INPUT {name} sha256={Convert.ToHexStringLower(SHA256.HashData(before[i]))}");
                native[i]=ReceiveNative(before[i]);FullCheck(native[i],name+" native receive");
            }
            var reference=args.FirstOrDefault(a=>a.StartsWith("--reference-target="));
            if(reference!=null)
            {
                var bytes=File.ReadAllBytes(Path.Combine(root,"temp_docs","contract-sol",reference["--reference-target=".Length..],"target.x_t"));
                Console.WriteLine($"REFERENCE TARGET sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
                var accepted=ReceiveNative(bytes);FullCheck(accepted,"reference target");
                ParasolidBodyCompare.AssertEquivalent(accepted,native[0],"reference versus current uncut target");Mass(accepted,native[0]);
            }
            if(args.Contains("--blend-only")){Console.WriteLine("BLEND-ONLY diagnostic complete; Boolean not executed");return;}
            var nativeOptions=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c};
            var options=new M.PK_BODY_boolean_o_s();
            Require(sizeof(M.PK_BODY_boolean_o_s)==sizeof(PK_BODY_boolean_o_t),"generated options layout size");
            Buffer.MemoryCopy(&nativeOptions,&options,sizeof(M.PK_BODY_boolean_o_s),sizeof(PK_BODY_boolean_o_t));
            var optionBytes=new ReadOnlySpan<byte>(&options,sizeof(M.PK_BODY_boolean_o_s)).ToArray();
            File.WriteAllBytes(Path.Combine(output,"boolean-options.bin"),optionBytes);
            using(var file=File.Create(Path.Combine(output,"inputs.json")))
            using(var json=new Utf8JsonWriter(file,new(){Indented=true}))
            {
                json.WriteStartObject();json.WriteString("operation","PK_BODY_boolean_2");json.WriteString("function","subtract");
                json.WriteNumber("optionsVersion",options.o_t_version);json.WriteNumber("defaultTolerance",options.default_tol);
                json.WriteString("optionsSha256",Convert.ToHexStringLower(SHA256.HashData(optionBytes)));
                json.WriteStartArray("inputs");
                for(var i=0;i<before.Length;i++)
                {json.WriteStartObject();json.WriteString("file",i==0?"target.x_t":$"tool-{i}.x_t");json.WriteString("sha256",Convert.ToHexStringLower(SHA256.HashData(before[i])));json.WriteEndObject();}
                json.WriteEndArray();json.WriteEndObject();
            }
            Console.WriteLine($"OPTIONS version={options.o_t_version} function={options.function} default_tol={options.default_tol:R}");
            var entered=false;KernelRuntime.ForbidSpatialBoundaryReuse=true;
            KernelRuntime.TraceFiniteBoundary=t=>
            {
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.CellSelection)
                    Console.WriteLine($"BOOLEAN SELECT face={t.Face} source={t.Source} surface={t.Surface} keep={t.Selected} reverse={t.Reversed} material={t.MaterialInside}/{t.MaterialOutside} uv=({t.UvStart.param[0]:R},{t.UvStart.param[1]:R}) xyz=({t.StartPoint.X:R},{t.StartPoint.Y:R},{t.StartPoint.Z:R})");
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.ComponentCellMap)
                    Console.WriteLine($"BOOLEAN MAP old={t.PreviousFace} merged={t.Face} source={t.Source} source_generation={KernelRuntime.Faces[t.Source].Header.Generation} surface={t.Surface}");
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.CutRootLocation)
                    Console.WriteLine($"BOOLEAN ROOT source_curve={t.Curve} target_curve={t.ProductCurve} supports={t.Surface}/{t.OtherSurface} station={t.Station:R} box=[{t.First:R},{t.Last:R}] decision={(t.Selected!=0?"Accepted":t.ClipDecision.ToString())} witness={t.Witness} xyz=({t.StartPoint.X:R},{t.StartPoint.Y:R},{t.StartPoint.Z:R})");
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.SourceFiniteRoot)
                    Console.WriteLine($"BOOLEAN SOURCE ROOT curve={t.Curve}/{t.Generation} target={t.ProductCurve} occurrence={t.Edge}/{t.InputCoedge} supports={t.Surface}/{t.OtherSurface} certificate={t.Certificate} witness={t.Witness} native={t.Station:R}/[{t.First:R},{t.Last:R}] rolling=[{t.RollingStation.Lower:R},{t.RollingStation.Upper:R}] error={t.SpatialError:R} xyz=({t.StartPoint.X:R},{t.StartPoint.Y:R},{t.StartPoint.Z:R})");
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.CutIntervalDecision)
                    Console.WriteLine($"BOOLEAN CLIP curve={t.Curve}/{t.Generation} supports={t.Surface}/{t.OtherSurface} native=[{t.First:R},{t.Last:R}] witness={t.Station:R} decision={t.ClipDecision} xyz=({t.StartPoint.X:R},{t.StartPoint.Y:R},{t.StartPoint.Z:R})");
                if(args.Contains("--trace")&&t.Stage==KernelRuntime.BoundaryTraceStage.Input)
                {
                    var sourceGeneration=t.Source>=0&&KernelRuntime.Faces.IsAlive(t.Source)?KernelRuntime.Faces[t.Source].Header.Generation:0;
                    Console.WriteLine($"BOOLEAN INPUT POINT curve={t.Curve}/{t.Generation} surface={t.Surface}/{t.SurfaceGeneration} source={t.Source}/{sourceGeneration} events={t.StartEvent}/{t.EndEvent} native=[{t.First:R},{t.Last:R}] xyz=({t.StartPoint.X:R},{t.StartPoint.Y:R},{t.StartPoint.Z:R})->({t.EndPoint.X:R},{t.EndPoint.Y:R},{t.EndPoint.Z:R})");
                    var carrier=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(t.Curve)];
                    if(carrier.Class==CurveClass.ICurve)
                    {var data=KernelRuntime.ICurveDataPool[carrier.DataIndex];Console.WriteLine($"BOOLEAN INPUT SUPPORTS curve={t.Curve} supports={data.Surface0Tag}/{data.Surface1Tag} sense={carrier.Sense}");}
                }
                if(args.Contains("--trace")&&t.Stage is KernelRuntime.BoundaryTraceStage.Input or KernelRuntime.BoundaryTraceStage.Arrangement or KernelRuntime.BoundaryTraceStage.Successor or KernelRuntime.BoundaryTraceStage.ComponentBoundary)
                    Console.WriteLine($"BOOLEAN {t.Stage} surface={t.Surface} source={t.Source} face={t.Face} edge={t.Edge} curve={t.Curve} events={t.StartEvent}/{t.EndEvent} forward={t.Forward} next={t.NextEdge} angles={t.StartAngle:R}/{t.EndAngle:R}");
                if(t.Stage!=KernelRuntime.BoundaryTraceStage.ProjectedCellReplacement)return;
                entered=true;Console.WriteLine("PROJECTED replacement entered");
                if(args.Contains("--inject"))KernelRuntime.InjectAllocationFailure(0);
            };
            M.PK_TOPOL_track_r_s tracking=default;M.PK_boolean_r_s result=default;
            error=KernelRuntime.BodyBoolean2(target,2,tools,&options,&tracking,&result);
            KernelRuntime.InjectAllocationFailure(-1);
            Console.WriteLine($"GM COMPONENT phase={KernelRuntime.LastBooleanComponentPhase} face={KernelRuntime.LastBooleanComponentFace} edge={KernelRuntime.LastBooleanComponentEdge}");
            Console.WriteLine($"GM BOOLEAN error={error} result={result.result} bodies={result.n_bodies} entered={entered} stage={KernelRuntime.LastRegularBooleanStage} faces={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} parameter={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} root_failure={SurfaceIsoPlaneRoots.LastFailure} root_box=[{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Lower:R},{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Upper:R}]");
            if(error==0)RequireFixtureResult(result.result,result.n_bodies,result.bodies!=null,"GM two-tool subtraction");
            var own=error==0?new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray():[];
            Check(KernelRuntime.TopolTrackResultFree(&tracking),"GM tracking free");Check(KernelRuntime.BooleanResultFree(&result),"GM result free");
            if(error!=0)for(var i=0;i<inputs.Length;i++)Require(before[i].AsSpan().SequenceEqual(TransmitManaged(inputs[i])),"GM rollback input "+i);
            PK_TOPOL_track_r_t nativeTracking=default;PK_boolean_r_t nativeResult=default;
            fixed(int* nativeInputs=native)
            {
                var nativeError=PK_BODY_boolean_2(nativeInputs[0],2,nativeInputs+1,&nativeOptions,&nativeTracking,&nativeResult);
                Console.WriteLine($"PK SAME INPUT BOOLEAN error={nativeError} result={nativeResult.result} bodies={nativeResult.n_bodies}");
                try
                {
                    if(nativeError==0)RequireFixtureResult(nativeResult.result,nativeResult.n_bodies,nativeResult.bodies!=null,"PK two-tool subtraction");
                    if(nativeError==0)for(var i=0;i<nativeResult.n_bodies;i++)
                    {
                        FullCheck(nativeResult.bodies[i],"PK output");File.WriteAllBytes(Path.Combine(output,$"pk-output-{i}.x_t"),TransmitNative(nativeResult.bodies[i]));
                    }
                    if(error==0&&nativeError==0)
                    {
                        Require(own.Length==nativeResult.n_bodies,"output body count");
                        for(var i=0;i<own.Length;i++)
                        {
                            var bytes=TransmitManaged(own[i]);File.WriteAllBytes(Path.Combine(output,$"gm-output-{i}.x_t"),bytes);
                            var received=ReceiveNative(bytes);FullCheck(received,"GM output received by PK");
                            ParasolidBodyCompare.AssertEquivalent(nativeResult.bodies[i],received,"same-input Boolean");Mass(nativeResult.bodies[i],received);
                        }
                    }
                    Require(nativeError==0,"PK Boolean failed");
                }
                finally{Check(PK_TOPOL_track_r_f(&nativeTracking),"PK tracking free");Check(PK_boolean_r_f(&nativeResult),"PK result free");}
            }
            Require(args.Contains("--inject")?entered&&error==M.ParasolidConstants.PK_ERROR_memory_full:error==0,"GM Boolean contract incomplete");
        }
        finally
        {
            KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.TraceFiniteBoundary=null;KernelRuntime.ForbidSpatialBoundaryReuse=false;
            KernelRuntime.ForbidLegacyRegularBlendAssembly=false;KernelRuntime.SessionStop();
        }
    }
}

static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string label){if(error!=0)throw new Exception(label+": "+error);}
static void Require(bool pass,string label){if(!pass)throw new Exception(label);}
static void RequireFixtureResult(int status,int count,bool hasBodies,string label)
{
    if(status!=PK_boolean_result_success_c||count!=1||!hasBodies)
        throw new InvalidOperationException($"{label}: expected PK_boolean_result_success_c and one nonempty result body, got status={status} count={count} has_bodies={hasBodies}");
}

static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c};
        int count;int* parts;Check(KernelRuntime.PartReceiveB(block,&options,&count,&parts),"GM receive");
        try{Require(count==1,"GM receive count");return parts[0];}finally{KernelRuntime.MemoryFree(parts);}
    }
}
static unsafe byte[] TransmitManaged(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,
        transmit_user_fields=1,transmit_meshes=M.ParasolidConstants.PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"GM transmit");
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return bytes.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"GM transmit free");}
}
static unsafe int ReceiveNative(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};
        int count;int* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"PK receive");
        try{Require(count==1,"PK receive count");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}
static unsafe byte[] TransmitNative(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"PK transmit");
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return bytes.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"PK transmit free");}
}
static unsafe void FullCheck(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;
    Check(PK_BODY_check(body,&options,&count,&faults),label+" full check");
    try{Console.WriteLine($"FULL {label} faults={count}");for(var i=0;i<count;i++)Console.WriteLine($"FAULT {faults[i].state} {faults[i].entity_1}/{faults[i].entity_2}");Require(count==0,label+" faults");}
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static unsafe void Mass(int reference,int actual)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&options,a,a+1,a+2,a+5,a+14),"PK mass");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&options,b,b+1,b+2,b+5,b+14),"GM mass");
    var worst=0d;for(var i=0;i<15;i++)worst=Math.Max(worst,Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i])));
    Console.WriteLine($"MASS normalized15={worst:R}");Require(worst<1e-7,"mass gate");
}
static void Selection(int body,Span<int> selected)
{
    var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var junction=-1;selected.Clear();
    for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
    {
        var record=KernelRuntime.Edges[edge];if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Ellipse||!SupportPair(edge,SurfaceClass.Cylinder))continue;
        Require(selected[0]==0,"unique ellipse");selected[0]=record.Header.Tag;
        foreach(var vertex in new[]{record.StartVertex,record.EndVertex})
        {
            var degree=0;var incident=KernelRuntime.Bodies[slot].FirstEdgeBody;
            for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,incident=KernelRuntime.Edges[incident].NextInBody)
            {var candidate=KernelRuntime.Edges[incident];if(candidate.StartVertex==vertex||candidate.EndVertex==vertex)degree++;}
            if(degree==4)junction=vertex;
        }
    }
    Require(junction>=0,"source junction");edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
    for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
    {
        var record=KernelRuntime.Edges[edge];if(record.StartVertex!=junction&&record.EndVertex!=junction)continue;
        if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Line||!SupportPair(edge,SurfaceClass.Plane))continue;
        Require(selected[1]==0,"unique line");selected[1]=record.Header.Tag;
    }
    Require(selected[0]!=0&&selected[1]!=0,"selection");
}
static bool SupportPair(int edge,SurfaceClass type)
{
    var fin=KernelRuntime.Edges[edge].FirstFinEdge;var other=KernelRuntime.Fins[fin].Other;
    var a=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;var b=KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face;
    return KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[a].SurfTag)].Class==type
        &&KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[b].SurfTag)].Class==type;
}
