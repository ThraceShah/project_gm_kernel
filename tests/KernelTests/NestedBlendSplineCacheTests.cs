using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public class NestedBlendSplineCacheTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public unsafe void CompleteRollingBlendBoundsAreBitwiseUnchangedByTerminalJetReuse(bool curvedWall)
    {
        KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));
        try
        {
            var body=RegularSolidBlendTests.Roof(.4,curvedWall);var edge=RegularSolidBlendTests.SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
            Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
            Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));KernelRuntime.TryResolveBodySlot(body,out var slot);
            var face=KernelRuntime.Bodies[slot].FirstFaceBody;var surface=-1;
            for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
            {var sf=KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag);if(KernelRuntime.Surfaces[sf].Class==SurfaceClass.BlendSurface){surface=sf;break;}}
            Assert.True(surface>=0);var record=KernelRuntime.Surfaces[surface];var data=KernelRuntime.BlendedEdgeDataPool[record.DataIndex];
            CommandScratch.Bind(KernelRuntime.State.Session,0);
            try
            {
                var program=KernelRuntime.GetNestedBlendProgram(data);Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];
                var u=record.UMin+(record.UMax-record.UMin)*.47;var cell=new SurfaceParameterBox(u,u+.002*(record.UMax-record.UMin),.2,.8);
                JointNestedBlendEvaluation.ReuseSplineContactBounds=false;Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,cell,expected));
                JointNestedBlendEvaluation.ReuseSplineContactBounds=true;Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,cell,actual));
                for(var i=0;i<9;i++){Assert.Equal(expected[i].Lower,actual[i].Lower);Assert.Equal(expected[i].Upper,actual[i].Upper);}
            }
            finally{CommandScratch.Unbind(KernelRuntime.State.Session,0);}
        }
        finally{JointNestedBlendEvaluation.ReuseSplineContactBounds=true;KernelRuntime.SessionStop();}
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnseededCartesianJetsCanBeRecombinedWithDifferentDualSeedsAndSupportSenses(bool rational)
    {
        var program=Program(rational,out var source,out _);var u=new IntervalNumber(-.03,.04);var v=new IntervalNumber(.97,1.03);
        foreach(var (a,b) in new[]{(1.0,0.0),(0.0,1.0),(.37,-.83)})
        {
            var query=source;query.NormalSense=-1;query.Offset=.17;
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestSplineContactReuse(program,source,query,u,v,new(u,a),new(v,b),true,out var p,out var du,out var dv,out var hit));Assert.True(hit);
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestSplineContactReuse(program,source,query,u,v,new(u,a),new(v,b),false,out var expected,out var eu,out var ev,out _));
            Equal(expected,p);Equal(eu,du);Equal(ev,dv);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DifferentUnwrappedBoxesAndSplineBasisCannotUseTheCachedProof(bool rational)
    {
        var program=Program(rational,out var source,out var other);var u=new IntervalNumber(.2,.3);var v=new IntervalNumber(.6,.7);
        foreach(var change in new[]{0,1,2})
        {
            var query=change==2?other:source;var actualU=change==0?new IntervalNumber(.21,.31):u;var actualV=change==1?new IntervalNumber(.61,.71):v;
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestSplineContactReuse(program,source,query,u,v,new(actualU,.2),new(actualV,.7),true,out var p,out var du,out var dv,out var hit));Assert.False(hit);
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestSplineContactReuse(program,source,query,u,v,new(actualU,.2),new(actualV,.7),false,out var ep,out var eu,out var ev,out _));Equal(ep,p);Equal(eu,du);Equal(ev,dv);
        }
    }

    private static NestedBlendProgram Program(bool rational,out BlendSurfaceInstruction first,out BlendSurfaceInstruction second)
    {
        var dim=rational?4:3;var stride=9*dim+12;var data=new double[2*stride];
        for(var surface=0;surface<2;surface++)
        {
            var offset=surface*stride;
            for(var u=0;u<3;u++)for(var v=0;v<3;v++)
            {
                var w=rational?1+.15*u+.2*v:1;var at=offset+dim*(3*u+v);
                data[at]=(u*.5+surface*3)*w;data[at+1]=v*.5*w;data[at+2]=(.3*u*u-.1*v*v+.07*u*v+surface)*w;if(rational)data[at+3]=w;
            }
            double[] knots=[0,0,0,1,1,1];knots.CopyTo(data,offset+9*dim);knots.CopyTo(data,offset+9*dim+6);
        }
        first=new(){IsBSpline=true,NormalSense=1,UDegree=2,VDegree=2,UCount=3,VCount=3,Dimension=dim,Rational=rational};second=first;second.ScalarOffset=stride;
        return new(new[]{first,second},Array.Empty<BlendSpineInstruction>(),Array.Empty<BlendChartInstruction>(),data,new double[2048]);
    }
    private static void Equal(in IntervalVector expected,in IntervalVector actual)
    {Equal(expected.X,actual.X);Equal(expected.Y,actual.Y);Equal(expected.Z,actual.Z);}
    private static void Equal(in IntervalDual expected,in IntervalDual actual)
    {Assert.Equal(expected.Value.Lower,actual.Value.Lower);Assert.Equal(expected.Value.Upper,actual.Value.Upper);Assert.Equal(expected.Derivative.Lower,actual.Derivative.Lower);Assert.Equal(expected.Derivative.Upper,actual.Derivative.Upper);}
}
