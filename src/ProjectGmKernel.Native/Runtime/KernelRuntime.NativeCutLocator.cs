using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int LocateCertifiedCut(in SheetCutCurve cut,in RegularBoundaryRoot root,out double parameter,out IntervalNumber isolation,out bool handled)
    {
        parameter=0;isolation=new(double.NaN,double.NaN);handled=false;
        if(root.Certified==NativeTripleCertificate||root.NativeBoundaryAxis!=0&&cut.Construction==CutConstruction.NativeSurfacePair)
            return LocateNativeTripleCut(cut,root,out parameter,out isolation,out handled);
        if(root.Certified==ExplicitPlaneCertificate)
            return LocateCertifiedLineCut(cut,root,ref parameter,ref isolation,out handled);
        if(root.Certified==PlanePairBlendLineCertificate)
            return LocateCertifiedBladeCut(cut,root,ref parameter,ref isolation,out handled);
        if(root.Certified==PlanePairSplineLineCertificate)
            return LocateCertifiedSplineCut(cut,root,ref parameter,ref isolation,out handled);
        if(root.Certified==PlanePairCylinderLineCertificate
            ||root.Certified==NativePlaneCertificate&&cut.Construction==CutConstruction.AnalyticCylinderPlaneConic)
            return LocateCertifiedCylinderConic(cut,root,ref parameter,ref isolation,out handled);
        if(root.Certified!=NativePlaneCertificate||root.Root.Vertex>=0||!(root.Root.SpatialError>0)||!double.IsFinite(root.Root.SpatialError))return 0;
        var source=Curves[GetCurveSlotByTag(root.Curve)];var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(source.Class!=CurveClass.ICurve||(target.Class!=CurveClass.ICurve&&target.Class!=CurveClass.Line)
            ||Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)].Class!=SurfaceClass.Plane)return 0;
        var from=ICurveDataPool[source.DataIndex];
        if(from.ProgramBlock<=0||from.IsBlendContact!=0)return 0;
        var straight=target.Class==CurveClass.Line;
        var line=default(ParametricSpineCurve);
        var to=straight?default:ICurveDataPool[target.DataIndex];
        var sourceFirst=SameSheetSupport(from.Surface0Tag,root.ContactSurface);
        if(straight)
        {
            if(!TryReadExplicitSpineCurve(target,out line)
                ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.ContactSurface)],out var common)||common.Kind!=SurfaceClass.Plane
                ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)],out var cutting)
                ||!(TryCertifySupportChart(from.Surface0Tag,root.ContactSurface,out _)||TryCertifySupportChart(from.Surface1Tag,root.ContactSurface,out _))
                ||!PlanePairLineCertified(cut,line,common,cutting))return 0;
        }
        else if(to.ProgramBlock<=0||to.IsBlendContact!=0
            ||!(sourceFirst||SameSheetSupport(from.Surface1Tag,root.ContactSurface))
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
            if(straight)
            {
                // Both exact plane equations hold at the certified root.
                // They select one analytic line; bound its station in the
                // stored line's gauge using the entire source CHART box.
                handled=true;
                Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
                status=JointNestedBlendEvaluation.BoundSpine(compiler.View,sourceSpine,new(root.Root.Lower,root.Root.Upper,0,0),bounds);
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                var offset=new IntervalVector(bounds[0],bounds[1],bounds[2])-new IntervalVector(line.Origin);
                var axis=new IntervalVector(line.First);
                isolation=IntervalVector.Dot(offset,axis).Value/IntervalVector.Dot(axis,axis).Value;
                if(!isolation.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                // The forward point of the isolated source root is the
                // best available representative. The box decides the
                // occurrence and order, never the projected point.
                parameter=Math.Clamp(Dot(Sub(root.Root.Point,line.Origin),line.First),isolation.Lower,isolation.Upper);
                return 0;
            }
            var program=GetNestedBlendProgram(to.ProgramBlock,to.ProgramSurfaceCount,to.ProgramSpineCount,to.ProgramChartCount,to.ProgramScalarCount);
            return LocateInCertifiedCutChart(cut,root,compiler.View,sourceSpine,sourceFirst,
                program,to.ProgramSpine,SameSheetSupport(to.Surface0Tag,root.ContactSurface),out parameter,out isolation,out handled);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static int LocateCertifiedBladeCut(in SheetCutCurve cut,in RegularBoundaryRoot root,
        ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;
        var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(target.Class!=CurveClass.ICurve||!root.BlendStation.Finite)return 0;
        return LocateCertifiedBladeCut(cut,root,ICurveDataPool[target.DataIndex],ref parameter,ref isolation,out handled);
    }

    private static int LocateCertifiedSplineCut(in SheetCutCurve cut,in RegularBoundaryRoot root,
        ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(target.Class!=CurveClass.ICurve||!root.SplineDomain.Valid||!double.IsFinite(root.Root.SpatialError))return 0;
        var data=ICurveDataPool[target.DataIndex];
        var shared=TryCertifySupportChart(data.Surface0Tag,root.ContactSurface,out _)
            &&SameSheetSupport(data.Surface1Tag,root.CuttingSurface)
            ||TryCertifySupportChart(data.Surface1Tag,root.ContactSurface,out _)
            &&SameSheetSupport(data.Surface0Tag,root.CuttingSurface);
        if(!shared)return 0;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready){handled=true;return ParasolidConstants.PK_ERROR_memory_full;}
            var status=compiler.Spine(cut.Curve,out var spine);if(status!=AlgorithmStatus.Success){handled=true;return IntersectionError(status);}
            var program=compiler.View;var definition=program.Spines[spine];
            var first=program.Surfaces[definition.First];var second=program.Surfaces[definition.Second];
            if(first.IsBlend||second.IsBlend||first.IsBSpline==second.IsBSpline)return 0;
            var analytic=first.IsBSpline?second:first;if(analytic.Analytic.Kind!=SurfaceClass.Plane)return 0;
            handled=true;var radius=new IntervalNumber(-root.Root.SpatialError,root.Root.SpatialError);
            Span<IntervalNumber> witness=stackalloc IntervalNumber[5]
            {
                new IntervalNumber(root.Root.Point.X)+radius,new IntervalNumber(root.Root.Point.Y)+radius,new IntervalNumber(root.Root.Point.Z)+radius,
                new(root.SplineDomain.U0,root.SplineDomain.U1),new(root.SplineDomain.V0,root.SplineDomain.V1)
            };
            return LocateCertifiedChart(cut,program,spine,witness,out parameter,out isolation,root.Root.Point);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static int LocateCertifiedBladeCut(in SheetCutCurve cut,in RegularBoundaryRoot root,in ICurveData data,
        ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;
        if(data.ProgramBlock<=0||data.IsBlendContact!=0)return 0;
        var planeFirst=SameSheetSupport(data.Surface0Tag,root.CuttingSurface)&&TryCertifySupportChart(data.Surface1Tag,root.ContactSurface,out _);
        if(!planeFirst&&!(SameSheetSupport(data.Surface1Tag,root.CuttingSurface)&&TryCertifySupportChart(data.Surface0Tag,root.ContactSurface,out _)))return 0;
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var definition=program.Spines[data.ProgramSpine];
        // The compiled supports need not have the same order as the persisted ICurve pair.
        var first=program.Surfaces[definition.First];var second=program.Surfaces[definition.Second];
        var blend=first.IsBlend?first:second;var plane=first.IsBlend?second:first;
        if(SpineParameterMap.Explicit(definition)||!blend.IsBlend||plane.IsBlend||plane.IsBSpline||plane.Analytic.Kind!=SurfaceClass.Plane)return 0;
        var spine=program.Spines[blend.Spine];
        if(!SpineParameterMap.Explicit(spine))return 0;
        handled=true;
        var status=SpineParameterMap.Interval(spine.Curve,new(root.BlendStation,0),out var center,out _);
        if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        var point=root.Root.Point;var error=root.Root.SpatialError;var radius=new IntervalNumber(-error,error);
        var x=point.X+radius;var y=point.Y+radius;var z=point.Z+radius;
        if(root.ExactPositionAxes!=0&&TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(root.Curve)],out var line))
        {
            if((root.ExactPositionAxes&1)!=0)x=line.Origin.X;
            if((root.ExactPositionAxes&2)!=0)y=line.Origin.Y;
            if((root.ExactPositionAxes&4)!=0)z=line.Origin.Z;
        }
        Span<IntervalNumber> witness=stackalloc IntervalNumber[7]{x,y,z,
            center.X.Value,center.Y.Value,center.Z.Value,root.BlendStation};
        return LocateCertifiedChart(cut,program,data.ProgramSpine,witness,out parameter,out isolation,point);
    }

    private static byte ExactCutLineCoordinateMask(CurveTag carrier,ReadOnlySpan<IntervalNumber> poles)
    {
        if(poles.Length!=8||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(carrier)],out var line)||line.Kind!=CurveClass.Line)return 0;
        byte axes=0;
        for(var i=0;i<3;i++)
        {
            var coordinate=i==0?line.Origin.X:i==1?line.Origin.Y:line.Origin.Z;
            // Both homogeneous endpoints must satisfy the same exact
            // coordinate equation, including the correction of the line.
            if((poles[i]-coordinate*poles[3]).Magnitude==0&&(poles[i+4]-coordinate*poles[7]).Magnitude==0)
                axes|=(byte)(1<<i);
        }
        return axes;
    }

    private static int LocateCertifiedLineCut(in SheetCutCurve cut,in RegularBoundaryRoot root,ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;
        var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(target.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(target,out var line)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.ContactSurface)],out var common)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)],out var cutting)||cutting.Kind!=SurfaceClass.Plane
            ||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(root.Curve)],out var source))return 0;
        var station=new IntervalNumber(root.Root.Lower,root.Root.Upper);
        if(common.Kind==SurfaceClass.Cylinder)
        {
            if(!ExplicitCurveExactlyOnCylinder(source,common)||!TryCertifiedGeneratorSide(cut,line,common,cutting,out var side,out var positive))return 0;
            handled=true;
            var sourceSide=ProjectExplicitRoot(source,common.Origin,side,station);
            if(!sourceSide.Finite||sourceSide.ContainsZero)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if((sourceSide.Lower>0)!=positive)return ParasolidConstants.PK_ERROR_not_on_curve;
        }
        else
        {
            if(common.Kind!=SurfaceClass.Plane||!ExplicitCurveExactlyOnPlane(source,common))return 0;
            if(!PlanePairLineCertified(cut,line,common,cutting))return 0;
        }
        // Use the represented carrier's gauge for the selected true
        // branch; a rounded representation must have been bounded above.
        handled=true;
        var axis=new IntervalVector(line.First);
        isolation=ProjectExplicitRoot(source,line.Origin,axis,station)/IntervalVector.Dot(axis,axis).Value;
        if(!isolation.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        parameter=isolation.Midpoint;
        return 0;
    }

    private static bool PlanePairLineCertified(in SheetCutCurve cut,in ParametricSpineCurve line,in AnalyticSurface first,in AnalyticSurface second)
    {
        if(ExplicitCurveExactlyOnPlane(line,first)&&ExplicitCurveExactlyOnPlane(line,second))
        {
            var cross=IntervalVector.Cross(new IntervalVector(first.Axis),new IntervalVector(second.Axis));
            return IntervalVector.Dot(cross,cross).Value.Lower>0;
        }
        return cut.Construction==CutConstruction.AnalyticPlanePair&&BoundRoundedPlaneLine(cut,line,first,second);
    }

    private static bool BoundRoundedPlaneLine(in SheetCutCurve cut,in ParametricSpineCurve line,in AnalyticSurface first,in AnalyticSurface second,
        Span<IntervalNumber> poles=default)
    {
        var domain=new IntervalNumber(cut.First,cut.Last);
        if(!domain.Finite)return false;
        var a=new IntervalVector(first.Axis);var b=new IntervalVector(second.Axis);
        var q=IntervalVector.Cross(a,b);var qq=IntervalVector.Dot(q,q).Value;
        var origin=new IntervalVector(line.Origin);var direction=new IntervalVector(line.First);
        var alignment=IntervalVector.Dot(q,direction).Value;
        if(!(qq.Lower>0)||!qq.Finite||!alignment.Finite||alignment.ContainsZero)return false;
        var offsetA=IntervalVector.Dot(a,new IntervalVector(first.Origin)-origin).Value;var slopeA=IntervalVector.Dot(a,direction).Value;
        var offsetB=IntervalVector.Dot(b,new IntervalVector(second.Origin)-origin).Value;var slopeB=IntervalVector.Dot(b,direction).Value;
        // Minimum transverse correction from the represented point to the
        // unique analytic line. Keep the gauge by adding the component
        // along q that makes the correction orthogonal to the stored axis.
        var error=Correction(domain).Norm().Value;
        var scale=origin.Norm().Value+new IntervalVector(first.Origin).Norm().Value+new IntervalVector(second.Origin).Norm().Value
            +direction.Norm().Value*domain.Magnitude;
        var roundoff=(64*SmallLinearSolve.MachineEpsilon*scale).Upper;
        if(!error.Finite||error.Upper>Math.Min(roundoff,OperationIntersection.DistanceTolerance/16))return false;
        if(!poles.IsEmpty)
        {
            // An affine span, not a fit: these are the two exact endpoints
            // of the corrected line in the original carrier gauge.
            for(var end=0;end<2;end++)
            {
                IntervalNumber station=end==0?cut.First:cut.Last;
                var point=origin+direction*new IntervalDual(station,0)+Correction(station);
                if(!point.Finite)return false;
                poles[4*end]=point.X.Value;poles[4*end+1]=point.Y.Value;poles[4*end+2]=point.Z.Value;poles[4*end+3]=1;
            }
        }
        return true;

        IntervalVector Correction(IntervalNumber station)
        {
            var ra=offsetA-slopeA*station;var rb=offsetB-slopeB*station;
            var transverse=(IntervalVector.Cross(b,q)*new IntervalDual(ra,0)+IntervalVector.Cross(q,a)*new IntervalDual(rb,0))/new IntervalDual(qq,0);
            return transverse-q*(IntervalVector.Dot(transverse,direction)/new IntervalDual(alignment,0));
        }
    }

    private static bool TryCertifiedGeneratorSide(in SheetCutCurve cut,in ParametricSpineCurve line,in AnalyticSurface cylinder,in AnalyticSurface plane,
        out IntervalVector side,out bool positive)
    {
        side=default;positive=false;
        var axis=new IntervalVector(cylinder.Axis);var normal=new IntervalVector(plane.Axis);
        var tilt=IntervalVector.Dot(axis,normal).Value;
        var direction=new IntervalVector(line.First);var cross=IntervalVector.Cross(axis,direction);
        var tilted=tilt.Magnitude!=0;
        if(!tilt.Finite||tilted&&cut.Construction!=CutConstruction.FiniteCylinderPlane
            ||IntervalVector.Dot(cross,cross).Value.Magnitude!=0)return false;
        side=IntervalVector.Cross(axis,normal);
        var origin=new IntervalVector(cylinder.Origin);var planeOrigin=new IntervalVector(plane.Origin);
        var offset=new IntervalVector(line.Origin)-origin;var axial=IntervalVector.Dot(offset,axis);
        var h=IntervalVector.Dot(planeOrigin-origin,normal).Value;
        var height=axial.Value;
        if(tilted)
        {
            // The published generator uses the cylinder's axial gauge.
            // Reconstruct its true radial branch at every represented
            // station in the finite window, not just at the anchor.
            var domain=new IntervalNumber(cut.First,cut.Last);
            if(!domain.Finite)return false;
            height+=IntervalVector.Dot(axis,direction).Value*domain;
            if(!height.Finite)return false;
            h-=tilt*height;
            normal-=axis*new IntervalDual(tilt,0);
        }
        var nn=IntervalVector.Dot(normal,normal).Value;
        if(!(nn.Lower>0))return false;
        var radius=new IntervalNumber(cylinder.Radius);
        // With a unit normal, factor the radial discriminant to retain
        // its small positive gap near tangency without subtracting squares.
        var gap=nn.Lower==1&&nn.Upper==1?(radius-h)*(radius+h):(radius*radius-h*h/nn)/nn;
        if(!gap.Finite||!(gap.Lower>0))return false;
        var branchSign=IntervalVector.Dot(offset,side).Value;
        if(!branchSign.Finite||branchSign.ContainsZero)return false;
        positive=branchSign.Lower>0;
        // The exact radial solution is (h/nn)*n_perp ± sqrt(gap)*(a × n).
        var radial=offset-axis*axial;
        var branch=normal*new IntervalDual(h/nn,0)+side*(IntervalDual.Sqrt(new(gap,0))*(positive?1:-1));
        if(!branch.Finite)return false;
        var error=(radial-branch).Norm().Value;
        if(!error.Finite||!(error.Upper<OperationIntersection.DistanceTolerance/16))return false;
        if(!tilted)return Covers(branch.X.Value,radial.X.Value)&&Covers(branch.Y.Value,radial.Y.Value)&&Covers(branch.Z.Value,radial.Z.Value);
        var roundoff=(64*SmallLinearSolve.MachineEpsilon*(origin.Norm().Value+planeOrigin.Norm().Value+height.Magnitude+radius)).Upper;
        return error.Upper<roundoff;

        static bool Covers(IntervalNumber outer,IntervalNumber inner)=>inner.Lower>=outer.Lower&&inner.Upper<=outer.Upper;
    }

    private static IntervalNumber ProjectExplicitRoot(in ParametricSpineCurve source,in KernelVector3 origin,IntervalVector axis,IntervalNumber station)
    {
        var offset=IntervalVector.Dot(new IntervalVector(source.Origin)-new IntervalVector(origin),axis).Value;
        var u=IntervalVector.Dot(new IntervalVector(source.First),axis).Value;
        if(source.Kind==CurveClass.Line)return offset+u*station;
        IntervalDual.SinCos(new(station,0),out var sine,out var cosine);
        return offset+u*cosine.Value+IntervalVector.Dot(new IntervalVector(source.Second),axis).Value*sine.Value;
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
        return LocateCertifiedChart(cut,program,cutSpine,witness,out parameter,out isolation);
    }

    private static int LocateCertifiedChart(in SheetCutCurve cut,in NestedBlendProgram program,BlendSpineIndex cutSpine,
        ReadOnlySpan<IntervalNumber> witness,out double parameter,out IntervalNumber isolation,KernelVector3? representative=null)
    {
        parameter=0;isolation=UncertifiedStation;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var definition=program.Spines[cutSpine];
        var position=new IntervalVector(witness[0],witness[1],witness[2]);
        var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        BufferOffset foundChart=-1;var paired=false;var native=default(IntervalNumber);var covered=cut.First;var station=0.0;
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
            if(projected.Lower<first||projected.Upper>last
                ||JointNestedBlendEvaluation.BoundSharedSpineWitness(program,cutSpine,new(lo,hi,0,0),bounds,require:witness)!=AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(foundChart>=0)
            {
                // Adjacent affine gauges can assign different native t to
                // one position on the same explicit rolling branch. Both
                // contractions must contain the entire position/center/U
                // witness and stay regularly oriented up to the shared knot.
                if(paired||witness.Length!=7||chart!=foundChart+1
                    ||!SameRollingChartContinuation(program,cutSpine,native.Lower,frame.Parameter,enclosure.Upper,witness,bounds))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                paired=true;continue;
            }
            native=enclosure;foundChart=chart;
            station=representative.HasValue?Math.Clamp(frame.Parameter+frame.Scale*Dot(frame.Chord,Sub(representative.Value,frame.Position)),lo,hi)
                :enclosure.Midpoint;
        }
        if(covered!=cut.Last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(foundChart<0)return ParasolidConstants.PK_ERROR_not_on_curve;
        parameter=station;isolation=native;
        return 0;

        static bool Separated(IntervalNumber a,IntervalNumber b)=>a.Upper<b.Lower||b.Upper<a.Lower;
    }

    private static bool SameRollingChartContinuation(in NestedBlendProgram program,BlendSpineIndex spine,
        double first,double knot,double last,ReadOnlySpan<IntervalNumber> witness,Span<IntervalNumber> bounds)
    {
        if(!(first<knot&&knot<last)
            ||JointNestedBlendEvaluation.BoundSharedSpineWitness(program,spine,new(first,knot,0,0),bounds,require:witness)!=AlgorithmStatus.Success)
            return false;
        var left=new IntervalVector(bounds[3],bounds[4],bounds[5]);
        if(JointNestedBlendEvaluation.BoundSharedSpineWitness(program,spine,new(knot,last,0,0),bounds,require:witness)!=AlgorithmStatus.Success)
            return false;
        var right=new IntervalVector(bounds[3],bounds[4],bounds[5]);
        return IntervalVector.Dot(left,right).Value.Lower>0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestLocateSharedChart(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last,
        ReadOnlySpan<IntervalNumber> witness,out IntervalNumber station)
        =>LocateCertifiedChart(new(){First=first,Last=last},program,spine,witness,out _,out station);

    internal static int TestRoundTripBladeWitness(CurveTag curve,double first,double last,double at,bool reversePair,out IntervalNumber station,out bool handled)
    {
        station=UncertifiedStation;handled=false;
        var record=Curves[GetCurveSlotByTag(curve)];var data=ICurveDataPool[record.DataIndex];
        var support=data.Surface0Tag;var plane=data.Surface1Tag;
        if(Surfaces[GetSurfaceSlotByTag(support)].Class!=SurfaceClass.BlendSurface)(support,plane)=(plane,support);
        var scratch=EnsureCommandScratch();
        try
        {
            var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],witness=stackalloc IntervalNumber[7];
            var status=JointNestedBlendEvaluation.BoundSharedSpineWitness(program,data.ProgramSpine,new(at,at,0,0),bounds,capture:witness);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var point=Vector(witness[0].Midpoint,witness[1].Midpoint,witness[2].Midpoint);
            var error=(new IntervalVector(witness[0],witness[1],witness[2])-new IntervalVector(point)).Norm().Value.Upper;
            var root=new RegularBoundaryRoot{ContactSurface=support,CuttingSurface=plane,Certified=PlanePairBlendLineCertificate,
                BlendStation=witness[6],Root=new(){Point=point,SpatialError=error}};
            if(reversePair)(data.Surface0Tag,data.Surface1Tag)=(data.Surface1Tag,data.Surface0Tag);
            var parameter=double.NaN;
            return LocateCertifiedBladeCut(new(){Curve=curve,First=first,Last=last},root,data,ref parameter,ref station,out handled);
        }
        finally{if(scratch)ReleaseCommandScratch(State.Session);}
    }

    internal static int TestLocateCertifiedLineCut(CurveTag source,CurveTag target,SurfTag support,SurfTag plane,double first,double last,
        out IntervalNumber bound,out bool certified,out bool inside,double sourceFirst=0,double sourceLast=1,bool fromPlanePair=false,bool fromFiniteGenerator=false)
    {
        bound=UncertifiedStation;certified=inside=false;
        var edge=new ModelingEdge{Curve=source,Start=sourceFirst,End=sourceLast};
        FiniteCurveSurfaceRoot witness;
        if(TryFiniteLinePlaneEvent(edge,0,plane,out var proof)&&proof.Count==1)witness=proof.FirstRoot;
        else
        {
            Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[1];
            Span<ScalarRootIsolation> boxes=stackalloc ScalarRootIsolation[1];Span<double> errors=stackalloc double[1];
            if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)],out var prepared)
                ||IsolateFiniteExplicitLevelSet(edge,prepared,roots,boxes,errors,out var n)!=AlgorithmStatus.Success
                ||n!=1||!AllStrictlyInterior(edge,boxes))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            witness=ThirdFiniteRoot(roots[0],boxes[0],errors[0]);
        }
        var root=new RegularBoundaryRoot{Curve=source,ContactSurface=support,CuttingSurface=plane,Certified=ExplicitPlaneCertificate,Witness=1,Root=witness};
        var cut=new SheetCutCurve{Curve=target,First=first,Last=last,Construction=fromPlanePair?CutConstruction.AnalyticPlanePair
            :fromFiniteGenerator?CutConstruction.FiniteCylinderPlane:CutConstruction.None};
        var error=LocateCertifiedCut(cut,root,out var station,out var projected,out certified);
        if(error!=0||!certified)return error;
        var anchor=new BoundaryParameter{Parameter=station,Lower=projected.Lower,Upper=projected.Upper};
        return LocateBoundedStation(target,0,anchor,first,last,-1,-1,out _,out bound,out inside);
    }
#endif
}
