using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryAffinePlaneCrossing(SurfaceSlot first,SurfaceSlot second,Span<PlaneIsoBranch> sections,
        out SurfaceSlot spline,out SurfaceSlot plane)
    {
        spline=Surfaces[first].Class==SurfaceClass.BSurface?first:second;
        plane=spline==first?second:first;
        if(Surfaces[spline].Class!=SurfaceClass.BSurface||Surfaces[plane].Class!=SurfaceClass.Plane
            ||!TryPrepareSurface(Surfaces[plane],out var support))return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[spline].DataIndex]);
        // Both crossing families must admit an exact affine terminal span.
        if(view.Rational||view.UDegree!=1||view.VDegree!=1||view.UVertexCount!=2||view.VVertexCount!=2)return false;
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

    private static int PublishAffinePlaneCrossing(SurfaceSlot spline,SurfaceSlot plane,bool inputSplineFirst,BodySlot owner,ReadOnlySpan<PlaneIsoBranch> sections,
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
                error=BindAffinePlaneHalfBranch(spline,plane,inputSplineFirst,support,flat,a,b,sections[i],out tags[i]);
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

    private static int BindAffinePlaneHalfBranch(SurfaceSlot spline,SurfaceSlot plane,bool inputSplineFirst,in ICurveSupportView support,in ICurveSupportView flat,
        SurfTag splineTag,SurfTag planeTag,in PlaneIsoBranch section,out CurveTag tag)
    {
        tag=0;
        var start=section.StartSingular?section.Last:section.First;
        var end=section.StartSingular?section.First:section.Last;
        var branch=start+(end-start)*.5;
        if(branch==start||branch==end)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<double> stations=stackalloc double[3]{start,branch,end};
        Span<double> uv=stackalloc double[12];Span<KernelVector3> points=stackalloc KernelVector3[3];
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        for(var i=0;i<stations.Length;i++)
        {
            var o=4*i;uv[o]=section.FixedU?section.Fixed:stations[i];uv[o+1]=section.FixedU?stations[i]:section.Fixed;
            if(ICurveSupportEvaluation.Evaluate(support,uv[o],uv[o+1],layout,points[i..])!=AlgorithmStatus.Success
                ||OperationIntersection.Parameters(flat.Analytic,points[i],out uv[o+2],out uv[o+3])!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        if(!TryChartTangent(support,Surfaces[spline].Sense,uv[0],uv[1],
            flat,Surfaces[plane].Sense,uv[2],uv[3],points[0],out var tangent))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var turn=Dot(tangent,Sub(points[1],points[0]));
        if(!double.IsFinite(turn)||turn==0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var sense=(turn>0)==inputSplineFirst?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        // Each chart runs into its singular event. Order the defining
        // supports to agree with that half-branch's regular tangent.
        if(turn<0)
        {
            (splineTag,planeTag)=(planeTag,splineTag);
            for(var i=0;i<uv.Length;i+=4)
            {(uv[i],uv[i+2])=(uv[i+2],uv[i]);(uv[i+1],uv[i+3])=(uv[i+3],uv[i+1]);}
        }
        Span<double> chart=stackalloc double[6]{points[0].X,points[0].Y,points[0].Z,points[1].X,points[1].Y,points[1].Z};
        Span<double> terminal=stackalloc double[6]{points[2].X,points[2].Y,points[2].Z,points[1].X,points[1].Y,points[1].Z};
        var input=new IcurveDecodeInput{Surface0Tag=splineTag,Surface1Tag=planeTag,BaseScale=1,ChartCount=2,ChartHvecs=chart,
            UvType=IntersectionUvType.Both,UvValues=uv,AngularError=Math.PI,Sense=sense,
            Start=new(){Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart[..3]},
            End=new(){Type=LimitType.Terminator,TermUse=LimitTermUse.First,Hvecs=terminal}};
        var status=DecodeIcurve(input,out var data,out _,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        status=TryBindICurveEntity(data,out tag);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        ICurveDataPool[data].OwnsSupportSurfaces=1;return 0;
    }
}
