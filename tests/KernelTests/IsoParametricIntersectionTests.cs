global using IntersectionCurveCode=System.Int32;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IsoParametricIntersectionTests:IDisposable
{
    public IsoParametricIntersectionTests()
    {KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public void TensorPatch_FindsAllThreeNonlinearBranchesInEitherDirection(bool rational,bool transpose)
    {
        var surface=Patch(0,rational,transpose);var plane=Plane();
        using var result=Intersect(surface,plane);
        Assert.Equal(0,result.PointCount);Assert.Equal(3,result.CurveCount);
        for(var branch=0;branch<3;branch++)
        {
            Assert.Equal(14651,result.Kinds[branch]);
            for(var sample=0;sample<=8;sample++)
            {
                var t=result.Intervals[branch].value[0]+(result.Intervals[branch].value[1]-result.Intervals[branch].value[0])*sample/8;
                var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(result.Curves[branch],t,0,&point));
                Assert.InRange(Math.Abs(point.coord[2]),0,1e-10);
                var p=new KernelVector3{X=point.coord[0],Y=point.coord[1],Z=point.coord[2]};
                Assert.Equal(ProjectGmKernel.Native.Computation.AlgorithmStatus.Success,
                    KernelRuntime.TryIntersectionPairedParameters(surface,plane,p,out _,out _));
            }
        }
    }

    [Fact]
    public void TensorSaddle_SplitsBothQuadraticCurvesAtTheCrossing()
    {
        using var result=Intersect(Patch(1,false,false),Plane());Assert.Equal(4,result.CurveCount);Assert.Equal(0,result.PointCount);
        var center=new[]{.5625,.5625,0};
        for(var i=0;i<4;i++)
        {
            var endpoint=new PK_VECTOR_s();var start=result.Intervals[i].value[0];var end=result.Intervals[i].value[1];
            var crossing=start>0?start:end;Assert.Equal(0,KernelRuntime.CurveEval(result.Curves[i],crossing,0,&endpoint));
            for(var c=0;c<3;c++)Assert.InRange(Math.Abs(endpoint.coord[c]-center[c]),0,1e-10);
        }
    }

    [Fact]
    public void TensorTangency_ReturnsOneTangentCurveAndDisjointReturnsNoCurves()
    {
        var plane=Plane();using(var tangent=Intersect(Patch(2,false,false),plane))
        {Assert.Equal(1,tangent.CurveCount);Assert.Equal(14652,tangent.Kinds[0]);}
        using(var disjoint=Intersect(Patch(3,false,false),plane)){Assert.Equal(0,disjoint.CurveCount);Assert.Equal(0,disjoint.PointCount);}
        using(var coincident=Intersect(Patch(4,false,false),plane)){Assert.Equal(0,coincident.CurveCount);Assert.Equal(0,coincident.PointCount);}
    }

    [Fact]
    public void CollapsedBoundaryAtSurfacePole_IsAnIsolatedPointInsteadOfAConstantCurve()
    {
        using var result=Intersect(Patch(6,false,false),Plane(true));
        Assert.Equal(0,result.CurveCount);Assert.Equal(1,result.PointCount);
        for(var c=0;c<3;c++)Assert.Equal(0,result.Points[0].coord[c]);
    }

    [Theory]
    [InlineData(1e-200)]
    [InlineData(1e200)]
    public void HomogeneousScale_DoesNotCollapseThreeCurvesIntoPoints(double scale)
    {
        using var ordinary=Intersect(Patch(0,true,false),Plane());
        using var scaled=Intersect(Patch(0,true,false,scale),Plane());
        Assert.Equal(3,scaled.CurveCount);Assert.Equal(0,scaled.PointCount);
        for(var i=0;i<3;i++)for(var sample=0;sample<=4;sample++)
        {
            var a=new PK_VECTOR_s();var b=new PK_VECTOR_s();var t=sample/4.0;
            Assert.Equal(0,KernelRuntime.CurveEval(ordinary.Curves[i],t,0,&a));
            Assert.Equal(0,KernelRuntime.CurveEval(scaled.Curves[i],t,0,&b));
            for(var c=0;c<3;c++)Assert.InRange(Math.Abs(a.coord[c]-b.coord[c]),0,1e-10);
        }
    }

    [Fact]
    public void NearSeparablePatch_IsRejectedInsteadOfSilentlyChangingItsZeroSet()
    {
        var surface=Patch(5,false,false);var plane=Plane();var result=new Result();var options=Options();
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfIntersectSurf(surface,plane,&options,
            &result.PointCount,&result.Points,&result.CurveCount,&result.Curves,&result.Intervals,&result.Kinds));
        Assert.Equal(0,result.CurveCount);Assert.Equal(0,result.PointCount);Assert.True(result.Curves==null&&result.Points==null);
    }

    [Fact]
    public void ExactBranchesOwnTheirGeometryAndFailuresRollbackAllAllocations()
    {
        var surface=Patch(0,true,false);var plane=Plane();var baseline=KernelRuntime.Curves.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        for(var fault=0;fault<20;fault++)
        {
            var result=new Result();var options=Options();KernelRuntime.InjectAllocationFailure(fault);
            var error=KernelRuntime.SurfIntersectSurf(surface,plane,&options,&result.PointCount,&result.Points,&result.CurveCount,&result.Curves,&result.Intervals,&result.Kinds);
            KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){for(var i=0;i<result.CurveCount;i++)Assert.Equal(0,KernelRuntime.EntityDelete(1,result.Curves+i));result.Dispose();}
            else{Assert.Equal(0,result.CurveCount);Assert.Equal(0,result.PointCount);Assert.True(result.Points==null&&result.Curves==null&&result.Intervals==null&&result.Kinds==null);}
            Assert.Equal(baseline,KernelRuntime.Curves.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
        }
        using var owned=Intersect(surface,plane);Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));Assert.Equal(0,KernelRuntime.EntityDelete(1,&plane));
        var p=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(owned.Curves[0],.4,0,&p));Assert.InRange(Math.Abs(p.coord[2]),0,1e-10);
    }

    private static int Patch(int mode,bool rational,bool transpose,double homogeneousScale=1)
    {
        var pu=2;var pv=mode==0||mode==5?3:2;var dim=rational?4:3;
        double* poles=stackalloc double[(pu+1)*(pv+1)*dim];
        double* zc=stackalloc double[4]{-.09375,.13541666666666666,-.13541666666666666,.09375};
        for(var u=0;u<=pu;u++)for(var v=0;v<=pv;v++)
        {
            var w=rational?1+v*.5:1;var at=(transpose?v*(pu+1)+u:u*(pv+1)+v)*dim;
            var x=u/(double)pu+(mode==1?.25*v*(v-1)/(pv*(pv-1)):0);
            var y=v/(double)pv+.25*u*(u-1)/(pu*(pu-1));
            var z=mode switch{0 or 5=>zc[v],1=>(u/(double)pu-.5)*(v/(double)pv-.5),2=>v==1?-.25:.25,3=>.25+v*(v-1)/(double)(pv*(pv-1)),_=>0};
            if(mode==6){y=u*v/4.0;z=u==2?1:0;}
            if(mode==5&&u==1&&v==1)z+=1e-9;
            poles[at]=x*w*homogeneousScale;poles[at+1]=y*w*homogeneousScale;poles[at+2]=z*homogeneousScale;if(rational)poles[at+3]=w*homogeneousScale;
        }
        double* uk=stackalloc double[2]{0,1};double* vk=stackalloc double[2]{0,1};
        int* um=stackalloc int[2]{(transpose?pv:pu)+1,(transpose?pv:pu)+1};int* vm=stackalloc int[2]{(transpose?pu:pv)+1,(transpose?pu:pv)+1};
        var sf=new PK_BSURF_sf_s{u_degree=transpose?pv:pu,v_degree=transpose?pu:pv,n_u_vertices=(transpose?pv:pu)+1,n_v_vertices=(transpose?pu:pv)+1,
            vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=um,v_knot_mult=vm,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        int tag=0;Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&tag));return tag;
    }

    private static int Plane(bool vertical=false)
    {var sf=new PK_PLANE_sf_s();sf.basis_set.axis.coord[vertical?0:2]=1;sf.basis_set.ref_direction.coord[vertical?1:0]=1;int tag=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&tag));return tag;}
    private static PK_SURF_intersect_surf_o_s Options()=>new(){o_t_version=3,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c,tolerance=1e-5};
    private static Result Intersect(int surface,int plane)
    {var result=new Result();var options=Options();Assert.Equal(0,KernelRuntime.SurfIntersectSurf(surface,plane,&options,&result.PointCount,&result.Points,&result.CurveCount,&result.Curves,&result.Intervals,&result.Kinds));return result;}
    private struct Result:IDisposable
    {
        internal BufferCount PointCount,CurveCount;internal PK_VECTOR_s* Points;internal EntityTag* Curves;internal PK_INTERVAL_s* Intervals;internal IntersectionCurveCode* Kinds;
        public void Dispose(){if(Points!=null)Assert.Equal(0,KernelRuntime.MemoryFree(Points));if(Curves!=null)Assert.Equal(0,KernelRuntime.MemoryFree(Curves));if(Intervals!=null)Assert.Equal(0,KernelRuntime.MemoryFree(Intervals));if(Kinds!=null)Assert.Equal(0,KernelRuntime.MemoryFree(Kinds));}
    }
}
