using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
public class SectionRootReuseTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void WholeGraphAndFiniteRootCertificatesMatchWithoutReuse(bool cylinder)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36];for(var u=0;u<3;u++)for(var v=0;v<3;v++){var at=4*(3*u+v);poles[at]=u*.5;poles[at+1]=v*.5;poles[at+2]=(u==1?-.25:.25)+(v==1?-.25:.25)-.23*.23;poles[at+3]=1;}
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(0,1,0,1)));var support=cylinder?new AnalyticSurface(SurfaceClass.Cylinder,new(){X=.5,Y=.5},new(){Z=1},new(){X=1},.23):new AnalyticSurface(SurfaceClass.Plane,default,new(){Z=1},new(){X=1});
        var a=new SurfaceIntersectionNode[512];var b=new SurfaceIntersectionNode[512];var ea=new SurfaceIntersectionSegment[512];var eb=new SurfaceIntersectionSegment[512];var na=0;var nb=0;var nea=0;var neb=0;var ba=32768;var bb=32768;
        try
        {
            SurfacePlaneIntersection.ReuseSectionRoots=false;var expected=SurfacePlaneIntersection.Find(patch,support,a,ref na,ea,ref nea,ref ba);
            SurfacePlaneIntersection.ReuseSectionRoots=true;SurfacePlaneIntersection.TestSectionReuses=0;var status=SurfacePlaneIntersection.Find(patch,support,b,ref nb,eb,ref neb,ref bb);
            Assert.Equal(AlgorithmStatus.Success,expected);Assert.Equal(expected,status);Assert.Equal(na,nb);Assert.Equal(nea,neb);Assert.Equal(ba,bb);Assert.True(SurfacePlaneIntersection.TestSectionReuses>0);
            for(var i=0;i<na;i++)
            {
                var x=a[i];var y=b[i];foreach(var pair in new[]{(x.Point.X,y.Point.X),(x.Point.Y,y.Point.Y),(x.Point.Z,y.Point.Z),(x.U,y.U),(x.V,y.V),(x.S,y.S),(x.T,y.T),(x.SpatialError,y.SpatialError),(x.Isolation.Root.Lower,y.Isolation.Root.Lower),(x.Isolation.Root.Upper,y.Isolation.Root.Upper),(x.Isolation.MonotoneDomain.Lower,y.Isolation.MonotoneDomain.Lower),(x.Isolation.MonotoneDomain.Upper,y.Isolation.MonotoneDomain.Upper)})Assert.Equal(BitConverter.DoubleToInt64Bits(pair.Item1),BitConverter.DoubleToInt64Bits(pair.Item2));
                Assert.Equal(x.RootAxis,y.RootAxis);Assert.Equal(x.RemappedParameters,y.RemappedParameters);Assert.Equal(x.Isolation.Certified,y.Isolation.Certified);Assert.Equal(x.Isolation.Increasing,y.Isolation.Increasing);
            }
            for(var i=0;i<nea;i++){Assert.Equal(ea[i].Start,eb[i].Start);Assert.Equal(ea[i].End,eb[i].End);Assert.Equal(BitConverter.DoubleToInt64Bits(ea[i].ChordalError),BitConverter.DoubleToInt64Bits(eb[i].ChordalError));}
            // A new finite call has a separate proof set and the original
            // exhausted-budget failure, even after a successful prior graph.
            na=nea=0;ba=0;Assert.Equal(AlgorithmStatus.NotConverged,SurfacePlaneIntersection.Find(patch,support,a,ref na,ea,ref nea,ref ba));Assert.Equal(0,na);Assert.Equal(0,nea);
        }
        finally{SurfacePlaneIntersection.ReuseSectionRoots=true;}
    }
}
