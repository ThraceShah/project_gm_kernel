using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Exact intersection of a bounded positive-weight ruled NURBS patch and a
/// plane. On each u span the homogeneous surface is A(u)+v B(u). Substituting
/// v=-plane(A)/plane(B) gives a rational polynomial of degree at most 2p.
/// Every numerator, opposite-boundary and denominator root partitions the
/// domain, so a midpoint classifies a whole root-free interval, not a sample
/// approximation to the intersection. Unresolved common factors fail closed.
/// </summary>
internal static unsafe class RuledBSurfacePlaneIntersection
{
    internal static AlgorithmStatus Find(in BSurfaceView surface,in AnalyticSurface plane,
        Span<OperationIntersectionBranch> branches,out BufferCount curveCount,
        Span<KernelVector3> isolated,out BufferCount pointCount)
    {
        curveCount=0;pointCount=0;
        if(surface.UDegree<1||surface.UDegree>8||surface.VDegree!=1||surface.VVertexCount!=2
            ||surface.UPeriodic||surface.VPeriodic||surface.Dimension!=(surface.Rational?4:3))return AlgorithmStatus.Unsupported;
        var p=surface.UDegree;
        Span<double> left=stackalloc double[36];Span<double> right=stackalloc double[36];
        Span<double> f0=stackalloc double[9];Span<double> f1=stackalloc double[9];Span<double> difference=stackalloc double[9];
        Span<double> breaks=stackalloc double[32];Span<double> roots=stackalloc double[16];
        Span<double> rational=stackalloc double[68];
        Span<byte> touches=stackalloc byte[32];
        for(var span=p;span<surface.UVertexCount;span++)
        {
            var u0=surface.UKnots[span];var u1=surface.UKnots[span+1];if(!(u1>u0))continue;
            var status=LayerPolynomial(surface,span,0,left);if(status!=AlgorithmStatus.Success)return status;
            status=LayerPolynomial(surface,span,1,right);if(status!=AlgorithmStatus.Success)return status;
            var same=true;var zero0=true;var zero1=true;
            for(var k=0;k<=p;k++)
            {
                f0[k]=PlaneNumerator(left,k,plane);f1[k]=PlaneNumerator(right,k,plane);difference[k]=f1[k]-f0[k];
                if(f0[k]!=0)zero0=false;if(f1[k]!=0)zero1=false;if(difference[k]!=0)same=false;
            }
            if(zero0&&zero1)continue; // fully coincident surface area has no intersection curves in PK.
            if(same)
            {
                status=BoundedPolynomialRoots.Find(f0[..(p+1)],0,1,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var i=0;i<n;i++)
                {
                    var a=EvaluateLayer(left,p,roots[i]);var b=EvaluateLayer(right,p,roots[i]);
                    if(!AddSegment(a,b,branches,ref curveCount,isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
                }
                continue;
            }
            // An entire boundary in the plane is one exact boundary curve.
            if(zero0||zero1)
            {
                var layer=zero0?left:right;
                status=CreateRational(layer,p,0,1,u0,u1,branches,ref curveCount);
                if(status!=AlgorithmStatus.Success)return status;
                var other=zero0?f1:f0;
                status=BoundedPolynomialRoots.Find(other[..(p+1)],0,1,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var i=0;i<n;i++)
                {
                    var a=EvaluateLayer(left,p,roots[i]);var b=EvaluateLayer(right,p,roots[i]);
                    if(!AddSegment(a,b,branches,ref curveCount,isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
                }
                continue;
            }
            // F(u,v)=g(u)*(1+(ratio-1)v): every zero of g is an
            // entire ruling and the second factor may be a transverse
            // isoparametric curve. This includes the two crossing branches
            // of a bilinear saddle, which a single continuation seed misses.
            var pivot=0;for(var k=1;k<=p;k++)if(Math.Abs(f0[k])>Math.Abs(f0[pivot]))pivot=k;
            var ratio=f1[pivot]/f0[pivot];var proportional=double.IsFinite(ratio);
            for(var k=0;k<=p;k++)if(f1[k]!=ratio*f0[k])proportional=false;
            if(proportional)
            {
                var v=1/(1-ratio);
                status=BoundedPolynomialRoots.Find(f0[..(p+1)],0,1,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                if(v>=0&&v<=1)
                {
                    rational.Clear();for(var k=0;k<4*(p+1);k++)rational[k]=(1-v)*left[k]+v*right[k];
                    var start=0.0;
                    for(var i=0;i<=n;i++)
                    {
                        var end=i==n?1:roots[i];
                        if(end>start+1e-13){status=CreateRational(rational,p,start,end,u0,u1,branches,ref curveCount);if(status!=AlgorithmStatus.Success)return status;}
                        start=end;
                    }
                }
                for(var i=0;i<n;i++)
                {
                    var a=EvaluateLayer(left,p,roots[i]);var b=EvaluateLayer(right,p,roots[i]);
                    if(v>0&&v<1)
                    {
                        var crossing=EvaluateLayer(rational,p,roots[i]);
                        if(!AddSegment(a,crossing,branches,ref curveCount,isolated,ref pointCount)
                            ||!AddSegment(b,crossing,branches,ref curveCount,isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
                    }
                    else if(!AddSegment(a,b,branches,ref curveCount,isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
                }
                continue;
            }
            var breakCount=2;breaks[0]=0;breaks[1]=1;
            for(var polynomial=0;polynomial<3;polynomial++)
            {
                var values=polynomial==0?f0:polynomial==1?f1:difference;
                status=BoundedPolynomialRoots.Find(values[..(p+1)],0,1,roots,out var n);if(status!=AlgorithmStatus.Success)return status;
                for(var i=0;i<n;i++)if(!AppendBreak(breaks,ref breakCount,roots[i]))return AlgorithmStatus.OutputTooSmall;
            }
            for(var i=1;i<breakCount;i++){var value=breaks[i];var j=i;while(j>0&&breaks[j-1]>value){breaks[j]=breaks[j-1];j--;}breaks[j]=value;}
            rational.Clear();
            // P=(A*F1 - B*F0)/(W_A*F1 - W_B*F0), where B is the opposite row.
            for(var i=0;i<=p;i++)for(var j=0;j<=p;j++)for(var coordinate=0;coordinate<4;coordinate++)
                rational[4*(i+j)+coordinate]+=left[4*i+coordinate]*f1[j]-right[4*i+coordinate]*f0[j];
            touches.Clear();
            for(var i=0;i<breakCount-1;i++)
            {
                var a=breaks[i];var b=breaks[i+1];if(!(b>a))continue;var mid=a/2+b/2;
                var denominator=Value(difference,p,mid);var v=-Value(f0,p,mid)/denominator;
                if(!double.IsFinite(v))return AlgorithmStatus.NumericalFailure;
                if(v<0||v>1)continue;
                // A simultaneous boundary zero needs common-factor removal;
                // reporting failure preserves completeness until that case is supported.
                if(Math.Abs(Value(f0,p,a))+Math.Abs(Value(f1,p,a))<=1e-14
                    ||Math.Abs(Value(f0,p,b))+Math.Abs(Value(f1,p,b))<=1e-14)return AlgorithmStatus.Unsupported;
                status=CreateRational(rational,2*p,a,b,u0,u1,branches,ref curveCount);if(status!=AlgorithmStatus.Success)return status;
                touches[i]=1;touches[i+1]=1;
            }
            for(var i=0;i<breakCount;i++)
            {
                if(touches[i]!=0)continue;var u=breaks[i];var a=Value(f0,p,u);var b=Value(f1,p,u);
                if(Math.Abs(a)<=1e-13&&Math.Abs(b)<=1e-13)return AlgorithmStatus.Unsupported;
                if(Math.Abs(a)<=1e-13&&!AddIsolated(EvaluateLayer(left,p,u),isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
                if(Math.Abs(b)<=1e-13&&!AddIsolated(EvaluateLayer(right,p,u),isolated,ref pointCount))return AlgorithmStatus.OutputTooSmall;
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus LayerPolynomial(in BSurfaceView surface,KnotIndex span,ControlPointIndex layer,Span<double> output)
    {
        var p=surface.UDegree;var stride=4*(p+1);Span<double> work=stackalloc double[9*9*4];work.Clear();output.Clear();
        var a=surface.UKnots[span];var b=surface.UKnots[span+1];
        for(var j=0;j<=p;j++)
        {
            var pole=((span-p+j)*2+layer)*surface.Dimension;
            for(var c=0;c<3;c++)work[j*stride+c]=surface.Vertices[pole+c];
            work[j*stride+3]=surface.Rational?surface.Vertices[pole+3]:1;
        }
        for(var r=1;r<=p;r++)for(var j=p;j>=r;j--)
        {
            var index=span-p+j;var denom=surface.UKnots[index+p-r+1]-surface.UKnots[index];if(!(denom>0))return AlgorithmStatus.NumericalFailure;
            var alpha=(a-surface.UKnots[index])/denom;var beta=(b-a)/denom;
            for(var k=r;k>=0;k--)for(var c=0;c<4;c++)
            {
                var target=j*stride+4*k+c;var left=target-stride;var value=work[left]+alpha*(work[target]-work[left]);
                if(k>0)value+=beta*(work[target-4]-work[left-4]);work[target]=value;
            }
        }
        work.Slice(p*stride,stride).CopyTo(output);return AlgorithmStatus.Success;
    }
    private static double PlaneNumerator(ReadOnlySpan<double> p,KnotIndex k,in AnalyticSurface plane)
        =>Dot(plane.Axis,Vector(p[4*k]-plane.Origin.X*p[4*k+3],p[4*k+1]-plane.Origin.Y*p[4*k+3],p[4*k+2]-plane.Origin.Z*p[4*k+3]));
    private static double Value(ReadOnlySpan<double> p,SplineDegree degree,double t)
    {var value=p[degree];for(var i=degree-1;i>=0;i--)value=Math.FusedMultiplyAdd(value,t,p[i]);return value;}
    private static KernelVector3 EvaluateLayer(ReadOnlySpan<double> p,SplineDegree degree,double t)
    {
        var x=p[4*degree];var y=p[4*degree+1];var z=p[4*degree+2];var w=p[4*degree+3];
        for(var i=degree-1;i>=0;i--){x=Math.FusedMultiplyAdd(x,t,p[4*i]);y=Math.FusedMultiplyAdd(y,t,p[4*i+1]);z=Math.FusedMultiplyAdd(z,t,p[4*i+2]);w=Math.FusedMultiplyAdd(w,t,p[4*i+3]);}
        return Vector(x/w,y/w,z/w);
    }
    private static bool AppendBreak(Span<double> values,ref BufferCount count,double value)
    {for(var i=0;i<count;i++)if(Math.Abs(values[i]-value)<=2e-13)return true;if(count==values.Length)return false;values[count++]=value;return true;}
    private static bool AddIsolated(KernelVector3 point,Span<KernelVector3> points,ref BufferCount count)
    {for(var i=0;i<count;i++)if(OperationIntersection.Norm(Sub(point,points[i]))<=OperationIntersection.DistanceTolerance)return true;if(count==points.Length)return false;points[count++]=point;return true;}
    private static bool AddSegment(KernelVector3 a,KernelVector3 b,Span<OperationIntersectionBranch> curves,ref BufferCount count,Span<KernelVector3> points,ref BufferCount pointCount)
    {
        var length=OperationIntersection.Norm(Sub(b,a));if(length<=OperationIntersection.DistanceTolerance)return AddIsolated(a,points,ref pointCount);
        if(count==curves.Length)return false;var line=OperationIntersection.Line(a,Scale(Sub(b,a),1/length));line.Start=0;line.End=length;curves[count++]=line;return true;
    }

    private static AlgorithmStatus CreateRational(ReadOnlySpan<double> polynomial,SplineDegree degree,double a,double b,
        double spanStart,double spanEnd,Span<OperationIntersectionBranch> curves,ref BufferCount count)
    {
        if(count==curves.Length)return AlgorithmStatus.OutputTooSmall;
        Span<double> local=stackalloc double[68];local.Clear();
        for(var c=0;c<4;c++)
        {
            local[c]=polynomial[4*degree+c];var n=0;
            for(var k=degree-1;k>=0;k--)
            {
                local[4*(n+1)+c]=local[4*n+c]*(b-a);
                for(var j=n;j>0;j--)local[4*j+c]=Math.FusedMultiplyAdd(local[4*j+c],a,local[4*(j-1)+c]*(b-a));
                local[c]=Math.FusedMultiplyAdd(local[c],a,polynomial[4*k+c]);n++;
            }
        }
        var branch=new OperationIntersectionBranch{Class=CurveClass.BCurve,Degree=degree,Start=Math.FusedMultiplyAdd(a,spanEnd-spanStart,spanStart),
            End=Math.FusedMultiplyAdd(b,spanEnd-spanStart,spanStart),Kind=IntersectionCurveKind.Simple};
        var sign=local[3]>=0?1:-1;
        for(var i=0;i<=degree;i++)for(var c=0;c<4;c++)
        {
            var value=0.0;var factor=1.0;
            for(var k=0;k<=i;k++){if(k>0)factor*=((double)i-k+1)/(degree-k+1);value+=factor*local[4*k+c];}
            value*=sign;if(!double.IsFinite(value))return AlgorithmStatus.NumericalFailure;branch.Poles[4*i+c]=value;
            if(c==3&&!(value>0))return AlgorithmStatus.Unsupported;
        }
        if(degree==1)
        {
            var first=Vector(branch.Poles[0]/branch.Poles[3],branch.Poles[1]/branch.Poles[3],branch.Poles[2]/branch.Poles[3]);
            var last=Vector(branch.Poles[4]/branch.Poles[7],branch.Poles[5]/branch.Poles[7],branch.Poles[6]/branch.Poles[7]);
            var length=OperationIntersection.Norm(Sub(last,first));if(!(length>OperationIntersection.DistanceTolerance))return AlgorithmStatus.Singular;
            branch=OperationIntersection.Line(first,Scale(Sub(last,first),1/length));branch.Start=0;branch.End=length;
        }
        curves[count++]=branch;return AlgorithmStatus.Success;
    }
}
