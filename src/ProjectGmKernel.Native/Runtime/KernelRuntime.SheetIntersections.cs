using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct SheetCutCurve
    {
        internal CurveTag Curve;
        internal double First, Last;
        internal byte Closed;
    }

    private static int PrepareSheetCutCurves(SurfTag surface, SurfTag plane, Span<SheetCutCurve> cuts, out BufferCount count)
    {
        count = 0;
        var a = GetSurfaceSlotByTag(surface); var b = GetSurfaceSlotByTag(plane);
        // Exact ruled/iso construction below expects the parametric support
        // first. A repeated Boolean may encounter the old planar tool first.
        if(Surfaces[a].Class==SurfaceClass.Plane&&Surfaces[b].Class!=SurfaceClass.Plane)
        { (surface,plane)=(plane,surface);(a,b)=(b,a); }
        Span<OperationIntersectionBranch> branches = stackalloc OperationIntersectionBranch[64];
        Span<KernelVector3> isolated = stackalloc KernelVector3[64];
        var status = FindSurfaceIntersections(a, b, branches, out var nc, isolated, out var np);
        if (status == AlgorithmStatus.Unsupported)
        {
            var options = new PK_SURF_intersect_surf_o_s { o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c };
            // Unlike public interest boxes, these are actual finite modeling
            // domains. Result arcs are subsequently clipped to the face loops.
            if(Surfaces[a].Class==SurfaceClass.BlendSurface)
            {
                var support=Surfaces[a];options.have_uvbox_1=1;
                options.uvbox_1.param[0]=support.UMin;options.uvbox_1.param[1]=support.VMin;
                options.uvbox_1.param[2]=support.UMax;options.uvbox_1.param[3]=support.VMax;
            }
            if(Surfaces[b].Class==SurfaceClass.BlendSurface)
            {
                var support=Surfaces[b];options.have_uvbox_2=1;
                options.uvbox_2.param[0]=support.UMin;options.uvbox_2.param[1]=support.VMin;
                options.uvbox_2.param[2]=support.UMax;options.uvbox_2.param[3]=support.VMax;
            }
            int* curves = null; PK_INTERVAL_s* intervals = null; int* kinds = null;
            var error = GeneralSurfIntersectSurf(a,b,-1,&options,&nc,&curves,&intervals,&kinds);
            try
            {
                if (error != 0) return error;
                if (nc > cuts.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                for (var i=0;i<nc;i++) cuts[i]=new(){Curve=curves[i],First=intervals[i].value[0],Last=intervals[i].value[1],Closed=ProfileClosedICurve(curves[i],intervals[i].value[0],intervals[i].value[1])?(byte)1:(byte)0};
                count=nc;return 0;
            }
            finally { if(curves!=null)MemoryFree(curves);if(intervals!=null)MemoryFree(intervals);if(kinds!=null)MemoryFree(kinds); }
        }
        if (status != AlgorithmStatus.Success) return IntersectionError(status);
        if (np!=0||nc>cuts.Length) return ParasolidConstants.PK_ERROR_not_implemented;
        if (nc==0) return 0;
        var clone=TryCloneEntityCore(surface,out var first);if(clone!=0)return clone;
        clone=TryCloneEntityCore(plane,out var second);if(clone!=0)return clone;
        for (var i=0;i<nc;i++)
        {
            if(branches[i].Kind!=IntersectionCurveKind.Simple)return ParasolidConstants.PK_ERROR_not_implemented;
            CurveTag exact=0;var error=CreateIntersectionBranch(branches[i],&exact);if(error!=0)return error;
            error=MakeCurvedCutICurve(exact,branches[i].Start,branches[i].End,first,second,out var curve);if(error!=0)return error;
            error=EntityDelete(1,&exact);if(error!=0)return error;
            var record=Curves[GetCurveSlotByTag(curve)];cuts[i]=new(){Curve=curve,First=record.TMin,Last=record.TMax};
        }
        count=nc;return 0;
    }

    private static AlgorithmStatus IntersectSheetCurvePlane(CurveTag curve, double first, double last,
        SurfTag plane, Span<OperationIntersectionPoint> output, out BufferCount count)
    {
        count=0;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)],out var cutting))return AlgorithmStatus.Unsupported;
        return IntersectSheetCurvePlane(curve,first,last,cutting,output,out count);
    }

    private static AlgorithmStatus IntersectSheetCurvePlane(CurveTag curve,double first,double last,in AnalyticSurface cutting,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var slot=GetCurveSlotByTag(curve);if(slot<0)return AlgorithmStatus.InvalidInput;
        if(Curves[slot].Class==CurveClass.SPCurve)return IntersectBlendIsoparamPlane(slot,first,last,cutting,output,out count);
        if(Curves[slot].Class!=CurveClass.ICurve)
        {
            var interval=new PK_INTERVAL_s();interval.value[0]=first;interval.value[1]=last;
            var error=ResolveIntersectionCurve(curve,interval,out var span);if(error!=0)return AlgorithmStatus.Unsupported;
            var status=IntersectSurfaceCurve(cutting,span,output,out count);
            if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<count;i++)output[i].First=(output[i].First-span.Offset)/span.Scale;
            OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
        }
        var data=ICurveDataPool[Curves[slot].DataIndex];
        var a=Surfaces[GetSurfaceSlotByTag(data.Surface0Tag)];var b=Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)];
        if(a.Class==SurfaceClass.BlendSurface||b.Class==SurfaceClass.BlendSurface)
        {
            var other=a.Class==SurfaceClass.BlendSurface?b:a;
            if(TryPrepareSurface(other,out var flat)&&flat.Kind==SurfaceClass.Plane&&OperationIntersection.Norm(ProfileCross(flat.Axis,cutting.Axis))==0)
            {
                if(Dot(Sub(flat.Origin,cutting.Origin),cutting.Axis)!=0)return AlgorithmStatus.Success;
                Span<KernelVector3> p=stackalloc KernelVector3[1];var evaluated=EvaluateCurveCore(in Curves[slot],first,0,false,p,out _);if(evaluated!=AlgorithmStatus.Success)return evaluated;
                OperationIntersection.AddPoint(output,ref count,p[0],first,0,IntersectionVectorKind.Start);
                evaluated=EvaluateCurveCore(in Curves[slot],last,0,false,p,out _);if(evaluated!=AlgorithmStatus.Success)return evaluated;
                return OperationIntersection.AddPoint(output,ref count,p[0],last,0,IntersectionVectorKind.End)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
            }
            return IntersectProceduralICurvePlane(slot,first,last,cutting,output,out count);
        }
        if(a.Class!=SurfaceClass.BSurface)(a,b)=(b,a);
        if(a.Class!=SurfaceClass.BSurface)return AlgorithmStatus.Unsupported;
        var analytic=TryPrepareSurface(b,out var support);
        if(analytic&&AnalyticBezierPatches.Count(support)==0)return AlgorithmStatus.Unsupported;
        if(analytic&&support.Kind==SurfaceClass.Plane&&OperationIntersection.Norm(ProfileCross(support.Axis,cutting.Axis))==0)
        {
            if(Dot(Sub(support.Origin,cutting.Origin),cutting.Axis)!=0)return AlgorithmStatus.Success;
            Span<KernelVector3> point=stackalloc KernelVector3[2];
            var status=EvaluateCurveCore(in Curves[slot],first,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            OperationIntersection.AddPoint(output,ref count,point[0],first,0,IntersectionVectorKind.Start);
            status=EvaluateCurveCore(in Curves[slot],last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            return OperationIntersection.AddPoint(output,ref count,point[0],last,0,IntersectionVectorKind.End)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        var av=GetBSurfaceView(BSurfaceDataStore[a.DataIndex]);var bv=b.Class==SurfaceClass.BSurface?GetBSurfaceView(BSurfaceDataStore[b.DataIndex]):default;
        if(!analytic&&b.Class!=SurfaceClass.BSurface)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        var extent=GeneralSurfaceExtent(av);
        for(var u=av.UDegree;u<av.UVertexCount;u++)for(var v=av.VDegree;v<av.VVertexCount;v++)
        {
            if(!(av.UKnots[u+1]>av.UKnots[u])||!(av.VKnots[v+1]>av.VKnots[v]))continue;
            var status=BezierSurfaceBounds.Extract(av,u,v,ap);if(status!=AlgorithmStatus.Success)return status;
            var patch=new BezierSurfacePatch(ap[..(4*(av.UDegree+1)*(av.VDegree+1))],av.UDegree,av.VDegree,new(av.UKnots[u],av.UKnots[u+1],av.VKnots[v],av.VKnots[v+1]));
            var sc=analytic?AnalyticBezierPatches.Count(support):bv.UVertexCount-bv.UDegree;
            var tc=analytic?1:bv.VVertexCount-bv.VDegree;
            for(var s=0;s<sc;s++)for(var t=0;t<tc;t++)
            {
                SplineDegree p,q;SurfaceParameterBox domain;
                if(analytic){status=AnalyticBezierPatches.Create(support,extent,s,bp,out p,out q,out domain);if(status!=AlgorithmStatus.Success)return status;}
                else
                {
                    var us=s+bv.UDegree;var vs=t+bv.VDegree;if(!(bv.UKnots[us+1]>bv.UKnots[us])||!(bv.VKnots[vs+1]>bv.VKnots[vs]))continue;
                    status=BezierSurfaceBounds.Extract(bv,us,vs,bp);if(status!=AlgorithmStatus.Success)return status;p=bv.UDegree;q=bv.VDegree;domain=new(bv.UKnots[us],bv.UKnots[us+1],bv.VKnots[vs],bv.VKnots[vs+1]);
                }
                var other=new BezierSurfacePatch(bp[..(4*(p+1)*(q+1))],p,q,domain);
                status=SurfacePairPlaneRoots.Find(patch,other,cutting,roots,out var nr);if(status!=AlgorithmStatus.Success)return status;
                for(var i=0;i<nr;i++)
                {
                    var vector=new PK_VECTOR_s();WriteVector(&vector,roots[i].Point);double parameter=0;
                    var error=CurveParameteriseVectorCore(curve,vector,&parameter);
                    if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;
                    if(error!=0)return error==ParasolidConstants.PK_ERROR_memory_full?AlgorithmStatus.WorkspaceTooSmall:AlgorithmStatus.NotConverged;
                    if(!OperationIntersection.InInterval(parameter,first,last))continue;
                    if(!OperationIntersection.AddPoint(output,ref count,roots[i].Point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
