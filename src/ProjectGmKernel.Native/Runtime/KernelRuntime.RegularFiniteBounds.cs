using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A bounded planar face lies in the coordinate hull of its complete
    // boundary. Curved supports use their complete finite support domain.
    private static AlgorithmStatus RegularFiniteFaceExtent(FaceSlot face,out IntervalBox3 extent)
    {
        extent=default;var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0)return AlgorithmStatus.InvalidInput;
        if(Surfaces[slot].Class==SurfaceClass.Cylinder)return CertifiedCylindricalFaceExtent(face,out extent);
        if(Surfaces[slot].Class==SurfaceClass.Cone)return CertifiedConicalFaceExtent(face,out extent);
        if(Surfaces[slot].Class!=SurfaceClass.Plane)return NativeDefinedSurfaceExtent(slot,out extent);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17],bounds=stackalloc IntervalNumber[9];
        var first=true;var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var f=0;f<Loops[loop].FinCount;f++,fin=Fins[fin].NextInLoop)
            {
                var edge=Edges[Fins[fin].Edge];if(!TryProfileEdgeRange(edge,out var lo,out var hi))return AlgorithmStatus.Unsupported;
                var carrier=GetCurveSlotByTag(edge.CurveTag);if(carrier<0)return AlgorithmStatus.Unsupported;
                if(Curves[carrier].Class==CurveClass.ICurve)
                {
                    var owns=EnsureCommandScratch();AlgorithmStatus status;IntervalBox3 trim;
                    try{status=CertifiedIntersectionCurveExtent(edge.CurveTag,lo,hi,out trim);}
                    finally{if(owns)ReleaseCommandScratch(State.Session);}
                    if(status!=AlgorithmStatus.Success)return status;
                    bounds[0]=new(trim.XLo,trim.XHi);bounds[1]=new(trim.YLo,trim.YHi);bounds[2]=new(trim.ZLo,trim.ZHi);
                    IncludeRegularExtent(ref extent,ref first,bounds);continue;
                }
                var interval=new Generated.PK_INTERVAL_s();interval.value[0]=lo;interval.value[1]=hi;
                if(ResolveIntersectionCurve(edge.CurveTag,interval,out var curve)!=0)return AlgorithmStatus.Unsupported;
                var count=GeneralCurvePieceCount(curve);if(count<=0)return AlgorithmStatus.Unsupported;
                for(var p=0;p<count;p++)
                {
                    var status=GeneralCurvePiece(curve,p,poles,out var degree,out var domain,out var a,out var b);if(status!=AlgorithmStatus.Success)return status;
                    if(!(b>a))continue;
                    status=BezierSurfaceBounds.Bound(new(poles[..(4*(degree+1))],degree,0,domain),new(a,b,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                    IncludeRegularExtent(ref extent,ref first,bounds);
                }
            }
        }
        return first?AlgorithmStatus.Unsupported:AlgorithmStatus.Success;
    }

    private static bool RegularFinitePairSeparated(FaceSlot first,FaceSlot second,double tolerance,ref ThirdFaceBounds a,ref ThirdFaceBounds b)
    {
        if(!a.Checked)
        {
            a.Checked=true;a.Margin=tolerance;
            a.Available=TryThirdFacePrecision(first,ref a.Margin)&&RegularFiniteFaceExtent(first,out a.Box)==AlgorithmStatus.Success;
        }
        if(!b.Checked)
        {
            b.Checked=true;b.Margin=tolerance;
            b.Available=TryThirdFacePrecision(second,ref b.Margin)&&RegularFiniteFaceExtent(second,out b.Box)==AlgorithmStatus.Success;
        }
        if(!a.Available||!b.Available)return false;
        var margin=(new IntervalNumber(a.Margin)+b.Margin).Upper;
        return double.IsFinite(margin)&&ThirdFaceBoxesDisjoint(a.Box,b.Box,margin);
    }

    // A valid native face is a subset of its surface definition (FD 18.3,
    // 30.6, 96.1.4). Evaluation's linear continuation is not a model domain.
    // This proof is independent of the patch family an intersection searches.
    internal static AlgorithmStatus NativeDefinedSurfaceExtent(SurfaceSlot slot,out IntervalBox3 extent)
    {
        extent=default;if(slot<0)return AlgorithmStatus.InvalidInput;
        var record=Surfaces[slot];
        if(record.Class==SurfaceClass.BSurface)
            return BSurfaceDomainBounds.Position(GetBSurfaceView(BSurfaceDataStore[record.DataIndex]),
                new(record.UMin,record.UMax,record.VMin,record.VMax),allowLinearExtension:false,out extent);
        if(TryPrepareSurface(record,out var analytic))
        {
            if(analytic.Kind==SurfaceClass.Sphere){extent=AnalyticBallBounds(analytic.Origin,analytic.Radius);return AlgorithmStatus.Success;}
            if(analytic.Kind==SurfaceClass.Torus)
            {
                var radius=(new IntervalNumber(Math.Abs(analytic.Radius))+Math.Abs(analytic.Secondary)).Upper;
                if(!double.IsFinite(radius))return AlgorithmStatus.NumericalFailure;
                extent=AnalyticBallBounds(analytic.Origin,radius);return AlgorithmStatus.Success;
            }
            return AlgorithmStatus.Unsupported;
        }
        if(record.Class!=SurfaceClass.BlendSurface)return AlgorithmStatus.Unsupported;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data,record);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        if(charts.Length<2||!double.IsFinite(record.UMin)||!double.IsFinite(record.UMax)||!(record.UMax>record.UMin)
            ||!double.IsFinite(record.VMin)||!double.IsFinite(record.VMax)||record.VMin<0||record.VMax>1||!(record.VMax>record.VMin))return AlgorithmStatus.Unsupported;
        for(var i=0;i<charts.Length;i++)
            if(!double.IsFinite(charts[i].Parameter)||i>0&&charts[i].Parameter<=charts[i-1].Parameter)return AlgorithmStatus.Unsupported;
        if(record.UMin<charts[0].Parameter||record.UMax>charts[^1].Parameter)return AlgorithmStatus.Unsupported;
        return BlendExtent(program,data.ProgramRoot,new(record.UMin,record.UMax,record.VMin,record.VMax),out extent);
    }

    internal static bool RegularPointOutsideNativeSupport(FaceSlot face,in KernelVector3 point,double tolerance)
    {
        if(face<0||!double.IsFinite(tolerance)||tolerance<0)return false;
        tolerance=Math.Max(tolerance,Faces[face].Tolerance);
        if(!TryThirdFacePrecision(face,ref tolerance)
            ||NativeDefinedSurfaceExtent(GetSurfaceSlotByTag(Faces[face].SurfTag),out var box)!=AlgorithmStatus.Success)return false;
        return point.X<(new IntervalNumber(box.XLo)-tolerance).Lower||point.X>(new IntervalNumber(box.XHi)+tolerance).Upper
            ||point.Y<(new IntervalNumber(box.YLo)-tolerance).Lower||point.Y>(new IntervalNumber(box.YHi)+tolerance).Upper
            ||point.Z<(new IntervalNumber(box.ZLo)-tolerance).Lower||point.Z>(new IntervalNumber(box.ZHi)+tolerance).Upper;
    }

    private static bool TryNativeSolidExtent(BodyTag body,double tolerance,out IntervalBox3 extent)
    {
        extent=default;var slot=TagRec(body).Slot;var record=Bodies[slot];
        if(Xt.IsOpaque(slot)||record.BodyType!=Generated.ParasolidConstants.PK_BODY_type_solid_c
            ||record.FaceCountBody<=0||record.RegionCount<2||Regions[record.FirstRegion].IsSolid!=0)return false;
        var face=record.FirstFaceBody;var first=true;
        for(var i=0;i<record.FaceCountBody;i++,face=Faces[face].NextInBody)
        {
            var margin=tolerance;
            if(!TryThirdFacePrecision(face,ref margin)||RegularFiniteFaceExtent(face,out var box)!=AlgorithmStatus.Success)return false;
            var x=new IntervalNumber(box.XLo,box.XHi)+new IntervalNumber(-margin,margin);
            var y=new IntervalNumber(box.YLo,box.YHi)+new IntervalNumber(-margin,margin);
            var z=new IntervalNumber(box.ZLo,box.ZHi)+new IntervalNumber(-margin,margin);
            if(!x.Finite||!y.Finite||!z.Finite)return false;
            extent=first?new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper)
                :new(Math.Min(extent.XLo,x.Lower),Math.Max(extent.XHi,x.Upper),Math.Min(extent.YLo,y.Lower),Math.Max(extent.YHi,y.Upper),Math.Min(extent.ZLo,z.Lower),Math.Max(extent.ZHi,z.Upper));first=false;
        }
        return !first;
    }

    private static void IncludeRegularExtent(ref IntervalBox3 extent,ref bool first,ReadOnlySpan<IntervalNumber> bounds)
    {
        extent=first?new(bounds[0].Lower,bounds[0].Upper,bounds[1].Lower,bounds[1].Upper,bounds[2].Lower,bounds[2].Upper)
            :new(Math.Min(extent.XLo,bounds[0].Lower),Math.Max(extent.XHi,bounds[0].Upper),Math.Min(extent.YLo,bounds[1].Lower),Math.Max(extent.YHi,bounds[1].Upper),Math.Min(extent.ZLo,bounds[2].Lower),Math.Max(extent.ZHi,bounds[2].Upper));
        first=false;
    }
}
