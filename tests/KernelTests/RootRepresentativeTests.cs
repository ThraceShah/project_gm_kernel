using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
[Collection("KernelTests")]
public class RootRepresentativeTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void StrictBracketKeepsNativeParametersAndUsesFewerPointEvaluations(bool quadratic)
    {
        var r=.371;var degree=quadratic?2:1;Span<IntervalNumber> poles=stackalloc IntervalNumber[12];poles.Clear();
        var first=quadratic?0:-3.0;var last=quadratic?1:5.0;
        for(var i=0;i<=degree;i++){poles[4*i]=first+(last-first)*i/degree;poles[4*i+3]=1;poles[4*i+2]=quadratic?(i==degree?1:0)-r*r:(double)i-r;}
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*(degree+1))],degree,0,new(first,last,0,1)));var plane=new AnalyticSurface(SurfaceClass.Plane,default,new KernelVector3{Z=1},new KernelVector3{X=1});Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];
        try
        {
            SurfaceIsoPlaneRoots.BisectionRepresentatives=true;SurfaceIsoPlaneRoots.TestRepresentativeEvaluations=0;var count=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,plane,first,last,roots,ref count));Assert.Equal(1,count);var old=SurfaceIsoPlaneRoots.TestRepresentativeEvaluations;
            SurfaceIsoPlaneRoots.BisectionRepresentatives=false;SurfaceIsoPlaneRoots.TestRepresentativeEvaluations=0;count=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,plane,first,last,roots,ref count));Assert.Equal(1,count);Assert.True(SurfaceIsoPlaneRoots.TestRepresentativeEvaluations<old);Assert.InRange(Math.Abs(roots[0].First-(quadratic?r:first+(last-first)*r)),0,1e-9);Assert.InRange(Math.Abs(roots[0].Point.Z),0,2e-12);
            count=0;var start=GC.GetAllocatedBytesForCurrentThread();var status=SurfaceIsoPlaneRoots.Find(curve,plane,first,last,roots,ref count);var allocated=GC.GetAllocatedBytesForCurrentThread()-start;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
        }
        finally{SurfaceIsoPlaneRoots.BisectionRepresentatives=false;}
    }
    [Fact]
    public void CylinderKeepsBothFiniteRootsAndCertifiedIsolationDoesNotUseTheNewProposal()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];poles.Clear();poles[0]=-1;poles[4]=1;poles[1]=poles[5]=.25;poles[3]=poles[7]=1;
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,0,new(-1,1,0,1)));var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new KernelVector3{Z=1},new KernelVector3{X=1},.5);Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[4];
        try{foreach(var old in new[]{true,false})
        {
            SurfaceIsoPlaneRoots.BisectionRepresentatives=old;var count=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,cylinder,-1,1,roots,ref count));Assert.Equal(2,count);OperationIntersection.Sort(roots[..count]);var at=Math.Sqrt(3)/4;Assert.InRange(Math.Abs(roots[0].First+at),0,1e-9);Assert.InRange(Math.Abs(roots[1].First-at),0,1e-9);
            count=0;SurfaceIsoPlaneRoots.TestRepresentativeEvaluations=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,cylinder,-1,1,roots,ref count,isolation:proof));Assert.Equal(2,count);Assert.Equal(0,SurfaceIsoPlaneRoots.TestRepresentativeEvaluations);for(var i=0;i<count;i++)Assert.True(proof[i].Certified);
            count=0;Assert.Equal(AlgorithmStatus.OutputTooSmall,SurfaceIsoPlaneRoots.Find(curve,cylinder,-1,1,roots[..0],ref count));
        }}finally{SurfaceIsoPlaneRoots.BisectionRepresentatives=false;}
    }
    [Fact]
    public void TangentAndCoincidentCellsKeepTheirOriginalUnresolvedStatus()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[12];poles.Clear();for(var i=0;i<3;i++){poles[4*i]=i*.5;poles[4*i+3]=1;}var plane=new AnalyticSurface(SurfaceClass.Plane,default,new KernelVector3{Z=1},new KernelVector3{X=1});Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];
        try{foreach(var tangent in new[]{false,true})
        {
            poles[2]=poles[6]=0;poles[10]=tangent?1:0;var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,0,new(0,1,0,1)));AlgorithmStatus? original=null;
            foreach(var old in new[]{true,false}){SurfaceIsoPlaneRoots.BisectionRepresentatives=old;var count=0;var status=SurfaceIsoPlaneRoots.Find(curve,plane,0,1,roots,ref count,strictSeparation:true);Assert.NotEqual(AlgorithmStatus.Success,status);if(original.HasValue)Assert.Equal(original.Value,status);original=status;}
        }}finally{SurfaceIsoPlaneRoots.BisectionRepresentatives=false;}
    }
}
