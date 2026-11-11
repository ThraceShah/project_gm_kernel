using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>Exact parametric geometry with outward position/Jacobian bounds.</summary>
[InlineArray(9)] internal struct SurfaceBoundValues{private IntervalNumber value;}
internal struct SurfaceBoundCacheEntry
{internal SurfaceParameterBox Box;internal SurfaceBoundValues Values;internal AlgorithmStatus Status;}

internal readonly ref struct SurfaceIntersectionPatch
{
    private readonly BezierSurfacePatch bezier;
    private readonly NestedBlendProgram blend;
    private readonly BlendSurfaceIndex surface;
    private readonly bool isBlend;
    private readonly bool isSpine;
    private readonly byte supportParameterSide;
    private readonly byte isoAxis, sweepAxis;
    private readonly double isoParameter;
    private readonly bool parameterLine;
    private readonly double normalOffset;
    private readonly double lineU,lineV,lineDU,lineDV;
    private readonly Span<SurfaceBoundCacheEntry> cache;
    private readonly bool pinnedCache;
    private readonly GeomTag cacheTag;
    private readonly EntityGeneration cacheGeneration;
    private readonly KernelSense cacheSense;
    internal SurfaceParameterBox Domain{get;}
    internal bool IsProcedural=>isBlend||normalOffset!=0;
    internal bool UVariable=>isBlend||bezier.UDegree>0;
    internal bool VVariable=>isBlend&&!isSpine||bezier.VDegree>0;
    internal SplineDegree UDegree=>isBlend?1:bezier.UDegree;
    internal SplineDegree VDegree=>isBlend?(isSpine?0:1):bezier.VDegree;
    internal SurfaceIntersectionPatch(BezierSurfacePatch patch){bezier=patch;blend=default;surface=0;isBlend=isSpine=false;Domain=patch.Domain;}
    internal SurfaceIntersectionPatch(BezierSurfacePatch patch,double offset):this(patch){normalOffset=offset;}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch source,double offset){this=source;normalOffset+=offset;cache=default;}
    internal SurfaceIntersectionPatch(NestedBlendProgram program,BlendSurfaceIndex root,SurfaceParameterBox domain){bezier=default;blend=program;surface=root;isBlend=true;isSpine=false;Domain=domain;}
    internal SurfaceIntersectionPatch(NestedBlendProgram program,BlendSpineIndex spine,double lower,double upper){bezier=default;blend=program;surface=spine;isBlend=isSpine=true;Domain=new(lower,upper,0,1);}
    // Scalar chart-coordinate adapter, used only for native-t root isolation.
    // Its X value is a supporting Blend's u; it is not spatial geometry.
    internal SurfaceIntersectionPatch(NestedBlendProgram program,BlendSpineIndex spine,BufferOffset side,double lower,double upper)
        :this(program,spine,lower,upper){supportParameterSide=(byte)(side+1);}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch source,Span<SurfaceBoundCacheEntry> entries,bool pinned=false)
    {this=source;cache=entries;pinnedCache=pinned;}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch source,GeomTag tag,EntityGeneration generation,KernelSense sense)
    {this=source;cacheTag=tag;cacheGeneration=generation;cacheSense=sense;}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch source,bool constantU,double parameter,double lower,double upper)
    {this=source;isoAxis=constantU?(byte)1:(byte)2;isoParameter=parameter;Domain=new(lower,upper,0,1);cache=default;}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch curve,BufferOffset axis,double height)
    {this=curve;sweepAxis=(byte)(axis+1);Domain=new(curve.Domain.U0,curve.Domain.U1,-height,height);cache=default;}
    internal SurfaceIntersectionPatch(SurfaceIntersectionPatch source,double u,double v,double du,double dv,double lower,double upper)
    {this=source;parameterLine=true;lineU=u;lineV=v;lineDU=du;lineDV=dv;Domain=new(lower,upper,0,1);cache=default;}
    internal bool ExactCorner(double u,double v,Span<IntervalNumber> homogeneous)
    {
        if(isBlend||normalOffset!=0||isoAxis!=0||sweepAxis!=0||parameterLine||supportParameterSide!=0)return false;
        var i=bezier.UDegree==0||u==bezier.Domain.U0?0:u==bezier.Domain.U1?bezier.UDegree:-1;
        var j=bezier.VDegree==0||v==bezier.Domain.V0?0:v==bezier.Domain.V1?bezier.VDegree:-1;
        if(i<0||j<0)return false;
        bezier.Poles.Slice(4*(i*(bezier.VDegree+1)+j),4).CopyTo(homogeneous);
        return homogeneous[3].Finite&&homogeneous[3].Lower>0;
    }

    internal AlgorithmStatus Bound(in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        ModelingPerformance.BoundRequest();
        if(cache.IsEmpty||output.Length<9)return BoundCore(box,output);
        var hash=BitConverter.DoubleToInt64Bits(box.U0)*31+BitConverter.DoubleToInt64Bits(box.U1)*997+BitConverter.DoubleToInt64Bits(box.V0)*8191+BitConverter.DoubleToInt64Bits(box.V1)*131071;
        hash^=hash>>32;hash^=hash>>16;ref var entry=ref cache[pinnedCache?0:(int)hash&(cache.Length-1)];
        if(entry.Status!=AlgorithmStatus.NotRun&&entry.Box.U0==box.U0&&entry.Box.U1==box.U1&&entry.Box.V0==box.V0&&entry.Box.V1==box.V1)
        {if(entry.Status==AlgorithmStatus.Success)((ReadOnlySpan<IntervalNumber>)entry.Values).CopyTo(output);return entry.Status;}
        if(pinnedCache)return BoundCore(box,output);
        var status=BoundCore(box,output);entry.Box=box;entry.Status=status;if(status==AlgorithmStatus.Success)output[..9].CopyTo(entry.Values);return status;
    }
    private AlgorithmStatus BoundCore(in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        if(supportParameterSide!=0)
        {
            if(ModelingPerformance.TryBound(cacheTag,cacheGeneration,cacheSense,surface,true,box,output,supportParameterSide))return AlgorithmStatus.Success;
            Span<IntervalNumber> parameter=stackalloc IntervalNumber[2];
            var started=ModelingPerformance.StartBound();
            var found=JointNestedBlendEvaluation.BoundSpineSupportU(blend,surface,box.U0,box.U1,supportParameterSide-1,parameter);
            ModelingPerformance.EndBound(started);
            if(found!=AlgorithmStatus.Success)return found;
            output[..9].Clear();output[0]=parameter[0];output[3]=parameter[1];
            ModelingPerformance.StoreBound(cacheTag,cacheGeneration,cacheSense,surface,true,box,output,supportParameterSide);return found;
        }
        if(normalOffset!=0&&isBlend)return AlgorithmStatus.Unsupported;
        var mapped=isoAxis==1?new SurfaceParameterBox(isoParameter,isoParameter,box.U0,box.U1)
            :isoAxis==2?new(box.U0,box.U1,isoParameter,isoParameter):sweepAxis!=0?new(box.U0,box.U1,0,0):box;
        if(parameterLine)
        {
            var t=new IntervalNumber(box.U0,box.U1);var u=lineU+t*lineDU;var v=lineV+t*lineDV;
            mapped=new(u.Lower,u.Upper,v.Lower,v.Upper);
        }
        AlgorithmStatus status;
        if(isBlend&&ModelingPerformance.TryBound(cacheTag,cacheGeneration,cacheSense,surface,isSpine,mapped,output))status=AlgorithmStatus.Success;
        else
        {
            var started=isBlend?ModelingPerformance.StartBound():0;
            status=isSpine?JointNestedBlendEvaluation.BoundSpine(blend,surface,mapped,output)
                :isBlend?JointNestedBlendEvaluation.Bound(blend,surface,mapped,output):OffsetBezierBounds.Bound(bezier,mapped,normalOffset,output);
            if(isBlend){ModelingPerformance.EndBound(started);if(status==AlgorithmStatus.Success)ModelingPerformance.StoreBound(cacheTag,cacheGeneration,cacheSense,surface,isSpine,mapped,output);}
        }
        if(status!=AlgorithmStatus.Success)return status;
        if(isoAxis!=0)
        {
            if(isoAxis==1)output.Slice(6,3).CopyTo(output[3..]);
            output[6]=output[7]=output[8]=0;
        }
        if(parameterLine)
        {
            for(var d=0;d<3;d++)output[3+d]=output[3+d]*lineDU+output[6+d]*lineDV;
            output[6]=output[7]=output[8]=0;
        }
        if(sweepAxis!=0)
        {
            var axis=sweepAxis-1;output[axis]+=new IntervalNumber(box.V0,box.V1);
            output[6]=output[7]=output[8]=0;output[6+axis]=1;
        }
        return status;
    }

    // Estimates only guide local optimization. Accepted upper bounds are
    // recomputed with Bound; estimates never reject a search cell.
    internal AlgorithmStatus Estimate(double u,double v,Span<IntervalNumber> output)
    {
        if(!isBlend||isoAxis!=0||sweepAxis!=0||parameterLine)return Bound(new(u,u,v,v),output);
        Span<KernelVector3> jets=stackalloc KernelVector3[4];SurfaceDerivativeLayout.TryCreate(1,1,out var layout);
        var status=isSpine?JointNestedBlendEvaluation.EvaluateSpine(blend,surface,u,1,jets):JointNestedBlendEvaluation.Evaluate(blend,surface,u,v,layout,jets);if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<3;i++){var p=i==0?jets[0]:i==1?jets[isSpine?1:2]:isSpine?default:jets[1];output[3*i]=p.X;output[3*i+1]=p.Y;output[3*i+2]=p.Z;}return AlgorithmStatus.Success;
    }

    internal AlgorithmStatus SupportRange(in SurfaceParameterBox box,in KernelVector3 direction,ReadOnlySpan<IntervalNumber> bounds,out IntervalNumber range)
    {
        range=default;
        if(!isBlend&&normalOffset==0&&isoAxis==0&&sweepAxis==0&&!parameterLine)
        {
            Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];var status=BezierSurfaceBounds.Restrict(bezier,box,poles);if(status!=AlgorithmStatus.Success)return status;
            range=new(double.PositiveInfinity,double.NegativeInfinity);
            for(var i=0;i<(bezier.UDegree+1)*(bezier.VDegree+1);i++)
            {var at=4*i;var value=poles[at]/poles[at+3]*direction.X+poles[at+1]/poles[at+3]*direction.Y+poles[at+2]/poles[at+3]*direction.Z;range=IntervalNumber.Hull(range,value);}
            return AlgorithmStatus.Success;
        }
        Span<IntervalNumber> center=stackalloc IntervalNumber[9];var u=box.U0+(box.U1-box.U0)*.5;var v=box.V0+(box.V1-box.V0)*.5;var found=Bound(new(u,u,v,v),center);if(found!=AlgorithmStatus.Success)return found;
        var anchor=center[0]*direction.X+center[1]*direction.Y+center[2]*direction.Z;
        var du=bounds[3]*direction.X+bounds[4]*direction.Y+bounds[5]*direction.Z;var dv=bounds[6]*direction.X+bounds[7]*direction.Y+bounds[8]*direction.Z;
        var mean=anchor+du*new IntervalNumber(box.U0-u,box.U1-u)+dv*new IntervalNumber(box.V0-v,box.V1-v);
        var hull=bounds[0]*direction.X+bounds[1]*direction.Y+bounds[2]*direction.Z;range=new(Math.Max(mean.Lower,hull.Lower),Math.Min(mean.Upper,hull.Upper));return range.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }
}
