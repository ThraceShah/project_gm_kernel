using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Exact isoparametric loci on a positive-weight tensor Bezier patch. A rank-one
/// homogeneous plane numerator factors as f(u)g(v), so isolating all roots of
/// both factors discovers every component, including crossings and tangencies.
/// The rank test uses exact floating-point products, never a near-rank tolerance.
/// </summary>
internal static unsafe class IsoParametricBSurfacePlaneIntersection
{
    internal static AlgorithmStatus Find(in BSurfaceView surface,in AnalyticSurface plane,
        Span<OperationIntersectionBranch> branches,out BufferCount curveCount,
        Span<KernelVector3> isolated,out BufferCount pointCount)
    {
        curveCount=0;pointCount=0;
        if(surface.UDegree is <1 or >16||surface.VDegree is <1 or >16
            ||surface.UVertexCount!=surface.UDegree+1||surface.VVertexCount!=surface.VDegree+1
            ||surface.UPeriodic||surface.VPeriodic||surface.Dimension!=(surface.Rational?4:3)
            ||!Clamped(surface.UKnots,surface.UDegree)||!Clamped(surface.VKnots,surface.VDegree))return AlgorithmStatus.Unsupported;
        var nu=surface.UVertexCount;var nv=surface.VVertexCount;
        // A common homogeneous scale cannot change either the zero set or
        // whether an isocurve is a point. Normalize before interpolation and
        // products so tiny weights cannot turn all cross-products into zero.
        var maxWeight=1.0;
        if(surface.Rational)
        {
            maxWeight=0;
            for(var i=0;i<nu*nv;i++)
            {
                var weight=surface.Vertices[4*i+3];
                if(!(weight>0)||!double.IsFinite(weight))return AlgorithmStatus.Unsupported;
                maxWeight=Math.Max(maxWeight,weight);
            }
        }
        var exponent=-Math.ILogB(maxWeight);
        Span<double> normalizedPoles=stackalloc double[17*17*4];
        for(var i=0;i<nu*nv*surface.Dimension;i++)
        {
            var original=surface.Vertices[i];var value=Math.ScaleB(original,exponent);
            if(!double.IsFinite(value)||value==0&&original!=0)return AlgorithmStatus.NumericalFailure;
            normalizedPoles[i]=value;
        }
        var normalized=new BSurfaceView(surface.UDegree,surface.VDegree,nu,nv,surface.Dimension,surface.Rational,false,false,
            normalizedPoles[..(nu*nv*surface.Dimension)],surface.UKnots,surface.VKnots);
        Span<double> numerator=stackalloc double[17*17];var pivot=0;
        for(var i=0;i<nu*nv;i++)
        {
            var at=i*surface.Dimension;var w=surface.Rational?normalizedPoles[at+3]:1;
            var value=Dot(plane.Axis,Vector(normalizedPoles[at]-plane.Origin.X*w,
                normalizedPoles[at+1]-plane.Origin.Y*w,normalizedPoles[at+2]-plane.Origin.Z*w));
            if(!double.IsFinite(value))return AlgorithmStatus.NumericalFailure;
            numerator[i]=value;if(Math.Abs(value)>Math.Abs(numerator[pivot]))pivot=i;
        }
        if(numerator[pivot]==0)return AlgorithmStatus.Success; // coincident area
        Span<double> u=stackalloc double[17];Span<double> v=stackalloc double[17];
        var factored=false;
        for(var attempt=0;attempt<2&&!factored;attempt++)
        {
            var row=pivot/nv;var column=pivot%nv;var scale=numerator[pivot];
            for(var i=0;i<nu;i++)u[i]=attempt==0?numerator[i*nv+column]/scale:numerator[i*nv+column];
            for(var j=0;j<nv;j++)v[j]=attempt==0?numerator[row*nv+j]:numerator[row*nv+j]/scale;
            factored=true;
            for(var i=0;i<nu;i++)for(var j=0;j<nv;j++)
                if(!double.IsFinite(u[i])||!double.IsFinite(v[j])
                    ||Math.FusedMultiplyAdd(u[i],v[j],-numerator[i*nv+j])!=0)factored=false;
        }
        if(!factored)return AlgorithmStatus.Unsupported;
        Span<double> up=stackalloc double[17];Span<double> vp=stackalloc double[17];
        BernsteinToPower(u[..nu],up);BernsteinToPower(v[..nv],vp);
        Span<double> ur=stackalloc double[16];Span<double> vr=stackalloc double[16];
        var status=BoundedPolynomialRoots.Find(up[..nu],0,1,ur,out var un);if(status!=AlgorithmStatus.Success)return status;
        status=BoundedPolynomialRoots.Find(vp[..nv],0,1,vr,out var vn);if(status!=AlgorithmStatus.Success)return status;
        // Each crossing divides both curves. Isolated roots on the boundary
        // still produce the entire boundary isocurve, not an isolated point.
        for(var direction=0;direction<2;direction++)
        {
            var roots=direction==0?ur[..un]:vr[..vn];var splits=direction==0?vr[..vn]:ur[..un];
            var polynomial=direction==0?up[..nu]:vp[..nv];
            for(var i=0;i<roots.Length;i++)
            {
                status=RootKind(polynomial,roots[i],out var kind);if(status!=AlgorithmStatus.Success)return status;
                status=AppendIsocurve(normalized,plane,direction==0,roots[i],splits,kind,branches,ref curveCount,isolated,ref pointCount);
                if(status!=AlgorithmStatus.Success)return status;
            }
        }
        // A collapsed parameter line can meet a noncollapsed branch at a
        // split endpoint. It is already part of that curve, not isolated.
        for(var i=pointCount-1;i>=0;i--)
        {
            var onCurve=false;
            for(var j=0;j<curveCount&&!onCurve;j++)for(var end=0;end<2;end++)
            {
                ref readonly var branch=ref branches[j];KernelVector3 endpoint;
                if(branch.Class==CurveClass.Line)endpoint=Add(branch.Origin,Scale(branch.Axis,end==0?branch.Start:branch.End));
                else{var at=end==0?0:4*branch.Degree;var w=branch.Poles[at+3];endpoint=Vector(branch.Poles[at]/w,branch.Poles[at+1]/w,branch.Poles[at+2]/w);}
                if(OperationIntersection.Norm(Sub(endpoint,isolated[i]))<=OperationIntersection.DistanceTolerance)onCurve=true;
            }
            if(onCurve){isolated[(i+1)..pointCount].CopyTo(isolated[i..]);pointCount--;}
        }
        return AlgorithmStatus.Success;
    }

    private static bool Clamped(ReadOnlySpan<double> knots,SplineDegree degree)
    {
        if(knots.Length!=2*(degree+1)||!(knots[degree+1]>knots[degree]))return false;
        for(var i=0;i<=degree;i++)if(knots[i]!=knots[degree]||knots[degree+1+i]!=knots[degree+1])return false;
        return true;
    }

    private static void BernsteinToPower(ReadOnlySpan<double> poles,Span<double> power)
    {
        Span<double> differences=stackalloc double[17];poles.CopyTo(differences);
        var n=poles.Length-1;var binomial=1.0;
        for(var k=0;k<=n;k++)
        {
            power[k]=binomial*differences[0];
            for(var i=0;i<n-k;i++)differences[i]=differences[i+1]-differences[i];
            if(k<n)binomial*=((double)n-k)/(k+1);
        }
    }

    private static AlgorithmStatus RootKind(ReadOnlySpan<double> p,double root,out IntersectionCurveKind kind)
    {
        var derivative=0.0;var bound=0.0;
        for(var i=p.Length-1;i>0;i--)
        {derivative=Math.FusedMultiplyAdd(derivative,root,i*p[i]);bound=bound*Math.Abs(root)+Math.Abs(i*p[i]);}
        kind=derivative==0?IntersectionCurveKind.Tangent:IntersectionCurveKind.Simple;
        return derivative!=0&&Math.Abs(derivative)<=64*2.2204460492503131e-16*bound
            ?AlgorithmStatus.NumericalFailure:AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AppendIsocurve(in BSurfaceView surface,in AnalyticSurface plane,bool fixedU,double parameter,
        ReadOnlySpan<double> splitParameters,IntersectionCurveKind kind,Span<OperationIntersectionBranch> branches,ref BufferCount count,
        Span<KernelVector3> isolated,ref BufferCount pointCount)
    {
        var degree=fixedU?surface.VDegree:surface.UDegree;var fixedDegree=fixedU?surface.UDegree:surface.VDegree;
        Span<double> poles=stackalloc double[68];Span<double> work=stackalloc double[68];
        for(var j=0;j<=degree;j++)
        {
            for(var i=0;i<=fixedDegree;i++)
            {
                var at=(fixedU?i*surface.VVertexCount+j:j*surface.VVertexCount+i)*surface.Dimension;
                for(var c=0;c<3;c++)work[4*i+c]=surface.Vertices[at+c];
                work[4*i+3]=surface.Rational?surface.Vertices[at+3]:1;
            }
            for(var n=fixedDegree;n>0;n--)for(var i=0;i<n;i++)for(var c=0;c<4;c++)
                work[4*i+c]=(1-parameter)*work[4*i+c]+parameter*work[4*(i+1)+c];
            for(var c=0;c<4;c++)poles[4*j+c]=work[c];
            var weight=work[3];if(!(weight>0)||!double.IsFinite(weight))return AlgorithmStatus.NumericalFailure;
            var residual=Dot(plane.Axis,Sub(Vector(work[0]/weight,work[1]/weight,work[2]/weight),plane.Origin));
            // The convex hull of these positive-weight poles bounds the
            // distance of the entire curve from the plane, not just samples.
            if(!double.IsFinite(residual)||Math.Abs(residual)>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NumericalFailure;
        }
        var constant=true;
        for(var i=1;i<=degree&&constant;i++)for(var c=0;c<3;c++)
        {
            var a=poles[4*i+c]*poles[3];var b=poles[c]*poles[4*i+3];
            if(!double.IsFinite(a)||!double.IsFinite(b))return AlgorithmStatus.NumericalFailure;
            // FMA error terms are exact only without product underflow. Very
            // wide weight ratios remain an explicit numerical limitation.
            if(double.IsSubnormal(a)||double.IsSubnormal(b)||a==0&&poles[4*i+c]!=0||b==0&&poles[c]!=0)
                return AlgorithmStatus.NumericalFailure;
            if(a!=b||Math.FusedMultiplyAdd(poles[4*i+c],poles[3],-a)!=Math.FusedMultiplyAdd(poles[c],poles[4*i+3],-b))constant=false;
        }
        if(constant)
        {
            var point=Vector(poles[0]/poles[3],poles[1]/poles[3],poles[2]/poles[3]);
            for(var i=0;i<pointCount;i++)if(OperationIntersection.Norm(Sub(point,isolated[i]))<=OperationIntersection.DistanceTolerance)return AlgorithmStatus.Success;
            if(pointCount==isolated.Length)return AlgorithmStatus.OutputTooSmall;isolated[pointCount++]=point;return AlgorithmStatus.Success;
        }
        Span<double> polynomial=stackalloc double[68];Span<double> component=stackalloc double[17];Span<double> power=stackalloc double[17];
        for(var c=0;c<4;c++)
        {
            for(var i=0;i<=degree;i++)component[i]=poles[4*i+c];
            BernsteinToPower(component[..(degree+1)],power);
            for(var i=0;i<=degree;i++)polynomial[4*i+c]=power[i];
        }
        var domainStart=fixedU?surface.VStart:surface.UStart;var domainEnd=fixedU?surface.VEnd:surface.UEnd;
        var start=0.0;
        for(var i=0;i<=splitParameters.Length;i++)
        {
            var end=i==splitParameters.Length?1:splitParameters[i];
            if(end>start)
            {
                var status=RuledBSurfacePlaneIntersection.CreateRational(polynomial,degree,start,end,domainStart,domainEnd,branches,ref count);
                if(status!=AlgorithmStatus.Success)return status;
                branches[count-1].Kind=kind;
            }
            start=end;
        }
        return AlgorithmStatus.Success;
    }
}
