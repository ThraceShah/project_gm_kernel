using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IntersectionOperationTests:IDisposable
{
    public IntersectionOperationTests()
    {KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void LineLine_ReturnsBothParametersAndOppositelyDirectedOverlapMarkers()
    {
        var a=Line(0,0,0,1,0,0);var b=Line(1,-2,0,0,1,0);
        using(var result=CC(a,-3,3,b,0,4))
        {Assert.Equal(1,result.Count);Near(1,result.First[0]);Near(2,result.Second[0]);Assert.Equal(14611,result.Kinds[0]);}
        var opposite=Line(0,0,0,-1,0,0);
        using(var result=CC(a,-2,2,opposite,-1,1))
        {Assert.Equal(2,result.Count);Near(-1,result.First[0]);Near(1,result.Second[0]);Near(1,result.First[1]);Near(-1,result.Second[1]);Assert.Equal(14613,result.Kinds[0]);Assert.Equal(14614,result.Kinds[1]);}
        var skew=Line(0,0,1,0,1,0);using(var result=CC(a,-3,3,skew,-3,3)){Assert.Equal(0,result.Count);Assert.True(result.Points==null);}
    }

    [Fact]
    public void CurveIntervals_MapTrimmedParametersAndFilterFullConicBranches()
    {
        var line=Line(0,0,0,1,0,0);var trimSf=new PK_TRCURVE_sf_s{basis_curve=line};trimSf.t_int=Range(2,6);
        int trimmed=0;Assert.Equal(0,KernelRuntime.TrCurveCreate(&trimSf,&trimmed));
        var cross=Line(4,0,0,0,1,0);using(var result=CC(trimmed,0,4,cross,-1,1)){Assert.Equal(1,result.Count);Near(2,result.First[0]);Near(0,result.Second[0]);}
        var circle=Circle(0,0,0,2);using(var result=CC(line,-3,3,circle,0,Math.PI/2)){Assert.Equal(1,result.Count);Near(2,result.First[0]);Near(0,result.Second[0]);}
        using(var result=CC(Line(0,2,0,1,0,0),-3,3,circle,0,Math.Tau)){Assert.Equal(1,result.Count);Assert.Equal(14611,result.Kinds[0]);}
    }

    [Fact]
    public void NoncoplanarCircles_FindBothComponentsWithoutASeed()
    {
        var a=Circle(0,0,0,2);var definition=new PK_CIRCLE_sf_s{basis_set=Axis(0,0,0,0,1,0,1,0,0),radius=2};
        int b=0;Assert.Equal(0,KernelRuntime.CircleCreate(&definition,&b));
        using(var result=CC(a,0,Math.Tau,b,0,Math.Tau))
        {Assert.Equal(2,result.Count);Near(2,Math.Abs(result.Points[0].coord[0]));Near(2,Math.Abs(result.Points[1].coord[0]));Assert.NotEqual(Math.Sign(result.Points[0].coord[0]),Math.Sign(result.Points[1].coord[0]));}
    }

    [Fact]
    public void SurfaceCurve_HandlesFourTorusRootsTangencyAndCoincidenceSentinels()
    {
        var line=Line(0,0,0,1,0,0);var torusDefinition=new PK_TORUS_sf_s{basis_set=Axis(),major_radius=3,minor_radius=1};
        int torus=0;Assert.Equal(0,KernelRuntime.TorusCreate(&torusDefinition,&torus));
        using(var result=SC(torus,line,-5,5))
        {Assert.Equal(4,result.Count);var expected=new[]{-4.0,-2.0,2.0,4.0};for(var i=0;i<4;i++){Near(expected[i],result.First[i]);Assert.Equal(14611,result.Kinds[i]);CheckUv(torus,result,i);}Near(-Math.PI,result.Uv[1].param[1]);Near(-Math.PI,result.Uv[2].param[1]);}
        var sphere=Sphere(0,0,0,2);var tangent=Line(0,2,0,1,0,0);
        using(var result=SC(sphere,tangent,-3,3)){Assert.Equal(1,result.Count);Assert.Equal(14612,result.Kinds[0]);Near(Math.PI/2,result.Uv[0].param[0]);Near(0,result.Uv[0].param[1]);}
        var plane=Plane(0,0,0,0,0,1);
        using(var result=SC(plane,line,-3,3)){Assert.Equal(2,result.Count);Assert.Equal(14613,result.Kinds[0]);Assert.Equal(14614,result.Kinds[1]);for(var i=0;i<2;i++){Near(-1,result.Uv[i].param[0]);Near(-1,result.Uv[i].param[1]);}}
    }

    [Fact]
    public void SurfaceConic_TwoRationalChartsIncludeAllFourRootsAndThePiSeam()
    {
        var sphere=Sphere(0,0,0,2);var ellipseSf=new PK_ELLIPSE_sf_s{basis_set=Axis(),R1=3,R2=1};int ellipse=0;
        Assert.Equal(0,KernelRuntime.EllipseCreate(&ellipseSf,&ellipse));
        using(var result=SC(sphere,ellipse,0,Math.Tau))
        {Assert.Equal(4,result.Count);for(var i=0;i<4;i++){Near(4,SquareNorm(result.Points[i]),2e-8);CheckUv(sphere,result,i);}}
        var offsetSphere=Sphere(1,0,0,3);var circle=Circle(0,0,0,2);
        using(var result=SC(offsetSphere,circle,0,Math.Tau)){Assert.Equal(1,result.Count);Near(Math.PI,result.First[0]);Assert.Equal(14612,result.Kinds[0]);}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BCurvePlane_EnumeratesAllCubicRootsIncludingRationalWeights(bool rational)
    {
        var curve=Cubic(rational,false);var plane=Plane(0,0,0,0,0,1);
        using(var result=SC(plane,curve,0,1))
        {Assert.Equal(3,result.Count);Near(.2,result.First[0]);Near(.5,result.First[1]);Near(.8,result.First[2]);for(var i=0;i<3;i++)Near(0,result.Points[i].coord[2]);}
    }

    [Fact]
    public void BCurvePlane_TraversesEveryKnotSpanAndMergesCoincidentSpanBoundaries()
    {
        var curve=Cubic(false,true);var plane=Plane(0,0,0,0,0,1);
        using(var result=SC(plane,curve,0,2)){Assert.Equal(6,result.Count);for(var i=0;i<6;i++)Near((i/3)+(i%3==0?.2:i%3==1?.5:.8),result.First[i]);}
        var yplane=Plane(0,0,0,0,1,0);
        using(var result=SC(yplane,curve,0,2)){Assert.Equal(2,result.Count);Near(0,result.First[0]);Near(2,result.First[1]);Assert.Equal(14613,result.Kinds[0]);Assert.Equal(14614,result.Kinds[1]);}
    }

    [Fact]
    public void SurfaceSurface_ProducesExactEllipseAndIndependentPairedUv()
    {
        var cylinderSf=new PK_CYL_sf_s{basis_set=Axis(),radius=2};int cylinder=0;Assert.Equal(0,KernelRuntime.CylCreate(&cylinderSf,&cylinder));
        var q=Math.Sqrt(.5);var plane=Plane(0,0,0,q,0,q,0,1,0);
        using(var result=SS(plane,cylinder))
        {
            Assert.Equal(1,result.CurveCount);Assert.Equal(0,result.PointCount);
            var sf=new PK_ELLIPSE_sf_s();Assert.Equal(0,KernelRuntime.EllipseAsk(result.Curves[0],&sf));Near(2*Math.Sqrt(2),sf.R1);Near(2,sf.R2);
            for(var i=0;i<13;i++)CheckPaired(plane,cylinder,result.Curves[0],Math.Tau*i/13);
        }
    }

    [Fact]
    public void SphereSurface_ClosedCircleAndIsolatedTangencyHaveDifferentResults()
    {
        var a=Sphere(0,0,0,2);var b=Sphere(2,0,0,2);
        using(var result=SS(a,b))
        {Assert.Equal(1,result.CurveCount);Assert.Equal(0,result.PointCount);var sf=new PK_CIRCLE_sf_s();Assert.Equal(0,KernelRuntime.CircleAsk(result.Curves[0],&sf));Near(1,sf.basis_set.location.coord[0]);Near(Math.Sqrt(3),sf.radius);for(var i=0;i<9;i++)CheckPaired(a,b,result.Curves[0],Math.Tau*i/9);}
        var tangent=Sphere(4,0,0,2);using(var result=SS(a,tangent)){Assert.Equal(0,result.CurveCount);Assert.Equal(1,result.PointCount);Near(2,result.Points[0].coord[0]);}
        using(var result=SS(a,a)){Assert.Equal(0,result.CurveCount);Assert.Equal(0,result.PointCount);}
    }

    [Theory]
    [InlineData(false,-1,0,1)] [InlineData(false,0,1,0)] [InlineData(false,1,0,0)]
    [InlineData(true,-1,0,0)] [InlineData(true,0,1,0)] [InlineData(true,1,0,1)]
    public void SphereContactDoesNotConsumeAProvedGapOrPositiveCircle(bool internalContact,int side,int points,int curves)
    {
        var small=Math.ScaleB(1,-10);var large=internalContact?2*small:small;
        var distance=(internalContact?large-small:large+small)+side*Math.ScaleB(1,-59);
        var a=Sphere(0,0,0,large);var b=Sphere(distance,0,0,small);
        using var result=SS(a,b);
        Assert.Equal(points,result.PointCount);Assert.Equal(curves,result.CurveCount);
        if(curves!=0)
        {
            PK_CIRCLE_sf_s circle;Assert.Equal(0,KernelRuntime.CircleAsk(result.Curves[0],&circle));
            Assert.True(circle.radius>0&&circle.radius<1e-8);
        }
        if(points!=0)Assert.Equal(large,result.Points[0].coord[0]);
    }

    [Fact]
    public void PlaneTorus_FindsBothClosedBranchesAndExplicitSeedSelectsOne()
    {
        var definition=new PK_TORUS_sf_s{basis_set=Axis(),major_radius=3,minor_radius=1};int torus=0;Assert.Equal(0,KernelRuntime.TorusCreate(&definition,&torus));
        var plane=Plane(0,0,0,0,0,1);
        using(var result=SS(plane,torus)){Assert.Equal(2,result.CurveCount);for(var i=0;i<2;i++)CheckPaired(plane,torus,result.Curves[i],.7);}
        var options=SurfaceOptions();options.have_vector=1;options.vector.coord[0]=2;
        using(var result=SS(plane,torus,options)){Assert.Equal(1,result.CurveCount);var sf=new PK_CIRCLE_sf_s();Assert.Equal(0,KernelRuntime.CircleAsk(result.Curves[0],&sf));Near(2,sf.radius);}
    }

    [Fact]
    public void NonplanarBilinearNurbsPlane_CreatesExactBoundedRationalBranch()
    {
        var surface=Bilinear(false);var plane=Plane(0,0,.25,0,0,1);
        using(var result=SS(surface,plane))
        {
            Assert.Equal(1,result.CurveCount);Assert.Equal(0,result.PointCount);var sf=new PK_BCURVE_sf_s();Assert.Equal(0,KernelRuntime.BCurveAsk(result.Curves[0],&sf));
            try{Assert.Equal(2,sf.degree);Assert.Equal((byte)1,sf.is_rational);}finally{Free(sf.vertex);Free(sf.knot);Free(sf.knot_mult);}
            Near(.25,result.Intervals[0].value[0]);Near(1,result.Intervals[0].value[1]);
            for(var i=0;i<=16;i++)
            {
                var t=.25+.75*i/16;var p=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(result.Curves[0],t,0,&p));
                Near(.25,p.coord[0]*p.coord[1]);Near(.25,p.coord[2]);CheckPaired(surface,plane,result.Curves[0],t);
            }
        }
    }

    [Fact]
    public void BilinearSaddlePlane_SplitsFourHalfBranchesAtTheSingularCrossing()
    {
        var surface=Bilinear(true);var plane=Plane(0,0,0,0,0,1);
        using(var result=SS(surface,plane))
        {
            Assert.Equal(4,result.CurveCount);Assert.Equal(0,result.PointCount);
            for(var i=0;i<4;i++)for(var j=0;j<=4;j++)CheckPaired(surface,plane,result.Curves[i],result.Intervals[i].value[0]+(result.Intervals[i].value[1]-result.Intervals[i].value[0])*j/4);
        }
    }

    [Theory]
    [InlineData(false,false,false)]
    [InlineData(true,false,false)]
    [InlineData(true,true,false)]
    [InlineData(false,false,true)]
    [InlineData(true,false,true)]
    public void HigherDegreeRuledNurbsPlane_EnumeratesEveryBoundedComponent(bool rational,bool twoSpans,bool transpose)
    {
        var surface=CubicRuled(rational,twoSpans,transpose);var plane=Plane(0,0,0,0,0,1);
        using(var result=SS(surface,plane))
        {
            Assert.Equal(twoSpans?4:2,result.CurveCount);Assert.Equal(0,result.PointCount);
            double[] starts=twoSpans?[0,.5,1.2,1.8]:[0,.5];double[] ends=twoSpans?[.2,.8,1.5,2]:[.2,.8];
            for(var i=0;i<result.CurveCount;i++)
            {
                Near(starts[i],result.Intervals[i].value[0]);Near(ends[i],result.Intervals[i].value[1]);
                for(var sample=0;sample<=10;sample++)CheckPaired(surface,plane,result.Curves[i],starts[i]+(ends[i]-starts[i])*sample/10);
            }
        }
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void TransposedMultipleSpanRuledNurbsPlane_RejectsWithoutPublishingPartialResults(bool rational,bool reverse)
    {
        var surface=CubicRuled(rational,true,true);var plane=Plane(0,0,0,0,0,1);
        var before=KernelRuntime.ReturnOutstandingBlocks;var alive=KernelRuntime.Curves.AliveCount;
        var options=SurfaceOptions();int np=-1,nc=-1;PK_VECTOR_s* points=(PK_VECTOR_s*)1;int* curves=(int*)1;
        PK_INTERVAL_s* intervals=(PK_INTERVAL_s*)1;int* kinds=(int*)1;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfIntersectSurf(
            reverse?plane:surface,reverse?surface:plane,&options,&np,&points,&nc,&curves,&intervals,&kinds));
        Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(points==null&&curves==null&&intervals==null&&kinds==null);
        Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(alive,KernelRuntime.Curves.AliveCount);
        Assert.True(KernelRuntime.IsValidTag(surface));Assert.True(KernelRuntime.IsValidTag(plane));
    }

    [Fact]
    public void LineNurbs_UsesBothSpatialConstraintsAndFindsEveryCrossing()
    {
        var line=Line(0,0,0,1,0,0);var curve=Cubic(false,false);
        using(var result=CC(line,0,1,curve,0,1)){Assert.Equal(3,result.Count);Near(.2,result.First[0]);Near(.5,result.First[1]);Near(.8,result.First[2]);}
        var offset=Line(0,1,0,1,0,0);using(var result=CC(offset,0,1,curve,0,1))Assert.Equal(0,result.Count);
    }

    [Fact]
    public void ReturnedArraysAreIndependentAndFailureLeavesNoBuffersOrNewEntities()
    {
        var a=Line(0,0,0,1,0,0);var b=Line(0,0,0,0,1,0);var before=KernelRuntime.ReturnOutstandingBlocks;
        using(var result=CC(a,-1,1,b,-1,1)){Assert.Equal(0,KernelRuntime.EntityDelete(1,&a));Assert.Equal(0,KernelRuntime.EntityDelete(1,&b));Near(0,result.First[0]);Near(0,result.Points[0].coord[0]);}
        Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);
        var plane=Plane(0,0,0,0,0,1);var sphere=Sphere(0,0,0,2);var alive=KernelRuntime.Curves.AliveCount;
        for(var fault=0;fault<10;fault++)
        {
            var options=SurfaceOptions();int np=-1,nc=-1;PK_VECTOR_s* p=(PK_VECTOR_s*)1;int* c=(int*)1;PK_INTERVAL_s* intervals=(PK_INTERVAL_s*)1;int* types=(int*)1;
            KernelRuntime.InjectAllocationFailure(fault);var error=KernelRuntime.SurfIntersectSurf(plane,sphere,&options,&np,&p,&nc,&c,&intervals,&types);KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){for(var i=0;i<nc;i++)Assert.Equal(0,KernelRuntime.EntityDelete(1,c+i));Free(p);Free(c);Free(intervals);Free(types);}
            else {Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(p==null&&c==null&&intervals==null&&types==null);}
            Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(alive,KernelRuntime.Curves.AliveCount);
        }
    }

    [Fact]
    public void UnsupportedAndNumericallyAmbiguousCasesNeverReturnSuccessEmpty()
    {
        var sphere=Sphere(0,0,0,2);var torusSf=new PK_TORUS_sf_s{basis_set=Axis(),major_radius=3,minor_radius=1};int torus=0;Assert.Equal(0,KernelRuntime.TorusCreate(&torusSf,&torus));
        var options=SurfaceOptions();int np=9,nc=9;PK_VECTOR_s* p=null;int* c=null;PK_INTERVAL_s* intervals=null;int* types=null;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(sphere,torus,&options,&np,&p,&nc,&c,&intervals,&types));Assert.Equal(0,np);Assert.Equal(1,nc);
        Assert.Equal(ParasolidConstants.PK_intersect_curve_tangent_c,types[0]);Free(p);Free(c);Free(intervals);Free(types);
        var first=Plane(0,0,0,0,0,1);var second=Plane(1,0,0,1e-13,0,1,0,1,0);
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&p,&nc,&c,&intervals,&types));
        Span<double> coefficients=stackalloc double[3]{.25-1e-16,-1,1};Span<double> roots=stackalloc double[4];
        // Centering this polynomial removes cancellation at its critical
        // point, so both nearby roots are resolvable and must be preserved.
        Assert.Equal(AlgorithmStatus.Success,BoundedPolynomialRoots.Find(coefficients,0,1,roots,out var rootCount));
        Assert.Equal(2,rootCount);Assert.True(roots[0]<.5&&roots[1]>.5);
        // Moving the critical point away from the centered chart makes its
        // tiny residual indistinguishable from rounded cancellation.
        coefficients[0]=.09-1e-16;coefficients[1]=-.6;
        Assert.NotEqual(AlgorithmStatus.Success,BoundedPolynomialRoots.Find(coefficients,0,1,roots,out _));
    }

    [Fact]
    public void SameBodySurfaceIntersectionRegistersConstructionGeometryAndRejectsMixedOwnership()
    {
        int body=0,n=0;int* faces=null;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(2,3,4,null,&body));Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&faces));
        int first=0,second=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(faces[0],&first));
        var firstPlane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(first,&firstPlane));
        for(var i=1;i<n;i++)
        {
            int candidate=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(faces[i],&candidate));var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(candidate,&plane));
            var dot=0.0;for(var k=0;k<3;k++)dot+=plane.basis_set.axis.coord[k]*firstPlane.basis_set.axis.coord[k];if(Math.Abs(dot)<.5){second=candidate;break;}
        }
        Free(faces);Assert.NotEqual(0,second);
        var options=SurfaceOptions();int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* intervals=null;int* types=null;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&intervals,&types));Assert.Equal(1,nc);var created=curves[0];
        Assert.Equal(body,KernelRuntime.GetCurveByTag(created).ConstructionBodyTag);
        Free(points);Free(curves);Free(intervals);Free(types);
        var orphan=Plane(0,0,0,0,0,1);
        Assert.Equal(ParasolidConstants.PK_ERROR_not_in_same_body,KernelRuntime.SurfIntersectSurf(first,orphan,&options,&np,&points,&nc,&curves,&intervals,&types));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.False(KernelRuntime.IsValidTag(created));Assert.True(KernelRuntime.IsValidTag(orphan));
    }

    [Fact]
    public void RepeatedPublicPointIntersectionDoesNotAllocateManagedMemory()
    {
        var sphere=Sphere(0,0,0,2);var line=Line(0,0,0,1,0,0);var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};
        var error=0;long bytes=0;
        for(var iteration=0;iteration<101;iteration++)
        {
            if(iteration==1)bytes=GC.GetAllocatedBytesForCurrentThread();
            int count=0;PK_VECTOR_s* points=null;PK_UV_s* uv=null;double* ts=null;int* kinds=null;
            error|=KernelRuntime.SurfIntersectCurve(sphere,line,Range(-3,3),&options,&count,&points,&uv,&ts,&kinds);
            if(count!=2)error|=1;error|=KernelRuntime.MemoryFree(points);error|=KernelRuntime.MemoryFree(uv);error|=KernelRuntime.MemoryFree(ts);error|=KernelRuntime.MemoryFree(kinds);
        }
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;Assert.Equal(0,error);Assert.Equal(0,bytes);
    }

    private static void CheckPaired(int first,int second,int curve,double t)
    {
        var p=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,t,0,&p));var point=new KernelVector3{X=p.coord[0],Y=p.coord[1],Z=p.coord[2]};
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.TryIntersectionPairedParameters(first,second,point,out var a,out var b));
        var va=new PK_VECTOR_s();var vb=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(first,a,0,0,0,&va));Assert.Equal(0,KernelRuntime.SurfEval(second,b,0,0,0,&vb));
        for(var i=0;i<3;i++){Near(p.coord[i],va.coord[i],2e-8);Near(p.coord[i],vb.coord[i],2e-8);}
    }
    private static void CheckUv(int surface,PointResult result,int index)
    {var value=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,result.Uv[index],0,0,0,&value));for(var i=0;i<3;i++)Near(result.Points[index].coord[i],value.coord[i],2e-8);}
    private static double SquareNorm(PK_VECTOR_s p)=>p.coord[0]*p.coord[0]+p.coord[1]*p.coord[1]+p.coord[2]*p.coord[2];
    private static void Near(double expected,double actual,double tolerance=2e-9)=>Assert.InRange(Math.Abs(actual-expected),0,tolerance);
    private static void Free(void* p){if(p!=null)Assert.Equal(0,KernelRuntime.MemoryFree(p));}
    private static PK_INTERVAL_s Range(double a,double b){var r=new PK_INTERVAL_s();r.value[0]=a;r.value[1]=b;return r;}
    private static PK_AXIS2_sf_s Axis(double x=0,double y=0,double z=0,double nx=0,double ny=0,double nz=1,double rx=1,double ry=0,double rz=0)
    {var a=new PK_AXIS2_sf_s();a.location.coord[0]=x;a.location.coord[1]=y;a.location.coord[2]=z;a.axis.coord[0]=nx;a.axis.coord[1]=ny;a.axis.coord[2]=nz;a.ref_direction.coord[0]=rx;a.ref_direction.coord[1]=ry;a.ref_direction.coord[2]=rz;return a;}
    private static int Line(double x,double y,double z,double dx,double dy,double dz)
    {var sf=new PK_LINE_sf_s();sf.basis_set.location.coord[0]=x;sf.basis_set.location.coord[1]=y;sf.basis_set.location.coord[2]=z;sf.basis_set.axis.coord[0]=dx;sf.basis_set.axis.coord[1]=dy;sf.basis_set.axis.coord[2]=dz;int tag=0;Assert.Equal(0,KernelRuntime.LineCreate(&sf,&tag));return tag;}
    private static int Circle(double x,double y,double z,double radius)
    {var sf=new PK_CIRCLE_sf_s{basis_set=Axis(x,y,z),radius=radius};int tag=0;Assert.Equal(0,KernelRuntime.CircleCreate(&sf,&tag));return tag;}
    private static int Sphere(double x,double y,double z,double radius)
    {var sf=new PK_SPHERE_sf_s{basis_set=Axis(x,y,z),radius=radius};int tag=0;Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&tag));return tag;}
    private static int Plane(double x,double y,double z,double nx,double ny,double nz,double rx=1,double ry=0,double rz=0)
    {var sf=new PK_PLANE_sf_s{basis_set=Axis(x,y,z,nx,ny,nz,rx,ry,rz)};int tag=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&tag));return tag;}
    private static int Cubic(bool rational,bool twoSpans)
    {
        var count=twoSpans?7:4;var dim=rational?4:3;double* vertices=stackalloc double[count*dim];double* zs=stackalloc double[7]{-.08,.14,-.14,.08,-.14,.14,-.08};
        for(var i=0;i<count;i++){var w=rational?(i%3+1):1;vertices[i*dim]=i/3.0*w;vertices[i*dim+1]=0;vertices[i*dim+2]=zs[i];if(rational)vertices[i*dim+3]=w;}
        double* knots=stackalloc double[3]{0,1,2};int* mult=stackalloc int[3]{4,twoSpans?3:4,4};
        var sf=new PK_BCURVE_sf_s{degree=3,n_vertices=count,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=vertices,n_knots=twoSpans?3:2,knot=knots,knot_mult=mult,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c};
        int tag=0;Assert.Equal(0,KernelRuntime.BCurveCreate(&sf,&tag));return tag;
    }
    private static int Bilinear(bool saddle)
    {
        double* vertices=stackalloc double[12];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++){var at=(u*2+v)*3;vertices[at]=saddle?2*u-1:u;vertices[at+1]=saddle?2*v-1:v;vertices[at+2]=vertices[at]*vertices[at+1];}
        double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{2,2};
        var sf=new PK_BSURF_sf_s{u_degree=1,v_degree=1,n_u_vertices=2,n_v_vertices=2,vertex_dim=3,vertex=vertices,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        int tag=0;Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&tag));return tag;
    }
    private static int CubicRuled(bool rational,bool twoSpans,bool transpose=false)
    {
        var count=twoSpans?7:4;var dimension=rational?4:3;double* vertices=stackalloc double[count*2*dimension];double* zs=stackalloc double[7]{-.08,.14,-.14,.08,-.14,.14,-.08};
        for(var u=0;u<count;u++)for(var v=0;v<2;v++)
        {var at=(transpose?v*count+u:u*2+v)*dimension;var weight=rational?u%3+1:1;vertices[at]=u/3.0*weight;vertices[at+1]=v*weight;vertices[at+2]=zs[u]+v*weight;if(rational)vertices[at+3]=weight;}
        double* uk=stackalloc double[3]{0,1,2};double* vk=stackalloc double[2]{0,1};int* um=stackalloc int[3]{4,twoSpans?3:4,4};int* vm=stackalloc int[2]{2,2};
        var sf=new PK_BSURF_sf_s{u_degree=3,v_degree=1,n_u_vertices=count,n_v_vertices=2,vertex_dim=dimension,is_rational=(byte)(rational?1:0),vertex=vertices,n_u_knots=twoSpans?3:2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=um,v_knot_mult=vm,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        if(transpose){sf.u_degree=1;sf.v_degree=3;sf.n_u_vertices=2;sf.n_v_vertices=count;sf.n_u_knots=2;sf.n_v_knots=twoSpans?3:2;sf.u_knot=vk;sf.v_knot=uk;sf.u_knot_mult=vm;sf.v_knot_mult=um;}
        int tag=0;Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&tag));return tag;
    }
    private static PK_SURF_intersect_surf_o_s SurfaceOptions()=>new(){o_t_version=3,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c,tolerance=1e-5};
    private static PointResult CC(int a,double al,double ah,int b,double bl,double bh)
    {
        var result=new PointResult();var options=new PK_CURVE_intersect_curve_o_s{o_t_version=1};
        delegate* unmanaged<int,PK_INTERVAL_s,int,PK_INTERVAL_s,PK_CURVE_intersect_curve_o_s*,int*,PK_VECTOR_s**,double**,double**,int**,int> invoke=&KernelExports.PK_CURVE_intersect_curve;
        Assert.Equal(0,invoke(a,Range(al,ah),b,Range(bl,bh),&options,&result.Count,&result.Points,&result.First,&result.Second,&result.Kinds));return result;
    }
    private static PointResult SC(int surface,int curve,double lo,double hi)
    {
        var result=new PointResult();var options=new PK_SURF_intersect_curve_o_s{o_t_version=1};
        delegate* unmanaged<int,int,PK_INTERVAL_s,PK_SURF_intersect_curve_o_s*,int*,PK_VECTOR_s**,PK_UV_s**,double**,int**,int> invoke=&KernelExports.PK_SURF_intersect_curve;
        Assert.Equal(0,invoke(surface,curve,Range(lo,hi),&options,&result.Count,&result.Points,&result.Uv,&result.First,&result.Kinds));return result;
    }
    private static SurfaceResult SS(int a,int b,PK_SURF_intersect_surf_o_s? supplied=null)
    {
        var result=new SurfaceResult();var options=supplied??SurfaceOptions();
        delegate* unmanaged<int,int,PK_SURF_intersect_surf_o_s*,int*,PK_VECTOR_s**,int*,int**,PK_INTERVAL_s**,int**,int> invoke=&KernelExports.PK_SURF_intersect_surf;
        Assert.Equal(0,invoke(a,b,&options,&result.PointCount,&result.Points,&result.CurveCount,&result.Curves,&result.Intervals,&result.Kinds));return result;
    }
    private struct PointResult:IDisposable
    {internal int Count;internal PK_VECTOR_s* Points;internal double* First;internal double* Second;internal PK_UV_s* Uv;internal int* Kinds;
        public void Dispose(){Free(Points);Free(First);Free(Second);Free(Uv);Free(Kinds);}}
    private struct SurfaceResult:IDisposable
    {internal int PointCount,CurveCount;internal PK_VECTOR_s* Points;internal int* Curves;internal PK_INTERVAL_s* Intervals;internal int* Kinds;
        public void Dispose(){for(var i=0;i<CurveCount;i++)Assert.Equal(0,KernelRuntime.EntityDelete(1,Curves+i));Free(Points);Free(Curves);Free(Intervals);Free(Kinds);}}
}
