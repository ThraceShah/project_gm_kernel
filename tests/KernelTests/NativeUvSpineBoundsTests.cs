using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

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
        Span<IntervalNumber> witness=stackalloc IntervalNumber[7],uv=stackalloc IntervalNumber[4];
        // An isolated source event is required inside the target branch's
        // contraction box. Two outer bounds of an entire parameter family
        // need not contain one another, even for the identical curve.
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,new(0,0,0,0),bound,capture:witness));
        var eventDomain=new SurfaceParameterBox(-.002,.002,0,0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,eventDomain,bound,require:witness));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,cell,bound));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineSupportUv(program,0,cell.U0,cell.U1,0,uv));
        for(var i=0;i<=20;i++)
        {
            var t=cell.U0+(cell.U1-cell.U0)*i/20;
            var point=Map(t,curved?.2*t*t:-.2,curved?.3*t*t:.2,scale);
            var derivative=MapDirection(1,curved?.4*t:0,curved?.6*t:0,scale);
            Contains(bound,0,point);Contains(bound,3,derivative);
            var nativeU=nativeFirst+(nativeLast-nativeFirst)*(t+1)*.5;
            Assert.InRange(nativeU,uv[0].Lower,uv[0].Upper);
            Assert.InRange((nativeLast-nativeFirst)*.5,uv[1].Lower,uv[1].Upper);
            Assert.InRange(reverse?(curved?.3*t*t:.2):(curved?.2*t*t:-.2),uv[2].Lower,uv[2].Upper);
            Span<KernelVector3> numeric=stackalloc KernelVector3[2];
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(program,0,t,1,numeric));
            Contains(bound,0,numeric[0]);Contains(bound,3,numeric[1]);
        }
        var position=witness[0];witness[0]+=scale;
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,eventDomain,bound,require:witness));
        witness[0]=position;witness[3]+=nativeLast-nativeFirst;
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,eventDomain,bound,require:witness));
    }

    [Fact]
    public void EqualNativeOffsetSheetsCannotPublishAUniqueIntersectionTube()
    {
        var program=Program(true,false,1,true);Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(-.2,.2,0,0),bound));
    }

    [Theory]
    [InlineData(false,false,.125,-.1)] [InlineData(false,true,8,.13)]
    [InlineData(true,false,8,-.1)] [InlineData(true,true,.125,.13)]
    [InlineData(true,false,1,0)] [InlineData(true,true,1,0)]
    public void OffsetFootDifferentialEnclosesBothNativeDirectionsIncludingLinearExtension(bool rational,bool outside,double scale,double offset)
    {
        var original=Program(true,false,scale,false,-2,3);var sources=original.Surfaces.ToArray();var scalars=original.Scalars.ToArray();
        if(rational)
        {
            scalars=new double[68];
            for(var side=0;side<2;side++)
            {
                var source=sources[side];var at=34*side;
                for(var u=0;u<3;u++)for(var v=0;v<2;v++)
                {
                    var weight=.8+.2*u+.1*v;var pole=2*u+v;
                    for(var axis=0;axis<3;axis++)scalars[at+4*pole+axis]=original.Scalars[source.ScalarOffset+3*pole+axis]*weight;
                    scalars[at+4*pole+3]=weight;
                }
                original.Scalars.Slice(source.ScalarOffset+18,10).CopyTo(scalars.AsSpan(at+24));
                source.ScalarOffset=at;source.Dimension=4;source.Rational=true;sources[side]=source;
            }
        }
        for(var side=0;side<2;side++){var source=sources[side];source.Offset=offset*scale;sources[side]=source;}
        var program=new NestedBlendProgram(sources,original.Spines,original.Charts,scalars,original.Workspace);
        var domain=outside?new SurfaceParameterBox(2.96,3.04,-.17,.19):new SurfaceParameterBox(.08,.12,-.17,.19);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[6];
        for(var side=0;side<2;side++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestNativeUvOffsetDerivatives(program,side,domain,bounds));
            for(var i=0;i<=4;i++)for(var j=0;j<=4;j++)
            {
                var u=domain.U0+(domain.U1-domain.U0)*i/4;var v=domain.V0+(domain.V1-domain.V0)*j/4;
                Assert.Equal(AlgorithmStatus.Success,NestedSplineEvaluation.Contact(program,sources[side],u,v,out var contact));
                var du=Add(contact.Du,Scale(contact.NormalU,sources[side].Offset));
                var dv=Add(contact.Dv,Scale(contact.NormalV,sources[side].Offset));
                Contains(bounds,0,du);Contains(bounds,3,dv);
            }
        }
    }

    [Theory]
    [InlineData(false,.125,-.125,.375)] [InlineData(true,8,-2,3)]
    public void AnalyticPlaneEliminationPreservesNativeSplineParameters(bool reverse,double scale,double nativeFirst,double nativeLast)
    {
        var program=MixedProgram(reverse,scale,nativeFirst,nativeLast,false);
        Span<IntervalNumber> bound=stackalloc IntervalNumber[9],uv=stackalloc IntervalNumber[4];var cell=new SurfaceParameterBox(-.2,.2,0,0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,cell,bound));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineSupportUv(program,0,cell.U0,cell.U1,reverse?1:0,uv));
        for(var i=0;i<=20;i++)
        {
            var t=cell.U0+(cell.U1-cell.U0)*i/20;
            Contains(bound,0,Map(t,0,.3*t*t,scale));Contains(bound,3,MapDirection(1,0,.6*t,scale));
            var u=nativeFirst+(nativeLast-nativeFirst)*(t+1)*.5;
            Assert.InRange(u,uv[0].Lower,uv[0].Upper);Assert.InRange(0,uv[2].Lower,uv[2].Upper);
        }
        // A tangent plane gives no regular two-dimensional implicit root;
        // keeping the same topology or seed cannot manufacture a tube.
        var tangent=MixedProgram(reverse,scale,nativeFirst,nativeLast,true);
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(tangent,0,cell,bound));
    }

    private static NestedBlendProgram MixedProgram(bool reverse,double scale,double nativeFirst,double nativeLast,bool tangent)
    {
        var basis=Program(true,false,scale,false,nativeFirst,nativeLast);var surfaces=basis.Surfaces.ToArray();
        surfaces[1]=new(){Analytic=new(SurfaceClass.Plane,Map(0,0,0,scale),MapDirection(0,tangent?0:1,tangent?1:0,1),MapDirection(1,0,0,1)),NormalSense=1};
        var charts=basis.Charts.ToArray();
        for(var i=0;i<charts.Length;i++)
        {
            var t=charts[i].Parameter;var u=nativeFirst+(nativeLast-nativeFirst)*(t+1)*.5;
            charts[i].Position=Map(t,0,.3*t*t,scale);
            charts[i].FirstU=reverse?0:u;charts[i].FirstV=0;charts[i].SecondU=reverse?u:0;charts[i].SecondV=0;
        }
        BlendSpineInstruction[] spines=[new(){First=reverse?1:0,Second=reverse?0:1,ChartCount=2,StartType=LimitType.Artificial,EndType=LimitType.Artificial}];
        return new(surfaces,spines,charts,basis.Scalars,basis.Workspace);
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
