using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curves[0])];var data=KernelRuntime.ICurveDataPool[record.DataIndex];
            Assert.Equal(IntersectionUvType.Both,data.UvType);Assert.Equal(4*data.ChartCount,data.UvValueCount);
            var witnesses=new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.UvValueBlock),data.UvValueCount);
            var hulls=new ReadOnlySpan<KernelVector3>(KernelRuntime.DereferenceBlock(data.HvecBlock),data.HvecCount);
            PK_VECTOR_s witnessPoint;var uv=new PK_UV_s();
            for(var chart=0;chart<data.ChartCount;chart++)for(var side=0;side<2;side++)
            {
                var support=side==0?data.Surface0Tag:data.Surface1Tag;uv.param[0]=witnesses[4*chart+2*side];uv.param[1]=witnesses[4*chart+2*side+1];
                Assert.Equal(0,KernelRuntime.SurfEval(support,uv,0,0,0,&witnessPoint));
                var position=new KernelVector3{X=witnessPoint.coord[0],Y=witnessPoint.coord[1],Z=witnessPoint.coord[2]};
                Assert.InRange(OperationIntersection.Norm(EvaluationMath.Sub(position,hulls[chart+1])),0,1e-9);
            }
            for(var i=0;i<=12;i++)
            {
                var t=intervals[0].value[0]+(intervals[0].value[1]-intervals[0].value[0])*i/12;var p=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curves[0],t,0,&p));
                Assert.InRange(Math.Abs(p.coord[1]-.5),0,1e-9);Assert.InRange(Math.Abs(p.coord[0]*p.coord[0]+p.coord[2]*p.coord[2]-.04),0,1e-9);
            }
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(intervals!=null)KernelRuntime.MemoryFree(intervals);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterestBoxesPreserveWholeBranchesBeyondTheRequestedRegion(bool reverse)
    {
        var blend=CreateBlend();var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[1]=.5;
        definition.basis_set.axis.coord[1]=1;definition.basis_set.ref_direction.coord[0]=1;int plane;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1,have_uvbox_2=1,have_box=1};
        options.uvbox_1.param[0]=options.uvbox_1.param[1]=.4;options.uvbox_1.param[2]=options.uvbox_1.param[3]=.6;
        options.uvbox_2=options.uvbox_1;
        for(var d=0;d<3;d++){options.box.coord[d]=-1;options.box.coord[d+3]=1;}
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(reverse?plane:blend,reverse?blend:plane,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        try
        {
            Assert.Equal(0,np);Assert.Equal(1,nc);var first=new PK_VECTOR_s();var last=new PK_VECTOR_s();
            Assert.Equal(0,KernelRuntime.CurveEval(curves[0],intervals[0].value[0],0,&first));
            Assert.Equal(0,KernelRuntime.CurveEval(curves[0],intervals[0].value[1],0,&last));
            Assert.InRange(Math.Min(Math.Abs(first.coord[0]),Math.Abs(last.coord[0])),0,1e-8);
            Assert.InRange(Math.Max(Math.Abs(first.coord[0]),Math.Abs(last.coord[0])),.2-1e-8,.2+1e-8);
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(intervals!=null)KernelRuntime.MemoryFree(intervals);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }

    [Fact]
    public void AnInterestBoxCannotTurnAnArtificialSpineLimitIntoAGeometryBoundary()
    {
        var blend=CreateBlend(artificial:true);var definition=new PK_PLANE_sf_s();definition.basis_set.axis.coord[0]=1;
        definition.basis_set.ref_direction.coord[1]=1;definition.basis_set.location.coord[0]=-.1;int plane;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1};options.uvbox_1.param[2]=options.uvbox_1.param[3]=1;
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.SurfIntersectSurf(blend,plane,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(points==null&&curves==null&&intervals==null&&kinds==null);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)]
    public void InvalidInterestDomainsFailWithoutPublishingGeometry(double upper)
    {
        var blend=CreateBlend();var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1};
        options.uvbox_1.param[2]=upper;options.uvbox_1.param[3]=1;var surfaces=KernelRuntime.Surfaces.AliveCount;var curvesBefore=KernelRuntime.Curves.AliveCount;
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,KernelRuntime.SurfIntersectSurf(blend,blend,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(points==null&&curves==null&&intervals==null&&kinds==null);
        Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(curvesBefore,KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void DegenerateInterestRegionsAreExplicitlyUnsupported()
    {
        var blend=CreateBlend();var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1};
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfIntersectSurf(blend,blend,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(points==null&&curves==null&&intervals==null&&kinds==null);
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
    public void IndependentBlendProgramsIgnorePaddingButRequireIdenticalGeometry()
    {
        var surfaces=NestedBlendSeriesTests.Surfaces();var copy=NestedBlendSeriesTests.Surfaces();
        var spines=NestedBlendSeriesTests.Spines();var charts=NestedBlendSeriesTests.Charts();
        var original=new NestedBlendProgram(surfaces,spines,charts);
        var duplicate=new NestedBlendProgram(copy,spines,charts);
        var index=Array.FindIndex(copy,surface=>surface.IsBlend);Assert.True(index>=0);
        ref var record=ref copy[index];ref var start=ref Unsafe.As<BlendSurfaceInstruction,byte>(ref record);
        var flag=Unsafe.ByteOffset(ref start,ref Unsafe.As<bool,byte>(ref record.ReverseSection));
        var sense=Unsafe.ByteOffset(ref start,ref Unsafe.As<double,byte>(ref record.NormalSense));
        Assert.True(sense>flag+1);
        MemoryMarshal.AsBytes(copy.AsSpan(index,1))[checked((int)(flag+1))]^=0xff;
        Assert.True(KernelRuntime.TestSameBlendSupportProgram(original,duplicate));
        record.Range0=Math.BitIncrement(record.Range0);
        Assert.False(KernelRuntime.TestSameBlendSupportProgram(original,duplicate));
    }

    [Fact]
    public void NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative()
    {
        var blend=CreateBlend();var plane=new PK_PLANE_sf_s();var length=Math.Sqrt(17);
        plane.basis_set.location.coord[1]=.5;plane.basis_set.axis.coord[0]=1/length;plane.basis_set.axis.coord[1]=4/length;plane.basis_set.ref_direction.coord[2]=1;int cut;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&cut));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_both_c,have_uvbox_1=1};
        options.uvbox_1.param[0]=options.uvbox_1.param[1]=.1;options.uvbox_1.param[2]=options.uvbox_1.param[3]=.9;
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* intervals;int* kinds;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(blend,cut,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        try
        {
            Assert.Equal(1,nc);var data=KernelRuntime.ICurveDataPool[KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curves[0])].DataIndex];
            var program=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            Span<IntervalNumber> result=stackalloc IntervalNumber[2];
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineSupportU(program,data.ProgramSpine,intervals[0].value[0],intervals[0].value[1],0,result));
            PK_VECTOR_s* jets=stackalloc PK_VECTOR_s[2];
            for(var sample=0;sample<=8;sample++)
            {
                var parameter=intervals[0].value[0]+(intervals[0].value[1]-intervals[0].value[0])*sample/8;
                Assert.Equal(0,KernelRuntime.CurveEval(curves[0],parameter,1,jets));
                Assert.InRange(jets[0].coord[1],result[0].Lower,result[0].Upper);
                Assert.InRange(jets[1].coord[1],result[1].Lower,result[1].Upper);
                Assert.InRange(Math.Abs(jets[0].coord[1]+jets[0].coord[0]/4-.5),0,1e-12);
                Assert.InRange(Math.Abs(jets[1].coord[1]+jets[1].coord[0]/4),0,1e-12);
            }
            Assert.Equal(AlgorithmStatus.Unsupported,JointNestedBlendEvaluation.BoundSpineSupportU(program,data.ProgramSpine,intervals[0].value[0],intervals[0].value[1],1,result));
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(intervals!=null)KernelRuntime.MemoryFree(intervals);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
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
