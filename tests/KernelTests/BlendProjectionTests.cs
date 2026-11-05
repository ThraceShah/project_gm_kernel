using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendProjectionTests:IDisposable
{
    public BlendProjectionTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Fact]
    public void RangeOptionsRetainTheCAlignmentOfCompositeArrays()
    {
        var options=new PK_GEOM_range_o_s();Assert.Equal(240,sizeof(PK_GEOM_range_o_s));Assert.Equal(152,(byte*)options.param_bound-(byte*)&options);Assert.Equal(232,(byte*)&options.opt_level-(byte*)&options);
    }

    [Theory]
    [InlineData(.4)]
    [InlineData(2.0)]
    public void GlobalProjectionFindsTheInteriorOrTheOpenSpineEndpoint(double y)
    {
        var blend=BlendIntersectionTests.CreateBlend();var point=new PK_VECTOR_s();var angle=.37*Math.PI/2;point.coord[0]=-.3*Math.Cos(angle);point.coord[1]=y;point.coord[2]=-.3*Math.Sin(angle);
        var options=new PK_GEOM_range_vector_o_s{o_t_version=2,opt_level=ParasolidConstants.PK_range_opt_accuracy_c,have_tolerance=1,tolerance=1e-7};options.guess.type=ParasolidConstants.PK_range_guess_no_c;int status;var result=new PK_range_1_r_s();
        Assert.Equal(0,KernelRuntime.GeomRangeVector(blend,point,&options,&status,&result));Assert.Equal(ParasolidConstants.PK_range_result_found_c,status);
        var expected=Math.Sqrt(.01+Math.Pow(Math.Max(0,y-1),2));Assert.InRange(Math.Abs(result.distance-expected),0,2e-7);Assert.InRange(Math.Abs(result.end.parameters[0]-Math.Clamp(y,0,1)),0,1e-4);Assert.InRange(Math.Abs(result.end.parameters[1]-.37),0,1e-4);
    }

    [Fact]
    public void SeparatedSphereDistanceUsesTheCertifiedCenterMinimum()
    {
        var blend=BlendIntersectionTests.CreateBlend();var sf=new PK_SPHERE_sf_s{radius=.01};sf.basis_set.location.coord[0]=-.3/Math.Sqrt(2);sf.basis_set.location.coord[1]=.4;sf.basis_set.location.coord[2]=-.3/Math.Sqrt(2);sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int sphere;Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&sphere));
        var options=new PK_GEOM_range_o_s{o_t_version=3,opt_level=ParasolidConstants.PK_range_opt_accuracy_c,have_tolerance=1,tolerance=1e-6,range_type=ParasolidConstants.PK_range_type_minimum_c};var guesses=(PK_range_guess_s*)options.guesses;guesses[0].type=guesses[1].type=ParasolidConstants.PK_range_guess_no_c;int status;var result=new PK_range_2_r_s();
        Assert.Equal(0,KernelRuntime.GeomRange(blend,sphere,&options,&status,&result));Assert.Equal(ParasolidConstants.PK_range_result_found_c,status);Assert.InRange(Math.Abs(result.distance-.09),0,2e-6);
        Assert.Equal(0,KernelRuntime.GeomRange(sphere,blend,&options,&status,&result));Assert.InRange(Math.Abs(result.distance-.09),0,2e-6);
    }

    [Fact]
    public void GeometryPairMinimumAcceptsABlendAndAnUnboundedPlane()
    {
        var blend=BlendIntersectionTests.CreateBlend();var sf=new PK_PLANE_sf_s();sf.basis_set.location.coord[1]=2;sf.basis_set.axis.coord[1]=1;sf.basis_set.ref_direction.coord[0]=1;int plane;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&plane));
        var options=new PK_GEOM_range_o_s{o_t_version=3,opt_level=ParasolidConstants.PK_range_opt_accuracy_c,have_tolerance=1,tolerance=1e-6,range_type=ParasolidConstants.PK_range_type_minimum_c};
        var guesses=(PK_range_guess_s*)options.guesses;guesses[0].type=guesses[1].type=ParasolidConstants.PK_range_guess_no_c;int status;var result=new PK_range_2_r_s();
        Assert.Equal(0,KernelRuntime.GeomRange(blend,plane,&options,&status,&result));Assert.Equal(ParasolidConstants.PK_range_result_found_c,status);Assert.InRange(Math.Abs(result.distance-1),0,2e-6);
    }
}
