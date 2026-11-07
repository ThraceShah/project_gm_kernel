using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Axial height is affine on the intrinsic cylinder chart, so a bounded
    // finite face attains its extrema on its complete boundary. Its angular
    // image is enclosed by the entire cross-sectional disk, including seams.
    internal static AlgorithmStatus CertifiedCylindricalFaceExtent(FaceSlot face,out IntervalBox3 extent)
    {
        extent=default;
        var status=CertifiedCylindricalFaceAxialRange(face,out var cylinder,out var height,out var tolerance);
        if(status!=AlgorithmStatus.Success)return status;
        var x=cylinder.X;var y=cylinder.Y;var axis=cylinder.Axis;
        Span<IntervalNumber> coordinates=stackalloc IntervalNumber[3];
        for(var d=0;d<3;d++)
        {
            var xx=d==0?x.X:d==1?x.Y:x.Z;var yy=d==0?y.X:d==1?y.Y:y.Z;
            var square=IntervalNumber.Square(new(xx))+IntervalNumber.Square(new(yy));
            var radius=xx==0&&yy==0?0:Math.BitIncrement(Math.Sqrt(square.Upper));var radial=(new IntervalNumber(cylinder.Radius)*radius+tolerance).Upper;
            var origin=d==0?cylinder.Origin.X:d==1?cylinder.Origin.Y:cylinder.Origin.Z;var direction=d==0?axis.X:d==1?axis.Y:axis.Z;
            coordinates[d]=new IntervalNumber(origin)+height*direction+new IntervalNumber(-radial,radial);if(!coordinates[d].Finite)return AlgorithmStatus.NotConverged;
        }
        extent=new(coordinates[0].Lower,coordinates[0].Upper,coordinates[1].Lower,coordinates[1].Upper,coordinates[2].Lower,coordinates[2].Upper);return AlgorithmStatus.Success;
    }

    // A near-orthogonal axis can intersect the infinite plane at an
    // arbitrarily distant station. Test the finite axial range directly;
    // the plane normal's projection of the full radial disk is bounded.
    internal static bool RegularCylinderFaceSeparatedFromPlane(FaceSlot face,SurfTag other,double tolerance)
    {
        var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);var planeSlot=GetSurfaceSlotByTag(other);
        if(slot<0||planeSlot<0)return false;
        if(Surfaces[slot].Class!=SurfaceClass.Cylinder||Surfaces[planeSlot].Class!=SurfaceClass.Plane
            ||!TryPrepareSurface(Surfaces[slot],out var cylinder)||!TryPrepareSurface(Surfaces[planeSlot],out var plane))return false;
        var tilt=Math.Abs(Dot(cylinder.Axis,plane.Axis));if(tilt==0||tilt>OperationIntersection.AngularTolerance)return false;
        if(CertifiedCylindricalFaceAxialRange(face,out cylinder,out var height,out var precision)!=AlgorithmStatus.Success)return false;
        var n=new IntervalVector(plane.Axis);var x=IntervalVector.Dot(n,new IntervalVector(cylinder.X)).Value;
        var y=IntervalVector.Dot(n,new IntervalVector(cylinder.Y)).Value;
        var radial=Math.BitIncrement(Math.Sqrt((IntervalNumber.Square(x)+IntervalNumber.Square(y)).Upper));
        var margin=(new IntervalNumber(cylinder.Radius)*radial+new IntervalNumber(precision)*n.Norm().Value).Upper;
        var distance=IntervalVector.Dot(n,new IntervalVector(cylinder.Origin)-new IntervalVector(plane.Origin)).Value
            +height*IntervalVector.Dot(n,new IntervalVector(cylinder.Axis)).Value;
        var bound=distance+new IntervalNumber(-margin,margin);
        return bound.Finite&&(bound.Lower>tolerance||bound.Upper< -tolerance);
    }

    // The infinite near-parallel conic has an ill-conditioned centre. Over
    // the finite cylinder occurrence, solve h+d*v+r*(a*cos(u)+b*sin(u))=0.
    // A strict cosine range proves exactly two branches for every height.
    // Use a generator carrier only when the complete branch motion and its
    // anchor enclosure fit the arithmetic error of the input frame.
    private static int TryPrepareFiniteCylinderPlaneGenerators(ReadOnlySpan<ModelingFace> faces,ReadOnlySpan<FaceSlot> sources,
        SurfTag first,SurfTag second,Span<SheetCutCurve> cuts,out BufferCount count,out bool handled)
    {
        count=0;handled=false;var a=Surfaces[GetSurfaceSlotByTag(first)];var b=Surfaces[GetSurfaceSlotByTag(second)];
        if(!(a.Class==SurfaceClass.Cylinder&&b.Class==SurfaceClass.Plane||a.Class==SurfaceClass.Plane&&b.Class==SurfaceClass.Cylinder))return 0;
        if(!TryPrepareSurface(a,out var av)||!TryPrepareSurface(b,out var bv))return 0;
        var reverse=av.Kind==SurfaceClass.Plane&&bv.Kind==SurfaceClass.Cylinder;
        var cylinder=reverse?bv:av;var plane=reverse?av:bv;
        if(cylinder.Kind!=SurfaceClass.Cylinder||plane.Kind!=SurfaceClass.Plane)return 0;
        var tilt=Math.Abs(Dot(cylinder.Axis,plane.Axis));if(tilt==0||tilt>OperationIntersection.AngularTolerance)return 0;
        var cylinderTag=reverse?second:first;IntervalNumber height=default;var haveRange=false;
        for(var f=0;f<faces.Length;f++)if(faces[f].Surface==cylinderTag)
        {
            if(CertifiedCylindricalFaceAxialRange(sources[f],out _,out var local,out _)!=AlgorithmStatus.Success)return 0;
            height=haveRange?IntervalNumber.Hull(height,local):local;haveRange=true;
        }
        if(!haveRange)return 0;
        var axis=new IntervalVector(cylinder.Axis);var normal=new IntervalVector(plane.Axis);
        var x=new IntervalVector(cylinder.X);var y=new IntervalVector(cylinder.Y);var origin=new IntervalVector(cylinder.Origin);
        var dx=IntervalVector.Dot(x,normal).Value;var dy=IntervalVector.Dot(y,normal).Value;
        var rho=IntervalDual.Sqrt(new IntervalDual(IntervalNumber.Square(dx)+IntervalNumber.Square(dy),0)).Value;
        var amplitude=rho*cylinder.Radius;if(!(amplitude.Lower>0))return 0;
        var h=IntervalVector.Dot(origin-new IntervalVector(plane.Origin),normal).Value;
        var d=IntervalVector.Dot(axis,normal).Value;var ratio=-(h+d*height)/amplitude;
        if(!ratio.Finite||!(ratio.Magnitude<1))return 0;
        var gap=IntervalDual.Sqrt(new IntervalDual(1-IntervalNumber.Square(ratio),0)).Value;
        if(!(gap.Lower>0))return 0;
        var station=height.Midpoint;var delta=height-station;
        var drift=(new IntervalNumber(d.Magnitude)*delta.Magnitude/(amplitude*gap)
            *cylinder.Radius*(x.Norm().Value+y.Norm().Value)).Upper;
        var level=-(h+d*station)/amplitude;var sine=IntervalDual.Sqrt(new IntervalDual(1-IntervalNumber.Square(level),0)).Value;
        var center=origin+axis*new IntervalDual(station,0);
        var roundoff=(64*SmallLinearSolve.MachineEpsilon*(origin.Norm().Value+new IntervalVector(plane.Origin).Norm().Value
            +height.Magnitude+cylinder.Radius)).Upper;
        var limit=Math.Min(roundoff,OperationIntersection.DistanceTolerance/16);if(!(limit>0)||!(drift<limit))return 0;
        Span<KernelVector3> anchors=stackalloc KernelVector3[2];Span<KernelSense> senses=stackalloc KernelSense[2];
        for(var i=0;i<2;i++)
        {
            var sign=i==0?1:-1;
            var c=(level*dx-sign*sine*dy)/rho;var s=(level*dy+sign*sine*dx)/rho;
            var anchor=center+(x*new IntervalDual(c,0)+y*new IntervalDual(s,0))*cylinder.Radius;
            var point=new KernelVector3{X=anchor.X.Value.Midpoint,Y=anchor.Y.Value.Midpoint,Z=anchor.Z.Value.Midpoint};
            if(((anchor-new IntervalVector(point)).Norm().Value+drift).Upper>=limit)return 0;
            if(AnalyticImplicitEvaluation.Evaluate(av,point,1,out var ga)!=AlgorithmStatus.Success
                ||AnalyticImplicitEvaluation.Evaluate(bv,point,1,out var gb)!=AlgorithmStatus.Success)return 0;
            var turn=Dot(ProfileCross(ga.Gradient,gb.Gradient),cylinder.Axis)*LocalSenseSign(a.Sense)*LocalSenseSign(b.Sense);
            if(turn==0||!double.IsFinite(turn))return 0;
            anchors[i]=point;senses[i]=turn>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        }
        handled=true;if(cuts.Length<2)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<2;i++)
        {
            var p=anchors[i];var curve=CreateLineCurveTag(p.X,p.Y,p.Z,cylinder.Axis.X,cylinder.Axis.Y,cylinder.Axis.Z);
            if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;
            ref var record=ref Curves[GetCurveSlotByTag(curve)];record.Sense=senses[i];
            ref var line=ref LineDataPool[record.DataIndex];
            line.AxisX=cylinder.Axis.X;line.AxisY=cylinder.Axis.Y;line.AxisZ=cylinder.Axis.Z;
            cuts[count++]=new(){Curve=curve,First=delta.Lower,Last=delta.Upper};
        }
        return 0;
    }

    private static AlgorithmStatus CertifiedCylindricalFaceAxialRange(FaceSlot face,out AnalyticSurface cylinder,out IntervalNumber height,out double tolerance)
    {
        cylinder=default;height=default;tolerance=0;if(face<0||!Faces.IsAlive(face))return AlgorithmStatus.InvalidInput;
        var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0||!TryPrepareSurface(Surfaces[slot],out cylinder)||cylinder.Kind!=SurfaceClass.Cylinder||!double.IsFinite(cylinder.Radius)||cylinder.Radius<=0)return AlgorithmStatus.Unsupported;
        // Use a dual of the stored radial frame rather than assuming rounded
        // axis/reference data are exactly unit and orthogonal.
        var x=cylinder.X;var y=cylinder.Y;var axis=cylinder.Axis;
        var nx=new IntervalNumber(x.Y)*y.Z-new IntervalNumber(x.Z)*y.Y;
        var ny=new IntervalNumber(x.Z)*y.X-new IntervalNumber(x.X)*y.Z;
        var nz=new IntervalNumber(x.X)*y.Y-new IntervalNumber(x.Y)*y.X;
        var denominator=nx*axis.X+ny*axis.Y+nz*axis.Z;
        if(!denominator.Finite||denominator.Lower<=0)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17],restricted=stackalloc IntervalNumber[4*17];
        var first=true;var haveCurve=false;tolerance=Math.Max(OperationIntersection.DistanceTolerance,Faces[face].Tolerance);
        var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            if(RegularVertexLoop(loop,out var vertex))
            {
                var error=SheetOriginalVertex(vertex,out var point);if(error!=0)return AlgorithmStatus.InvalidInput;
                var value=((new IntervalNumber(point.X)-cylinder.Origin.X)*nx+(new IntervalNumber(point.Y)-cylinder.Origin.Y)*ny+(new IntervalNumber(point.Z)-cylinder.Origin.Z)*nz)/denominator;
                height=first?value:IntervalNumber.Hull(height,value);first=false;tolerance=Math.Max(tolerance,Vertices[vertex].Tolerance);continue;
            }
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {
                var edgeSlot=Fins[fin].Edge;if(edgeSlot<0)return AlgorithmStatus.Unsupported;var edge=Edges[edgeSlot];
                if(!TryProfileEdgeRange(edge,out var lo,out var hi))return AlgorithmStatus.Unsupported;
                tolerance=Math.Max(tolerance,edge.Tolerance);
                if(edge.StartVertex>=0)tolerance=Math.Max(tolerance,Vertices[edge.StartVertex].Tolerance);
                if(edge.EndVertex>=0)tolerance=Math.Max(tolerance,Vertices[edge.EndVertex].Tolerance);
                var carrierSlot=GetCurveSlotByTag(edge.CurveTag);if(carrierSlot<0)return AlgorithmStatus.Unsupported;
                if(Curves[carrierSlot].Class==CurveClass.ICurve)
                {
                    var owns=EnsureCommandScratch();IntervalBox3 box;AlgorithmStatus certified;
                    try{certified=CertifiedIntersectionCurveExtent(edge.CurveTag,lo,hi,out box);}
                    finally{if(owns)ReleaseCommandScratch(State.Session);}
                    if(certified!=AlgorithmStatus.Success)return certified;
                    var value=((new IntervalNumber(box.XLo,box.XHi)-cylinder.Origin.X)*nx
                        +(new IntervalNumber(box.YLo,box.YHi)-cylinder.Origin.Y)*ny
                        +(new IntervalNumber(box.ZLo,box.ZHi)-cylinder.Origin.Z)*nz)/denominator;
                    if(!value.Finite)return AlgorithmStatus.NotConverged;
                    height=first?value:IntervalNumber.Hull(height,value);first=false;haveCurve=true;continue;
                }
                var interval=new PK_INTERVAL_s();interval.value[0]=lo;interval.value[1]=hi;
                if(ResolveIntersectionCurve(edge.CurveTag,interval,out var curve)!=0)return AlgorithmStatus.Unsupported;
                var count=GeneralCurvePieceCount(curve);if(count<=0)return AlgorithmStatus.Unsupported;
                for(var piece=0;piece<count;piece++)
                {
                    var status=GeneralCurvePiece(curve,piece,poles,out var degree,out var domain,out var a,out var b);if(status!=AlgorithmStatus.Success)return status;if(!(b>a))continue;
                    status=BezierSurfaceBounds.Restrict(new(poles[..(4*(degree+1))],degree,0,domain),new(a,b,0,1),restricted);if(status!=AlgorithmStatus.Success)return status;
                    for(var i=0;i<=degree;i++)
                    {
                        var at=4*i;var weight=restricted[at+3];if(!weight.Finite||weight.Lower<=0)return AlgorithmStatus.Unsupported;
                        var value=((restricted[at]-cylinder.Origin.X*weight)*nx+(restricted[at+1]-cylinder.Origin.Y*weight)*ny+(restricted[at+2]-cylinder.Origin.Z*weight)*nz)/(weight*denominator);
                        if(!value.Finite)return AlgorithmStatus.NotConverged;height=first?value:IntervalNumber.Hull(height,value);first=false;
                    }
                    haveCurve=true;
                }
            }
        }
        if(!haveCurve||!double.IsFinite(tolerance)||tolerance<0)return AlgorithmStatus.Unsupported;
        var normalSquare=IntervalNumber.Square(nx)+IntervalNumber.Square(ny)+IntervalNumber.Square(nz);
        var margin=new IntervalNumber(tolerance)*Math.BitIncrement(Math.Sqrt(normalSquare.Upper))/denominator;
        if(!margin.Finite)return AlgorithmStatus.NotConverged;height+=new IntervalNumber(-margin.Upper,margin.Upper);
        return height.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }
}
