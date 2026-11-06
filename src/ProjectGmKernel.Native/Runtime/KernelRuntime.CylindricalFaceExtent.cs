using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Axial height is affine on the intrinsic cylinder chart, so a bounded
    // finite face attains its extrema on its complete boundary. Its angular
    // image is enclosed by the entire cross-sectional disk, including seams.
    internal static AlgorithmStatus CertifiedCylindricalFaceExtent(FaceSlot face,out IntervalBox3 extent)
    {
        extent=default;if(face<0||!Faces.IsAlive(face))return AlgorithmStatus.InvalidInput;
        var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0||!TryPrepareSurface(Surfaces[slot],out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder||!double.IsFinite(cylinder.Radius)||cylinder.Radius<=0)return AlgorithmStatus.Unsupported;
        // Use a dual of the stored radial frame rather than assuming rounded
        // axis/reference data are exactly unit and orthogonal.
        var x=cylinder.X;var y=cylinder.Y;var axis=cylinder.Axis;
        var nx=new IntervalNumber(x.Y)*y.Z-new IntervalNumber(x.Z)*y.Y;
        var ny=new IntervalNumber(x.Z)*y.X-new IntervalNumber(x.X)*y.Z;
        var nz=new IntervalNumber(x.X)*y.Y-new IntervalNumber(x.Y)*y.X;
        var denominator=nx*axis.X+ny*axis.Y+nz*axis.Z;
        if(!denominator.Finite||denominator.Lower<=0)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17],restricted=stackalloc IntervalNumber[4*17];
        IntervalNumber height=default;var first=true;var haveCurve=false;var tolerance=Math.Max(Geometry.Intersection.OperationIntersection.DistanceTolerance,Faces[face].Tolerance);
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
                var interval=new PK_INTERVAL_s();interval.value[0]=lo;interval.value[1]=hi;
                if(ResolveIntersectionCurve(edge.CurveTag,interval,out var curve)!=0)return AlgorithmStatus.Unsupported;
                var count=GeneralCurvePieceCount(curve);if(count<=0)return AlgorithmStatus.Unsupported;
                tolerance=Math.Max(tolerance,edge.Tolerance);
                if(edge.StartVertex>=0)tolerance=Math.Max(tolerance,Vertices[edge.StartVertex].Tolerance);
                if(edge.EndVertex>=0)tolerance=Math.Max(tolerance,Vertices[edge.EndVertex].Tolerance);
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
}
