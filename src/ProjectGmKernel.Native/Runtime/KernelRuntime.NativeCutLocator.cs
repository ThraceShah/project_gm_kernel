using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A certified source ICurve/plane root is on this cut's two supports
    // when the source curve supplies the identical other support. Project
    // its entire spatial box into every finite native CHART gauge. Only a
    // single connected chart occurrence may supply the cut parameter.
    private static int LocateCertifiedNativeCut(in SheetCutCurve cut,in RegularBoundaryRoot root,out double parameter,out IntervalNumber isolation,out bool handled)
    {
        parameter=0;isolation=new(double.NaN,double.NaN);handled=false;
        if(root.Certified==0||root.Root.Vertex>=0||!(root.Root.SpatialError>0)||!double.IsFinite(root.Root.SpatialError))return 0;
        var source=Curves[GetCurveSlotByTag(root.Curve)];var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(source.Class!=CurveClass.ICurve||target.Class!=CurveClass.ICurve
            ||Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)].Class!=SurfaceClass.Plane)return 0;
        var from=ICurveDataPool[source.DataIndex];var to=ICurveDataPool[target.DataIndex];
        if(from.ProgramBlock<=0||to.ProgramBlock<=0||from.IsBlendContact!=0||to.IsBlendContact!=0
            ||!(SameSheetSupport(from.Surface0Tag,root.ContactSurface)||SameSheetSupport(from.Surface1Tag,root.ContactSurface))
            ||!(SameSheetSupport(to.Surface0Tag,root.ContactSurface)&&SameSheetSupport(to.Surface1Tag,root.CuttingSurface)
                ||SameSheetSupport(to.Surface1Tag,root.ContactSurface)&&SameSheetSupport(to.Surface0Tag,root.CuttingSurface)))return 0;
        var sourceProgram=GetNestedBlendProgram(from.ProgramBlock,from.ProgramSurfaceCount,from.ProgramSpineCount,from.ProgramChartCount,from.ProgramScalarCount);
        var program=GetNestedBlendProgram(to.ProgramBlock,to.ProgramSurfaceCount,to.ProgramSpineCount,to.ProgramChartCount,to.ProgramScalarCount);
        var sourceDefinition=sourceProgram.Spines[from.ProgramSpine];var definition=program.Spines[to.ProgramSpine];
        if(SpineParameterMap.Explicit(sourceDefinition)||SpineParameterMap.Explicit(definition))return 0;
        var sourceFirst=SameSheetSupport(from.Surface0Tag,root.ContactSurface);var targetFirst=SameSheetSupport(to.Surface0Tag,root.ContactSurface);
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
        if(JointNestedBlendEvaluation.BoundSharedSpineWitness(sourceProgram,from.ProgramSpine,new(root.Root.Lower,root.Root.Upper,0,0),bounds,capture:witness)!=AlgorithmStatus.Success)
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
            if(!enclosure.Finite||JointNestedBlendEvaluation.BoundSpine(program,to.ProgramSpine,new(lo,hi,0,0),bounds)!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(Separated(bounds[0],position.X.Value)||Separated(bounds[1],position.Y.Value)||Separated(bounds[2],position.Z.Value))continue;
            // The shared support, cutting plane and projected CHART
            // equation are satisfied by the source root. It must lie in
            // the target's contraction box, including the nearest station
            // of a shared explicit rolling spine. Overlap alone is not a
            // correspondence; this proves uniqueness in the same branch.
            if(found||projected.Lower<first||projected.Upper>last
                ||JointNestedBlendEvaluation.BoundSharedSpineWitness(program,to.ProgramSpine,new(lo,hi,0,0),bounds,require:witness)!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            native=enclosure;found=true;
        }
        if(covered!=cut.Last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!found)return ParasolidConstants.PK_ERROR_not_on_curve;
        parameter=native.Midpoint;isolation=native;
        return 0;

        static bool Separated(IntervalNumber a,IntervalNumber b)=>a.Upper<b.Lower||b.Upper<a.Lower;
    }
}
