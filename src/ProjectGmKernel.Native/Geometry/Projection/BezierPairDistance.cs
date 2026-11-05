using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Projection;

internal struct GeometryDistancePair
{
    internal SurfaceIntersectionNode Parameters;
    internal KernelVector3 First, Second;
    internal double Upper;
}

/// <summary>Global minimum between bounded positive rational patches, curves
/// or points. Local optimization only improves the upper bound. Every discarded
/// cell has an outward-rounded box or separating support-plane lower bound.</summary>
internal static class BezierPairDistance
{
    internal static AlgorithmStatus Minimum(in BezierSurfacePatch first,in BezierSurfacePatch second,
        double tolerance,ref GeometryDistancePair best)
        =>MinimumProcedural(new SurfaceIntersectionPatch(first),new SurfaceIntersectionPatch(second),tolerance,ref best);

    internal static AlgorithmStatus MinimumProcedural(scoped in SurfaceIntersectionPatch first,scoped in SurfaceIntersectionPatch second,double tolerance,ref GeometryDistancePair best)
    {
        if(!(tolerance>0)||!double.IsFinite(tolerance))return AlgorithmStatus.InvalidInput;
        var domain=new ParameterCell{Lower=new(){U=first.Domain.U0,V=first.Domain.V0,S=second.Domain.U0,T=second.Domain.V0},
            Upper=new(){U=first.Domain.U1,V=first.Domain.V1,S=second.Domain.U1,T=second.Domain.V1}};
        Span<ParameterCell> pending=stackalloc ParameterCell[256];pending[0]=domain;var count=1;var visited=0;
        Span<IntervalNumber> a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];
        while(count>0)
        {
            if(++visited>200000)return AlgorithmStatus.NotConverged;
            var cell=pending[--count];
            var status=first.Bound(cell.First,a);var force=status==AlgorithmStatus.NotConverged?0:-1;if(force==0)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
            status=second.Bound(cell.Second,b);if(status==AlgorithmStatus.NotConverged){force=2;goto Subdivide;}if(status!=AlgorithmStatus.Success)return status;
            var lower=BoxLower(a,b);if(lower>=best.Upper-tolerance)continue;
            if(!first.IsProcedural&&!second.IsProcedural||cell.Depth%8==0)Improve(first,second,cell,ref best);
            if(best.Upper<=tolerance)continue;
            var direction=Sub(best.First,best.Second);var scale=Math.Max(Math.Abs(direction.X),Math.Max(Math.Abs(direction.Y),Math.Abs(direction.Z)));
            if(scale>0&&double.IsFinite(scale))
            {
                direction=Scale(direction,1/scale);
                var length=Math.BitIncrement(Math.Sqrt((IntervalNumber.Square(new(direction.X))+IntervalNumber.Square(new(direction.Y))+IntervalNumber.Square(new(direction.Z))).Upper));
                direction=Scale(direction,Math.BitDecrement(1/length));
                status=first.SupportRange(cell.First,direction,a,out var da);if(status!=AlgorithmStatus.Success)return status;
                status=second.SupportRange(cell.Second,direction,b,out var db);if(status!=AlgorithmStatus.Success)return status;
                lower=Math.Max(lower,(new IntervalNumber(da.Lower)-db.Upper).Lower);
            }
            if(lower>=best.Upper-tolerance)continue;
            Subdivide:
            var split=-1;var width=0.0;
            for(var d=0;d<4;d++)
            {
                var degree=d==0?first.UDegree:d==1?first.VDegree:d==2?second.UDegree:second.VDegree;
                if(force>=0&&d/2!=force/2)continue;
                if(degree==0)continue;
                var relative=cell.Width(d)/domain.Width(d);
                if(relative>width){width=relative;split=d;}
            }
            if(force==0&&first.IsProcedural)split=0;else if(force==2&&second.IsProcedural)split=2;
            if(split<0||width<1e-13||count+2>pending.Length)return AlgorithmStatus.NotConverged;
            var middle=cell.Lower.Parameter(split)+cell.Width(split)*.5;
            if(!(middle>cell.Lower.Parameter(split)&&middle<cell.Upper.Parameter(split)))return AlgorithmStatus.NumericalFailure;
            var right=cell;cell.Upper.SetParameter(split,middle);right.Lower.SetParameter(split,middle);cell.Depth++;right.Depth=cell.Depth;
            pending[count++]=right;pending[count++]=cell;
        }
        return AlgorithmStatus.Success;
    }

    private static void Improve(scoped in SurfaceIntersectionPatch first,scoped in SurfaceIntersectionPatch second,in ParameterCell cell,ref GeometryDistancePair best)
    {
        Span<IntervalNumber> a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];
        Span<double> matrix=stackalloc double[16],rhs=stackalloc double[4];Span<int> pivots=stackalloc int[4];
        Span<KernelVector3> columns=stackalloc KernelVector3[4];
        for(var seed=0;seed<3;seed++)
        {
            var point=seed==0?cell.Center:seed==1?cell.Lower:cell.Upper;
            for(var iteration=0;iteration<24;iteration++)
            {
                if(!Evaluate(first,second,point,a,b,out var upper,false))break;
                var pa=Vector(a[0].Midpoint,a[1].Midpoint,a[2].Midpoint);var pb=Vector(b[0].Midpoint,b[1].Midpoint,b[2].Midpoint);
                if(upper<best.Upper&&Evaluate(first,second,point,a,b,out var certified)&&certified<best.Upper)best=new(){Parameters=point,First=pa,Second=pb,Upper=certified};
                if(upper==0)break;
                columns[0]=Vector(a[3].Midpoint,a[4].Midpoint,a[5].Midpoint);columns[1]=Vector(a[6].Midpoint,a[7].Midpoint,a[8].Midpoint);
                columns[2]=Vector(-b[3].Midpoint,-b[4].Midpoint,-b[5].Midpoint);columns[3]=Vector(-b[6].Midpoint,-b[7].Midpoint,-b[8].Midpoint);
                var residual=Sub(pa,pb);var max=0.0;
                for(var d=0;d<4;d++){rhs[d]=-Dot(columns[d],residual);max=Math.Max(max,Dot(columns[d],columns[d]));}
                if(!(max>0)||!double.IsFinite(max))break;
                for(var i=0;i<4;i++)for(var j=0;j<4;j++)matrix[4*i+j]=Dot(columns[i],columns[j])+(i==j?max*1e-8:0);
                if(SmallLinearSolve.LuFactorize(matrix,4,pivots)!=AlgorithmStatus.Success||SmallLinearSolve.LuSolveInPlace(matrix,4,pivots,rhs)!=AlgorithmStatus.Success)break;
                var accepted=false;
                for(var line=0;line<14;line++)
                {
                    var trial=point;
                    for(var d=0;d<4;d++)trial.SetParameter(d,Math.Clamp(point.Parameter(d)+rhs[d],cell.Lower.Parameter(d),cell.Upper.Parameter(d)));
                    if(Evaluate(first,second,trial,a,b,out var trialUpper,false)&&trialUpper<upper){point=trial;accepted=true;break;}
                    for(var d=0;d<4;d++)rhs[d]*=.5;
                }
                if(!accepted)break;
            }
        }
    }

    private static bool Evaluate(scoped in SurfaceIntersectionPatch first,scoped in SurfaceIntersectionPatch second,in SurfaceIntersectionNode point,
        Span<IntervalNumber> a,Span<IntervalNumber> b,out double upper,bool certify=true)
    {
        upper=double.PositiveInfinity;
        var firstStatus=certify?first.Bound(new(point.U,point.U,point.V,point.V),a):first.Estimate(point.U,point.V,a);
        var secondStatus=certify?second.Bound(new(point.S,point.S,point.T,point.T),b):second.Estimate(point.S,point.T,b);
        if(firstStatus!=AlgorithmStatus.Success||secondStatus!=AlgorithmStatus.Success)return false;
        var square=IntervalNumber.Square(a[0]-b[0])+IntervalNumber.Square(a[1]-b[1])+IntervalNumber.Square(a[2]-b[2]);
        upper=Math.BitIncrement(Math.Sqrt(Math.Max(0,square.Upper)));return double.IsFinite(upper);
    }

    private static double BoxLower(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b)
    {
        IntervalNumber square=0;
        for(var d=0;d<3;d++)
        {
            var difference=a[d]-b[d];
            if(!difference.ContainsZero){var value=Math.Min(Math.Abs(difference.Lower),Math.Abs(difference.Upper));square+=IntervalNumber.Square(new(value));}
        }
        return Math.Max(0,Math.BitDecrement(Math.Sqrt(Math.Max(0,square.Lower))));
    }

}
