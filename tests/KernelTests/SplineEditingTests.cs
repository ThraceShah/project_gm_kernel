using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class SplineEditingTests:IDisposable
{
    public SplineEditingTests(){Start();}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}
    private static void Start(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CurveInsertionAndDegreeElevationPreserveAllSampledJetsAndTheOriginalHandle(bool rational)
    {
        var curve=Curve(rational);PK_VECTOR_s* before=stackalloc PK_VECTOR_s[33*3];var sample=new PK_VECTOR_s[3];
        for(var i=0;i<=32;i++)Assert.Equal(0,KernelRuntime.CurveEval(curve,-2+5.0*i/32,2,before+3*i));
        Assert.Equal(0,KernelRuntime.BCurveAddKnot(curve,.7));var options=new PK_BCURVE_raise_degree_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.BCurveRaiseDegree(curve,2,&options));
        fixed(PK_VECTOR_s* actual=sample)for(var i=0;i<=32;i++)
        {
            Assert.Equal(0,KernelRuntime.CurveEval(curve,-2+5.0*i/32,2,actual));
            for(var j=0;j<3;j++)for(var d=0;d<3;d++)Near(before[3*i+j].coord[d],actual[j].coord[d]);
        }
        var data=KernelRuntime.BCurveDataStore[KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve)].DataIndex];
        Assert.Equal(4,data.Degree);Assert.Equal(11,data.NVertices);Assert.Equal(1,KernelRuntime.Curves.AliveCount);Assert.Equal(1,KernelRuntime.BCurveDataStore.AliveCount);
    }

    [Fact]
    public void CurveReparameterisationChangesParameterScaleAndDerivatives()
    {
        var curve=Curve(false);var original=new PK_VECTOR_s[3];fixed(PK_VECTOR_s* p=original)Assert.Equal(0,KernelRuntime.CurveEval(curve,.37,2,p));
        var interval=new PK_INTERVAL_s();interval.value[0]=7;interval.value[1]=17;var options=new PK_BCURVE_reparameterise_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.BCurveReparameterise(curve,interval,&options));
        PK_VECTOR_s* actual=stackalloc PK_VECTOR_s[3];Assert.Equal(0,KernelRuntime.CurveEval(curve,7+(.37+2)*2,2,actual));
        for(var j=0;j<3;j++)for(var d=0;d<3;d++)Near(original[j].coord[d]/Math.Pow(2,j),actual[j].coord[d]);
    }

    [Fact]
    public void SurfaceKnotInsertionAndElevationPreserveTheTensorProduct()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);PK_VECTOR_s* expected=stackalloc PK_VECTOR_s[9*9*4];var uv=new PK_UV_s();
        for(var u=0;u<=8;u++)for(var v=0;v<=8;v++){uv.param[0]=u/8.0;uv.param[1]=v/8.0;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,1,1,0,expected+4*(9*u+v)));}
        Assert.Equal(0,KernelRuntime.BSurfAddUKnot(surface,.37));Assert.Equal(0,KernelRuntime.BSurfAddVKnot(surface,.63));var options=new PK_BSURF_raise_degree_o_s{o_t_version=1};
        Assert.Equal(0,KernelRuntime.BSurfRaiseDegree(surface,ParasolidConstants.PK_PARAM_direction_u_c,1,&options));Assert.Equal(0,KernelRuntime.BSurfRaiseDegree(surface,ParasolidConstants.PK_PARAM_direction_v_c,2,&options));
        PK_VECTOR_s* actual=stackalloc PK_VECTOR_s[4];
        for(var u=0;u<=8;u++)for(var v=0;v<=8;v++)
        {
            uv.param[0]=u/8.0;uv.param[1]=v/8.0;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,1,1,0,actual));
            for(var j=0;j<4;j++)for(var d=0;d<3;d++)Near(expected[4*(9*u+v)+j].coord[d],actual[j].coord[d]);
        }
        Assert.Equal(1,KernelRuntime.Surfaces.AliveCount);Assert.Equal(1,KernelRuntime.BSurfaceDataStore.AliveCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SurfaceParameterReversalOrTransposeMapsToTheOriginalGeometry(bool transpose)
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var uv=new PK_UV_s();uv.param[0]=.17;uv.param[1]=.73;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
        var options=new PK_BSURF_reparameterise_o_s{o_t_version=2,transpose=transpose?(byte)1:(byte)0,reverse_u=transpose?(byte)0:(byte)1};Assert.Equal(0,KernelRuntime.BSurfReparameterise(surface,&options));
        if(transpose){uv.param[0]=.73;uv.param[1]=.17;}else uv.param[0]=.83;
        var actual=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&actual));for(var d=0;d<3;d++)Near(point.coord[d],actual.coord[d]);
    }

    [Fact]
    public void EditingAllocationFailuresRestoreTheOriginalPayloadAndReleaseTemporaryEntities()
    {
        var failed=false;var passed=false;
        for(var budget=0;budget<80;budget+=5)
        {
            var curve=Curve(true);var before=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,.37,0,&before));var options=new PK_BCURVE_raise_degree_o_s{o_t_version=1};
            KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BCurveRaiseDegree(curve,1,&options);KernelRuntime.InjectAllocationFailure(-1);
            if(error!=0)failed=true;else passed=true;
            var actual=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,.37,0,&actual));for(var d=0;d<3;d++)Near(before.coord[d],actual.coord[d]);
            Assert.Equal(1,KernelRuntime.Curves.AliveCount);Assert.Equal(1,KernelRuntime.BCurveDataStore.AliveCount);Start();
        }
        Assert.True(failed&&passed);
    }

    [Fact]
    public void InvalidEditsAreRejectedBeforeChangingGeometryOrAllocatingPayloads()
    {
        var curve=Curve(false);var surface=GeneralSurfaceIntersectionTests.Surface(false);
        var co=new PK_BCURVE_raise_degree_o_s{o_t_version=1};var so=new PK_BSURF_raise_degree_o_s{o_t_version=1};
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,KernelRuntime.BCurveRaiseDegree(curve,int.MaxValue,&co));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,KernelRuntime.BSurfRaiseDegree(surface,ParasolidConstants.PK_PARAM_direction_u_c,int.MaxValue,&so));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,KernelRuntime.BCurveAddKnot(curve,double.NaN));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,KernelRuntime.BSurfAddVKnot(surface,1));
        Assert.Equal(1,KernelRuntime.BCurveDataStore.AliveCount);Assert.Equal(1,KernelRuntime.BSurfaceDataStore.AliveCount);
        var interval=new PK_INTERVAL_s();interval.value[0]=-2;interval.value[1]=3;int body=0;
        Assert.Equal(0,KernelRuntime.CurveMakeWireBody(curve,interval,&body));
        int n=0;int* edges=null;Assert.Equal(0,KernelRuntime.BodyAskEdges(body,&n,&edges));
        try
        {
            int attached=0;Assert.Equal(0,KernelRuntime.EdgeAskCurve(edges[0],&attached));
            Assert.Equal(ParasolidConstants.PK_ERROR_is_attached,KernelRuntime.BCurveAddKnot(attached,.7));
        }
        finally{KernelRuntime.MemoryFree(edges);}
    }

    [Fact]
    public void MarkGotoRestoresTheOriginalDefinitionAndReclaimsEditedPayloads()
    {
        var curve=Curve(true);var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,.37,0,&point));int mark=0;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var raise=new PK_BCURVE_raise_degree_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.BCurveRaiseDegree(curve,1,&raise));
        var interval=new PK_INTERVAL_s();interval.value[0]=7;interval.value[1]=17;var remap=new PK_BCURVE_reparameterise_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.BCurveReparameterise(curve,interval,&remap));
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));var restored=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,.37,0,&restored));
        for(var d=0;d<3;d++)Near(point.coord[d],restored.coord[d]);
        Assert.Equal(2,KernelRuntime.BCurveDataStore[KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve)].DataIndex].Degree);
        Assert.Equal(1,KernelRuntime.Curves.AliveCount);Assert.Equal(1,KernelRuntime.BCurveDataStore.AliveCount);
    }

    private static int Curve(bool rational)
    {
        double* p=stackalloc double[16];double* x=stackalloc double[4]{0,.3,.8,1};double* y=stackalloc double[4]{0,.8,-.4,1};double* z=stackalloc double[4]{0,.2,.3,0};
        double* weights=stackalloc double[4]{1,1.2,.8,1.1};var dim=rational?4:3;
        for(var i=0;i<4;i++){var w=rational?weights[i]:1;p[i*dim]=x[i]*w;p[i*dim+1]=y[i]*w;p[i*dim+2]=z[i]*w;if(rational)p[i*dim+3]=w;}
        double* knots=stackalloc double[3]{-2,.4,3};int* mult=stackalloc int[3]{3,1,3};
        var sf=new PK_BCURVE_sf_s{degree=2,n_vertices=4,vertex_dim=dim,is_rational=rational?(byte)1:(byte)0,vertex=p,n_knots=3,knot=knots,knot_mult=mult,form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_unset_c};
        int curve=0;Assert.Equal(0,KernelRuntime.BCurveCreate(&sf,&curve));return curve;
    }
    private static void Near(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,2e-9);
}
