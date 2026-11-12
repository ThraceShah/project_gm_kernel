using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
public class PreparedNativeSectionTests
{
    [Theory] [InlineData(1.5707963267948966,false)] [InlineData(.001,false)] [InlineData(0,false)] [InlineData(-1.0471975511965976,false)] [InlineData(1.5707963267948966,true)]
    public void PreparedConstantsRetainAllBoundsAndCompleteRootCertificates(double angle,bool reverse)
    {
        var x=new KernelVector3{X=1};var z=new KernelVector3{Z=1};var last=new KernelVector3{X=Math.Cos(angle),Y=Math.Sin(angle)};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,last,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2*last.X,Y=-.2*last.Y},last,z),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1,ReverseSection=reverse}];
        BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Line,First=z},First=0,Second=1,ChartCount=3}];BlendChartInstruction[] charts=[new(){Parameter=2,Position=new(){Z=2},Scale=1},new(){Parameter=3,Position=new(){Z=3},Scale=1},new(){Parameter=4,Position=new(){Z=4},Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var source=new SurfaceIntersectionPatch(program,4,new(2,4,0,1));
        Span<IntervalNumber> actual=stackalloc IntervalNumber[9],original=stackalloc IntervalNumber[9];Span<PreparedNativeSection> storage=stackalloc PreparedNativeSection[2];
        Assert.Equal(AlgorithmStatus.Success,source.BoundCell(new(2.2,4.4,-.4,1.4),actual,out var certified));
        try
        {
            foreach(var u in new[]{2.7,3,4.2})
            {
                var curve=new SurfaceIntersectionPatch(certified,true,u,-.3,1.3);var prepared=curve.PrepareNativeSection(storage,-.3,1.3);
                foreach(var range in new[]{new IntervalNumber(-.3,1.3),new(.2,.8),new(.37),new(.999,1.001)})
                {
                    SurfaceIntersectionPatch.UsePreparedSections=false;var old=curve.Bound(new(range.Lower,range.Upper,0,0),original);
                    SurfaceIntersectionPatch.UsePreparedSections=true;SurfaceIntersectionPatch.TestPreparedSectionBounds=0;
                    var before=GC.GetAllocatedBytesForCurrentThread();var status=prepared.Bound(new(range.Lower,range.Upper,0,0),actual);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                    Assert.Equal(old,status);Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);Assert.Equal(1,SurfaceIntersectionPatch.TestPreparedSectionBounds);Equal(original,actual);
                }
                SurfaceIntersectionPatch.TestPreparedSectionBounds=0;var unprepared=curve.PrepareNativeSection(Span<PreparedNativeSection>.Empty,-.3,1.3);Assert.Equal(AlgorithmStatus.Success,unprepared.Bound(new(.2,.8,0,0),actual));Assert.Equal(0,SurfaceIntersectionPatch.TestPreparedSectionBounds);
            }
            foreach(var curve in new[]{false,true})
            {
                var section=new SurfaceIntersectionPatch(curve?source:certified,true,2.7,-.3,1.3);SurfaceIntersectionPatch.TestPreparedSectionBounds=0;
                var refused=section.PrepareNativeSection(storage,-.5,1.5);Assert.Equal(AlgorithmStatus.Success,refused.Bound(new(.2,.8,0,0),actual));Assert.Equal(0,SurfaceIntersectionPatch.TestPreparedSectionBounds);
            }
            if(angle!=Math.PI/2||reverse)return;
            var iso=new SurfaceIntersectionPatch(certified,true,2.7,0,1);var plane=new AnalyticSurface(SurfaceClass.Plane,new(){Y=-.1},new(){Y=1},z);
            Span<OperationIntersectionPoint> oldPoints=stackalloc OperationIntersectionPoint[32],newPoints=stackalloc OperationIntersectionPoint[32];Span<ScalarRootIsolation> oldRoots=stackalloc ScalarRootIsolation[32],newRoots=stackalloc ScalarRootIsolation[32];BufferCount a=0,b=0;
            SurfaceIntersectionPatch.UsePreparedSections=false;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(iso,plane,0,1,oldPoints,ref a,isolation:oldRoots,strictSeparation:true));
            SurfaceIntersectionPatch.UsePreparedSections=true;SurfaceIntersectionPatch.TestPreparedSectionBounds=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(iso,plane,0,1,newPoints,ref b,isolation:newRoots,strictSeparation:true));
            Assert.True(SurfaceIntersectionPatch.TestPreparedSectionBounds>0);Assert.Equal(1,a);Assert.Equal(a,b);Assert.InRange(2.0/3,newRoots[0].Root.Lower,newRoots[0].Root.Upper);
            for(var i=0;i<a;i++){Assert.Equal(oldPoints[i],newPoints[i]);Assert.Equal(oldRoots[i],newRoots[i]);}
        }
        finally{SurfaceIntersectionPatch.UsePreparedSections=true;}
    }
    private static void Equal(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b)
    {for(var i=0;i<9;i++){Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Lower),BitConverter.DoubleToInt64Bits(b[i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Upper),BitConverter.DoubleToInt64Bits(b[i].Upper));}}
}
