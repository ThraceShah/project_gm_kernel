using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;

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
        =>TryExactNativeSplineBoundaryContact(edge,surface,out _,out _);

    private static bool BoundExactNativeSplineBoundary(in ModelingEdge edge,SurfaceSlot surface,in IntervalNumber stations,
        out SurfaceParameterBox uv,out byte boundary)
    {
        uv=default;boundary=0;if(!TryExactNativeSplineBoundaryContact(edge,surface,out var side,out var reverse)||!stations.Finite)return false;
        var c=GetBCurveView(BCurveDataStore[Curves[GetCurveSlotByTag(edge.Curve)].DataIndex]);
        var target=GetBSurfaceView(BSurfaceDataStore[Surfaces[surface].DataIndex]);
        var normalized=(stations-c.Start)/(new IntervalNumber(c.End)-c.Start);if(reverse)normalized=1-normalized;
        var variable=side<2?target.VStart+normalized*(new IntervalNumber(target.VEnd)-target.VStart):target.UStart+normalized*(new IntervalNumber(target.UEnd)-target.UStart);
        var fixedValue=side switch{0=>target.UStart,1=>target.UEnd,2=>target.VStart,_=>target.VEnd};
        uv=side<2?new(fixedValue,fixedValue,variable.Lower,variable.Upper):new(variable.Lower,variable.Upper,fixedValue,fixedValue);
        boundary=(byte)(side+1);return uv.Valid;
    }

    // Equal geometric line carriers need not have equal native spline
    // parameters. Transport both the root enclosure and an INNER image of
    // its uniqueness domain through the literal native Bernstein polynomials.
    private static bool BoundNativeSplineBoundaryRoot(in ModelingEdge edge,SurfaceSlot surface,
        in IntervalNumber root,in IntervalNumber unique,out SurfaceParameterBox uv,out SurfaceParameterBox domain,out byte boundary)
    {
        uv=domain=default;boundary=0;
        if(TryExactNativeSplineBoundaryContact(edge,surface,out var exactSide,out var reversed))
        {
            if(!BoundExactNativeSplineBoundary(edge,surface,root,out uv,out boundary)
                ||!BoundExactNativeSplineBoundary(edge,surface,new(unique.Lower),out var first,out _)
                ||!BoundExactNativeSplineBoundary(edge,surface,new(unique.Upper),out var last,out _))return false;
            var lo=exactSide<2?(reversed?last.V1:first.V1):(reversed?last.U1:first.U1);
            var hi=exactSide<2?(reversed?first.V0:last.V0):(reversed?first.U0:last.U0);
            domain=exactSide<2?new(uv.U0,uv.U1,lo,hi):new(lo,hi,uv.V0,uv.V1);
            return domain.Valid&&IsolatedRootCertificate.Contains(domain,uv);
        }
        var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(record.Class!=CurveClass.BCurve||Surfaces[surface].Class!=SurfaceClass.BSurface)return false;
        var c=GetBCurveView(BCurveDataStore[record.DataIndex]);var s=GetBSurfaceView(BSurfaceDataStore[Surfaces[surface].DataIndex]);
        if(c.Rational||s.Rational||c.Dimension!=3||s.Dimension!=3||c.VertexCount!=c.Degree+1
            ||s.UVertexCount!=s.UDegree+1||s.VVertexCount!=s.VDegree+1||!NativeClamped(c.Knots,c.Degree)
            ||!NativeClamped(s.UKnots,s.UDegree)||!NativeClamped(s.VKnots,s.VDegree))return false;
        var coordinate=-1;
        for(var d=0;d<3;d++)if(c.Vertices[d]!=c.Vertices[3*c.Degree+d]){if(coordinate>=0)return false;coordinate=d;}
        if(coordinate<0)return false;
        for(var i=0;i<c.VertexCount;i++)for(var d=0;d<3;d++)if(d!=coordinate&&c.Vertices[3*i+d]!=c.Vertices[d])return false;
        Span<IntervalNumber> cp=stackalloc IntervalNumber[4*17],sp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],values=stackalloc IntervalNumber[9];
        if(BezierCurveBounds.Extract(c,c.Degree,cp)!=AlgorithmStatus.Success||BezierSurfaceBounds.Extract(s,s.UDegree,s.VDegree,sp)!=AlgorithmStatus.Success)return false;
        var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(cp[..(4*(c.Degree+1))],c.Degree,0,new(c.Start,c.End,0,1)));
        if(source.Bound(new(c.Start,c.End,0,0),values)!=AlgorithmStatus.Success||values[3+coordinate].ContainsZero)return false;
        var increasing=values[3+coordinate].Lower>0;
        var whole=new SurfaceIntersectionPatch(new BezierSurfacePatch(sp[..(4*(s.UDegree+1)*(s.VDegree+1))],s.UDegree,s.VDegree,new(s.UStart,s.UEnd,s.VStart,s.VEnd)));
        for(var side=0;side<4;side++)
        {
            var degree=side<2?s.VDegree:s.UDegree;var same=true;
            for(var i=0;i<=degree;i++)for(var d=0;d<3;d++)if(d!=coordinate&&s.Vertices[At(side,i,s.UVertexCount,s.VVertexCount)+d]!=c.Vertices[d])same=false;
            if(!same)continue;
            var first=side<2?s.VStart:s.UStart;var last=side<2?s.VEnd:s.UEnd;
            var fixedValue=side switch{0=>s.UStart,1=>s.UEnd,2=>s.VStart,_=>s.VEnd};
            var target=new SurfaceIntersectionPatch(whole,side<2,fixedValue,first,last);
            if(target.Bound(new(first,last,0,0),values)!=AlgorithmStatus.Success||values[3+coordinate].ContainsZero)continue;
            var targetIncreasing=values[3+coordinate].Lower>0;
            var x0=s.Vertices[At(side,0,s.UVertexCount,s.VVertexCount)+coordinate];var x1=s.Vertices[At(side,degree,s.UVertexCount,s.VVertexCount)+coordinate];
            var source0=c.Vertices[coordinate];var source1=c.Vertices[3*c.Degree+coordinate];
            if(Math.Min(source0,source1)<Math.Min(x0,x1)||Math.Max(source0,source1)>Math.Max(x0,x1))continue;
            if(source.Bound(new(root.Lower,root.Upper,0,0),values)!=AlgorithmStatus.Success)return false;
            // Full finite carrier inclusion above licenses restriction of an
            // outward coordinate enclosure to the target's exact endpoint range.
            var x=new IntervalNumber(Math.Max(Math.Min(x0,x1),values[coordinate].Lower),Math.Min(Math.Max(x0,x1),values[coordinate].Upper));
            if(!InvertNativeBoundaryCoordinate(target,coordinate,x.Lower,first,last,x0,x1,out var a)
                ||!InvertNativeBoundaryCoordinate(target,coordinate,x.Upper,first,last,x0,x1,out var b))return false;
            var outer=targetIncreasing?new IntervalNumber(a.Lower,b.Upper):new(b.Lower,a.Upper);
            var lo=Math.Max(c.Start,unique.Lower);var hi=Math.Min(c.End,unique.Upper);
            if(!(hi>lo)||source.Bound(new(lo,lo,0,0),values)!=AlgorithmStatus.Success)return false;
            var left=values[coordinate];if(lo==c.Start)left=source0;
            if(source.Bound(new(hi,hi,0,0),values)!=AlgorithmStatus.Success)return false;
            var right=values[coordinate];if(hi==c.End)right=source1;
            var innerX=increasing?new IntervalNumber(left.Upper,right.Lower):new(right.Upper,left.Lower);
            if(!innerX.Finite||!InvertNativeBoundaryCoordinate(target,coordinate,innerX.Lower,first,last,x0,x1,out a)
                ||!InvertNativeBoundaryCoordinate(target,coordinate,innerX.Upper,first,last,x0,x1,out b))return false;
            var inner=targetIncreasing?new IntervalNumber(a.Upper,b.Lower):new(b.Upper,a.Lower);
            if(!outer.Finite||!inner.Finite||inner.Lower>outer.Lower||inner.Upper<outer.Upper)return false;
            uv=side<2?new(fixedValue,fixedValue,outer.Lower,outer.Upper):new(outer.Lower,outer.Upper,fixedValue,fixedValue);
            domain=side<2?new(fixedValue,fixedValue,inner.Lower,inner.Upper):new(inner.Lower,inner.Upper,fixedValue,fixedValue);
            boundary=(byte)(side+1);return true;
        }
        return false;
        static BufferOffset At(BufferOffset side,BufferOffset i,BufferCount nu,BufferCount nv)=>3*(side<2?((side==0?0:nu-1)*nv+i):i*nv+(side==2?0:nv-1));
        static bool NativeClamped(ReadOnlySpan<double> knots,SplineDegree degree)
        {if(knots.Length!=2*(degree+1)||!(knots[^1]>knots[0]))return false;for(var i=0;i<=degree;i++)if(knots[i]!=knots[0]||knots[knots.Length-1-i]!=knots[^1])return false;return true;}
    }

    private static bool InvertNativeBoundaryCoordinate(in SurfaceIntersectionPatch target,BufferOffset coordinate,double value,
        double first,double last,double x0,double x1,out IntervalNumber parameter)
    {
        parameter=default;if(value==x0){parameter=new(first);return true;}if(value==x1){parameter=new(last);return true;}
        if(!(value>Math.Min(x0,x1)&&value<Math.Max(x0,x1)))return false;
        var point=new KernelVector3{X=coordinate==0?value:0,Y=coordinate==1?value:0,Z=coordinate==2?value:0};
        var axis=new KernelVector3{X=coordinate==0?1:0,Y=coordinate==1?1:0,Z=coordinate==2?1:0};
        var plane=new AnalyticSurface(SurfaceClass.Plane,point,axis,default);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[1];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[1];var count=0;
        var status=SurfaceIsoPlaneRoots.Find(target,plane,first,last,roots,ref count,strictSeparation:true,isolation:proof);
        if(status!=AlgorithmStatus.Success||count!=1||!proof[0].Certified)return false;parameter=proof[0].Root;return true;
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestNativeSplineBoundaryRoot(CurveTag curve,SurfTag surface,in IntervalNumber root,in IntervalNumber unique,
        out SurfaceParameterBox uv,out SurfaceParameterBox domain)
    {
        var view=GetBCurveView(BCurveDataStore[Curves[GetCurveSlotByTag(curve)].DataIndex]);
        return BoundNativeSplineBoundaryRoot(new(){Curve=curve,Start=view.Start,End=view.End},GetSurfaceSlotByTag(surface),root,unique,out uv,out domain,out _);
    }
#endif

    private static bool TryExactNativeSplineBoundaryContact(in ModelingEdge edge,SurfaceSlot surface,out BufferOffset side,out bool reversed)
    {
        side=-1;reversed=false;
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
                if(matches){side=boundary;reversed=reverse!=0;return true;}
                if(!TryExactNativeAffineCurve(curve,out _))continue;
                matches=true;
                for(var i=0;i<=degree&&matches;i++)for(var d=0;d<3;d++)
                {
                    var first=c.Vertices[c.Dimension*(reverse==0?0:c.Degree)+d];var last=c.Vertices[c.Dimension*(reverse==0?c.Degree:0)+d];
                    var residual=new IntervalNumber(s.Vertices[At(boundary,i,s.UVertexCount,s.VVertexCount)+d])*weight*degree
                        -new IntervalNumber(first)*(degree-i)-new IntervalNumber(last)*i;
                    matches&=residual.Magnitude==0;
                }
                if(matches){side=boundary;reversed=reverse!=0;return true;}
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
