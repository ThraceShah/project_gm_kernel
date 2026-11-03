using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Complete bounded positive-weight B-spline/analytic-support intersection.
/// Homogeneous substitution produces a polynomial of degree 2p (quadrics) or
/// 4p (a ring torus). Derivative isolation visits every possible branch in each
/// knot span; uncertain floating-point critical values are explicit failures.
/// </summary>
internal static class BCurveAnalyticIntersection
{
    internal static AlgorithmStatus Find(in BCurveView curve,in AnalyticSurface surface,double lo,double hi,
        Span<OperationIntersectionPoint> points,out BufferCount count)
    {
        if(surface.Kind==SurfaceClass.Plane)return BCurvePlaneIntersection.Find(curve,surface,lo,hi,points,out count);
        count=0;
        if(surface.Kind is not (SurfaceClass.Sphere or SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Torus)
            ||curve.Degree<1||curve.Degree>(surface.Kind==SurfaceClass.Torus?4:8)
            ||curve.Dimension!=(curve.Rational?4:3)||lo<curve.Start||hi>curve.End
            ||surface.Kind==SurfaceClass.Torus&&!(surface.Radius>surface.Secondary))return AlgorithmStatus.Unsupported;
        Span<double> workspace=stackalloc double[9*9*4];Span<double> polynomial=stackalloc double[17];
        Span<double> generator=stackalloc double[9];Span<double> roots=stackalloc double[17];
        Span<double> evaluate=stackalloc double[9*2*4];Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var pending=false;var pendingStart=0.0;var pendingEnd=0.0;
        for(var span=curve.Degree;span<curve.VertexCount;span++)
        {
            var start=Math.Max(curve.Knots[span],lo);var end=Math.Min(curve.Knots[span+1],hi);
            if(!(end>start))continue;
            var status=SpanPolynomial(curve,surface,span,start,end,workspace,polynomial,generator,
                out var degree,out var scale,out var minWeight,out var coefficientMagnitude);
            if(status!=AlgorithmStatus.Success)return status;
            var coincident=CoincidentBound(polynomial[..(degree+1)],generator[..(curve.Degree+1)],surface,scale,minWeight,coefficientMagnitude);
            if(coincident)
            {
                // A cone's algebraic zero set contains both nappes. Isolate
                // every generator zero before classifying this root-free span.
                var rootCount=0;
                if(surface.Kind==SurfaceClass.Cone)
                {
                    status=BoundedPolynomialRoots.Find(generator[..(curve.Degree+1)],-1,1,roots,out rootCount);
                    if(status!=AlgorithmStatus.Success)return status;
                    // The apex is singular and the native intersection query
                    // rejects a coincident generator that passes through it.
                    if(rootCount!=0)return AlgorithmStatus.NumericalFailure;
                }
                if(surface.Kind==SurfaceClass.Cone&&Value(generator[..(curve.Degree+1)],0)<=0)continue;
                if(pending&&start>pendingEnd)
                {status=Flush(curve,surface,evaluate,jet,points,ref count,ref pending,pendingStart,pendingEnd);if(status!=AlgorithmStatus.Success)return status;}
                if(!pending){pending=true;pendingStart=start;}pendingEnd=end;
                continue;
            }
            if(pending)
            {status=Flush(curve,surface,evaluate,jet,points,ref count,ref pending,pendingStart,pendingEnd);if(status!=AlgorithmStatus.Success)return status;}
            status=BoundedPolynomialRoots.Find(polynomial[..(degree+1)],-1,1,roots,out var n);
            if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<n;i++)
            {
                var t=Math.Clamp(Math.FusedMultiplyAdd((roots[i]+1)/2,end-start,start),start,end);
                status=EvaluateAndAdd(curve,surface,t,IntersectionVectorKind.Simple,evaluate,jet,points,ref count);
                if(status!=AlgorithmStatus.Success)return status;
            }
        }
        if(pending)
        {var status=Flush(curve,surface,evaluate,jet,points,ref count,ref pending,pendingStart,pendingEnd);if(status!=AlgorithmStatus.Success)return status;}
        OperationIntersection.Sort(points[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus SpanPolynomial(in BCurveView curve,in AnalyticSurface surface,KnotIndex span,
        double start,double end,Span<double> workspace,Span<double> polynomial,Span<double> generator,
        out SplineDegree degree,out double coordinateScale,out double minWeight,out double coefficientMagnitude)
    {
        degree=curve.Degree*(surface.Kind==SurfaceClass.Torus?4:2);coordinateScale=surface.Radius;
        minWeight=double.PositiveInfinity;coefficientMagnitude=0;var maxWeight=0.0;var p=curve.Degree;var stride=(p+1)*4;
        if(!double.IsFinite(end-start)||!((end-start)/2>0))return AlgorithmStatus.NumericalFailure;
        for(var j=span-p;j<=span;j++)
        {
            var at=j*curve.Dimension;var w=curve.Rational?curve.Vertices[at+3]:1;
            if(!(w>0)||!double.IsFinite(w))return AlgorithmStatus.Unsupported;
            minWeight=Math.Min(minWeight,w);maxWeight=Math.Max(maxWeight,w);
            var x=curve.Vertices[at]/w-surface.Origin.X;var y=curve.Vertices[at+1]/w-surface.Origin.Y;var z=curve.Vertices[at+2]/w-surface.Origin.Z;
            coordinateScale=Math.Max(coordinateScale,Math.Max(Math.Abs(x),Math.Max(Math.Abs(y),Math.Abs(z))));
        }
        if(!(coordinateScale>0)||!double.IsFinite(coordinateScale)||!(minWeight/maxWeight>0))return AlgorithmStatus.NumericalFailure;
        // Powers of two keep normalization exact for representable values;
        // arbitrary scale division can turn an exact tangent into a root pair.
        coordinateScale=Math.ScaleB(1,Math.ILogB(coordinateScale));maxWeight=Math.ScaleB(1,Math.ILogB(maxWeight));
        minWeight/=maxWeight;workspace.Clear();polynomial.Clear();generator.Clear();
        for(var j=0;j<=p;j++)
        {
            var at=(span-p+j)*curve.Dimension;var w=(curve.Rational?curve.Vertices[at+3]:1)/maxWeight;
            workspace[j*stride]=Math.FusedMultiplyAdd(-surface.Origin.X,w,curve.Vertices[at]/maxWeight)/coordinateScale;
            workspace[j*stride+1]=Math.FusedMultiplyAdd(-surface.Origin.Y,w,curve.Vertices[at+1]/maxWeight)/coordinateScale;
            workspace[j*stride+2]=Math.FusedMultiplyAdd(-surface.Origin.Z,w,curve.Vertices[at+2]/maxWeight)/coordinateScale;
            workspace[j*stride+3]=w;
        }
        for(var r=1;r<=p;r++)for(var j=p;j>=r;j--)
        {
            var index=span-p+j;var denominator=curve.Knots[index+p-r+1]-curve.Knots[index];
            if(!(denominator>0)||!double.IsFinite(denominator))return AlgorithmStatus.NumericalFailure;
            // Center the coordinate polynomial before forming its squares or
            // fourth powers. Centering only the final implicit polynomial loses
            // small root separations to cancellation of its large coefficients.
            var alpha=Math.FusedMultiplyAdd(.5,end-start,start-curve.Knots[index])/denominator;var beta=((end-start)/2)/denominator;
            for(var k=r;k>=0;k--)for(var d=0;d<4;d++)
            {
                var target=j*stride+4*k+d;var left=target-stride;
                var value=Math.FusedMultiplyAdd(alpha,workspace[target]-workspace[left],workspace[left]);
                if(k>0)value=Math.FusedMultiplyAdd(beta,workspace[target-4]-workspace[left-4],value);
                workspace[target]=value;
            }
        }
        var coefficients=workspace.Slice(p*stride,stride);
        Span<KernelVector3> position=stackalloc KernelVector3[9];Span<KernelVector3> radial=stackalloc KernelVector3[9];
        Span<double> weight=stackalloc double[9];Span<double> norm=stackalloc double[17];Span<double> radialNorm=stackalloc double[17];Span<double> w2=stackalloc double[17];
        norm.Clear();radialNorm.Clear();w2.Clear();
        var radius=surface.Radius/coordinateScale;var minor=surface.Secondary/coordinateScale;
        // Squared homogeneous factors must not silently underflow to an
        // identically-zero implicit polynomial (e.g. a unit-radius cylinder
        // whose curve has axial coordinates near 1e200).
        if(surface.Radius>0&&!(radius*radius>0)
            ||surface.Kind==SurfaceClass.Torus&&!(minor*minor>0))return AlgorithmStatus.NumericalFailure;
        for(var i=0;i<=p;i++)
        {
            var r=Vector(coefficients[4*i],coefficients[4*i+1],coefficients[4*i+2]);var z=Dot(r,surface.Axis);
            position[i]=r;radial[i]=Sub(r,Scale(surface.Axis,z));weight[i]=coefficients[4*i+3];
            generator[i]=Math.FusedMultiplyAdd(surface.Secondary,z,radius*weight[i]);
            if(surface.Kind is SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Torus
                &&Dot(radial[i],radial[i])==0&&OperationIntersection.Norm(radial[i])>0
                ||surface.Kind==SurfaceClass.Cone&&generator[i]!=0&&generator[i]*generator[i]==0)
                return AlgorithmStatus.NumericalFailure;
            coefficientMagnitude+=Math.Abs(r.X)+Math.Abs(r.Y)+Math.Abs(r.Z)+Math.Abs(weight[i]);
        }
        for(var i=0;i<=p;i++)for(var j=0;j<=p;j++)
        {norm[i+j]+=Dot(position[i],position[j]);radialNorm[i+j]+=Dot(radial[i],radial[j]);w2[i+j]=Math.FusedMultiplyAdd(weight[i],weight[j],w2[i+j]);}
        if(surface.Kind==SurfaceClass.Torus)
        {
            for(var i=0;i<=2*p;i++)norm[i]=Math.FusedMultiplyAdd(radius*radius-minor*minor,w2[i],norm[i]);
            for(var i=0;i<=2*p;i++)for(var j=0;j<=2*p;j++)
                polynomial[i+j]+=Math.FusedMultiplyAdd(norm[i],norm[j],-4*radius*radius*radialNorm[i]*w2[j]);
        }
        else if(surface.Kind==SurfaceClass.Cone)
        {
            radialNorm[..(2*p+1)].CopyTo(polynomial);
            for(var i=0;i<=p;i++)for(var j=0;j<=p;j++)polynomial[i+j]=Math.FusedMultiplyAdd(-generator[i],generator[j],polynomial[i+j]);
        }
        else
        {
            var squared=surface.Kind==SurfaceClass.Sphere?norm:radialNorm;
            for(var i=0;i<=2*p;i++)polynomial[i]=Math.FusedMultiplyAdd(-radius*radius,w2[i],squared[i]);
        }
        for(var i=0;i<=degree;i++)if(!double.IsFinite(polynomial[i]))return AlgorithmStatus.NumericalFailure;
        return AlgorithmStatus.Success;
    }

    private static bool CoincidentBound(ReadOnlySpan<double> polynomial,ReadOnlySpan<double> generator,in AnalyticSurface surface,
        double scale,double minWeight,double coefficientMagnitude)
    {
        var bound=0.0;for(var i=0;i<polynomial.Length;i++)bound+=Math.Abs(polynomial[i]);
        // A power-basis absolute sum bounds the numerator on [-1,1]. Positive
        // B-spline weights bound the denominator below throughout this span.
        // The rounding allowance also rejects an ill-conditioned conversion.
        var magnitude=Math.Max(1,coefficientMagnitude);var allowance=512*2.2204460492503131e-16*magnitude*magnitude;
        var radius=surface.Radius/scale;var w2=minWeight*minWeight;
        if(surface.Kind==SurfaceClass.Torus)
        {
            allowance*=magnitude*magnitude;var minor=surface.Secondary/scale;
            var denominator=(radius*radius-minor*minor)*minor*w2*w2;
            if(!(denominator>0)||Dot(surface.Axis,surface.Axis)!=1)return false;
            return (bound+allowance)/denominator<=OperationIntersection.DistanceTolerance/scale;
        }
        if(surface.Kind==SurfaceClass.Cone)
        {
            var generatorLower=Math.Abs(generator[0]);for(var i=1;i<generator.Length;i++)generatorLower-=Math.Abs(generator[i]);
            return generatorLower>0&&(bound+allowance)/(generatorLower*minWeight)<=OperationIntersection.DistanceTolerance/scale;
        }
        return radius>0&&(bound+allowance)/(radius*w2)<=OperationIntersection.DistanceTolerance/scale;
    }

    private static double Value(ReadOnlySpan<double> p,double t)
    {var v=p[^1];for(var i=p.Length-2;i>=0;i--)v=Math.FusedMultiplyAdd(v,t,p[i]);return v;}

    private static AlgorithmStatus Flush(in BCurveView curve,in AnalyticSurface surface,Span<double> workspace,
        Span<KernelVector3> jet,Span<OperationIntersectionPoint> points,ref BufferCount count,ref bool pending,double start,double end)
    {
        pending=false;var status=EvaluateAndAdd(curve,surface,start,IntersectionVectorKind.Start,workspace,jet,points,ref count);
        return status==AlgorithmStatus.Success?EvaluateAndAdd(curve,surface,end,IntersectionVectorKind.End,workspace,jet,points,ref count):status;
    }

    private static AlgorithmStatus EvaluateAndAdd(in BCurveView curve,in AnalyticSurface surface,double t,
        IntersectionVectorKind kind,Span<double> workspace,Span<KernelVector3> jet,Span<OperationIntersectionPoint> points,ref BufferCount count)
    {
        var status=BCurveEvaluation.Evaluate(curve,t,1,jet,workspace,out var tangent);if(status!=AlgorithmStatus.Success)return status;
        if(surface.Kind==SurfaceClass.Cone)
        {
            var r=Sub(jet[0],surface.Origin);var z=Dot(r,surface.Axis);var g=Math.FusedMultiplyAdd(surface.Secondary,z,surface.Radius);
            if(Math.Abs(g)<=OperationIntersection.DistanceTolerance&&OperationIntersection.Norm(Sub(r,Scale(surface.Axis,z)))<=OperationIntersection.DistanceTolerance)
                return AlgorithmStatus.NumericalFailure;
            if(g<0)return kind==IntersectionVectorKind.Simple?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
        }
        status=AnalyticImplicitEvaluation.GeometricDeviation(surface,jet[0],out var deviation);
        if(status!=AlgorithmStatus.Success||deviation>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NumericalFailure;
        if(kind==IntersectionVectorKind.Simple)
        {
            status=AnalyticImplicitEvaluation.Evaluate(surface,jet[0],1,out var implicitJet);if(status!=AlgorithmStatus.Success)return status;
            if(OperationIntersection.Norm(implicitJet.Gradient)==0||OperationIntersection.Norm(tangent)==0)return AlgorithmStatus.Singular;
            if(Math.Abs(Dot(Unit(implicitJet.Gradient),tangent))<=1e-8)kind=IntersectionVectorKind.Tangent;
        }
        return OperationIntersection.AddPoint(points,ref count,jet[0],t,0,kind)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
    }
}
