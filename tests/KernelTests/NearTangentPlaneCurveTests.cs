using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NearTangentPlaneCurveTests:IDisposable
{
    public NearTangentPlaneCurveTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(16)] [InlineData(20)] [InlineData(24)]
    public void RegularNearContactLoopRemainsClosedAndEvaluableWithCurvature(int exponent)
    {
        var height=Math.ScaleB(1,-exponent);var surface=Surface();var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[2]=.5+height;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int plane;Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-5,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c};
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* ranges;int* kinds;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(surface,plane,&options,&np,&points,&nc,&curves,&ranges,&kinds));
        try
        {
            Assert.Equal(0,np);Assert.Equal(1,nc);PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[3];var angle=0.0;var previous=0.0;
            for(var i=0;i<=64;i++)
            {
                var t=ranges[0].value[0]+(ranges[0].value[1]-ranges[0].value[0])*i/64;Assert.Equal(0,KernelRuntime.CurveEval(curves[0],t,2,jet));
                var x=jet[0].coord[0];var y=jet[0].coord[1];Assert.True(Math.Abs(x*x+y*y-height)<1e-12);Assert.True(Math.Abs(jet[0].coord[2]-(.5+height))<1e-12);
                var dx=jet[1].coord[0];var dy=jet[1].coord[1];var ddx=jet[2].coord[0];var ddy=jet[2].coord[1];var speed=Math.Sqrt(dx*dx+dy*dy);
                Assert.True(speed>0);Assert.True(Math.Abs(x*dx+y*dy)/(Math.Sqrt(height)*speed)<1e-7);
                var curvature=Math.Abs(dx*ddy-dy*ddx)/(speed*speed*speed);Assert.True(Math.Abs(curvature*Math.Sqrt(height)-1)<1e-6);
                var a=Math.Atan2(y,x);if(i>0){var d=a-previous;while(d>Math.PI)d-=Math.Tau;while(d< -Math.PI)d+=Math.Tau;angle+=d;}previous=a;
            }
            Assert.True(Math.Abs(Math.Abs(angle)-Math.Tau)<1e-8);Assert.Equal(0,KernelRuntime.EntityDelete(nc,curves));
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(ranges!=null)KernelRuntime.MemoryFree(ranges);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }

    private static int Surface()
    {
        double* poles=stackalloc double[27];double* a=stackalloc double[3]{.25,-.25,.25};
        for(var i=0;i<3;i++)for(var j=0;j<3;j++){var at=3*(3*i+j);poles[at]=i*.5-.5;poles[at+1]=j*.5-.5;poles[at+2]=.5+a[i]+a[j];}
        double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{3,3};
        var sf=new PK_BSURF_sf_s{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};int surface;
        Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&surface));return surface;
    }
}
