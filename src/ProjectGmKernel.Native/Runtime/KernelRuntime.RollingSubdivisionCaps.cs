using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RollingCapRoot
    {
        internal IntervalNumber Station,DistanceSquared;
        internal IntervalVector Center;
        internal bool Alternate;
    }

    // Two equal intersecting cylinders have two offset conics. Each finite
    // support occurrence independently supplies its nearest normal-cap root.
    // A pair of roots is an end only when it has a common rolling center.
    private static bool TryRollingSubdivisionCap(in RollingBandCandidate band,in RollingSubdivision split,
        in RegularBoundaryView input,ReadOnlySpan<FaceSlot> sources,
        out bool continued)
    {
        continued=false;
        var record=Surfaces[GetSurfaceSlotByTag(band.Surface)];
        if(record.Class!=SurfaceClass.BlendSurface)return true;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        var root=program.Surfaces[data.ProgramRoot];var a=program.Surfaces[root.First];var b=program.Surfaces[root.Second];
        var spine=program.Spines[root.Spine].Curve;
        if(!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(Edges[split.Before].CurveTag)],out var source)
            ||SpineParameterMap.Interval(source,new(split.Parameter,0),out var vertex,out var tangent)!=AlgorithmStatus.Success
            ||!TryOtherRollingConic(spine,a,b,root.Range0,root.Range1,out var alternate))return false;
        var first=sources.IndexOf(band.FirstSupport);var second=sources.IndexOf(band.SecondSupport);
        if(first<0||second<0)return false;
        if(!TryNearestRollingCapRoot(input,first,band.FirstSupport,a,root.Range0,band.Radius,spine,alternate,vertex,tangent,split,out var left)
            ||!TryNearestRollingCapRoot(input,second,band.SecondSupport,b,root.Range1,band.Radius,spine,alternate,vertex,tangent,split,out var right))
            return false;
        if(!left.Alternate&&!right.Alternate)
        {
            var firstStation=LiftRollingCapStation(left.Station,split.Station);var secondStation=LiftRollingCapStation(right.Station,split.Station);
            return Math.Max(split.StationLower,Math.Max(firstStation.Lower,secondStation.Lower))
                <=Math.Min(split.StationUpper,Math.Min(firstStation.Upper,secondStation.Upper));
        }
        continued=(left.Center-right.Center).Norm().Value.Lower>OperationIntersection.DistanceTolerance;
        return continued;
    }

    private static IntervalNumber LiftRollingCapStation(IntervalNumber station,double anchor)
        =>station+new IntervalNumber(Math.BitDecrement(Math.Tau),Math.BitIncrement(Math.Tau))*Math.Round((anchor-station.Midpoint)/Math.Tau);

    private static bool TryOtherRollingConic(in ParametricSpineCurve spine,in BlendSurfaceInstruction a,in BlendSurfaceInstruction b,
        double ra,double rb,out ParametricSpineCurve other)
    {
        other=default;
        if(spine.Kind!=CurveClass.Ellipse||a.Analytic.Kind!=SurfaceClass.Cylinder||b.Analytic.Kind!=SurfaceClass.Cylinder
            ||a.Analytic.Radius!=b.Analytic.Radius)return false;
        var signA=IntervalVector.Dot(new(spine.First),new(a.Analytic.Axis)).Value;
        var signB=IntervalVector.Dot(new(spine.First),new(b.Analytic.Axis)).Value;
        if(signA.ContainsZero||signB.ContainsZero)return false;
        // Equal radii factor the common cylinder equations into the two
        // axial bisector planes. Select the plane not used by this spine.
        var sign=(signA.Lower>0)==(signB.Lower>0)?1:-1;
        var axis=Sub(a.Analytic.Axis,Scale(b.Analytic.Axis,sign));if(!ProfileNormalize(ref axis))return false;
        var status=OffsetAnalyticBlendSupport(a.Analytic,ra*a.NormalSense,out var oa);if(status!=AlgorithmStatus.Success)return false;
        status=OffsetAnalyticBlendSupport(b.Analytic,rb*b.NormalSense,out var ob);
        if(status!=AlgorithmStatus.Success||oa.Radius!=ob.Radius)return false;
        var radial=ProfileCross(axis,oa.Axis);var norm=OperationIntersection.Norm(radial);if(!(norm>0))return false;
        var otherA=IntervalVector.Dot(new(axis),new(oa.Axis)).Value;
        var otherB=IntervalVector.Dot(new(axis),new(ob.Axis)).Value;
        if(otherA.ContainsZero||otherB.ContainsZero||((otherA.Lower>0)==(otherB.Lower>0))==(sign>0))return false;
        other=spine;other.First=Scale(axis,oa.Radius/norm);
        return SpineParameterMap.Certify(other,oa,0,Math.Tau)==AlgorithmStatus.Success
            &&SpineParameterMap.Certify(other,ob,0,Math.Tau)==AlgorithmStatus.Success;
    }

    private static bool TryNearestRollingCapRoot(in RegularBoundaryView input,BufferOffset face,FaceSlot source,
        in BlendSurfaceInstruction support,double offset,double radius,in ParametricSpineCurve spine,in ParametricSpineCurve other,
        in IntervalVector vertex,in IntervalVector tangent,in RollingSubdivision split,out RollingCapRoot nearest)
    {
        nearest=default;
        if(AnalyticSpineContact.Curve(spine,support,offset,out var contact)!=AlgorithmStatus.Success
            ||AnalyticSpineContact.Curve(other,support,offset,out var alternate)!=AlgorithmStatus.Success
            ||SpineParameterMap.Interval(contact,new(new(split.StationLower,split.StationUpper),0),out var known,out _)!=AlgorithmStatus.Success)
            return false;
        var axis=new IntervalVector(support.Analytic.Axis);var radial=vertex-new IntervalVector(support.Analytic.Origin);
        radial-=axis*IntervalVector.Dot(radial,axis);
        var direction=IntervalVector.Cross(tangent,radial).Unit();
        var sign=IntervalVector.Dot(direction,known-vertex).Value;
        if(!direction.Finite||sign.ContainsZero)return false;
        var seed=vertex+direction*(sign.Lower>0?radius:-radius);
        var precision=OperationIntersection.DistanceTolerance;
        if(!TryThirdFacePrecision(source,ref precision))return false;
        Span<RollingCapRoot> candidates=stackalloc RollingCapRoot[4];BufferCount count=0;
        Span<IntervalNumber> roots=stackalloc IntervalNumber[2];
        for(var branch=0;branch<2;branch++)
        {
            var curve=branch==0?contact:alternate;var center=branch==0?spine:other;
            if(!TryIsolateConicSectionRoots(curve,vertex,tangent,roots,out var n))return false;
            for(var i=0;i<n;i++)
            {
                var station=roots[i];
                if(SpineParameterMap.Interval(curve,new(station,0),out var point,out _)!=AlgorithmStatus.Success
                    ||!TryClassifyRollingCapRoot(input,face,point,precision,out var inside))return false;
                if(!inside)continue;
                if(SpineParameterMap.Interval(center,new(station,0),out var rolling,out _)!=AlgorithmStatus.Success)return false;
                var d=point-seed;
                candidates[count++]=new(){Station=station,Alternate=branch!=0,Center=rolling,
                    DistanceSquared=IntervalNumber.Square(d.X.Value)+IntervalNumber.Square(d.Y.Value)+IntervalNumber.Square(d.Z.Value)};
            }
        }
        for(var i=0;i<count;i++)
        {
            var wins=true;
            for(var j=0;j<count&&wins;j++)if(j!=i)wins=candidates[i].DistanceSquared.Upper<candidates[j].DistanceSquared.Lower;
            if(wins){nearest=candidates[i];return true;}
        }
        return false;
    }

    // A*cos(t)+B*sin(t)+C=0 has exactly two simple roots per turn
    // when A^2+B^2-C^2 is positive. Isolate both by signs; an estimate
    // only proposes a bracket. A tangent/unordered pair stays unresolved.
    private static bool TryIsolateConicSectionRoots(in ParametricSpineCurve curve,in IntervalVector vertex,in IntervalVector tangent,
        Span<IntervalNumber> roots,out BufferCount count)
    {
        count=0;
        var a=IntervalVector.Dot(new(curve.First),tangent).Value;
        var b=IntervalVector.Dot(new(curve.Second),tangent).Value;
        var c=IntervalVector.Dot(new IntervalVector(curve.Origin)-vertex,tangent).Value;
        var amplitude=IntervalNumber.Square(a)+IntervalNumber.Square(b);
        var discriminant=amplitude-IntervalNumber.Square(c);
        if(discriminant.Upper<0)return true;
        if(!(discriminant.Lower>0))return false;
        var phase=Math.Atan2(b.Midpoint,a.Midpoint);
        var angle=Math.Acos(Math.Clamp(-c.Midpoint/Math.Sqrt(amplitude.Midpoint),-1,1));
        for(var i=0;i<2;i++)
        {
            var estimate=phase+(i==0?-angle:angle);
            var width=16*SmallLinearSolve.MachineEpsilon*(1+Math.Abs(estimate));var found=false;
            for(var step=0;step<24;step++,width*=2)
            {
                var lo=Math.BitDecrement(estimate-width);var hi=Math.BitIncrement(estimate+width);
                var domain=new IntervalNumber(lo,hi);
                if(SectionSlope(a,b,domain).ContainsZero)break;
                var left=SectionHeight(a,b,c,new(lo));var right=SectionHeight(a,b,c,new(hi));
                if(!(left.Upper<0&&right.Lower>0||right.Upper<0&&left.Lower>0))continue;
                roots[i]=domain;found=true;break;
            }
            if(!found)return false;
        }
        var distance=Math.Abs(Math.IEEERemainder(roots[1].Midpoint-roots[0].Midpoint,Math.Tau));
        if(distance<=roots[1].Upper-roots[1].Lower+roots[0].Upper-roots[0].Lower+16*SmallLinearSolve.MachineEpsilon)return false;
        count=2;return true;
    }

    private static bool TryClassifyRollingCapRoot(in RegularBoundaryView input,BufferOffset face,in IntervalVector point,double precision,out bool inside)
    {
        inside=false;var record=input.Faces[face];
        var bounds=new IntervalBox3(point.X.Value.Lower,point.X.Value.Upper,point.Y.Value.Lower,point.Y.Value.Upper,point.Z.Value.Lower,point.Z.Value.Upper);
        Span<IntervalNumber> pending=stackalloc IntervalNumber[40];
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                var edge=input.Edges[use.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];BufferCount n=1;var budget=512;
                pending[0]=new(edge.Start,edge.End);
                while(n>0)
                {
                    if(--budget<0)return false;
                    var cell=pending[--n];
                    if(!TryFiniteCarrierExtent(new(){Curve=edge.Curve,First=cell.Lower,Last=cell.Upper},curve,out var box))return false;
                    if(ThirdFaceBoxesDisjoint(bounds,box,precision))continue;
                    var mid=cell.Midpoint;if(!(mid>cell.Lower&&mid<cell.Upper)||n>pending.Length-2)return false;
                    pending[n++]=new(cell.Lower,mid);pending[n++]=new(mid,cell.Upper);
                }
            }
        }
        bounds.Midpoint(out var position);
        return IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(record.Surface)],position,out var uv)==AlgorithmStatus.Success
            &&RegularFaceContains(input,face,uv,out inside)==0;
    }

    private static bool RollingContactCoversSubdivision(in RollingBandCandidate band,BufferOffset support,BufferOffset bandFace,
        in RollingSubdivision split,ReadOnlySpan<RegularFaceCut> cuts)
    {
        foreach(ref readonly var cut in cuts)
        {
            if(cut.First!=support||cut.Second!=bandFace||cut.SecondIsBoundary==0)continue;
            var edge=cut.Cut;var curve=Curves[GetCurveSlotByTag(edge.Curve)];
            if(!TryAnalyticBlendContactMap(band.Surface,curve,edge.First,edge.Last,out _,out var shift,out var scale))continue;
            var first=new IntervalNumber(shift)+new IntervalNumber(scale)*edge.First;var last=new IntervalNumber(shift)+new IntervalNumber(scale)*edge.Last;
            if(scale<0)(first,last)=(last,first);
            if(first.Upper<split.StationLower&&last.Lower>split.StationUpper)return true;
        }
        return false;
    }
}
