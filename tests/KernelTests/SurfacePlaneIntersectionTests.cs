using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SurfacePlaneIntersectionTests
{
    [Fact]
    public void FindsBothDisconnectedBranchesWithoutBoundarySeeds()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[24];poles.Clear();
        for(var i=0;i<3;i++)for(var j=0;j<2;j++)
        {var at=4*(2*i+j);poles[at]=i*.5;poles[at+1]=j;poles[at+2]=i==1?-.3125:.1875;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,1,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[512];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[512];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
        Assert.Equal(2,Components(nodes[..nn],edges[..ne],false));
        foreach(var node in nodes[..nn]){Assert.True(Math.Abs(node.U-.25)<1e-8||Math.Abs(node.U-.75)<1e-8);Assert.True(Math.Abs(node.Point.Z)<1e-10);}
    }

    [Fact]
    public void FindsAnInteriorClosedComponent()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36];poles.Clear();
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {var at=4*(3*i+j);poles[at]=i*.5;poles[at+1]=j*.5;poles[at+2]=(i==1?-.25:.25)+(j==1?-.25:.25)-.23*.23;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[512];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[512];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
        Assert.Equal(1,Components(nodes[..nn],edges[..ne],true));
        foreach(var node in nodes[..nn])Assert.Equal(.23*.23,(node.U-.5)*(node.U-.5)+(node.V-.5)*(node.V-.5),8);
    }

    [Fact]
    public void PreservesACompleteDomainBoundaryBranch()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++)
        {var at=4*(2*i+j);poles[at]=poles[at+2]=i;poles[at+1]=j;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[512];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[512];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
        Assert.Equal(1,Components(nodes[..nn],edges[..ne],false));
        var low=1.0;var high=0.0;foreach(var node in nodes[..nn]){Assert.Equal(0,node.U,8);low=Math.Min(low,node.V);high=Math.Max(high,node.V);}
        Assert.Equal(0,low);Assert.Equal(1,high);
    }

    [Fact]
    public void CoincidentUnresolvedCellsCannotSucceedAsNoIntersection()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++){var at=4*(2*i+j);poles[at]=i;poles[at+1]=j;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[512];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[512];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.NotConverged,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
    }

    [Fact]
    public void TinyFreeParameterSpanDoesNotDiscardASpatiallyLongBranch()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++)
        {var at=4*(2*i+j);poles[at]=i;poles[at+1]=j;poles[at+2]=i-.5;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1e-12)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[512];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[512];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
        Assert.Equal(1,Components(nodes[..nn],edges[..ne],false));
        var low=1.0;var high=0.0;foreach(var node in nodes[..nn]){low=Math.Min(low,node.Point.Y);high=Math.Max(high,node.Point.Y);}
        Assert.Equal(0,low,8);Assert.Equal(1,high,8);
    }

    [Fact]
    public void ExhaustedSharedBudgetCannotPublishAResult()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++){var at=4*(2*i+j);poles[at]=i;poles[at+1]=j;poles[at+2]=i-.5;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[64];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[64];var nn=0;var ne=0;var remaining=0;
        Assert.Equal(AlgorithmStatus.NotConverged,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne,ref remaining));
        Assert.Equal(0,nn);Assert.Equal(0,ne);
    }

    [Fact]
    public void IsolatedCornerContactCannotSucceedAsAnEmptyGraph()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++){var at=4*(2*i+j);poles[at]=i;poles[at+1]=j;poles[at+2]=i+j;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1)));
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[64];Span<SurfaceIntersectionSegment> edges=stackalloc SurfaceIntersectionSegment[64];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.NotConverged,SurfacePlaneIntersection.Find(patch,Plane(),nodes,ref nn,edges,ref ne));
    }

    private static AnalyticSurface Plane()=>new(SurfaceClass.Plane,default,new KernelVector3{Z=1},new KernelVector3{X=1});
    private static int Components(ReadOnlySpan<SurfaceIntersectionNode> nodes,ReadOnlySpan<SurfaceIntersectionSegment> edges,bool closed)
    {
        Assert.True(nodes.Length>1&&edges.Length>0);var degrees=new int[nodes.Length];var components=new int[nodes.Length];Array.Fill(components,-1);
        foreach(var edge in edges){degrees[edge.Start]++;degrees[edge.End]++;Assert.True(double.IsFinite(edge.ChordalError)&&edge.ChordalError>=0);}
        foreach(var degree in degrees)Assert.True(closed?degree==2:degree is 1 or 2);
        var count=0;var queue=new int[nodes.Length];
        for(var seed=0;seed<nodes.Length;seed++)
        {
            if(components[seed]>=0)continue;var head=0;var tail=1;queue[0]=seed;components[seed]=count++;
            while(head<tail){var at=queue[head++];foreach(var edge in edges){var other=edge.Start==at?edge.End:edge.End==at?edge.Start:-1;if(other<0||components[other]>=0)continue;components[other]=components[at];queue[tail++]=other;}}
        }
        return count;
    }
}
