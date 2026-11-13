using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Computation.Numerics;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static double BlendContactV(in ICurveData contact)
    {
        var blend=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(contact.Surface1Tag)].DataIndex];
        return GetNestedBlendProgram(blend).Surfaces[blend.ProgramRoot].ReverseSection?contact.BlendContactSide:1-contact.BlendContactSide;
    }

    private static Span<double> BlendContactMap(in ICurveData data)
        =>new(DereferenceBlock(data.ContactChartBlock),6*data.ChartCount-4);

    internal static int CreateBlendContactCurve(SurfTag blend,BufferOffset side,out CurveTag curve)
    {
        curve=0;if(!Dispatcher.IsExecuting)return ParasolidConstants.PK_ERROR_bad_parameter;
        var owns=EnsureCommandScratch();
        try{return CreateBlendContactCurveCore(blend,side,out curve);}
        finally{if(owns)ReleaseCommandScratch(State.Session);}
    }

    private static int CreateBlendContactCurveCore(SurfTag blend,BufferOffset side,out CurveTag curve)
    {
        curve=0;var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(blend)].DataIndex];var program=GetNestedBlendProgram(data);
        var definition=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        if(charts.Length>1024)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> positions=stackalloc KernelVector3[charts.Length];Span<double> parameters=stackalloc double[charts.Length];
        var v=program.Surfaces[data.ProgramRoot].ReverseSection?side:1-side;SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        for(var i=0;i<charts.Length;i++)
        {
            // With support first and its boundary second, the two contact
            // intersections have opposite natural directions.
            var index=side==0?i:charts.Length-1-i;parameters[i]=charts[index].Parameter;
            var status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,parameters[i],v,layout,positions.Slice(i,1));
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        }
        return BindBlendContactCore(blend,side,positions,parameters,0,1,ParasolidConstants.PK_TOPOL_sense_positive_c,out curve);
    }

    // Preserve the actual imported CHART anchors and parameter scale. Only
    // their exact station on the defining blend is recovered, never refitted.
    private static int BindBlendContactChart(SurfTag blend,BufferOffset side,ReadOnlySpan<KernelVector3> positions,
        double baseParameter,double baseScale,KernelSense sense,out CurveTag curve)
    {
        curve=0;if(positions.Length>1024)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> u=stackalloc double[positions.Length];
        for(var i=0;i<positions.Length;i++)
        {
            var status=ParameteriseNestedSurface(Surfaces[GetSurfaceSlotByTag(blend)],positions[i],out var uv);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            u[i]=uv.param[0];
        }
        return BindBlendContactCore(blend,side,positions,u,baseParameter,baseScale,sense,out curve);
    }

    private static int BindBlendContactCore(SurfTag blend,BufferOffset side,ReadOnlySpan<KernelVector3> positions,ReadOnlySpan<double> u,
        double baseParameter,double baseScale,KernelSense sense,out CurveTag curve)
    {
        curve=0;var n=positions.Length;if(n<2||u.Length!=n||side is <0 or >1)return ParasolidConstants.PK_ERROR_bad_parameter;
        var blendData=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(blend)].DataIndex];
        var source=side==0?blendData.Surface0Tag:blendData.Surface1Tag;var support=Surfaces[GetSurfaceSlotByTag(source)];
        var error=CheckGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(source));if(error!=0)return error;
        error=CheckGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(blend));if(error!=0)return error;
        Span<double> chart=stackalloc double[3*n],uvs=stackalloc double[2*n];
        Span<KernelVector3> tangents=stackalloc KernelVector3[n];
        var program=GetNestedBlendProgram(blendData);var v=program.Surfaces[blendData.ProgramRoot].ReverseSection?side:1-side;
        SurfaceDerivativeLayout.TryCreate(1,0,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var direction=Math.Sign(u[1]-u[0]);if(direction==0)return ParasolidConstants.PK_ERROR_bad_parameter;
        for(var i=0;i<n;i++)
        {
            if(i>0&&direction*(u[i]-u[i-1])<=0)return ParasolidConstants.PK_ERROR_not_implemented;
            var status=JointNestedBlendEvaluation.Evaluate(program,blendData.ProgramRoot,u[i],v,layout,jet);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            if(ProfileDistance(jet[0],positions[i])>1e-8)return ParasolidConstants.PK_ERROR_not_on_curve;
            tangents[i]=Scale(Unit(jet[1]),direction);
            chart[3*i]=positions[i].X;chart[3*i+1]=positions[i].Y;chart[3*i+2]=positions[i].Z;
            var uv=new PK_UV_s();
            if(support.Class==SurfaceClass.BSurface)
            {
                status=JointNestedBlendEvaluation.SplineContactParameters(program,blendData.ProgramRoot,u[i],side,out var su,out var sv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                uv.param[0]=su;uv.param[1]=sv;
                SurfaceDerivativeLayout.TryCreate(0,0,out var pointLayout);status=EvaluateSurfaceCore(in support,su,sv,pointLayout,jet,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                if(ProfileDistance(jet[0],positions[i])>1e-8)return ParasolidConstants.PK_ERROR_not_on_surface;
            }
            else{status=IntersectionSurfaceParameters(support,positions[i],out uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);}
            uvs[2*i]=uv.param[0];uvs[2*i+1]=uv.param[1];
        }
        Span<double> prepared=stackalloc double[6*n-4];u.CopyTo(prepared);
        var parameters=prepared.Slice(n,n);var scales=prepared.Slice(2*n,n-1);var chords=MemoryMarshal.Cast<double,KernelVector3>(prepared[(3*n-1)..]);
        var build=OriginalChartParameterMap.Build(positions,tangents,baseParameter,baseScale,parameters,scales,chords,out _,out _);if(build!=AlgorithmStatus.Success)return IntersectionError(build);
        // A contact differs from its center by a unit radial vector times r;
        // its chord deviation is bounded by the spine's bound plus 2r.
        var chartError=program.Spines[program.Surfaces[blendData.ProgramRoot].Spine].ChordalError+2*Math.Abs(blendData.Range0);
        var input=new IcurveDecodeInput{Surface0Tag=source,Surface1Tag=blend,BaseParameter=baseParameter,BaseScale=baseScale,ChartCount=n,ChartHvecs=chart,
            UvType=IntersectionUvType.First,UvValues=uvs,ChordalError=chartError,AngularError=Math.PI,
            Start=new(){Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart[..3]},
            End=new(){Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart[^3..]},Sense=sense};
        var decoded=DecodeIcurve(input,out var slot,out _,out _);if(decoded!=AlgorithmStatus.Success)return IntersectionError(decoded);
        ref var data=ref ICurveDataPool[slot];data.IsBlendContact=1;data.BlendContactSide=side;
        var storage=State.Session->Blocks.TryAllocate((nuint)((6*n-4)*8));
        if(storage==null){FreeICurveData(slot);return ParasolidConstants.PK_ERROR_memory_full;}
        data.ContactChartBlock=State.Session->Blocks.HandleOf(storage);
        if(data.ContactChartBlock<=0){State.Session->Blocks.Free(storage);FreeICurveData(slot);return ParasolidConstants.PK_ERROR_memory_full;}
        prepared.CopyTo(BlendContactMap(data));
        if(!Curves.TryAllocate(out var cs)){FreeICurveData(slot);return ParasolidConstants.PK_ERROR_memory_full;}
        ref var record=ref Curves[cs];AssignPartition(ref record.Header,CurrentPartition);record.Class=CurveClass.ICurve;record.DataIndex=slot;record.OwnerEdge=-1;
        record.TMin=parameters[0];record.TMax=parameters[^1];record.Sense=data.Sense;
        var tag=AllocateTag(EntityClass.Curve,PoolKind.Curve,cs,record.Header.Generation);
        if(tag<=0){FreeICurveData(slot);Curves.Free(cs);return ParasolidConstants.PK_ERROR_memory_full;}
        AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(source));AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(blend));record.DependenciesAcquired=1;data.OwnsSupportSurfaces=1;
        error=TryAdoptDependentGeometry(tag);if(error!=0)return error;curve=tag;return 0;
    }

    private static AlgorithmStatus EvaluateBlendContact(in ICurveData data,double parameter,DerivativeOrder order,Span<KernelVector3> output,out double u)
    {
        u=0;if(order>2)return AlgorithmStatus.Unsupported;
        var n=data.ChartCount;var map=BlendContactMap(data);var station=map[..n];var parameters=map.Slice(n,n);var scales=map.Slice(2*n,n-1);
        var chords=MemoryMarshal.Cast<double,KernelVector3>(map[(3*n-1)..]);var positions=MemoryMarshal.Cast<double,KernelVector3>(HvecSpan(data).Slice(3*data.StartLimit.HvecCount,3*n));
        if(parameter<parameters[0]-1e-10||parameter>parameters[^1]+1e-10)return AlgorithmStatus.InvalidInput;
        parameter=Math.Clamp(parameter,parameters[0],parameters[^1]);var segment=0;while(segment<n-2&&parameters[segment+1]<=parameter)segment++;
        var lo=Math.Min(station[segment],station[segment+1]);var hi=Math.Max(station[segment],station[segment+1]);
        u=Math.Clamp(station[segment]+(station[segment+1]-station[segment])*(parameter-parameters[segment])/(parameters[segment+1]-parameters[segment]),lo,hi);
        var blend=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)].DataIndex];var program=GetNestedBlendProgram(blend);var v=BlendContactV(data);
        SurfaceDerivativeLayout.TryCreate(Math.Max(1,order),0,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[3];
        var converged=false;var denominator=0.0;
        for(var iteration=0;iteration<16;iteration++)
        {
            var status=JointNestedBlendEvaluation.Evaluate(program,blend.ProgramRoot,u,v,layout,jet);if(status!=AlgorithmStatus.Success)return status;
            denominator=Dot(jet[1],chords[segment])*scales[segment];
            if(Math.Abs(denominator)<1e-14)return AlgorithmStatus.Singular;
            var residual=Dot(Sub(jet[0],positions[segment]),chords[segment])*scales[segment]+parameters[segment]-parameter;
            if(Math.Abs(residual)<1e-12){converged=true;break;}
            u=Math.Clamp(u-residual/denominator,lo,hi);
        }
        if(!converged)return AlgorithmStatus.NotConverged;output[0]=jet[0];
        if(order>=1)output[1]=Scale(jet[1],1/denominator);
        if(order>=2)output[2]=Scale(Sub(jet[2],Scale(jet[1],Dot(jet[2],chords[segment])*scales[segment]/denominator)),1/(denominator*denominator));
        return AlgorithmStatus.Success;
    }

    private static int BlendContactParameter(in ICurveData data,in KernelVector3 point,double u,out double parameter)
    {
        parameter=0;var n=data.ChartCount;var map=BlendContactMap(data);var station=map[..n];var ts=map.Slice(n,n);var scales=map.Slice(2*n,n-1);
        var chords=MemoryMarshal.Cast<double,KernelVector3>(map[(3*n-1)..]);var positions=MemoryMarshal.Cast<double,KernelVector3>(HvecSpan(data).Slice(3*data.StartLimit.HvecCount,3*n));
        for(var i=0;i<n-1;i++)if(u>=Math.Min(station[i],station[i+1])-1e-9&&u<=Math.Max(station[i],station[i+1])+1e-9)
        {
            parameter=ts[i]+scales[i]*Dot(Sub(point,positions[i]),chords[i]);
            if(parameter>=ts[i]-1e-8&&parameter<=ts[i+1]+1e-8){parameter=Math.Clamp(parameter,ts[i],ts[i+1]);return 0;}
        }
        return ParasolidConstants.PK_ERROR_not_on_curve;
    }

    private static int BlendContactParameterAtStation(in ICurveData data,in KernelVector3 point,double u,out double parameter)
    {
        parameter=0;var n=data.ChartCount;var map=BlendContactMap(data);var station=map[..n];var ts=map.Slice(n,n);var scales=map.Slice(2*n,n-1);
        var chords=MemoryMarshal.Cast<double,KernelVector3>(map[(3*n-1)..]);var positions=MemoryMarshal.Cast<double,KernelVector3>(HvecSpan(data).Slice(3*data.StartLimit.HvecCount,3*n));
        // The contact map is built with strictly monotone blend stations.
        // A station at a knot names the shared native endpoint exactly.
        for(var i=0;i<n;i++)
        {
            if(u==station[i]){parameter=ts[i];return 0;}
            if(i+1==n||!(u>Math.Min(station[i],station[i+1])&&u<Math.Max(station[i],station[i+1])))continue;
            parameter=ts[i]+scales[i]*Dot(Sub(point,positions[i]),chords[i]);
            return parameter>ts[i]&&parameter<ts[i+1]?0:ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        return ParasolidConstants.PK_ERROR_not_on_curve;
    }

    private static AlgorithmStatus BlendContactInterval(in ICurveData data,double first,double last,out double lower,out double upper)
    {
        lower=upper=0;Span<KernelVector3> point=stackalloc KernelVector3[1];
        var status=EvaluateBlendContact(data,first,0,point,out var a);if(status!=AlgorithmStatus.Success)return status;
        status=EvaluateBlendContact(data,last,0,point,out var b);if(status!=AlgorithmStatus.Success)return status;
        lower=Math.Min(a,b);upper=Math.Max(a,b);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AppendBlendContactRoots(in ICurveData data,double first,double last,ReadOnlySpan<OperationIntersectionPoint> roots,
        Span<OperationIntersectionPoint> output,ref BufferCount count)
    {
        foreach(var root in roots)
        {
            if(BlendContactParameter(data,root.Point,root.First,out var t)!=0)return AlgorithmStatus.NotConverged;
            if(OperationIntersection.InInterval(t,first,last)&&!OperationIntersection.AddPoint(output,ref count,root.Point,t,0,root.Kind))return AlgorithmStatus.OutputTooSmall;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectBlendContactPlane(CurveSlot curve,double first,double last,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[curve].DataIndex];
        if(TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(data.Surface0Tag)],out var support)&&support.Kind==SurfaceClass.Plane
            &&OperationIntersection.Norm(ProfileCross(support.Axis,plane.Axis))<1e-14&&Math.Abs(Dot(Sub(support.Origin,plane.Origin),plane.Axis))<1e-12)
        {
            Span<KernelVector3> point=stackalloc KernelVector3[1];var found=EvaluateBlendContact(data,first,0,point,out _);if(found!=AlgorithmStatus.Success)return found;
            if(!OperationIntersection.AddPoint(output,ref count,point[0],first,0,IntersectionVectorKind.Start))return AlgorithmStatus.OutputTooSmall;
            found=EvaluateBlendContact(data,last,0,point,out _);if(found!=AlgorithmStatus.Success)return found;
            return OperationIntersection.AddPoint(output,ref count,point[0],last,0,IntersectionVectorKind.End)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        var status=BlendContactInterval(data,first,last,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return status;
        var surface=GetSurfaceSlotByTag(data.Surface1Tag);IntervalBox3 extent=default;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,poles,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var a=Math.Max(lo,patch.Domain.U0);var b=Math.Min(hi,patch.Domain.U1);if(!(b>a))continue;
            var iso=new SurfaceIntersectionPatch(patch,false,BlendContactV(data),a,b);var n=0;
            status=SurfaceIsoPlaneRoots.Find(iso,plane,a,b,roots,ref n);if(status!=AlgorithmStatus.Success)return status;
            status=AppendBlendContactRoots(data,first,last,roots[..n],output,ref count);if(status!=AlgorithmStatus.Success)return status;
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectBlendContactSurface(SurfaceSlot target,CurveSlot curve,double first,double last,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[curve].DataIndex];var surface=GetSurfaceSlotByTag(data.Surface1Tag);
        var status=BlendContactInterval(data,first,last,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return status;
        status=RegularSurfaceExtent(surface,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,ap,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var a=Math.Max(lo,patch.Domain.U0);var b=Math.Min(hi,patch.Domain.U1);if(!(b>a))continue;
            var iso=new SurfaceIntersectionPatch(patch,false,BlendContactV(data),a,b);
            for(var j=0;j<RegularSurfacePatchCount(target);j++)
            {
                status=RegularSurfacePatch(target,j,extent,bp,out var support);if(status!=AlgorithmStatus.Success)return status;
                var box=support.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
                var cell=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=a},Upper=new(){U=box.U1,V=box.V1,S=b}};
                status=ParametricSurfaceIntersection.Isolate(support,iso,cell,3,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var k=0;k<n;k++)
                {
                    if(BlendContactParameter(data,roots[k].Point,roots[k].S,out var t)!=0)return AlgorithmStatus.NotConverged;
                    if(OperationIntersection.InInterval(t,first,last)&&!OperationIntersection.AddPoint(output,ref count,roots[k].Point,t,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectBlendContactRay(SurfTag target,PK_UV_s query,double du,double dv,in ModelingEdge edge,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[GetCurveSlotByTag(edge.Curve)].DataIndex];var fixedV=BlendContactV(data);
        if(SameSheetSupport(target,data.Surface1Tag))
        {
            if(dv==0)return Math.Abs(query.param[1]-fixedV)<1e-12?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
            var along=(fixedV-query.param[1])/dv;var u=query.param[0]+du*along;if(along< -1e-10)return AlgorithmStatus.Success;
            var blend=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)].DataIndex];SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
            var record=Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)];if(!OperationIntersection.InInterval(u,record.UMin,record.UMax))return AlgorithmStatus.Success;
            Span<KernelVector3> point=stackalloc KernelVector3[1];var status=JointNestedBlendEvaluation.Evaluate(GetNestedBlendProgram(blend),blend.ProgramRoot,u,fixedV,layout,point);if(status!=AlgorithmStatus.Success)return status;
            if(BlendContactParameter(data,point[0],u,out var t)!=0||!OperationIntersection.InInterval(t,edge.Start,edge.End))return AlgorithmStatus.Success;
            return OperationIntersection.AddPoint(output,ref count,point[0],along,t,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        var source=GetSurfaceSlotByTag(target);var contact=GetSurfaceSlotByTag(data.Surface1Tag);
        var band=BlendedEdgeDataPool[Surfaces[contact].DataIndex];var program=GetNestedBlendProgram(band,Surfaces[contact]);
        if(SameSheetSupport(target,data.Surface0Tag)&&Surfaces[source].Class==SurfaceClass.BSurface
            &&!SpineParameterMap.Explicit(program.Spines[program.Surfaces[band.ProgramRoot].Spine]))
            return IntersectSplineContactParameterRay(source,data,program,band.ProgramRoot,edge,query,du,dv,output,out count);
        var found=BlendContactInterval(data,edge.Start,edge.End,out var first,out var last);if(found!=AlgorithmStatus.Success)return found;
        found=RegularSurfaceExtent(source,out var extent);if(found!=AlgorithmStatus.Success)return found;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var i=0;i<RegularSurfacePatchCount(source);i++)
        {
            found=RegularSurfacePatch(source,i,extent,ap,out var patch);if(found!=AlgorithmStatus.Success)return found;
            if(patch.Domain.U1<=patch.Domain.U0||patch.Domain.V1<=patch.Domain.V0)continue;
            var lo=0.0;var hi=double.PositiveInfinity;
            if(!ClipParameterRay(query.param[0],du,patch.Domain.U0,patch.Domain.U1,ref lo,ref hi)
                ||!ClipParameterRay(query.param[1],dv,patch.Domain.V0,patch.Domain.V1,ref lo,ref hi)||!(hi>lo)||!double.IsFinite(hi))continue;
            var ray=new SurfaceIntersectionPatch(patch,query.param[0],query.param[1],du,dv,lo,hi);
            for(var j=0;j<RegularSurfacePatchCount(contact);j++)
            {
                found=RegularSurfacePatch(contact,j,extent,bp,out var support);if(found!=AlgorithmStatus.Success)return found;
                var a=Math.Max(first,support.Domain.U0);var b=Math.Min(last,support.Domain.U1);if(!(b>a))continue;
                var iso=new SurfaceIntersectionPatch(support,false,fixedV,a,b);var n=0;
                found=BezierCurveIntersection.Find(ray,iso,lo,hi,a,b,roots,ref n);if(found!=AlgorithmStatus.Success)return found;
                for(var k=0;k<n;k++)
                {
                    var root=roots[k];if(BlendContactParameter(data,root.Point,root.Second,out var t)!=0)return AlgorithmStatus.NotConverged;
                    if(OperationIntersection.InInterval(t,edge.Start,edge.End)&&!OperationIntersection.AddPoint(output,ref count,root.Point,root.First,t,root.Kind))return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
