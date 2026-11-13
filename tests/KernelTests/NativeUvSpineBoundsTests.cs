using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NativeUvSpineBoundsTests
{
    [Theory]
    [InlineData(false,false,.125,-1,1)] [InlineData(false,true,8,-1,1)]
    [InlineData(true,false,.125,-1,1)] [InlineData(true,true,8,-1,1)]
    [InlineData(false,false,.125,-.125,.375)] [InlineData(false,true,8,-2,3)]
    [InlineData(true,false,.125,-.125,.375)] [InlineData(true,true,8,-2,3)]
    public void ReducedFootSystemEnclosesKnownNativeCurveAndDerivatives(bool curved,bool reverse,double scale,double nativeFirst,double nativeLast)
    {
        var program=Program(curved,reverse,scale,false,nativeFirst,nativeLast);Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        var cell=new SurfaceParameterBox(-.2,.2,0,0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,cell,bound));
        for(var i=0;i<=20;i++)
        {
            var t=cell.U0+(cell.U1-cell.U0)*i/20;
            var point=Map(t,curved?.2*t*t:-.2,curved?.3*t*t:.2,scale);
            var derivative=MapDirection(1,curved?.4*t:0,curved?.6*t:0,scale);
            Contains(bound,0,point);Contains(bound,3,derivative);
            Span<KernelVector3> numeric=stackalloc KernelVector3[2];
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(program,0,t,1,numeric));
            Contains(bound,0,numeric[0]);Contains(bound,3,numeric[1]);
        }
    }

    [Fact]
    public void EqualNativeOffsetSheetsCannotPublishAUniqueIntersectionTube()
    {
        var program=Program(true,false,1,true);Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(-.2,.2,0,0),bound));
    }

    private static NestedBlendProgram Program(bool curved,bool reverse,double scale,bool same,double nativeFirst=-1,double nativeLast=1)
    {
        const int stride=28;var scalars=new double[2*stride];var sources=new BlendSurfaceInstruction[2];
        for(var side=0;side<2;side++)
        {
            var offset=stride*side;var wall=side!=0&&!same;var height=wall?.2:.3;
            for(var u=0;u<3;u++)for(var v=0;v<2;v++)
            {
                var h=curved?(u==1?-height:height):0;
                var p=Map(u-1,wall?h:2*v-1,wall?2*v-1:h,scale);var at=offset+3*(2*u+v);
                scalars[at]=p.X;scalars[at+1]=p.Y;scalars[at+2]=p.Z;
            }
            double[] uk=[nativeFirst,nativeFirst,nativeFirst,nativeLast,nativeLast,nativeLast],vk=[-1,-1,1,1];uk.CopyTo(scalars,offset+18);vk.CopyTo(scalars,offset+24);
            sources[side]=new(){IsBSpline=true,SplineBasis=side,ScalarOffset=offset,UDegree=2,VDegree=1,UCount=3,VCount=2,Dimension=3,NormalSense=1,Offset=curved?0:-.2*scale};
        }
        var charts=new BlendChartInstruction[2];
        for(var i=0;i<2;i++)
        {
            var t=i==0?-.5:.5;var a=curved?.2*t*t:-.2;var b=curved?.3*t*t:.2;
            var u=nativeFirst+(nativeLast-nativeFirst)*(t+1)*.5;
            charts[i]=new(){Position=Map(t,a,b,scale),Parameter=t,Scale=1/scale,Chord=MapDirection(1,0,0,1),
                FirstU=u,FirstV=reverse?b:a,SecondU=u,SecondV=reverse?a:b};
        }
        BlendSpineInstruction[] spines=[new(){First=reverse?1:0,Second=reverse?0:1,ChartCount=2,StartType=LimitType.Artificial,EndType=LimitType.Artificial}];
        return new(sources,spines,charts,scalars,new double[2048]);
    }

    // A reflected rigid frame exercises native UV independently of world XYZ.
    private static KernelVector3 Map(double x,double y,double z,double scale)
    {var d=MapDirection(x,y,z,scale);return new(){X=d.X+.37,Y=d.Y-.29,Z=d.Z+.13};}
    private static KernelVector3 MapDirection(double x,double y,double z,double scale)
        =>new(){X=scale*(-.8*x+.6*y),Y=scale*(.6*x+.8*y),Z=scale*z};
    private static void Contains(ReadOnlySpan<IntervalNumber> bound,int offset,in KernelVector3 value)
    {Assert.InRange(value.X,bound[offset].Lower,bound[offset].Upper);Assert.InRange(value.Y,bound[offset+1].Lower,bound[offset+1].Upper);Assert.InRange(value.Z,bound[offset+2].Lower,bound[offset+2].Upper);}
}
