using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    private readonly ref struct JacobianExpressions(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,Span<BufferOffset> outputs)
    {
        internal readonly Span<IntervalExpressionNode> Nodes=nodes;
        internal readonly Span<BufferCount> Count=count;
        internal readonly Span<BufferOffset> Outputs=outputs;
        internal IntervalExpression Variable(BufferOffset column)=>IntervalExpression.FromNode(Nodes,Count,column);
        internal IntervalExpressionVector Point(BufferOffset offset)=>new(Variable(offset),Variable(offset+1),Variable(offset+2));
        internal void Store(BufferOffset row,IntervalExpression value)
        {
            if(Count[0]>Nodes.Length)return;
            var attached=value.Attach(Nodes,Count);
            if(Count[0]<=Nodes.Length)Outputs[row]=attached.OutputIndex();
        }
    }

    private ref struct IntervalJacobianRecord(Span<IntervalExpressionNode> nodes,Span<IntervalNumber> derivatives,Span<BufferOffset> outputs)
    {
        internal Span<IntervalExpressionNode> Nodes=nodes;
        internal Span<IntervalNumber> Derivatives=derivatives;
        internal Span<BufferOffset> Outputs=outputs;
        internal BufferCount Length;
        internal bool Ready;
    }

    // Occurrences and chart choices are fixed by this BoundSegment's snapshot.
    // Spline hulls and rational explicit spines keep their dedicated evaluator.
    private static bool CanRecordJacobian(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,BufferCount variables)
    {
        if(variables<7||variables>15||HasAnyUv(nodes))return false;
        foreach(var node in nodes)if(program.Spines[node.Spine].Curve.Kind==CurveClass.BCurve)return false;
        return true;
    }

    private static AlgorithmStatus IntervalJacobianWithRecord(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,ReadOnlySpan<IntervalNumber> boxes,
        IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> fixedCharts,
        Span<IntervalSplineContact> cache,ref IntervalJacobianRecord record)
        =>record.Nodes.IsEmpty?IntervalJacobian(program,nodes,boxes,parameter,matrix,variables,equations,fixedCharts,cache)
            :RecordedIntervalJacobian(program,nodes,boxes,parameter,matrix,fixedCharts,cache,ref record);

    // Every use belongs to one immutable occurrence/chart structure. A new
    // tube gets a new record; every box rebinds every variable and rechecks
    // the original finite/positive guards before a derivative is consumed.
    private static AlgorithmStatus RecordedIntervalJacobian(in NestedBlendProgram program,ReadOnlySpan<Occurrence> occurrences,
        ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,ReadOnlySpan<BufferOffset> fixedCharts,
        ReadOnlySpan<IntervalSplineContact> cache,ref IntervalJacobianRecord record)
    {
        AlgorithmStatus status;ModelingPerformance.Jacobian(boxes.Length);
        if(record.Ready)
        {
            ModelingPerformance.JacobianRecord(record.Length,true);
            status=IntervalExpression.Rebind(record.Nodes[..record.Length],boxes);
            if(status!=AlgorithmStatus.Success)return status;
        }
        else
        {
            Span<BufferCount> count=stackalloc BufferCount[1];count[0]=0;
            var expressions=new JacobianExpressions(record.Nodes,count,record.Outputs);
            for(var i=0;i<boxes.Length;i++)IntervalExpression.Variable(record.Nodes,count,boxes[i],i);
            status=RecordIntervalResidual(program,occurrences,parameter,fixedCharts,cache,expressions);
            ModelingPerformance.JacobianRecord(Math.Min(count[0],record.Nodes.Length),false);
            if(count[0]>record.Nodes.Length)return AlgorithmStatus.WorkspaceTooSmall;
            if(status!=AlgorithmStatus.Success)return status;
            record.Length=count[0];
            if(IntervalExpression.PrepareDerivatives(record.Nodes[..record.Length])>record.Derivatives.Length)return AlgorithmStatus.WorkspaceTooSmall;
            record.Ready=true;
        }
        return IntervalExpression.Differentiate(record.Nodes[..record.Length],record.Outputs,boxes.Length,record.Derivatives,matrix);
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestIntervalJacobianRecord(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,double width,
        Span<IntervalNumber> reference,Span<IntervalNumber> actual,out BufferCount variables,out AlgorithmStatus referenceStatus,out AlgorithmStatus actualStatus)
    {
        variables=0;referenceStatus=actualStatus=AlgorithmStatus.NotRun;
        Span<TaylorJet> snapshot=stackalloc TaylorJet[MaxVariables];Span<Occurrence> nodes=stackalloc Occurrence[MaxOccurrences];Span<BufferCount> counts=stackalloc BufferCount[2];
        Span<KernelVector3> point=stackalloc KernelVector3[2];SurfaceDerivativeLayout.TryCreate(1,0,out var layout);
        var status=EvaluateCore(program,spine,-1,parameter,0,layout,point,false,default,out _,out _,snapshot:snapshot,nodeSnapshot:nodes,counts:counts);
        if(status!=AlgorithmStatus.Success)return status;
        variables=counts[0];var occurrences=nodes[..counts[1]];
        Span<IntervalNumber> boxes=stackalloc IntervalNumber[MaxVariables];Span<BufferOffset> charts=stackalloc BufferOffset[MaxOccurrences];
        Span<IntervalDual> values=stackalloc IntervalDual[MaxVariables],equations=stackalloc IntervalDual[MaxVariables];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[MaxVariables];
        for(var i=0;i<variables;i++)boxes[i]=new(snapshot[i][0]-width,snapshot[i][0]+width);
        for(var i=0;i<occurrences.Length;i++)
        {
            var range=Chart(program,occurrences[i].Spine);var t=i==0?parameter:snapshot[occurrences[i].Offset+3][0];var at=0;
            while(at<range.Length-2&&range[at+1].Parameter<=t)at++;charts[i]=at;
        }
        referenceStatus=IntervalJacobian(program,occurrences,boxes[..variables],new(parameter-width,parameter+width),reference,values,equations,charts,cache);
        var capacity=64+512*occurrences.Length;
        Span<IntervalExpressionNode> recorded=stackalloc IntervalExpressionNode[capacity];Span<IntervalNumber> derivatives=stackalloc IntervalNumber[capacity*variables];Span<BufferOffset> output=stackalloc BufferOffset[MaxVariables];
        var record=new IntervalJacobianRecord(recorded,derivatives,output[..variables]);
        actualStatus=RecordedIntervalJacobian(program,occurrences,boxes[..variables],new(parameter-width,parameter+width),actual,charts,cache,ref record);
        if(referenceStatus==AlgorithmStatus.Success&&actualStatus==AlgorithmStatus.Success)
        {
            // Exercise the production lifetime: the same occurrence/chart
            // record, a freshly rebound tighter box and unchanged native t.
            for(var i=0;i<variables;i++)boxes[i]=new(snapshot[i][0]-.75*width,snapshot[i][0]+.75*width);
            referenceStatus=IntervalJacobian(program,occurrences,boxes[..variables],new(parameter-width,parameter+width),reference,values,equations,charts,cache);
            // Spline coefficients are box-dependent; production does not retain
            // their record between boxes. This hook also tests their cold rule.
            if(HasAnyUv(occurrences))record.Ready=false;
            actualStatus=RecordedIntervalJacobian(program,occurrences,boxes[..variables],new(parameter-width,parameter+width),actual,charts,cache,ref record);
        }
        return AlgorithmStatus.Success;
    }
#endif

    private static AlgorithmStatus RecordIntervalResidual(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        IntervalExpression parameter,ReadOnlySpan<BufferOffset> fixedCharts,ReadOnlySpan<IntervalSplineContact> cache,JacobianExpressions expressions)
    {
        for(var i=nodes.Length-1;i>=0;i--)
        {
            var node=nodes[i];var spine=program.Spines[node.Spine];var point=expressions.Point(node.Offset);
            if(SpineParameterMap.Explicit(spine))
            {
                var t=i==0?parameter:expressions.Variable(node.Offset+3);
                var curve=spine.Curve;var origin=new IntervalExpressionVector(curve.Origin);var first=new IntervalExpressionVector(curve.First);var second=new IntervalExpressionVector(curve.Second);
                IntervalExpressionVector exact,tangent;
                if(curve.Kind==CurveClass.Line){exact=origin+first*t;tangent=first;}
                else if(curve.Kind is CurveClass.Circle or CurveClass.Ellipse)
                {
                    IntervalExpression.SinCos(t,out var sin,out var cos);exact=origin+first*cos+second*sin;tangent=second*cos-first*sin;
                    if(!exact.Finite||!tangent.Finite)return AlgorithmStatus.NotConverged;
                }
                else return AlgorithmStatus.Unsupported;
                var delta=point-exact;expressions.Store(node.Offset,delta.X);expressions.Store(node.Offset+1,delta.Y);expressions.Store(node.Offset+2,delta.Z);
                if(i>0)expressions.Store(node.Offset+3,IntervalExpressionVector.Dot(expressions.Point(nodes[node.Parent].Offset)-point,tangent));
                continue;
            }
            var status=RecordIntervalNormal(program,spine.First,nodes,i,expressions,out var a,out var n0,cache);if(status!=AlgorithmStatus.Success)return status;
            status=RecordIntervalNormal(program,spine.Second,nodes,i,expressions,out var b,out var n1,cache);if(status!=AlgorithmStatus.Success)return status;
            expressions.Store(node.Offset,a);expressions.Store(node.Offset+1,b);
            for(var side=0;side<2;side++)
            {
                var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0)continue;var support=program.Surfaces[side==0?spine.First:spine.Second];
                status=RecordIntervalSpline(program,support,expressions.Variable(uv),expressions.Variable(uv+1),out var foot,out var su,out var sv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
                expressions.Store(uv,IntervalExpressionVector.Dot(point-foot,su.Unit()));expressions.Store(uv+1,IntervalExpressionVector.Dot(point-foot,sv.Unit()));
            }
            var station=i==0?parameter:expressions.Variable(node.Offset+3);var chart=Chart(program,node.Spine)[fixedCharts[i]];
            // This is the original affine CHART equation, with the same
            // arithmetic grouping and native parameter, never a chord refit.
            expressions.Store(node.Offset+2,IntervalExpressionVector.Dot(point-new IntervalExpressionVector(chart.Position),new IntervalExpressionVector(chart.Chord))-(station-chart.Parameter)/chart.Scale);
            if(i>0)expressions.Store(node.Offset+3,IntervalExpressionVector.Dot(expressions.Point(nodes[node.Parent].Offset)-point,IntervalExpressionVector.Cross(n0,n1).Unit()));
        }
        if(expressions.Count[0]>expressions.Nodes.Length)return AlgorithmStatus.WorkspaceTooSmall;
        foreach(var output in expressions.Outputs)
            if(!IntervalExpression.FromNode(expressions.Nodes,expressions.Count,output).Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus RecordIntervalNormal(scoped in NestedBlendProgram program,BlendSurfaceIndex surface,scoped ReadOnlySpan<Occurrence> nodes,
        BufferOffset occurrence,JacobianExpressions expressions,out IntervalExpression distance,out IntervalExpressionVector normal,scoped ReadOnlySpan<IntervalSplineContact> cache)
    {
        distance=default;normal=default;var node=program.Surfaces[surface];var point=expressions.Point(nodes[occurrence].Offset);
        if(node.IsBSpline)
        {
            var uv=SplineUv(program,node,nodes[occurrence]);if(uv<0)return AlgorithmStatus.Unsupported;
            var status=RecordIntervalSpline(program,node,expressions.Variable(uv),expressions.Variable(uv+1),out var foot,out var su,out var sv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
            normal=IntervalExpressionVector.Cross(su,sv).Unit()*node.NormalSense;distance=IntervalExpressionVector.Dot(point-foot,normal)-node.Offset*node.NormalSense;
        }
        else if(node.IsBlend)
        {
            var a=nodes[occurrence].FirstChild;var b=nodes[occurrence].SecondChild;
            var child=a>=0&&nodes[a].Spine==node.Spine?a:b>=0&&nodes[b].Spine==node.Spine?b:-1;if(child<0)return AlgorithmStatus.Unsupported;
            var offset=point-expressions.Point(nodes[child].Offset);var length=offset.Norm();normal=offset/length*node.NormalSense;distance=(length-Math.Abs(node.Range0))*node.NormalSense;
        }
        else
        {
            var s=node.Analytic;var relative=point-new IntervalExpressionVector(s.Origin);var axis=new IntervalExpressionVector(s.Axis);var z=IntervalExpressionVector.Dot(relative,axis);
            switch(s.Kind)
            {
                case SurfaceClass.Plane:distance=z;normal=axis;break;
                case SurfaceClass.Sphere:var length=relative.Norm();distance=length-s.Radius;normal=relative/length;break;
                case SurfaceClass.Cylinder:
                case SurfaceClass.Cone:
                case SurfaceClass.Torus:
                    var radial=relative-axis*z;var rho=radial.Norm();var unit=radial/rho;
                    if(s.Kind==SurfaceClass.Cylinder){distance=rho-s.Radius;normal=unit;}
                    else if(s.Kind==SurfaceClass.Cone)
                    {
                        if(!((IntervalExpression)s.Radius+s.Secondary*z).PositiveValue())return AlgorithmStatus.NotConverged;
                        var factor=1/Math.Sqrt(1+s.Secondary*s.Secondary);distance=(rho-s.Radius-z*s.Secondary)*factor;normal=(unit-axis*s.Secondary)*factor;
                    }
                    else
                    {
                        if(!(s.Radius>s.Secondary&&s.Secondary>0))return AlgorithmStatus.Unsupported;
                        var delta=rho-s.Radius;var profile=IntervalExpression.Sqrt(IntervalExpression.Square(delta)+IntervalExpression.Square(z));distance=profile-s.Secondary;normal=(unit*delta+axis*z)/profile;
                    }
                    break;
                default:return AlgorithmStatus.Unsupported;
            }
            distance*=node.NormalSense;normal=normal*node.NormalSense;
        }
        return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus RecordIntervalSpline(scoped in NestedBlendProgram program,scoped in BlendSurfaceInstruction node,IntervalExpression u,IntervalExpression v,
        out IntervalExpressionVector point,out IntervalExpressionVector du,out IntervalExpressionVector dv,scoped ReadOnlySpan<IntervalSplineContact> cache,BufferOffset cacheIndex)
    {
        point=du=dv=default;Span<IntervalNumber> local=stackalloc IntervalNumber[18];scoped ReadOnlySpan<IntervalNumber> hull;
        if(SplineContactMatches(cache[cacheIndex],node,u.Value,v.Value))hull=cache[cacheIndex].Values;
        else{var status=IntervalSplineBounds(program,node,u.Value,v.Value,local);if(status!=AlgorithmStatus.Success)return status;hull=local;}
        point=RecordSplineVector(hull,0,3,6,u,v);du=RecordSplineVector(hull,3,9,12,u,v);dv=RecordSplineVector(hull,6,12,15,u,v);
        return point.Finite&&du.Finite&&dv.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }
    private static IntervalExpressionVector RecordSplineVector(scoped ReadOnlySpan<IntervalNumber> jets,BufferOffset p,BufferOffset du,BufferOffset dv,IntervalExpression u,IntervalExpression v)
        =>new(IntervalExpression.Spline(jets[p],jets[du],jets[dv],u,v),IntervalExpression.Spline(jets[p+1],jets[du+1],jets[dv+1],u,v),IntervalExpression.Spline(jets[p+2],jets[du+2],jets[dv+2],u,v));
}
