using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Analytic implicit differentiation through a compiled constant-radius
/// Blend graph. Taylor coefficients solve the same spatial constraints as D0;
/// no nested evaluator is finite-differenced.</summary>
internal static class NestedBlendSeriesEvaluation
{
    private const double ResidualTolerance=2e-13;
    private const BufferCount MaxDepth=16;

    internal static AlgorithmStatus Evaluate(in NestedBlendProgram program,BlendSurfaceIndex surface,
        double u,double v,in SurfaceDerivativeLayout layout,Span<KernelVector3> output)
    {
        if(layout.UOrder>2||layout.VOrder>2||output.Length<layout.Count)return AlgorithmStatus.Unsupported;
        if(!double.IsFinite(u)||!double.IsFinite(v))return AlgorithmStatus.InvalidInput;
        var node=program.Surfaces[surface];if(!node.IsBlend)return AlgorithmStatus.Unsupported;
        var order=layout.UOrder;
        var status=Spine(program,node.Spine,u,order,0,out var center);if(status!=AlgorithmStatus.Success)return status;
        status=Distance(program,node.First,center,0,out _,out var n0);if(status!=AlgorithmStatus.Success)return status;
        status=Distance(program,node.Second,center,0,out _,out var n1);if(status!=AlgorithmStatus.Success)return status;
        return FromFrame(node,center,n0,n1,v,layout,output);
    }

    internal static AlgorithmStatus FromFrame(in BlendSurfaceInstruction node,in TaylorVector center,
        in TaylorVector n0,in TaylorVector n1,double v,in SurfaceDerivativeLayout layout,Span<KernelVector3> output)
    {
        var radius=Math.Abs(node.Range0);var x=n1*(-node.Range1/radius);var end=n0*(-node.Range0/radius);
        if(node.ReverseSection)(x,end)=(end,x);
        var cosine=TaylorVector.Dot(x,end);var transverse=end-x*cosine;var sine=transverse.Norm();
        if(!(sine[0]>1e-7))return AlgorithmStatus.Singular;
        var y=transverse/sine;var arc=TaylorJet.Atan2(sine,cosine);var theta=arc*v;
        TaylorJet.SinCos(theta,out var sin,out var cos);
        var radial=x*cos+y*sin;var tangent=y*cos-x*sin;
        var point=center+radial*radius;var dv=tangent*(arc*radius);var dvv=radial*(arc*arc*(-radius));
        double factorial=1;
        for(var i=0;i<=layout.UOrder;i++)
        {
            if(i>0)factorial*=i;
            for(var j=0;j<=layout.VOrder;j++)
            {
                var series=j==0?point:j==1?dv:dvv;
                var value=new KernelVector3{X=series.X[i]*factorial,Y=series.Y[i]*factorial,Z=series.Z[i]*factorial};
                if(!IsFinite(value))return AlgorithmStatus.NumericalFailure;output[layout.GetIndex(i,j)]=value;
            }
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Spine(in NestedBlendProgram program,BlendSpineIndex index,double parameter,
        DerivativeOrder order,BufferCount depth,out TaylorVector result)
    {
        result=default;
        if(depth>MaxDepth||order>TaylorJet.MaxOrder||order<0)return AlgorithmStatus.Unsupported;
        var node=program.Spines[index];var charts=program.Charts.Slice(node.ChartOffset,node.ChartCount);
        if(parameter<charts[0].Parameter||parameter>charts[^1].Parameter)return AlgorithmStatus.InvalidInput;
        var segment=0;while(segment<charts.Length-2&&charts[segment+1].Parameter<=parameter)segment++;
        var chart=charts[segment];var e=chart.Chord;
        var fraction=(parameter-chart.Parameter)/(charts[segment+1].Parameter-chart.Parameter);
        var point=Add(chart.Position,Scale(Sub(charts[segment+1].Position,chart.Position),fraction));
        KernelVector3 n0=default,n1=default;
        var converged=false;
        for(var iteration=0;iteration<16;iteration++)
        {
            var path=TaylorVector.Constant(point,0);
            var status=Distance(program,node.First,path,depth+1,out var d0,out var normal0);if(status!=AlgorithmStatus.Success)return status;
            status=Distance(program,node.Second,path,depth+1,out var d1,out var normal1);if(status!=AlgorithmStatus.Success)return status;
            n0=normal0.Value;n1=normal1.Value;
            var plane=Dot(e,Sub(point,chart.Position))-(parameter-chart.Parameter)/chart.Scale;
            if(Math.Max(Math.Max(Math.Abs(d0[0]),Math.Abs(d1[0])),Math.Abs(plane))<=ResidualTolerance){converged=true;break;}
            if(!Solve(n0,n1,e,-d0[0],-d1[0],-plane,out var step))return AlgorithmStatus.Singular;
            var length=Math.Sqrt(Dot(step,step));var limit=2*Math.Sqrt(Dot(Sub(charts[segment+1].Position,chart.Position),Sub(charts[segment+1].Position,chart.Position)));
            if(length>limit)step=Scale(step,limit/length);point=Add(point,step);
            if(!IsFinite(point))return AlgorithmStatus.NumericalFailure;
        }
        if(!converged)return AlgorithmStatus.NotConverged;
        result=TaylorVector.Constant(point,order);
        for(var k=1;k<=order;k++)
        {
            var path=result.Truncate(k);
            var status=Distance(program,node.First,path,depth+1,out var d0,out _);if(status!=AlgorithmStatus.Success)return status;
            status=Distance(program,node.Second,path,depth+1,out var d1,out _);if(status!=AlgorithmStatus.Success)return status;
            if(!Solve(n0,n1,e,-d0[k],-d1[k],k==1?1/chart.Scale:0,out var coefficient))return AlgorithmStatus.Singular;
            result.X[k]=coefficient.X;result.Y[k]=coefficient.Y;result.Z[k]=coefficient.Z;
        }
        return result.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus Distance(in NestedBlendProgram program,BlendSurfaceIndex index,in TaylorVector point,
        BufferCount depth,out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;if(depth>MaxDepth)return AlgorithmStatus.Unsupported;
        var node=program.Surfaces[index];
        if(node.IsBSpline)return NestedSplineEvaluation.Distance(program,node,point,out distance,out normal);
        if(!node.IsBlend)
        {
            var status=AnalyticDistanceSeries.Evaluate(node.Analytic,point,out distance,out normal);
            distance*=node.NormalSense;normal=normal*node.NormalSense;return status;
        }
        return JointNestedBlendEvaluation.Distance(program,index,point,out distance,out normal);
    }

    internal static AlgorithmStatus RecursiveDistance(in NestedBlendProgram program,BlendSurfaceIndex index,in TaylorVector point,
        BufferCount depth,out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;var node=program.Surfaces[index];
        var order=point.X.Order;if(order+1>TaylorJet.MaxOrder)return AlgorithmStatus.Unsupported;
        var spineNode=program.Spines[node.Spine];var charts=program.Charts.Slice(spineNode.ChartOffset,spineNode.ChartCount);
        var seed=0;var best=double.PositiveInfinity;
        for(var i=0;i<charts.Length;i++){var delta=Sub(point.Value,charts[i].Position);var square=Dot(delta,delta);if(square<best){best=square;seed=i;}}
        var parameter=charts[seed].Parameter;TaylorVector spine=default;double denominator=0;var converged=false;
        for(var iteration=0;iteration<16;iteration++)
        {
            var status=Spine(program,node.Spine,parameter,Math.Max(order+1,2),depth+1,out spine);if(status!=AlgorithmStatus.Success)return status;
            var q=Sub(point.Value,spine.Value);var tangent=spine.Differentiate().Value;
            var second=spine.Differentiate().Differentiate().Value;denominator=Dot(tangent,tangent)-Dot(q,second);
            if(!(denominator>1e-10*Dot(tangent,tangent)))return AlgorithmStatus.Singular;
            var residual=Dot(q,tangent);
            if(Math.Abs(residual)<=ResidualTolerance*Math.Max(1,Math.Sqrt(Dot(tangent,tangent)))){converged=true;break;}
            var next=parameter+residual/denominator;
            if(spineNode.StartType==LimitType.Help&&spineNode.EndType==LimitType.Help
                &&charts[0].Position.X==charts[^1].Position.X&&charts[0].Position.Y==charts[^1].Position.Y&&charts[0].Position.Z==charts[^1].Position.Z)
            {
                var first=charts[0].Parameter;var last=charts[^1].Parameter;
                if(next<first||next>last){var period=last-first;next=first+((next-first)%period+period)%period;}
            }
            else next=Math.Clamp(next,charts[0].Parameter,charts[^1].Parameter);
            if(next==parameter)return AlgorithmStatus.NotConverged;parameter=next;
        }
        if(!converged)return AlgorithmStatus.NotConverged;
        var deltaParameter=new TaylorJet(0,order);
        for(var k=1;k<=order;k++)
        {
            var delta=deltaParameter.Truncate(k);var center=spine.Compose(delta);
            var tangent=spine.Differentiate().Compose(delta);
            var equation=TaylorVector.Dot(point.Truncate(k)-center,tangent);
            deltaParameter[k]=equation[k]/denominator;
        }
        var offset=point-spine.Compose(deltaParameter);var length=offset.Norm();if(!(length[0]>0))return AlgorithmStatus.Singular;
        distance=(length-Math.Abs(node.Range0))*node.NormalSense;normal=(offset/length)*node.NormalSense;
        return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    private static bool Solve(in KernelVector3 a,in KernelVector3 b,in KernelVector3 c,
        double x,double y,double z,out KernelVector3 solution)
    {
        var bc=Cross(b,c);var determinant=Dot(a,bc);solution=default;
        var scale=Math.Sqrt(Dot(a,a)*Dot(b,b)*Dot(c,c));if(!(Math.Abs(determinant)>1e-12*scale))return false;
        solution=Scale(Add(Add(Scale(bc,x),Scale(Cross(c,a),y)),Scale(Cross(a,b),z)),1/determinant);return IsFinite(solution);
    }
}
