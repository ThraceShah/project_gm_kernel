using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RollingBuildStage:byte{Offset,Compile,Seed,Project,Forward,Backward,EndSeed,EndProject,Charts,Proof,Bind}
    private const BufferCount RollingTracePointLimit=512;
    [ThreadStatic] internal static RollingBuildStage LastRollingBuildStage;
    private static int CreateGeneralRollingBlend(SurfTag first,double r0,SurfTag second,double r1,
        in KernelVector3 start,in KernelVector3 end,PK_SURF_create_blend_o_s* options,SurfTag* result)
        =>CreateGeneralRollingBlend(first,r0,second,r1,start,end,options,result,default);

    internal static int CreateFiniteRollingBlend(SurfTag first,double r0,SurfTag second,double r1,
        in KernelVector3 start,in KernelVector3 end,PK_SURF_create_blend_o_s* options,SurfTag* result,ReadOnlySpan<AnalyticSurface> outwardTerminationPlanes)
    {
        if(!Dispatcher.IsExecuting)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(outwardTerminationPlanes.Length<2)return ParasolidConstants.PK_ERROR_bad_parameter;
        foreach(ref readonly var plane in outwardTerminationPlanes)if(plane.Kind!=SurfaceClass.Plane||!IsFinite(plane.Axis)||!IsFinite(plane.Origin)||!(OperationIntersection.Norm(plane.Axis)>0))return ParasolidConstants.PK_ERROR_bad_parameter;
        var owns=EnsureCommandScratch();
        try{return CreateGeneralRollingBlend(first,r0,second,r1,start,end,options,result,outwardTerminationPlanes);}
        finally{if(owns)ReleaseCommandScratch(State.Session);}
    }

    private static int CreateGeneralRollingBlend(SurfTag first,double r0,SurfTag second,double r1,
        in KernelVector3 start,in KernelVector3 end,PK_SURF_create_blend_o_s* options,SurfTag* result,ReadOnlySpan<AnalyticSurface> outwardTerminationPlanes)
    {
        LastRollingBuildStage=RollingBuildStage.Offset;
        var error=RollingOffsetSupport(first,r0,0,out var offset0);if(error!=0)return error;
        error=RollingOffsetSupport(second,r1,0,out var offset1);if(error!=0)return error;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            LastRollingBuildStage=RollingBuildStage.Compile;
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
            var status=compiler.Surface(offset0,out var a);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            status=compiler.Surface(offset1,out var b);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var retainSupportUv=compiler.View.Surfaces[a].IsBSpline||compiler.View.Surfaces[b].IsBSpline;
            var supportUv=retainSupportUv?NestedWorkspace<double>(4*RollingTracePointLimit):default;
            var backwardUv=retainSupportUv?NestedWorkspace<double>(4*RollingTracePointLimit):default;
            if(retainSupportUv&&(supportUv.IsEmpty||backwardUv.IsEmpty))return ParasolidConstants.PK_ERROR_memory_full;
            LastRollingBuildStage=RollingBuildStage.Seed;
            // Reserve the trace witnesses before borrowing the program's numeric workspace.
            var program=compiler.View;var seed=options->have_help!=0?Vector(options->help.coord[0],options->help.coord[1],options->help.coord[2]):start;
            Span<double> contacts=stackalloc double[4];status=RollingBlendSample(program,a,b,seed,out _,out _,out var normal,contacts);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var projectedSpines=NestedWorkspace<BlendSpineInstruction>(program.Spines.Length+1);var projectedCharts=NestedWorkspace<BlendChartInstruction>(program.Charts.Length+2);
            if(projectedSpines.IsEmpty||projectedCharts.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
            program.Spines.CopyTo(projectedSpines);program.Charts.CopyTo(projectedCharts);var projectionSpine=program.Spines.Length;var projectionChart=program.Charts.Length;
            projectedSpines[projectionSpine]=new(){First=a,Second=b,ChartOffset=projectionChart,ChartCount=2};
            program=new(program.Surfaces,program.Spines,program.Charts,program.Scalars,CommandScratch.Current.AvailableSpanOfDoubles());
            var projector=new NestedBlendProgram(program.Surfaces,projectedSpines,projectedCharts,program.Scalars,CommandScratch.Current.AvailableSpanOfDoubles());
            LastRollingBuildStage=RollingBuildStage.Project;
            status=RollingBlendProjectJoint(projector,projectionSpine,projectedCharts[projectionChart..],seed,seed,normal,contacts,out var initial,out var tangent);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var scale=Math.Max(1,OperationIntersection.Norm(Sub(start,end)));var step=.05*scale;
            var feature=Math.Min(RollingFeature(program.Surfaces[a]),RollingFeature(program.Surfaces[b]));if(double.IsFinite(feature))step=Math.Min(step,.5*feature);
            if(!(step>1e-12*scale))return ParasolidConstants.PK_ERROR_at_singularity;
            Span<KernelVector3> points=stackalloc KernelVector3[RollingTracePointLimit],backward=stackalloc KernelVector3[RollingTracePointLimit];
            Span<double> initialContacts=stackalloc double[4];contacts.CopyTo(initialContacts);
            LastRollingBuildStage=RollingBuildStage.Forward;
            status=TraceRollingBlend(projector,projectionSpine,projectedCharts[projectionChart..],initial,tangent,step,contacts,points,supportUv,out var count,out var closed,out var ctol,outwardTerminationPlanes,Math.Abs(r0));
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            if(!closed)
            {
                LastRollingBuildStage=RollingBuildStage.Backward;
                initialContacts.CopyTo(contacts);
                status=TraceRollingBlend(projector,projectionSpine,projectedCharts[projectionChart..],initial,Scale(tangent,-1),step,contacts,backward,backwardUv,out var left,out var leftClosed,out var leftError,outwardTerminationPlanes,Math.Abs(r0));
                if(status!=AlgorithmStatus.Success||leftClosed)return EvaluationError(status==AlgorithmStatus.Success?AlgorithmStatus.NotConverged:status);
                if(left+count-1>points.Length)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                points[..count].CopyTo(points[(left-1)..]);
                if(!supportUv.IsEmpty)supportUv[..(4*count)].CopyTo(supportUv[(4*(left-1))..]);
                for(var i=0;i<left-1;i++)
                {
                    points[i]=backward[left-1-i];
                    if(!supportUv.IsEmpty)backwardUv.Slice(4*(left-1-i),4).CopyTo(supportUv.Slice(4*i,4));
                }
                count+=left-1;ctol=Math.Max(ctol,leftError);
            }
            LastRollingBuildStage=RollingBuildStage.EndSeed;
            status=RollingBlendSample(program,a,b,end,out _,out _,out var endNormal);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var nearest=points[0];var best=OperationIntersection.Norm(Sub(nearest,end));for(var i=1;i<count;i++){var distance=OperationIntersection.Norm(Sub(points[i],end));if(distance<best){best=distance;nearest=points[i];}}
            // The two inputs select the rolling branch; neither is required
            // to lie on its offset spine. In particular a hint's tangent
            // plane can miss a small closed spine completely.
            if(best>OperationIntersection.Norm(Sub(start,end))+Math.Abs(r0)+Math.Abs(r1))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            LastRollingBuildStage=RollingBuildStage.EndProject;
            status=RollingBlendProjectJoint(projector,projectionSpine,projectedCharts[projectionChart..],nearest,nearest,endNormal,contacts,out var endPoint,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var matched=false;for(var i=0;i<count-1;i++)if(OperationIntersection.Norm(Sub(points[i],endPoint))<2*step)matched=true;
            if(!matched)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            LastRollingBuildStage=RollingBuildStage.Charts;
            // Only the spline unknowns are native UV witnesses. Other support
            // kinds keep their existing chart parameterisation path.
            if(!supportUv.IsEmpty)for(var i=0;i<count;i++)for(var side=0;side<2;side++)
                if(!program.Surfaces[side==0?a:b].IsBSpline){supportUv[4*i+2*side]=double.NaN;supportUv[4*i+2*side+1]=double.NaN;}
            status=compiler.ChartSpine(a,b,points[..count],0,1,Math.Max(2*ctol,1e-8*scale),Math.PI/12,closed?LimitType.Help:LimitType.Artificial,closed?LimitType.Help:LimitType.Artificial,out var spineIndex,r0*r1<0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c,
                knownUv:supportUv.IsEmpty?default:supportUv[..(4*count)]);
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            if(!outwardTerminationPlanes.IsEmpty)
            {
                LastRollingBuildStage=RollingBuildStage.Proof;
                status=CertifyFiniteRollingTrace(compiler.View,spineIndex,outwardTerminationPlanes,Math.Abs(r0),closed);
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            }
            LastRollingBuildStage=RollingBuildStage.Bind;
            CurveTag spine=0;error=BindNestedBlendSpine(offset0,offset1,compiler.View,spineIndex,&spine);if(error!=0)return error;
            return CompileAndBindNestedBlend(first,second,spine,r0,r1,result);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static double RollingFeature(in BlendSurfaceInstruction surface)
        =>surface.IsBlend?Math.Abs(surface.Range0):surface.Analytic.Kind==SurfaceClass.Torus?surface.Analytic.Secondary:
            surface.Analytic.Kind is SurfaceClass.Sphere or SurfaceClass.Cylinder?surface.Analytic.Radius:double.PositiveInfinity;

    private static AlgorithmStatus TraceRollingBlend(in NestedBlendProgram program,BlendSpineIndex spine,Span<BlendChartInstruction> charts,
        in KernelVector3 initial,in KernelVector3 tangent,double step,Span<double> contacts,Span<KernelVector3> points,Span<double> supportUv,
        out BufferCount count,out bool closed,out double error,ReadOnlySpan<AnalyticSurface> outwardTerminationPlanes=default,double radius=0)
    {
        count=1;closed=false;error=0;points[0]=initial;if(!supportUv.IsEmpty)contacts.CopyTo(supportUv[..4]);
        var previous=initial;var direction=tangent;var minimum=step*1e-6;Span<double> savedContacts=stackalloc double[4];
        for(var visited=0;visited<2048&&count<points.Length;visited++)
        {
            var target=Add(previous,Scale(direction,step));contacts.CopyTo(savedContacts);
            var status=RollingBlendProjectJoint(program,spine,charts,target,target,direction,contacts,out var point,out var next);
            if(status==AlgorithmStatus.Success&&outwardTerminationPlanes.IsEmpty&&!RollingContactsInside(program,spine,contacts)){savedContacts.CopyTo(contacts);status=AlgorithmStatus.InvalidInput;}
            var correction=OperationIntersection.Norm(Sub(point,target));
            if(status!=AlgorithmStatus.Success||correction>step*.15||Dot(direction,next)<.9659258262890683)
            {
                if(outwardTerminationPlanes.IsEmpty)
                {
                    var boundary=RollingBlendBoundary(program,spine,charts,previous,direction,step,contacts,out var endpoint);
                    if(boundary==AlgorithmStatus.Success)
                    {
                        if(OperationIntersection.Norm(Sub(endpoint,previous))>1e-12)
                        {if(!supportUv.IsEmpty)contacts.CopyTo(supportUv.Slice(4*count,4));points[count++]=endpoint;}
                        return AlgorithmStatus.Success;
                    }
                }
                step*=.5;if(step<minimum)return status==AlgorithmStatus.Success?AlgorithmStatus.NotConverged:status;continue;
            }
            error=Math.Max(error,correction);
            if(count>=8&&OperationIntersection.Norm(Sub(point,initial))<step*.8&&Dot(next,tangent)>.9)
            {
                if(!supportUv.IsEmpty)supportUv[..4].CopyTo(supportUv.Slice(4*count,4));
                points[count++]=initial;closed=true;return AlgorithmStatus.Success;
            }
            if(!supportUv.IsEmpty)contacts.CopyTo(supportUv.Slice(4*count,4));
            points[count++]=point;previous=point;direction=next;
            if(!outwardTerminationPlanes.IsEmpty&&RollingTraceBeyondPlane(new(point.X),new(point.Y),new(point.Z),outwardTerminationPlanes,radius))return AlgorithmStatus.Success;
        }
        return AlgorithmStatus.NotConverged;
    }

    private static bool RollingContactsInside(in NestedBlendProgram program,BlendSpineIndex spine,ReadOnlySpan<double> contacts)
    {
        var definition=program.Spines[spine];
        for(var side=0;side<2;side++)
        {
            var source=program.Surfaces[side==0?definition.First:definition.Second];if(!source.IsBSpline)continue;var view=program.Spline(source);
            if(!BSurfaceEvaluation.IsInDomain(view,contacts[2*side],contacts[2*side+1]))return false;
        }
        return true;
    }

    private static AlgorithmStatus RollingBlendBoundary(in NestedBlendProgram program,BlendSpineIndex spine,Span<BlendChartInstruction> charts,
        in KernelVector3 last,in KernelVector3 direction,double step,Span<double> contacts,out KernelVector3 endpoint)
    {
        endpoint=default;var definition=program.Spines[spine];
        Span<double> trial=stackalloc double[4];Span<KernelVector3> jet=stackalloc KernelVector3[1];
        for(var side=0;side<2;side++)
        {
            var surface=program.Surfaces[side==0?definition.First:definition.Second];if(!surface.IsBSpline)continue;var spline=program.Spline(surface);
            for(var axis=0;axis<2;axis++)
            {
                if(axis==0?spline.UPeriodic:spline.VPeriodic)continue;
                var lo=axis==0?spline.UStart:spline.VStart;var hi=axis==0?spline.UEnd:spline.VEnd;var current=contacts[2*side+axis];var value=current-lo<hi-current?lo:hi;
                if(Math.Abs(current-value)>.05*(hi-lo))continue;
                contacts.CopyTo(trial);trial[2*side+axis]=value;
                charts[0]=new(){Position=last,Chord=direction,Scale=1,FirstU=trial[0],FirstV=trial[1],SecondU=trial[2],SecondV=trial[3]};charts[1]=charts[0];charts[1].Position=Add(last,direction);charts[1].Parameter=1;
                var status=JointNestedBlendEvaluation.ProjectBoundary(program,spine,side,axis,value,jet,trial);if(status!=AlgorithmStatus.Success)continue;
                var delta=Sub(jet[0],last);if(OperationIntersection.Norm(delta)>4*step||Dot(delta,direction)<-1e-10)continue;
                endpoint=jet[0];trial.CopyTo(contacts);return AlgorithmStatus.Success;
            }
        }
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus RollingBlendSample(in NestedBlendProgram program,BlendSurfaceIndex a,BlendSurfaceIndex b,
        in KernelVector3 point,out double d0,out double d1,out KernelVector3 tangent,Span<double> contacts=default)
    {
        d0=d1=0;tangent=default;var path=TaylorVector.Constant(point,0);
        TaylorJet first,second;TaylorVector n0,n1;double u0=0,v0=0,u1=0,v1=0;
        var status=program.Surfaces[a].IsBSpline?NestedSplineEvaluation.Distance(program,program.Surfaces[a],path,out first,out n0,out u0,out v0):NestedBlendSeriesEvaluation.Distance(program,a,path,0,out first,out n0);if(status!=AlgorithmStatus.Success)return status;
        status=program.Surfaces[b].IsBSpline?NestedSplineEvaluation.Distance(program,program.Surfaces[b],path,out second,out n1,out u1,out v1):NestedBlendSeriesEvaluation.Distance(program,b,path,0,out second,out n1);if(status!=AlgorithmStatus.Success)return status;
        if(contacts.Length>=4){contacts[0]=u0;contacts[1]=v0;contacts[2]=u1;contacts[3]=v1;}
        var cross=EvaluationMath.Cross(n0.Value,n1.Value);var length=OperationIntersection.Norm(cross);if(!(length>1e-8))return AlgorithmStatus.Singular;
        d0=first[0];d1=second[0];tangent=Scale(cross,1/length);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus RollingBlendProjectJoint(in NestedBlendProgram program,BlendSpineIndex spine,
        Span<BlendChartInstruction> charts,in KernelVector3 anchor,in KernelVector3 guess,in KernelVector3 plane,Span<double> contacts,out KernelVector3 point,out KernelVector3 tangent)
    {
        point=tangent=default;var seed=Sub(guess,Scale(plane,Dot(plane,Sub(guess,anchor))));
        charts[0]=new(){Position=seed,Chord=plane,Scale=1,FirstU=contacts[0],FirstV=contacts[1],SecondU=contacts[2],SecondV=contacts[3]};
        charts[1]=charts[0];charts[1].Position=Add(seed,plane);charts[1].Parameter=1;
        Span<KernelVector3> jets=stackalloc KernelVector3[2];var status=JointNestedBlendEvaluation.Project(program,spine,jets,contacts);if(status!=AlgorithmStatus.Success)return status;
        point=jets[0];tangent=Unit(jets[1]);return AlgorithmStatus.Success;
    }

    private static int RollingOffsetSupport(SurfTag surface,double distance,BufferCount depth,out SurfTag result)
    {
        result=0;if(depth>16)return ParasolidConstants.PK_ERROR_not_implemented;
        var slot=GetSurfaceSlotByTag(surface);if(slot<0)return ParasolidConstants.PK_ERROR_bad_class;var source=Surfaces[slot];
        if(TryPrepareSurface(source,out var analytic))
        {
            var status=OffsetAnalyticBlendSupport(analytic,distance*SenseSign(source.Sense),out var offset);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var basis=new PK_AXIS2_sf_s();WriteVector(&basis.location,offset.Origin);WriteVector(&basis.axis,offset.Axis);WriteVector(&basis.ref_direction,offset.X);SurfTag created=0;int error;
            switch(offset.Kind)
            {
                case SurfaceClass.Plane:var plane=new PK_PLANE_sf_s{basis_set=basis};error=PlaneCreate(&plane,&created);break;
                case SurfaceClass.Sphere:var sphere=new PK_SPHERE_sf_s{basis_set=basis,radius=offset.Radius};error=SphereCreate(&sphere,&created);break;
                case SurfaceClass.Cylinder:var cylinder=new PK_CYL_sf_s{basis_set=basis,radius=offset.Radius};error=CylCreate(&cylinder,&created);break;
                case SurfaceClass.Cone:var cone=new PK_CONE_sf_s{basis_set=basis,radius=offset.Radius,semi_angle=Math.Atan(offset.Secondary)};error=ConeCreate(&cone,&created);break;
                case SurfaceClass.Torus:var torus=new PK_TORUS_sf_s{basis_set=basis,major_radius=offset.Radius,minor_radius=offset.Secondary};error=TorusCreate(&torus,&created);break;
                default:return ParasolidConstants.PK_ERROR_not_implemented;
            }
            if(error!=0)return error;Surfaces[GetSurfaceSlotByTag(created)].Sense=source.Sense;result=created;return 0;
        }
        if(source.Class is SurfaceClass.BSurface or SurfaceClass.Offset)
        {
            if(Math.Abs(distance)<=1e-12){result=surface;return 0;}
            var definition=new PK_OFFSET_sf_s{underlying_surface=surface,offset_distance=distance*SenseSign(source.Sense)};SurfTag offset=0;
            var error=OffsetCreate(&definition,&offset);if(error!=0)return error;
            Surfaces[GetSurfaceSlotByTag(offset)].Sense=source.Sense;result=offset;return 0;
        }
        if(source.Class!=SurfaceClass.BlendSurface)return ParasolidConstants.PK_ERROR_not_implemented;
        var data=BlendedEdgeDataPool[source.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var radius=Math.Abs(data.Range0)+distance*root.NormalSense*SenseSign(source.Sense);if(!(radius>1e-12))return ParasolidConstants.PK_ERROR_at_singularity;
        var r0=Math.CopySign(radius,data.Range0);var r1=Math.CopySign(radius,data.Range1);
        var first=RollingOffsetSupport(data.Surface0Tag,data.Range0-r0,depth+1,out var a);if(first!=0)return first;
        var second=RollingOffsetSupport(data.Surface1Tag,data.Range1-r1,depth+1,out var b);if(second!=0)return second;
        SurfTag blend=0;var found=CompileAndBindNestedBlend(a,b,data.SpineTag,r0,r1,&blend);if(found!=0)return found;
        Surfaces[GetSurfaceSlotByTag(blend)].Sense=source.Sense;result=blend;return 0;
    }
}
