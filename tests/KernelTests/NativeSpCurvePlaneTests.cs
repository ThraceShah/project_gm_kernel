using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NativeSpCurvePlaneTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void HomogeneousNativeUvCompositionEnclosesPositionsDerivativesAndFiniteRoots(bool rationalUv,bool rationalSurface)
    {
        Span<IntervalNumber> uv=stackalloc IntervalNumber[12],surface=stackalloc IntervalNumber[16],composed=stackalloc IntervalNumber[68];
        var n=rationalUv?2:1;
        if(rationalUv)
        {
            uv[0]=uv[1]=0;uv[3]=2;uv[4]=.5;uv[5]=1;uv[7]=1.5;uv[8]=uv[9]=uv[11]=1;
        }
        else{uv[0]=uv[1]=0;uv[3]=1;uv[4]=uv[5]=uv[7]=1;}
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            var w=rationalSurface?1+2*u+v:1;var at=4*(2*u+v);surface[at]=u*w;surface[at+1]=v*w;surface[at+2]=u*v*w;surface[at+3]=w;
        }
        Assert.Equal(AlgorithmStatus.Success,SpCurveBezier.Compose(new(surface,1,1,new(0,1,0,1)),uv,n,composed,out var degree));
        var patch=new BezierSurfacePatch(composed[..(4*(degree+1))],degree,0,new(0,1,0,1));Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Bound(patch,new(.1,.9,0,0),bounds));
        for(var sample=1;sample<10;sample++)
        {
            var t=sample*.1;var u=rationalUv?t/(2-t):t;var du=rationalUv?2/((2-t)*(2-t)):1;var v=t;
            var d=rationalSurface?1+2*u+v:1;var dd=rationalSurface?2*du+1:0;
            var x=rationalSurface?3*u+u*v:u;var y=rationalSurface?2*v+2*u*v:v;var z=rationalSurface?4*u*v:u*v;
            var dx=rationalSurface?3*du+du*v+u:du;var dy=rationalSurface?2+2*du*v+2*u:1;var dz=rationalSurface?4*du*v+4*u:du*v+u;
            Contains(bounds[0],x/d);Contains(bounds[1],y/d);Contains(bounds[2],z/d);
            Contains(bounds[3],(dx*d-x*dd)/(d*d));Contains(bounds[4],(dy*d-y*dd)/(d*d));Contains(bounds[5],(dz*d-z*dd)/(d*d));
        }
        const double station=.375;var a=rationalUv?station/(2-station):station;
        var height=rationalSurface?4*a*station/(1+2*a+station):a*station;
        var plane=new AnalyticSurface(SurfaceClass.Plane,new(){Z=height},new(){Z=1},new(){X=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[8];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(new(patch),plane,0,1,roots,ref count,strictSeparation:true,isolation:proof));
        Assert.Equal(1,count);Assert.True(proof[0].Certified);Assert.InRange(Math.Abs(roots[0].First-station),0,1e-10);
        count=0;Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(new(patch),plane,.7,.9,roots,ref count,strictSeparation:true,isolation:proof));Assert.Equal(0,count);
    }

    [Fact]
    public void NativeUvOutsideTheCellIsRejectedWithoutSnappingItsCoordinates()
    {
        Span<IntervalNumber> uv=stackalloc IntervalNumber[8]{-1e-13,0,0,1,1,1,0,1};
        Span<IntervalNumber> surface=stackalloc IntervalNumber[16]{0,0,0,1,0,1,0,1,1,0,0,1,1,1,1,1},result=stackalloc IntervalNumber[12];
        Assert.Equal(AlgorithmStatus.Unsupported,SpCurveBezier.Compose(new(surface,1,1,new(0,1,0,1)),uv,1,result,out _));
    }

    [Theory]
    [InlineData(.375)] [InlineData(.625)]
    public void ContinuousNativePiecesEncloseTheJoinAndKeepTwoDistinctNearbyRoots(double knot)
    {
        Span<IntervalNumber> global=stackalloc IntervalNumber[12]{0,0,knot*knot,1,.5,.5,knot*knot-knot,1,1,1,(1-knot)*(1-knot),1};
        var source=new BezierSurfacePatch(global,2,0,new(0,1,0,1));
        Span<SurfaceParameterBox> domains=stackalloc SurfaceParameterBox[2]{new(.1,knot,0,1),new(knot,.9,0,1)};
        Span<IntervalNumber> pieces=stackalloc IntervalNumber[24],values=stackalloc IntervalNumber[9],corner=stackalloc IntervalNumber[4];
        for(var i=0;i<2;i++)Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Restrict(source,domains[i],pieces.Slice(12*i,12)));
        var curve=new SurfaceIntersectionPatch(pieces,domains,2);
        Assert.True(curve.IsProcedural&&curve.UVariable&&!curve.VVariable);Assert.Equal(2,curve.UDegree);Assert.Equal(0,curve.VDegree);
        Assert.False(curve.ExactCorner(knot,0,corner));
        Assert.Equal(AlgorithmStatus.Success,curve.Bound(new(knot-.0625,knot+.0625,0,0),values));
        Contains(values[2],0);Contains(values[2],.0625*.0625);Contains(values[5],-.125);Contains(values[5],.125);
        var plane=new AnalyticSurface(SurfaceClass.Plane,new(){Z=.0625*.0625},new(){Z=1},new(){X=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];Span<ScalarRootIsolation> proof=stackalloc ScalarRootIsolation[8];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,plane,.1,.9,roots,ref count,strictSeparation:true,isolation:proof));
        Assert.Equal(2,count);Assert.True(proof[0].Certified&&proof[1].Certified&&!proof[0].SameRoot(proof[1]));
        Assert.InRange(Math.Abs(Math.Min(roots[0].First,roots[1].First)-(knot-.0625)),0,1e-10);
        Assert.InRange(Math.Abs(Math.Max(roots[0].First,roots[1].First)-(knot+.0625)),0,1e-10);
        var shifted=new SurfaceIntersectionPatch(curve,.1);Assert.Equal(AlgorithmStatus.Unsupported,shifted.Bound(new(.2,.8,0,0),values));
        domains[1]=new(knot+.01,.9,0,1);Assert.Equal(AlgorithmStatus.Unsupported,curve.Bound(new(.1,.9,0,0),values));
    }

    [Fact]
    public void C0KnotsCannotSupplyTheContinuousDerivativeContract()
    {
        Assert.True(SpCurveBezier.HasC1Joins([0,0,0,.375,1,1,1],2,4));
        Assert.False(SpCurveBezier.HasC1Joins([0,0,0,.375,.375,1,1,1],2,5));
    }

    private static void Contains(IntervalNumber bound,double value)=>Assert.True(bound.Lower<=value+1e-13&&bound.Upper>=value-1e-13,$"{value:R} outside [{bound.Lower:R},{bound.Upper:R}]");
}
