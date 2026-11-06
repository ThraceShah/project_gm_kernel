using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool SameSheetSupport(SurfTag first, SurfTag second)
    {
        if(first==second)return true;
        var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);if(a<0||b<0||Surfaces[a].Class!=Surfaces[b].Class)return false;
        if(Surfaces[a].Class==SurfaceClass.BlendSurface)
        {
            var x=BlendedEdgeDataPool[Surfaces[a].DataIndex];var y=BlendedEdgeDataPool[Surfaces[b].DataIndex];
            var xp=GetNestedBlendProgram(x);var yp=GetNestedBlendProgram(y);
            return x.ProgramRoot==y.ProgramRoot
                &&System.Runtime.InteropServices.MemoryMarshal.AsBytes(xp.Surfaces).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(yp.Surfaces))
                &&System.Runtime.InteropServices.MemoryMarshal.AsBytes(xp.Spines).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(yp.Spines))
                &&System.Runtime.InteropServices.MemoryMarshal.AsBytes(xp.Charts).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(yp.Charts))
                &&xp.Scalars.SequenceEqual(yp.Scalars);
        }
        if(Surfaces[a].Class!=SurfaceClass.BSurface)return false;
        var av=GetBSurfaceView(BSurfaceDataStore[Surfaces[a].DataIndex]);var bv=GetBSurfaceView(BSurfaceDataStore[Surfaces[b].DataIndex]);
        return av.UDegree==bv.UDegree&&av.VDegree==bv.VDegree&&av.Dimension==bv.Dimension&&av.Rational==bv.Rational
            &&av.UKnots.SequenceEqual(bv.UKnots)&&av.VKnots.SequenceEqual(bv.VKnots)&&av.Vertices.SequenceEqual(bv.Vertices);
    }

    private static int SheetCurveUV(SurfTag surface, CurveTag curve, double parameter, bool forward,
        out PK_UV_s uv,out PK_UV_s tangent,out KernelVector3 point)
    {
        uv=tangent=default;point=default;
        var slot=GetCurveSlotByTag(curve);if(slot<0)return ParasolidConstants.PK_ERROR_missing_geom;
        LastSheetPartitionStage=SheetPartitionStage.CurveEvaluation;
        LastSheetCurve=curve;LastSheetParameter=parameter;
        var contact=Curves[slot].Class==CurveClass.ICurve?ICurveDataPool[Curves[slot].DataIndex]:default;
        var splineContact=contact.IsBlendContact!=0&&Surfaces[GetSurfaceSlotByTag(surface)].Class==SurfaceClass.BSurface&&SameSheetSupport(surface,contact.Surface0Tag);
        double station=0;Span<KernelVector3> jet=stackalloc KernelVector3[2];var status=splineContact?EvaluateBlendContact(contact,parameter,1,jet,out station):EvaluateCurveCore(in Curves[slot],parameter,1,false,jet,out _);
        LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return EvaluationError(status);point=jet[0];
        LastSheetPartitionStage=SheetPartitionStage.SurfaceInverse;
        if(splineContact)
        {
            var blend=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(contact.Surface1Tag)].DataIndex];var program=GetNestedBlendProgram(blend);
            status=JointNestedBlendEvaluation.SplineContactParameters(program,blend.ProgramRoot,station,contact.BlendContactSide,out var su,out var sv);uv.param[0]=su;uv.param[1]=sv;
        }
        else status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(surface)],point,out uv);
        if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> support=stackalloc KernelVector3[4];
        status=EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(surface)],uv.param[0],uv.param[1],in layout,support,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        var du=support[layout.GetIndex(1,0)];var dv=support[layout.GetIndex(0,1)];var aa=Dot(du,du);var ab=Dot(du,dv);var bb=Dot(dv,dv);var det=aa*bb-ab*ab;
        if(!(det>1e-24*aa*bb)){LastSheetPartitionStage=SheetPartitionStage.UVTangent;return ParasolidConstants.PK_ERROR_eval_failure;}
        var sign=(forward?1:-1)*LocalSenseSign(Curves[slot].Sense);var ru=Dot(jet[1],du)*sign;var rv=Dot(jet[1],dv)*sign;
        tangent.param[0]=(ru*bb-rv*ab)/det;tangent.param[1]=(rv*aa-ru*ab)/det;
        return 0;
    }

    private static int SheetCoedgePoint(SurfTag surface,in ModelingEdge edge,byte forward,double fraction,
        out PK_UV_s uv,out PK_UV_s tangent,out KernelVector3 point)
    {
        var positive=(forward!=0)==(LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0);
        var parameter=positive?edge.Start+(edge.End-edge.Start)*fraction:edge.End-(edge.End-edge.Start)*fraction;
        return SheetCurveUV(surface,edge.Curve,parameter,forward!=0,out uv,out tangent,out point);
    }

    // Exact isoparametric rays reduce UV winding to existing bounded curve
    // intersection. No polygonal sampling decides face/loop containment.
    private static int SheetLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s query,out BufferCount winding)
    {
        ModelingPerformance.Winding();
        if(Surfaces[GetSurfaceSlotByTag(surface)].Class==SurfaceClass.Cylinder)
            return CylindricalLoopWinding(surface,edges,loop,query,out winding);
        if(Surfaces[GetSurfaceSlotByTag(surface)].Class==SurfaceClass.Sphere)
            return SphericalLoopWinding(surface,edges,loop,query,out winding);
        var vertical=BlendWindingPrefersVertical(surface,edges,loop,query);var error=0;winding=0;
        for(var attempt=0;attempt<6;attempt++)
        {
            var du=attempt switch{0=>vertical?0:1,1=>vertical?1:0,4=>.4142135623730951,5=>-.7320508075688772,_=>1};
            var dv=attempt switch{0=>vertical?1:0,1=>vertical?0:1,2=>.3819660112501051,3=>-.6180339887498949,_=>1};
            error=SheetLoopWindingRay(surface,edges,loop,query,du,dv,out winding);
            if(error==0||error is not (ParasolidConstants.PK_ERROR_cant_do_intersect or ParasolidConstants.PK_ERROR_eval_failure))return error;
        }
        return error;
    }

    // A local jet chooses a promising ray direction only. Root isolation and
    // winding still certify the result over the complete boundary intervals.
    private static bool BlendWindingPrefersVertical(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> loop,PK_UV_s query)
    {
        var slot=GetSurfaceSlotByTag(surface);
        if(Surfaces[slot].Class==SurfaceClass.BSurface)
        {
            foreach(ref readonly var coedge in loop)
            {
                var edge=edges[coedge.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];if(curve.Class!=CurveClass.ICurve)continue;
                var contact=ICurveDataPool[curve.DataIndex];
                if(contact.IsBlendContact==0||contact.UvType!=IntersectionUvType.First||!SameSheetSupport(surface,contact.Surface0Tag))continue;
                var n=contact.ChartCount;var parameters=BlendContactMap(contact).Slice(n,n);var at=(edge.Start+edge.End)*.5;var segment=0;
                while(segment<n-2&&parameters[segment+1]<=at)segment++;
                var uv=new ReadOnlySpan<double>(DereferenceBlock(contact.UvValueBlock),contact.UvValueCount);
                var du=uv[2*segment+2]-uv[2*segment];var dv=uv[2*segment+3]-uv[2*segment+1];
                // Stored UV chords choose a transverse ray only. Complete
                // root isolation and winding still certify the result.
                if(Math.Abs(du)>4*Math.Abs(dv))return true;
            }
            return false;
        }
        if(Surfaces[slot].Class!=SurfaceClass.BlendSurface)return false;
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
        if(EvaluateSurfaceCore(in Surfaces[slot],query.param[0],query.param[1],in layout,jet,0)!=AlgorithmStatus.Success)return false;
        foreach(ref readonly var coedge in loop)
        {
            var curve=Curves[GetCurveSlotByTag(edges[coedge.Edge].Curve)];if(curve.Class!=CurveClass.ICurve)continue;
            var data=ICurveDataPool[curve.DataIndex];var other=SameSheetSupport(surface,data.Surface0Tag)?data.Surface1Tag
                :SameSheetSupport(surface,data.Surface1Tag)?data.Surface0Tag:0;
            var otherSlot=GetSurfaceSlotByTag(other);if(otherSlot<0||!TryPrepareSurface(Surfaces[otherSlot],out var plane)||plane.Kind!=SurfaceClass.Plane)continue;
            if(Math.Abs(Dot(Sub(jet[0],plane.Origin),plane.Axis))>1e-8)continue;
            if(Math.Abs(Dot(jet[layout.GetIndex(0,1)],plane.Axis))>4*Math.Abs(Dot(jet[layout.GetIndex(1,0)],plane.Axis)))return true;
        }
        return false;
    }

    private static int SheetLoopWindingRay(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s query,double du,double dv,out BufferCount winding)
    {
        winding=0;var record=Surfaces[GetSurfaceSlotByTag(surface)];
        if(record.Class==SurfaceClass.Plane)return PlanarLoopWinding(surface,edges,loop,query,du,dv,out winding);
        if(query.param[0]<record.UMin||query.param[0]>record.UMax||query.param[1]<record.VMin||query.param[1]>record.VMax)return 0;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        foreach(ref readonly var coedge in loop)
        {
            ref readonly var edge=ref edges[coedge.Edge];
            LastSheetPartitionStage=SheetPartitionStage.RayIntersections;
            var status=IntersectParametricBoundaryRay(surface,query,du,dv,edge,roots,out var count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            for(var i=0;i<count;i++)
            {
                var root=roots[i];if(root.Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_eval_failure;
                if(root.First< -1e-10)continue;
                if(Math.Abs(root.First)<1e-10)return ParasolidConstants.PK_ERROR_eval_failure;
                var duplicate=false;for(var j=0;j<i;j++)if(Math.Abs(roots[j].First-root.First)<1e-10)duplicate=true;if(duplicate)continue;
                var epsilon=1e-10*Math.Max(1,edge.End-edge.Start);
                if(edge.StartVertex>=0&&(Math.Abs(root.Second-edge.Start)<epsilon||Math.Abs(root.Second-edge.End)<epsilon))
                    {LastSheetPartitionStage=SheetPartitionStage.RayEndpoint;return ParasolidConstants.PK_ERROR_eval_failure;}
                var error=SheetCurveUV(surface,edge.Curve,root.Second,coedge.Forward!=0,out _,out var tangent,out _);if(error!=0)return error;
                var crossing=tangent.param[1]*du-tangent.param[0]*dv;
                if(Math.Abs(crossing)<1e-10*(Math.Abs(tangent.param[0])+Math.Abs(tangent.param[1]))){LastSheetPartitionStage=SheetPartitionStage.RayTangency;return ParasolidConstants.PK_ERROR_eval_failure;}
                winding+=crossing>0?1:-1;
            }
        }
        return 0;
    }

    private static int SheetSourceContains(FaceSlot face,PK_UV_s query,out bool inside)
    {
        inside=false;var surface=Faces[face].SurfTag;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[2048];Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2048];
        var loop=Faces[face].FirstLoop;var winding=0;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var n=Loops[loop].FinCount;if(n>edges.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<n;k++,fin=Fins[fin].NextInLoop)
            {
                var record=Edges[Fins[fin].Edge];if(record.CurveTag<=0||!TryProfileEdgeRange(record,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                edges[k]=new(){Curve=record.CurveTag,Start=a,End=b,StartVertex=record.StartVertex,EndVertex=record.EndVertex};
                coedges[k]=new(){Edge=k,Forward=Fins[fin].Sense=='+'?(byte)1:(byte)0};
            }
            var error=SheetLoopWinding(surface,edges[..n],coedges[..n],query,out var contribution);if(error!=0)return error;winding+=contribution;
        }
        inside=winding!=0;return 0;
    }

    private static int CloneSheetCurveArc(CurveTag original,double first,double last,bool wrap,out CurveTag curve,out double lower,out double upper)
    {
        curve=0;lower=first;upper=last;
        if(!wrap)return TryCloneEntityCore(original,out curve);
        var record=Curves[GetCurveSlotByTag(original)];
        if(record.Class is CurveClass.Circle or CurveClass.Ellipse)
        { upper=last+Math.Tau;return TryCloneEntityCore(original,out curve); }
        if(record.Class!=CurveClass.ICurve)return ParasolidConstants.PK_ERROR_not_implemented;
        var data=ICurveDataPool[record.DataIndex];
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var status=TryPrepareICurveView(record,out var view,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var scratch=CommandScratch.Current;var memory=scratch.Take((view.ChartParameters.Length+3)*7);
            if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
            var samples=System.Runtime.InteropServices.MemoryMarshal.Cast<double,SurfaceIntersectionNode>(memory);
            var count=0;
            Span<KernelVector3> evaluated=stackalloc KernelVector3[1];
            for(var phase=0;phase<2;phase++)
            {
                var lo=phase==0?first:record.TMin;var hi=phase==0?record.TMax:last;
                for(var i=-1;i<=view.ChartParameters.Length;i++)
                {
                    var at=i<0?lo:i==view.ChartParameters.Length?hi:view.ChartParameters[i];
                    if(i>=0&&i<view.ChartParameters.Length&&(at<=lo||at>=hi))continue;
                    if(phase==1&&at==record.TMin)continue;
                    status=ICurveEvaluation.Evaluate(view,at,0,evaluated,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    status=TryIntersectionPairedParameters(data.Surface0Tag,data.Surface1Tag,evaluated[0],out var a,out var b);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    samples[count++]=new(){Point=evaluated[0],U=a.param[0],V=a.param[1],S=b.param[0],T=b.param[1]};
                }
            }
            var error=TryCloneEntityCore(data.Surface0Tag,out var support0);if(error!=0)return error;
            error=TryCloneEntityCore(data.Surface1Tag,out var support1);if(error!=0)return error;
            error=CreateGeneralIntersectionCurve(support0,support1,samples[..count],false,out curve,2*data.ChordalError,Math.Min(Math.PI,2*data.AngularError));if(error!=0)return error;
            var slot=GetCurveSlotByTag(curve);Curves[slot].Sense=record.Sense;lower=Curves[slot].TMin;upper=Curves[slot].TMax;return 0;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }
}
