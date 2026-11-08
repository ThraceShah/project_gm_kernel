using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ExplicitSpineTests:IDisposable
{
    public ExplicitSpineTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void PositiveRationalSpineUsesNativeKnotsAndCertifiesTheWholeArc()
    {
        var w=Math.Sqrt(.5);
        Span<double> poles=stackalloc double[20]{1,0,0,1,w,w,0,w,0,1,0,1,-w,w,0,w,-1,0,0,1};
        Span<double> knots=stackalloc double[8]{0,0,0,.4,.4,1,1,1};
        var spine=new BCurveView(2,4,true,false,poles,knots);
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1);
        Assert.Equal(2,RationalSpine.SpanCount(spine));
        Assert.Equal(AlgorithmStatus.Success,RationalSpine.CertifyRegular(spine,2));
        Assert.Equal(AlgorithmStatus.Success,RationalSpine.Certify(spine,cylinder));
        Assert.Equal(AlgorithmStatus.Success,RationalSpine.Series(spine,TaylorJet.Variable(.2,2),out var point,out var tangent));
        Assert.InRange(Math.Abs(point.X[0]-w)+Math.Abs(point.Y[0]-w),0,1e-14);
        Assert.InRange(Math.Abs(point.X[1]-tangent.X[0])+Math.Abs(point.Y[2]*2-tangent.Y[1]),0,1e-13);
        Assert.Equal(AlgorithmStatus.Success,RationalSpine.Bound(spine,new(new(.19,.21),1),out var box,out var derivative));
        Assert.InRange(point.X[0],box.X.Value.Lower,box.X.Value.Upper);
        Assert.InRange(point.X[1],box.X.Derivative.Lower,box.X.Derivative.Upper);
        Assert.InRange(point.Y[2]*2,derivative.Y.Derivative.Lower,derivative.Y.Derivative.Upper);
        Assert.Equal(AlgorithmStatus.NotConverged,RationalSpine.Bound(spine,new(new(.39,.41),1),out _,out _));
        poles[4]+=1e-5; // Same finite end points, different interior carrier.
        Assert.Equal(AlgorithmStatus.InvalidInput,RationalSpine.Certify(spine,cylinder));
    }

    [Fact]
    public void RationalConicGaugeMapsBothFiniteSpansWithoutAnXyzInverse()
    {
        var w=Math.Sqrt(.5);
        Span<double> poles=stackalloc double[20]{1,0,0,1,w,w,0,w,0,1,0,1,-w,w,0,w,-1,0,0,1};
        Span<double> knots=stackalloc double[8]{0,0,0,.4,.4,1,1,1};
        var spine=new BCurveView(2,4,true,false,poles,knots);
        var conic=new ParametricSpineCurve{Kind=CurveClass.Circle,First=new(){X=1},Second=new(){Y=1}};
        Span<RationalConicArc> arcs=stackalloc RationalConicArc[2];
        Span<IntervalNumber> x=stackalloc IntervalNumber[8],y=stackalloc IntervalNumber[8];
        Assert.True(RationalConicGauge.Certify(spine,conic,1e-10,arcs,x,y,out var count));Assert.Equal(2,count);
        Assert.True(RationalConicGauge.NativeAngleLift(arcs,Math.Tau+.1,Math.Tau+3,out var lift));
        for(var i=0;i<2;i++)
        {
            var angle=new IntervalNumber(Math.Tau+(i==0?Math.PI/4:3*Math.PI/4))+lift;
            Assert.True(RationalConicGauge.LocateStation(2,arcs,x,y,angle,out var station,out var slope,out var interior));
            Assert.True(interior&&slope>0&&station.Upper-station.Lower<1e-12);
            Assert.InRange(i==0?.2:.7,station.Lower,station.Upper);
            Assert.True(RationalConicGauge.LocateAngle(2,arcs,x,y,station,out var recovered));
            Assert.InRange(i==0?Math.PI/4:3*Math.PI/4,recovered.Lower,recovered.Upper);
        }
        Assert.False(RationalConicGauge.LocateStation(2,arcs,x,y,new(-.1),out _,out _,out _));
        Assert.False(RationalConicGauge.LocateAngle(2,arcs,x,y,new(.39,.41),out _));
        poles[4]+=1e-5;
        Assert.False(RationalConicGauge.Certify(spine,conic,1e-10,arcs,x,y,out _));
    }

    [Fact]
    public void NativeShortEdgeProductIsEditableAndFreshXtRetainsSpineAndTrueLimits()
    {
        var body=Import();BooleanOperationTests.Counts(body,10,23,15);
        var data=Definition(body,out var surface);var program=KernelRuntime.GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var instruction=program.Spines[root.Spine];Assert.Equal(CurveClass.Ellipse,instruction.Curve.Kind);
        Assert.Equal(LimitType.Terminator,data.StartLimit.Type);Assert.Equal(LimitType.Terminator,data.EndLimit.Type);
        Assert.Equal(1,data.StartLimit.HvecCount);Assert.Equal(1,data.EndLimit.HvecCount);Assert.Equal(2,data.HvecCount);
        var sf=new PK_BLENDSF_sf_s();Assert.Equal(0,KernelRuntime.BlendSfAsk(surface,&sf));
        Assert.InRange(Math.Abs(sf.spine_extent.value[0]-Math.PI/2),0,1e-12);Assert.InRange(Math.Abs(sf.spine_extent.value[1]-3*Math.PI/2),0,1e-12);
        CheckShortEdges(body);
        SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(body).Slot);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var fresh));
        BooleanOperationTests.Counts(fresh,10,23,15);var copyData=Definition(fresh,out _);
        Assert.Equal(1,copyData.StartLimit.HvecCount);Assert.Equal(1,copyData.EndLimit.HvecCount);CheckShortEdges(fresh);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&fresh));
        Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void ReflectedFirstCutReloadsWithEveryPairedChartWitnessOnItsFiniteSupport()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-small-rolling-first-cut.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));var witnesses=CheckWitnesses(body);CheckLiftedLineUses(body);
        SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(body).Slot);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var fresh));
        Assert.Equal(witnesses,CheckWitnesses(fresh));CheckLiftedLineUses(fresh);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&fresh));
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);

        static int CheckWitnesses(int solid)
        {
            var slot=KernelRuntime.ResolveTagRecord(solid).Slot;Assert.False(KernelRuntime.Xt.IsOpaque(slot));
            var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var count=0;
            for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
            {
                var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];
                if(record.Class!=CurveClass.ICurve)continue;
                var data=KernelRuntime.ICurveDataPool[record.DataIndex];if(data.ProgramBlock<=0||data.ChartCount<=128)continue;
                Assert.Equal(IntersectionUvType.Both,data.UvType);Assert.Equal(4*data.ChartCount,data.UvValueCount);
                var uv=new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.UvValueBlock),data.UvValueCount);
                var hull=new ReadOnlySpan<KernelVector3>(KernelRuntime.DereferenceBlock(data.HvecBlock),data.HvecCount);
                for(var row=0;row<data.ChartCount;row++)for(var side=0;side<2;side++)
                {
                    var at=4*row+2*side;var parameter=new PK_UV_s();parameter.param[0]=uv[at];parameter.param[1]=uv[at+1];
                    PK_VECTOR_s actual;Assert.Equal(0,KernelRuntime.SurfEval(side==0?data.Surface0Tag:data.Surface1Tag,parameter,0,0,0,&actual));
                    var expected=hull[data.StartLimit.HvecCount+row];
                    var distance=EvaluationMath.Sub(new KernelVector3{X=actual.coord[0],Y=actual.coord[1],Z=actual.coord[2]},expected);
                    Assert.InRange(Math.Sqrt(EvaluationMath.Dot(distance,distance)),0,1e-8);
                }
                count+=data.UvValueCount;
            }
            Assert.True(count>512);return count;
        }

        static void CheckLiftedLineUses(int solid)
        {
            var slot=KernelRuntime.ResolveTagRecord(solid).Slot;var face=KernelRuntime.Bodies[slot].FirstFaceBody;var found=0;
            for(var f=0;f<KernelRuntime.Bodies[slot].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
            {
                var record=KernelRuntime.Faces[face];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
                if(surface.Class!=SurfaceClass.BlendSurface)continue;
                var loop=record.FirstLoop;var fin=KernelRuntime.Loops[loop].FirstFin;
                for(var k=0;k<KernelRuntime.Loops[loop].FinCount;k++,fin=KernelRuntime.Fins[fin].NextInLoop)
                {
                    var edge=KernelRuntime.Edges[KernelRuntime.Fins[fin].Edge];
                    if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)].Class!=CurveClass.Line)continue;
                    found++;
                    var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[edge.StartVertex].PointTag)].Position;
                    var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[edge.EndVertex].PointTag)].Position;
                    for(var station=0;station<3;station++)
                    {
                        var point=new PK_VECTOR_s();var uv=new PK_UV_s();PK_VECTOR_s evaluated;
                        var t=station*.5;
                        point.coord[0]=(1-t)*a.X+t*b.X;point.coord[1]=(1-t)*a.Y+t*b.Y;point.coord[2]=(1-t)*a.Z+t*b.Z;
                        Assert.Equal(0,KernelRuntime.SurfParameteriseVector(record.SurfTag,point,&uv));
                        Assert.InRange(uv.param[0],surface.UMin,surface.UMax);Assert.InRange(uv.param[1],0,1);
                        Assert.Equal(0,KernelRuntime.SurfEval(record.SurfTag,uv,0,0,0,&evaluated));
                        var dx=point.coord[0]-evaluated.coord[0];var dy=point.coord[1]-evaluated.coord[1];var dz=point.coord[2]-evaluated.coord[2];
                        Assert.InRange(Math.Sqrt(dx*dx+dy*dy+dz*dz),0,1e-8);
                    }
                }
            }
            Assert.Equal(2,found);
        }
    }

    [Fact]
    public void ExactEllipseNativeParameterJetsAndCertifiedTubeAgree()
    {
        var body=Import();var data=Definition(body,out var surface);var program=KernelRuntime.GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        Span<KernelVector3> actual=stackalloc KernelVector3[3];var expected=new PK_VECTOR_s[3];
        for(var repeat=0;repeat<2;repeat++)for(var i=0;i<11;i++)
        {
            var t=2.5+.04*i;Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(program,root.Spine,t,2,actual));
            fixed(PK_VECTOR_s* output=expected)Assert.Equal(0,KernelRuntime.CurveEval(data.SpineTag,t,2,output));
            for(var d=0;d<3;d++)
            {
                Assert.InRange(Math.Abs(actual[d].X-expected[d].coord[0]),0,2e-12);Assert.InRange(Math.Abs(actual[d].Y-expected[d].coord[1]),0,2e-12);Assert.InRange(Math.Abs(actual[d].Z-expected[d].coord[2]),0,2e-12);
            }
        }
        // The independent interval equations use the exact conic gauge too.
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,root.Spine,new(2.65,2.66,0,0),bounds));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(2.65,2.66,.3,.4),bounds));
        var uv=new PK_UV_s();uv.param[0]=2.655;uv.param[1]=.35;PK_VECTOR_s point;
        Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
        for(var d=0;d<3;d++)Assert.InRange(point.coord[d],bounds[d].Lower,bounds[d].Upper);
        var inverse=new PK_UV_s();Assert.Equal(0,KernelRuntime.SurfParameteriseVector(surface,point,&inverse));
        Assert.InRange(Math.Abs(inverse.param[0]-uv.param[0]),0,1e-11);Assert.InRange(Math.Abs(inverse.param[1]-uv.param[1]),0,1e-11);
    }

    [Fact]
    public void FiniteEllipticContactUsesRetainUvAtCollapsedRollingEndpoints()
    {
        var body=Import();Definition(body,out var surface);var face=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot].FirstFaceBody;var found=0;
        var owns=KernelRuntime.EnsureCommandScratch();
        try
        {
            for(var f=0;f<KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
            {
                if(KernelRuntime.Faces[face].SurfTag!=surface)continue;
                var loop=KernelRuntime.Faces[face].FirstLoop;
                for(var l=0;l<KernelRuntime.Faces[face].LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
                {
                    var fin=KernelRuntime.Loops[loop].FirstFin;
                    for(var k=0;k<KernelRuntime.Loops[loop].FinCount;k++,fin=KernelRuntime.Fins[fin].NextInLoop)
                    {
                        var edge=KernelRuntime.Edges[KernelRuntime.Fins[fin].Edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
                        if(curve.Class!=CurveClass.Ellipse)continue;found++;
                        var use=new KernelRuntime.ModelingEdge{Curve=edge.CurveTag,Start=edge.ParameterStart,End=edge.ParameterEnd,StartVertex=0,EndVertex=1};
                        for(var direction=0;direction<2;direction++)for(var station=0;station<3;station++)
                        {
                            var error=KernelRuntime.SheetCoedgePoint(surface,use,(byte)direction,.5*station,out var uv,out var tangent,out var point);
                            Assert.True(error==0,$"edge={edge.Header.Tag} direction={direction} station={station} error={error}");
                            Assert.True(double.IsFinite(tangent.param[0]));Assert.Equal(0,tangent.param[1]);Assert.True(tangent.param[0]!=0);
                            PK_VECTOR_s evaluated;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&evaluated));
                            Assert.InRange(Math.Abs(point.X-evaluated.coord[0])+Math.Abs(point.Y-evaluated.coord[1])+Math.Abs(point.Z-evaluated.coord[2]),0,1e-11);
                        }
                    }
                }
            }
        }
        finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
        Assert.Equal(2,found);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
    }

    [Theory]
    [InlineData(2.0)] [InlineData(-2.0)]
    public void CopySourceDeletionSimilarityAndUndoRetainExplicitProgramAndLimitPayload(double xScale)
    {
        var body=Import();int copy;Assert.Equal(0,KernelRuntime.EntityCopy(body,&copy));
        var old=Definition(body,out _);var data=Definition(copy,out var surface);
        Assert.NotEqual(old.ProgramBlock,data.ProgramBlock);Assert.NotEqual(old.HvecBlock,data.HvecBlock);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        var uv=new PK_UV_s();uv.param[0]=2.7;uv.param[1]=.37;PK_VECTOR_s before,after;
        Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&before));
        var sf=new PK_TRANSF_sf_s();sf.matrix[0]=xScale;sf.matrix[5]=sf.matrix[10]=2;sf.matrix[15]=1;sf.matrix[3]=3;sf.matrix[7]=5;sf.matrix[11]=7;
        int transform,mark;Assert.Equal(0,KernelRuntime.TransfCreate(&sf,&transform));Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        int n;int* replaced;byte* exact;Assert.Equal(0,KernelRuntime.BodyTransform(copy,transform,0,&n,&replaced,&exact));KernelRuntime.MemoryFree(replaced);KernelRuntime.MemoryFree(exact);
        if(xScale<0)uv.param[1]=1-uv.param[1];Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&after));
        for(var d=0;d<3;d++)Assert.InRange(Math.Abs(after.coord[d]-before.coord[d]*(d==0?xScale:2)-(d==0?3:d==1?5:7)),0,1e-11);
        SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(copy).Slot);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(copy),out var reloaded));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&reloaded));Assert.Equal(0,KernelRuntime.MarkGoto(mark));
        uv.param[1]=.37;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&after));
        for(var d=0;d<3;d++)Assert.Equal(before.coord[d],after.coord[d]);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&transform));
        Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void NativeTerminatorPointHasAContinuousRollingSectionAndConservativeBounds()
    {
        var body=Import();var data=Definition(body,out var surface);var record=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)];
        var program=KernelRuntime.GetNestedBlendProgram(data);var spine=program.Surfaces[data.ProgramRoot].Spine;
        var target=TaylorVector.Constant(new(){X=-2.5,Y=-1.5,Z=4},0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Distance(program,data.ProgramRoot,target,out var distance,out var normal));
        Assert.InRange(Math.Abs(distance[0]),0,1e-12);Assert.True(normal.Finite);
        var uv=new PK_UV_s();uv.param[0]=record.UMin;PK_VECTOR_s point;
        for(var i=0;i<=4;i++)
        {
            uv.param[1]=i/4.0;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
            Assert.InRange(Math.Abs(point.coord[0]+2.5),0,1e-12);Assert.InRange(Math.Abs(point.coord[1]+1.5),0,1e-12);Assert.InRange(Math.Abs(point.coord[2]-4),0,1e-12);
        }
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,spine,new(record.UMin,record.UMin+.01,0,0),bounds));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(record.UMin,record.UMin+.01,0,1),bounds));
        for(var d=0;d<3;d++)Assert.True(bounds[d].Finite);
        uv.param[0]=record.UMin-.01;
        Assert.NotEqual(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(record.UMin-.01,record.UMin+.01,-.01,1.01),bounds));
    }

    [Fact]
    public void WarmImportedSurfaceEvaluationDoesNotAllocate()
    {
        var body=Import();Definition(body,out var surface);var uv=new PK_UV_s();uv.param[0]=2.7;uv.param[1]=.37;PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[9];
        for(var i=0;i<4;i++)Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,2,2,0,jet));
        var before=GC.GetAllocatedBytesForCurrentThread();var error=0;
        for(var i=0;i<16;i++)error|=KernelRuntime.SurfEval(surface,uv,2,2,0,jet);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);
    }

    [Theory]
    [InlineData(-1.49999998,-2,5)] [InlineData(-1.4999999,-2,5)]
    [InlineData(-1.4999998969520074,-2.000038073674117,4.999999998550392)]
    [InlineData(-1.4999998169520075,-2.000038073674117,4.999999998550392)]
    [InlineData(-1.4999999069520076,-2.000038073674117,4.999999998550392)]
    [InlineData(-1.49999998,-2.5,4.5)] [InlineData(-1.4999999,-2.5,4.5)]
    public void NativeOnProbesUseTheFiniteIntersectionCurveBranch(double x,double y,double z)
    {
        // Exact coordinates from the independent native classification CSV;
        // the complete 510-point oracle still retains every unresolved row.
        var body=Import();var vector=new PK_VECTOR_s();vector.coord[0]=x;vector.coord[1]=y;vector.coord[2]=z;
        int enclosure,topology;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,vector,&enclosure,&topology));
        Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);Assert.True(topology>0);
    }

    [Fact]
    public void BothNativeShortIntersectionCurvesHaveCertifiedFiniteTubes()
    {
        var body=Import();var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var count=0;
        var owns=KernelRuntime.EnsureCommandScratch();
        try
        {
            for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            {
                var record=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)];if(curve.Class!=CurveClass.ICurve)continue;
                PK_VECTOR_s a,b,point;Assert.Equal(0,KernelRuntime.CurveEval(record.CurveTag,record.ParameterStart,0,&a));Assert.Equal(0,KernelRuntime.CurveEval(record.CurveTag,record.ParameterEnd,0,&b));
                var dx=a.coord[0]-b.coord[0];var dy=a.coord[1]-b.coord[1];var dz=a.coord[2]-b.coord[2];if(dx*dx+dy*dy+dz*dz>1e-6)continue;
                count++;Assert.Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedIntersectionCurveExtent(record.CurveTag,record.ParameterStart,record.ParameterEnd,out var extent));
                Assert.Equal(0,KernelRuntime.CurveEval(record.CurveTag,(record.ParameterStart+record.ParameterEnd)*.5,0,&point));
                Assert.InRange(point.coord[0],extent.XLo,extent.XHi);Assert.InRange(point.coord[1],extent.YLo,extent.YHi);Assert.InRange(point.coord[2],extent.ZLo,extent.ZHi);
            }
            Assert.Equal(2,count);
        }
        finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(12)] [InlineData(40)] [InlineData(100)] [InlineData(300)]
    public void FailedImportRestoresLimitsProgramsDependenciesAndReturnMemory(int budget)
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        var blocks=KernelRuntime.State.Session->Blocks.Statistics;var returns=KernelRuntime.ReturnOutstandingBlocks;
        KernelRuntime.InjectAllocationFailure(budget);int error,body;
        try{error=XtBrepImportTests.Receive(text,out body);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0)Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));else Assert.Equal(0,body);
        Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(0,KernelRuntime.Bodies.AliveCount);
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Points.AliveCount);
        Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);var after=KernelRuntime.State.Session->Blocks.Statistics;
        Assert.Equal(blocks.LiveBlocks,after.LiveBlocks);Assert.Equal(blocks.LiveBytes,after.LiveBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteCoefficientCertificateRejectsWrongSizeAndWrongCentre(bool shifted)
    {
        var circle=new ParametricSpineCurve{Kind=CurveClass.Circle,First=new(){X=1},Second=new(){Y=1}};
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1);
        Assert.Equal(AlgorithmStatus.Success,SpineParameterMap.Certify(circle,cylinder,-.1,Math.Tau+.1));
        if(shifted)circle.Origin.X=.01;else{circle.First.X=1.01;circle.Second.Y=1.01;}
        Assert.Equal(AlgorithmStatus.InvalidInput,SpineParameterMap.Certify(circle,cylinder,-.1,Math.Tau+.1));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitLineAndPeriodicCircleUseExactNativeGaugeInJointAndIntervalSolves(bool circle)
    {
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};
        var curve=new ParametricSpineCurve{Kind=circle?CurveClass.Circle:CurveClass.Line,First=circle?x:z,Second=circle?y:default};
        var first=new AnalyticSurface(circle?SurfaceClass.Sphere:SurfaceClass.Plane,default,circle?z:x,y,circle?1:0);
        var second=new AnalyticSurface(SurfaceClass.Plane,default,circle?z:y,x);
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=first,NormalSense=1},new(){Analytic=second,NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){Curve=curve,First=0,Second=1,ChartCount=2}];
        var lo=circle?6.1:2.0;var hi=circle?6.5:4.0;
        Assert.Equal(AlgorithmStatus.Success,SpineParameterMap.Series(curve,new(lo,0),out var a,out _));
        Assert.Equal(AlgorithmStatus.Success,SpineParameterMap.Series(curve,new(hi,0),out var b,out _));
        BlendChartInstruction[] charts=[new(){Position=a.Value,Parameter=lo,Scale=1},new(){Position=b.Value,Parameter=hi,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);Span<KernelVector3> jet=stackalloc KernelVector3[3];
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var middle=(lo+hi)*.5;
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(middle-.001,middle+.001,0,0),bounds));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(program,0,middle,2,jet));
        Assert.Equal(AlgorithmStatus.Success,SpineParameterMap.Series(curve,TaylorJet.Variable(middle,2),out var exact,out _));
        for(var d=0;d<3;d++)
        {
            var factor=d==2?2:1;Assert.InRange(Math.Abs(jet[d].X-factor*exact.X[d]),0,1e-12);Assert.InRange(Math.Abs(jet[d].Y-factor*exact.Y[d]),0,1e-12);Assert.InRange(Math.Abs(jet[d].Z-factor*exact.Z[d]),0,1e-12);
        }
        Assert.InRange(jet[0].X,bounds[0].Lower,bounds[0].Upper);Assert.InRange(jet[0].Y,bounds[1].Lower,bounds[1].Upper);Assert.InRange(jet[0].Z,bounds[2].Lower,bounds[2].Upper);
        if(circle)
        {
            var full=new BlendChartInstruction[9];
            for(var i=0;i<full.Length;i++)
            {
                var t=Math.Tau*i/8;SpineParameterMap.Series(curve,new(t,0),out var p,out var tangent);
                full[i]=new(){Parameter=t,Position=p.Value,Chord=tangent.Value,Scale=1};
            }
            full[^1].Position=full[0].Position;spines[0].ChartCount=full.Length;spines[0].StartType=spines[0].EndType=LimitType.Help;
            var closed=new NestedBlendProgram(surfaces,spines,full);
            foreach(var t in new[]{-.1,Math.Tau+.1})
            {
                Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(closed,0,t,2,jet));
                Assert.InRange(Math.Abs(jet[0].X-Math.Cos(t)),0,1e-12);Assert.InRange(Math.Abs(jet[0].Y-Math.Sin(t)),0,1e-12);
            }
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(closed,0,new(-.01,.01,0,0),bounds));
        }
    }

    [Theory]
    [InlineData(2.0)] [InlineData(-2.0)]
    public void ExplicitLineSpineSimilarityScalesNativeExtentAndStation(double xScale)
    {
        var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
        var a=new PK_PLANE_sf_s();a.basis_set.location.coord[0]=-.2;a.basis_set.axis.coord[0]=1;a.basis_set.ref_direction.coord[1]=1;
        var b=new PK_PLANE_sf_s();b.basis_set.location.coord[1]=-.2;b.basis_set.axis.coord[1]=1;b.basis_set.ref_direction.coord[2]=1;
        int first,second,spine;Assert.Equal(0,KernelRuntime.PlaneCreate(&a,&first));Assert.Equal(0,KernelRuntime.PlaneCreate(&b,&second));
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[2]=1;Assert.Equal(0,KernelRuntime.LineCreate(&line,&spine));
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,y,z),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Y=-.2},y,z),NormalSense=1},
            new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Line,First=z},First=0,Second=1,ChartCount=2,Sense=ParasolidConstants.PK_TOPOL_sense_positive_c}];
        BlendChartInstruction[] charts=[new(){Parameter=2,Position=new(){Z=2},Chord=z,Scale=1},new(){Parameter=4,Position=new(){Z=4},Chord=z,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);int surface;
        Assert.Equal(0,KernelRuntime.BindNestedBlend(first,second,spine,program,4,&surface));
        var uv=new PK_UV_s();uv.param[0]=3.3;uv.param[1]=.37;PK_VECTOR_s before,after;
        Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&before));
        var matrix=new PK_TRANSF_sf_s();matrix.matrix[0]=xScale;matrix.matrix[5]=matrix.matrix[10]=2;matrix.matrix[15]=1;matrix.matrix[3]=3;matrix.matrix[7]=5;matrix.matrix[11]=7;
        int transform;Assert.Equal(0,KernelRuntime.TransfCreate(&matrix,&transform));
        var options=new PK_GEOM_transform_o_s{o_t_version=1,modify=ParasolidConstants.PK_GEOM_transform_modify_c};
        int* inputs=stackalloc int[4]{surface,first,second,spine};
        Assert.Equal(0,KernelRuntime.GeomTransform2(4,inputs,transform,&options,null,null));
        var definition=new PK_BLENDSF_sf_s();Assert.Equal(0,KernelRuntime.BlendSfAsk(surface,&definition));Assert.Equal(4,definition.spine_extent.value[0]);Assert.Equal(8,definition.spine_extent.value[1]);
        uv.param[0]*=2;if(xScale<0)uv.param[1]=1-uv.param[1];Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&after));
        for(var d=0;d<3;d++)Assert.InRange(Math.Abs(after.coord[d]-before.coord[d]*(d==0?xScale:2)-(d==0?3:d==1?5:7)),0,1e-12);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));Assert.Equal(0,KernelRuntime.EntityDelete(1,&transform));
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void NearestExplicitEllipseUsesItsLiveGaugeWhenTangentTurnsWithinOneSeedInterval()
    {
        var axis=new KernelVector3{X=Math.Sqrt(1-.0001),Z=.01};var z=new KernelVector3{Z=1};var y=new KernelVector3{Y=1};
        var curve=new ParametricSpineCurve{Kind=CurveClass.Ellipse,First=new(){X=100},Second=y};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Cylinder,default,axis,y,1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,y),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Cylinder,default,axis,y,.8),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,y),NormalSense=1},
            new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){Curve=curve,First=0,Second=1,ChartCount=2}];
        SpineParameterMap.Series(curve,new(-.1,0),out var first,out var tangent);SpineParameterMap.Series(curve,new(.1,0),out var last,out _);
        BlendChartInstruction[] charts=[new(){Parameter=-.1,Position=first.Value,Chord=tangent.Value,Scale=1},new(){Parameter=.1,Position=last.Value,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var point=new KernelVector3{X=100*Math.Cos(.09),Y=Math.Sin(.09),Z=.3};
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Distance(program,4,TaylorVector.Constant(point,0),out var distance,out var normal));
        Assert.InRange(Math.Abs(distance[0]-.1),0,1e-11);Assert.InRange(Math.Abs(normal.Value.Z-1),0,1e-11);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitNearestDistanceD2UsesTheNativeCurveConstraints(bool circle)
    {
        var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
        var curve=new ParametricSpineCurve{Kind=circle?CurveClass.Circle:CurveClass.Line,First=circle?x:z,Second=circle?y:default};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(circle?SurfaceClass.Sphere:SurfaceClass.Plane,default,circle?z:x,y,circle?1:0),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,default,circle?z:y,x),NormalSense=1},
            new(){Analytic=new(circle?SurfaceClass.Sphere:SurfaceClass.Plane,circle?default:new(){X=-.2},circle?z:x,y,circle?.8:0),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,circle?new(){Z=-.2}:new(){Y=-.2},circle?z:y,x),NormalSense=1},
            new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){Curve=curve,First=0,Second=1,ChartCount=9,StartType=circle?LimitType.Help:LimitType.Terminator,EndType=circle?LimitType.Help:LimitType.Terminator}];
        var charts=new BlendChartInstruction[9];
        for(var i=0;i<charts.Length;i++)
        {
            var t=(circle?Math.Tau:4)*i/8;SpineParameterMap.Series(curve,new(t,0),out var p,out var tangent);
            charts[i]=new(){Parameter=t,Position=p.Value,Chord=tangent.Value,Scale=1};
        }
        if(circle)charts[^1].Position=charts[0].Position;
        var program=new NestedBlendProgram(surfaces,spines,charts);var u=TaylorJet.Variable(circle?.7:-.1,2);
        var path=new TaylorVector(u,new(circle?.1:-.1,2),new(circle?-.2:2.3,2));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Distance(program,4,path,out var actual,out var normal));
        var rho=TaylorJet.Sqrt(u*u+.01);var expected=circle?TaylorJet.Sqrt((rho-1)*(rho-1)+.04)-.2:rho-.2;
        for(var order=0;order<=2;order++)Assert.InRange(Math.Abs(actual[order]-expected[order]),0,2e-12);
        Assert.True(normal.Finite);
    }

    private static int Import()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));return body;
    }
    private static BlendedEdgeData Definition(int body,out int surface)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var face=KernelRuntime.Bodies[slot].FirstFaceBody;surface=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var tag=KernelRuntime.Faces[face].SurfTag;var record=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)];
            if(record.Class==SurfaceClass.BlendSurface){Assert.Equal(0,surface);surface=tag;}
        }
        Assert.NotEqual(0,surface);return KernelRuntime.BlendedEdgeDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].DataIndex];
    }
    private static void CheckShortEdges(int body)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var shortEdges=0;
        PK_VECTOR_s* endpoints=stackalloc PK_VECTOR_s[2];
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];
            for(var endpoint=0;endpoint<2;endpoint++)
            {
                var point=new PK_POINT_sf_s();var vertex=endpoint==0?record.StartVertex:record.EndVertex;
                Assert.Equal(0,KernelRuntime.PointAsk(KernelRuntime.Vertices[vertex].PointTag,&point));endpoints[endpoint]=point.position;
            }
            var sum=0.0;for(var d=0;d<3;d++){var delta=endpoints[1].coord[d]-endpoints[0].coord[d];sum+=delta*delta;}
            if(sum>=1e-6)continue;
            Assert.Equal(CurveClass.ICurve,KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class);
            Assert.InRange(Math.Sqrt(sum),7.6147e-5,7.6149e-5);shortEdges++;
        }
        Assert.Equal(2,shortEdges);
    }
}
