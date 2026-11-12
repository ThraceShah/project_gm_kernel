using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NativeSupportParameterBoundsTests
{
    [Theory]
    [InlineData(false,false,false)] [InlineData(true,false,false)]
    [InlineData(false,true,false)] [InlineData(true,true,false)]
    [InlineData(false,false,true)] [InlineData(true,false,true)]
    [InlineData(false,true,true)] [InlineData(true,true,true)]
    public void NativeUvAndDerivativeRetainTheSupportingSplineGaugeAndOrder(bool rational,bool shifted,bool reverse)
    {
        var surfaces=NestedBlendBoundsInitializationContractTests.SplineSurfaces();var charts=NestedBlendSeriesTests.Charts();var spines=NestedBlendSeriesTests.Spines();
        var u0=shifted?2:0;var u1=shifted?5:1;var v0=shifted?-3:-1;var v1=shifted?-1:1;var dimension=rational?4:3;
        var scalars=new double[4*dimension+8];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {var at=(2*u+v)*dimension;var w=rational?2:1;scalars[at+1]=u*2*w;scalars[at+2]=(2*v-1)*2*w;if(rational)scalars[at+3]=w;}
        new double[]{u0,u0,u1,u1,v0,v0,v1,v1}.CopyTo(scalars,4*dimension);
        surfaces[0].Dimension=dimension;surfaces[0].Rational=rational;
        surfaces[2]=surfaces[0];surfaces[2].Offset=-.2;
        if(reverse)(spines[0].First,spines[0].Second)=(spines[0].Second,spines[0].First);
        for(var i=0;i<charts.Length;i++)
        {if(reverse){charts[i].SecondU=u0+(u1-u0)*i*.5;charts[i].SecondV=(v0+v1)*.5;}else{charts[i].FirstU=u0+(u1-u0)*i*.5;charts[i].FirstV=(v0+v1)*.5;}}
        var program=new NestedBlendProgram(surfaces,spines,charts,scalars,new double[8192]);var side=reverse?1:0;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[4],values=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineSupportUv(program,0,.1,.9,side,bounds));
        Contains(bounds[0],u0+(u1-u0)*.05);Contains(bounds[0],u0+(u1-u0)*.45);Contains(bounds[1],(u1-u0)*.5);
        Contains(bounds[2],(v0+v1)*.5);Contains(bounds[3],0);
        var coordinate=new SurfaceIntersectionPatch(program,0,side,.1,.9,true);
        Assert.Equal(AlgorithmStatus.Success,coordinate.Bound(new(.1,.9,0,0),values));
        Assert.Equal(bounds[0],values[0]);Assert.Equal(bounds[1],values[3]);Assert.Equal(bounds[2],values[1]);Assert.Equal(bounds[3],values[4]);
        Assert.Equal(AlgorithmStatus.Success,coordinate.Estimate(.375,0,values));Contains(values[0],u0+(u1-u0)*.1875);Contains(values[1],(v0+v1)*.5);
        var level=new AnalyticSurface(SurfaceClass.Plane,new(){X=u0+(u1-u0)*.1875},new(){X=1},new(){Y=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[4];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(coordinate,level,.1,.9,roots,ref count,0,true,proof,use:RootIsolationUse.NativeSupportCoordinate));
        Assert.Equal(1,count);Assert.True(proof[0].Certified);Contains(proof[0].Root,.375);
        level=new(SurfaceClass.Plane,new(){X=u0+2*(u1-u0)},new(){X=1},new(){Y=1});count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(coordinate,level,.1,.9,roots,ref count,0,true,proof,use:RootIsolationUse.NativeSupportCoordinate));Assert.Equal(0,count);
        Assert.Equal(AlgorithmStatus.Unsupported,new SurfaceIntersectionPatch(coordinate,.1).Bound(new(.2,.3,0,0),values));
        if(!rational&&!shifted&&!reverse)
        {
            var before=GC.GetAllocatedBytesForCurrentThread();var success=true;
            for(var i=0;i<8;i++)success&=coordinate.Bound(new(.1,.9,0,0),values)==AlgorithmStatus.Success;
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.True(success);Assert.Equal(0,bytes);
        }
    }

    private static void Contains(IntervalNumber box,double value)=>Assert.InRange(value,box.Lower,box.Upper);
}
