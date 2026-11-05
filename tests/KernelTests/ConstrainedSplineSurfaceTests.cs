using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ConstrainedSplineSurfaceTests:IDisposable
{
    public ConstrainedSplineSurfaceTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public void ClampedBoundaryAndCornerTwistsReproduceAnIndependentBicubicPolynomial(int count)
    {
        double* u=stackalloc double[3]{0,.7,2};double* v=stackalloc double[3]{-1,.2,1};if(count==2){u[1]=2;v[1]=1;}
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[count*count];PK_VECTOR_s* ul=stackalloc PK_VECTOR_s[count];PK_VECTOR_s* uh=stackalloc PK_VECTOR_s[count];
        PK_VECTOR_s* vl=stackalloc PK_VECTOR_s[count];PK_VECTOR_s* vh=stackalloc PK_VECTOR_s[count];
        for(var j=0;j<count;j++)for(var i=0;i<count;i++)points[j*count+i]=Jet(u[i],v[j],0,0);
        for(var i=0;i<count;i++){ul[i]=Jet(0,v[i],1,0);uh[i]=Jet(2,v[i],1,0);vl[i]=Jet(u[i],-1,0,1);vh[i]=Jet(u[i],1,0,1);}
        var hu=u[1]-u[0];var hU=u[count-1]-u[count-2];var hv=v[1]-v[0];var hV=v[count-1]-v[count-2];
        for(var i=0;i<count;i++)for(var d=0;d<3;d++){ul[i].coord[d]*=hu;uh[i].coord[d]*=hU;vl[i].coord[d]*=hv;vh[i].coord[d]*=hV;}
        var definition=new PK_BSURF_splinewise_sf_s{u_degree=3,v_degree=3,n_u_positions=count,n_v_positions=count,positions=points,
            u_end=18512,U_end=18512,v_end=18512,V_end=18512,u_derivs=ul,U_derivs=uh,v_derivs=vl,V_derivs=vh,
            u_how_knot=18521,v_how_knot=18521,u_knot=u,v_knot=v,u_v=18531,u_V=18531,U_v=18531,U_V=18531,
            u_v_twist=Jet(0,-1,1,1),u_V_twist=Jet(0,1,1,1),U_v_twist=Jet(2,-1,1,1),U_V_twist=Jet(2,1,1,1)};
        for(var d=0;d<3;d++){definition.u_v_twist.coord[d]*=hu*hv;definition.u_V_twist.coord[d]*=hu*hV;definition.U_v_twist.coord[d]*=hU*hv;definition.U_V_twist.coord[d]*=hU*hV;}
        int surface=0;Assert.Equal(0,KernelRuntime.BSurfCreateSplinewise(&definition,&surface));
        PK_VECTOR_s* evaluated=stackalloc PK_VECTOR_s[9];var uv=new PK_UV_s();
        for(var i=0;i<=8;i++)for(var j=0;j<=8;j++)
        {
            uv.param[0]=i*.25;uv.param[1]=-1+j*.25;Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,2,2,0,evaluated));
            for(var a=0;a<=2;a++)for(var b=0;b<=2;b++)
            {var expected=Jet(uv.param[0],uv.param[1],a,b);for(var d=0;d<3;d++)Assert.InRange(Math.Abs(expected.coord[d]-evaluated[b*3+a].coord[d]),0,2e-10);}
        }
    }

    private static PK_VECTOR_s Jet(double u,double v,int a,int b)
    {
        var p=new PK_VECTOR_s();
        p.coord[0]=b==0?(a==0?u:a==1?1:0):0;p.coord[1]=a==0?(b==0?v:b==1?1:0):0;
        p.coord[2]=(a,b) switch{(0,0)=>u*u*v+u*v*v+.3*u*u*u-.2*v*v*v,(1,0)=>2*u*v+v*v+.9*u*u,
            (0,1)=>u*u+2*u*v-.6*v*v,(2,0)=>2*v+1.8*u,(0,2)=>2*u-1.2*v,(1,1)=>2*u+2*v,(2,1)=>2,(1,2)=>2,_=>0};return p;
    }
}
