using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Unit-radius great-circle interpolation with a removable zero-angle
/// limit. A coincident pair of contacts is a collapsed section, not an
/// undefined normalisation of their transverse vector.</summary>
internal static class RollingSectionEvaluation
{
    internal static AlgorithmStatus Series(in TaylorVector first,in TaylorVector last,double v,
        out TaylorVector point,out TaylorVector dv,out TaylorVector dvv)
    {
        point=dv=dvv=default;var cosine=TaylorVector.Dot(first,last);var q=1-cosine;
        if(q[0]<-64*SmallLinearSolve.MachineEpsilon)return AlgorithmStatus.NumericalFailure;
        if(q[0]<=.125)
        {
            if(q[0]<0)q[0]=0;
            Weight(q,1-v,out var a,out var av,out var avv);Weight(q,v,out var b,out var bv,out var bvv);
            point=first*a+last*b;dv=last*bv-first*av;dvv=first*avv+last*bvv;
        }
        else
        {
            var transverse=last-first*cosine;var sine=transverse.Norm();if(!(sine[0]>1e-7))return AlgorithmStatus.Singular;
            var y=transverse/sine;var arc=TaylorJet.Atan2(sine,cosine);TaylorJet.SinCos(arc*v,out var sin,out var cos);
            point=first*cos+y*sin;dv=(y*cos-first*sin)*arc;dvv=point*(arc*arc*(-1));
        }
        return point.Finite&&dv.Finite&&dvv.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    // sin(v*acos(1-q))/sin(acos(1-q)) = sum a_k(v)*q^k,
    // a_0=v, a_k=a_(k-1)*(k^2-v^2)/(k*(2k+1)). This is analytic at q=0.
    private static void Weight(TaylorJet q,double v,out TaylorJet value,out TaylorJet first,out TaylorJet second)
    {
        var term=new TaylorJet(v,q.Order);var d=new TaylorJet(1,q.Order);var dd=new TaylorJet(0,q.Order);
        value=term;first=d;second=dd;
        for(var k=1;k<=24;k++)
        {
            var denominator=(double)k*(2*k+1);var f=(k*(double)k-v*v)/denominator;var df=-2*v/denominator;var ddf=-2/denominator;
            var next=(dd*f+d*(2*df)+term*ddf)*q;d=(d*f+term*df)*q;term=term*(q*f);dd=next;
            value+=term;first+=d;second+=dd;
        }
    }

    internal static AlgorithmStatus Bound(in IntervalVector first,in IntervalVector last,IntervalDual v,out IntervalVector point)
    {
        point=default;var cosine=IntervalVector.Dot(first,last);var q=1-cosine;
        if(q.Value.Lower<=.125)
        {
            // The support normals are unit vectors, hence 0<=1-cos(theta)<=2.
            var lower=Math.Max(0,q.Value.Lower);var upper=Math.Min(2,q.Value.Upper);if(upper<lower)return AlgorithmStatus.NotConverged;
            q=new(new(lower,upper),q.Derivative);
            var status=Weight(q,1-v,out var a);if(status!=AlgorithmStatus.Success)return status;
            status=Weight(q,v,out var b);if(status!=AlgorithmStatus.Success)return status;
            point=first*a+last*b;
        }
        else
        {
            var transverse=last-first*cosine;var sine=transverse.Norm();var y=transverse/sine;var arc=IntervalDual.Atan2(sine,cosine);
            IntervalDual.SinCos(arc*v,out var sin,out var cos);point=first*cos+y*sin;
        }
        return point.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus Weight(IntervalDual q,IntervalDual v,out IntervalDual value)
    {
        using var cost=new ModelingPerformance.EvaluationScope(EvaluationCost.IntervalWeight);
        value=default;if(q.Value.Lower<0||q.Value.Upper>=2)return AlgorithmStatus.NotConverged;
        var terms=q.Value.Upper<=1?64:128;if(v.Value.Magnitude>terms)return AlgorithmStatus.NotConverged;
        var term=v;value=term;
        for(var k=1;k<=terms;k++)
        {
            var denominator=(double)k*(2*k+1);term=term*((k*(double)k-IntervalDual.Square(v))/denominator)*q;value+=term;
            if(k<4||(k&(k-1))!=0||v.Value.Magnitude>k)continue;
            // For every remaining index, |v|<=k bounds the recurrence by
            // r=q_max/2<1. Keep the complete value and differentiated tails.
            IntervalNumber ratio=new IntervalNumber(q.Value.Upper)*.5;var denominatorTail=1-ratio;
            var factorDerivative=new IntervalNumber(q.Derivative.Magnitude)*.5
                +new IntervalNumber(q.Value.Upper)*(2*new IntervalNumber(v.Value.Magnitude)/((k+1.0)*(2*k+3)))*v.Derivative.Magnitude;
            var tail=new IntervalNumber(term.Value.Magnitude)*ratio/denominatorTail;
            var derivative=new IntervalNumber(term.Derivative.Magnitude)*ratio/denominatorTail
                +new IntervalNumber(term.Value.Magnitude)*factorDerivative/IntervalNumber.Square(denominatorTail);
            if(!tail.Finite||!derivative.Finite){if(k==terms)return AlgorithmStatus.NotConverged;continue;}
            if(k!=terms&&(!BelowFloatingStep(tail.Upper,value.Value)||!BelowFloatingStep(derivative.Upper,value.Derivative)))continue;
            value=new(value.Value+new IntervalNumber(-tail.Upper,tail.Upper),value.Derivative+new IntervalNumber(-derivative.Upper,derivative.Upper));
            return value.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
        }
        return AlgorithmStatus.NotConverged;
    }

    private static bool BelowFloatingStep(double error,IntervalNumber value)
        =>value.Finite&&error<=Math.Min(value.Lower-Math.BitDecrement(value.Lower),Math.BitIncrement(value.Upper)-value.Upper);
}
