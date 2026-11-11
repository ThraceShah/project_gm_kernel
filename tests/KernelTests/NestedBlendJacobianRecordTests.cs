global using BlendSpineIndex=System.Int32;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public class NestedBlendJacobianRecordTests
{
    [Fact]
    public void RecordedArithmeticRetainsDirectionalEnclosuresAndSparseHighBitSeeds()
    {
        Span<IntervalExpressionNode> record=stackalloc IntervalExpressionNode[512];
        Span<IntervalNumber> scratch=stackalloc IntervalNumber[512],matrix=stackalloc IntervalNumber[63];
        Span<IntervalNumber> boxes=stackalloc IntervalNumber[63];
        Span<BufferCount> count=stackalloc BufferCount[1];Span<BufferOffset> outputs=stackalloc BufferOffset[1];
        foreach(var width in new[]{0.0,1e-12,.03,.7})
        {
            MemoryMarshal.AsBytes(record).Fill(0xA5);count[0]=0;
            var a=new IntervalNumber(1.2-width,1.2+width);var b=new IntervalNumber(2.3-width,2.3+width);
            for(var i=0;i<63;i++){boxes[i]=i==62?b:a;IntervalExpression.Variable(record,count,boxes[i],i);}
            var x=IntervalExpression.FromNode(record,count,0);var y=IntervalExpression.FromNode(record,count,62);
            IntervalExpression.SinCos(x,out var sin,out var cos);
            var expression=IntervalExpression.Sqrt(IntervalExpression.Square(x)+IntervalExpression.Square(y))+(sin-cos*x)/(y+x*x);
            Assert.True(expression.Finite);outputs[0]=expression.Attach(record,count).OutputIndex();
            Assert.InRange(IntervalExpression.PrepareDerivatives(record[..count[0]]),1,scratch.Length);
            Assert.Equal(AlgorithmStatus.Success,IntervalExpression.Differentiate(record[..count[0]],outputs,63,scratch,matrix));
            Assert.Equal(AlgorithmStatus.Success,IntervalExpression.Rebind(record[..count[0]],boxes));
            var allocated=GC.GetAllocatedBytesForCurrentThread();var status=AlgorithmStatus.NotRun;
            for(var iteration=0;iteration<16;iteration++)
            {
                status=IntervalExpression.Rebind(record[..count[0]],boxes);
                if(status==AlgorithmStatus.Success)status=IntervalExpression.Differentiate(record[..count[0]],outputs,63,scratch,matrix);
            }
            var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,bytes);
            for(var column=0;column<63;column++)
            {
                var u=new IntervalDual(a,column==0?1:0);var v=new IntervalDual(b,column==62?1:0);
                IntervalDual.SinCos(u,out var s,out var c);
                var expected=IntervalDual.Sqrt(IntervalDual.Square(u)+IntervalDual.Square(v))+(s-c*u)/(v+u*u);
                Equal(expected.Value,expression.Value);Equal(expected.Derivative,matrix[column]);
            }
        }
    }

    [Fact]
    public void FiniteChecksRejectAnIntermediateSingularityEvenIfLaterArithmeticMasksIt()
    {
        Span<IntervalExpressionNode> record=stackalloc IntervalExpressionNode[64];Span<BufferCount> count=stackalloc BufferCount[1];count[0]=0;
        var x=IntervalExpression.Variable(record,count,new(-1,1),0);var invalid=1/x;
        Assert.False(invalid.Finite);
        var masked=invalid*0;Assert.True(masked.Finite);
        Span<BufferOffset> output=stackalloc BufferOffset[1]{masked.OutputIndex()};
        Span<IntervalNumber> scratch=stackalloc IntervalNumber[64],matrix=stackalloc IntervalNumber[1];
        IntervalExpression.PrepareDerivatives(record[..count[0]]);
        Assert.Equal(AlgorithmStatus.NotConverged,IntervalExpression.Differentiate(record[..count[0]],output,1,scratch,matrix));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteSplineAndOffsetResidualRetainsEveryJacobianEntry(bool rational)
    {
        var surfaces=NestedBlendSeriesTests.Surfaces();var charts=NestedBlendSeriesTests.Charts();var dimension=rational?4:3;var scalars=new double[4*dimension+8];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            var i=(2*u+v)*dimension;var weight=rational?2:1;scalars[i+1]=u*weight;scalars[i+2]=(2*v-1)*weight;if(rational)scalars[i+3]=weight;
        }
        double[] knots=[0,0,1,1,-1,-1,1,1];knots.CopyTo(scalars,4*dimension);
        surfaces[0]=new(){IsBSpline=true,SplineBasis=0,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=dimension,Rational=rational,NormalSense=1};
        surfaces[2]=surfaces[0];surfaces[2].Offset=-.2;charts[1].FirstU=1;
        var program=new NestedBlendProgram(surfaces,NestedBlendSeriesTests.Spines(),charts,scalars,new double[8192]);
        Check(program,0,.37,[0,1e-10,.001,.03]);
    }

    [Theory]
    [InlineData((int)SurfaceClass.Sphere)] [InlineData((int)SurfaceClass.Cylinder)]
    [InlineData((int)SurfaceClass.Cone)] [InlineData((int)SurfaceClass.Torus)]
    public void AnalyticSupportsPreserveAllDirectionsAndSingularFailure(int kindValue)
    {
        var kind=(SurfaceClass)kindValue;
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};
        var radius=kind==SurfaceClass.Torus?2:1;var secondary=kind==SurfaceClass.Torus?.3:kind==SurfaceClass.Cone?.2:0;
        var position=new KernelVector3{X=kind==SurfaceClass.Torus?radius+secondary:radius};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(kind,default,z,x,radius,secondary),NormalSense=-1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=position,Chord=new(){Y=1},Scale=1},new(){Position=new(){X=position.X,Y=.1},Parameter=.1,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);
        Check(program,0,0,[0,1e-10,.01,4]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedNativeOccurrencesRebindChildCoordinatesAndParametersWithoutChangingTheJacobian(bool explicitChild)
    {
        var surfaces=NestedBlendSeriesTests.Surfaces().Concat(new[]{new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Z=-.1},new(){Z=1},new(){X=1}),NormalSense=1}}).ToArray();
        var spines=new[]{NestedBlendSeriesTests.Spines()[0],new BlendSpineInstruction{First=4,Second=5,ChartOffset=2,ChartCount=2}};
        if(explicitChild)spines[0].Curve=new(){Kind=CurveClass.Line,First=new(){Y=1}};
        var x=-Math.Sqrt(.03);var charts=NestedBlendSeriesTests.Charts().Concat(new[]{new BlendChartInstruction{Position=new(){X=x,Z=-.1},Chord=new(){Y=1},Scale=1},new BlendChartInstruction{Position=new(){X=x,Y=1,Z=-.1},Parameter=1,Scale=1}}).ToArray();
        var program=new NestedBlendProgram(surfaces,spines,charts);
        Check(program,1,.37,[0,1e-10,.002,.15,1]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public unsafe void RuntimeRollingBlendAndFreshCommandWorkspaceKeepIdenticalJacobians(bool curvedWall)
    {
        KernelRuntime.SessionStop();var start=new ProjectGmKernel.Native.Generated.PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));
        try
        {
            var body=RegularSolidBlendTests.Roof(.4,curvedWall);var edge=RegularSolidBlendTests.SelectedEdge(body);
            var set=new ProjectGmKernel.Native.Generated.PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
            Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
            var fix=new ProjectGmKernel.Native.Generated.PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
            Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));KernelRuntime.TryResolveBodySlot(body,out var slot);
            var face=KernelRuntime.Bodies[slot].FirstFaceBody;
            for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
            {
                var sf=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)];if(sf.Class!=SurfaceClass.BlendSurface)continue;
                var data=KernelRuntime.BlendedEdgeDataPool[sf.DataIndex];CommandScratch.Bind(KernelRuntime.State.Session,0);
                try
                {
                    var program=KernelRuntime.GetNestedBlendProgram(data);var spine=program.Surfaces[data.ProgramRoot].Spine;
                    Check(program,spine,sf.UMin+(sf.UMax-sf.UMin)*.47,[0,1e-10,.001]);
                }
                finally{CommandScratch.Unbind(KernelRuntime.State.Session,0);}
                return;
            }
            Assert.Fail("No native rolling blend surface");
        }
        finally{KernelRuntime.SessionStop();}
    }

    private static void Check(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,double[] widths)
    {
        Span<IntervalNumber> expected=stackalloc IntervalNumber[63*63],actual=stackalloc IntervalNumber[63*63];
        foreach(var width in widths)
        {
            MemoryMarshal.AsBytes(actual).Fill(0xA5);
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestIntervalJacobianRecord(program,spine,parameter,width,expected,actual,out var n,out var a,out var b));
            Assert.Equal(a,b);
            if(a==AlgorithmStatus.Success)for(var i=0;i<n*n;i++)Equal(expected[i],actual[i]);
        }
    }
    private static void Equal(IntervalNumber a,IntervalNumber b)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Lower),BitConverter.DoubleToInt64Bits(b.Lower));
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Upper),BitConverter.DoubleToInt64Bits(b.Upper));
    }
}
