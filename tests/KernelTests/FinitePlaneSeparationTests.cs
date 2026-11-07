global using BoundaryEventId=System.Int32;

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
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

    [Fact]
    public void DifferentPlaneEquationsCanCertifyOneNonrepresentableNativeLineRoot()
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var lateral=Math.Sqrt(7)/4;
        // Both planes meet the line at t=(lateral/2)/.75. Their normals
        // differ, and the exact root has no binary64 representation.
        Assert.NotEqual(0,Math.FusedMultiplyAdd((lateral*.5)/.75,.75,-lateral*.5));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=.75;
        plane.basis_set.axis.coord[1]=lateral;plane.basis_set.location.coord[1]=.5;plane.basis_set.ref_direction.coord[2]=1;
        SurfTag a,b,near;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&a));
        plane.basis_set.axis.coord[1]=0;plane.basis_set.location.coord[1]=0;plane.basis_set.ref_direction.coord[2]=0;
        plane.basis_set.axis.coord[2]=lateral;plane.basis_set.location.coord[2]=.5;plane.basis_set.ref_direction.coord[1]=1;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        plane.basis_set.location.coord[2]+=5e-11;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&near));
        Span<SurfTag> planes=stackalloc SurfTag[3]{a,b,near};Span<BoundaryEventId> ids=stackalloc BoundaryEventId[3];
        Assert.Equal(0,KernelRuntime.TestFiniteLinePlaneEventIdentities(carrier,-1,1,planes,ids,out var associations,requireProof:true));
        Assert.Equal(ids[0],ids[1]);Assert.NotEqual(ids[0],ids[2]);Assert.Equal(3,associations);
    }

    [Fact]
    public void OverlappingRootEnclosuresWithoutCommonEquationAreUnresolved()
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=.75;plane.basis_set.axis.coord[1]=Math.Sqrt(7)/4;
        plane.basis_set.location.coord[1]=.5;plane.basis_set.ref_direction.coord[2]=1;
        SurfTag a,b;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&a));
        plane.basis_set.location.coord[1]=Math.BitIncrement(.5);
        Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        Span<SurfTag> planes=stackalloc SurfTag[2]{a,b};Span<BoundaryEventId> ids=stackalloc BoundaryEventId[2];
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestFiniteLinePlaneEventIdentities(carrier,-1,1,planes,ids,out _,requireProof:true));
    }

    [Fact]
    public void CoincidentCarrierDoesNotIdentifyDistinctSourceEdgeOccurrences()
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        Span<BufferCount> fragmentCounts=stackalloc BufferCount[2];
        Assert.Equal(0,KernelRuntime.TestCoincidentSourceFragments(carrier,fragmentCounts,out var ambiguousCut));
        Assert.Equal(1,fragmentCounts[0]);Assert.Equal(1,fragmentCounts[1]);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,ambiguousCut);
    }

    [Fact]
    public void RepeatedDiscoveryOfOneIsolatedRootCanJoinButAnotherBranchCannot()
    {
        var circle=new PK_CIRCLE_sf_s{radius=1};circle.basis_set.axis.coord[2]=1;circle.basis_set.ref_direction.coord[0]=1;
        CurveTag carrier;Assert.Equal(0,KernelRuntime.CircleCreate(&circle,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;
        SurfTag equation;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&equation));
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestFiniteRootWitnessIdentity(carrier,equation,out var first,out var repeated));
        Assert.True(first>=0);Assert.Equal(first,repeated);
    }

    [Theory]
    [InlineData(1.5,4,false)]
    [InlineData(2.5,0,false)]
    [InlineData(2,0,true)]
    public void FiniteConicCylinderProviderDistinguishesSimpleEmptyAndTangentDomains(double radius,int expectedCount,bool unresolved)
    {
        var ellipse=new PK_ELLIPSE_sf_s{R1=2,R2=1};ellipse.basis_set.axis.coord[2]=1;ellipse.basis_set.ref_direction.coord[0]=1;
        CurveTag carrier;Assert.Equal(0,KernelRuntime.EllipseCreate(&ellipse,&carrier));
        var cylinder=new PK_CYL_sf_s{radius=radius,basis_set=ellipse.basis_set};SurfTag support;Assert.Equal(0,KernelRuntime.CylCreate(&cylinder,&support));
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[8];
        Span<double> errors=stackalloc double[8];
        var expected=unresolved?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
        Assert.Equal(expected,KernelRuntime.IsolateFiniteConicCylinder(new(){Curve=carrier,Start=0,End=Math.Tau},support,roots,isolation,errors,out var count));
        if(unresolved)return;
        Assert.Equal(expectedCount,count);if(count==0)return;
        var angle=Math.Acos(Math.Sqrt(5.0/12.0));Span<double> exact=stackalloc double[4]{angle,Math.PI-angle,Math.PI+angle,Math.Tau-angle};
        for(var i=0;i<count;i++)
        {
            Assert.True(isolation[i].Certified);Assert.InRange(exact[i],isolation[i].Root.Lower,isolation[i].Root.Upper);
            if(i>0)Assert.True(isolation[i-1].Root.Upper<isolation[i].Root.Lower);
        }
    }
    private static void Write(PK_VECTOR_s* target,KernelVector3 value){target->coord[0]=value.X;target->coord[1]=value.Y;target->coord[2]=value.Z;}
}
