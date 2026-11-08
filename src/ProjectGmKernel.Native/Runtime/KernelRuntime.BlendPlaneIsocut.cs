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
    internal enum BlendPlaneCutStage:byte{None,Bounds,Monotonicity,Contacts,Section,Ready,Empty}
    [ThreadStatic] internal static BlendPlaneCutStage LastBlendPlaneCutStage;
    [ThreadStatic] internal static SurfTag LastBlendPlaneCutPlane;
    [ThreadStatic] internal static BufferCount LastBlendPlaneFirstRoots,LastBlendPlaneSecondRoots;
    // Recognize a complete circular section only after proving a common
    // station at both contacts and strict u monotonicity over the whole strip.
    // Other regular branches continue through the general SSI implementation.
    private static int TryPrepareBlendPlaneIsocut(SurfTag surface,SurfTag planeTag,Span<SheetCutCurve> cuts,
        out BufferCount count,out bool handled,out FiniteSectionChart section)
    {
        count=0;handled=false;section=default;LastBlendPlaneCutStage=BlendPlaneCutStage.Bounds;LastBlendPlaneCutPlane=planeTag;
        LastBlendPlaneFirstRoots=LastBlendPlaneSecondRoots=0;
        var slot=GetSurfaceSlotByTag(surface);var record=Surfaces[slot];
        TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(planeTag)],out var plane);
        if(CertifyExplicitRollingBallPlaneSeparation(record,plane))
        {handled=true;LastBlendPlaneCutStage=BlendPlaneCutStage.Empty;return 0;}
        // Procedural patches carry their own certified parameter boxes; they
        // do not need a single global tube bound before chart subdivision.
        AlgorithmStatus status;
        Span<OperationIntersectionPoint> first=stackalloc OperationIntersectionPoint[64],second=stackalloc OperationIntersectionPoint[64];var nf=0;var ns=0;
        status=CertifyBlendPlaneRange(record,plane,false,out var positive,out var negative);if(status!=AlgorithmStatus.Success)return 0;
        if(positive||negative){handled=true;LastBlendPlaneCutStage=BlendPlaneCutStage.Empty;return 0;}
        LastBlendPlaneCutStage=BlendPlaneCutStage.Monotonicity;
        status=CertifyBlendPlaneRange(record,plane,true,out var increasing,out var decreasing);if(status!=AlgorithmStatus.Success)return 0;
        if(!increasing&&!decreasing)return 0;
        LastBlendPlaneCutStage=BlendPlaneCutStage.Contacts;
        status=SolveMonotoneBlendPlaneContact(record,plane,record.VMin,first,out nf);if(status!=AlgorithmStatus.Success)return 0;
        status=SolveMonotoneBlendPlaneContact(record,plane,record.VMax,second,out ns);if(status!=AlgorithmStatus.Success)return 0;
        LastBlendPlaneFirstRoots=nf;LastBlendPlaneSecondRoots=ns;
        if(nf!=1||ns!=1||Math.Abs(first[0].First-second[0].First)>1e-10)return 0;
        LastBlendPlaneCutStage=BlendPlaneCutStage.Section;
        var u=(first[0].First+second[0].First)*.5;
        var error=NestedBlendIsoparam(surface,true,u,record.VMin,record.VMax,out var circle,out var lower,out var upper);if(error!=0)return error;
        var data=CircleDataPool[Curves[GetCurveSlotByTag(circle)].DataIndex];
        if(OperationIntersection.Norm(ProfileCross(Vector(data.AxisX,data.AxisY,data.AxisZ),plane.Axis))>1e-12
            ||Math.Abs(Dot(Sub(Vector(data.CenterX,data.CenterY,data.CenterZ),plane.Origin),plane.Axis))>1e-10)
        {error=EntityDelete(1,&circle);return error;}
        if(cuts.IsEmpty)return ParasolidConstants.PK_ERROR_not_implemented;
        // PK's circular cut boundary uses a negative curve use. The sheet
        // arrangement maps its endpoints/coedges through that use sense.
        Curves[GetCurveSlotByTag(circle)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
        cuts[0]=new(){Curve=circle,First=lower,Last=upper};
        section=MakeFiniteSectionChart(surface,circle,u,lower,upper,record.VMin,record.VMax,planeTag);
        count=1;handled=true;LastBlendPlaneCutStage=BlendPlaneCutStage.Ready;return 0;
    }

    private static bool CertifyExplicitRollingBallPlaneSeparation(in SurfaceRecord surface,in AnalyticSurface plane)
    {
        var data=BlendedEdgeDataPool[surface.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];var spine=program.Spines[root.Spine];
        if(!SpineParameterMap.Explicit(spine))return false;
        var status=SpineParameterMap.Interval(spine.Curve,new IntervalDual(new IntervalNumber(surface.UMin,surface.UMax),1),out var centers,out _);
        if(status!=AlgorithmStatus.Success)return false;
        var normal=new IntervalVector(plane.Axis);var height=IntervalVector.Dot(centers-new IntervalVector(plane.Origin),normal).Value;
        var radius=normal.Norm().Value*Math.Abs(root.Range0);
        var envelope=height+new IntervalNumber(-radius.Upper,radius.Upper);
        return envelope.Finite&&(envelope.Lower>0||envelope.Upper<0);
    }

    // Called only after a complete strip certificate proved strict u
    // monotonicity. Endpoint signs prove existence; Newton is a candidate
    // locator and a final interval residual is still required to publish it.
    private static AlgorithmStatus SolveMonotoneBlendPlaneContact(in SurfaceRecord surface,in AnalyticSurface plane,double v,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=BlendedEdgeDataPool[surface.DataIndex];var program=GetNestedBlendProgram(data);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(surface.UMin,surface.UMin,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        var left=(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
        status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(surface.UMax,surface.UMax,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        var right=(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
        var u=surface.UMin;
        if(left.Magnitude<=1e-10)u=surface.UMin;
        else if(right.Magnitude<=1e-10)u=surface.UMax;
        else if(left.Lower>0&&right.Lower>0||left.Upper<0&&right.Upper<0)return AlgorithmStatus.Success;
        else if(left.Upper<0&&right.Lower>0||left.Lower>0&&right.Upper<0)
        {
            u=surface.UMin+(surface.UMax-surface.UMin)*.5;SurfaceDerivativeLayout.TryCreate(1,0,out var layout);
            Span<KernelVector3> jet=stackalloc KernelVector3[2];var found=false;
            for(var i=0;i<20;i++)
            {
                status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,u,v,layout,jet);if(status!=AlgorithmStatus.Success)return status;
                var distance=Dot(Sub(jet[0],plane.Origin),plane.Axis);if(Math.Abs(distance)<1e-12){found=true;break;}
                var derivative=Dot(jet[1],plane.Axis);if(Math.Abs(derivative)<1e-14)return AlgorithmStatus.NotConverged;
                u=Math.Clamp(u-distance/derivative,surface.UMin,surface.UMax);
            }
            if(!found)return AlgorithmStatus.NotConverged;
        }
        else return AlgorithmStatus.NotConverged;
        status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(u,u,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        var residual=(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
        if(residual.Magnitude>1e-10)return AlgorithmStatus.NotConverged;
        var point=new KernelVector3{X=bounds[0].Midpoint,Y=bounds[1].Midpoint,Z=bounds[2].Midpoint};
        return OperationIntersection.AddPoint(output,ref count,point,u,0,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
    }

    private static AlgorithmStatus CertifyBlendPlaneRange(in SurfaceRecord surface,in AnalyticSurface plane,bool derivative,
        out bool positive,out bool negative)
    {
        positive=negative=true;var data=BlendedEdgeDataPool[surface.DataIndex];var program=GetNestedBlendProgram(data);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        Span<SurfaceParameterBox> cells=stackalloc SurfaceParameterBox[128];Span<BufferCount> depths=stackalloc BufferCount[128];
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var visited=0;
        for(var i=0;i<charts.Length-1;i++)
        {
            cells[0]=new(charts[i].Parameter,charts[i+1].Parameter,surface.VMin,surface.VMax);depths[0]=0;var pending=1;
            while(pending>0)
            {
                if(++visited>8192)return AlgorithmStatus.NotConverged;
                var index=--pending;var cell=cells[index];var depth=depths[index];
                var status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,cell,bounds);
                if(status==AlgorithmStatus.Success)
                {
                    var value=derivative?bounds[3]*plane.Axis.X+bounds[4]*plane.Axis.Y+bounds[5]*plane.Axis.Z
                        :(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
                    if(!derivative||!value.ContainsZero)
                    {
                        positive&=value.Lower>(derivative?0:1e-10);negative&=value.Upper<(derivative?0:-1e-10);continue;
                    }
                }
                else if(status!=AlgorithmStatus.NotConverged)return status;
                if(depth>=20||pending+2>cells.Length)return AlgorithmStatus.NotConverged;
                var alongU=status==AlgorithmStatus.NotConverged||depth%2==1;
                var mid=alongU?cell.U0+(cell.U1-cell.U0)*.5:cell.V0+(cell.V1-cell.V0)*.5;
                cells[pending]=alongU?new(cell.U0,mid,cell.V0,cell.V1):new(cell.U0,cell.U1,cell.V0,mid);depths[pending++]=depth+1;
                cells[pending]=alongU?new(mid,cell.U1,cell.V0,cell.V1):new(cell.U0,cell.U1,mid,cell.V1);depths[pending++]=depth+1;
            }
        }
        return AlgorithmStatus.Success;
    }

    private static bool TryBlendCircleStation(SurfTag surface,in CurveRecord curve,double first,double last,out double station)
    {
        station=0;Span<KernelVector3> sample=stackalloc KernelVector3[1];
        if(EvaluateCurveCore(in curve,(first+last)*.5,0,false,sample,out _)!=AlgorithmStatus.Success)return false;
        var record=Surfaces[GetSurfaceSlotByTag(surface)];
        if(ParameteriseNestedSurface(record,sample[0],out var uv)!=AlgorithmStatus.Success)return false;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var circle=CircleDataPool[curve.DataIndex];var center=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);
        if(JointNestedBlendEvaluation.EvaluateSpine(program,root.Spine,uv.param[0],0,sample)!=AlgorithmStatus.Success
            ||ProfileDistance(sample[0],center)>1e-10||Math.Abs(circle.Radius-Math.Abs(data.Range0))>1e-10)return false;
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> ends=stackalloc KernelVector3[2];
        if(JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,uv.param[0],0,layout,ends[..1])!=AlgorithmStatus.Success
            ||JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,uv.param[0],1,layout,ends[1..])!=AlgorithmStatus.Success)return false;
        var normal=ProfileCross(Sub(ends[0],center),Sub(ends[1],center));if(!ProfileNormalize(ref normal))return false;
        // Equal center/radius/plane certifies the whole analytic circle,
        // rather than inferring an isoparametric boundary from sample hits.
        if(OperationIntersection.Norm(ProfileCross(normal,Vector(circle.AxisX,circle.AxisY,circle.AxisZ)))>1e-10)return false;
        station=uv.param[0];return true;
    }

    private static AlgorithmStatus IntersectBlendCircleRay(SurfTag surface,PK_UV_s query,double du,double dv,in ModelingEdge edge,double station,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;if(du==0)return Math.Abs(query.param[0]-station)<1e-12?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
        var along=(station-query.param[0])/du;var v=query.param[1]+dv*along;
        if(along< -1e-10||v<0||v>1)return AlgorithmStatus.Success;
        var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(surface)].DataIndex];SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        Span<KernelVector3> point=stackalloc KernelVector3[1];var status=JointNestedBlendEvaluation.Evaluate(GetNestedBlendProgram(data),data.ProgramRoot,station,v,layout,point);if(status!=AlgorithmStatus.Success)return status;
        var vector=new PK_VECTOR_s();WriteVector(&vector,point[0]);double parameter=0;
        if(CurveParameteriseVectorCore(edge.Curve,vector,&parameter)!=0)return AlgorithmStatus.NotConverged;
        parameter+=Math.Ceiling((edge.Start-parameter-1e-10)/Math.Tau)*Math.Tau;
        if(!OperationIntersection.InInterval(parameter,edge.Start,edge.End))return AlgorithmStatus.Success;
        return OperationIntersection.AddPoint(output,ref count,point[0],along,parameter,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
    }
}
