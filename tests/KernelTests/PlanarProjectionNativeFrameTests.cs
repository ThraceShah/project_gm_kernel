using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public class PlanarProjectionNativeFrameTests
{
    [Theory]
    [InlineData(false,.125)] [InlineData(true,.125)]
    [InlineData(false,1.7)] [InlineData(true,1.7)]
    [InlineData(false,8.0)] [InlineData(true,8.0)]
    public void PlaneFunctionalsAndCoupledWallBracketsKeepTheirNativeCorrelation(bool rotated,double scale)
    {
        var x=Direction(new(){X=1},rotated);var y=Direction(new(){Y=1},rotated);var z=Direction(new(){Z=1},rotated);
        var origin=new KernelVector3{X=3,Y=5,Z=7};var radius=.2*scale;
        Span<BlendSurfaceInstruction> surfaces=stackalloc BlendSurfaceInstruction[3];
        surfaces[0]=new(){Analytic=new(SurfaceClass.Plane,Add(origin,Scale(x,-radius)),x,y),NormalSense=1};
        surfaces[1]=new(){Analytic=new(SurfaceClass.Plane,Add(origin,Scale(z,-radius)),z,x),NormalSense=1};
        surfaces[2]=new(){IsBlend=true,First=0,Second=1,Spine=0,Range0=radius,Range1=radius,NormalSense=1};
        Span<BlendSpineInstruction> spines=stackalloc BlendSpineInstruction[1];
        spines[0]=new(){Curve=new(){Kind=CurveClass.Line,Origin=origin,First=y},ChartCount=2};
        Span<BlendChartInstruction> charts=stackalloc BlendChartInstruction[2];charts[0]=new(){Parameter=0};charts[1]=new(){Parameter=scale};
        var program=new NestedBlendProgram(surfaces,spines,charts);var projection=surfaces[1].Analytic;
        var patch=new SurfaceParameterBox(.3*scale,.3001*scale,0,.0001);var tolerance=5e-9*scale;
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyPlanarProjection(program,2,patch,projection,tolerance,out var near,out _));Assert.True(near);
        var far=new SurfaceParameterBox(patch.U0,patch.U1,.1,.1001);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyPlanarProjection(program,2,far,projection,tolerance,out near,out _));Assert.False(near);
        var cu=.30005*scale;const double cv=.00005;
        var corner=Add(origin,Add(Scale(x,-radius*Math.Sin(cv*Math.PI*.5)),Add(Scale(y,cu),Scale(z,-radius))));
        var a=new AnalyticSurface(SurfaceClass.Plane,corner,Scale(Add(x,y),1/Math.Sqrt(2)),z);
        var b=new AnalyticSurface(SurfaceClass.Plane,corner,Scale(Add(x,Scale(y,2)),1/Math.Sqrt(5)),z);
        var box=new SurfaceParameterBox(cu-.00004*scale,cu+.00004*scale,cv-.00001,cv+.00001);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,2,box,projection,a,b));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,2,box,projection,b,a));
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,2,box,projection,a,a));
        var outside=new AnalyticSurface(SurfaceClass.Plane,Add(corner,Scale(y,scale)),b.Axis,b.X);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,2,box,projection,a,outside));
        var before=GC.GetAllocatedBytesForCurrentThread();
        var status=JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,2,box,projection,a,b);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }
    private static KernelVector3 Direction(KernelVector3 p,bool rotated)=>rotated?new(){X=p.X,Y=.6*p.Y-.8*p.Z,Z=.8*p.Y+.6*p.Z}:p;
}
