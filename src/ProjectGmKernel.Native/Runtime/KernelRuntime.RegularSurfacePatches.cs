using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static BufferCount RegularSurfacePatchCount(SurfaceSlot slot)
    {
        var record=Surfaces[slot];
        if(record.Class==SurfaceClass.BlendSurface)
        {
            var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
            return program.Spines[program.Surfaces[data.ProgramRoot].Spine].ChartCount-1;
        }
        if(record.Class==SurfaceClass.BSurface)
        {
            var view=GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);
            return (view.UVertexCount-view.UDegree)*(view.VVertexCount-view.VDegree);
        }
        return TryPrepareSurface(record,out var analytic)?AnalyticBezierPatches.Count(analytic):0;
    }

    private static AlgorithmStatus RegularSurfacePatch(SurfaceSlot slot,BufferOffset index,in IntervalBox3 extent,
        Span<IntervalNumber> poles,out SurfaceIntersectionPatch patch)
    {
        patch=default;var record=Surfaces[slot];
        if(record.Class==SurfaceClass.BlendSurface)
        {
            var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
            var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
            patch=new(program,data.ProgramRoot,new(charts[index].Parameter,charts[index+1].Parameter,record.VMin,record.VMax));
            patch=new(patch,record.Header.Tag,record.Header.Generation,record.Sense);
            return AlgorithmStatus.Success;
        }
        if(record.Class==SurfaceClass.BSurface)
        {
            var view=GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);var nv=view.VVertexCount-view.VDegree;
            var u=view.UDegree+index/nv;var v=view.VDegree+index%nv;
            var domain=new SurfaceParameterBox(view.UKnots[u],view.UKnots[u+1],view.VKnots[v],view.VKnots[v+1]);
            if(domain.U1<=domain.U0||domain.V1<=domain.V0)return AlgorithmStatus.Success;
            var status=BezierSurfaceBounds.Extract(view,u,v,poles);if(status!=AlgorithmStatus.Success)return status;
            patch=new(new BezierSurfacePatch(poles[..(4*(view.UDegree+1)*(view.VDegree+1))],view.UDegree,view.VDegree,domain));
            return status;
        }
        if(!TryPrepareSurface(record,out var analytic))return AlgorithmStatus.Unsupported;
        var found=AnalyticBezierPatches.Create(analytic,extent,index,poles,out var p,out var q,out var box);
        if(found==AlgorithmStatus.Success)patch=new(new BezierSurfacePatch(poles[..(4*(p+1)*(q+1))],p,q,box));
        return found;
    }

    private static AlgorithmStatus RegularSurfaceExtent(SurfaceSlot slot,out IntervalBox3 extent)
    {
        extent=default;var record=Surfaces[slot];
        if(record.Class==SurfaceClass.BSurface)
        {extent=GeneralSurfaceExtent(GetBSurfaceView(BSurfaceDataStore[record.DataIndex]));return AlgorithmStatus.Success;}
        if(record.Class==SurfaceClass.Sphere&&TryPrepareSurface(record,out var sphere))
        {extent=AnalyticBallBounds(sphere.Origin,sphere.Radius);return AlgorithmStatus.Success;}
        if(record.Class!=SurfaceClass.BlendSurface)return AlgorithmStatus.Unsupported;
        var data=BlendedEdgeDataPool[record.DataIndex];
        return BlendExtent(GetNestedBlendProgram(data),data.ProgramRoot,new(record.UMin,record.UMax,record.VMin,record.VMax),out extent);
    }

    // The exact SPCURVEs produced by NestedBlendIsoparam use u=t and a
    // constant v. Other parameter curves require their own certified bounds.
    private static bool TryBlendIsoparam(CurveSlot slot,out SurfaceSlot surface,out double v)
    {
        surface=-1;v=0;if(Curves[slot].Class!=CurveClass.SPCurve)return false;
        var data=SpCurveDataPool[Curves[slot].DataIndex];var support=GetSurfaceSlotByTag(data.SurfTag);
        var basis=GetCurveSlotByTag(data.BCurveTag);if(support<0||basis<0||Surfaces[support].Class!=SurfaceClass.BlendSurface)return false;
        var view=GetBCurveView(BCurveDataStore[Curves[basis].DataIndex]);
        if(view.Rational||view.Dimension!=2||view.Degree!=1||view.VertexCount!=2
            ||view.Vertices[0]!=view.Start||view.Vertices[2]!=view.End||view.Vertices[1]!=view.Vertices[3])return false;
        surface=support;v=view.Vertices[1];return true;
    }

    private static AlgorithmStatus IntersectBlendIsoparamPlane(CurveSlot curve,double first,double last,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;if(!TryBlendIsoparam(curve,out var surface,out var fixedV))return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        IntervalBox3 extent=default;AlgorithmStatus status;
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,poles,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var lower=Math.Max(first,patch.Domain.U0);var upper=Math.Min(last,patch.Domain.U1);if(!(upper>lower))continue;
            var iso=new SurfaceIntersectionPatch(patch,false,fixedV,lower,upper);
            status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,output,ref count);if(status!=AlgorithmStatus.Success)return status;
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectProceduralICurvePlane(CurveSlot curve,double first,double last,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[curve].DataIndex];var a=GetSurfaceSlotByTag(data.Surface0Tag);var b=GetSurfaceSlotByTag(data.Surface1Tag);
        if(Surfaces[a].Class!=SurfaceClass.BlendSurface)(a,b)=(b,a);
        // Two planes define an exact line. Isolating that line against the
        // Blend removes two redundant plane parameters from the root system.
        if(TryPrepareSurface(Surfaces[b],out var support)&&support.Kind==SurfaceClass.Plane)
            return IntersectProceduralICurveTwoPlanes(curve,first,last,a,support,plane,output,out count);
        var status=RegularSurfaceExtent(a,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(a);i++)
        {
            status=RegularSurfacePatch(a,i,extent,ap,out var patch);if(status!=AlgorithmStatus.Success)return status;
            for(var j=0;j<RegularSurfacePatchCount(b);j++)
            {
                status=RegularSurfacePatch(b,j,extent,bp,out var other);if(status!=AlgorithmStatus.Success)return status;
                if(other.Domain.U1<=other.Domain.U0||other.Domain.V1<=other.Domain.V0)continue;
                status=SurfacePairPlaneRoots.Find(patch,other,plane,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var k=0;k<n;k++)
                {
                    var vector=new Generated.PK_VECTOR_s();WriteVector(&vector,roots[k].Point);double parameter=0;
                    var error=CurveParameteriseVectorCore(Curves[curve].Header.Tag,vector,&parameter);
                    if(error==Generated.ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                    if(OperationIntersection.InInterval(parameter,first,last)
                        &&!OperationIntersection.AddPoint(output,ref count,roots[k].Point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
