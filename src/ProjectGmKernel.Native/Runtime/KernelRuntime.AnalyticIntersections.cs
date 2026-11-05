using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Finite intersection domains must follow from the geometry. A search
    // radius chosen from a seed or a sampling grid cannot prove completeness.
    private static AlgorithmStatus AnalyticIntersectionBounds(in AnalyticSurface first, in AnalyticSurface second,
        out IntervalBox3 bounds)
    {
        bounds = default;
        var boundedFirst = first.Kind is SurfaceClass.Sphere or SurfaceClass.Torus;
        var boundedSecond = second.Kind is SurfaceClass.Sphere or SurfaceClass.Torus;
        if (boundedFirst || boundedSecond)
        {
            var a = boundedFirst ? first : second;
            var radius = new IntervalNumber(a.Radius) + (a.Kind == SurfaceClass.Torus ? a.Secondary : 0);
            if (boundedFirst && boundedSecond)
            {
                var otherRadius = new IntervalNumber(second.Radius) + (second.Kind == SurfaceClass.Torus ? second.Secondary : 0);
                if (otherRadius.Upper < radius.Upper) { a = second; radius = otherRadius; }
            }
            bounds = AnalyticBallBounds(a.Origin, radius.Upper); return AlgorithmStatus.Success;
        }
        if (first.Kind != SurfaceClass.Cylinder || second.Kind != SurfaceClass.Cylinder) return AlgorithmStatus.Unsupported;
        // On cylinder A, distance to its axis is rA. If a point is also on B,
        // the corresponding A-axis point lies within rA+rB of the B axis.
        // Its distance grows at sin(angle) along the A axis.
        var ax=new IntervalNumber(first.Axis.X);var ay=new IntervalNumber(first.Axis.Y);var az=new IntervalNumber(first.Axis.Z);
        var bx=new IntervalNumber(second.Axis.X);var by=new IntervalNumber(second.Axis.Y);var bz=new IntervalNumber(second.Axis.Z);
        var cx=ay*bz-az*by;var cy=az*bx-ax*bz;var cz=ax*by-ay*bx;
        var sin2 = IntervalNumber.Square(cx) + IntervalNumber.Square(cy) + IntervalNumber.Square(cz);
        if (!(sin2.Lower > 1e-24)) return AlgorithmStatus.Unsupported;
        var dx=new IntervalNumber(first.Origin.X)-second.Origin.X;var dy=new IntervalNumber(first.Origin.Y)-second.Origin.Y;var dz=new IntervalNumber(first.Origin.Z)-second.Origin.Z;
        var px = (dy * bz) - (dz * by);
        var py = (dz * bx) - (dx * bz);
        var pz = (dx * by) - (dy * bx);
        var numerator = -(px*cx+py*cy+pz*cz);
        var centerParameter = numerator / sin2;
        var sinLower = Math.BitDecrement(Math.Sqrt(sin2.Lower));
        var boundA=AnalyticFrameRadius(first);var boundB=AnalyticFrameRadius(second);
        var axisLength=Math.BitIncrement(Math.Sqrt((IntervalNumber.Square(bx)+IntervalNumber.Square(by)+IntervalNumber.Square(bz)).Upper));
        var halfWidth = ((new IntervalNumber(boundA) + boundB)*axisLength / sinLower).Upper;
        var parameter = new IntervalNumber(Math.BitDecrement(centerParameter.Lower - halfWidth), Math.BitIncrement(centerParameter.Upper + halfWidth));
        var x = new IntervalNumber(first.Origin.X) + first.Axis.X * parameter + new IntervalNumber(-boundA, boundA);
        var y = new IntervalNumber(first.Origin.Y) + first.Axis.Y * parameter + new IntervalNumber(-boundA, boundA);
        var z = new IntervalNumber(first.Origin.Z) + first.Axis.Z * parameter + new IntervalNumber(-boundA, boundA);
        if (!x.Finite || !y.Finite || !z.Finite) return AlgorithmStatus.NumericalFailure;
        bounds = new(x.Lower, x.Upper, y.Lower, y.Upper, z.Lower, z.Upper); return AlgorithmStatus.Success;
    }

    private static double AnalyticFrameRadius(in AnalyticSurface surface)
    {
        var square=IntervalNumber.Square(new(surface.X.X))+IntervalNumber.Square(new(surface.X.Y))+IntervalNumber.Square(new(surface.X.Z))
            +IntervalNumber.Square(new(surface.Y.X))+IntervalNumber.Square(new(surface.Y.Y))+IntervalNumber.Square(new(surface.Y.Z));
        return (new IntervalNumber(surface.Radius)*Math.BitIncrement(Math.Sqrt(square.Upper))).Upper;
    }

    private static IntervalBox3 AnalyticBallBounds(in KernelVector3 center, double radius)
    {
        var r = new IntervalNumber(-radius, radius);
        var x = center.X + r; var y = center.Y + r; var z = center.Z + r;
        return new(x.Lower, x.Upper, y.Lower, y.Upper, z.Lower, z.Upper);
    }

    private static AlgorithmStatus DiscoverAnalyticSurfaceIntersections(SurfaceSlot first, SurfaceSlot second,
        Span<SurfaceIntersectionNode> nodes, out BufferCount nodeCount,
        Span<SurfaceIntersectionSegment> segments, out BufferCount segmentCount)
    {
        nodeCount = segmentCount = 0;
        if (!TryPrepareSurface(Surfaces[first], out var a) || !TryPrepareSurface(Surfaces[second], out var b)) return AlgorithmStatus.Unsupported;
        var status = AnalyticIntersectionBounds(a, b, out var bounds); if (status != AlgorithmStatus.Success) return status;
        var ac = AnalyticBezierPatches.Count(a); var bc = AnalyticBezierPatches.Count(b);
        if (ac == 0 || bc == 0) return AlgorithmStatus.Unsupported;
        if (!AnalyticBezierPatches.HasDomain(a, bounds) || !AnalyticBezierPatches.HasDomain(b, bounds)) return AlgorithmStatus.Success;
        Span<IntervalNumber> ap = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> bp = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var scratch = CommandScratch.Current; var memory = scratch.Take(100000);
        if (memory.IsEmpty) return AlgorithmStatus.WorkspaceTooSmall;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var localNodes = ModelSpan<SurfaceIntersectionNode>(bytes, ref cursor, GeneralIntersectionNodeLimit);
            var localEdges = ModelSpan<SurfaceIntersectionSegment>(bytes, ref cursor, GeneralIntersectionNodeLimit);
            var map = ModelSpan<BufferOffset>(bytes, ref cursor, GeneralIntersectionNodeLimit);
            for (var i = 0; i < ac; i++)
            {
                status = AnalyticBezierPatches.Create(a, bounds, i, ap, out var p, out var q, out var domain);
                if (status != AlgorithmStatus.Success) return status;
                var patch = new BezierSurfacePatch(ap[..(4*(p+1)*(q+1))],p,q,domain);
                for (var j = 0; j < bc; j++)
                {
                    status = AnalyticBezierPatches.Create(b, bounds, j, bp, out var r, out var s, out var otherDomain);
                    if (status != AlgorithmStatus.Success) return status;
                    var other = new BezierSurfacePatch(bp[..(4*(r+1)*(s+1))],r,s,otherDomain);
                    var nn = 0; var ne = 0;
                    status = ParametricSurfaceIntersection.Find(patch,other,localNodes,ref nn,localEdges,ref ne);
                    if (status != AlgorithmStatus.Success) return status;
                    for (var n = 0; n < nn; n++)
                    {
                        ref var node = ref localNodes[n];
                        status = OperationIntersection.Parameters(a,node.Point,out node.U,out node.V); if (status != AlgorithmStatus.Success) return status;
                        status = OperationIntersection.Parameters(b,node.Point,out node.S,out node.T); if (status != AlgorithmStatus.Success) return status;
                        var found = -1;
                        for (var k = 0; k < nodeCount; k++) if (GeneralIntersectionSameNode(first,second,nodes[k],node)) { found=k; break; }
                        if (found < 0) { if (nodeCount==nodes.Length) return AlgorithmStatus.OutputTooSmall; found=nodeCount; nodes[nodeCount++]=node; }
                        map[n]=found;
                    }
                    for (var e = 0; e < ne; e++)
                    {
                        var start=map[localEdges[e].Start];var end=map[localEdges[e].End];var duplicate=false;
                        for(var k=0;k<segmentCount;k++)if(segments[k].Start==start&&segments[k].End==end||segments[k].Start==end&&segments[k].End==start)duplicate=true;
                        if(duplicate)continue;
                        if(segmentCount==segments.Length)return AlgorithmStatus.OutputTooSmall;
                        segments[segmentCount++]=new(){Start=start,End=end,ChordalError=localEdges[e].ChordalError};
                    }
                }
            }
            return AlgorithmStatus.Success;
        }
        finally { scratch.Return(memory); }
    }
}
