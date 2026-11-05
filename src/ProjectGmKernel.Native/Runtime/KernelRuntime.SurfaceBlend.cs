using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int SurfCreateBlend(GeomTag first,double firstRange,GeomTag second,double secondRange,
        PK_VECTOR_s start,PK_VECTOR_s end,PK_SURF_create_blend_o_s* options,SurfTag* surface)
    {
        if(surface!=null)*surface=0;
        if(Dispatcher.IsExecuting)return SurfCreateBlendCore(first,firstRange,second,secondRange,start,end,options,surface);
        var command=new SurfaceBlendCommand{First=first,Second=second,FirstRange=firstRange,SecondRange=secondRange,Start=start,End=end,Options=options,Surface=surface};
        return Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,first);
    }

    private struct SurfaceBlendCommand:IKernelCommand
    {
        internal GeomTag First,Second;
        internal double FirstRange,SecondRange;
        internal PK_VECTOR_s Start,End;
        internal PK_SURF_create_blend_o_s* Options;
        internal SurfTag* Surface;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{var error=SurfCreateBlendCore(First,FirstRange,Second,SecondRange,Start,End,Options,Surface);if(error!=0&&Surface!=null)*Surface=0;return error;}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int SurfCreateBlendCore(GeomTag first,double firstRange,GeomTag second,double secondRange,
        PK_VECTOR_s start,PK_VECTOR_s end,PK_SURF_create_blend_o_s* options,SurfTag* result)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||result==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=1)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var aPoint=Vector(start.coord[0],start.coord[1],start.coord[2]);var bPoint=Vector(end.coord[0],end.coord[1],end.coord[2]);
        if(!IsFinite(aPoint)||!IsFinite(bPoint)||!double.IsFinite(firstRange)||!double.IsFinite(secondRange)||firstRange==0||secondRange==0
            ||options->have_help>1||options->have_box>1)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->have_box!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        if(options->have_help!=0&&!IsFinite(Vector(options->help.coord[0],options->help.coord[1],options->help.coord[2])))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(Math.Abs(firstRange)!=Math.Abs(secondRange))return ParasolidConstants.PK_ERROR_not_implemented;
        var sa=GetSurfaceSlotByTag(first);var sb=GetSurfaceSlotByTag(second);if(sa<0||sb<0)return ParasolidConstants.PK_ERROR_bad_class;
        if(Surfaces[sa].Header.Partition!=CurrentPartition||Surfaces[sb].Header.Partition!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(!TryPrepareSurface(Surfaces[sa],out var a)||!TryPrepareSurface(Surfaces[sb],out var b))
            return CreateGeneralRollingBlend(first,firstRange,second,secondRange,aPoint,bPoint,options,result);
        var found=OffsetAnalyticBlendSupport(a,firstRange,out var offsetA);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
        found=OffsetAnalyticBlendSupport(b,secondRange,out var offsetB);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[16];Span<KernelVector3> points=stackalloc KernelVector3[16];
        found=OperationIntersection.Surfaces(offsetA,offsetB,branches,out var count,points,out var pointCount);
        if(found==AlgorithmStatus.Unsupported)return CreateGeneralRollingBlend(first,firstRange,second,secondRange,aPoint,bPoint,options,result);
        if(found!=AlgorithmStatus.Success)return IntersectionError(found);
        if(pointCount!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var chosen=-1;var score=double.PositiveInfinity;var ambiguous=false;
        for(var i=0;i<count;i++)
        {
            ref readonly var branch=ref branches[i];
            if(branch.Class is not (CurveClass.Line or CurveClass.Circle))return ParasolidConstants.PK_ERROR_not_implemented;
            var candidate=IntersectionBranchDistance(branch,aPoint)+IntersectionBranchDistance(branch,bPoint);
            if(options->have_help!=0)candidate+=IntersectionBranchDistance(branch,Vector(options->help.coord[0],options->help.coord[1],options->help.coord[2]));
            if(candidate<score){ambiguous=double.IsFinite(score)&&Math.Abs(candidate-score)<1e-10;score=candidate;chosen=i;}
            else if(Math.Abs(candidate-score)<1e-10)ambiguous=true;
        }
        if(chosen<0||ambiguous)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        ref readonly var spine=ref branches[chosen];var radius=Math.Abs(firstRange);
        if(spine.Class==CurveClass.Line)
        {
            var center=Add(spine.Origin,Scale(spine.Axis,Dot(Sub(aPoint,spine.Origin),spine.Axis)));
            found=AnalyticImplicitEvaluation.Evaluate(offsetA,center,1,out var j0);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
            found=AnalyticImplicitEvaluation.Evaluate(offsetB,center,1,out var j1);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
            var axis=ProfileCross(j0.Gradient,j1.Gradient);if(!ProfileNormalize(ref axis))return ParasolidConstants.PK_ERROR_at_singularity;
            if(!(Dot(Sub(bPoint,aPoint),axis)>0))return ParasolidConstants.PK_ERROR_bad_value;
            var reference=j0.Gradient;if(!ProfileNormalize(ref reference))return ParasolidConstants.PK_ERROR_at_singularity;
            var definition=new PK_CYL_sf_s{radius=radius};WriteVector(&definition.basis_set.location,center);WriteVector(&definition.basis_set.axis,axis);WriteVector(&definition.basis_set.ref_direction,reference);
            return CylCreateImplementation(&definition,result);
        }
        if(!(spine.Radius>radius))return ParasolidConstants.PK_ERROR_not_implemented;
        var delta=Sub(aPoint,spine.Origin);var radial=Sub(delta,Scale(spine.Axis,Dot(delta,spine.Axis)));
        if(!ProfileNormalize(ref radial))return ParasolidConstants.PK_ERROR_bad_value;
        var torus=new PK_TORUS_sf_s{major_radius=spine.Radius,minor_radius=radius};WriteVector(&torus.basis_set.location,spine.Origin);
        WriteVector(&torus.basis_set.axis,spine.Axis);WriteVector(&torus.basis_set.ref_direction,radial);
        return TorusCreateImplementation(&torus,result);
    }

    private static AlgorithmStatus OffsetAnalyticBlendSupport(in AnalyticSurface surface,double distance,out AnalyticSurface offset)
    {
        offset=surface;
        switch(surface.Kind)
        {
            case SurfaceClass.Plane:offset=new(surface.Kind,Add(surface.Origin,Scale(surface.Axis,distance)),surface.Axis,surface.X);return AlgorithmStatus.Success;
            case SurfaceClass.Sphere:
            case SurfaceClass.Cylinder:
                if(!(surface.Radius+distance>0))return AlgorithmStatus.Singular;
                offset=new(surface.Kind,surface.Origin,surface.Axis,surface.X,surface.Radius+distance);return AlgorithmStatus.Success;
            case SurfaceClass.Cone:
                var radius=surface.Radius+distance*Math.Sqrt(1+surface.Secondary*surface.Secondary);if(!(radius>0))return AlgorithmStatus.Unsupported;
                offset=new(surface.Kind,surface.Origin,surface.Axis,surface.X,radius,surface.Secondary);return AlgorithmStatus.Success;
            case SurfaceClass.Torus:
                var minor=surface.Secondary+distance;if(!(minor>0&&surface.Radius>minor))return AlgorithmStatus.Unsupported;
                offset=new(surface.Kind,surface.Origin,surface.Axis,surface.X,surface.Radius,minor);return AlgorithmStatus.Success;
            default:return AlgorithmStatus.Unsupported;
        }
    }
}
