using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
public class NativeScalarLevelTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void StoredAxisAndNonOrthogonalDirectionsKeepTheOriginalQuadratic(bool cylinder)
    {
        var center=new IntervalVector(new KernelVector3{X=2,Y=-1,Z=3});var x=new IntervalVector(new KernelVector3{X=1,Y=.5,Z=-.25});var y=new IntervalVector(new KernelVector3{X=.25,Y=1,Z=.5});
        var target=new AnalyticSurface(cylinder?SurfaceClass.Cylinder:SurfaceClass.Sphere,new(){X=.5,Y=.25,Z=-.5},new(){X=.5,Y=.5,Z=.5},new(){X=1},.5);
        var level=NativeScalarLevel.Create(center,x,y,1,.5,target,new(-2,3));Assert.True(level.Ready);Assert.True(level.TryBound(new(0),out var value,out var derivative));
        // At v=0 all inputs, P and P^T P, and this value/derivative are dyadic.
        decimal[] p=[2m+.5m-.5m,-1m+.25m-.25m,3m-.125m+.5m];decimal[] d=[.125m,.5m,.25m];
        if(cylinder){var sum=p.Sum()/4;var ds=d.Sum()/4;for(var i=0;i<3;i++){p[i]-=sum;d[i]-=ds;}}
        var expected=(double)(p.Sum(t=>t*t)-.25m);var expectedDerivative=(double)(2*p.Zip(d,(a,b)=>a*b).Sum());
        Assert.InRange(expected,value.Lower,value.Upper);Assert.InRange(expectedDerivative,derivative.Lower,derivative.Upper);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        foreach(var v in new[]{-1.2,0,.37,1.1,2.8})
        {
            var angle=new IntervalDual(new IntervalNumber(v),1);IntervalDual.SinCos(angle,out var sin,out var cos);var point=center+(x*cos+y*sin)*.5;
            bounds[0]=point.X.Value;bounds[1]=point.Y.Value;bounds[2]=point.Z.Value;bounds[3]=point.X.Derivative;bounds[4]=point.Y.Derivative;bounds[5]=point.Z.Derivative;
            Assert.True(level.TryBound(new(v),out value,out derivative));Overlap(AnalyticLevelSet.Value(bounds,target),value);Overlap(AnalyticLevelSet.Derivative(bounds,target,3),derivative);
        }
        Assert.False(level.TryBound(new(-3),out _,out _));
        var before=GC.GetAllocatedBytesForCurrentThread();var succeeded=level.TryBound(new(.1,.6),out _,out _);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.True(succeeded);Assert.Equal(0,allocated);
    }
    [Theory] [InlineData(1.5707963267948966,false)] [InlineData(1.5707963267948966,true)] [InlineData(.001,false)] [InlineData(0,false)]
    public void NativeRootProofAndUnresolvedContactsKeepTheirContracts(double angle,bool reverse)
    {
        var x=new KernelVector3{X=1};var z=new KernelVector3{Z=1};var last=new KernelVector3{X=Math.Cos(angle),Y=Math.Sin(angle)};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,last,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2*last.X,Y=-.2*last.Y},last,z),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1,ReverseSection=reverse}];
        BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Line,First=z},First=0,Second=1,ChartCount=3}];BlendChartInstruction[] charts=[new(){Parameter=2,Position=new(){Z=2},Scale=1},new(){Parameter=3,Position=new(){Z=3},Scale=1},new(){Parameter=4,Position=new(){Z=4},Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var source=new SurfaceIntersectionPatch(program,4,new(2,4,0,1));Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];Assert.Equal(AlgorithmStatus.Success,source.BoundCell(new(2.2,4.4,-.2,1.2),bounds,out var proven));
        var plane=new AnalyticSurface(SurfaceClass.Plane,new(){Y=-.1},new(){Y=1},z);var iso=new SurfaceIntersectionPatch(proven,true,3,0,1);var level=iso.SectionLevel(plane,0,1);Assert.Equal(angle==Math.PI/2,level.Ready);
        Assert.False(new SurfaceIntersectionPatch(source,true,3,0,1).SectionLevel(plane,0,1).Ready);Assert.False(iso.SectionLevel(plane,-1,2).Ready);Assert.False(new SurfaceIntersectionPatch(proven,false,.4,2.2,4.4).SectionLevel(plane,2.2,4.4).Ready);
        if(!level.Ready)return;
        Span<OperationIntersectionPoint> a=stackalloc OperationIntersectionPoint[32],b=stackalloc OperationIntersectionPoint[32];Span<ScalarRootIsolation> ca=stackalloc ScalarRootIsolation[32],cb=stackalloc ScalarRootIsolation[32];BufferCount na=0,nb=0;
        try
        {
            SurfaceIntersectionPatch.UseNativeScalarLevel=false;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(iso,plane,0,1,a,ref na,isolation:ca,strictSeparation:true));
            SurfaceIntersectionPatch.UseNativeScalarLevel=true;SurfaceIntersectionPatch.TestNativeScalarLevels=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(iso,plane,0,1,b,ref nb,isolation:cb,strictSeparation:true));
            Assert.True(SurfaceIntersectionPatch.TestNativeScalarLevels>0);Assert.Equal(1,na);Assert.Equal(na,nb);var expected=reverse?1.0/3:2.0/3;Assert.InRange(expected,cb[0].Root.Lower,cb[0].Root.Upper);Overlap(ca[0].Root,cb[0].Root);Assert.True(cb[0].Certified);Assert.Equal(ca[0].Increasing,cb[0].Increasing);
            // Identically zero and tangent-at-endpoint equations are not an empty intersection.
            var coincident=new AnalyticSurface(SurfaceClass.Plane,new(){Z=3},z,x);nb=0;Assert.Equal(AlgorithmStatus.NotConverged,SurfaceIsoPlaneRoots.Find(iso,coincident,0,1,b,ref nb,isolation:cb,strictSeparation:true));
            var tangent=new AnalyticSurface(SurfaceClass.Plane,new(){Y=-.2},new(){Y=1},z);nb=0;Assert.Equal(AlgorithmStatus.NotConverged,SurfaceIsoPlaneRoots.Find(iso,tangent,0,1,b,ref nb,isolation:cb,strictSeparation:true));
        }
        finally{SurfaceIntersectionPatch.UseNativeScalarLevel=true;}
    }
    private static void Overlap(IntervalNumber a,IntervalNumber b)=>Assert.True(Math.Max(a.Lower,b.Lower)<=Math.Min(a.Upper,b.Upper),$"{a.Lower:R}..{a.Upper:R} versus {b.Lower:R}..{b.Upper:R}");
}
