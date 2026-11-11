using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class ConicConstraintSubstitutionTests
{
    [Theory]
    [InlineData(false,false,false)] [InlineData(false,true,false)]
    [InlineData(true,false,false)] [InlineData(true,true,false)]
    [InlineData(false,false,true)] [InlineData(true,true,true)]
    public void DirectColumnsOverlapFullDirectionalEnclosures(bool ellipse,bool reversed,bool cylinder)
    {
        var program=Program(ellipse,reversed,cylinder);Span<IntervalNumber> matrix=stackalloc IntervalNumber[49],directional=stackalloc IntervalNumber[49];
        IntervalNumber[] boxes=[new(1.4,1.5),new(.04,.06),new(.02,.03),new(.9,1),new(.01,.03),new(-.01,.01),new(.01,.02)];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,boxes,matrix,directional));
        for(var i=0;i<49;i++)Assert.True((matrix[i]-directional[i]).ContainsZero,$"row={i/7} column={i%7}");
        var before=GC.GetAllocatedBytesForCurrentThread();
        var status=JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,boxes,matrix,directional);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void CircleTubeKeepsExactNativePositionsDerivativesAndSharedWitnessContract(bool reversed,bool cylinder)
    {
        var program=Program(false,reversed,false,boundCylinder:cylinder);var domain=new SurfaceParameterBox(.01,.011,0,0);Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var radius=cylinder?1.2:1.5;var height=cylinder?Math.Sqrt(.25-(radius-1)*(radius-1)):0;
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,1,domain,bounds));
        foreach(var t in new[]{domain.U0,(domain.U0+domain.U1)*.5,domain.U1})
        {
            var x=Math.Sqrt(radius*radius-t*t);Assert.InRange(x,bounds[0].Lower,bounds[0].Upper);Assert.InRange(t,bounds[1].Lower,bounds[1].Upper);Assert.InRange(height,bounds[2].Lower,bounds[2].Upper);
            Assert.InRange(-t/x,bounds[3].Lower,bounds[3].Upper);Assert.InRange(1,bounds[4].Lower,bounds[4].Upper);Assert.True(bounds[5].ContainsZero);
        }
        Span<IntervalNumber> witness=stackalloc IntervalNumber[7];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,1,domain,bounds,witness));
        witness[6]=new(10,11);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,1,domain,bounds,default,witness));
    }

    [Fact]
    public void DegenerateConicCannotPublishAValidJacobian()
    {
        var program=Program(false,false,false,degenerate:true);Span<IntervalNumber> matrix=stackalloc IntervalNumber[49],directional=stackalloc IntervalNumber[49];
        IntervalNumber[] boxes=[new(1.4,1.5),new(.04,.06),new(.02,.03),new(.9,1),new(.01,.03),new(-.01,.01),new(.01,.02)];
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,boxes,matrix,directional));
    }

    private static NestedBlendProgram Program(bool ellipse,bool reversed,bool cylinder,bool degenerate=false,bool boundCylinder=false)
    {
        BlendSurfaceInstruction[] surfaces=[
            new(){IsBlend=true,Spine=0,First=1,Second=2,Range0=.5,Range1=.5,NormalSense=1},
            new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},
            new(){Analytic=cylinder?new(SurfaceClass.Cylinder,default,new(){X=.6,Y=.8},new(){Z=1},.7):new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=-1}];
        if(boundCylinder)surfaces[3]=new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1.2),NormalSense=-1};
        BlendSpineInstruction[] spines=[new(){First=1,Second=2,Curve=new(){Kind=ellipse?CurveClass.Ellipse:CurveClass.Circle,First=degenerate?default:new(){X=1},Second=degenerate?default:new(){Y=ellipse?.75:1}},ChartCount=2},
            new(){First=reversed?3:0,Second=reversed?0:3,ChartOffset=2,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=new(){X=1},Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=ellipse?.75:1},Parameter=Math.PI/2,Scale=1},
            new(){Position=new(){X=1.5},Chord=new(){Y=1},Scale=1},new(){Position=new(){X=Math.Sqrt(2.25-.15*.15),Y=.15},Parameter=.15,Scale=1}];
        if(boundCylinder)
        {
            var height=Math.Sqrt(.25-(1.2-1)*(1.2-1));charts[2].Position=new(){X=1.2,Z=height};
            charts[3].Position=new(){X=Math.Sqrt(1.2*1.2-.15*.15),Y=.15,Z=height};
        }
        return new(surfaces,spines,charts);
    }
}
