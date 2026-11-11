using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int AssociateNativeSplineBoundaryPair(in RegularBoundaryView input,scoped Span<RegularFaceCut> cuts,
        BufferOffset first,BufferOffset second,SurfTag support,ref SheetArrangement model,out bool handled)
    {
        handled=false;var a=input.Edges[first];var b=input.Edges[second];
        if(Curves[GetCurveSlotByTag(a.Curve)].Class==CurveClass.ICurve)(a,b,first,second)=(b,a,second,first);
        var target=Curves[GetCurveSlotByTag(b.Curve)];
        if(Curves[GetCurveSlotByTag(a.Curve)].Class!=CurveClass.BCurve||target.Class!=CurveClass.ICurve)return 0;
        var data=ICurveDataPool[target.DataIndex];if(data.IsBlendContact!=0)return 0;
        var other=SameSheetSupport(support,data.Surface0Tag)?data.Surface1Tag:SameSheetSupport(support,data.Surface1Tag)?data.Surface0Tag:0;
        var surface=GetSurfaceSlotByTag(support);
        if(other==0||!ExactNativeSplineBoundaryContact(a,surface)
            ||!TryFiniteLinePlaneEvent(a,first,other,out var found,allowExactEnd:true))return 0;
        handled=true;if(found.Count==0)return 0;
        var source=found.FirstRoot;
        if(source.Lower!=source.Upper||!TryNativeSplineBoundaryEnd(input,first,second,support,source.Parameter,out var station))
            return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var crossing=new OperationIntersectionPoint{Point=source.Point,First=source.Parameter,Second=station,Kind=IntersectionVectorKind.Simple};
        return AssociateCoincidentLineRoot(input,cuts,a,first,b,second,crossing,ref model);
    }

    private static bool TryNativeSplineBoundaryEnd(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        SurfTag support,double sourceParameter,out double station)
    {
        station=0;var found=false;var source=input.Edges[first];var target=input.Edges[second];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        foreach(ref readonly var face in input.Faces)
        {
            if(!SameSheetSupport(face.Surface,support))continue;
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=input.Loops[face.FirstLoop+l];var usesTarget=false;
                foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))usesTarget|=use.Edge==second;
                if(!usesTarget)continue;
                foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(use.Edge==second)continue;var other=input.Edges[use.Edge];
                    if(!TryExactFiniteBoundaryRoots(source,other,roots,out var count))continue;
                    for(var r=0;r<count;r++)
                    {
                        if(roots[r].First!=sourceParameter)continue;
                        var endpoint=NativeLineEnd(other,roots[r].Second);if(endpoint<0)continue;
                        var at=endpoint==NativeLineEnd(target,target.Start)?target.Start:endpoint==NativeLineEnd(target,target.End)?target.End:double.NaN;
                        if(double.IsNaN(at))continue;
                        // The actual source coedge and target coedge own this
                        // native endpoint. No CHART inverse or spatial match
                        // substitutes for their finite incidence.
                        if(found&&station!=at)return false;station=at;found=true;
                    }
                }
            }
        }
        return found;
    }

    private static bool ExactNativeSplineBoundaryContact(in ModelingEdge edge,SurfaceSlot surface)
    {
        var curve=Curves[GetCurveSlotByTag(edge.Curve)];
        if(curve.Class!=CurveClass.BCurve||Surfaces[surface].Class!=SurfaceClass.BSurface)return false;
        var c=GetBCurveView(BCurveDataStore[curve.DataIndex]);var s=GetBSurfaceView(BSurfaceDataStore[Surfaces[surface].DataIndex]);
        if(s.Rational||c.Dimension!=(c.Rational?4:3)||s.Dimension!=3||c.VertexCount!=c.Degree+1
            ||s.UVertexCount!=s.UDegree+1||s.VVertexCount!=s.VDegree+1||edge.Start<c.Start||edge.End>c.End)return false;
        if(!Clamped(c.Knots,c.Degree)||!Clamped(s.UKnots,s.UDegree)||!Clamped(s.VKnots,s.VDegree))return false;
        var weight=c.Rational?c.Vertices[3]:1;if(!(weight>0))return false;
        if(c.Rational)for(var i=0;i<c.VertexCount;i++)if(c.Vertices[c.Dimension*i+3]!=weight)return false;
        for(var boundary=0;boundary<4;boundary++)
        {
            var degree=boundary<2?s.VDegree:s.UDegree;
            for(var reverse=0;reverse<2;reverse++)
            {
                var matches=degree==c.Degree;
                if(matches)for(var i=0;i<=degree&&matches;i++)for(var d=0;d<3;d++)
                    matches&=(new IntervalNumber(s.Vertices[At(boundary,i,s.UVertexCount,s.VVertexCount)+d])*weight-c.Vertices[c.Dimension*(reverse==0?i:degree-i)+d]).Magnitude==0;
                if(matches)return true;
                if(!TryExactNativeAffineCurve(curve,out _))continue;
                matches=true;
                for(var i=0;i<=degree&&matches;i++)for(var d=0;d<3;d++)
                {
                    var first=c.Vertices[c.Dimension*(reverse==0?0:c.Degree)+d];var last=c.Vertices[c.Dimension*(reverse==0?c.Degree:0)+d];
                    var residual=new IntervalNumber(s.Vertices[At(boundary,i,s.UVertexCount,s.VVertexCount)+d])*weight*degree
                        -new IntervalNumber(first)*(degree-i)-new IntervalNumber(last)*i;
                    matches&=residual.Magnitude==0;
                }
                if(matches)return true;
            }
        }
        return false;
        static BufferOffset At(BufferOffset side,BufferOffset i,BufferCount nu,BufferCount nv)
            =>3*(side<2?((side==0?0:nu-1)*nv+i):i*nv+(side==2?0:nv-1));
        static bool Clamped(ReadOnlySpan<double> knots,SplineDegree degree)
        {
            if(knots.Length!=2*(degree+1)||!(knots[^1]>knots[0]))return false;
            for(var i=0;i<=degree;i++)if(knots[i]!=knots[0]||knots[knots.Length-1-i]!=knots[^1])return false;
            return true;
        }
    }
}
