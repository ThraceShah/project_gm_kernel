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
    private static AlgorithmStatus IntersectProceduralICurveTwoPlanes(CurveSlot curve,double first,double last,SurfaceSlot surface,
        in AnalyticSurface support,in AnalyticSurface cutter,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[1];Span<KernelVector3> isolated=stackalloc KernelVector3[1];
        var status=OperationIntersection.Surfaces(support,cutter,branches,out var nc,isolated,out var np);if(status!=AlgorithmStatus.Success)return status;
        if(np!=0||nc!=1||branches[0].Class!=CurveClass.Line)return AlgorithmStatus.Unsupported;
        status=CertifiedIntersectionCurveExtent(Curves[curve].Header.Tag,first,last,out var extent);
        if(status!=AlgorithmStatus.Success){status=RegularSurfaceExtent(surface,out extent);if(status!=AlgorithmStatus.Success)return status;}
        var line=branches[0];var direction=line.Axis;var origin=line.Origin;
        var parameter=(new IntervalNumber(extent.XLo,extent.XHi)-origin.X)*direction.X
            +(new IntervalNumber(extent.YLo,extent.YHi)-origin.Y)*direction.Y+(new IntervalNumber(extent.ZLo,extent.ZHi)-origin.Z)*direction.Z;
        if(!parameter.Finite||!(parameter.Upper>parameter.Lower))return AlgorithmStatus.NotConverged;
        var lo=parameter.Lower-1e-9;var hi=parameter.Upper+1e-9;
        Span<IntervalNumber> linePoles=stackalloc IntervalNumber[8],poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        for(var end=0;end<2;end++)
        {
            IntervalNumber t=end==0?lo:hi;linePoles[4*end]=origin.X+t*direction.X;linePoles[4*end+1]=origin.Y+t*direction.Y;
            linePoles[4*end+2]=origin.Z+t*direction.Z;linePoles[4*end+3]=1;
        }
        var ray=new SurfaceIntersectionPatch(new BezierSurfacePatch(linePoles,1,0,new(lo,hi,0,1)));
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,poles,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var box=patch.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
            var cell=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=lo},Upper=new(){U=box.U1,V=box.V1,S=hi}};
            status=ParametricSurfaceIntersection.Isolate(patch,ray,cell,3,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
            for(var r=0;r<n;r++)
            {
                var vector=new PK_VECTOR_s();WriteVector(&vector,roots[r].Point);double t=0;
                var error=CurveParameteriseVectorCore(Curves[curve].Header.Tag,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                if(OperationIntersection.InInterval(t,first,last)&&!OperationIntersection.AddPoint(output,ref count,roots[r].Point,t,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
