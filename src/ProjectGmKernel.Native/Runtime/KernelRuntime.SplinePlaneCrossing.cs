using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TrySplinePlaneCrossing(SurfaceSlot first,SurfaceSlot second,Span<PlaneIsoBranch> sections,
        out SurfaceSlot spline,out SurfaceSlot plane)
    {
        spline=Surfaces[first].Class==SurfaceClass.BSurface?first:second;
        plane=spline==first?second:first;
        if(Surfaces[spline].Class!=SurfaceClass.BSurface||Surfaces[plane].Class!=SurfaceClass.Plane
            ||!TryPrepareSurface(Surfaces[plane],out var support))return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[spline].DataIndex]);
        if(view.Rational||view.UVertexCount!=view.UDegree+1||view.VVertexCount!=view.VDegree+1)return false;
        Span<OperationIntersectionBranch> exact=stackalloc OperationIntersectionBranch[sections.Length];
        Span<KernelVector3> points=stackalloc KernelVector3[1];
        if(IsoParametricBSurfacePlaneIntersection.Find(view,support,exact,out var count,points,out var isolated,sections)!=AlgorithmStatus.Success
            ||count!=sections.Length||isolated!=0)return false;
        for(var i=0;i<count;i++)
        {
            var section=sections[i];
            if(exact[i].Kind!=IntersectionCurveKind.Simple||section.StartSingular==section.EndSingular)return false;
            var lo=section.FixedU?view.VStart:view.UStart;var hi=section.FixedU?view.VEnd:view.UEnd;
            var terminal=section.StartSingular?section.First:section.Last;
            if(!(terminal>lo&&terminal<hi))return false;
        }
        return true;
    }

    private static int PublishSplinePlaneCrossing(SurfaceSlot spline,SurfaceSlot plane,bool inputSplineFirst,BodySlot owner,ReadOnlySpan<PlaneIsoBranch> sections,
        BufferCount* curveCount,CurveTag** curves,PK_INTERVAL_s** intervals,int** kinds)
    {
        if(TryPrepareICurveSupport(Surfaces[spline],out var support)!=AlgorithmStatus.Success
            ||TryPrepareICurveSupport(Surfaces[plane],out var flat)!=AlgorithmStatus.Success)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var error=TryCloneEntityCore(Surfaces[spline].Header.Tag,out var a);if(error!=0)return error;
        error=TryCloneEntityCore(Surfaces[plane].Header.Tag,out var b);if(error!=0)return error;
        var tags=AllocateReturnSlice(sections.Length);
        var ranges=(PK_INTERVAL_s*)State.Session->Returns.TryAllocate((nuint)sections.Length*(nuint)sizeof(PK_INTERVAL_s));
        var types=AllocateReturnSlice(sections.Length);
        if(tags==null||ranges==null||types==null)error=ParasolidConstants.PK_ERROR_memory_full;
        for(var i=0;i<sections.Length&&error==0;i++)
        {
            var checkpoint=CommandScratch.ThreadArenaOffset;
            try
            {
                error=BindSplinePlaneHalfBranch(spline,plane,inputSplineFirst,support,flat,a,b,sections[i],out tags[i]);
                if(error==0&&owner>=0)error=TryRegisterConstructionGeometry(owner,tags[i]);
                if(error!=0)break;
                ref readonly var record=ref Curves[GetCurveSlotByTag(tags[i])];
                ranges[i].value[0]=record.TMin;ranges[i].value[1]=record.TMax;
                types[i]=ParasolidConstants.PK_intersect_curve_simple_c;
            }
            finally{CommandScratch.ThreadArenaOffset=checkpoint;}
        }
        if(error!=0)
        {State.Session->Returns.TryFree(tags);State.Session->Returns.TryFree(ranges);State.Session->Returns.TryFree(types);return error;}
        *curveCount=sections.Length;*curves=tags;*intervals=ranges;*kinds=types;return 0;
    }

    private static int BindSplinePlaneHalfBranch(SurfaceSlot spline,SurfaceSlot plane,bool inputSplineFirst,in ICurveSupportView support,in ICurveSupportView flat,
        SurfTag splineTag,SurfTag planeTag,in PlaneIsoBranch section,out CurveTag tag)
    {
        tag=0;
        var start=section.StartSingular?section.Last:section.First;
        var end=section.StartSingular?section.First:section.Last;
        var branch=start+(end-start)*.5;
        if(branch==start||branch==end)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<double> uv=stackalloc double[24];Span<KernelVector3> points=stackalloc KernelVector3[6];
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        var affine=(section.FixedU?support.BSpline.VDegree:support.BSpline.UDegree)==1;
        var regularCount=affine?2:5;var last=regularCount-1;
        if(!SplineStation(support,flat,section,start,layout,uv,points)
            ||!SplineStation(support,flat,section,end,layout,uv[(4*regularCount)..],points[regularCount..])
            ||!SplineStation(support,flat,section,branch,layout,uv[(4*last)..],points[last..]))
            return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!TryChartTangent(support,Surfaces[spline].Sense,uv[0],uv[1],
            flat,Surfaces[plane].Sense,uv[2],uv[3],points[0],out var tangent))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var turn=Dot(tangent,Sub(points[last],points[0]));
        if(!double.IsFinite(turn)||turn==0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var sense=(turn>0)==inputSplineFirst?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        var chordalError=0.0;
        if(!affine)
        {
            var ready=false;
            for(var attempt=0;attempt<20;attempt++)
            {
                var limit=new TerminatorLimit(turn>0?LimitTermUse.First:LimitTermUse.Second,points[regularCount],points[last]);
                var at=new SampleWitness(uv[4*last],uv[4*last+1],uv[4*last+2],uv[4*last+3],0,3);
                var prior=new SampleWitness(uv[0],uv[1],uv[2],uv[3],0,3);
                // Reserve the remainder of the edge tolerance for the
                // independent geometry and the native side of the join.
                if(SplineTerminalSection.Certify(support,flat,limit,points[last],at,prior,out var proof)
                    &&proof.Departure<=OperationIntersection.DistanceTolerance/8){chordalError=proof.Departure;ready=true;break;}
                var next=branch+(end-branch)*.5;
                if(next==branch||next==end||!SplineStation(support,flat,section,next,layout,uv[(4*last)..],points[last..]))break;
                branch=next;
            }
            if(!ready)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            for(var i=1;i<last;i++)
                if(!SplineStation(support,flat,section,start+(branch-start)*i/last,layout,uv[(4*i)..],points[i..]))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(!BoundRegularSplineChart(support,section,uv[..(4*regularCount)],points[..regularCount],ref chordalError))
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        // Each chart runs into its singular event. Order the defining
        // supports to agree with that half-branch's regular tangent.
        if(turn<0)
        {
            (splineTag,planeTag)=(planeTag,splineTag);
            for(var i=0;i<4*(regularCount+1);i+=4)
            {(uv[i],uv[i+2])=(uv[i+2],uv[i]);(uv[i+1],uv[i+3])=(uv[i+3],uv[i+1]);}
        }
        Span<double> chart=stackalloc double[15];
        for(var i=0;i<regularCount;i++){chart[3*i]=points[i].X;chart[3*i+1]=points[i].Y;chart[3*i+2]=points[i].Z;}
        var endpoint=points[regularCount];var before=points[last];
        Span<double> terminal=stackalloc double[6]{endpoint.X,endpoint.Y,endpoint.Z,before.X,before.Y,before.Z};
        var input=new IcurveDecodeInput{Surface0Tag=splineTag,Surface1Tag=planeTag,BaseScale=1,ChartCount=regularCount,ChartHvecs=chart[..(3*regularCount)],
            UvType=IntersectionUvType.Both,UvValues=uv[..(4*(regularCount+1))],ChordalError=chordalError,AngularError=Math.PI,Sense=sense,
            Start=new(){Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart[..3]},
            End=new(){Type=LimitType.Terminator,TermUse=LimitTermUse.First,Hvecs=terminal}};
        var status=DecodeIcurve(input,out var data,out _,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        status=TryBindICurveEntity(data,out tag);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        ICurveDataPool[data].OwnsSupportSurfaces=1;return 0;
    }
    private static bool SplineStation(in ICurveSupportView support,in ICurveSupportView flat,in PlaneIsoBranch section,
        double station,in SurfaceDerivativeLayout layout,Span<double> uv,Span<KernelVector3> point)
    {
        uv[0]=section.FixedU?section.Fixed:station;uv[1]=section.FixedU?station:section.Fixed;
        return ICurveSupportEvaluation.Evaluate(support,uv[0],uv[1],layout,point)==AlgorithmStatus.Success
            &&OperationIntersection.Parameters(flat.Analytic,point[0],out uv[2],out uv[3])==AlgorithmStatus.Success;
    }

    private static bool BoundRegularSplineChart(in ICurveSupportView support,in PlaneIsoBranch section,
        ReadOnlySpan<double> uv,ReadOnlySpan<KernelVector3> points,ref double error)
    {
        ref readonly var bs=ref support.BSpline;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        if(BezierSurfaceBounds.Extract(bs,bs.UDegree,bs.VDegree,poles)!=AlgorithmStatus.Success)return false;
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*bs.UVertexCount*bs.VVertexCount)],
            bs.UDegree,bs.VDegree,new(bs.UStart,bs.UEnd,bs.VStart,bs.VEnd)));
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        for(var i=0;i<points.Length-1;i++)
        {
            var k=section.FixedU?1:0;var a=uv[4*i+k];var b=uv[4*(i+1)+k];
            var lo=Math.Min(a,b);var hi=Math.Max(a,b);
            var box=section.FixedU?new SurfaceParameterBox(section.Fixed,section.Fixed,lo,hi):new(lo,hi,section.Fixed,section.Fixed);
            if(patch.Bound(box,jet)!=AlgorithmStatus.Success)return false;
            var d=jet.Slice(section.FixedU?6:3,3);var chord=Sub(points[i+1],points[i]);
            var slope=d[0]*chord.X+d[1]*chord.Y+d[2]*chord.Z;
            if(!(a<b?slope.Lower>0:slope.Upper<0))return false;
            var dx=new IntervalNumber(d[0].Upper)-d[0].Lower;var dy=new IntervalNumber(d[1].Upper)-d[1].Lower;
            var dz=new IntervalNumber(d[2].Upper)-d[2].Lower;
            var squared=IntervalNumber.Square(dx)+IntervalNumber.Square(dy)+IntervalNumber.Square(dz);
            var bound=((new IntervalNumber(hi)-lo)*.25*Math.BitIncrement(Math.Sqrt(squared.Upper))
                +ICurveEvaluation.ResidualTolerance*support.LengthScale).Upper;
            if(!double.IsFinite(bound))return false;
            error=Math.Max(error,bound);
        }
        return true;
    }
}
