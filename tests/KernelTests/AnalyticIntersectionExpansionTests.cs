using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class AnalyticIntersectionExpansionTests : IDisposable
{
    public AnalyticIntersectionExpansionTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(0,2)] [InlineData(1,2)] [InlineData(2,2)] [InlineData(3,2)] [InlineData(4,2)]
    public void BoundedAnalyticIntersectionsDiscoverAllRegularClosedComponents(int kind,int expected)
    {
        int a,b;
        if(kind==0){a=Cylinder(1);b=Cylinder(.6,.1,.2,true);}
        else if(kind==1){a=Sphere(1,.2,.1,.3);b=Cylinder(.6);}
        else if(kind==2){a=Torus(1,.4);b=Sphere(1,.2,0,0);}
        else if(kind==3){a=Torus(2,.6);b=Cylinder(.5,0,0,false,2);}
        else {a=Torus(2,.6);b=Torus(2,.6,.2);}
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c};
        int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* ranges=null;int* kinds=null;
        var error=KernelRuntime.SurfIntersectSurf(a,b,&options,&np,&points,&nc,&curves,&ranges,&kinds);
        Assert.True(error==0,$"intersect={error}, chart={KernelRuntime.LastChartBuildFailure}");
        try
        {
            Assert.Equal(0,np);Assert.Equal(expected,nc);var uv=new PK_UV_s();var p=new PK_VECTOR_s();var start=new PK_VECTOR_s();var end=new PK_VECTOR_s();
            for(var i=0;i<nc;i++)
            {
                Assert.Equal(0,KernelRuntime.CurveEval(curves[i],ranges[i].value[0],0,&start));
                Assert.Equal(0,KernelRuntime.CurveEval(curves[i],ranges[i].value[1],0,&end));
                for(var d=0;d<3;d++)Assert.InRange(Math.Abs(start.coord[d]-end.coord[d]),0,1e-8);
                for(var j=0;j<=24;j++)
                {
                    var t=j==24?ranges[i].value[1]:ranges[i].value[0]+(ranges[i].value[1]-ranges[i].value[0])*j/24;
                    error=KernelRuntime.CurveEval(curves[i],t,0,&p);
                    Assert.True(error==0,$"curve={i}, t={t:R}, error={error}, kind={KernelRuntime.LastICurveEvalReport.Kind}, detail={KernelRuntime.LastICurveEvalReport.Detail}, residual={KernelRuntime.LastICurveEvalReport.Residual:R}");
                    Assert.Equal(0,KernelRuntime.SurfParameteriseVector(a,p,&uv));Assert.Equal(0,KernelRuntime.SurfParameteriseVector(b,p,&uv));
                }
            }
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(ranges!=null)KernelRuntime.MemoryFree(ranges);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }

    private static int Cylinder(double radius,double y=0,double z=0,bool xAxis=false,double x=0)
    {
        var sf=new PK_CYL_sf_s{radius=radius};sf.basis_set.location.coord[0]=x;sf.basis_set.location.coord[1]=y;sf.basis_set.location.coord[2]=z;
        sf.basis_set.axis.coord[xAxis?0:2]=1;sf.basis_set.ref_direction.coord[xAxis?1:0]=1;
        int tag=0;Assert.Equal(0,KernelRuntime.CylCreate(&sf,&tag));return tag;
    }
    private static int Sphere(double radius,double x,double y,double z)
    {
        var sf=new PK_SPHERE_sf_s{radius=radius};sf.basis_set.location.coord[0]=x;sf.basis_set.location.coord[1]=y;sf.basis_set.location.coord[2]=z;
        sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int tag=0;Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&tag));return tag;
    }
    private static int Torus(double major,double minor,double z=0)
    {
        var sf=new PK_TORUS_sf_s{major_radius=major,minor_radius=minor};sf.basis_set.location.coord[2]=z;
        sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int tag=0;Assert.Equal(0,KernelRuntime.TorusCreate(&sf,&tag));return tag;
    }
}
