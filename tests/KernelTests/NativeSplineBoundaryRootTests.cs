using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NativeSplineBoundaryRootTests:IDisposable
{
    public NativeSplineBoundaryRootTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Theory]
    [InlineData(false,0,true)] [InlineData(true,0,true)]
    [InlineData(false,1e-13,false)] [InlineData(false,-1e-13,false)]
    public void SameLineCarrierRetainsTheNonlinearNativeBoundaryParameter(bool reversed,double offset,bool expected)
    {
        double* vertices=stackalloc double[6]{reversed?1:0,offset,0,reversed?0:1,offset,0};
        double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{2,2};
        var curve=new PK_BCURVE_sf_s{degree=1,n_vertices=2,vertex_dim=3,vertex=vertices,n_knots=2,knot=knots,knot_mult=mult,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c};
        CurveTag tag;Assert.Equal(0,KernelRuntime.BCurveCreate(&curve,&tag));
        double* poles=stackalloc double[18];
        for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i==1?.6:i*.5;poles[at+1]=j;poles[at+2]=0;}
        int* um=stackalloc int[2]{3,3};var surface=new PK_BSURF_sf_s{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,
            n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=mult,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        SurfTag support;Assert.Equal(0,KernelRuntime.BSurfCreate(&surface,&support));
        Assert.Equal(expected,KernelRuntime.TestNativeSplineBoundaryRoot(tag,support,new IntervalNumber(.5),new IntervalNumber(.25,.75),out var uv,out var domain));
        if(!expected)return;
        var exact=(1.2-Math.Sqrt(1.04))/.4;
        Assert.InRange(exact,uv.U0,uv.U1);Assert.True(uv.U1<.5);Assert.Equal(0,uv.V0);Assert.Equal(0,uv.V1);
        Assert.True(IsolatedRootCertificate.Contains(domain,uv));
        Assert.True(domain.U0<.25&&domain.U1<.75);
    }
}
