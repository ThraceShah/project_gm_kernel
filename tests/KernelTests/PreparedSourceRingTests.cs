using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class PreparedSourceRingTests:IDisposable
{
    public PreparedSourceRingTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Theory]
    [InlineData(.125,false)]
    [InlineData(1,true)]
    [InlineData(8,false)]
    public void OnlyAnEndpointFreeWholeRingCanChooseAWindowWithCompleteNativeRoots(double scale,bool ellipse)
    {
        var basis=new PK_AXIS2_sf_s();basis.location.coord[0]=7;basis.location.coord[1]=-11;basis.location.coord[2]=4;
        basis.axis.coord[2]=1;basis.ref_direction.coord[0]=.8;basis.ref_direction.coord[1]=-.6;
        CurveTag curve;
        if(ellipse){var definition=new PK_ELLIPSE_sf_s{basis_set=basis,R1=4*scale,R2=2*scale};Assert.Equal(0,KernelRuntime.EllipseCreate(&definition,&curve));}
        else{var definition=new PK_CIRCLE_sf_s{basis_set=basis,radius=4*scale};Assert.Equal(0,KernelRuntime.CircleCreate(&definition,&curve));}
        var cutting=new PK_PLANE_sf_s();cutting.basis_set.location=basis.location;
        cutting.basis_set.axis.coord[0]=.6;cutting.basis_set.axis.coord[1]=.8;cutting.basis_set.ref_direction.coord[2]=1;
        SurfTag plane;Assert.Equal(0,KernelRuntime.PlaneCreate(&cutting,&plane));
        var full=new KernelRuntime.ModelingEdge{Curve=curve,Start=0,End=Math.Tau,StartVertex=-1,EndVertex=-1};
        var anchored=full;anchored.StartVertex=anchored.EndVertex=0;
        var partial=full;partial.End=Math.PI;
        Span<KernelRuntime.ModelingEdge> edges=stackalloc KernelRuntime.ModelingEdge[3]{full,anchored,partial};
        Span<KernelRuntime.ModelingFace> faces=stackalloc KernelRuntime.ModelingFace[1]{new(){Surface=plane}};
        KernelRuntime.PrepareRegularSourceRingWindows(edges,new(edges,default,default,faces));
        Assert.True(edges[0].Start>0);Assert.Equal(0,edges[1].Start);Assert.Equal(Math.Tau,edges[1].End);
        Assert.Equal(0,edges[2].Start);Assert.Equal(Math.PI,edges[2].End);
        PK_INTERVAL_s native;Assert.Equal(0,KernelRuntime.CurveAskInterval(curve,&native));Assert.Equal(0,native.value[0]);Assert.Equal(Math.Tau,native.value[1]);
        var analytic=new AnalyticSurface(SurfaceClass.Plane,new(){X=7,Y=-11,Z=4},new(){X=.6,Y=.8},new(){Z=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[8];Span<double> error=stackalloc double[8];
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.IsolateFiniteExplicitLevelSet(edges[0],analytic,roots,proof,error,out var count));Assert.Equal(2,count);
        for(var i=0;i<count;i++){Assert.True(proof[i].Certified);Assert.True(proof[i].Root.Lower>edges[0].Start);Assert.True(proof[i].Root.Upper<edges[0].End);}
    }
}
