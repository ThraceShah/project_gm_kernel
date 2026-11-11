# Review-only reproducer sources — fixed 9b2f4c6

These scripts are saved in ignored `temp_docs/r3-frame-review/`; relative paths resolve from each script. They do not change production/test sources. `--restore-vertex` is a deliberate temporary-model input intervention, not an implementation repair. `NativeCheck.cs` uses the independent pure-native product preserved by review commit `0a33258db4436c318982220c13b9af432781bc4f`, whose native-control source and product hash are documented there.

## temp_docs/r3-frame-review/Directory.Build.props

```xml
<Project><Import Project="../../scripts/Directory.Build.props" /></Project>
```

## temp_docs/r3-frame-review/FrameProbe.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property KernelTestHooks=true
#:property AllowUnsafeBlocks=true
global using EdgeTag=System.Int32;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Computation;
using System.Text;
using System.Runtime.CompilerServices;
var here=Path.GetDirectoryName(ScriptPath())!;
Environment.SetEnvironmentVariable("PARASOLID_SCHEMA_DIR",Path.GetFullPath(Path.Combine(here,"..","..","third_party","parasolid","schema")));
unsafe
{
    KernelRuntime.ForbidLegacyRegularBlendAssembly=true;
    var start=new PK_SESSION_start_o_s{o_t_version=1};C(KernelRuntime.SessionStart(&start));
    try
    {
        var bytes=File.ReadAllBytes(Path.Combine(here,"..","..","tests","KernelTests","Fixtures","generated-short-edge-input.x_t"));int target;
        fixed(byte* p=bytes)
        {
            var block=new PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var receive=new PK_PART_receive_o_s{o_t_version=1,transmit_format=ParasolidConstants.PK_transmit_format_text_c};int n;int* parts;
            C(KernelRuntime.PartReceiveB(block,&receive,&n,&parts));target=parts[0];C(KernelRuntime.MemoryFree(parts));
        }
        Span<EdgeTag> selected=stackalloc EdgeTag[2];Selection(target,selected);if(args.Contains("--reverse"))selected.Reverse();
        var mark=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};mark.properties.tolerance=1e-7;int count;
        fixed(EdgeTag* edges=selected)C(KernelRuntime.EdgeSetBlendConstant(2,edges,.5,&mark,&count,null));
        var observed=0;
        if(args.Contains("--trace-fix"))KernelRuntime.TraceFiniteBoundary=t=>
        {
            if(observed>=8||!Near(t.StartPoint)&&!Near(t.EndPoint))return;
            observed++;Console.WriteLine($"FIX_POINT order={observed} stage={t.Stage} curve={t.Curve} sourceEdge={t.SourceEdge} source={t.Source} event={t.StartEvent}/{t.EndEvent} t=[{t.First:R},{t.Last:R}] p={V(t.StartPoint)} q={V(t.EndPoint)}");
            if(t.Stage==KernelRuntime.BoundaryTraceStage.Input)
            {
                PK_VECTOR_s a,b;C(KernelRuntime.CurveEval(t.Curve,t.First,0,&a));C(KernelRuntime.CurveEval(t.Curve,t.Last,0,&b));
                Console.WriteLine($"FIX_CARRIER curve={t.Curve} class={KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(t.Curve)].Class} first=({a.coord[0]:R},{a.coord[1]:R},{a.coord[2]:R}) last=({b.coord[0]:R},{b.coord[1]:R},{b.coord[2]:R})");
            }
        };
        KernelRuntime.TraceRollingNetwork=t=>
        {
            if(t.Stage!=KernelRuntime.RollingNetworkStage.Bands)return;
            var carrier=KernelRuntime.Edges[t.Source];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(carrier.CurveTag)];
            if(curve.Class!=CurveClass.Ellipse)return;
            var old=KernelRuntime.EllipseDataPool[curve.DataIndex];
            var s=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(t.Surface)];var bd=KernelRuntime.BlendedEdgeDataPool[s.DataIndex];var p=KernelRuntime.GetNestedBlendProgram(bd);var root=p.Surfaces[bd.ProgramRoot];var spine=p.Spines[root.Spine];
            var oldX=new KernelVector3{X=old.RefDirX,Y=old.RefDirY,Z=old.RefDirZ};var oldZ=new KernelVector3{X=old.AxisX,Y=old.AxisY,Z=old.AxisZ};
            var first=spine.Curve.First;var newZ=EvaluationMath.Cross(spine.Curve.First,spine.Curve.Second);
            var alpha=Math.Sign(EvaluationMath.Dot(oldZ,newZ));var sigma=Math.Sign(EvaluationMath.Dot(oldX,first));
            Console.WriteLine($"FRAME source={carrier.CurveTag} sourceRange=[{carrier.ParameterStart:R},{carrier.ParameterEnd:R}] oldAxis={V(oldZ)} oldRef={V(oldX)} newFirst={V(spine.Curve.First)} newSecond={V(spine.Curve.Second)} alpha={alpha} sigma={sigma} bandRange=[{t.First:R},{t.Last:R}]");
            SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
            for(var side=0;side<2;side++)
            {
                var status=JointNestedBlendEvaluation.Evaluate(p,bd.ProgramRoot,Math.PI,side,layout,point);
                Console.WriteLine($"QUADRANT_FLOAT u=pi v={side} status={status} p={V(point[0])} algebraic_center_pi={V(EvaluationMath.Sub(spine.Curve.Origin,spine.Curve.First))}");
            }
            foreach(var u in new[]{t.First,t.Last,0.0,Math.PI})
            {
                var proper=alpha*u+(sigma<0?Math.PI:0);PK_VECTOR_s raw,mapped;
                C(KernelRuntime.CurveEval(carrier.CurveTag,u,0,&raw));C(KernelRuntime.CurveEval(carrier.CurveTag,proper,0,&mapped));
                Console.WriteLine($"PARAM u={u:R} expected_carrier={proper:R} raw=({raw.coord[0]:R},{raw.coord[1]:R},{raw.coord[2]:R}) mapped=({mapped.coord[0]:R},{mapped.coord[1]:R},{mapped.coord[2]:R})");
            }
        };
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,ef,tf;
        C(KernelRuntime.BodyFixBlends(target,&fix,&count,null,null,null,&fault,&ef,&tf));
        Console.WriteLine($"FIX target={target} count={count} reverse={args.Contains("--reverse")}");
        var slot=KernelRuntime.ResolveTagRecord(target).Slot;var body=KernelRuntime.Bodies[slot];
        var face=body.FirstFaceBody;
        for(var i=0;i<body.FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)];
            if(surface.Class!=SurfaceClass.BlendSurface)continue;
            var data=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];var spine=program.Spines[root.Spine];
            Console.WriteLine($"BLEND face={face} tag={surface.Header.Tag} domain=[{surface.UMin:R},{surface.UMax:R}] sense={spine.Sense} reverseSection={root.ReverseSection} first={V(spine.Curve.First)} second={V(spine.Curve.Second)} origin={V(spine.Curve.Origin)}");
        }
        var vertex=body.FirstVertexBody;var nearest=-1;var distance=double.PositiveInfinity;
        for(var i=0;i<body.VertexCountBody;i++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
        {
            var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position;
            var d=Math.Sqrt(Math.Pow(point.X+1.5,2)+Math.Pow(point.Y+2,2)+Math.Pow(point.Z-5,2));
            if(d<distance){distance=d;nearest=vertex;}
        }
        var pointSlot=KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[nearest].PointTag);
        Console.WriteLine($"NEAREST vertex={nearest} distance={distance:R} p={V(KernelRuntime.Points[pointSlot].Position)}");
        if(args.Contains("--restore-vertex"))
        {
            // Diagnostic model-input intervention only; no source or test edit.
            KernelRuntime.Points[pointSlot].Position=new(){X=-1.5,Y=-2,Z=5};
            Console.WriteLine("DIAGNOSTIC_INPUT_INTERVENTION nearest vertex reset to original exact stationary point; not a proposed fix");
        }
        var plane=new PK_PLANE_sf_s();plane.basis_set.location.coord[0]=-1.5;plane.basis_set.axis.coord[0]=plane.basis_set.ref_direction.coord[1]=1;int cutting;C(KernelRuntime.PlaneCreate(&plane,&cutting));
        var edge=body.FirstEdgeBody;
        for(var i=0;i<body.EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(record.StartVertex!=nearest&&record.EndVertex!=nearest)continue;
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)];if(curve.Class!=CurveClass.ICurve)continue;
            var code=KernelRuntime.TestNativePlaneEdge(edge,cutting,out var roots,out var exactVertex,out var station,out var transverse);
            Console.WriteLine($"STATIONARY edge={edge} curve={record.CurveTag} range=[{record.ParameterStart:R},{record.ParameterEnd:R}] code={code} roots={roots} vertex={exactVertex} station=[{station.Lower:R},{station.Upper:R}] transverse={transverse} iso={SurfaceIsoPlaneRoots.LastFailure} box=[{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Lower:R},{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Upper:R}]");
        }
        C(KernelRuntime.EntityDelete(1,&cutting));
        var probe=new PK_VECTOR_s();probe.coord[0]=-1.499999667811987;probe.coord[1]=-2.500000004201637;probe.coord[2]=4.50007614662344;int enclosure,topology;
        var probeError=KernelRuntime.BodyContainsVector(target,probe,&enclosure,&topology);Console.WriteLine($"ROW289 error={probeError} enclosure={enclosure} topology={topology} stage={KernelRuntime.LastRegularClassificationStage}");
        File.WriteAllText(Path.Combine(here,args.Contains("--restore-vertex")?"intervened-target.x_t":"candidate-target.x_t"),Transmit(target));
        if(args.Contains("--no-boolean"))return 0;
        var q=1/Math.Sqrt(2);var center=(-7-3.8e-5)*q-.075;
        var oblique=new PK_AXIS2_sf_s();oblique.location.coord[0]=-1.5;oblique.location.coord[1]=(1+center)*q;oblique.location.coord[2]=(1-center)*q;oblique.axis.coord[1]=oblique.axis.coord[2]=q;oblique.ref_direction.coord[1]=q;oblique.ref_direction.coord[2]=-q;
        var meridian=new PK_AXIS2_sf_s();meridian.location.coord[0]=-1.55;meridian.location.coord[1]=-2.03;meridian.location.coord[2]=4.8;meridian.axis.coord[2]=meridian.ref_direction.coord[0]=1;
        int* tools=stackalloc int[2];C(KernelRuntime.BodyCreateSolidBlock(.15,.4,2.5,&oblique,tools));C(KernelRuntime.BodyCreateSolidBlock(.10,.16,1.3,&meridian,tools+1));
        var inputs=new[]{target,tools[0],tools[1]};var before=inputs.Select(Transmit).ToArray();var entered=false;
        KernelRuntime.ForbidSpatialBoundaryReuse=true;
        KernelRuntime.TraceFiniteBoundary=t=>
        {
            if(t.Stage is KernelRuntime.BoundaryTraceStage.Input or KernelRuntime.BoundaryTraceStage.Successor or KernelRuntime.BoundaryTraceStage.Arrangement or KernelRuntime.BoundaryTraceStage.Product)return;
            Console.WriteLine($"TRACE {t.Stage} curve={t.Curve} source={t.Source} face={t.Face} support={t.Surface}/{t.OtherSurface} t=[{t.First:R},{t.Last:R}] event={t.StartEvent}/{t.EndEvent} witness={t.Witness}");
            if(t.Stage==KernelRuntime.BoundaryTraceStage.ProjectedCellReplacement){entered=true;if(args.Contains("--inject"))KernelRuntime.InjectAllocationFailure(0);}
        };
        var options=Options(ParasolidConstants.PK_boolean_subtract_c);PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        var error=KernelRuntime.BodyBoolean2(target,2,tools,&options,&tracking,&result);KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.TraceFiniteBoundary=null;
        Console.WriteLine($"BOOLEAN error={error} projectedEntered={entered} stage={KernelRuntime.LastRegularBooleanStage} face={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} param={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R}) SSI={KernelRuntime.LastGeneralIntersectionStage}/{KernelRuntime.LastGeneralIntersectionStatus} iso={SurfaceIsoPlaneRoots.LastFailure} box=[{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Lower:R},{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Upper:R}]");
        if(error!=0)for(var i=0;i<inputs.Length;i++)Console.WriteLine($"ROLLBACK index={i} unchanged={before[i]==Transmit(inputs[i])}");
        return 0;
    }
    finally{KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.TraceRollingNetwork=null;KernelRuntime.TraceFiniteBoundary=null;KernelRuntime.ForbidSpatialBoundaryReuse=false;KernelRuntime.ForbidLegacyRegularBlendAssembly=false;KernelRuntime.SessionStop();}
}
static unsafe string Transmit(int body)
{
    var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_user_fields=1,transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};PK_MEMORY_block_s block;C(KernelRuntime.PartTransmitB(1,&body,&options,&block));
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return Encoding.ASCII.GetString(stream.ToArray());}finally{C(KernelRuntime.MemoryBlockFree(&block));}
}
static string V(KernelVector3 p)=>$"({p.X:R},{p.Y:R},{p.Z:R})";
static bool Near(KernelVector3 p)=>Math.Abs(p.X+1.5)<1e-6&&Math.Abs(p.Y+2)<1e-6&&Math.Abs(p.Z-5)<1e-6;
static void C(int error){if(error!=0)throw new Exception("API "+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;
    static void Selection(int body,Span<EdgeTag> selected)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var junction=-1;selected.Clear();
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Ellipse)continue;
            if(!SupportPair(edge,SurfaceClass.Cylinder))continue;Verify.Equal(0,selected[0]);selected[0]=record.Header.Tag;
            foreach(var vertex in new[]{record.StartVertex,record.EndVertex})
            {
                var degree=0;var incident=KernelRuntime.Bodies[slot].FirstEdgeBody;
                for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,incident=KernelRuntime.Edges[incident].NextInBody)
                {var candidate=KernelRuntime.Edges[incident];if(candidate.StartVertex==vertex||candidate.EndVertex==vertex)degree++;}
                if(degree==4)junction=vertex;
            }
        }
        Verify.NotEqual(-1,junction);edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(record.StartVertex!=junction&&record.EndVertex!=junction)continue;
            if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Line||!SupportPair(edge,SurfaceClass.Plane))continue;
            Verify.Equal(0,selected[1]);selected[1]=record.Header.Tag;
        }
        Verify.True(selected[0]!=0&&selected[1]!=0);
    }
    static bool SupportPair(int edge,SurfaceClass type)
    {
        var fin=KernelRuntime.Edges[edge].FirstFinEdge;var other=KernelRuntime.Fins[fin].Other;
        var a=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;var b=KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face;
        return KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[a].SurfTag)].Class==type
            &&KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[b].SurfTag)].Class==type;
    }

    static PK_BODY_boolean_o_s Options(int operation) => new()
    {
        o_t_version = 19, function = operation, default_tol = 1e-5,
        fence = ParasolidConstants.PK_boolean_fence_none_c, check_fa = ParasolidConstants.PK_boolean_check_fa_yes_c,
        imprint_complete_targ = ParasolidConstants.PK_imprint_complete_no_c, imprint_complete_tool = ParasolidConstants.PK_imprint_complete_no_c,
        target_material_side = ParasolidConstants.PK_boolean_material_default_c, tool_material_side = ParasolidConstants.PK_boolean_material_default_c,
        resulting_body_type = ParasolidConstants.PK_boolean_prefer_original_c, flag_no_effect = ParasolidConstants.PK_boolean_no_effect_basic_c,
        nm_edge_repair = ParasolidConstants.PK_nm_edge_repair_no_c, blend_radius = 1e-5,
        tracking_type = ParasolidConstants.PK_boolean_track_type_basic_c, track_regions = ParasolidConstants.PK_track_regions_no_c,
        update = ParasolidConstants.PK_boolean_update_default_c, merge_in_solid_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c,
        merge_in_face_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c, impose_bodies = ParasolidConstants.PK_boolean_impose_no_c,
        merge_tools = ParasolidConstants.PK_merge_tools_no_c,
    };

static class Verify
{
    public static void Equal<T>(T a,T b){if(!EqualityComparer<T>.Default.Equals(a,b))throw new Exception($"{a}!={b}");}
    public static void NotEqual<T>(T a,T b){if(EqualityComparer<T>.Default.Equals(a,b))throw new Exception($"{a}=={b}");}
    public static void True(bool condition){if(!condition)throw new Exception("precondition");}
}
```

## temp_docs/r3-frame-review/NativeCheck.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
var here=Path.GetDirectoryName(ScriptPath())!;
if(!ParasolidScriptHost.TryStartSession("R3 fixed frame native check",out var session,out var skip))throw new Exception(skip);
using(session)unsafe
{
    var expected=Load(Path.Combine(here,"..","opus-r2-r4-review","native-product.x_t"));
    var actual=Load(Path.Combine(here,"candidate-target.x_t"));
    ParasolidBodyCompare.AssertEquivalent(expected,actual,"fixed R3 uncut native check");
    Console.WriteLine("STRICT full/global/local gate passed; only chart-point diagnostic allowed");
}
return 0;
static unsafe int Load(string path)
{
    var bytes=File.ReadAllBytes(path);int body;
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        C(PK_PART_receive_b(block,&options,&n,&parts));if(n!=1)throw new Exception("parts");body=parts[0];C(PK_MEMORY_free(parts));
    }
    var check=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;C(PK_BODY_check(body,&check,&count,&faults));
    Console.WriteLine($"BODY {Path.GetFileName(path)} SHA256={Convert.ToHexStringLower(SHA256.HashData(bytes))} full_faults={count}");
    if(count!=0)throw new Exception("body faults");if(faults!=null)C(PK_MEMORY_free(faults));return body;
}
static void C(int error){if(error!=0)throw new Exception("PK "+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;
```
