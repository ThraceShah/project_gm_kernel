using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendIntersectionTests:IDisposable
{
    public BlendIntersectionTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Fact]
    public void PublicBlendPlaneIntersectionReturnsAnExactProceduralArc()
    {
        var blend=CreateBlend();var plane=new PK_PLANE_sf_s();plane.basis_set.location.coord[1]=.5;plane.basis_set.axis.coord[1]=1;plane.basis_set.ref_direction.coord[0]=1;int cut;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&cut));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1};options.uvbox_1.param[0]=.1;options.uvbox_1.param[1]=.1;options.uvbox_1.param[2]=.9;options.uvbox_1.param[3]=.9;
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(blend,cut,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        try
        {
            Assert.Equal(0,np);Assert.Equal(1,nc);
            for(var i=0;i<=12;i++)
            {
                var t=intervals[0].value[0]+(intervals[0].value[1]-intervals[0].value[0])*i/12;var p=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curves[0],t,0,&p));
                Assert.InRange(Math.Abs(p.coord[1]-.5),0,1e-9);Assert.InRange(Math.Abs(p.coord[0]*p.coord[0]+p.coord[2]*p.coord[2]-.04),0,1e-9);
            }
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(intervals!=null)KernelRuntime.MemoryFree(intervals);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }

    [Fact]
    public void IntervalGraphDiscoveryWorksDirectlyOnTheExactBlendDefinition()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        IntervalNumber[] poles=[-1,.5,-1,1,-1,.5,1,1,1,.5,-1,1,1,.5,1,1];
        var first=new SurfaceIntersectionPatch(program,4,new(.1,.9,.1,.9));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(0,1,0,1)));
        var nodes=new SurfaceIntersectionNode[8192];var edges=new SurfaceIntersectionSegment[8192];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,ParametricSurfaceIntersection.FindProcedural(first,second,nodes,ref nn,edges,ref ne));Assert.True(ne>0);
        for(var i=0;i<nn;i++)Assert.InRange(Math.Abs(nodes[i].Point.Y-.5),0,1e-8);
    }

    [Fact]
    public void ArtificialSpineLimitCannotBeReportedAsACompletedIntersectionEnd()
    {
        var blend=CreateBlend(artificial:true);var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;plane.basis_set.location.coord[0]=-.1;int cut;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&cut));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c};int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.SurfIntersectSurf(blend,cut,&options,&np,&points,&nc,&curves,&intervals,&kinds));Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(curves==null);
    }

    internal static int CreateBlend(bool artificial=false)
    {
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;int x;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&x));
        plane=new();plane.basis_set.axis.coord[2]=1;plane.basis_set.ref_direction.coord[0]=1;int z;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&z));
        var surfaces=NestedBlendSeriesTests.Surfaces();var spines=NestedBlendSeriesTests.Spines();if(artificial){spines[0].StartType=spines[0].EndType=LimitType.Artificial;}spines[0].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;spines[0].First=1;spines[0].Second=0;surfaces[4].ReverseSection=true;
        var program=new NestedBlendProgram(surfaces,spines,NestedBlendSeriesTests.Charts());int spine;Assert.Equal(0,KernelRuntime.BindNestedBlendSpine(z,x,program,0,&spine));
        plane=new();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;plane.basis_set.location.coord[0]=-.2;int a;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&a));
        plane=new();plane.basis_set.axis.coord[2]=1;plane.basis_set.ref_direction.coord[0]=1;plane.basis_set.location.coord[2]=-.2;int b;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        int blend;Assert.Equal(0,KernelRuntime.BindNestedBlend(a,b,spine,program,4,&blend));return blend;
    }
}
