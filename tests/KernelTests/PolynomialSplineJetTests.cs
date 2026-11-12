using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class PolynomialSplineJetTests
{
    [Fact]
    public void NonzeroMixedHigherDerivativesKeepTheNativeTensorLinearExtension()
    {
        var node=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=4,VDegree=2,UCount=5,VCount=3,Dimension=3};
        var scalars=new double[61];for(var u=0;u<5;u++)for(var v=0;v<3;v++){var at=3*(3*u+v);scalars[at]=u/4.0;scalars[at+1]=v/2.0;scalars[at+2]=u==4&&v==2?1:0;}
        double[] knots=[0,0,0,0,0,1,1,1,1,1,0,0,0,1,1,1];knots.CopyTo(scalars,45);
        var program=new NestedBlendProgram([node],[],[],scalars,new double[2048]);Span<IntervalNumber> hull=stackalloc IntervalNumber[18];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.IntervalSplineBounds(program,node,new(1.125),new(1.25),hull));
        double[] expected=[1.125,1.25,2.25,1,0,6,0,1,3,0,0,0,0,0,8,0,0,0];
        for(var i=0;i<18;i++)Assert.InRange(expected[i],hull[i].Lower,hull[i].Upper);
    }

    [Fact]
    public void PolynomialMomentsAndAllRectangularDerivativesAreEnclosedWithoutAnArtificialWeight()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[60],bounds=stackalloc IntervalNumber[27];
        for(var u=0;u<5;u++)for(var v=0;v<3;v++)
        {
            var at=4*(3*u+v);poles[at]=u*.25+v*.125;poles[at+1]=v*.5-u*.0625;poles[at+2]=.125*u*u-.25*v*v+.0625*u*v;
            // A polynomial source fixes W=1 mathematically. Its unused
            // extracted homogeneous channel may carry arithmetic enclosure.
            poles[at+3]=new(.99,1.01);
        }
        var patch=new BezierSurfacePatch(poles,4,2,new(-2,2,.5,2.5));
        foreach(var box in new[]{new SurfaceParameterBox(-.7,.4,.8,1.2),new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(-2,-1.9,2.4,2.5)})
        {
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundPolynomialRectangularSecond(patch,box,bounds));
            for(var i=0;i<=4;i++)for(var j=0;j<=4;j++)for(var du=0;du<=2;du++)for(var dv=0;dv<=2;dv++)
            {
                var x=box.U0+(box.U1-box.U0)*i/4;var y=box.V0+(box.V1-box.V0)*j/4;var s=(x+2)/4;var t=(y-.5)/2;
                var expected=(du,dv) switch
                {
                    (0,0)=>new KernelVector3{X=s+.25*t,Y=t-.25*s,Z=.5*s+1.5*s*s-.5*t-.5*t*t+.5*s*t},
                    (1,0)=>new KernelVector3{X=.25,Y=-.0625,Z=(.5+3*s+.5*t)/4},
                    (0,1)=>new KernelVector3{X=.125,Y=.5,Z=(-.5-t+.5*s)/2},
                    (2,0)=>new KernelVector3{Z=.1875},(1,1)=>new KernelVector3{Z=.0625},(0,2)=>new KernelVector3{Z=-.25},_=>default,
                };
                var at=3*(3*du+dv);Assert.InRange(expected.X,bounds[at].Lower,bounds[at].Upper);Assert.InRange(expected.Y,bounds[at+1].Lower,bounds[at+1].Upper);Assert.InRange(expected.Z,bounds[at+2].Lower,bounds[at+2].Upper);
            }
        }
        var warmBox=new SurfaceParameterBox(-.7,.4,.8,1.2);var before=GC.GetAllocatedBytesForCurrentThread();var status=BezierSurfaceBounds.BoundPolynomialRectangularSecond(patch,warmBox,bounds);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,BezierSurfaceBounds.BoundPolynomialRectangularSecond(patch,warmBox,bounds[..26]));
    }
}
