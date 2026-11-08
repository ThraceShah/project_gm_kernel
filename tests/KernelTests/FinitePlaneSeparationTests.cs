global using BoundaryEventId=System.Int32;

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;
using BodyTag=System.Int32;
using FaceSlot=System.Int32;

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

    [Theory]
    [InlineData(2.5,3,0)] // The exact plane crossing misses the finite side.
    [InlineData(1.5,.5,0)] // The actual curve trim ends before the plane.
    [InlineData(1.5,3,ParasolidConstants.PK_ERROR_not_implemented)]
    public void GuardedThirdFaceChecksTheActualCurveAndFaceTrims(double y,double end,int expected)
    {
        BodyTag body;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(2,2,2,null,&body));
        var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];FaceSlot side=-1;
        var face=record.FirstFaceBody;
        for(var i=0;i<record.FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            PK_PLANE_sf_s plane;Assert.Equal(0,KernelRuntime.PlaneAsk(KernelRuntime.Faces[face].SurfTag,&plane));
            if(plane.basis_set.location.coord[0]==1&&Math.Abs(plane.basis_set.axis.coord[0])==1){side=face;break;}
        }
        Assert.True(side>=0);
        var line=new PK_LINE_sf_s();var diagonal=1/Math.Sqrt(2);
        Write(&line.basis_set.axis,Vector(diagonal,-diagonal,0));Write(&line.basis_set.location,Vector(0,y,1));
        CurveTag carrier;Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        Assert.Equal(expected,KernelRuntime.TestFiniteThirdFaceEvents(body,side,carrier,0,end,out var events));
        Assert.Equal(0,events);
    }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(.75,0,ParasolidConstants.PK_ERROR_not_implemented)]
    [InlineData(-.75,.5,ParasolidConstants.PK_ERROR_not_implemented)] // A horizontal ray would hit the two inner vertices.
    [InlineData(.5,0,ParasolidConstants.PK_ERROR_cant_do_intersect)]
    public void ThirdFaceWindingExcludesARootInsideAnActualInnerLoop(double x,double y,int expected)
    {
        BodyTag body,tool;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(4,4,2,null,&body));
        var basis=new PK_AXIS2_sf_s();
        Write(&basis.location,Vector(0,0,-1));Write(&basis.axis,Vector(0,0,1));Write(&basis.ref_direction,Vector(1,0,0));
        Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(1,1,4,&basis,&tool));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        PK_TOPOL_track_r_s track=default;PK_boolean_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodyBoolean2(body,1,&tool,&options,&track,&result));
        Assert.Equal(1,result.n_bodies);
        body=result.bodies[0];KernelRuntime.TopolTrackResultFree(&track);KernelRuntime.BooleanResultFree(&result);

        var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];FaceSlot top=-1;
        var face=record.FirstFaceBody;
        for(var i=0;i<record.FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var occurrence=KernelRuntime.Faces[face];PK_PLANE_sf_s plane;Assert.Equal(0,KernelRuntime.PlaneAsk(occurrence.SurfTag,&plane));
            if(occurrence.LoopCount==2&&plane.basis_set.location.coord[2]==2&&Math.Abs(plane.basis_set.axis.coord[2])==1){top=face;break;}
        }
        Assert.True(top>=0);
        var line=new PK_LINE_sf_s();Write(&line.basis_set.axis,Vector(0,0,1));Write(&line.basis_set.location,Vector(x,y,1));
        CurveTag carrier;Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var error=KernelRuntime.TestFiniteThirdFaceEvents(body,top,carrier,0,2,out var events);
        Assert.Equal(expected,error);Assert.Equal(0,events);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public void ThreePlaneCutsShareOneEventOnlyWhenAllFiniteCarriersProveIt(bool truncate,bool displaced)
    {
        Span<SurfTag> surfaces=stackalloc SurfTag[3];
        Span<CurveTag> carriers=stackalloc CurveTag[3];
        Span<KernelVector3> axes=stackalloc KernelVector3[3]{Vector(1,0,0),Vector(0,1,0),Vector(0,0,1)};
        for(var i=0;i<3;i++)
        {
            var plane=new PK_PLANE_sf_s();
            Write(&plane.basis_set.axis,axes[i]);Write(&plane.basis_set.ref_direction,axes[(i+1)%3]);
            SurfTag tag;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&tag));surfaces[i]=tag;
        }
        Span<KernelVector3> origins=stackalloc KernelVector3[3]{Vector(0,0,-.25),Vector(displaced?.125:0,.375,0),Vector(-.625,0,0)};
        for(var i=0;i<3;i++)
        {
            var line=new PK_LINE_sf_s();Write(&line.basis_set.location,origins[i]);
            Write(&line.basis_set.axis,Scale(axes[2-i],i==1?-1:1));
            CurveTag tag;Assert.Equal(0,KernelRuntime.LineCreate(&line,&tag));carriers[i]=tag;
        }
        Span<double> stations=stackalloc double[3];
        var error=KernelRuntime.TestThreePlaneCutEvents(surfaces,carriers,truncate,stations,out var events);
        if(truncate||displaced)
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,error);Assert.Equal(0,events);return;
        }
        Assert.Equal(0,error);Assert.Equal(1,events);
        Assert.Equal(.25,stations[0]);Assert.Equal(.375,stations[1]);Assert.Equal(.625,stations[2]);
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

    [Theory]
    [InlineData(.4995,.5005,1e-10,true,false,0,1)]
    [InlineData(.4995,.5005,.1,true,false,ParasolidConstants.PK_ERROR_cant_do_intersect,1)]
    [InlineData(.5011,.502,1e-10,true,false,ParasolidConstants.PK_ERROR_cant_do_intersect,1)]
    [InlineData(.5011,.502,1e-10,false,false,ParasolidConstants.PK_ERROR_cant_do_intersect,1)]
    [InlineData(.4995,.5005,.1,true,true,ParasolidConstants.PK_ERROR_cant_do_intersect,2)]
    public void RediscoveredRootWitnessCannotIdentifyADifferentEvent(double lower,double upper,double offset,bool requireProof,
        bool associateOnly,int expected,BufferCount expectedEvents)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.location.coord[0]=.5;
        plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;
        SurfTag equation;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&equation));
        Assert.Equal(expected,KernelRuntime.TestFiniteWitnessRediscovery(carrier,equation,lower,upper,offset,requireProof,associateOnly,out var events));
        Assert.Equal(expectedEvents,events);
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

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void AFiniteRootSplitsOnlyItsCertifiedSourceOccurrence(bool requireProof)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        Span<BufferCount> fragmentCounts=stackalloc BufferCount[2];
        Assert.Equal(0,KernelRuntime.TestCoincidentSourceFragments(carrier,fragmentCounts,out _,sourceRoot:true,requireProof:requireProof));
        Assert.Equal(2,fragmentCounts[0]);Assert.Equal(1,fragmentCounts[1]);
    }

    [Theory]
    [InlineData(2e-14,false)]
    [InlineData(2e-13,true)]
    public void CertifiedSourceStationsNeedDisjointBoxesToPublishFragments(double halfWidth,bool unresolved)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        const double a=.5,b=.5+2e-13;
        Span<IntervalNumber> roots=stackalloc IntervalNumber[2]{new(b-halfWidth,b+halfWidth),new(a-halfWidth,a+halfWidth)};
        var result=KernelRuntime.TestBoundedSourceStationOrder(carrier,roots,out var fragments,out var middle);
        Assert.Equal(unresolved?ParasolidConstants.PK_ERROR_cant_do_intersect:0,result);
        Assert.Equal(unresolved?0:3,fragments);
        if(!unresolved)Assert.InRange(middle,1.9e-13,2.1e-13);
    }

    [Theory]
    [InlineData(-5e-11,-4e-11,0,1)]
    [InlineData(-5e-11,5e-11,ParasolidConstants.PK_ERROR_cant_do_intersect,0)]
    public void CertifiedSourceStationCannotBorrowTheTrimEndpoint(double lower,double upper,int expected,int expectedFragments)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        Span<IntervalNumber> roots=stackalloc IntervalNumber[1]{new(lower,upper)};
        Assert.Equal(expected,KernelRuntime.TestBoundedSourceStationOrder(carrier,roots,out var fragments,out _));
        Assert.Equal(expectedFragments,fragments);
    }

    [Fact]
    public void PeriodicStationUsesOneCertifiedLiftAndExplicitEndIdentity()
    {
        var circle=new PK_CIRCLE_sf_s{radius=1};circle.basis_set.axis.coord[2]=1;circle.basis_set.ref_direction.coord[0]=1;
        CurveTag carrier;Assert.Equal(0,KernelRuntime.CircleCreate(&circle,&carrier));
        Assert.Equal(0,KernelRuntime.TestLocatePeriodicBoundaryStation(carrier,new(.039,.041),6.1,6.4,false,out var parameter,out var bound,out var inside));
        Assert.True(inside);Assert.InRange(parameter,bound.Lower,bound.Upper);
        Assert.InRange(Math.Tau+.04,bound.Lower,bound.Upper);

        Assert.Equal(0,KernelRuntime.TestLocatePeriodicBoundaryStation(carrier,new(-.2,-.19),6.1,6.4,false,out _,out _,out inside));
        Assert.False(inside);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestLocatePeriodicBoundaryStation(carrier,new(-.02,-.01),0,13,false,out _,out _,out _));

        var limit=Math.Tau+.05;
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestLocatePeriodicBoundaryStation(carrier,new(.049,.051),limit,6.4,false,out _,out _,out _));
        Assert.Equal(0,KernelRuntime.TestLocatePeriodicBoundaryStation(carrier,new(.049,.051),limit,6.4,true,out parameter,out bound,out inside));
        Assert.True(inside);Assert.Equal(limit,parameter);Assert.Equal(limit,bound.Lower);Assert.Equal(limit,bound.Upper);
    }

    [Fact]
    public void MaterialWitnessStaysInTheOpenGapOnBothCharts()
    {
        Assert.True(KernelRuntime.TryInteriorBoundaryStation(.75,.9,0,1,false,.38,out var interior));
        Assert.True(interior>.75&&interior<.9);
        Assert.True(KernelRuntime.TryInteriorBoundaryStation(.95,.15,0,1,true,.2,out interior));
        Assert.True(interior>.95&&interior<1);
        Assert.True(KernelRuntime.TryInteriorBoundaryStation(.95,.15,0,1,true,.75,out interior));
        Assert.True(interior>0&&interior<.15);
        Assert.False(KernelRuntime.TryInteriorBoundaryStation(.75,Math.BitIncrement(.75),0,1,false,.38,out _));
    }

    [Fact]
    public void CertifiedCutRediscoveryRefinesOneRootAndRejectsAnOverlappingDifferentOccurrence()
    {
        IntervalNumber first=new(.2,.4),second=new(.35,.6);
        Assert.Equal(0,KernelRuntime.TestCertifiedCutRediscovery(first,second,true,true,out var joined,out var representative));
        Assert.True(joined);Assert.Equal(.35,representative);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestCertifiedCutRediscovery(first,second,false,true,out _,out _));
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestCertifiedCutRediscovery(first,second,true,false,out _,out _));
    }

    [Fact]
    public void DisjointCertifiedCutRootsSurviveTheOldCandidateEpsilon()
    {
        IntervalNumber first=new(.5,.5+2e-14),second=new(.5+2e-13,.5+3e-13);
        Assert.Equal(0,KernelRuntime.TestCertifiedCutRediscovery(first,second,true,false,out var joined,out _));
        Assert.False(joined);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,
            KernelRuntime.TestCertifiedCutRediscovery(first,second,true,true,out _,out _));
    }

    [Fact]
    public void DifferentSourceCoedgesNeedActualEndpointIdentityAndAnExactCommonCutStation()
    {
        Assert.Equal(0,KernelRuntime.TestCertifiedCutVertexJoin(true,true,true,out var joined));Assert.True(joined);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestCertifiedCutVertexJoin(false,true,true,out _));
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestCertifiedCutVertexJoin(true,false,true,out _));
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestCertifiedCutVertexJoin(true,true,false,out _));
    }

    [Theory]
    [InlineData(.2,.3,true)]
    [InlineData(2.2,2.3,true)]
    [InlineData(1,1,true)]
    [InlineData(1.25,1.5,false)]
    [InlineData(.9,1.1,false)]
    [InlineData(.9,2.1,false)]
    public void CarrierSurfaceIncidenceRequiresTheWholeEventBoxOnOneFiniteUse(double lower,double upper,bool incident)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;CurveTag carrier;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[2]=1;plane.basis_set.ref_direction.coord[0]=1;
        SurfTag surface;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&surface));
        Assert.Equal(0,KernelRuntime.TestBoundedCarrierIncidence(carrier,surface,lower,upper,out var associated));
        Assert.Equal(incident,associated);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CutAssociationUsesOnlySupportsOnTheRequestedTrim(bool remoteUse)
    {
        Span<CurveTag> curves=stackalloc CurveTag[3];
        for(var axis=0;axis<3;axis++)
        {
            var line=new PK_LINE_sf_s();line.basis_set.axis.coord[axis]=1;
            if(axis!=0)line.basis_set.location.coord[0]=.5;
            CurveTag curve;Assert.Equal(0,KernelRuntime.LineCreate(&line,&curve));curves[axis]=curve;
        }
        Span<SurfTag> surfaces=stackalloc SurfTag[3];
        for(var i=0;i<3;i++)
        {
            var plane=new PK_PLANE_sf_s();plane.basis_set.ref_direction.coord[0]=1;
            if(i==0)plane.basis_set.axis.coord[2]=1;
            else if(i==1)plane.basis_set.axis.coord[1]=1;
            else plane.basis_set.axis.coord[1]=plane.basis_set.axis.coord[2]=Math.Sqrt(.5);
            SurfTag surface;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&surface));surfaces[i]=surface;
        }
        Assert.Equal(0,KernelRuntime.TestFiniteCutIncidence(curves[0],curves[1..],surfaces,remoteUse,out var associated,out var parameter));
        Assert.Equal(remoteUse,associated);
        if(associated)Assert.Equal(.5,parameter);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedDiscoveryOfOneIsolatedRootCanJoinButAnotherBranchOrOccurrenceCannot(bool separateOccurrence)
    {
        var circle=new PK_CIRCLE_sf_s{radius=1};circle.basis_set.axis.coord[2]=1;circle.basis_set.ref_direction.coord[0]=1;
        CurveTag carrier;Assert.Equal(0,KernelRuntime.CircleCreate(&circle,&carrier));
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;
        SurfTag equation;Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&equation));
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestFiniteRootWitnessIdentity(carrier,equation,separateOccurrence,out var first,out var repeated));
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
        Assert.Equal(expected,KernelRuntime.IsolateFiniteExplicitCylinder(new(){Curve=carrier,Start=0,End=Math.Tau},support,roots,isolation,errors,out var count));
        if(unresolved)return;
        Assert.Equal(expectedCount,count);if(count==0)return;
        var angle=Math.Acos(Math.Sqrt(5.0/12.0));Span<double> exact=stackalloc double[4]{angle,Math.PI-angle,Math.PI+angle,Math.Tau-angle};
        for(var i=0;i<count;i++)
        {
            Assert.True(isolation[i].Certified);Assert.InRange(exact[i],isolation[i].Root.Lower,isolation[i].Root.Upper);
            if(i>0)Assert.True(isolation[i-1].Root.Upper<isolation[i].Root.Lower);
        }
    }

    [Theory]
    [InlineData(.75,.1,6,2,false)]
    [InlineData(.75,.1,3,1,false)]
    [InlineData(.75,1.5,4.5,0,false)]
    [InlineData(-2,.1,6,0,true)]
    public void FiniteEllipsePlaneRootsKeepNativeBranchAndReportTangency(double x,double first,double last,int expected,bool tangent)
    {
        var ellipse=new PK_ELLIPSE_sf_s{R1=2,R2=1};ellipse.basis_set.axis.coord[2]=1;ellipse.basis_set.ref_direction.coord[0]=1;
        CurveTag carrier;Assert.Equal(0,KernelRuntime.EllipseCreate(&ellipse,&carrier));
        var plane=new AnalyticSurface(SurfaceClass.Plane,Vector(x,0,0),Vector(1,0,0),Vector(0,1,0));
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];Span<ScalarRootIsolation> intervals=stackalloc ScalarRootIsolation[4];
        Span<double> errors=stackalloc double[4];
        var status=KernelRuntime.IsolateFiniteExplicitLevelSet(new(){Curve=carrier,Start=first,End=last},plane,roots,intervals,errors,out var count);
        Assert.Equal(tangent?AlgorithmStatus.NotConverged:AlgorithmStatus.Success,status);
        if(tangent)return;
        Assert.Equal(expected,count);
        if(count>0)Assert.InRange(Math.Acos(x/2),intervals[0].Root.Lower,intervals[0].Root.Upper);
        if(count>1)
        {
            Assert.InRange(Math.Tau-Math.Acos(x/2),intervals[1].Root.Lower,intervals[1].Root.Upper);
            Assert.True(intervals[0].Root.Upper<intervals[1].Root.Lower);
        }
    }

    [Theory]
    [InlineData(0,2)]
    [InlineData(.45,1)]
    public void ParameterBoxesKeepSeparateLineSurfaceRootsAndRespectTheFiniteLine(double lineStart,int expectedCount)
    {
        // P(u,v)=(u,v,(u-.5)^2), L(t)=(t,.4,.01).
        Span<IntervalNumber> poles=stackalloc IntervalNumber[24]
        {0,0,.25,1,0,1,.25,1,.5,0,-.25,1,.5,1,-.25,1,1,0,.25,1,1,1,.25,1};
        Span<IntervalNumber> line=stackalloc IntervalNumber[8]{0,.4,.01,1,1,.4,.01,1};
        var domain=new SurfaceParameterBox(0,1,0,1);
        var a=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,1,domain));
        var b=new SurfaceIntersectionPatch(new BezierSurfacePatch(line,1,0,domain));
        var search=new ParameterCell{Lower=new(){S=lineStart},Upper=new(){U=1,V=1,S=1}};
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[4];
        Span<IsolatedRootCertificate> proofs=stackalloc IsolatedRootCertificate[4];
        Assert.Equal(AlgorithmStatus.Success,ParametricSurfaceIntersection.Isolate(a,b,search,3,roots,out var count,proofs));
        Assert.Equal(expectedCount,count);var lower=false;var upper=false;
        for(var i=0;i<count;i++)
        {
            var p=proofs[i];Assert.True(IsolatedRootCertificate.Contains(search.Second,p.Second));
            lower|=p.Second.U0<=.4&&p.Second.U1>=.4;upper|=p.Second.U0<=.6&&p.Second.U1>=.6;
        }
        Assert.Equal(expectedCount==2,lower);Assert.True(upper);
        if(count==2)Assert.False(proofs[0].MayShareRoot(proofs[1]));
    }
    private static void Write(PK_VECTOR_s* target,KernelVector3 value){target->coord[0]=value.X;target->coord[1]=value.Y;target->coord[2]=value.Z;}
}
