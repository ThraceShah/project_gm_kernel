using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ProfileCurveBoundsTests:IDisposable
{
    public ProfileCurveBoundsTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedWireOwnsAnExactlyClippedSupportWithOriginalParameters(bool rational)
    {
        var curve=Spline(0,rational);int body=0;Assert.Equal(0,KernelRuntime.CurveMakeWireBody(curve,Range(.1,.4),&body));
        var edge=Record(body).FirstEdgeBody;int support=0;Assert.Equal(0,KernelRuntime.EdgeAskCurve(KernelRuntime.Edges[edge].Header.Tag,&support));
        var definition=new PK_BCURVE_sf_s();Assert.Equal(0,KernelRuntime.BCurveAsk(support,&definition));
        try{Assert.Equal(2,definition.n_vertices);Assert.Equal(2,definition.n_knots);Assert.Equal(.1,definition.knot[0]);Assert.Equal(.4,definition.knot[1]);}
        finally{Free(definition.vertex);Free(definition.knot);Free(definition.knot_mult);}
        for(var i=0;i<=8;i++)
        {
            var t=.1+.3*i/8;var expected=new PK_VECTOR_s();var actual=new PK_VECTOR_s();
            Assert.Equal(0,KernelRuntime.CurveEval(curve,t,0,&expected));Assert.Equal(0,KernelRuntime.CurveEval(support,t,0,&actual));
            for(var c=0;c<3;c++)Assert.InRange(Math.Abs(expected.coord[c]-actual.coord[c]),0,2e-12);
        }
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&curve));var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(support,.25,0,&point));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonG1KnotsProduceThreeEdgesAndMapEveryEdgeToItsInput(bool rational)
    {
        var curve=Spline(0,rational);var options=Options();var interval=Range(0,3);int body=0,n=0;int* edges=null;int* indices=null;
        Assert.Equal(0,KernelRuntime.CurveMakeWireBody2(1,&curve,&interval,&options,&body,&n,&edges,&indices));
        try
        {
            Assert.Equal(3,n);
            for(var i=0;i<3;i++)
            {
                Assert.Equal(0,indices[i]);int support=0;Assert.Equal(0,KernelRuntime.EdgeAskCurve(edges[i],&support));
                var sf=new PK_BCURVE_sf_s();Assert.Equal(0,KernelRuntime.BCurveAsk(support,&sf));
                try{Assert.Equal(2,sf.n_vertices);Assert.Equal(i,sf.knot[0]);Assert.Equal(i+1,sf.knot[1]);}
                finally{Free(sf.vertex);Free(sf.knot);Free(sf.knot_mult);}
            }
            Assert.Equal(4,Record(body).VertexCountBody);
        }
        finally{Free(edges);Free(indices);}
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GeometricG1JunctionRemainsOneEdgeDespiteAParameterSpeedJump(int shape)
    {
        var curve=Spline(shape,false);int body=0;Assert.Equal(0,KernelRuntime.CurveMakeWireBody(curve,Range(0,2),&body));
        Assert.Equal(1,Record(body).EdgeCountBody);
    }

    [Fact]
    public void SplitIndicesReferToTheOriginalCurveArrayRatherThanOutputPositions()
    {
        int* curves=stackalloc int[2]{Spline(0,false),Spline(1,false)};
        // Keep the second input disjoint: starting it at t=1.5 would join
        // the first curve's C0 corner and correctly form a rejected T-wire.
        PK_INTERVAL_s* bounds=stackalloc PK_INTERVAL_s[2]{Range(0,1.5),Range(0,.5)};
        var options=Options();options.allow_disjoint=1;int body=0,n=0;int* edges=null;int* indices=null;
        Assert.Equal(0,KernelRuntime.CurveMakeWireBody2(2,curves,bounds,&options,&body,&n,&edges,&indices));
        try{Assert.Equal(3,n);Assert.Equal(0,indices[0]);Assert.Equal(0,indices[1]);Assert.Equal(1,indices[2]);}
        finally{Free(edges);Free(indices);}
    }

    [Fact]
    public void ClippingAndSplitAllocationFailuresLeaveSourceAndPoolsUnchanged()
    {
        var curve=Spline(0,true);var before=KernelRuntime.Curves.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;var succeeded=false;
        for(var budget=0;budget<150;budget++)
        {
            var options=Options();var range=Range(.1,2.8);int body=17,n=17;int* edges=(int*)1;int* indices=(int*)1;
            KernelRuntime.InjectAllocationFailure(budget);
            var error=KernelRuntime.CurveMakeWireBody2(1,&curve,&range,&options,&body,&n,&edges,&indices);KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){succeeded=true;Free(edges);Free(indices);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));}
            else{Assert.Equal(0,body);Assert.Equal(0,n);Assert.True(edges==null&&indices==null);}
            Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(before,KernelRuntime.Curves.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
            Assert.True(KernelRuntime.IsValidTag(curve));
            if(succeeded)break;
        }
        Assert.True(succeeded);
    }

    private static int Spline(int shape,bool rational)
    {
        var degree=shape==2?2:1;var count=shape==0?4:shape==1?3:5;var dim=rational?4:3;
        double* input=stackalloc double[15];
        if(shape==0){double* values=stackalloc double[12]{-3,0,-.2,2,0,0,3,0,.1,0,0,.3};new ReadOnlySpan<double>(values,12).CopyTo(new Span<double>(input,15));}
        else if(shape==1){double* values=stackalloc double[9]{0,0,0,1,0,0,3,0,0};new ReadOnlySpan<double>(values,9).CopyTo(new Span<double>(input,15));}
        else{double* values=stackalloc double[15]{0,0,0,.5,0,0,1,1,0,2,3,0,3,4,0};new ReadOnlySpan<double>(values,15).CopyTo(new Span<double>(input,15));}
        double* poles=stackalloc double[count*dim];
        for(var i=0;i<count;i++){var weight=rational?1+i*.5:1;for(var c=0;c<3;c++)poles[i*dim+c]=weight*input[3*i+c];if(rational)poles[i*dim+3]=weight;}
        double* knots=stackalloc double[4]{0,1,2,3};int* multiplicities=stackalloc int[4]{degree+1,degree,shape==0?degree:degree+1,degree+1};
        var sf=new PK_BCURVE_sf_s{degree=degree,n_vertices=count,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=poles,n_knots=shape==0?4:3,knot=knots,knot_mult=multiplicities,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_unset_c};
        int curve=0;Assert.Equal(0,KernelRuntime.BCurveCreate(&sf,&curve));return curve;
    }
    private static PK_CURVE_make_wire_body_o_s Options()=>new(){o_t_version=3,tolerance=1e-6,check=1,want_edges=1,want_indices=1,sequential=ParasolidConstants.PK_CURVE_sequential_no_c};
    private static BodyRecord Record(int body){Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));return KernelRuntime.GetBodyRecord(slot);}
    private static PK_INTERVAL_s Range(double first,double last){var result=new PK_INTERVAL_s();result.value[0]=first;result.value[1]=last;return result;}
    private static void Free(void* p){if(p!=null)Assert.Equal(0,KernelRuntime.MemoryFree(p));}
}
