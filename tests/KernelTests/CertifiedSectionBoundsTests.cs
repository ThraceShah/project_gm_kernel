using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
public class CertifiedSectionBoundsTests
{
    [Theory] [InlineData(1.5707963267948966,false)] [InlineData(.001,false)] [InlineData(0,false)] [InlineData(-1.0471975511965976,false)] [InlineData(1.5707963267948966,true)]
    public void ParentProofRetainsNativePositionsFreeDerivativesAndCoverage(double angle,bool reverse)
    {
        var x=new KernelVector3{X=1};var z=new KernelVector3{Z=1};var last=new KernelVector3{X=Math.Cos(angle),Y=Math.Sin(angle)};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,last,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2*last.X,Y=-.2*last.Y},last,z),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1,ReverseSection=reverse}];
        BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Line,First=z},First=0,Second=1,ChartCount=3}];BlendChartInstruction[] charts=[new(){Parameter=2,Position=new(){Z=2},Scale=1},new(){Parameter=3,Position=new(){Z=3},Scale=1},new(){Parameter=4,Position=new(){Z=4},Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var patch=new SurfaceIntersectionPatch(program,4,new(2,4,0,1));Span<IntervalNumber> parent=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9],original=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,patch.BoundCell(new(2.2,3.8,.1,.9),parent,out var certified));SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
        try
        {
            foreach(var constantU in new[]{false,true})
            {
                var at=constantU?2.7:.4;var lo=constantU?.2:2.4;var hi=constantU?.8:3.4;var curve=new SurfaceIntersectionPatch(certified,constantU,at,lo,hi);
                SurfaceIntersectionPatch.UseCertifiedSectionBounds=false;Assert.Equal(AlgorithmStatus.Success,curve.Bound(new(lo,hi,0,0),original));SurfaceIntersectionPatch.UseCertifiedSectionBounds=true;SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;
                var before=GC.GetAllocatedBytesForCurrentThread();var status=curve.Bound(new(lo,hi,0,0),actual);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);Assert.Equal(1,SurfaceIntersectionPatch.TestCertifiedSectionBounds);
                for(var i=0;i<3;i++){Assert.Equal(BitConverter.DoubleToInt64Bits(original[3+i].Lower),BitConverter.DoubleToInt64Bits(actual[3+i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(original[3+i].Upper),BitConverter.DoubleToInt64Bits(actual[3+i].Upper));Assert.Equal(0,actual[6+i].Lower);Assert.Equal(0,actual[6+i].Upper);}
                for(var i=0;i<=10;i++)
                {
                    var t=lo+(hi-lo)*i/10;Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(program,4,constantU?at:t,constantU?t:at,layout,jet));var d=jet[layout.GetIndex(constantU?0:1,constantU?1:0)];
                    foreach(var value in new[]{(jet[0].X,actual[0]),(jet[0].Y,actual[1]),(jet[0].Z,actual[2]),(d.X,actual[3]),(d.Y,actual[4]),(d.Z,actual[5])})Assert.InRange(value.Item1,value.Item2.Lower,value.Item2.Upper);
                }
                // A covered source does not authorize a wider free domain.
                SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;var outside=new SurfaceIntersectionPatch(certified,constantU,at,lo-1,hi+1);outside.Bound(new(lo-1,hi+1,0,0),actual);Assert.Equal(0,SurfaceIntersectionPatch.TestCertifiedSectionBounds);
            }
            SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;var unproved=new SurfaceIntersectionPatch(patch,true,2.7,.2,.8);Assert.Equal(AlgorithmStatus.Success,unproved.Bound(new(.2,.8,0,0),actual));Assert.Equal(0,SurfaceIntersectionPatch.TestCertifiedSectionBounds);
            spines[0].Curve=default;Assert.False(JointNestedBlendEvaluation.CanBoundExplicitSection(program,4));
        }
        finally{SurfaceIntersectionPatch.UseCertifiedSectionBounds=true;}
    }
}
