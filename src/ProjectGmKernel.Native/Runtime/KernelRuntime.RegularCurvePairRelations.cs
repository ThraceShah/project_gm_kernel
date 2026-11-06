using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool UniqueMonotoneICurveCarrier(in ICurveData data,out KernelVector3 direction)
    {
        direction=default;if(data.IsBlendContact!=0)return false;
        var a=GetSurfaceSlotByTag(data.Surface0Tag);var b=GetSurfaceSlotByTag(data.Surface1Tag);var spline=-1;AnalyticSurface plane=default;
        var planeB=TryPrepareSurface(Surfaces[b],out plane)&&plane.Kind==SurfaceClass.Plane||TryAffinePlanarSurface(b,out plane,out _,out _);
        if(Surfaces[a].Class==SurfaceClass.BSurface&&planeB)spline=a;
        else if(Surfaces[b].Class==SurfaceClass.BSurface&&(TryPrepareSurface(Surfaces[a],out plane)&&plane.Kind==SurfaceClass.Plane||TryAffinePlanarSurface(a,out plane,out _,out _)))spline=b;
        if(spline<0)return false;
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[64];Span<KernelVector3> points=stackalloc KernelVector3[64];
        var status=RuledBSurfacePlaneIntersection.Find(GetBSurfaceView(BSurfaceDataStore[Surfaces[spline].DataIndex]),plane,branches,out var n,points,out var np);
        if(status!=AlgorithmStatus.Success||n!=1||np!=0||branches[0].Kind!=IntersectionCurveKind.Simple)return false;
        var branch=branches[0];if(branch.Class==CurveClass.Line){direction=branch.Axis;return OperationIntersection.Norm(direction)>0;}
        if(branch.Class!=CurveClass.BCurve)return false;
        var p=new ReadOnlySpan<double>(branch.Poles,4*(branch.Degree+1));for(var i=0;i<=branch.Degree;i++)if(p[4*i+3]<=0)return false;
        var start=ProfileEuclideanPole(p,4,0);direction=Sub(ProfileEuclideanPole(p,4,branch.Degree),start);if(!ProfileNormalize(ref direction))return false;
        var previous=double.NegativeInfinity;
        for(var i=0;i<=branch.Degree;i++){var projection=Dot(Sub(ProfileEuclideanPole(p,4,i),start),direction);if(projection<previous)return false;previous=projection;}
        return previous>0;
    }

    private static AlgorithmStatus IntersectCoincidentICurveIntervals(in IntersectionCurveSpan a,in IntersectionCurveSpan b,in KernelVector3 direction,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;Span<KernelVector3> points=stackalloc KernelVector3[4],jet=stackalloc KernelVector3[2];Span<double> projections=stackalloc double[4];
        for(var i=0;i<4;i++)
        {
            var span=i<2?a:b;var t=(i&1)==0?span.Lower:span.Upper;var status=EvaluateCurveCore(in Curves[span.Slot],t,0,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;
            points[i]=jet[0];projections[i]=Dot(Sub(jet[0],points[0]),direction);
        }
        var a0=projections[0]<=projections[1]?0:1;var b0=projections[2]<=projections[3]?2:3;
        var lower=projections[a0]>=projections[b0]?a0:b0;var upper=projections[1-a0]<=projections[5-b0]?1-a0:5-b0;
        if(projections[upper]<projections[lower]-1e-12)return AlgorithmStatus.Success;
        for(var end=0;end<2;end++)
        {
            var point=new PK_VECTOR_s();WriteVector(&point,points[end==0?lower:upper]);double ta=0,tb=0;
            var error=CurveParameteriseVectorCore(Curves[a.Slot].Header.Tag,point,&ta);if(error!=0)return AlgorithmStatus.NotConverged;
            error=CurveParameteriseVectorCore(Curves[b.Slot].Header.Tag,point,&tb);if(error!=0)return AlgorithmStatus.NotConverged;
            if(!OperationIntersection.InInterval(ta,a.Lower,a.Upper)||!OperationIntersection.InInterval(tb,b.Lower,b.Upper))return AlgorithmStatus.NotConverged;
            if(!OperationIntersection.AddPoint(output,ref count,points[end==0?lower:upper],ta,tb,projections[upper]==projections[lower]?IntersectionVectorKind.Tangent:end==0?IntersectionVectorKind.Start:IntersectionVectorKind.End))return AlgorithmStatus.OutputTooSmall;
        }
        OperationIntersection.Sort(output[..count]);if(count==2&&output[0].Kind==IntersectionVectorKind.End){output[0].Kind=IntersectionVectorKind.Start;output[1].Kind=IntersectionVectorKind.End;}return AlgorithmStatus.Success;
    }

    private static bool SameRegularICurve(in ICurveData a,in ICurveData b)
    {
        if(a.IsBlendContact!=b.IsBlendContact||a.Sense!=b.Sense||a.BaseParameter!=b.BaseParameter||a.BaseScale!=b.BaseScale||a.ChartCount!=b.ChartCount)return false;
        if(a.IsBlendContact!=0&&(a.BlendContactSide!=b.BlendContactSide||a.BlendContactReversed!=b.BlendContactReversed))return false;
        if(!RegularCoincidentSupports(a.Surface0Tag,b.Surface0Tag)||!RegularCoincidentSupports(a.Surface1Tag,b.Surface1Tag))return false;
        if(Surfaces[GetSurfaceSlotByTag(a.Surface0Tag)].Sense!=Surfaces[GetSurfaceSlotByTag(b.Surface0Tag)].Sense
            ||Surfaces[GetSurfaceSlotByTag(a.Surface1Tag)].Sense!=Surfaces[GetSurfaceSlotByTag(b.Surface1Tag)].Sense)return false;
        for(var side=0;side<2;side++)
        {
            var first=GetSurfaceSlotByTag(side==0?a.Surface0Tag:a.Surface1Tag);var second=GetSurfaceSlotByTag(side==0?b.Surface0Tag:b.Surface1Tag);
            if(RegularPlanarSupport(first,out var pa,out _)&&RegularPlanarSupport(second,out var pb,out _)&&EvaluationMath.Dot(pa.Axis,pb.Axis)<=0)return false;
        }
        return HvecSpan(a).Slice(3*a.StartLimit.HvecCount,3*a.ChartCount).SequenceEqual(HvecSpan(b).Slice(3*b.StartLimit.HvecCount,3*b.ChartCount));
    }

    private static AlgorithmStatus IntersectRegularCurvePair(in IntersectionCurveSpan first,in IntersectionCurveSpan second,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var a=Curves[first.Slot];var b=Curves[second.Slot];
        if(a.Class!=CurveClass.ICurve)
        {
            var swapped=IntersectRegularCurvePair(second,first,output,out count);
            for(var i=0;i<count;i++)(output[i].First,output[i].Second)=(output[i].Second,output[i].First);OperationIntersection.Sort(output[..count]);return swapped;
        }
        var data=ICurveDataPool[a.DataIndex];
        if(b.Class==CurveClass.ICurve&&SameRegularICurve(data,ICurveDataPool[b.DataIndex]))
        {
            var lo=Math.Max(first.Lower,second.Lower);var hi=Math.Min(first.Upper,second.Upper);if(hi<lo)return AlgorithmStatus.Success;
            Span<KernelVector3> jet=stackalloc KernelVector3[2];var status=EvaluateCurveCore(in a,lo,0,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;
            if(!OperationIntersection.AddPoint(output,ref count,jet[0],lo,lo,hi==lo?IntersectionVectorKind.Tangent:IntersectionVectorKind.Start))return AlgorithmStatus.OutputTooSmall;
            if(hi>lo){status=EvaluateCurveCore(in a,hi,0,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;if(!OperationIntersection.AddPoint(output,ref count,jet[0],hi,hi,IntersectionVectorKind.End))return AlgorithmStatus.OutputTooSmall;}
            return AlgorithmStatus.Success;
        }
        if(b.Class==CurveClass.ICurve)
        {
            var other=ICurveDataPool[b.DataIndex];
            var sameConstraints=RegularCoincidentSupports(data.Surface0Tag,other.Surface0Tag)&&RegularCoincidentSupports(data.Surface1Tag,other.Surface1Tag)
                ||RegularCoincidentSupports(data.Surface0Tag,other.Surface1Tag)&&RegularCoincidentSupports(data.Surface1Tag,other.Surface0Tag);
            if(sameConstraints&&UniqueMonotoneICurveCarrier(data,out var direction))return IntersectCoincidentICurveIntervals(first,second,direction,output,out count);
        }
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var side=0;side<2;side++)
        {
            var surface=side==0?data.Surface0Tag:data.Surface1Tag;
            if(b.Class==CurveClass.ICurve)
            {
                var other=ICurveDataPool[b.DataIndex];
                if(RegularCoincidentSupports(surface,other.Surface0Tag)||RegularCoincidentSupports(surface,other.Surface1Tag))continue;
            }
            // Any crossing with the selected ICurve lies on both defining
            // supports. A complete finite intersection with either support
            // gives a superset; exact ICurve inversion selects its branch.
            var status=IntersectRegularSurfaceCurve(surface,b.Header.Tag,second.Lower,second.Upper,roots,out var n);
            if(status!=AlgorithmStatus.Success)return status;
            if(n==2&&roots[0].Kind==IntersectionVectorKind.Start&&roots[1].Kind==IntersectionVectorKind.End)continue;
            for(var i=0;i<n;i++)
            {
                var point=new PK_VECTOR_s();WriteVector(&point,roots[i].Point);double t=0;var error=CurveParameteriseVectorCore(a.Header.Tag,point,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                if(!OperationIntersection.InInterval(t,first.Lower,first.Upper))continue;
                if(!OperationIntersection.AddPoint(output,ref count,roots[i].Point,t,roots[i].First,roots[i].Kind))return AlgorithmStatus.OutputTooSmall;
            }
            OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
        }
        return AlgorithmStatus.Unsupported;
    }
}
