using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NestedBlendRuntimeTests:IDisposable
{
    public NestedBlendRuntimeTests(){Start();}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}
    private static void Start(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}

    [Fact]
    public void PublicSurfaceEvaluationUsesTheCompiledProceduralDefinitionWithoutManagedAllocation()
    {
        var surface=Create(out var a,out var b,out var spine);var uv=new PK_UV_s();uv.param[0]=.37;uv.param[1]=.23;
        PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[9];long before=0;var error=0;
        for(var i=0;i<16;i++){if(i==4)before=GC.GetAllocatedBytesForCurrentThread();error|=KernelRuntime.SurfEval(surface,uv,2,2,0,jet);}
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);
        Assert.InRange(Math.Abs(jet[0].coord[0]+.2*Math.Sin(.23*Math.PI/2)),0,1e-12);Assert.InRange(Math.Abs(jet[0].coord[1]-.37),0,1e-12);
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced,KernelRuntime.EntityDelete(1,&a));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);
        Assert.False(KernelRuntime.IsValidTag(a));Assert.False(KernelRuntime.IsValidTag(b));Assert.False(KernelRuntime.IsValidTag(spine));
    }

    [Fact]
    public void ADeepCopyOwnsIndependentProgramStorageAndItsDefiningGeometry()
    {
        var surface=Create(out _,out _,out _);int copy=0;Assert.Equal(0,KernelRuntime.EntityCopy(surface,&copy));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));var definition=new PK_BLENDSF_sf_s();Assert.Equal(0,KernelRuntime.BlendSfAsk(copy,&definition));
        var uv=new PK_UV_s();uv.param[0]=.43;uv.param[1]=.61;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(copy,uv,0,0,0,&point));
        Assert.InRange(Math.Abs(point.coord[0]+.2*Math.Sin(.61*Math.PI/2)),0,1e-12);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void SurfaceParameterisationInvertsTheProceduralFrameAndRejectsOffSurfacePoints()
    {
        var surface=Create(out _,out _,out _);var uv=new PK_UV_s();uv.param[0]=.37;uv.param[1]=.23;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
        var inverse=new PK_UV_s();Assert.Equal(0,KernelRuntime.SurfParameteriseVector(surface,point,&inverse));
        Assert.InRange(Math.Abs(inverse.param[0]-uv.param[0]),0,1e-11);Assert.InRange(Math.Abs(inverse.param[1]-uv.param[1]),0,1e-11);
        point.coord[0]-=.03;Assert.Equal(ParasolidConstants.PK_ERROR_not_on_surface,KernelRuntime.SurfParameteriseVector(surface,point,&inverse));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(-2.0)]
    public void SimilarityTransformsEveryCompiledJetAndMarkGotoRestoresThePayload(double xScale)
    {
        var surface=Create(out var a,out var b,out var spine);var uv=new PK_UV_s();uv.param[0]=.37;uv.param[1]=.23;
        PK_VECTOR_s* before=stackalloc PK_VECTOR_s[9];PK_VECTOR_s* after=stackalloc PK_VECTOR_s[9];Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,2,2,0,before));
        var sf=new PK_TRANSF_sf_s();sf.matrix[0]=xScale;sf.matrix[5]=sf.matrix[10]=2;sf.matrix[3]=3;sf.matrix[7]=5;sf.matrix[11]=7;sf.matrix[15]=1;
        int transform;Assert.Equal(0,KernelRuntime.TransfCreate(&sf,&transform));int mark;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var options=new PK_GEOM_transform_o_s{o_t_version=1,modify=ParasolidConstants.PK_GEOM_transform_modify_c,want_out_geoms=1,want_exact=1};
        int* inputs=stackalloc int[4]{surface,a,b,spine};int* outputs=stackalloc int[4];byte* exact=stackalloc byte[4];
        Assert.Equal(0,KernelRuntime.GeomTransform2(4,inputs,transform,&options,outputs,exact));if(xScale<0)uv.param[1]=1-uv.param[1];Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,2,2,0,after));
        for(var i=0;i<9;i++)for(var d=0;d<3;d++)
        {
            var expected=before[i].coord[d]*(d==0?xScale:2)*(xScale<0&&i/3%2==1?-1:1)+(i==0?(d==0?3:d==1?5:7):0);Assert.InRange(Math.Abs(expected-after[i].coord[d]),0,1e-11);
        }
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));uv.param[1]=.23;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,2,2,0,after));
        for(var i=0;i<9;i++)for(var d=0;d<3;d++)Assert.Equal(before[i].coord[d],after[i].coord[d]);
    }

    [Fact]
    public void BindingFailuresDoNotLeakProgramPayloadsOrReferences()
    {
        var failed=false;var passed=false;
        for(var budget=0;budget<60;budget+=3)
        {
            Inputs(out var a,out var b,out var spine);var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());int surface=0;
            KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BindNestedBlend(a,b,spine,program,4,&surface);KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){passed=true;Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));}
            else{failed=true;Assert.Equal(0,surface);Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);Assert.Equal(2,KernelRuntime.Surfaces.AliveCount);Assert.Equal(1,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.EntityDelete(1,&a));}
            Start();
        }
        Assert.True(failed&&passed);
    }

    [Fact]
    public void GeneralConstructionRollsBackIntermediateOffsetsAndDeletesOwnedDefinitionsOnSuccess()
    {
        var torusSf=new PK_TORUS_sf_s{major_radius=2,minor_radius=.5};torusSf.basis_set.axis.coord[2]=1;torusSf.basis_set.ref_direction.coord[0]=1;int torus;
        var planeSf=new PK_PLANE_sf_s();planeSf.basis_set.axis.coord[1]=.6;planeSf.basis_set.axis.coord[2]=.8;planeSf.basis_set.ref_direction.coord[0]=1;int plane;
        var options=new PK_SURF_create_blend_o_s{o_t_version=1};var start=new PK_VECTOR_s();start.coord[0]=2.5;
        var end=new PK_VECTOR_s();end.coord[0]=2;end.coord[1]=.5;end.coord[2]=-.375;
        var failed=false;var succeeded=false;
        foreach(var budget in new[]{0,1,3,7,15,31})
        {
            Assert.Equal(0,KernelRuntime.TorusCreate(&torusSf,&torus));Assert.Equal(0,KernelRuntime.PlaneCreate(&planeSf,&plane));
            int result=0;KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.SurfCreateBlend(torus,.1,plane,.1,start,end,&options,&result);KernelRuntime.InjectAllocationFailure(-1);
            if(error!=0)
            {
                failed=true;Assert.Equal(0,result);Assert.Equal(2,KernelRuntime.Surfaces.AliveCount);
                Assert.True(KernelRuntime.IsValidTag(torus));Assert.True(KernelRuntime.IsValidTag(plane));
                Assert.Equal(0,KernelRuntime.EntityDelete(1,&torus));Assert.Equal(0,KernelRuntime.EntityDelete(1,&plane));
            }
            else
            {
                succeeded=true;Assert.Equal(0,KernelRuntime.EntityDelete(1,&result));
                Assert.False(KernelRuntime.IsValidTag(torus));Assert.False(KernelRuntime.IsValidTag(plane));
            }
            Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.BlendedEdgeDataPool.AliveCount);
        }
        Assert.True(failed&&succeeded);
    }

    private static int Create(out int a,out int b,out int spine)
    {
        Inputs(out a,out b,out spine);var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());int surface=0;
        Assert.Equal(0,KernelRuntime.BindNestedBlend(a,b,spine,program,4,&surface));return surface;
    }
    private static void Inputs(out int a,out int b,out int spine)
    {
        var x=new PK_PLANE_sf_s();x.basis_set.location.coord[0]=-.2;x.basis_set.axis.coord[0]=1;x.basis_set.ref_direction.coord[1]=1;int first=0;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&x,&first));a=first;
        var z=new PK_PLANE_sf_s();z.basis_set.location.coord[2]=-.2;z.basis_set.axis.coord[2]=1;z.basis_set.ref_direction.coord[0]=1;int second=0;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&z,&second));b=second;
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[1]=1;int curve=0;Assert.Equal(0,KernelRuntime.LineCreate(&line,&curve));spine=curve;
    }
}
