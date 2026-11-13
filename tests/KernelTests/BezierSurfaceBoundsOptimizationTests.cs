using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class BezierSurfaceBoundsOptimizationTests
{
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(true,600)] [InlineData(true,-600)]
    public void SingleClampedSpanHasItsOriginalHomogeneousBezierNetAndEnclosesRectangularJets(bool rational,int exponent)
    {
        var dim=rational?4:3;var vertices=new double[15*dim];
        for(var u=0;u<5;u++)for(var v=0;v<3;v++)
        {
            var at=dim*(3*u+v);var weight=rational?Math.ScaleB(1+.13*u+.21*v,exponent):1;
            vertices[at]=(u*.25+v*.125)*weight;vertices[at+1]=(v*.5-u*.0625)*weight;vertices[at+2]=(.125*u*u-.25*v*v+.0625*u*v)*weight;
            if(rational)vertices[at+3]=weight;
        }
        double[] uk=[-2,-2,-2,-2,-2,2,2,2,2,2],vk=[.5,.5,.5,2.5,2.5,2.5];
        var view=new BSurfaceView(4,2,5,3,dim,rational,false,false,vertices,uk,vk);
        Span<IntervalNumber> actual=stackalloc IntervalNumber[60],reference=stackalloc IntervalNumber[60];
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Extract(view,4,2,actual));
        SymbolicExtract(view,4,2,reference);
        var scale=rational?int.MinValue:0;for(var i=0;rational&&i<15;i++)scale=Math.Max(scale,Math.ILogB(vertices[4*i+3]));
        for(var i=0;i<15;i++)for(var d=0;d<4;d++)
        {
            var expected=d==3&&!rational?1:Math.ScaleB(vertices[dim*i+d],-scale);
            Assert.Equal(expected,actual[4*i+d].Lower);Assert.Equal(expected,actual[4*i+d].Upper);
            Assert.InRange(expected,reference[4*i+d].Lower,reference[4*i+d].Upper);
        }
        var patch=new BezierSurfacePatch(actual,4,2,new(-2,2,.5,2.5));var box=new SurfaceParameterBox(-.7,.4,.8,1.2);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[27];Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundRectangularSecond(patch,box,bounds));
        SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[9];Span<double> workspace=stackalloc double[4096];
        for(var i=0;i<=6;i++)for(var j=0;j<=6;j++)
        {
            var atU=box.U0+(box.U1-box.U0)*i/6;var atV=box.V0+(box.V1-box.V0)*j/6;
            if(rational)Assert.Equal(AlgorithmStatus.Success,BSurfaceEvaluation.EvaluateInDomain(view,atU,atV,layout,workspace,jet));
            for(var u=0;u<=2;u++)for(var v=0;v<=2;v++)Check(bounds.Slice(3*(3*u+v),3),rational?jet[layout.GetIndex(u,v)]:PolynomialJet(atU,atV,u,v));
        }
    }

    [Fact]
    public void EqualPoleCountsWithoutFullEndClampingStillUseSymbolicExtraction()
    {
        double[] vertices=[0,0,0,0,1,.3,0,2,.6,1,0,.5,1,1,.1,1,2,.2,2,0,.2,2,1,.3,2,2,1];
        double[] knots=[-1,0,0,1,1,2];var view=new BSurfaceView(2,2,3,3,3,false,false,false,vertices,knots,knots);
        Span<IntervalNumber> actual=stackalloc IntervalNumber[36],reference=stackalloc IntervalNumber[36];
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Extract(view,2,2,actual));SymbolicExtract(view,2,2,reference);
        for(var i=0;i<36;i++){Assert.Equal(reference[i].Lower,actual[i].Lower);Assert.Equal(reference[i].Upper,actual[i].Upper);}
    }

    [Theory]
    [InlineData(-.25)] [InlineData(0)]
    public void NonpositiveWeightDoesNotAcquireAnUncertifiedCartesianBound(double weight)
    {
        double[] vertices=[0,0,0,1,0,1,0,1,1,0,0,1,1,1,1,weight],knots=[0,0,1,1];
        var view=new BSurfaceView(1,1,2,2,4,true,false,false,vertices,knots,knots);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16],bounds=stackalloc IntervalNumber[27];
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Extract(view,1,1,poles));
        Assert.Equal(AlgorithmStatus.NumericalFailure,BezierSurfaceBounds.BoundRectangularSecond(new(poles,1,1,new(0,1,0,1)),new(0,1,0,1),bounds));
    }

    [Fact]
    public void NonzeroSubnormalNormalizationRemainsOutward()
    {
        var coordinate=Math.ScaleB(3,-52);var weight=Math.ScaleB(1,1023);
        double[] vertices=[coordinate,0,0,weight,coordinate,weight,0,weight,weight,0,0,weight,weight,weight,0,weight],knots=[0,0,1,1];
        var view=new BSurfaceView(1,1,2,2,4,true,false,false,vertices,knots,knots);Span<IntervalNumber> poles=stackalloc IntervalNumber[16];
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Extract(view,1,1,poles));
        Assert.InRange(coordinate,Math.ScaleB(poles[0].Lower,1023),Math.ScaleB(poles[0].Upper,1023));
        Assert.True(poles[0].Lower<poles[0].Upper);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReusedHomogeneousFirstHullsAreBitwiseEqualToIndependentRepeatedRestriction(bool rational)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36];
        for(var u=0;u<3;u++)for(var v=0;v<3;v++)
        {
            var at=4*(3*u+v);var w=rational?1+.2*u+.3*v:1;
            poles[at]=new((u*.4+v*.05)*w-1e-14,(u*.4+v*.05)*w+1e-14);
            poles[at+1]=(v*.5-u*.03)*w;poles[at+2]=(.2*u*u-.17*v*v+.02*u*v)*w;poles[at+3]=w;
        }
        var patch=new BezierSurfacePatch(poles,2,2,new(-1,2,.2,1.4));
        foreach(var box in new[]{new SurfaceParameterBox(-.2,.4,.4,.9),new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(-1.001,-.8,1.3,1.401)})
        {
            Span<IntervalNumber> actual=stackalloc IntervalNumber[27],reference=stackalloc IntervalNumber[27];
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundRectangularSecond(patch,box,actual));RepeatedRectangular(patch,box,reference);
            for(var i=0;i<27;i++){Assert.Equal(reference[i].Lower,actual[i].Lower);Assert.Equal(reference[i].Upper,actual[i].Upper);}
            Span<IntervalNumber> second=stackalloc IntervalNumber[18];
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundSecond(patch,box,second));
            for(var jet=0;jet<6;jet++)for(var axis=0;axis<3;axis++)
            {
                var at=jet==0?0:jet==1?9:jet==2?3:jet==3?18:jet==4?12:6;
                Assert.Equal(reference[at+axis].Lower,second[3*jet+axis].Lower);
                Assert.Equal(reference[at+axis].Upper,second[3*jet+axis].Upper);
            }
        }
    }

    // Deliberately retain the general symbolic reference for the specialization.
    private static void SymbolicExtract(in BSurfaceView view,int us,int vs,Span<IntervalNumber> output)
    {
        var p=view.UDegree;var q=view.VDegree;Span<IntervalNumber> layers=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],input=stackalloc IntervalNumber[68],result=stackalloc IntervalNumber[68];
        var weight=1.0;if(view.Rational){weight=0;for(var u=us-p;u<=us;u++)for(var v=vs-q;v<=vs;v++)weight=Math.Max(weight,view.Vertices[4*(u*view.VVertexCount+v)+3]);}
        var exponent=Math.ILogB(weight);
        for(var v=0;v<=q;v++)
        {
            for(var u=0;u<=p;u++)for(var d=0;d<4;d++)input[4*u+d]=Math.ScaleB(d==3&&!view.Rational?1:view.Vertices[((us-p+u)*view.VVertexCount+vs-q+v)*view.Dimension+d],-exponent);
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.ExtractCurve(input,p,view.UKnots,us,result));
            for(var u=0;u<=p;u++)for(var d=0;d<4;d++)layers[4*(u*(q+1)+v)+d]=result[4*u+d];
        }
        for(var u=0;u<=p;u++){layers.Slice(4*u*(q+1),4*(q+1)).CopyTo(input);Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.ExtractCurve(input,q,view.VKnots,vs,result));result[..(4*(q+1))].CopyTo(output[(4*u*(q+1))..]);}
    }

    private static void RepeatedRectangular(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        Span<IntervalNumber> first=stackalloc IntervalNumber[9],h=stackalloc IntervalNumber[36];Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Bound(patch,box,first));
        for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)JetHull(patch,box,i,j,h.Slice(4*(3*i+j),4));
        for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)for(var d=0;d<3;d++)
        {
            var at=3*(3*i+j)+d;if(i==0&&j==0){output[at]=first[d];continue;}if(i==1&&j==0){output[at]=first[3+d];continue;}if(i==0&&j==1){output[at]=first[6+d];continue;}
            var value=h[4*(3*i+j)+d];for(var a=0;a<=i;a++)for(var b=0;b<=j;b++){if(a==i&&b==j)continue;value-=(i==2&&a==1?2:1)*(j==2&&b==1?2:1)*output[3*(3*a+b)+d]*h[4*(3*(i-a)+(j-b))+3];}output[at]=value/h[3];
        }
    }

    private static void JetHull(in BezierSurfacePatch patch,in SurfaceParameterBox box,int du,int dv,Span<IntervalNumber> hull)
    {
        if(du>patch.UDegree||dv>patch.VDegree){hull.Clear();return;}
        Span<IntervalNumber> a=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],b=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];patch.Poles.CopyTo(a);var p=patch.UDegree;var q=patch.VDegree;
        for(var axis=0;axis<2;axis++)for(var order=0;order<(axis==0?du:dv);order++)
        {
            var np=p-(axis==0?1:0);var nq=q-(axis==1?1:0);var scale=axis==0?p/(new IntervalNumber(patch.Domain.U1)-patch.Domain.U0):q/(new IntervalNumber(patch.Domain.V1)-patch.Domain.V0);
            for(var u=0;u<=np;u++)for(var v=0;v<=nq;v++)for(var d=0;d<4;d++){var at=4*(u*(q+1)+v)+d;b[4*(u*(nq+1)+v)+d]=scale*(a[at+(axis==0?4*(q+1):4)]-a[at]);}b[..(4*(np+1)*(nq+1))].CopyTo(a);p=np;q=nq;
        }
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Restrict(new(a[..(4*(p+1)*(q+1))],p,q,patch.Domain),box,b));
        for(var d=0;d<4;d++){hull[d]=b[d];for(var i=1;i<(p+1)*(q+1);i++)hull[d]=IntervalNumber.Hull(hull[d],b[4*i+d]);}
    }

    private static void Check(ReadOnlySpan<IntervalNumber> bounds,in KernelVector3 value)
    {Assert.InRange(value.X,bounds[0].Lower,bounds[0].Upper);Assert.InRange(value.Y,bounds[1].Lower,bounds[1].Upper);Assert.InRange(value.Z,bounds[2].Lower,bounds[2].Upper);}

    // Independent binomial moments of the polynomial net: E[I]=4s,
    // E[I²]=4s+12s², E[J]=2t, E[J²]=2t+2t². Exact zero derivatives avoid
    // mistaking floating evaluator cancellation noise for geometric error.
    private static KernelVector3 PolynomialJet(double u,double v,int du,int dv)
    {
        var s=(u+2)/4;var t=(v-.5)/2;
        return(du,dv) switch
        {
            (0,0)=>new(){X=s+.25*t,Y=t-.25*s,Z=.5*s+1.5*s*s-.5*t-.5*t*t+.5*s*t},
            (1,0)=>new(){X=.25,Y=-.0625,Z=(.5+3*s+.5*t)/4},
            (0,1)=>new(){X=.125,Y=.5,Z=(-.5-t+.5*s)/2},
            (2,0)=>new(){Z=.1875},(1,1)=>new(){Z=.0625},(0,2)=>new(){Z=-.25},_=>default,
        };
    }
}
