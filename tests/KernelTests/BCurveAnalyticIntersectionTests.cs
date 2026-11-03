using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BCurveAnalyticIntersectionTests:IDisposable
{
    public BCurveAnalyticIntersectionTests()
    {KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(0,6)]
    [InlineData(1,6)]
    [InlineData(2,6)]
    [InlineData(3,12)]
    public void CubicAllBranches_AreEnumeratedAndHaveIndependentUv(int kind,int expected)
    {
        var curve=Oscillating(kind,false);var surface=Surface(kind);
        using var result=Intersect(surface,curve,0,1);Assert.Equal(expected,result.Count);
        for(var i=0;i<result.Count;i++)
        {if(i>0)Assert.True(result.Parameters[i]>result.Parameters[i-1]);Assert.Equal(14611,result.Kinds[i]);CheckUv(surface,curve,result,i);}
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RationalCubic_AllSpanRootsRemainOnBothSupports(int kind)
    {
        var curve=Oscillating(kind,true);var surface=Surface(kind);
        using var result=Intersect(surface,curve,0,1);Assert.True(result.Count>=2);
        for(var i=0;i<result.Count;i++)CheckUv(surface,curve,result,i);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LinearSpline_TangentIsOneContact(int kind)
    {
        var radius=kind==3?4:2;var curve=Curve(1,[-3,radius,0,3,radius,0],[0,1],[2,2]);
        var surface=Surface(kind,true);using var result=Intersect(surface,curve,0,1);
        Assert.Equal(1,result.Count);Near(.5,result.Parameters[0]);Assert.Equal(14612,result.Kinds[0]);CheckUv(surface,curve,result,0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RationalCircleCoincidence_MergesTheTwoSpanMarkers(int kind)
    {
        var w=Math.Sqrt(.5);var curve=Curve(2,[2,0,0,1,2*w,2*w,0,w,0,2,0,1,-2*w,2*w,0,w,-2,0,0,1],[0,1,2],[3,2,3],true);
        var surface=Surface(kind,true);using var result=Intersect(surface,curve,.25,1.75);
        Assert.Equal(2,result.Count);Near(.25,result.Parameters[0]);Near(1.75,result.Parameters[1]);
        Assert.Equal(14613,result.Kinds[0]);Assert.Equal(14614,result.Kinds[1]);
        for(var i=0;i<2;i++){Near(-1,result.Uv[i].param[0]);Near(-1,result.Uv[i].param[1]);}
    }

    [Fact]
    public void KnotBoundaryRoot_IsReturnedOnceAndPartialIntervalFiltersEverySpan()
    {
        var curve=Curve(1,[-3,0,0,2,0,0,3,0,0,0,0,0],[0,1,2,3],[2,1,1,2]);var sphere=Surface(0,true);
        using(var result=Intersect(sphere,curve,0,3))
        {Assert.Equal(3,result.Count);Near(.2,result.Parameters[0]);Near(1,result.Parameters[1]);Near(7.0/3,result.Parameters[2]);}
        using(var result=Intersect(sphere,curve,.5,2.5))
        {Assert.Equal(2,result.Count);Near(1,result.Parameters[0]);Near(7.0/3,result.Parameters[1]);}
    }

    [Fact]
    public void ConeFarNappe_IsExcludedFromBothIsolatedAndCoincidentResults()
    {
        var definition=new PK_CONE_sf_s{basis_set=Axis(),radius=1,semi_angle=Math.Atan(.5)};int cone=0;Assert.Equal(0,KernelRuntime.ConeCreate(&definition,&cone));
        var far=Curve(1,[-3,0,-4,3,0,-4],[0,1],[2,2]);using(var result=Intersect(cone,far,0,1))Assert.Equal(0,result.Count);
        var generator=Curve(1,[1,0,0,2,0,2],[0,1],[2,2]);using(var result=Intersect(cone,generator,0,1))
        {Assert.Equal(2,result.Count);Near(0,result.Parameters[0]);Near(1,result.Parameters[1]);Assert.Equal(14613,result.Kinds[0]);Assert.Equal(14614,result.Kinds[1]);}
        var apex=Curve(1,[-1,0,-4,1,0,0],[0,1],[2,2]);var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};int n=0;PK_VECTOR_s* p=null;PK_UV_s* uv=null;double* t=null;int* types=null;
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.SurfIntersectCurve(cone,apex,Range(0,1),&options,&n,&p,&uv,&t,&types));
        Assert.Equal(0,n);Assert.True(p==null&&uv==null&&t==null&&types==null);
    }

    [Fact]
    public void TrimmedCurve_MapsReturnedParametersAndLeavesTheBasisAlive()
    {
        var basis=Curve(1,[-3,0,0,3,0,0],[0,1],[2,2]);var definition=new PK_TRCURVE_sf_s{basis_curve=basis,t_int=Range(.1,.9)};int trim=0;
        Assert.Equal(0,KernelRuntime.TrCurveCreate(&definition,&trim));using var result=Intersect(Surface(0,true),trim,0,.8);
        Assert.Equal(2,result.Count);Near(1.0/6-.1,result.Parameters[0]);Near(5.0/6-.1,result.Parameters[1]);Assert.True(KernelRuntime.IsValidTag(basis));
    }

    [Fact]
    public void UnsupportedDegree_ReturnsExplicitFailureAndClearedOutputs()
    {
        var poles=new double[18];for(var i=0;i<6;i++)poles[3*i]=i;
        var curve=Curve(5,poles,[0,1],[6,6]);var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};
        int n=7;PK_VECTOR_s* points=(PK_VECTOR_s*)1;PK_UV_s* uv=(PK_UV_s*)1;double* t=(double*)1;int* kinds=(int*)1;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfIntersectCurve(Surface(3),curve,Range(0,1),&options,&n,&points,&uv,&t,&kinds));
        Assert.Equal(0,n);Assert.True(points==null&&uv==null&&t==null&&kinds==null);
    }

    [Fact]
    public void ExtremeAxialScale_CannotTurnUnderflowIntoFalseCylinderCoincidence()
    {
        double[] poles=[1,0,1e200,2,0,1e200,1,0,1e200];double[] knots=[0,0,0,1,1,1];
        var view=new BCurveView(2,3,false,false,poles,knots);
        var surface=new AnalyticSurface(SurfaceClass.Cylinder,default,new KernelVector3{Z=1},new KernelVector3{X=1},1);
        Span<OperationIntersectionPoint> result=stackalloc OperationIntersectionPoint[16];
        Assert.Equal(AlgorithmStatus.NumericalFailure,BCurveAnalyticIntersection.Find(view,surface,0,1,result,out var count));Assert.Equal(0,count);
    }

    [Fact]
    public void LargeParameterOrigin_DoesNotMoveTheCenteredSpanPastItsEndpoint()
    {
        var start=Math.ScaleB(1,53);var end=start+2;double[] poles=[-3,0,0,-2,0,0];double[] knots=[start,start,end,end];
        var view=new BCurveView(1,3,false,false,poles,knots);var surface=new AnalyticSurface(SurfaceClass.Sphere,default,new KernelVector3{Z=1},new KernelVector3{X=1},2);
        Span<OperationIntersectionPoint> result=stackalloc OperationIntersectionPoint[4];
        Assert.Equal(AlgorithmStatus.Success,BCurveAnalyticIntersection.Find(view,surface,start,end,result,out var count));
        Assert.Equal(1,count);Assert.Equal(end,result[0].First);Near(-2,result[0].Point.X);
    }

    [Fact]
    public void ReturnAllocationFailure_ReleasesEveryBufferWithoutChangingGeometry()
    {
        var curve=Oscillating(0,false);var surface=Surface(0);var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};
        var outstanding=KernelRuntime.ReturnOutstandingBlocks;var curves=KernelRuntime.Curves.AliveCount;
        for(var fault=0;fault<8;fault++)
        {
            int n=-1;PK_VECTOR_s* points=(PK_VECTOR_s*)1;PK_UV_s* uv=(PK_UV_s*)1;double* t=(double*)1;int* kinds=(int*)1;
            KernelRuntime.InjectAllocationFailure(fault);var error=KernelRuntime.SurfIntersectCurve(surface,curve,Range(0,1),&options,&n,&points,&uv,&t,&kinds);KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){Free(points);Free(uv);Free(t);Free(kinds);}
            else{Assert.Equal(0,n);Assert.True(points==null&&uv==null&&t==null&&kinds==null);}
            Assert.Equal(outstanding,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);
        }
    }

    [Fact]
    public void PublicNurbsIntersection_HasNoManagedAllocationAfterWarmup()
    {
        var curve=Oscillating(1,false);var surface=Surface(1);var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};var error=0;long before=0;
        for(var i=0;i<51;i++)
        {
            if(i==1)before=GC.GetAllocatedBytesForCurrentThread();int n=0;PK_VECTOR_s* points=null;PK_UV_s* uv=null;double* t=null;int* kinds=null;
            error|=KernelRuntime.SurfIntersectCurve(surface,curve,Range(0,1),&options,&n,&points,&uv,&t,&kinds);if(n!=6)error|=1;
            error|=KernelRuntime.MemoryFree(points);error|=KernelRuntime.MemoryFree(uv);error|=KernelRuntime.MemoryFree(t);error|=KernelRuntime.MemoryFree(kinds);
        }
        var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,bytes);
    }

    private static int Oscillating(int kind,bool rational)
    {
        var dim=rational?4:3;var poles=new double[dim*4];double[] wave=[-8,14,-14,8];
        for(var i=0;i<4;i++)
        {
            var w=rational?i%3+1:1;var t=i/3.0;
            if(kind==1){poles[dim*i]=t*w;poles[dim*i+1]=wave[i];}
            else{poles[dim*i]=wave[i];poles[dim*i+1]=.01*t*w;}
            if(kind==0)poles[dim*i+2]=0;if(rational)poles[dim*i+3]=w;
        }
        return Curve(3,poles,[0,1],[4,4],rational);
    }
    private static int Surface(int kind,bool large=false)
    {
        int tag=0;var axis=Axis();
        if(kind==0){var sf=new PK_SPHERE_sf_s{basis_set=axis,radius=large?2:.5};Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&tag));}
        if(kind==1){if(!large){axis.axis.coord[0]=1;axis.axis.coord[2]=0;axis.ref_direction.coord[0]=0;axis.ref_direction.coord[1]=1;}var sf=new PK_CYL_sf_s{basis_set=axis,radius=large?2:.5};Assert.Equal(0,KernelRuntime.CylCreate(&sf,&tag));}
        if(kind==2){var sf=new PK_CONE_sf_s{basis_set=axis,radius=large?2:.5,semi_angle=Math.Atan(.5)};Assert.Equal(0,KernelRuntime.ConeCreate(&sf,&tag));}
        if(kind==3){var sf=new PK_TORUS_sf_s{basis_set=axis,major_radius=large?3:.6,minor_radius=large?1:.2};Assert.Equal(0,KernelRuntime.TorusCreate(&sf,&tag));}
        return tag;
    }
    private static int Curve(int degree,double[] poles,double[] knots,int[] multiplicities,bool rational=false)
    {
        fixed(double* p=poles,k=knots)fixed(int* m=multiplicities)
        {
            var sf=new PK_BCURVE_sf_s{degree=degree,n_vertices=poles.Length/(rational?4:3),vertex_dim=rational?4:3,is_rational=(byte)(rational?1:0),vertex=p,
                n_knots=knots.Length,knot=k,knot_mult=m,form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_unset_c};
            int tag=0;Assert.Equal(0,KernelRuntime.BCurveCreate(&sf,&tag));return tag;
        }
    }
    private static PointResult Intersect(int surface,int curve,double lo,double hi)
    {
        var r=new PointResult();var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};
        delegate* unmanaged<int,int,PK_INTERVAL_s,PK_SURF_intersect_curve_o_s*,int*,PK_VECTOR_s**,PK_UV_s**,double**,int**,int> invoke=&KernelExports.PK_SURF_intersect_curve;
        Assert.Equal(0,invoke(surface,curve,Range(lo,hi),&options,&r.Count,&r.Points,&r.Uv,&r.Parameters,&r.Kinds));return r;
    }
    private static void CheckUv(int surface,int curve,PointResult result,int i)
    {
        var p=new PK_VECTOR_s();var q=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,result.Uv[i],0,0,0,&p));Assert.Equal(0,KernelRuntime.CurveEval(curve,result.Parameters[i],0,&q));
        for(var d=0;d<3;d++){Near(result.Points[i].coord[d],p.coord[d],2e-8);Near(result.Points[i].coord[d],q.coord[d],2e-8);}
    }
    private static PK_AXIS2_sf_s Axis(){var a=new PK_AXIS2_sf_s();a.axis.coord[2]=1;a.ref_direction.coord[0]=1;return a;}
    private static PK_INTERVAL_s Range(double a,double b){var r=new PK_INTERVAL_s();r.value[0]=a;r.value[1]=b;return r;}
    private static void Near(double expected,double actual,double tolerance=2e-9)=>Assert.InRange(Math.Abs(actual-expected),0,tolerance);
    private static void Free(void* p){if(p!=null)Assert.Equal(0,KernelRuntime.MemoryFree(p));}
    private struct PointResult:IDisposable
    {internal int Count;internal PK_VECTOR_s* Points;internal PK_UV_s* Uv;internal double* Parameters;internal int* Kinds;
        public void Dispose(){Free(Points);Free(Uv);Free(Parameters);Free(Kinds);}}
}
