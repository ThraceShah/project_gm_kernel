using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CreateGeneralRollingBlend(SurfTag first,double r0,SurfTag second,double r1,
        in KernelVector3 start,in KernelVector3 end,PK_SURF_create_blend_o_s* options,SurfTag* result)
    {
        Span<GeomTag> source=stackalloc GeomTag[2]{first,second},copies=stackalloc GeomTag[2];
        var error=TryCloneEntitiesCore(source,copies);if(error!=0)return error;
        error=RollingOffsetSupport(copies[0],r0,0,out var offset0);if(error!=0)return error;
        error=RollingOffsetSupport(copies[1],r1,0,out var offset1);if(error!=0)return error;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
            var status=compiler.Surface(offset0,out var a);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            status=compiler.Surface(offset1,out var b);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var program=compiler.View;var seed=options->have_help!=0?Vector(options->help.coord[0],options->help.coord[1],options->help.coord[2]):start;
            Span<double> contacts=stackalloc double[4];status=RollingBlendSample(program,a,b,seed,out _,out _,out var normal,contacts);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var projectedSpines=NestedWorkspace<BlendSpineInstruction>(program.Spines.Length+1);var projectedCharts=NestedWorkspace<BlendChartInstruction>(program.Charts.Length+2);
            if(projectedSpines.IsEmpty||projectedCharts.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
            program.Spines.CopyTo(projectedSpines);program.Charts.CopyTo(projectedCharts);var projectionSpine=program.Spines.Length;var projectionChart=program.Charts.Length;
            projectedSpines[projectionSpine]=new(){First=a,Second=b,ChartOffset=projectionChart,ChartCount=2};
            program=new(program.Surfaces,program.Spines,program.Charts,program.Scalars,CommandScratch.Current.AvailableSpanOfDoubles());
            var projector=new NestedBlendProgram(program.Surfaces,projectedSpines,projectedCharts,program.Scalars,CommandScratch.Current.AvailableSpanOfDoubles());
            status=RollingBlendProjectJoint(projector,projectionSpine,projectedCharts[projectionChart..],seed,seed,normal,contacts,out var initial,out var tangent);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var scale=Math.Max(1,OperationIntersection.Norm(Sub(start,end)));var step=.05*scale;
            var feature=Math.Min(RollingFeature(program.Surfaces[a]),RollingFeature(program.Surfaces[b]));if(double.IsFinite(feature))step=Math.Min(step,.5*feature);
            if(!(step>1e-12*scale))return ParasolidConstants.PK_ERROR_at_singularity;
            Span<KernelVector3> points=stackalloc KernelVector3[512];points[0]=initial;var count=1;var previous=initial;var previousTangent=tangent;
            var initialTangent=tangent;var closed=false;var ctol=0.0;var minimumStep=step*1e-6;
            for(var visited=0;visited<2048&&count<points.Length;visited++)
            {
                var target=Add(previous,Scale(previousTangent,step));
                status=RollingBlendProjectJoint(projector,projectionSpine,projectedCharts[projectionChart..],target,target,previousTangent,contacts,out var point,out var nextTangent);
                var correction=OperationIntersection.Norm(Sub(point,target));
                if(status!=AlgorithmStatus.Success||correction>step*.15||Dot(previousTangent,nextTangent)<.9659258262890683)
                {step*=.5;if(step<minimumStep)return EvaluationError(status==AlgorithmStatus.Success?AlgorithmStatus.NotConverged:status);continue;}
                ctol=Math.Max(ctol,correction);
                if(count>=8&&OperationIntersection.Norm(Sub(point,initial))<step*.8&&Dot(nextTangent,initialTangent)>.9)
                {points[count++]=initial;closed=true;break;}
                points[count++]=point;previous=point;previousTangent=nextTangent;
            }
            if(!closed)return ParasolidConstants.PK_ERROR_not_implemented;
            status=RollingBlendSample(program,a,b,end,out _,out _,out var endNormal);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var nearest=points[0];var best=OperationIntersection.Norm(Sub(nearest,end));for(var i=1;i<count;i++){var distance=OperationIntersection.Norm(Sub(points[i],end));if(distance<best){best=distance;nearest=points[i];}}
            // The two inputs select the rolling branch; neither is required
            // to lie on its offset spine. In particular a hint's tangent
            // plane can miss a small closed spine completely.
            if(best>OperationIntersection.Norm(Sub(start,end))+Math.Abs(r0)+Math.Abs(r1))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            status=RollingBlendProjectJoint(projector,projectionSpine,projectedCharts[projectionChart..],nearest,nearest,endNormal,contacts,out var endPoint,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var matched=false;for(var i=0;i<count-1;i++)if(OperationIntersection.Norm(Sub(points[i],endPoint))<2*step)matched=true;
            if(!matched)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            status=compiler.ChartSpine(a,b,points[..count],0,1,Math.Max(2*ctol,1e-8*scale),Math.PI/12,LimitType.Help,LimitType.Help,out var spineIndex);
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            CurveTag spine=0;error=BindNestedBlendSpine(offset0,offset1,compiler.View,spineIndex,&spine);if(error!=0)return error;
            return CompileAndBindNestedBlend(copies[0],copies[1],spine,r0,r1,result);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static double RollingFeature(in BlendSurfaceInstruction surface)
        =>surface.IsBlend?Math.Abs(surface.Range0):surface.Analytic.Kind==SurfaceClass.Torus?surface.Analytic.Secondary:
            surface.Analytic.Kind is SurfaceClass.Sphere or SurfaceClass.Cylinder?surface.Analytic.Radius:double.PositiveInfinity;

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
