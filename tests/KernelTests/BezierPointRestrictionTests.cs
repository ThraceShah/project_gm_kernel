using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace KernelTests;

public class BezierPointRestrictionTests
{
    [Theory]
    [InlineData(0,0)] [InlineData(0,16)] [InlineData(16,0)] [InlineData(1,1)]
    [InlineData(2,1)] [InlineData(1,2)] [InlineData(4,2)] [InlineData(16,16)]
    public void PointAndIsoparametricRestrictionsMatchOriginalBlossomsBitForBit(SplineDegree p,SplineDegree q)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],actual=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],expected=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var size=4*(p+1)*(q+1);
        for(var i=0;i<size;i++)poles[i]=i%4==3?new IntervalNumber(1+(i%17)*.125):new IntervalNumber(Math.Sin(i)-1e-13,Math.Sin(i)+1e-13);
        var patch=new BezierSurfacePatch(poles[..size],p,q,new(-1,2,.2,1.4));
        foreach(var box in new[]{new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(.2,.2,.4,.9),new SurfaceParameterBox(-.2,.4,.7,.7),new SurfaceParameterBox(-1.001,-1.001,1.401,1.401),new SurfaceParameterBox(-1,-1,.2,.2),new SurfaceParameterBox(-.2,.4,.4,.9)})
        {
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Restrict(patch,box,actual));OriginalRestriction(patch,box,expected);
            for(var i=0;i<size;i++)
            {Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Lower),BitConverter.DoubleToInt64Bits(actual[i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Upper),BitConverter.DoubleToInt64Bits(actual[i].Upper));}
        }
    }

    // Fixed original tensor restriction, with every blossom evaluated independently.
    // This checks arithmetic equivalence, including rounded normalization intervals.
    private static void OriginalRestriction(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        var p=patch.UDegree;var q=patch.VDegree;
        var u0=Normalize(box.U0,patch.Domain.U0,patch.Domain.U1);var u1=Normalize(box.U1,patch.Domain.U0,patch.Domain.U1);
        var v0=Normalize(box.V0,patch.Domain.V0,patch.Domain.V1);var v1=Normalize(box.V1,patch.Domain.V0,patch.Domain.V1);
        Span<IntervalNumber> layer=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],input=stackalloc IntervalNumber[68],result=stackalloc IntervalNumber[68];
        for(var v=0;v<=q;v++)
        {
            for(var u=0;u<=p;u++)for(var d=0;d<4;d++)input[4*u+d]=patch.Poles[4*(u*(q+1)+v)+d];
            OriginalCurve(input,p,u0,u1,result);for(var u=0;u<=p;u++)for(var d=0;d<4;d++)layer[4*(u*(q+1)+v)+d]=result[4*u+d];
        }
        for(var u=0;u<=p;u++){layer.Slice(4*u*(q+1),4*(q+1)).CopyTo(input);OriginalCurve(input,q,v0,v1,result);result[..(4*(q+1))].CopyTo(output[(4*u*(q+1))..]);}
    }

    private static void OriginalCurve(ReadOnlySpan<IntervalNumber> input,SplineDegree degree,IntervalNumber a,IntervalNumber b,Span<IntervalNumber> output)
    {
        Span<IntervalNumber> work=stackalloc IntervalNumber[68];
        for(var k=0;k<=degree;k++)
        {
            input[..(4*(degree+1))].CopyTo(work);
            for(var r=0;r<degree;r++){var t=r<degree-k?a:b;for(var j=0;j<degree-r;j++)for(var d=0;d<4;d++)work[4*j+d]=(1-t)*work[4*j+d]+t*work[4*(j+1)+d];}
            work[..4].CopyTo(output[(4*k)..]);
        }
    }

    private static IntervalNumber Normalize(double parameter,double first,double last)
        =>parameter==first?0:parameter==last?1:(new IntervalNumber(parameter)-first)/(new IntervalNumber(last)-first);
}
