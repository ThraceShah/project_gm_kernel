using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Joint occurrence solve: every nested spine contributes its point,
/// its native parameter and a nearest-spine stationarity equation. Nested
/// normals use those unknowns directly instead of invoking inner Newton loops.</summary>
internal static partial class JointNestedBlendEvaluation
{
    private const BufferCount MaxOccurrences=16,MaxVariables=63;
    private struct Occurrence
    {
        internal BlendSpineIndex Spine;
        internal BufferOffset Offset,Parent,FirstChild,SecondChild;
        internal BufferOffset FirstUv,SecondUv;
    }

    internal static AlgorithmStatus Evaluate(in NestedBlendProgram program,BlendSurfaceIndex surface,
        double u,double v,in SurfaceDerivativeLayout layout,Span<KernelVector3> output)
    {
        var root=program.Surfaces[surface];if(!root.IsBlend)return AlgorithmStatus.Unsupported;
        if(SpineParameterMap.Explicit(program.Spines[root.Spine]))return NestedBlendSeriesEvaluation.Evaluate(program,surface,u,v,layout,output);
        return EvaluateCore(program,root.Spine,surface,u,v,layout,output,false,default,out _,out _);
    }

    internal static AlgorithmStatus EvaluateSpine(in NestedBlendProgram program,BlendSpineIndex spine,
        double parameter,DerivativeOrder order,Span<KernelVector3> output,Span<double> contacts=default)
    {
        if(!SurfaceDerivativeLayout.TryCreate(order,0,out var layout))return AlgorithmStatus.InvalidInput;
        if(SpineParameterMap.Explicit(program.Spines[spine]))
        {
            if(order>2||output.Length<=order)return AlgorithmStatus.Unsupported;
            if(!double.IsFinite(parameter))return AlgorithmStatus.InvalidInput;
            var status=NestedBlendSeriesEvaluation.Spine(program,spine,parameter,order,0,out var exact);if(status!=AlgorithmStatus.Success)return status;
            double factor=1;for(var i=0;i<=order;i++){if(i>0)factor*=i;output[i]=new(){X=exact.X[i]*factor,Y=exact.Y[i]*factor,Z=exact.Z[i]*factor};}
            return AlgorithmStatus.Success;
        }
        return EvaluateCore(program,spine,-1,parameter,0,layout,output,false,default,out _,out _,contacts);
    }

    internal static AlgorithmStatus Project(in NestedBlendProgram program,BlendSpineIndex spine,Span<KernelVector3> output,Span<double> contacts)
    {
        SurfaceDerivativeLayout.TryCreate(1,0,out var layout);
        return EvaluateCore(program,spine,-1,0,0,layout,output,false,default,out _,out _,contacts);
    }

    internal static AlgorithmStatus ProjectBoundary(in NestedBlendProgram program,BlendSpineIndex spine,BufferOffset side,BufferOffset axis,double value,
        Span<KernelVector3> output,Span<double> contacts)
    {
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        return EvaluateCore(program,spine,-1,0,0,layout,output,false,default,out _,out _,contacts,side*2+axis,value);
    }

    internal static AlgorithmStatus Distance(in NestedBlendProgram program,BlendSurfaceIndex surface,in TaylorVector path,
        out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;var node=program.Surfaces[surface];if(!node.IsBlend)return AlgorithmStatus.Unsupported;
        var charts=Chart(program,node.Spine);var best=double.PositiveInfinity;var parameter=charts[0].Parameter;
        for(var i=0;i<charts.Length-1;i++)
        {
            var chord=Sub(charts[i+1].Position,charts[i].Position);var fraction=Math.Clamp(Dot(Sub(path.Value,charts[i].Position),chord)/Dot(chord,chord),0,1);
            var delta=Sub(path.Value,Add(charts[i].Position,Scale(chord,fraction)));var square=Dot(delta,delta);
            if(square<best){best=square;parameter=charts[i].Parameter+fraction*(charts[i+1].Parameter-charts[i].Parameter);}
        }
        if(!SurfaceDerivativeLayout.TryCreate(path.X.Order,0,out var layout))return AlgorithmStatus.Unsupported;
        return EvaluateCore(program,node.Spine,surface,parameter,0,layout,default,true,path,out distance,out normal);
    }

    private static AlgorithmStatus EvaluateCore(in NestedBlendProgram program,BlendSpineIndex rootSpine,
        BlendSurfaceIndex surface,double u,double v,in SurfaceDerivativeLayout layout,Span<KernelVector3> output,
        bool nearest,in TaylorVector path,out TaylorJet distance,out TaylorVector normal,Span<double> contacts=default,BufferOffset boundary=-1,double boundaryValue=0,
        Span<TaylorJet> snapshot=default,Span<Occurrence> nodeSnapshot=default,Span<BufferCount> counts=default)
    {
        distance=default;normal=default;
        if(!nearest&&(layout.UOrder>2||layout.VOrder>2||output.Length<layout.Count)||layout.UOrder>TaylorJet.MaxOrder)return AlgorithmStatus.Unsupported;
        if(!double.IsFinite(u)||!double.IsFinite(v))return AlgorithmStatus.InvalidInput;
        var root=surface>=0?program.Surfaces[surface]:default;
        Span<Occurrence> occurrences=stackalloc Occurrence[MaxOccurrences];
        Span<double> state=stackalloc double[MaxVariables],candidate=stackalloc double[MaxVariables];
        var count=1;var n=nearest?4:3;occurrences[0]=new(){Spine=rootSpine,Parent=-1,Offset=0,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        if(!Seed(program,rootSpine,u,out var first))return AlgorithmStatus.InvalidInput;
        SetPoint(state,0,first);
        if(nearest)state[3]=u;
        for(var i=0;i<count;i++)
        {
            var spine=program.Spines[occurrences[i].Spine];
            if(SpineParameterMap.Explicit(spine))continue;
            for(var side=0;side<2;side++)
            {
                var support=program.Surfaces[side==0?spine.First:spine.Second];
                if(support.IsBSpline)
                {
                    if(side==1&&occurrences[i].FirstUv>=0&&program.Surfaces[spine.First].SplineBasis==support.SplineBasis)
                    {occurrences[i].SecondUv=occurrences[i].FirstUv;continue;}
                    if(n+2>MaxVariables)return AlgorithmStatus.Unsupported;
                    var splineParameter=i==0?u:state[occurrences[i].Offset+3];var domain=Chart(program,occurrences[i].Spine);var segment=0;
                    while(segment<domain.Length-2&&domain[segment+1].Parameter<=splineParameter)segment++;
                    var fraction=(splineParameter-domain[segment].Parameter)/(domain[segment+1].Parameter-domain[segment].Parameter);
                    var firstU=side==0?domain[segment].FirstU:domain[segment].SecondU;var firstV=side==0?domain[segment].FirstV:domain[segment].SecondV;
                    var lastU=side==0?domain[segment+1].FirstU:domain[segment+1].SecondU;var lastV=side==0?domain[segment+1].FirstV:domain[segment+1].SecondV;
                    var spline=program.Spline(support);var du=lastU-firstU;var dv=lastV-firstV;
                    if(spline.UPeriodic)du-=(spline.UEnd-spline.UStart)*Math.Round(du/(spline.UEnd-spline.UStart));
                    if(spline.VPeriodic)dv-=(spline.VEnd-spline.VStart)*Math.Round(dv/(spline.VEnd-spline.VStart));
                    state[n]=firstU+fraction*du;state[n+1]=firstV+fraction*dv;
                    if(side==0)occurrences[i].FirstUv=n;else occurrences[i].SecondUv=n;n+=2;continue;
                }
                if(!support.IsBlend)continue;
                if(side==1&&occurrences[i].FirstChild>=0&&occurrences[occurrences[i].FirstChild].Spine==support.Spine)
                {occurrences[i].SecondChild=occurrences[i].FirstChild;continue;}
                if(count==MaxOccurrences||n+4>MaxVariables)return AlgorithmStatus.Unsupported;
                var child=count++;occurrences[child]=new(){Spine=support.Spine,Offset=n,Parent=i,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
                if(side==0)occurrences[i].FirstChild=child;else occurrences[i].SecondChild=child;
                var charts=Chart(program,support.Spine);var parent=Point(state,occurrences[i].Offset);
                var best=double.PositiveInfinity;var parameter=charts[0].Parameter;
                for(var j=0;j<charts.Length-1;j++)
                {
                    var chord=Sub(charts[j+1].Position,charts[j].Position);var square=Dot(chord,chord);
                    var fraction=Math.Clamp(Dot(Sub(parent,charts[j].Position),chord)/square,0,1);
                    var point=Add(charts[j].Position,Scale(chord,fraction));var delta=Sub(parent,point);var squareDistance=Dot(delta,delta);
                    if(squareDistance<best){best=squareDistance;parameter=charts[j].Parameter+(charts[j+1].Parameter-charts[j].Parameter)*fraction;}
                }
                if(!Seed(program,support.Spine,parameter,out var position))return AlgorithmStatus.InvalidInput;
                SetPoint(state,n,position);state[n+3]=parameter;n+=4;
            }
        }
        var nodes=occurrences[..count];state=state[..n];candidate=candidate[..n];
        var tolerance=ResidualAccuracy(program,rootSpine,first);if(!double.IsFinite(tolerance))return AlgorithmStatus.InvalidInput;
        var boundaryUv=boundary<0?-1:(boundary<2?nodes[0].FirstUv:nodes[0].SecondUv)+(boundary%2);
        if(boundary>=0){if(boundaryUv<3)return AlgorithmStatus.InvalidInput;state[boundaryUv]=boundaryValue;}
        Span<TaylorJet> variables=stackalloc TaylorJet[MaxVariables],residual=stackalloc TaylorJet[MaxVariables];
        Span<double> matrix=stackalloc double[MaxVariables*MaxVariables],rhs=stackalloc double[MaxVariables];
        Span<double> scalarResidual=stackalloc double[MaxVariables];
        Span<int> pivots=stackalloc int[MaxVariables];var converged=false;
        for(var iteration=0;iteration<20;iteration++)
        {
            var status=Assemble(program,nodes,state,u,scalarResidual[..n],matrix[..(n*n)],nearest,path.Value,boundaryUv,boundaryValue);if(status!=AlgorithmStatus.Success)return status;
            var error=Maximum(scalarResidual[..n]);
            if(error<tolerance)
            {status=ValidateNearest(program,nodes,state,matrix[..(n*n)],nearest);if(status!=AlgorithmStatus.Success)return status;}
            status=SmallLinearSolve.LuFactorize(matrix[..(n*n)],n,pivots);if(status!=AlgorithmStatus.Success)return status;
            if(error<tolerance){converged=true;break;}
            for(var row=0;row<n;row++)rhs[row]=-scalarResidual[row];
            status=SmallLinearSolve.LuSolveInPlace(matrix,n,pivots,rhs[..n]);if(status!=AlgorithmStatus.Success)return status;
            var accepted=false;
            for(var attempt=0;attempt<10;attempt++)
            {
                var factor=Math.ScaleB(1,-attempt);for(var k=0;k<n;k++)candidate[k]=state[k]+factor*rhs[k];
                if(boundaryUv>=0)candidate[boundaryUv]=boundaryValue;
                WrapClosedParameters(program,nodes,candidate,nearest);
                status=Assemble(program,nodes,candidate,u,scalarResidual[..n],default,nearest,path.Value,boundaryUv,boundaryValue);
                if(status!=AlgorithmStatus.Success||!(Maximum(scalarResidual[..n])<error))continue;
                candidate.CopyTo(state);accepted=true;break;
            }
            if(!accepted)return AlgorithmStatus.NotConverged;
        }
        if(!converged)return AlgorithmStatus.NotConverged;
        Initialize(state,layout.UOrder,variables);var parameterJet=TaylorJet.Variable(u,layout.UOrder);
        if(!contacts.IsEmpty)
        {
            if(contacts.Length<4)return AlgorithmStatus.OutputTooSmall;
            for(var side=0;side<2;side++)
            {
                var located=SpineSupportParameters(program,nodes,variables,side,u,out var su,out var sv);if(located!=AlgorithmStatus.Success)return located;
                contacts[2*side]=su;contacts[2*side+1]=sv;
            }
        }
        for(var order=1;order<=layout.UOrder;order++)
        {
            var status=Residual(program,nodes,variables[..n],parameterJet,residual[..n],nearest,path);if(status!=AlgorithmStatus.Success)return status;
            for(var row=0;row<n;row++)rhs[row]=-residual[row][order];
            status=SmallLinearSolve.LuSolveInPlace(matrix,n,pivots,rhs[..n]);if(status!=AlgorithmStatus.Success)return status;
            for(var k=0;k<n;k++)variables[k][order]=rhs[k];
        }
        var center=Point(variables,0);
        if(!snapshot.IsEmpty){variables[..n].CopyTo(snapshot);nodes.CopyTo(nodeSnapshot);counts[0]=n;counts[1]=nodes.Length;}
        if(nearest)
        {
            var offset=path-center;var length=offset.Norm();if(!(length[0]>1e-15))return AlgorithmStatus.Singular;
            distance=(length-Math.Abs(root.Range0))*root.NormalSense;normal=offset/length*root.NormalSense;
            return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
        }
        if(surface<0)
        {
            double factor=1;for(var k=0;k<=layout.UOrder;k++){if(k>0)factor*=k;output[k]=new(){X=center.X[k]*factor,Y=center.Y[k]*factor,Z=center.Z[k]*factor};}
            return AlgorithmStatus.Success;
        }
        var found=Normal(program,root.First,nodes,0,variables,out _,out var normal0);if(found!=AlgorithmStatus.Success)return found;
        found=Normal(program,root.Second,nodes,0,variables,out _,out var normal1);if(found!=AlgorithmStatus.Success)return found;
        return NestedBlendSeriesEvaluation.FromFrame(root,center,normal0,normal1,v,layout,output);
    }

    private static AlgorithmStatus SpineSupportParameters(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<TaylorJet> variables,BufferOffset side,double parameter,out double u,out double v)
    {
        u=v=0;var root=nodes[0];var spine=program.Spines[root.Spine];var support=program.Surfaces[side==0?spine.First:spine.Second];
        var splineUv=side==0?root.FirstUv:root.SecondUv;
        if(splineUv>=0){u=variables[splineUv][0];v=variables[splineUv+1][0];return AlgorithmStatus.Success;}
        var point=Point(variables,root.Offset).Value;
        if(!support.IsBlend)
        {
            var status=Geometry.Intersection.OperationIntersection.Parameters(support.Analytic,point,out u,out v);if(status!=AlgorithmStatus.Success)return status;
            if(support.Analytic.Kind is SurfaceClass.Cylinder or SurfaceClass.Sphere or SurfaceClass.Cone or SurfaceClass.Torus)
            {
                var charts=Chart(program,root.Spine);var segment=0;while(segment<charts.Length-2&&charts[segment+1].Parameter<=parameter)segment++;
                var f=(parameter-charts[segment].Parameter)/(charts[segment+1].Parameter-charts[segment].Parameter);
                var a=side==0?charts[segment].FirstU:charts[segment].SecondU;var b=side==0?charts[segment+1].FirstU:charts[segment+1].SecondU;
                b-=Math.Tau*Math.Round((b-a)/Math.Tau);u+=Math.Tau*Math.Round((a+(b-a)*f-u)/Math.Tau);
            }
            return AlgorithmStatus.Success;
        }
        var child=side==0?root.FirstChild:root.SecondChild;if(child<0)return AlgorithmStatus.Unsupported;
        u=variables[nodes[child].Offset+3][0];var center=Point(variables,nodes[child].Offset).Value;
        var found=Normal(program,support.First,nodes,child,variables,out _,out var n0);if(found!=AlgorithmStatus.Success)return found;
        found=Normal(program,support.Second,nodes,child,variables,out _,out var n1);if(found!=AlgorithmStatus.Success)return found;
        var radius=Math.Abs(support.Range0);var start=Scale(n1.Value,-support.Range1/radius);var end=Scale(n0.Value,-support.Range0/radius);
        if(support.ReverseSection)(start,end)=(end,start);
        var cosine=Dot(start,end);var transverse=Sub(end,Scale(start,cosine));var sine=Math.Sqrt(Dot(transverse,transverse));
        if(!(sine>0))return AlgorithmStatus.Singular;var offset=Sub(point,center);
        v=Math.Atan2(Dot(offset,Scale(transverse,1/sine)),Dot(offset,start))/Math.Atan2(sine,cosine);
        return double.IsFinite(u)&&double.IsFinite(v)?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    private static void WrapClosedParameters(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,Span<double> state,bool nearest)
    {
        for(var i=nearest?0:1;i<nodes.Length;i++)
        {
            var node=program.Spines[nodes[i].Spine];if(node.StartType!=LimitType.Help||node.EndType!=LimitType.Help)continue;
            var charts=Chart(program,nodes[i].Spine);var first=charts[0];var last=charts[^1];
            if(first.Position.X!=last.Position.X||first.Position.Y!=last.Position.Y||first.Position.Z!=last.Position.Z)continue;
            var offset=nodes[i].Offset+3;var value=state[offset];
            if(value<first.Parameter||value>last.Parameter){var period=last.Parameter-first.Parameter;state[offset]=first.Parameter+((value-first.Parameter)%period+period)%period;}
        }
    }

    // A stationary foot may be a maximum. With the parent point fixed, the
    // inverse parameter/stationarity entry is negative at a regular minimum.
    private static AlgorithmStatus ValidateNearest(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<double> state,ReadOnlySpan<double> matrix,bool nearest)
    {
        Span<BufferOffset> map=stackalloc BufferOffset[MaxVariables];Span<bool> selected=stackalloc bool[MaxOccurrences];
        Span<double> block=stackalloc double[MaxVariables*MaxVariables],rhs=stackalloc double[MaxVariables];Span<int> pivots=stackalloc int[MaxVariables];
        var n=state.Length;
        for(var root=nearest?0:1;root<nodes.Length;root++)
        {
            selected.Clear();selected[root]=true;var count=0;var parameterIndex=-1;
            for(var i=root;i<nodes.Length;i++)
            {
                if(i!=root)selected[i]=nodes[i].Parent>=0&&selected[nodes[i].Parent];if(!selected[i])continue;
                var node=nodes[i];for(var j=0;j<4;j++){if(i==root&&j==3)parameterIndex=count;map[count++]=node.Offset+j;}
                if(node.FirstUv>=0){map[count++]=node.FirstUv;map[count++]=node.FirstUv+1;}
                if(node.SecondUv>=0&&node.SecondUv!=node.FirstUv){map[count++]=node.SecondUv;map[count++]=node.SecondUv+1;}
            }
            for(var i=0;i<count;i++)for(var j=0;j<count;j++)block[i*count+j]=matrix[map[i]*n+map[j]];
            var status=SmallLinearSolve.LuFactorize(block[..(count*count)],count,pivots);if(status!=AlgorithmStatus.Success)return status;
            rhs[..count].Clear();rhs[parameterIndex]=1;status=SmallLinearSolve.LuSolveInPlace(block,count,pivots,rhs[..count]);if(status!=AlgorithmStatus.Success)return status;
            if(SpineParameterMap.Explicit(program.Spines[nodes[root].Spine]))
            {if(!(rhs[parameterIndex]<0)||!double.IsFinite(rhs[parameterIndex]))return AlgorithmStatus.Singular;continue;}
            var found=Partial(program,program.Spines[nodes[root].Spine].First,nodes,root,state,out var first,out _,out _,out _);if(found!=AlgorithmStatus.Success)return found;
            found=Partial(program,program.Spines[nodes[root].Spine].Second,nodes,root,state,out var second,out _,out _,out _);if(found!=AlgorithmStatus.Success)return found;
            var charts=Chart(program,nodes[root].Spine);var segment=0;var t=state[nodes[root].Offset+3];while(segment<charts.Length-2&&charts[segment+1].Parameter<=t)segment++;
            found=SpineParameterMap.Scalar(program.Spines[nodes[root].Spine],charts[segment],t,Point(state,nodes[root].Offset),out _,out var gauge,out _);
            if(found!=AlgorithmStatus.Success)return found;
            var orientation=Dot(Cross(first.Gradient,second.Gradient),gauge);
            if(!(rhs[parameterIndex]*orientation<0)||!double.IsFinite(rhs[parameterIndex]))return AlgorithmStatus.Singular;
        }
        for(var i=0;i<nodes.Length;i++)
        {
            var node=nodes[i];var spine=program.Spines[node.Spine];
            for(var side=0;side<2;side++)
            {
                var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0)continue;
                var support=program.Surfaces[side==0?spine.First:spine.Second];
                var status=NestedSplineEvaluation.Contact(program,support,state[uv],state[uv+1],out var c);if(status!=AlgorithmStatus.Success)return status;
                var q=Sub(Point(state,node.Offset),c.Point);var a=Dot(c.Du,c.Du)-Dot(q,c.Duu);var b=Dot(c.Du,c.Dv)-Dot(q,c.Duv);var d=Dot(c.Dv,c.Dv)-Dot(q,c.Dvv);
                if(!(a>0&&d>0&&a*d-b*b>1e-12*a*d))return AlgorithmStatus.Singular;
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Assemble(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<double> state,double parameter,Span<double> residual,Span<double> matrix,bool nearest,in KernelVector3 target,BufferOffset boundaryUv=-1,double boundaryValue=0)
    {
        var n=state.Length;matrix.Clear();
        for(var i=nodes.Length-1;i>=0;i--)
        {
            var node=nodes[i];var spine=program.Spines[node.Spine];var point=Point(state,node.Offset);
            if(SpineParameterMap.Explicit(spine))
            {
                var parameterValue=i==0&&!nearest?parameter:state[node.Offset+3];
                if(!InChartDomain(program,node.Spine,parameterValue,i!=0||nearest))return AlgorithmStatus.InvalidInput;
                var found=AssembleExplicit(program,spine,node,parameterValue,state,residual,matrix,i!=0||nearest,i==0?target:Point(state,nodes[node.Parent].Offset),i==0?-1:nodes[node.Parent].Offset);
                if(found!=AlgorithmStatus.Success)return found;continue;
            }
            var status=Partial(program,spine.First,nodes,i,state,out var a,out var firstChild,out var uv0,out var contact0);if(status!=AlgorithmStatus.Success)return status;
            status=Partial(program,spine.Second,nodes,i,state,out var b,out var secondChild,out var uv1,out var contact1);if(status!=AlgorithmStatus.Success)return status;
            residual[node.Offset]=a.Distance;residual[node.Offset+1]=b.Distance;
            AddGradient(matrix,n,node.Offset,node.Offset,a.Gradient);AddGradient(matrix,n,node.Offset+1,node.Offset,b.Gradient);
            if(firstChild>=0)AddGradient(matrix,n,node.Offset,nodes[firstChild].Offset,Scale(a.Gradient,-1));
            if(secondChild>=0)AddGradient(matrix,n,node.Offset+1,nodes[secondChild].Offset,Scale(b.Gradient,-1));
            if(uv0>=0)SplineRows(program.Surfaces[spine.First],contact0,point,node.Offset,uv0,node.Offset,state,residual,matrix);
            if(uv1>=0)SplineRows(program.Surfaces[spine.Second],contact1,point,node.Offset,uv1,node.Offset+1,state,residual,matrix,uv1!=uv0);
            var t=i==0&&!nearest?parameter:state[node.Offset+3];var charts=Chart(program,node.Spine);
            if(!InChartDomain(program,node.Spine,t,i!=0||nearest))return AlgorithmStatus.InvalidInput;
            var segment=0;while(segment<charts.Length-2&&charts[segment+1].Parameter<=t)segment++;
            var chart=charts[segment];status=SpineParameterMap.Scalar(spine,chart,t,point,out residual[node.Offset+2],out var gauge,out var dt);
            if(status!=AlgorithmStatus.Success)return status;
            AddGradient(matrix,n,node.Offset+2,node.Offset,gauge);
            if(i==0&&boundaryUv>=0)
            {
                residual[2]=state[boundaryUv]-boundaryValue;
                if(!matrix.IsEmpty){matrix.Slice(2*n,n).Clear();matrix[2*n+boundaryUv]=1;}
            }
            if(i!=0||nearest)
            {
                if(!matrix.IsEmpty)matrix[(node.Offset+2)*n+node.Offset+3]=dt;
                var tangent=Cross(a.Gradient,b.Gradient);var length=Math.Sqrt(Dot(tangent,tangent));if(!(length>1e-10))return AlgorithmStatus.Singular;
                tangent=Scale(tangent,1/length);var q=Sub(i==0?target:Point(state,nodes[node.Parent].Offset),point);
                var value=Dot(q,tangent);residual[node.Offset+3]=value;
                if(!matrix.IsEmpty)
                {
                    var adjoint=Scale(Sub(q,Scale(tangent,value)),1/length);
                    var da=Multiply(a,Cross(b.Gradient,adjoint));var db=Multiply(b,Cross(adjoint,a.Gradient));
                    AddGradient(matrix,n,node.Offset+3,node.Offset,Sub(Add(da,db),tangent));
                    if(i!=0)AddGradient(matrix,n,node.Offset+3,nodes[node.Parent].Offset,tangent);
                    if(firstChild>=0)AddGradient(matrix,n,node.Offset+3,nodes[firstChild].Offset,Scale(da,-1));
                    if(secondChild>=0)AddGradient(matrix,n,node.Offset+3,nodes[secondChild].Offset,Scale(db,-1));
                    if(uv0>=0)
                    {
                        var direction=Scale(Cross(b.Gradient,adjoint),program.Surfaces[spine.First].NormalSense);
                        matrix[(node.Offset+3)*n+uv0]+=Dot(direction,contact0.NormalU);matrix[(node.Offset+3)*n+uv0+1]+=Dot(direction,contact0.NormalV);
                    }
                    if(uv1>=0)
                    {
                        var direction=Scale(Cross(adjoint,a.Gradient),program.Surfaces[spine.Second].NormalSense);
                        matrix[(node.Offset+3)*n+uv1]+=Dot(direction,contact1.NormalU);matrix[(node.Offset+3)*n+uv1+1]+=Dot(direction,contact1.NormalV);
                    }
                }
            }
        }
        for(var i=0;i<n;i++)if(!double.IsFinite(residual[i]))return AlgorithmStatus.NumericalFailure;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Partial(in NestedBlendProgram program,BlendSurfaceIndex support,
        ReadOnlySpan<Occurrence> nodes,BufferOffset occurrence,ReadOnlySpan<double> state,
        out OrientedDistanceJet jet,out BufferOffset child,out BufferOffset uv,out NestedSplineEvaluation.ContactJet contact)
    {
        child=uv=-1;contact=default;var node=program.Surfaces[support];var point=Point(state,nodes[occurrence].Offset);
        if(node.IsBSpline)
        {
            uv=SplineUv(program,node,nodes[occurrence]);jet=default;if(uv<0)return AlgorithmStatus.Unsupported;
            var status=NestedSplineEvaluation.Contact(program,node,state[uv],state[uv+1],out contact);if(status!=AlgorithmStatus.Success)return status;
            var normal=Scale(contact.Normal,node.NormalSense);var value=node.NormalSense*(Dot(Sub(point,contact.Point),contact.Normal)-node.Offset);
            jet=new(value,normal,0,0,0,0,0,0,contact.Point,DistanceGrade.Exact);return AlgorithmStatus.Success;
        }
        if(!node.IsBlend)return SurfaceDistanceEvaluation.Evaluate(node.Analytic,point,(KernelSense)node.NormalSense,2,out jet);
        var a=nodes[occurrence].FirstChild;var b=nodes[occurrence].SecondChild;
        child=a>=0&&nodes[a].Spine==node.Spine?a:b>=0&&nodes[b].Spine==node.Spine?b:-1;
        jet=default;if(child<0)return AlgorithmStatus.Unsupported;
        // Holding the occurrence's spine point fixed, the local partials are
        // sphere-distance partials; child-point columns carry their negatives.
        var sphere=new AnalyticSurface(SurfaceClass.Sphere,Point(state,nodes[child].Offset),default,default,Math.Abs(node.Range0));
        return SurfaceDistanceEvaluation.Evaluate(sphere,point,(KernelSense)node.NormalSense,2,out jet);
    }
    private static KernelVector3 Multiply(in OrientedDistanceJet jet,in KernelVector3 value)=>new()
    {X=jet.Hxx*value.X+jet.Hxy*value.Y+jet.Hxz*value.Z,Y=jet.Hxy*value.X+jet.Hyy*value.Y+jet.Hyz*value.Z,Z=jet.Hxz*value.X+jet.Hyz*value.Y+jet.Hzz*value.Z};
    private static void AddGradient(Span<double> matrix,BufferCount n,BufferOffset row,BufferOffset column,in KernelVector3 value)
    {if(matrix.IsEmpty)return;matrix[row*n+column]+=value.X;matrix[row*n+column+1]+=value.Y;matrix[row*n+column+2]+=value.Z;}
    private static double Maximum(ReadOnlySpan<double> residual){var result=0.0;foreach(var value in residual)result=Math.Max(result,Math.Abs(value));return result;}
    internal static double ResidualAccuracy(in NestedBlendProgram program,BlendSpineIndex spine,in KernelVector3 point)
    {
        var scale=MaximumCoordinate(point);var charts=Chart(program,spine);scale=Math.Max(scale,Math.Max(MaximumCoordinate(charts[0].Position),MaximumCoordinate(charts[^1].Position)));
        foreach(var node in program.Surfaces)
        {
            if(node.IsBlend)scale=Math.Max(scale,Math.Max(Math.Abs(node.Range0),Math.Abs(node.Range1)));
            else if(node.IsBSpline)
            {
                var poles=program.Spline(node).Vertices;var weight=node.Rational?poles[3]:1;
                if(weight!=0)scale=Math.Max(scale,Math.Max(Math.Abs(poles[0]/weight),Math.Max(Math.Abs(poles[1]/weight),Math.Abs(poles[2]/weight))));
            }
            else scale=Math.Max(scale,Math.Max(MaximumCoordinate(node.Analytic.Origin),Math.Max(Math.Abs(node.Analytic.Radius),node.Analytic.Kind==SurfaceClass.Cone?0:Math.Abs(node.Analytic.Secondary))));
        }
        return 64*SmallLinearSolve.MachineEpsilon*(scale>0?scale:1);
    }
    private static double MaximumCoordinate(in KernelVector3 point)=>Math.Max(Math.Abs(point.X),Math.Max(Math.Abs(point.Y),Math.Abs(point.Z)));

    private static AlgorithmStatus Residual(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<TaylorJet> variables,TaylorJet parameter,Span<TaylorJet> residual,bool nearest,in TaylorVector target)
    {
        Span<TaylorVector> normals0=stackalloc TaylorVector[MaxOccurrences],normals1=stackalloc TaylorVector[MaxOccurrences];
        for(var i=nodes.Length-1;i>=0;i--)
        {
            var node=nodes[i];var spine=program.Spines[node.Spine];
            if(SpineParameterMap.Explicit(spine))
            {
                var parameterValue=i==0&&!nearest?parameter:variables[node.Offset+3];
                if(!InChartDomain(program,node.Spine,parameterValue[0],i!=0||nearest))return AlgorithmStatus.InvalidInput;
                var found=SpineParameterMap.Series(program,spine,parameterValue,out var exact,out var tangent);if(found!=AlgorithmStatus.Success)return found;
                var delta=Point(variables,node.Offset)-exact;residual[node.Offset]=delta.X;residual[node.Offset+1]=delta.Y;residual[node.Offset+2]=delta.Z;
                if(i!=0||nearest)residual[node.Offset+3]=TaylorVector.Dot((i==0?target:Point(variables,nodes[node.Parent].Offset))-Point(variables,node.Offset),tangent);
                continue;
            }
            var status=Normal(program,spine.First,nodes,i,variables,out var d0,out normals0[i]);if(status!=AlgorithmStatus.Success)return status;
            status=Normal(program,spine.Second,nodes,i,variables,out var d1,out normals1[i]);if(status!=AlgorithmStatus.Success)return status;
            residual[node.Offset]=d0;residual[node.Offset+1]=d1;
            var first=program.Surfaces[spine.First];var second=program.Surfaces[spine.Second];
            if(first.IsBSpline)
            {
                status=SplineResidual(program,first,node,variables,residual);if(status!=AlgorithmStatus.Success)return status;
            }
            if(second.IsBSpline)
            {
                status=SplineResidual(program,second,node,variables,residual);if(status!=AlgorithmStatus.Success)return status;
            }
            var t=i==0&&!nearest?parameter:variables[node.Offset+3];var charts=Chart(program,node.Spine);
            if(!InChartDomain(program,node.Spine,t[0],i!=0||nearest))return AlgorithmStatus.InvalidInput;
            var segment=0;while(segment<charts.Length-2&&charts[segment+1].Parameter<=t[0])segment++;
            var chart=charts[segment];var point=Point(variables,node.Offset);
            status=SpineParameterMap.Series(spine,chart,t,point,out residual[node.Offset+2]);if(status!=AlgorithmStatus.Success)return status;
            if(i!=0||nearest)
            {
                var tangent=TaylorVector.Cross(normals0[i],normals1[i]);if(!(tangent.Norm()[0]>1e-10))return AlgorithmStatus.Singular;
                residual[node.Offset+3]=TaylorVector.Dot((i==0?target:Point(variables,nodes[node.Parent].Offset))-point,tangent.Unit());
            }
        }
        for(var i=0;i<residual.Length;i++)if(!residual[i].Finite)return AlgorithmStatus.NumericalFailure;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Normal(in NestedBlendProgram program,BlendSurfaceIndex support,
        ReadOnlySpan<Occurrence> nodes,BufferOffset occurrence,ReadOnlySpan<TaylorJet> variables,
        out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;var node=program.Surfaces[support];var point=Point(variables,nodes[occurrence].Offset);
        if(node.IsBSpline)
        {
            var uv=SplineUv(program,node,nodes[occurrence]);if(uv<0)return AlgorithmStatus.Unsupported;
            var status=NestedSplineEvaluation.Jets(program,node,variables[uv],variables[uv+1],out var surfacePoint,out var su,out var sv);if(status!=AlgorithmStatus.Success)return status;
            var cross=TaylorVector.Cross(su,sv);if(!(cross.Norm()[0]>1e-12))return AlgorithmStatus.Singular;
            normal=cross.Unit()*node.NormalSense;distance=TaylorVector.Dot(point-surfacePoint,normal)-node.Offset*node.NormalSense;return AlgorithmStatus.Success;
        }
        if(!node.IsBlend)
        {
            var status=AnalyticDistanceSeries.Evaluate(node.Analytic,point,out distance,out normal);
            distance*=node.NormalSense;normal=normal*node.NormalSense;return status;
        }
        var first=nodes[occurrence].FirstChild;var second=nodes[occurrence].SecondChild;
        var child=first>=0&&nodes[first].Spine==node.Spine?first:second>=0&&nodes[second].Spine==node.Spine?second:-1;
        if(child<0)return AlgorithmStatus.Unsupported;
        var offset=point-Point(variables,nodes[child].Offset);var length=offset.Norm();if(!(length[0]>1e-15))return AlgorithmStatus.Singular;
        distance=(length-Math.Abs(node.Range0))*node.NormalSense;normal=offset/length*node.NormalSense;return AlgorithmStatus.Success;
    }

    private static ReadOnlySpan<BlendChartInstruction> Chart(in NestedBlendProgram program,BlendSpineIndex spine)
    {var node=program.Spines[spine];return program.Charts.Slice(node.ChartOffset,node.ChartCount);}
    private static bool InChartDomain(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,bool allowArtificialExtension)
    {
        var node=program.Spines[spine];var charts=Chart(program,spine);
        if(SpineParameterMap.Periodic(node))return double.IsFinite(parameter);
        return double.IsFinite(parameter)&&(parameter>=charts[0].Parameter||allowArtificialExtension&&node.StartType==LimitType.Artificial)
            &&(parameter<=charts[^1].Parameter||allowArtificialExtension&&node.EndType==LimitType.Artificial);
    }
    private static bool Seed(in NestedBlendProgram program,BlendSpineIndex spine,double t,out KernelVector3 point)
    {
        var charts=Chart(program,spine);point=default;
        if(!SpineParameterMap.Periodic(program.Spines[spine])&&(t<charts[0].Parameter||t>charts[^1].Parameter))return false;
        if(SpineParameterMap.Explicit(program.Spines[spine]))
        {
            var status=SpineParameterMap.Series(program,program.Spines[spine],new(t,0),out var exact,out _);point=exact.Value;return status==AlgorithmStatus.Success;
        }
        var segment=0;while(segment<charts.Length-2&&charts[segment+1].Parameter<=t)segment++;
        var fraction=(t-charts[segment].Parameter)/(charts[segment+1].Parameter-charts[segment].Parameter);
        point=Add(charts[segment].Position,Scale(Sub(charts[segment+1].Position,charts[segment].Position),fraction));return true;
    }
    private static KernelVector3 Point(ReadOnlySpan<double> variables,BufferOffset offset)=>new(){X=variables[offset],Y=variables[offset+1],Z=variables[offset+2]};
    private static TaylorVector Point(ReadOnlySpan<TaylorJet> variables,BufferOffset offset)=>new(variables[offset],variables[offset+1],variables[offset+2]);
    private static void SetPoint(Span<double> variables,BufferOffset offset,in KernelVector3 value){variables[offset]=value.X;variables[offset+1]=value.Y;variables[offset+2]=value.Z;}
    private static void Initialize(ReadOnlySpan<double> state,DerivativeOrder order,Span<TaylorJet> jets){for(var i=0;i<state.Length;i++)jets[i]=new(state[i],order);}

    private static BufferOffset SplineUv(in NestedBlendProgram program,in BlendSurfaceInstruction surface,in Occurrence node)
    {
        var spine=program.Spines[node.Spine];var first=program.Surfaces[spine.First];var second=program.Surfaces[spine.Second];
        return first.IsBSpline&&first.SplineBasis==surface.SplineBasis?node.FirstUv:second.IsBSpline&&second.SplineBasis==surface.SplineBasis?node.SecondUv:-1;
    }
    private static AlgorithmStatus SplineResidual(in NestedBlendProgram program,in BlendSurfaceInstruction surface,in Occurrence node,
        ReadOnlySpan<TaylorJet> variables,Span<TaylorJet> residual)
    {
        var uv=SplineUv(program,surface,node);if(uv<0)return AlgorithmStatus.Unsupported;
        var status=NestedSplineEvaluation.Jets(program,surface,variables[uv],variables[uv+1],out var point,out var su,out var sv);if(status!=AlgorithmStatus.Success)return status;
        if(!(su.Norm()[0]>1e-12&&sv.Norm()[0]>1e-12))return AlgorithmStatus.Singular;
        var q=Point(variables,node.Offset)-point;residual[uv]=TaylorVector.Dot(q,su.Unit());residual[uv+1]=TaylorVector.Dot(q,sv.Unit());return AlgorithmStatus.Success;
    }
    private static void SplineRows(in BlendSurfaceInstruction surface,in NestedSplineEvaluation.ContactJet contact,in KernelVector3 point,
        BufferOffset pointOffset,BufferOffset uv,BufferOffset row,ReadOnlySpan<double> state,Span<double> residual,Span<double> matrix,bool includeStationarity=true)
    {
        var n=state.Length;var q=Sub(point,contact.Point);var tu=Unit(contact.Du);var tv=Unit(contact.Dv);
        var uLength=Math.Sqrt(Dot(contact.Du,contact.Du));var vLength=Math.Sqrt(Dot(contact.Dv,contact.Dv));
        residual[uv]=Dot(q,tu);residual[uv+1]=Dot(q,tv);if(matrix.IsEmpty)return;
        matrix[row*n+uv]+=surface.NormalSense*Dot(q,contact.NormalU);matrix[row*n+uv+1]+=surface.NormalSense*Dot(q,contact.NormalV);
        if(!includeStationarity)return;
        AddGradient(matrix,n,uv,pointOffset,tu);AddGradient(matrix,n,uv+1,pointOffset,tv);
        var tuu=Scale(Sub(contact.Duu,Scale(tu,Dot(tu,contact.Duu))),1/uLength);var tuv=Scale(Sub(contact.Duv,Scale(tu,Dot(tu,contact.Duv))),1/uLength);
        var tvu=Scale(Sub(contact.Duv,Scale(tv,Dot(tv,contact.Duv))),1/vLength);var tvv=Scale(Sub(contact.Dvv,Scale(tv,Dot(tv,contact.Dvv))),1/vLength);
        matrix[uv*n+uv]+=-Dot(contact.Du,tu)+Dot(q,tuu);matrix[uv*n+uv+1]+=-Dot(contact.Dv,tu)+Dot(q,tuv);
        matrix[(uv+1)*n+uv]+=-Dot(contact.Du,tv)+Dot(q,tvu);matrix[(uv+1)*n+uv+1]+=-Dot(contact.Dv,tv)+Dot(q,tvv);
    }
}
