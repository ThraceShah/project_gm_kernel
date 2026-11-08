using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int LocateCertifiedCut(in SheetCutCurve cut,in RegularBoundaryRoot root,out double parameter,out IntervalNumber isolation,out bool handled)
    {
        parameter=0;isolation=new(double.NaN,double.NaN);handled=false;
        if(root.Certified==ExplicitPlaneCertificate)
            return LocateCertifiedLineCut(cut.Curve,root,ref parameter,ref isolation,out handled);
        // Project the entire source ICurve root box into the target's
        // finite CHART cells on the same two supports. Only one connected
        // chart occurrence may supply the station.
        if(root.Certified!=NativePlaneCertificate||root.Root.Vertex>=0||!(root.Root.SpatialError>0)||!double.IsFinite(root.Root.SpatialError))return 0;
        var source=Curves[GetCurveSlotByTag(root.Curve)];var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(source.Class!=CurveClass.ICurve||target.Class!=CurveClass.ICurve
            ||Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)].Class!=SurfaceClass.Plane)return 0;
        var from=ICurveDataPool[source.DataIndex];var to=ICurveDataPool[target.DataIndex];
        if(from.ProgramBlock<=0||to.ProgramBlock<=0||from.IsBlendContact!=0||to.IsBlendContact!=0
            ||!(SameSheetSupport(from.Surface0Tag,root.ContactSurface)||SameSheetSupport(from.Surface1Tag,root.ContactSurface))
            ||!(SameSheetSupport(to.Surface0Tag,root.ContactSurface)&&SameSheetSupport(to.Surface1Tag,root.CuttingSurface)
                ||SameSheetSupport(to.Surface1Tag,root.ContactSurface)&&SameSheetSupport(to.Surface0Tag,root.CuttingSurface)))return 0;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            // Rebuild the exact occurrence used by IntersectFiniteIntersectionCurvePlane.
            // It keeps the stored root CHART but reads the current support
            // definitions. A transformed cached spine can round differently.
            var compiler=new NestedProgramCompiler();
            if(!compiler.Ready){handled=true;return ParasolidConstants.PK_ERROR_memory_full;}
            var status=compiler.Spine(root.Curve,out var sourceSpine);
            if(status!=AlgorithmStatus.Success){handled=true;return IntersectionError(status);}
            var program=GetNestedBlendProgram(to.ProgramBlock,to.ProgramSurfaceCount,to.ProgramSpineCount,to.ProgramChartCount,to.ProgramScalarCount);
            return LocateInCertifiedCutChart(cut,root,compiler.View,sourceSpine,SameSheetSupport(from.Surface0Tag,root.ContactSurface),
                program,to.ProgramSpine,SameSheetSupport(to.Surface0Tag,root.ContactSurface),out parameter,out isolation,out handled);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static int LocateCertifiedLineCut(CurveTag cut,in RegularBoundaryRoot root,ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;
        var target=Curves[GetCurveSlotByTag(cut)];
        if(target.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(target,out var line)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.ContactSurface)],out var common)||common.Kind!=SurfaceClass.Plane
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)],out var cutting)||cutting.Kind!=SurfaceClass.Plane
            ||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(root.Curve)],out var source)
            ||!ExplicitCurveExactlyOnPlane(source,common)||!ExplicitCurveExactlyOnPlane(line,common)
            ||!ExplicitCurveExactlyOnPlane(line,cutting))return 0;
        // The source is transverse to the cutting plane and contained in
        // the common plane. Their intersection is therefore precisely the
        // target line. Transport the whole source native root enclosure.
        handled=true;
        var axis=new IntervalVector(line.First);
        var offset=IntervalVector.Dot(new IntervalVector(source.Origin)-new IntervalVector(line.Origin),axis).Value;
        var u=IntervalVector.Dot(new IntervalVector(source.First),axis).Value;
        var station=new IntervalNumber(root.Root.Lower,root.Root.Upper);
        if(source.Kind==CurveClass.Line)offset+=u*station;
        else
        {
            IntervalDual.SinCos(new(station,0),out var sine,out var cosine);
            offset+=u*cosine.Value+IntervalVector.Dot(new IntervalVector(source.Second),axis).Value*sine.Value;
        }
        isolation=offset/IntervalVector.Dot(axis,axis).Value;
        if(!isolation.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        parameter=isolation.Midpoint;
        return 0;
    }

    private static int LocateInCertifiedCutChart(in SheetCutCurve cut,in RegularBoundaryRoot root,
        in NestedBlendProgram sourceProgram,BlendSpineIndex sourceSpine,bool sourceFirst,in NestedBlendProgram program,BlendSpineIndex cutSpine,bool targetFirst,
        out double parameter,out IntervalNumber isolation,out bool handled)
    {
        parameter=0;isolation=new(double.NaN,double.NaN);handled=false;
        var sourceDefinition=sourceProgram.Spines[sourceSpine];var definition=program.Spines[cutSpine];
        if(SpineParameterMap.Explicit(sourceDefinition)||SpineParameterMap.Explicit(definition))return 0;
        var sourceSupport=sourceProgram.Surfaces[sourceFirst?sourceDefinition.First:sourceDefinition.Second];
        var targetSupport=program.Surfaces[targetFirst?definition.First:definition.Second];
        var sourceOther=sourceProgram.Surfaces[sourceFirst?sourceDefinition.Second:sourceDefinition.First];
        var targetOther=program.Surfaces[targetFirst?definition.Second:definition.First];
        if(sourceOther.IsBSpline||sourceOther.IsBlend||targetOther.IsBSpline||targetOther.IsBlend||sourceSupport.IsBSpline||targetSupport.IsBSpline
            ||sourceSupport.IsBlend!=targetSupport.IsBlend)return 0;
        if(sourceSupport.IsBlend)
        {
            var a=sourceProgram.Spines[sourceSupport.Spine];var b=program.Spines[targetSupport.Spine];
            if(!SpineParameterMap.Explicit(a)||!SpineParameterMap.Explicit(b)||a.Curve.Kind!=b.Curve.Kind
                ||!ExactVector(a.Curve.Origin,b.Curve.Origin)||!ExactVector(a.Curve.First,b.Curve.First)||!ExactVector(a.Curve.Second,b.Curve.Second)
                ||Math.Abs(sourceSupport.Range0)!=Math.Abs(targetSupport.Range0))return 0;
        }
        handled=true;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Span<IntervalNumber> witness=stackalloc IntervalNumber[sourceSupport.IsBlend?7:3];
        if(JointNestedBlendEvaluation.BoundSharedSpineWitness(sourceProgram,sourceSpine,new(root.Root.Lower,root.Root.Upper,0,0),bounds,capture:witness)!=AlgorithmStatus.Success)
            return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var position=new IntervalVector(witness[0],witness[1],witness[2]);
        var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        var found=false;var native=default(IntervalNumber);var covered=cut.First;
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            ref readonly var frame=ref charts[chart];
            var first=Math.Max(cut.First,frame.Parameter);var last=Math.Min(cut.Last,charts[chart+1].Parameter);
            if(!(last>first))continue;
            if(first>covered)return ParasolidConstants.PK_ERROR_cant_do_intersect;covered=last;
            var projected=frame.Parameter+frame.Scale*IntervalVector.Dot(new IntervalVector(frame.Chord),position-new IntervalVector(frame.Position)).Value;
            var lo=Math.Max(first,projected.Lower);var hi=Math.Min(last,projected.Upper);if(hi<lo)continue;
            var enclosure=new IntervalNumber(lo,hi);
            if(!enclosure.Finite||JointNestedBlendEvaluation.BoundSpine(program,cutSpine,new(lo,hi,0,0),bounds)!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(Separated(bounds[0],position.X.Value)||Separated(bounds[1],position.Y.Value)||Separated(bounds[2],position.Z.Value))continue;
            // The shared support, cutting plane and projected CHART
            // equation are satisfied by the source root. It must lie in
            // the target's contraction box, including the nearest station
            // of a shared explicit rolling spine. Overlap alone is not a
            // correspondence; this proves uniqueness in the same branch.
            if(found||projected.Lower<first||projected.Upper>last
                ||JointNestedBlendEvaluation.BoundSharedSpineWitness(program,cutSpine,new(lo,hi,0,0),bounds,require:witness)!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            native=enclosure;found=true;
        }
        if(covered!=cut.Last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!found)return ParasolidConstants.PK_ERROR_not_on_curve;
        parameter=native.Midpoint;isolation=native;
        return 0;

        static bool Separated(IntervalNumber a,IntervalNumber b)=>a.Upper<b.Lower||b.Upper<a.Lower;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestLocateCertifiedLineCut(CurveTag source,CurveTag target,SurfTag support,SurfTag plane,double first,double last,
        out IntervalNumber bound,out bool certified,out bool inside)
    {
        bound=UncertifiedStation;certified=inside=false;
        var edge=new ModelingEdge{Curve=source,Start=0,End=1};
        if(!TryFiniteLinePlaneEvent(edge,0,plane,out var proof)||proof.Count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var root=new RegularBoundaryRoot{Curve=source,ContactSurface=support,CuttingSurface=plane,Certified=ExplicitPlaneCertificate,Witness=1,Root=proof.FirstRoot};
        var cut=new SheetCutCurve{Curve=target,First=first,Last=last};
        var error=LocateCertifiedCut(cut,root,out var station,out var projected,out certified);
        if(error!=0||!certified)return error;
        var anchor=new BoundaryParameter{Parameter=station,Lower=projected.Lower,Upper=projected.Upper};
        return LocateBoundedStation(target,0,anchor,first,last,-1,-1,out _,out bound,out inside);
    }
#endif
}
