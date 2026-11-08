using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int JoinThreePlaneCutRoots(in RegularBoundaryView input,BufferOffset third,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset current,ref SheetArrangement model,out bool joined)
    {
        joined=false;
        var first=cuts[current].First;var second=cuts[current].Second;
        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[3];
        Span<FiniteCurveSurfaceRoot> roots=stackalloc FiniteCurveSurfaceRoot[3];
        if(!FindThreePlaneCutRoot(input,cuts,first,second,third,out anchors[0],out roots[0])
            ||anchors[0].SourceOccurrence!=RegularInputEdges+current
            ||!FindThreePlaneCutRoot(input,cuts,first,third,second,out anchors[1],out roots[1])
            ||!FindThreePlaneCutRoot(input,cuts,second,third,first,out anchors[2],out roots[2]))return 0;

        // Each carrier lies exactly on its two plane equations, with a
        // transverse root on the third. The independent first pair
        // therefore has exactly one common point with the third plane.
        // These are three gauges of that root, not three nearby XYZ hits.
        var error=model.AddPoint(roots[0].Point,-1,out var endpoint,anchors,requireProof:true);if(error!=0)return error;
        for(var i=0;i<3;i++)
        {
            ref readonly var anchor=ref anchors[i];
            error=model.AssociateParameter(endpoint,anchor.Carrier,anchor.Parameter,anchor.Lower,anchor.Upper,anchor.Surface,anchor.Witness);
            if(error!=0)return error;
            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.ThirdFaceStation,Curve=anchor.Carrier,Generation=anchor.Generation,
                StartEvent=endpoint,EndEvent=endpoint,OtherSurface=anchor.Surface,Witness=anchor.Witness,First=anchor.Lower,Last=anchor.Upper,
                SpatialError=roots[i].SpatialError});
        }
        joined=true;return 0;
    }

    private static bool FindThreePlaneCutRoot(in RegularBoundaryView input,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset first,BufferOffset second,BufferOffset third,out BoundaryParameter anchor,out FiniteCurveSurfaceRoot root)
    {
        anchor=default;root=default;var found=false;
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var cut=ref cuts[i];
            if(!(cut.First==first&&cut.Second==second||cut.First==second&&cut.Second==first))continue;
            if(!TryThreePlaneCutRoot(input,cut,third,out var candidate,out var crosses))return false;
            if(!crosses)continue;
            if(found)return false;
            root=candidate;anchor=ThreePlaneCutAnchor(input,cut,third,i,candidate);found=true;
        }
        return found;
    }

    private static bool TryThreePlaneCutRoot(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third,
        out FiniteCurveSurfaceRoot root,out bool crosses)
    {
        root=default;crosses=false;var curve=Curves[GetCurveSlotByTag(pair.Cut.Curve)];
        if(curve.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(curve,out var line))return false;
        var a=Surfaces[GetSurfaceSlotByTag(input.Faces[pair.First].Surface)];
        var b=Surfaces[GetSurfaceSlotByTag(input.Faces[pair.Second].Surface)];
        if(!TryPrepareSurface(a,out var first)||!TryPrepareSurface(b,out var second)||first.Kind!=SurfaceClass.Plane||second.Kind!=SurfaceClass.Plane
            ||IntervalVector.Cross(new(first.Axis),new(second.Axis)).Norm().Value.Lower<=0
            ||!LineExactlyOnPlane(line,first)||!LineExactlyOnPlane(line,second))return false;
        var edge=new ModelingEdge{Curve=pair.Cut.Curve,Start=pair.Cut.First,End=pair.Cut.Last};
        if(!TryFiniteLinePlaneEvent(edge,-1,input.Faces[third].Surface,out var result))return false;
        crosses=result.Count==1;root=result.FirstRoot;return true;
    }

    private static bool LineExactlyOnPlane(in ParametricSpineCurve line,in AnalyticSurface plane)
    {
        var normal=new IntervalVector(plane.Axis);
        var offset=IntervalVector.Dot(new IntervalVector(line.Origin)-new IntervalVector(plane.Origin),normal).Value;
        var tangent=IntervalVector.Dot(new IntervalVector(line.First),normal).Value;
        return offset.Lower==0&&offset.Upper==0&&tangent.Lower==0&&tangent.Upper==0;
    }

    private static BoundaryParameter ThreePlaneCutAnchor(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third,
        BufferOffset cutIndex,in FiniteCurveSurfaceRoot root)
        =>new(){Carrier=pair.Cut.Curve,Generation=Curves[GetCurveSlotByTag(pair.Cut.Curve)].Header.Generation,Surface=input.Faces[third].Surface,
            Witness=1+(RegularInputEdges+cutIndex)*IntersectionPointCapacity,Parameter=root.Parameter,Lower=root.Lower,Upper=root.Upper};

#if KERNEL_TEST_HOOKS
    internal static int TestThreePlaneCutEvents(ReadOnlySpan<SurfTag> surfaces,ReadOnlySpan<CurveTag> carriers,bool truncate,
        Span<double> stations,out BufferCount events)
    {
        Span<ModelingFace> faces=stackalloc ModelingFace[3];
        for(var i=0;i<3;i++)faces[i]=new(){Surface=surfaces[i]};
        Span<RegularFaceCut> cuts=stackalloc RegularFaceCut[3];
        cuts[0]=new(){First=0,Second=1,Cut=new(){Curve=carriers[0],First=0,Last=1}};
        cuts[1]=new(){First=0,Second=2,Cut=new(){Curve=carriers[1],First=0,Last=1}};
        cuts[2]=new(){First=1,Second=2,Cut=new(){Curve=carriers[2],First=truncate?.7:0,Last=1}};
        var model=new SheetArrangement{Points=stackalloc KernelVector3[3],OriginalVertices=stackalloc VertexSlot[3],
            Parameters=stackalloc BoundaryParameter[3],Tolerance=1e-9};
        var input=new RegularBoundaryView(default,default,default,faces);
        for(var i=0;i<3;i++)
        {
            var third=3-cuts[i].First-cuts[i].Second;
            var error=ReadThirdFaceEvents(input,third,-1,cuts,i,ref model,requireProof:true);
            if(error!=0){events=model.NP;return error;}
        }
        events=model.NP;
        for(var i=0;i<3;i++)stations[i]=model.KnownParameter(0,carriers[i],out var t)?t:double.NaN;
        return 0;
    }
#endif
}
