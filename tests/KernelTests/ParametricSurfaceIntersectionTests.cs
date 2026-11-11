using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace KernelTests;

public class ParametricSurfaceIntersectionTests
{
    [Fact]
    public void TransversePlanarPatchesDiscoverTheCompleteOpenBranch()
    {
        double[] first = [0,0,0, 0,1,0, 1,0,0, 1,1,0];
        double[] second = [0,.37,-.5, 0,.37,.5, 1,.37,-.5, 1,.37,.5];
        var (nodes, edges, n, e) = Intersect(first, 1, second, 1);
        Assert.True(e > 0); var degrees = new int[n];
        for (var i = 0; i < e; i++) { degrees[edges[i].Start]++; degrees[edges[i].End]++; }
        Assert.Equal(2, degrees.Count(value => value == 1)); Assert.All(degrees, value => Assert.InRange(value, 1, 2));
        Assert.Contains(nodes.Take(n), value => Math.Abs(value.Point.X) < 1e-9);
        Assert.Contains(nodes.Take(n), value => Math.Abs(value.Point.X - 1) < 1e-9);
        foreach (var node in nodes.Take(n)) { Assert.InRange(Math.Abs(node.Point.Y - .37), 0, 1e-9); Assert.InRange(Math.Abs(node.Point.Z), 0, 1e-9); }
    }

    [Fact]
    public void InternalClosedLoopIsDiscoveredWithoutAnyBoundarySeed()
    {
        var first = new double[27]; var plane = new double[27]; double[] z = [.25, -.25, .25];
        for (var u = 0; u < 3; u++) for (var v = 0; v < 3; v++)
        {
            var at = (3 * u + v) * 3; first[at] = plane[at] = u * .5; first[at + 1] = plane[at + 1] = v * .5;
            first[at + 2] = z[u] + z[v] - .0625;
        }
        var (nodes, edges, n, e) = Intersect(first, 2, plane, 2);
        Assert.True(e > 8); var degrees = new int[n];
        for (var i = 0; i < e; i++) { degrees[edges[i].Start]++; degrees[edges[i].End]++; }
        Assert.All(degrees, value => Assert.Equal(2, value));
        foreach (var node in nodes.Take(n))
        {
            var p = node.Point; Assert.InRange(Math.Abs((p.X - .5) * (p.X - .5) + (p.Y - .5) * (p.Y - .5) - .0625), 0, 1e-9);
        }
    }

    [Theory]
    [InlineData(0,1)] [InlineData(1e-13,1)] [InlineData(-1e-13,0)]
    public void OnlyAnExactlyProvenNativeCornerSuppliesAFiniteBoundaryRoot(double shift,int expected)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36],line=stackalloc IntervalNumber[8];
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {
            var at=4*(3*i+j);poles[at]=i*.5;poles[at+1]=j*.5;
            poles[at+2]=i==1&&j==1?.8:0;poles[at+3]=1;
        }
        line.Clear();line[2]=-shift;line[3]=1;line[6]=1-shift;line[7]=1;
        var surface=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(0,1,0,1)));
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(line,1,0,new(0,1,0,1)));
        var finite=new ParameterCell{Upper=new(){U=1,V=1,S=1}};
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[4];
        Span<IsolatedRootCertificate> proofs=stackalloc IsolatedRootCertificate[4];
        var status=ParametricSurfaceIntersection.Isolate(surface,curve,finite,3,roots,out var count,proofs);
        // The near-corner cases remain unresolved, rather than acquiring
        // the exact corner's existence proof through a small distance.
        if(shift!=0){Assert.Equal(AlgorithmStatus.NotConverged,status);Assert.Equal(0,count);return;}
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(expected,count);
        if(count!=0)
        {
            Assert.True(IsolatedRootCertificate.Contains(finite.First,proofs[0].First));
            Assert.True(IsolatedRootCertificate.Contains(finite.Second,proofs[0].Second));
            if(shift==0)Assert.Equal(0,proofs[0].Second.U0);
            else Assert.True(proofs[0].Second.U0>0);
        }
    }

    private static (SurfaceIntersectionNode[] Nodes, SurfaceIntersectionSegment[] Segments, int Count, int Edges)
        Intersect(double[] a, int p, double[] b, int q)
    {
        var ak = Enumerable.Repeat(0.0, p + 1).Concat(Enumerable.Repeat(1.0, p + 1)).ToArray();
        var bk = Enumerable.Repeat(0.0, q + 1).Concat(Enumerable.Repeat(1.0, q + 1)).ToArray();
        var first = new BSurfaceView(p, p, p + 1, p + 1, 3, false, false, false, a, ak, ak);
        var second = new BSurfaceView(q, q, q + 1, q + 1, 3, false, false, false, b, bk, bk);
        var ap = new IntervalNumber[4 * (p + 1) * (p + 1)]; var bp = new IntervalNumber[4 * (q + 1) * (q + 1)];
        Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Extract(first, p, p, ap));
        Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Extract(second, q, q, bp));
        var nodes = new SurfaceIntersectionNode[4096]; var edges = new SurfaceIntersectionSegment[4096]; int n = 0, e = 0;
        var status = ParametricSurfaceIntersection.Find(new(ap, p, p, new(0,1,0,1)), new(bp, q, q, new(0,1,0,1)), nodes, ref n, edges, ref e);
        Assert.Equal(AlgorithmStatus.Success, status); return (nodes, edges, n, e);
    }
}
