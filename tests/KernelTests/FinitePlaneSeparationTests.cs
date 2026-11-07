global using BoundaryEventId=System.Int32;

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class FinitePlaneSeparationTests:IDisposable
{
    public FinitePlaneSeparationTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NearParallelInfiniteIntersectionDoesNotObscureCertifiedFiniteSeparation(bool separated)
    {
        var normal=Vector(-.361615431964962,.932327345606034,0);var x=Vector(.932327345606034,.361615431964962,0);var y=Vector(0,0,1);
        var definition=new PK_PLANE_sf_s();Write(&definition.basis_set.axis,normal);Write(&definition.basis_set.ref_direction,x);int source;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&source));
        Span<KernelRuntime.ModelingEdge> edges=stackalloc KernelRuntime.ModelingEdge[4];Span<KernelRuntime.ModelingCoedge> coedges=stackalloc KernelRuntime.ModelingCoedge[4];
        Span<KernelVector3> points=stackalloc KernelVector3[4];points[0]=Add(Scale(x,-2),Scale(y,-1));points[1]=Add(Scale(x,2),Scale(y,-1));points[2]=Add(Scale(x,2),y);points[3]=Add(Scale(x,-2),y);
        for(var i=0;i<4;i++)
        {
            var delta=Sub(points[(i+1)%4],points[i]);var length=Math.Sqrt(Dot(delta,delta));var line=new PK_LINE_sf_s();Write(&line.basis_set.location,points[i]);Write(&line.basis_set.axis,Scale(delta,1/length));int curve;
            Assert.Equal(0,KernelRuntime.LineCreate(&line,&curve));edges[i]=new(){Curve=curve,Start=0,End=length,StartVertex=i,EndVertex=(i+1)%4};coedges[i]=new(){Edge=i,Forward=1};
        }
        var other=new AnalyticSurface(SurfaceClass.Plane,separated?Scale(normal,2.55):default,Vector(.361615431964962,-.9323273456060345,0),x);
        Assert.Equal(separated,KernelRuntime.PlanarLoopSeparatedFromPlane(source,edges,coedges,other,1e-8,out _));
    }

    [Theory]
    [InlineData(5e-9)] [InlineData(5e-11)]
    public void DistinctCertifiedRootsInsideTheSpatialCandidateRadiusKeepTheirOwnEvents(double separation)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;
        SurfTag a,b,equivalent;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&a));
        plane.basis_set.location.coord[1]=1;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&equivalent));
        plane.basis_set.location.coord[0]=separation;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        Span<SurfTag> planes=stackalloc SurfTag[4]{a,b,b,equivalent};Span<BoundaryEventId> ids=stackalloc BoundaryEventId[4];
        Assert.Equal(0,KernelRuntime.TestFiniteLinePlaneEventIdentities(carrier,-1,1,planes,ids,out var associations));
        Assert.NotEqual(ids[0],ids[1]);Assert.Equal(ids[0],ids[3]);Assert.Equal(ids[1],ids[2]);
        Assert.Equal(3,associations);
    }
    private static void Write(PK_VECTOR_s* target,KernelVector3 value){target->coord[0]=value.X;target->coord[1]=value.Y;target->coord[2]=value.Z;}
}
