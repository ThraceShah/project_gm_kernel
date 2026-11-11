using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool NativeTripleSurfaceApplicable(CurveTag curve,SurfaceSlot target)
    {
        var record=Curves[GetCurveSlotByTag(curve)];
        if(record.Class!=CurveClass.ICurve||Surfaces[target].Class!=SurfaceClass.BSurface||TryBoundaryPlane(target,out _))return false;
        var data=ICurveDataPool[record.DataIndex];
        return data.IsBlendContact==0&&!RegularCoincidentSupports(Surfaces[target].Header.Tag,data.Surface0Tag)
            &&!RegularCoincidentSupports(Surfaces[target].Header.Tag,data.Surface1Tag)
            &&Surfaces[GetSurfaceSlotByTag(data.Surface0Tag)].Class is SurfaceClass.BSurface or SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere
            &&Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)].Class is SurfaceClass.BSurface or SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere;
    }

    private static AlgorithmStatus IsolateFiniteNativeTriple(in ModelingEdge edge,SurfTag contact,SurfaceSlot target,
        Span<OperationIntersectionPoint> output,Span<ScalarRootIsolation> isolation,Span<double> errors,
        Span<SurfaceParameterBox> commonUv,Span<SurfaceParameterBox> targetUv,out BufferCount count)
    {
        count=0;var data=ICurveDataPool[Curves[GetCurveSlotByTag(edge.Curve)].DataIndex];
        var a=GetSurfaceSlotByTag(data.Surface0Tag);var b=GetSurfaceSlotByTag(data.Surface1Tag);
        var side=SameSheetSupport(contact,data.Surface0Tag)?0:SameSheetSupport(contact,data.Surface1Tag)?1:-1;
        if(side<0)return AlgorithmStatus.Unsupported;
        var status=RegularSurfaceExtent(target,out var extent);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],cp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bounds=stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        Span<SurfaceTripleRootCertificate> certificates=stackalloc SurfaceTripleRootCertificate[64];
        for(var i=0;i<RegularSurfacePatchCount(a);i++)
        {
            status=RegularSurfacePatch(a,i,extent,ap,out var first);if(status!=AlgorithmStatus.Success)return status;
            if(!(first.Domain.U1>first.Domain.U0&&first.Domain.V1>first.Domain.V0))continue;
            for(var j=0;j<RegularSurfacePatchCount(b);j++)
            {
                status=RegularSurfacePatch(b,j,extent,bp,out var second);if(status!=AlgorithmStatus.Success)return status;
                if(!(second.Domain.U1>second.Domain.U0&&second.Domain.V1>second.Domain.V0))continue;
                for(var k=0;k<RegularSurfacePatchCount(target);k++)
                {
                    status=RegularSurfacePatch(target,k,extent,cp,out var third);if(status!=AlgorithmStatus.Success)return status;
                    if(!(third.Domain.U1>third.Domain.U0&&third.Domain.V1>third.Domain.V0))continue;
                    status=SurfaceTripleRoots.Find(first,second,third,roots,out var n,certificates);if(status!=AlgorithmStatus.Success)return status;
                    for(var r=0;r<n;r++)
                    {
                        var proof=certificates[r];status=first.Bound(proof.First,bounds);if(status!=AlgorithmStatus.Success)return status;
                        var error=LocateNativeTripleChart(new(){Curve=edge.Curve,First=edge.Start,Last=edge.End},proof.First,proof.Second,bounds[..3],roots[r].Point,
                            out var parameter,out var interval);
                        if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;
                        if(error!=0)return AlgorithmStatus.NotConverged;
                        for(var q=0;q<count;q++)if(interval.Lower<=isolation[q].Root.Upper&&isolation[q].Root.Lower<=interval.Upper)
                            return AlgorithmStatus.NotConverged;
                        if(count==output.Length)return AlgorithmStatus.OutputTooSmall;
                        var spatial=(new IntervalVector(bounds[0],bounds[1],bounds[2])-new IntervalVector(roots[r].Point)).Norm().Value.Upper;
                        if(!double.IsFinite(spatial)||spatial>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NotConverged;
                        var at=count++;
                        while(at>0&&interval.Lower<isolation[at-1].Root.Lower)
                        {output[at]=output[at-1];isolation[at]=isolation[at-1];errors[at]=errors[at-1];commonUv[at]=commonUv[at-1];targetUv[at]=targetUv[at-1];at--;}
                        output[at]=new(){Point=roots[r].Point,First=parameter,Kind=IntersectionVectorKind.Simple};
                        isolation[at]=new(){Root=interval,Certified=true};errors[at]=spatial;
                        commonUv[at]=side==0?proof.First:proof.Second;targetUv[at]=proof.Third;
                    }
                }
            }
        }
        return AlgorithmStatus.Success;
    }

    private static int LocateNativeTripleChart(in SheetCutCurve cut,in SurfaceParameterBox first,in SurfaceParameterBox second,
        ReadOnlySpan<IntervalNumber> position,in KernelVector3 representative,out double parameter,out IntervalNumber interval)
    {
        parameter=0;interval=UncertifiedStation;var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
            var status=compiler.Spine(cut.Curve,out var spine);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var program=compiler.View;var definition=program.Spines[spine];
            if(program.Surfaces[definition.First].IsBlend||program.Surfaces[definition.Second].IsBlend)return ParasolidConstants.PK_ERROR_not_implemented;
            Span<IntervalNumber> witness=stackalloc IntervalNumber[7];position[..3].CopyTo(witness);var n=3;
            if(program.Surfaces[definition.First].IsBSpline){witness[n++]=new(first.U0,first.U1);witness[n++]=new(first.V0,first.V1);}
            if(program.Surfaces[definition.Second].IsBSpline){witness[n++]=new(second.U0,second.U1);witness[n++]=new(second.V0,second.V1);}
            return LocateCertifiedChart(cut,program,spine,witness[..n],out parameter,out interval,representative);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static int LocateNativeTripleCut(in SheetCutCurve cut,in RegularBoundaryRoot root,out double parameter,out IntervalNumber interval,out bool handled)
    {
        parameter=0;interval=UncertifiedStation;handled=false;var curve=Curves[GetCurveSlotByTag(cut.Curve)];
        if(curve.Class!=CurveClass.ICurve)return 0;var data=ICurveDataPool[curve.DataIndex];
        var same=SameSheetSupport(data.Surface0Tag,root.ContactSurface)&&SameSheetSupport(data.Surface1Tag,root.CuttingSurface);
        var swapped=SameSheetSupport(data.Surface1Tag,root.ContactSurface)&&SameSheetSupport(data.Surface0Tag,root.CuttingSurface);
        if(!same&&!swapped)return 0;handled=true;
        if(root.NativeBoundaryAxis!=0&&cut.Construction==CutConstruction.NativeSurfacePair)
        {
            var endError=MatchNativeSurfacePairEnd(cut,root,out var matched,out parameter);
            if(endError!=0)return endError;
            if(matched){interval=new(parameter);return 0;}
        }
        var radius=new IntervalNumber(-root.Root.SpatialError,root.Root.SpatialError);
        Span<IntervalNumber> position=stackalloc IntervalNumber[3]{root.Root.Point.X+radius,root.Root.Point.Y+radius,root.Root.Point.Z+radius};
        return LocateNativeTripleChart(cut,same?root.SplineDomain:root.CuttingDomain,same?root.CuttingDomain:root.SplineDomain,
            position,root.Root.Point,out parameter,out interval);
    }
}
