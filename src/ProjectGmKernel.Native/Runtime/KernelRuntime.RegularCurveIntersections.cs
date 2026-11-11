using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectRegularRay(SurfaceSlot surface,in KernelVector3 origin,in KernelVector3 direction,double upper,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var status=RegularSurfaceExtent(surface,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],line=stackalloc IntervalNumber[8];
        line[0]=origin.X;line[1]=origin.Y;line[2]=origin.Z;line[3]=line[7]=1;
        line[4]=new IntervalNumber(origin.X)+new IntervalNumber(direction.X)*upper;
        line[5]=new IntervalNumber(origin.Y)+new IntervalNumber(direction.Y)*upper;
        line[6]=new IntervalNumber(origin.Z)+new IntervalNumber(direction.Z)*upper;
        var ray=new SurfaceIntersectionPatch(new BezierSurfacePatch(line,1,0,new(0,upper,0,1)));
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,poles,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var box=patch.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
            var domain=new ParameterCell{Lower=new(){U=box.U0,V=box.V0},Upper=new(){U=box.U1,V=box.V1,S=upper}};
            status=ParametricSurfaceIntersection.Isolate(patch,ray,domain,3,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
            for(var r=0;r<n;r++)
            {
                if(!OperationIntersection.AddPoint(output,ref count,roots[r].Point,roots[r].S,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                for(var k=0;k<count;k++)if(Math.Abs(output[k].First-roots[r].S)<1e-10){output[k].U=roots[r].U;output[k].V=roots[r].V;}
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectRegularSurfaceCurve(SurfTag surface,CurveTag curve,double first,double last,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var slot=GetSurfaceSlotByTag(surface);var cs=GetCurveSlotByTag(curve);
        if(slot<0||cs<0)return AlgorithmStatus.InvalidInput;
        var contactError=IsRationalContact(surface,Curves[cs],first,last,out var rationalContact);
        if(contactError!=0)return AlgorithmStatus.NotConverged;
        if(rationalContact||TryAnalyticBlendContactMap(surface,Curves[cs],first,last,out _,out _,out _))
        {
            if(output.Length<2)return AlgorithmStatus.OutputTooSmall;Span<KernelVector3> point=stackalloc KernelVector3[1];
            var status=EvaluateCurveCore(in Curves[cs],first,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            output[count++]=new(){Point=point[0],First=first,Kind=IntersectionVectorKind.Start};
            status=EvaluateCurveCore(in Curves[cs],last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            output[count++]=new(){Point=point[0],First=last,Kind=IntersectionVectorKind.End};return AlgorithmStatus.Success;
        }
        if(Curves[cs].Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[Curves[cs].DataIndex];
            if(RegularCoincidentSupports(surface,data.Surface0Tag)||RegularCoincidentSupports(surface,data.Surface1Tag))
            {
                if(output.Length<2)return AlgorithmStatus.OutputTooSmall;Span<KernelVector3> point=stackalloc KernelVector3[2];
                var status=EvaluateCurveCore(in Curves[cs],first,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
                output[count++]=new(){Point=point[0],First=first,Kind=IntersectionVectorKind.Start};
                status=EvaluateCurveCore(in Curves[cs],last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
                output[count++]=new(){Point=point[0],First=last,Kind=IntersectionVectorKind.End};return AlgorithmStatus.Success;
            }
        }
        if(Surfaces[slot].Class==SurfaceClass.Plane)return IntersectSheetCurvePlane(curve,first,last,surface,output,out count);
        if(RegularPlanarSupport(slot,out var plane,out _))return IntersectSheetCurvePlane(curve,first,last,plane,output,out count);
        if(Curves[cs].Class==CurveClass.ICurve&&ICurveDataPool[Curves[cs].DataIndex].IsBlendContact!=0)
            return IntersectBlendContactSurface(slot,cs,first,last,output,out count);
        if(Curves[cs].Class==CurveClass.ICurve)return IntersectRegularICurveSurface(slot,cs,first,last,output,out count);
        if(TryBlendIsoparam(cs,out var isoSurface,out var fixedV))
        {
            var status=RegularSurfaceExtent(isoSurface,out var extent);if(status!=AlgorithmStatus.Success)return status;
            Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
            Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
            for(var i=0;i<RegularSurfacePatchCount(isoSurface);i++)
            {
                status=RegularSurfacePatch(isoSurface,i,extent,ap,out var basis);if(status!=AlgorithmStatus.Success)return status;
                var lo=Math.Max(first,basis.Domain.U0);var hi=Math.Min(last,basis.Domain.U1);if(!(hi>lo))continue;
                var iso=new SurfaceIntersectionPatch(basis,false,fixedV,lo,hi);
                for(var j=0;j<RegularSurfacePatchCount(slot);j++)
                {
                    status=RegularSurfacePatch(slot,j,extent,bp,out var target);if(status!=AlgorithmStatus.Success)return status;
                    var box=target.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
                    var domain=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=lo},Upper=new(){U=box.U1,V=box.V1,S=hi}};
                    status=ParametricSurfaceIntersection.Isolate(target,iso,domain,3,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<n;k++)if(!OperationIntersection.AddPoint(output,ref count,roots[k].Point,roots[k].S,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                }
            }
            OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
        }
        var interval=new PK_INTERVAL_s();interval.value[0]=first;interval.value[1]=last;
        if(ResolveIntersectionCurve(curve,interval,out var span)!=0)return AlgorithmStatus.Unsupported;
        if(AffineCurveOnSurfaceBoundary(slot,span))
        {
            if(output.Length<2)return AlgorithmStatus.OutputTooSmall;
            Span<KernelVector3> point=stackalloc KernelVector3[2];
            var status=EvaluateCurveCore(in Curves[cs],first,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            output[count++]=new(){Point=point[0],First=first,Kind=IntersectionVectorKind.Start};
            status=EvaluateCurveCore(in Curves[cs],last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
            if(count==output.Length)return AlgorithmStatus.OutputTooSmall;output[count++]=new(){Point=point[0],First=last,Kind=IntersectionVectorKind.End};return AlgorithmStatus.Success;
        }
        AlgorithmStatus found;
        if(TryPrepareSurface(Surfaces[slot],out var analytic))found=IntersectSurfaceCurve(analytic,span,output,out count);
        else if(Surfaces[slot].Class==SurfaceClass.BSurface)found=IntersectBSurfaceCurve(slot,span,output,out count);
        else found=IntersectBlendPrimitiveCurve(slot,span,output,out count);
        if(found!=AlgorithmStatus.Success)return found;
        for(var i=0;i<count;i++)output[i].First=(output[i].First-span.Offset)/span.Scale;
        OperationIntersection.Sort(output[..count]);return found;
    }

    private static AlgorithmStatus IntersectBlendPrimitiveCurve(SurfaceSlot surface,in IntersectionCurveSpan span,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;if(Surfaces[surface].Class!=SurfaceClass.BlendSurface)return AlgorithmStatus.Unsupported;
        var pieces=GeneralCurvePieceCount(span);if(pieces<=0)return AlgorithmStatus.Unsupported;
        var status=RegularSurfaceExtent(surface,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[4*17];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(surface);i++)
        {
            status=RegularSurfacePatch(surface,i,extent,ap,out var patch);if(status!=AlgorithmStatus.Success)return status;
            for(var j=0;j<pieces;j++)
            {
                status=GeneralCurvePiece(span,j,bp,out var degree,out var box,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return status;if(!(hi>lo))continue;
                var piece=new SurfaceIntersectionPatch(new BezierSurfacePatch(bp[..(4*(degree+1))],degree,0,box));
                var domain=new ParameterCell{Lower=new(){U=patch.Domain.U0,V=patch.Domain.V0,S=lo},Upper=new(){U=patch.Domain.U1,V=patch.Domain.V1,S=hi}};
                status=ParametricSurfaceIntersection.Isolate(patch,piece,domain,3,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var k=0;k<n;k++)
                {
                    if(!GeneralCurveNativeParameter(span,roots[k].Point,roots[k].S,out var parameter))return AlgorithmStatus.NumericalFailure;
                    if(!OperationIntersection.AddPoint(output,ref count,roots[k].Point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IsolateFiniteSurfaceLine(in ModelingEdge edge,SurfaceSlot surface,Span<OperationIntersectionPoint> output,
        Span<ScalarRootIsolation> lineParameters,Span<double> spatialErrors,out BufferCount count,
        scoped ReadOnlySpan<IntervalNumber> linePoles,Span<IntervalNumber> blendStations,Span<SurfaceParameterBox> supportDomains=default)
    {
        count=0;if(!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(edge.Curve)],out var line))return AlgorithmStatus.Unsupported;
        if(!(edge.End>edge.Start))return AlgorithmStatus.InvalidInput;
        Span<BlendSpineInstruction> spine=stackalloc BlendSpineInstruction[1]{new(){Curve=line,ChartCount=2}};
        Span<BlendChartInstruction> domain=stackalloc BlendChartInstruction[2]{new(){Parameter=edge.Start},new(){Parameter=edge.End}};
        var program=new NestedBlendProgram(default,spine,domain);
        var corrected=!linePoles.IsEmpty;
        var path=corrected?new SurfaceIntersectionPatch(new BezierSurfacePatch(linePoles,1,0,new(edge.Start,edge.End,0,1)))
            :new SurfaceIntersectionPatch(program,0,edge.Start,edge.End);
        var meridian=IsolateRollingMeridianLine(Surfaces[surface],line,path,linePoles,edge.Start,edge.End,output,lineParameters,spatialErrors,
            blendStations,out count);
        if(meridian is AlgorithmStatus.Success or AlgorithmStatus.OutputTooSmall)return meridian;
        count=0;
        var extentStatus=RegularSurfaceExtent(surface,out var extent);if(extentStatus!=AlgorithmStatus.Success)return extentStatus;
        Span<OperationIntersectionPoint> nominal=stackalloc OperationIntersectionPoint[corrected?output.Length:0];
        Span<ScalarRootIsolation> nominalParameters=stackalloc ScalarRootIsolation[nominal.Length];
        Span<double> nominalErrors=stackalloc double[nominal.Length];
        var nominalCount=0;
        if(corrected&&IsolateFiniteSurfaceLine(edge,surface,nominal,nominalParameters,nominalErrors,out nominalCount,default,default)!=AlgorithmStatus.Success)nominalCount=0;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> enclosure=stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        Span<IsolatedRootCertificate> certificates=stackalloc IsolatedRootCertificate[64],accepted=stackalloc IsolatedRootCertificate[output.Length];
        var patches=RegularSurfacePatchCount(surface);if(patches==0)return AlgorithmStatus.Unsupported;
        for(var i=0;i<patches;i++)
        {
            var status=RegularSurfacePatch(surface,i,extent,poles,out var patch);if(status!=AlgorithmStatus.Success)return status;
            var box=patch.Domain;if(!(box.U1>box.U0&&box.V1>box.V0))continue;
            var search=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=edge.Start},Upper=new(){U=box.U1,V=box.V1,S=edge.End}};
            status=ParametricSurfaceIntersection.Isolate(patch,path,search,3,roots,out var n,certificates);if(status!=AlgorithmStatus.Success)return status;
            for(var r=0;r<n;r++)
            {
                var proof=certificates[r];var interval=new IntervalNumber(proof.Second.U0,proof.Second.U1);
                if(!(interval.Lower>edge.Start&&interval.Upper<edge.End))return AlgorithmStatus.NotConverged;
                var duplicate=false;
                for(var k=0;k<count;k++)
                {
                    var previous=lineParameters[k].Root;if(previous.Upper<interval.Lower||interval.Upper<previous.Lower)continue;
                    if(!proof.MayShareRoot(accepted[k])||!proof.ProvesSameRoot(accepted[k]))return AlgorithmStatus.NotConverged;
                    duplicate=true;break;
                }
                if(duplicate)continue;
                if(count==output.Length)return AlgorithmStatus.OutputTooSmall;
                var t=Math.Clamp(roots[r].S,interval.Lower,interval.Upper);
                // The represented edge's root is only a point estimate.
                // Keep it if it lies in the independently proved box.
                for(var k=0;k<nominalCount;k++)
                    if(nominal[k].First>=interval.Lower&&nominal[k].First<=interval.Upper){t=nominal[k].First;break;}
                status=SpineParameterMap.Series(line,new(t,0),out var point,out _);if(status!=AlgorithmStatus.Success)return status;
                IntervalVector bound;
                if(corrected)
                {
                    status=path.Bound(new(interval.Lower,interval.Upper,0,0),enclosure);if(status!=AlgorithmStatus.Success)return status;
                    bound=new(enclosure[0],enclosure[1],enclosure[2]);
                }
                else
                {status=SpineParameterMap.Interval(line,new(interval,0),out bound,out _);if(status!=AlgorithmStatus.Success)return status;}
                var error=(bound-new IntervalVector(point.Value)).Norm().Value.Upper;
                if(!double.IsFinite(error)||error>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NotConverged;
                var at=count++;
                while(at>0&&interval.Lower<lineParameters[at-1].Root.Lower)
                {accepted[at]=accepted[at-1];lineParameters[at]=lineParameters[at-1];output[at]=output[at-1];spatialErrors[at]=spatialErrors[at-1];
                    if(corrected)blendStations[at]=blendStations[at-1];at--;}
                accepted[at]=proof;lineParameters[at]=new(){Root=interval,Certified=true};spatialErrors[at]=error;
                if(!supportDomains.IsEmpty)
                {
                    if(count>supportDomains.Length)return AlgorithmStatus.OutputTooSmall;
                    for(var k=count-1;k>at;k--)supportDomains[k]=supportDomains[k-1];
                    supportDomains[at]=proof.First;
                }
                if(corrected)blendStations[at]=new(proof.First.U0,proof.First.U1);
                output[at]=new(){Point=point.Value,First=t,U=Math.Clamp(roots[r].U,proof.First.U0,proof.First.U1),
                    V=Math.Clamp(roots[r].V,proof.First.V0,proof.First.V1),Kind=IntersectionVectorKind.Simple};
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IsolateFiniteSplineSurface(in ModelingEdge edge,SurfaceSlot surface,
        Span<OperationIntersectionPoint> output,Span<ScalarRootIsolation> parameters,Span<double> errors,out BufferCount count,
        Span<IsolatedRootCertificate> outputProofs=default)
    {
        count=0;var slot=GetCurveSlotByTag(edge.Curve);var record=Curves[slot];
        if(record.Class!=CurveClass.BCurve||Surfaces[surface].Class!=SurfaceClass.BSurface)return AlgorithmStatus.Unsupported;
        var span=new IntersectionCurveSpan{Slot=slot,Scale=1,Lower=edge.Start,Upper=edge.End};
        var status=RegularSurfaceExtent(surface,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> surfacePoles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],curvePoles=stackalloc IntervalNumber[4*17],bounds=stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        Span<IsolatedRootCertificate> certificates=stackalloc IsolatedRootCertificate[64];
        Span<IsolatedRootCertificate> accepted=outputProofs.IsEmpty?stackalloc IsolatedRootCertificate[output.Length]:outputProofs;
        Span<KernelVector3> point=stackalloc KernelVector3[2];
        for(var s=0;s<RegularSurfacePatchCount(surface);s++)
        {
            status=RegularSurfacePatch(surface,s,extent,surfacePoles,out var target);if(status!=AlgorithmStatus.Success)return status;
            var box=target.Domain;if(!(box.U1>box.U0&&box.V1>box.V0))continue;
            for(var c=0;c<GeneralCurvePieceCount(span);c++)
            {
                status=GeneralCurvePiece(span,c,curvePoles,out var degree,out var domain,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return status;
                if(!(hi>lo))continue;
                var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(curvePoles[..(4*(degree+1))],degree,0,domain));
                var search=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=lo},Upper=new(){U=box.U1,V=box.V1,S=hi}};
                status=ParametricSurfaceIntersection.Isolate(target,source,search,3,roots,out var n,certificates);if(status!=AlgorithmStatus.Success)return status;
                for(var r=0;r<n;r++)
                {
                    var proof=certificates[r];var interval=new IntervalNumber(proof.Second.U0,proof.Second.U1);var duplicate=false;
                    for(var k=0;k<count;k++)
                    {
                        if(parameters[k].Root.Upper<interval.Lower||interval.Upper<parameters[k].Root.Lower)continue;
                        if(!proof.MayShareRoot(accepted[k])||!proof.ProvesSameRoot(accepted[k]))return AlgorithmStatus.NotConverged;
                        duplicate=true;break;
                    }
                    if(duplicate)continue;
                    if(count==output.Length||count==parameters.Length||count==errors.Length)return AlgorithmStatus.OutputTooSmall;
                    var t=Math.Clamp(roots[r].S,interval.Lower,interval.Upper);
                    status=EvaluateCurveCore(in record,t,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
                    status=source.Bound(new(interval.Lower,interval.Upper,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                    var error=(new IntervalVector(bounds[0],bounds[1],bounds[2])-new IntervalVector(point[0])).Norm().Value.Upper;
                    if(!double.IsFinite(error)||error>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NotConverged;
                    var at=count++;
                    while(at>0&&interval.Lower<parameters[at-1].Root.Lower)
                    {accepted[at]=accepted[at-1];parameters[at]=parameters[at-1];output[at]=output[at-1];errors[at]=errors[at-1];at--;}
                    accepted[at]=proof;parameters[at]=new(){Root=interval,Certified=true};errors[at]=error;
                    output[at]=new(){Point=point[0],First=t,U=roots[r].U,V=roots[r].V,Kind=interval.Lower==interval.Upper&&t==edge.Start?IntersectionVectorKind.Start
                        :interval.Lower==interval.Upper&&t==edge.End?IntersectionVectorKind.End:IntersectionVectorKind.Simple};
                }
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectRegularICurveSurface(SurfaceSlot surface,CurveSlot curve,double first,double last,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[curve].DataIndex];var a=GetSurfaceSlotByTag(data.Surface0Tag);var b=GetSurfaceSlotByTag(data.Surface1Tag);
        var status=RegularSurfaceExtent(surface,out var extent);
        if(status!=AlgorithmStatus.Success){status=RegularSurfaceExtent(a,out extent);if(status!=AlgorithmStatus.Success)status=RegularSurfaceExtent(b,out extent);}
        if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],cp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        for(var i=0;i<RegularSurfacePatchCount(a);i++)
        {
            status=RegularSurfacePatch(a,i,extent,ap,out var firstPatch);if(status!=AlgorithmStatus.Success)return status;
            if(firstPatch.Domain.U1<=firstPatch.Domain.U0||firstPatch.Domain.V1<=firstPatch.Domain.V0)continue;
            for(var j=0;j<RegularSurfacePatchCount(b);j++)
            {
                status=RegularSurfacePatch(b,j,extent,bp,out var secondPatch);if(status!=AlgorithmStatus.Success)return status;
                if(secondPatch.Domain.U1<=secondPatch.Domain.U0||secondPatch.Domain.V1<=secondPatch.Domain.V0)continue;
                for(var k=0;k<RegularSurfacePatchCount(surface);k++)
                {
                    status=RegularSurfacePatch(surface,k,extent,cp,out var thirdPatch);if(status!=AlgorithmStatus.Success)return status;
                    if(thirdPatch.Domain.U1<=thirdPatch.Domain.U0||thirdPatch.Domain.V1<=thirdPatch.Domain.V0)continue;
                    status=SurfaceTripleRoots.Find(firstPatch,secondPatch,thirdPatch,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                    for(var r=0;r<n;r++)
                    {
                        var vector=new PK_VECTOR_s();WriteVector(&vector,roots[r].Point);double parameter=0;var error=CurveParameteriseVectorCore(Curves[curve].Header.Tag,vector,&parameter);
                        if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                        if(OperationIntersection.InInterval(parameter,first,last)&&!OperationIntersection.AddPoint(output,ref count,roots[r].Point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                    }
                }
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
