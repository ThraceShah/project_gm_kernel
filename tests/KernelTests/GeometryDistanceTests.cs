using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class GeometryDistanceTests:IDisposable
{
    public GeometryDistanceTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Fact]
    public void SurfacePointDistanceFindsTheGlobalInteriorMinimumAndReportsItsUv()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var p=Point(.5,.5,-1);var options=VectorOptions();int status=0;PK_range_1_r_s result=default;
        Assert.Equal(0,KernelRuntime.GeomRangeVector(surface,p,&options,&status,&result));Assert.Equal(ParasolidConstants.PK_range_result_found_c,status);
        Near(.9375,result.distance);Near(.5,result.end.parameters[0]);Near(.5,result.end.parameters[1]);Assert.Equal(surface,result.end.entity);Assert.Equal(0,result.end.sub_entity);
        var evaluated=new PK_VECTOR_s();var uv=new PK_UV_s();uv.param[0]=result.end.parameters[0];uv.param[1]=result.end.parameters[1];
        Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&evaluated));for(var d=0;d<3;d++)Near(evaluated.coord[d],result.end.vector.coord[d]);
    }

    [Theory]
    [InlineData(.5,18272)] [InlineData(1,18270)] [InlineData(2,18270)]
    public void UpperDistanceBoundsUseTheNativeStrictComparison(double upper,int expected)
    {
        var plane=GeneralSurfaceIntersectionTests.Plane();var options=VectorOptions();options.bound.have_upper_bound=1;options.bound.upper_bound=upper;
        var point=Point(.2,.7,1);int status=0;PK_range_1_r_s result=default;
        Assert.Equal(0,KernelRuntime.GeomRangeVector(plane,point,&options,&status,&result));Assert.Equal(expected,status);Near(expected==18270?1:0,result.distance);
    }

    [Fact]
    public void NonUniqueSphereCenterProjectionReturnsAValidWitnessAndRegionFlag()
    {
        var sf=new PK_SPHERE_sf_s{radius=2};sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int sphere=0;Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&sphere));
        var options=VectorOptions();int status=0;PK_range_1_r_s result=default;
        Assert.Equal(0,KernelRuntime.GeomRangeVector(sphere,Point(0,0,0),&options,&status,&result));Near(2,result.distance);Assert.Equal(1,result.end.region);Near(2,result.end.vector.coord[0]);
    }

    [Fact]
    public void CurveSurfaceDistanceFindsTheInteriorPairAndPreservesBothParameterSystems()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var line=Line(.5,.5,-1,1,0,0);var options=PairOptions();int status=0;PK_range_2_r_s result=default;
        Assert.Equal(0,KernelRuntime.GeomRange(line,surface,&options,&status,&result));Near(.9375,result.distance);Assert.Equal(ParasolidConstants.PK_range_result_found_c,status);
        var ends=(PK_range_end_s*)result.ends;Assert.Equal(line,ends[0].entity);Assert.Equal(surface,ends[1].entity);
        Near(0,ends[0].parameters[0]);Near(.5,ends[1].parameters[0]);Near(.5,ends[1].parameters[1]);
    }

    [Fact]
    public void InvalidRequestsClearOutputsAndLeaveGeometryUntouched()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var options=VectorOptions();options.have_tolerance=1;options.tolerance=-1;
        int status=99;var result=new PK_range_1_r_s{distance=99};var curves=KernelRuntime.Curves.AliveCount;
        Assert.NotEqual(0,KernelRuntime.GeomRangeVector(surface,Point(0,0,0),&options,&status,&result));Assert.Equal(ParasolidConstants.PK_range_result_not_found_c,status);Near(0,result.distance);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void PointToNurbsDistanceDoesNotAllocateManagedMemoryAfterWarmup()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var options=VectorOptions();var point=Point(.5,.5,-1);long before=0;var error=0;
        for(var i=0;i<5;i++)
        {
            if(i==1)before=GC.GetAllocatedBytesForCurrentThread();int status=0;PK_range_1_r_s result=default;
            error|=KernelRuntime.GeomRangeVector(surface,point,&options,&status,&result);if(status!=ParasolidConstants.PK_range_result_found_c)error|=1;
        }
        var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,bytes);
    }

    [Fact]
    public void RangeParameterBoundDiscriminatorMatchesTheNativeAbi()
    {
        Assert.Equal(40,sizeof(PK_range_param_bound_s));
        Assert.Equal((nint)4,System.Runtime.InteropServices.Marshal.OffsetOf<PK_range_param_bound_s>(nameof(PK_range_param_bound_s.param_bound_class)));
    }

    internal static PK_GEOM_range_vector_o_s VectorOptions()=>new(){o_t_version=2,have_tolerance=1,tolerance=1e-9,guess=new(){type=ParasolidConstants.PK_range_guess_no_c},opt_level=ParasolidConstants.PK_range_opt_accuracy_c};
    internal static PK_GEOM_range_o_s PairOptions()
    {
        var options=new PK_GEOM_range_o_s{o_t_version=3,have_tolerance=1,tolerance=1e-9,range_type=ParasolidConstants.PK_range_type_minimum_c,opt_level=ParasolidConstants.PK_range_opt_accuracy_c};
        var guesses=(PK_range_guess_s*)options.guesses;guesses[0].type=guesses[1].type=ParasolidConstants.PK_range_guess_no_c;return options;
    }
    private static PK_VECTOR_s Point(double x,double y,double z){var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;return p;}
    private static int Line(double x,double y,double z,double dx,double dy,double dz)
    {var sf=new PK_LINE_sf_s();sf.basis_set.location=Point(x,y,z);sf.basis_set.axis=Point(dx,dy,dz);int tag=0;Assert.Equal(0,KernelRuntime.LineCreate(&sf,&tag));return tag;}
    private static void Near(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,1e-8);
}
