using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    internal static bool ReuseSplineContactBounds=true;
    /// <summary>Certified local implicit tube, followed by interval analytic
    /// differentiation. Chart error estimates and sampled jets are not bounds.</summary>
    internal static AlgorithmStatus Bound(in NestedBlendProgram program,BlendSurfaceIndex surface,in SurfaceParameterBox domain,Span<IntervalNumber> output)
    {
        if(!domain.Valid||output.Length<9||!program.Surfaces[surface].IsBlend)return AlgorithmStatus.InvalidInput;
        return BoundGeometry(program,surface,program.Surfaces[surface].Spine,domain,output);
    }
    internal static AlgorithmStatus BoundSpine(in NestedBlendProgram program,BlendSpineIndex spine,in SurfaceParameterBox domain,Span<IntervalNumber> output)
    {
        if(!domain.Valid||output.Length<9||spine<0||spine>=program.Spines.Length)return AlgorithmStatus.InvalidInput;
        return BoundGeometry(program,-1,spine,domain,output);
    }

    // At one station, report both support locators from the midpoint of the
    // same certified joint state that supplies the position enclosure.
    internal static AlgorithmStatus BoundSpineAnchor(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,
        Span<IntervalNumber> output,Span<double> contacts)
    {
        var domain=new SurfaceParameterBox(parameter,parameter,0,0);
        if(!domain.Valid||output.Length<9||contacts.Length<4||spine<0||spine>=program.Spines.Length)return AlgorithmStatus.InvalidInput;
        if(SpineParameterMap.Explicit(program.Spines[spine]))return AlgorithmStatus.Unsupported;
        return BoundSegment(program,-1,spine,domain,output,anchorContacts:contacts);
    }

    // Shared root witnesses for an implicit intersection with analytic
    // supports, or one analytic support and one explicit rolling spine.
    // Both occurrences must use the same shared support equations.
    internal static AlgorithmStatus BoundSharedSpineWitness(in NestedBlendProgram program,BlendSpineIndex spine,
        in SurfaceParameterBox domain,Span<IntervalNumber> output,Span<IntervalNumber> capture=default,ReadOnlySpan<IntervalNumber> require=default)
    {
        var charts=Chart(program,spine);var chart=0;
        while(chart<charts.Length-2&&charts[chart+1].Parameter<=domain.U0)chart++;
        if(!domain.Valid||domain.U0<charts[chart].Parameter||domain.U1>charts[chart+1].Parameter)return AlgorithmStatus.NotConverged;
        return BoundSegment(program,-1,spine,domain,output,captureWitness:capture,requireWitness:require,rootChart:chart);
    }

    internal static AlgorithmStatus BoundSpineCurvature(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,BufferOffset side,out IntervalNumber curvature)
    {
        curvature=default;if(side is <0 or >1||SpineParameterMap.Explicit(program.Spines[spine]))return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> output=stackalloc IntervalNumber[9],result=stackalloc IntervalNumber[1];
        var found=BoundSegment(program,-1,spine,new(parameter,parameter,0,0),output,curvature:result,curvatureSide:side);
        if(found==AlgorithmStatus.Success)curvature=result[0];return found;
    }

    internal static AlgorithmStatus BoundSpineProjectedAcceleration(in NestedBlendProgram program,BlendSpineIndex spine,
        in SurfaceParameterBox domain,in KernelVector3 normal,out IntervalNumber acceleration,BufferOffset terminalChart=-1,
        scoped ReadOnlySpan<IntervalNumber> terminalWitness=default)
    {
        acceleration=default;
        if(!domain.Valid||spine<0||spine>=program.Spines.Length||SpineParameterMap.Explicit(program.Spines[spine]))
            return AlgorithmStatus.Unsupported;
        var charts=Chart(program,spine);var chart=0;
        while(chart<charts.Length-2&&charts[chart+1].Parameter<=domain.U0)chart++;
        if(terminalChart>=0)
        {
            if(terminalWitness.IsEmpty||terminalChart>=charts.Length-1)return AlgorithmStatus.InvalidInput;
            chart=terminalChart;
        }
        else if(domain.U0<charts[chart].Parameter||domain.U1>charts[chart+1].Parameter)return AlgorithmStatus.NotConverged;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],projection=stackalloc IntervalNumber[1];
        var status=BoundSegment(program,-1,spine,domain,bounds,requireWitness:terminalWitness,
            rootChart:chart,acceleration:projection,accelerationNormal:normal);
        if(status==AlgorithmStatus.Success)acceleration=projection[0];return status;
    }

    // A local implicit foot is not a global reach certificate. Consumers
    // that extend incident cap edges require this stronger whole-ball proof.
    internal static AlgorithmStatus CertifySplineReach(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last)
    {
        if(!(last>first)||spine<0||spine>=program.Spines.Length)return AlgorithmStatus.InvalidInput;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[64];Span<BufferCount> depth=stackalloc BufferCount[64],budget=stackalloc BufferCount[2];
        budget[0]=0;budget[1]=131072;Span<IntervalNumber> output=stackalloc IntervalNumber[9];var charts=Chart(program,spine);var covered=first;
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            var lo=Math.Max(first,charts[chart].Parameter);var hi=Math.Min(last,charts[chart+1].Parameter);if(!(hi>lo))continue;
            if(lo>covered)return AlgorithmStatus.NotConverged;covered=hi;
            pending[0]=new(lo,hi,0,0);depth[0]=0;var count=1;
            while(count>0)
            {
                if(++budget[0]>budget[1])return AlgorithmStatus.NotConverged;
                var at=--count;var box=pending[at];var level=depth[at];
                var status=BoundSegment(program,-1,spine,box,output,budget);if(status==AlgorithmStatus.Success)continue;
                if(status!=AlgorithmStatus.NotConverged)return status;
                if(level>=28||count+2>pending.Length)return AlgorithmStatus.NotConverged;
                var middle=box.U0+(box.U1-box.U0)*.5;if(!(middle>box.U0&&middle<box.U1))return AlgorithmStatus.NotConverged;
                pending[count]=new(box.U0,middle,0,0);depth[count++]=level+1;
                pending[count]=new(middle,box.U1,0,0);depth[count++]=level+1;
            }
        }
        return covered==last?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }
    private static AlgorithmStatus BoundGeometry(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,Span<IntervalNumber> output)
    {
        var charts=Chart(program,spine);var first=true;Span<IntervalNumber> local=stackalloc IntervalNumber[9];
        for(var i=0;i<charts.Length-1;i++)
        {
            var lo=i==0?domain.U0:Math.Max(domain.U0,charts[i].Parameter);var hi=i==charts.Length-2?domain.U1:Math.Min(domain.U1,charts[i+1].Parameter);
            if(lo>hi)continue;if(lo==hi&&domain.U0!=domain.U1)continue;
            var cell=new SurfaceParameterBox(lo,hi,domain.V0,domain.V1);
            var status=SpineParameterMap.Explicit(program.Spines[spine])?BoundExplicit(program,surface,spine,cell,local):BoundSegment(program,surface,spine,cell,local);
            if(status!=AlgorithmStatus.Success)return status;
            for(var k=0;k<9;k++)output[k]=first?local[k]:IntervalNumber.Hull(output[k],local[k]);first=false;
        }
        return first?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }

    private static AlgorithmStatus BoundExplicit(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,
        in SurfaceParameterBox domain,Span<IntervalNumber> output)
    {
        var definition=program.Spines[spine];
        // Like the implicit tube bound, this encloses the mathematical
        // extension used by boundary-root existence certificates. Finite T
        // limits remain in the patch domain and public evaluation contract.
        var status=SpineParameterMap.Interval(program,definition,new(new(domain.U0,domain.U1),1),out var center,out _);if(status!=AlgorithmStatus.Success)return status;
        if(surface<0)
        {
            output[0]=center.X.Value;output[1]=center.Y.Value;output[2]=center.Z.Value;
            output[3]=center.X.Derivative;output[4]=center.Y.Derivative;output[5]=center.Z.Derivative;output.Slice(6,3).Clear();return AlgorithmStatus.Success;
        }
        // Explicit curves already carry their native branch and certified
        // support identity. Their position/derivatives do not require an
        // implicit-offset Jacobian to remain invertible at a real T limit.
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        Span<IntervalDual> variables=stackalloc IntervalDual[3]{center.X,center.Y,center.Z};
        status=IntervalFrame(program,surface,nodes,variables,new IntervalNumber(domain.V0,domain.V1),out var frame);if(status!=AlgorithmStatus.Success)return status;
        output[0]=frame.X.Value;output[1]=frame.Y.Value;output[2]=frame.Z.Value;
        output[3]=frame.X.Derivative;output[4]=frame.Y.Derivative;output[5]=frame.Z.Derivative;
        variables[0]=center.X.Value;variables[1]=center.Y.Value;variables[2]=center.Z.Value;
        status=IntervalFrame(program,surface,nodes,variables,new(new(domain.V0,domain.V1),1),out frame);if(status!=AlgorithmStatus.Success)return status;
        output[6]=frame.X.Derivative;output[7]=frame.Y.Derivative;output[8]=frame.Z.Derivative;return AlgorithmStatus.Success;
    }

    // Active scratch prefixes are written before use; keep the explicit
    // matrix, RHS and live spline-cache initialization inside their owners.
    [SkipLocalsInit]
    private static AlgorithmStatus BoundSegment(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,Span<IntervalNumber> output,Span<BufferCount> reachBudget=default,
        Span<IntervalNumber> curvature=default,BufferOffset curvatureSide=0,Span<IntervalNumber> supportParameter=default,BufferOffset parameterSide=0,
        Span<IntervalNumber> captureWitness=default,ReadOnlySpan<IntervalNumber> requireWitness=default,BufferOffset rootChart=-1,
        Span<double> anchorContacts=default,Span<IntervalNumber> acceleration=default,KernelVector3 accelerationNormal=default,
        Span<BufferOffset> captureCharts=default,ReadOnlySpan<BufferOffset> requireCharts=default)
    {
        Span<TaylorJet> snapshot=stackalloc TaylorJet[MaxVariables];Span<Occurrence> occurrences=stackalloc Occurrence[MaxOccurrences];Span<BufferCount> counts=stackalloc BufferCount[2];
        Span<KernelVector3> point=stackalloc KernelVector3[2];SurfaceDerivativeLayout.TryCreate(1,0,out var layout);var chartRange=Chart(program,spine);var u=Math.Clamp(domain.U0+(domain.U1-domain.U0)*.5,chartRange[0].Parameter,chartRange[^1].Parameter);
        // A fixed root chart sets the interval gauge. When the domain meets
        // that cell, take the point preconditioner from the same cell rather
        // than from a neighbour that a boundary-straddling midpoint selects.
        if(rootChart>=0&&rootChart<chartRange.Length-1&&domain.U0<=chartRange[rootChart+1].Parameter&&domain.U1>=chartRange[rootChart].Parameter)
            u=Math.Clamp(u,chartRange[rootChart].Parameter,Math.BitDecrement(chartRange[rootChart+1].Parameter));
        var status=EvaluateCore(program,spine,-1,u,0,layout,point,false,default,out _,out _,snapshot:snapshot,nodeSnapshot:occurrences,counts:counts);
        if(status!=AlgorithmStatus.Success)return status==AlgorithmStatus.Unsupported?status:AlgorithmStatus.NotConverged;
        var n=counts[0];var nodes=occurrences[..counts[1]];var t=new IntervalNumber(domain.U0,domain.U1);
        var cartesianSpline=captureWitness.IsEmpty&&requireWitness.IsEmpty&&CartesianSplineRows(program,nodes,n);
        var substitutedConic=captureWitness.IsEmpty&&requireWitness.IsEmpty&&ConicConstraintSubstitution(program,nodes,n);
        Span<BufferOffset> fixedCharts=stackalloc BufferOffset[MaxOccurrences];
        for(var i=0;i<nodes.Length;i++)
        {
            if(i==0&&rootChart>=0){fixedCharts[i]=rootChart;continue;}
            var chart=Chart(program,nodes[i].Spine);var parameter=i==0?u:snapshot[nodes[i].Offset+3][0];var index=0;while(index<chart.Length-2&&chart[index+1].Parameter<=parameter)index++;fixedCharts[i]=index;
        }
        // A shared-root witness is only meaningful for one equation system:
        // every nested occurrence must use the same spine and CHART cell.
        if(!captureCharts.IsEmpty)
        {
            if(captureCharts.Length!=2*nodes.Length)return AlgorithmStatus.Unsupported;
            for(var i=0;i<nodes.Length;i++){captureCharts[2*i]=nodes[i].Spine;captureCharts[2*i+1]=fixedCharts[i];}
        }
        if(!requireCharts.IsEmpty)
        {
            if(requireCharts.Length!=2*nodes.Length)return AlgorithmStatus.Unsupported;
            for(var i=1;i<nodes.Length;i++)
                if(requireCharts[2*i]!=nodes[i].Spine||requireCharts[2*i+1]!=fixedCharts[i])return AlgorithmStatus.NotConverged;
        }
        Span<double> state=stackalloc double[MaxVariables],jacobian=stackalloc double[MaxVariables*MaxVariables],inverse=stackalloc double[MaxVariables*MaxVariables],rhs=stackalloc double[MaxVariables],residual=stackalloc double[MaxVariables];Span<int> pivots=stackalloc int[MaxVariables];
        Span<IntervalNumber> boxes=stackalloc IntervalNumber[MaxVariables],image=stackalloc IntervalNumber[MaxVariables],centerF=stackalloc IntervalNumber[MaxVariables];
        Span<IntervalNumber> intervalJacobian=stackalloc IntervalNumber[MaxVariables*MaxVariables],remainder=stackalloc IntervalNumber[MaxVariables*MaxVariables];
        Span<IntervalDual> variables=stackalloc IntervalDual[MaxVariables],equations=stackalloc IntervalDual[MaxVariables];
        for(var i=0;i<n;i++){state[i]=snapshot[i][0];boxes[i]=state[i];}
        if(substitutedConic)
        {
            status=ConicSubstitutionJacobian(program,nodes,boxes[..n],t,intervalJacobian[..(n*n)],variables,equations,fixedCharts);
            if(status!=AlgorithmStatus.Success)return status;
            status=ConicBlockInverse(intervalJacobian[..(n*n)],inverse[..(n*n)]);if(status!=AlgorithmStatus.Success)return status;
        }
        else
        {
            status=Assemble(program,nodes,state[..n],u,residual[..n],jacobian[..(n*n)],false,default);if(status!=AlgorithmStatus.Success)return status;
            status=SmallLinearSolve.LuFactorize(jacobian[..(n*n)],n,pivots);if(status!=AlgorithmStatus.Success)return AlgorithmStatus.NotConverged;
            for(var column=0;column<n;column++)
            {
                rhs[..n].Clear();rhs[column]=1;status=SmallLinearSolve.LuSolveInPlace(jacobian,n,pivots,rhs[..n]);if(status!=AlgorithmStatus.Success)return status;
                for(var row=0;row<n;row++)inverse[row*n+column]=rhs[row];
            }
            if(cartesianSpline){status=CartesianSplineInverse(program,nodes,state[..n],inverse[..(n*n)]);if(status!=AlgorithmStatus.Success)return status;}
        }
        Span<IntervalSplineContact> splineCache=stackalloc IntervalSplineContact[MaxVariables];
        var recordCapacity=!substitutedConic&&CanRecordJacobian(program,nodes,n)?64+512*nodes.Length:0;
        Span<IntervalExpressionNode> recordNodes=stackalloc IntervalExpressionNode[recordCapacity];
        Span<IntervalNumber> recordDerivatives=stackalloc IntervalNumber[recordCapacity*n];
        Span<BufferOffset> recordOutputs=stackalloc BufferOffset[MaxVariables];
        var jacobianRecord=new IntervalJacobianRecord(recordNodes,recordDerivatives,recordOutputs[..n]);
        if(!substitutedConic)
        {
            for(var i=0;i<n;i++)variables[i]=new(state[i],0);
            status=cartesianSpline?CartesianSplineResidual(program,nodes,variables[..n],t,equations[..n],fixedCharts,default)
                :IntervalResidual(program,nodes,variables[..n],t,equations[..n],fixedCharts);if(status!=AlgorithmStatus.Success)return status;
        }
        for(var i=0;i<n;i++)centerF[i]=equations[i].Value;
        var certified=false;var derivativeScale=0.0;for(var i=0;i<n;i++)derivativeScale=Math.Max(derivativeScale,Math.Abs(snapshot[i][1]));
        for(var expansion=0;expansion<6&&!certified;expansion++)
        {
            var factor=Math.ScaleB(1,expansion);
            for(var i=0;i<n;i++)
            {
                var radius=factor*(2*Math.Max(Math.Abs(snapshot[i][1]),.1*derivativeScale)*Math.Max(Math.Abs(domain.U1-u),Math.Abs(domain.U0-u))+1e-10*(1+Math.Abs(state[i])));
                boxes[i]=new(Math.BitDecrement(state[i]-radius),Math.BitIncrement(state[i]+radius));
            }
            status=IntervalJacobianWithRecord(program,nodes,boxes[..n],t,intervalJacobian[..(n*n)],variables,equations,fixedCharts,splineCache,ref jacobianRecord,cartesianSpline,substitutedConic);if(status!=AlgorithmStatus.Success)continue;
            if(substitutedConic)ConicBlockPrecondition(inverse[..(n*n)],intervalJacobian[..(n*n)],remainder[..(n*n)]);
            else Precondition(inverse[..(n*n)],intervalJacobian[..(n*n)],n,remainder);
            certified=true;
            for(var row=0;row<n;row++)
            {
                IntervalNumber value=state[row];for(var k=0;k<n;k++)value-=inverse[row*n+k]*centerF[k];
                for(var k=0;k<n;k++)value+=remainder[row*n+k]*(boxes[k]-state[k]);image[row]=value;
                certified&=value.Lower>boxes[row].Lower&&value.Upper<boxes[row].Upper;
                var contraction=0.0;for(var k=0;k<n;k++)contraction+=remainder[row*n+k].Magnitude*(boxes[k].Upper-boxes[k].Lower)/Math.Max(boxes[row].Upper-boxes[row].Lower,1e-300);certified&=contraction<.9;
            }
        }
        if(!certified)return AlgorithmStatus.NotConverged;
        if(!requireWitness.IsEmpty)
        {
            if(n!=requireWitness.Length)return AlgorithmStatus.Unsupported;
            for(var i=0;i<n;i++)
                if(!requireWitness[i].Finite||requireWitness[i].Lower<boxes[i].Lower||requireWitness[i].Upper>boxes[i].Upper)
                    return AlgorithmStatus.NotConverged;
        }
        for(var iteration=0;iteration<6;iteration++)
        {
            for(var i=0;i<n;i++)boxes[i]=new(Math.Max(boxes[i].Lower,image[i].Lower),Math.Min(boxes[i].Upper,image[i].Upper));
            status=IntervalJacobianWithRecord(program,nodes,boxes[..n],t,intervalJacobian[..(n*n)],variables,equations,fixedCharts,splineCache,ref jacobianRecord,cartesianSpline,substitutedConic);if(status!=AlgorithmStatus.Success)return status;
            if(substitutedConic)ConicBlockPrecondition(inverse[..(n*n)],intervalJacobian[..(n*n)],remainder[..(n*n)]);
            else Precondition(inverse[..(n*n)],intervalJacobian[..(n*n)],n,remainder);
            for(var row=0;row<n;row++)
            {
                IntervalNumber value=state[row];for(var k=0;k<n;k++)value-=inverse[row*n+k]*centerF[k];
                for(var k=0;k<n;k++)value+=remainder[row*n+k]*(boxes[k]-state[k]);image[row]=value;
            }
        }
        if(!ReuseSplineContactBounds)splineCache.Clear();
        for(var i=0;i<n;i++)variables[i]=new(boxes[i],0);
        status=substitutedConic?ConicBlockNearest(intervalJacobian[..(n*n)])
            :CertifyNearestBoxes(program,nodes,boxes[..n],variables[..n],intervalJacobian[..(n*n)],fixedCharts,splineCache);if(status!=AlgorithmStatus.Success)return status;
        if(!captureWitness.IsEmpty)
        {
            if(n!=captureWitness.Length)return AlgorithmStatus.Unsupported;
            boxes[..n].CopyTo(captureWitness);
        }
        var parameterIndex=-1;BufferOffset parameterChild=-1;
        if(!supportParameter.IsEmpty)
        {
            parameterChild=parameterSide==0?nodes[0].FirstChild:nodes[0].SecondChild;
            if(parameterChild<0)return AlgorithmStatus.Unsupported;
            parameterIndex=nodes[parameterChild].Offset+3;supportParameter[0]=boxes[parameterIndex];
        }
        if(!reachBudget.IsEmpty)
        {
            if(nodes.Length!=1)return AlgorithmStatus.Unsupported;
            var node=nodes[0];var sources=program.Spines[node.Spine];var centers=new IntervalBox3(boxes[0].Lower,boxes[0].Upper,boxes[1].Lower,boxes[1].Upper,boxes[2].Lower,boxes[2].Upper);
            for(var side=0;side<2;side++)
            {
                var source=program.Surfaces[side==0?sources.First:sources.Second];
                if(source.IsBlend)return AlgorithmStatus.Unsupported;
                if(source.IsBSpline)
                {
                    var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0)return AlgorithmStatus.Unsupported;
                    var feet=new SurfaceParameterBox(boxes[uv].Lower,boxes[uv].Upper,boxes[uv+1].Lower,boxes[uv+1].Upper);
                    status=RollingBallReach.CertifySpline(program.Spline(source),centers,feet,ref reachBudget[0],reachBudget[1],out _);
                }
                else status=RollingBallReach.CertifyPlane(source.Analytic,out _);
                if(status!=AlgorithmStatus.Success)return status;
            }
        }
        status=substitutedConic?ConicSubstitutionResidual(program,nodes,variables[..n],new(t,1),equations[..n],fixedCharts,out _,out _,out _)
            :cartesianSpline?CartesianSplineResidual(program,nodes,variables[..n],new(t,1),equations[..n],fixedCharts,splineCache)
            :IntervalResidual(program,nodes,variables[..n],new(t,1),equations[..n],fixedCharts,splineCache);if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<n;i++)image[i]=-equations[i].Derivative;
        status=substitutedConic?ConicBlockSolve(intervalJacobian[..(n*n)],image[..n])
            :SolveIntervals(intervalJacobian[..(n*n)],image[..n],n);if(status!=AlgorithmStatus.Success)return status;
        if(parameterIndex>=0)
        {
            supportParameter[1]=image[parameterIndex];
            if(supportParameter.Length==2)return AlgorithmStatus.Success;
            for(var i=0;i<n;i++)variables[i]=new(boxes[i],image[i]);
            var root=program.Spines[spine];var support=program.Surfaces[parameterSide==0?root.First:root.Second];
            status=BoundSpineSupportSection(program,nodes,variables[..n],parameterChild,support,splineCache,out var section);
            if(status!=AlgorithmStatus.Success)return status;
            supportParameter[2]=section.Value;supportParameter[3]=section.Derivative;
            return AlgorithmStatus.Success;
        }
        if(!curvature.IsEmpty||!acceleration.IsEmpty)
        {
            for(var i=0;i<n;i++)variables[i]=new(boxes[i],image[i]);
            var root=program.Spines[spine];var first=curvatureSide==0?root.First:root.Second;var second=curvatureSide==0?root.Second:root.First;
            status=IntervalNormal(program,first,nodes,0,variables[..n],out _,out var firstNormal,splineCache);if(status!=AlgorithmStatus.Success)return status;
            status=IntervalNormal(program,second,nodes,0,variables[..n],out _,out var secondNormal,splineCache);if(status!=AlgorithmStatus.Success)return status;
            var a=new IntervalVector(firstNormal.X.Value,firstNormal.Y.Value,firstNormal.Z.Value);
            var b=new IntervalVector(secondNormal.X.Value,secondNormal.Y.Value,secondNormal.Z.Value);
            var da=new IntervalVector(firstNormal.X.Derivative,firstNormal.Y.Derivative,firstNormal.Z.Derivative);
            var db=new IntervalVector(secondNormal.X.Derivative,secondNormal.Y.Derivative,secondNormal.Z.Derivative);
            var velocity=new IntervalVector(image[0],image[1],image[2]);
            if(!acceleration.IsEmpty)
            {
                // The two surface constraints give n·p'' = -n'·p'.
                // On this native CHART cell the gauge is affine in XYZ,
                // so its chord supplies the third row with zero RHS.
                var chord=new IntervalVector(chartRange[fixedCharts[0]].Chord);
                var bc=IntervalVector.Cross(b,chord);var ca=IntervalVector.Cross(chord,a);
                var determinant=IntervalVector.Dot(a,bc).Value;
                if(!determinant.Finite||determinant.ContainsZero)return AlgorithmStatus.NotConverged;
                var secondDerivative=(bc*(-IntervalVector.Dot(da,velocity).Value)+ca*(-IntervalVector.Dot(db,velocity).Value))/determinant;
                acceleration[0]=IntervalVector.Dot(secondDerivative,new IntervalVector(accelerationNormal)).Value;
                return acceleration[0].Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
            }
            var speed=velocity.Norm().Value;
            if(speed.Lower<=0)return AlgorithmStatus.NotConverged;var tangent=velocity/speed;
            var ha=-IntervalVector.Dot(da,velocity).Value/(speed*speed);var hb=-IntervalVector.Dot(db,velocity).Value/(speed*speed);
            var aa=IntervalVector.Dot(a,a).Value;var denominator=IntervalVector.Dot(b,IntervalVector.Cross(a,tangent)).Value;
            if(aa.Lower<=0||denominator.ContainsZero)return AlgorithmStatus.NotConverged;
            var alpha=ha/aa;
            curvature[0]=(hb-alpha*IntervalVector.Dot(b,a).Value)/denominator*a.Norm().Value;
            return curvature[0].Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
        }
        if(surface<0)
        {
            if(!anchorContacts.IsEmpty)
            {
                for(var i=0;i<n;i++)snapshot[i]=new(boxes[i].Midpoint,0);
                for(var side=0;side<2;side++)
                {
                    status=SpineSupportParameters(program,nodes,snapshot[..n],side,u,out anchorContacts[2*side],out anchorContacts[2*side+1]);
                    if(status!=AlgorithmStatus.Success)return status;
                }
            }
            for(var i=0;i<3;i++){output[i]=boxes[i];output[3+i]=image[i];output[6+i]=0;}return AlgorithmStatus.Success;
        }
        for(var i=0;i<n;i++)variables[i]=new(boxes[i],image[i]);
        var v=new IntervalNumber(domain.V0,domain.V1);
        status=IntervalFrame(program,surface,nodes,variables[..n],v,out var frame,splineCache);if(status!=AlgorithmStatus.Success)return status;
        output[0]=frame.X.Value;output[1]=frame.Y.Value;output[2]=frame.Z.Value;output[3]=frame.X.Derivative;output[4]=frame.Y.Derivative;output[5]=frame.Z.Derivative;
        for(var i=0;i<n;i++)variables[i]=new(boxes[i],0);
        status=IntervalFrame(program,surface,nodes,variables[..n],new(v,1),out frame,splineCache);if(status!=AlgorithmStatus.Success)return status;
        output[6]=frame.X.Derivative;output[7]=frame.Y.Derivative;output[8]=frame.Z.Derivative;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus CertifyNearestBoxes(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,ReadOnlySpan<IntervalNumber> boxes,
        ReadOnlySpan<IntervalDual> variables,ReadOnlySpan<IntervalNumber> jacobian,ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<IntervalSplineContact> cache)
    {
        Span<BufferOffset> map=stackalloc BufferOffset[MaxVariables];Span<bool> selected=stackalloc bool[MaxOccurrences];Span<IntervalNumber> block=stackalloc IntervalNumber[MaxVariables*MaxVariables],rhs=stackalloc IntervalNumber[MaxVariables];
        var n=boxes.Length;
        for(var root=1;root<nodes.Length;root++)
        {
            selected.Clear();selected[root]=true;var count=0;var parameterIndex=-1;
            for(var i=root;i<nodes.Length;i++)
            {
                if(i!=root)selected[i]=selected[nodes[i].Parent];if(!selected[i])continue;var node=nodes[i];
                for(var j=0;j<4;j++){if(i==root&&j==3)parameterIndex=count;map[count++]=node.Offset+j;}
                if(node.FirstUv>=0){map[count++]=node.FirstUv;map[count++]=node.FirstUv+1;}
                if(node.SecondUv>=0&&node.SecondUv!=node.FirstUv){map[count++]=node.SecondUv;map[count++]=node.SecondUv+1;}
            }
            for(var i=0;i<count;i++)for(var j=0;j<count;j++)block[i*count+j]=jacobian[map[i]*n+map[j]];
            rhs[..count].Clear();rhs[parameterIndex]=1;var status=SolveIntervals(block[..(count*count)],rhs[..count],count);if(status!=AlgorithmStatus.Success)return status;
            var spine=program.Spines[nodes[root].Spine];
            if(SpineParameterMap.Explicit(spine)){if(rhs[parameterIndex].Upper>=0)return AlgorithmStatus.NotConverged;continue;}
            status=IntervalNormal(program,spine.First,nodes,root,variables,out _,out var a,cache);if(status!=AlgorithmStatus.Success)return status;
            status=IntervalNormal(program,spine.Second,nodes,root,variables,out _,out var b,cache);if(status!=AlgorithmStatus.Success)return status;
            status=SpineParameterMap.Gradient(program.Spines[nodes[root].Spine],Chart(program,nodes[root].Spine)[charts[root]],variables[nodes[root].Offset+3],out var gauge);
            if(status!=AlgorithmStatus.Success)return status;
            var orientation=IntervalVector.Dot(IntervalVector.Cross(a,b),gauge).Value;
            if((rhs[parameterIndex]*orientation).Upper>=0)return AlgorithmStatus.NotConverged;
        }
        foreach(var node in nodes)for(var side=0;side<2;side++)
        {
            var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0||side==1&&uv==node.FirstUv)continue;var spine=program.Spines[node.Spine];var source=program.Surfaces[side==0?spine.First:spine.Second];
            var status=IntervalSpline(program,source,new(boxes[uv],1),new(boxes[uv+1],0),out var foot,out var su,out var sv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
            var delta=IntervalPoint(variables,node.Offset)-foot;
            var a=IntervalDual.Square(su.X).Value+IntervalDual.Square(su.Y).Value+IntervalDual.Square(su.Z).Value-(delta.X.Value*su.X.Derivative+delta.Y.Value*su.Y.Derivative+delta.Z.Value*su.Z.Derivative);
            var b=IntervalVector.Dot(su,sv).Value-(delta.X.Value*sv.X.Derivative+delta.Y.Value*sv.Y.Derivative+delta.Z.Value*sv.Z.Derivative);
            status=IntervalSpline(program,source,new(boxes[uv],0),new(boxes[uv+1],1),out _,out _,out var vv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
            var d=IntervalDual.Square(sv.X).Value+IntervalDual.Square(sv.Y).Value+IntervalDual.Square(sv.Z).Value-(delta.X.Value*vv.X.Derivative+delta.Y.Value*vv.Y.Derivative+delta.Z.Value*vv.Z.Derivative);
            if(a.Lower<=0||d.Lower<=0||(a*d-IntervalNumber.Square(b)).Lower<=0)return AlgorithmStatus.NotConverged;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntervalJacobian(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,ReadOnlySpan<IntervalNumber> boxes,
        IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> fixedCharts,Span<IntervalSplineContact> cache,bool cartesianSpline=false)
    {
        var n=boxes.Length;ModelingPerformance.Jacobian(n);for(var i=0;i<n;i++)variables[i]=new(boxes[i],0);
        if(HasAnyUv(nodes))cache.Clear();
        foreach(var node in nodes)for(var side=0;side<2;side++)
        {
            var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0)continue;var spine=program.Spines[node.Spine];var source=program.Surfaces[side==0?spine.First:spine.Second];
            if(SplineContactMatches(cache[uv],source,boxes[uv],boxes[uv+1]))continue;
            var status=CacheSplineContact(program,source,boxes[uv],boxes[uv+1],ref cache[uv]);if(status!=AlgorithmStatus.Success)return status;
        }
        var firstColumn=0;
        if(cartesianSpline)
        {
            var status=CartesianSplinePointColumns(program,nodes,variables[..n],parameter,equations[..n],fixedCharts,cache,matrix);
            if(status!=AlgorithmStatus.Success)return status;firstColumn=3;
        }
        else if(PlaneSplinePointColumns(program,nodes,boxes,parameter,fixedCharts,cache,matrix,out var pointStatus))
        {
            if(pointStatus!=AlgorithmStatus.Success)return pointStatus;firstColumn=3;
        }
        for(var column=firstColumn;column<n;column++)
        {
            ModelingPerformance.ResidualDirection();
            variables[column]=new(boxes[column],1);
            var status=cartesianSpline?CartesianSplineResidual(program,nodes,variables[..n],parameter,equations[..n],fixedCharts,cache)
                :IntervalResidual(program,nodes,variables[..n],parameter,equations[..n],fixedCharts,cache);if(status!=AlgorithmStatus.Success)return status;
            for(var row=0;row<n;row++)matrix[row*n+column]=equations[row].Derivative;variables[column]=new(boxes[column],0);
        }
        return AlgorithmStatus.Success;
    }
    private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
    {
        foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return true;
        return false;
    }
    private static void Precondition(ReadOnlySpan<double> inverse,ReadOnlySpan<IntervalNumber> jacobian,BufferCount n,Span<IntervalNumber> result)
    {
        for(var row=0;row<n;row++)for(var column=0;column<n;column++)
        {IntervalNumber value=row==column?1:0;for(var k=0;k<n;k++)value-=inverse[row*n+k]*jacobian[k*n+column];result[row*n+column]=value;}
    }
    private static AlgorithmStatus SolveIntervals(Span<IntervalNumber> matrix,Span<IntervalNumber> rhs,BufferCount n)
    {
        for(var k=0;k<n;k++)
        {
            var pivot=k;for(var i=k+1;i<n;i++)if(Math.Abs(matrix[i*n+k].Midpoint)>Math.Abs(matrix[pivot*n+k].Midpoint))pivot=i;
            if(matrix[pivot*n+k].ContainsZero)return AlgorithmStatus.NotConverged;
            if(pivot!=k){for(var j=k;j<n;j++)(matrix[k*n+j],matrix[pivot*n+j])=(matrix[pivot*n+j],matrix[k*n+j]);(rhs[k],rhs[pivot])=(rhs[pivot],rhs[k]);}
            for(var i=k+1;i<n;i++){var factor=matrix[i*n+k]/matrix[k*n+k];for(var j=k+1;j<n;j++)matrix[i*n+j]-=factor*matrix[k*n+j];rhs[i]-=factor*rhs[k];}
        }
        for(var i=n-1;i>=0;i--){for(var j=i+1;j<n;j++)rhs[i]-=matrix[i*n+j]*rhs[j];rhs[i]/=matrix[i*n+i];if(!rhs[i].Finite)return AlgorithmStatus.NotConverged;}
        return AlgorithmStatus.Success;
    }
    private static AlgorithmStatus IntervalFrame(in NestedBlendProgram program,BlendSurfaceIndex surface,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalDual> variables,IntervalDual v,out IntervalVector point,ReadOnlySpan<IntervalSplineContact> cache=default)
    {
        point=default;var root=program.Surfaces[surface];var status=IntervalNormal(program,root.First,nodes,0,variables,out _,out var n0,cache);if(status!=AlgorithmStatus.Success)return status;
        status=IntervalNormal(program,root.Second,nodes,0,variables,out _,out var n1,cache);if(status!=AlgorithmStatus.Success)return status;
        var radius=Math.Abs(root.Range0);var x=n1*(-root.Range1/radius);var end=n0*(-root.Range0/radius);if(root.ReverseSection)(x,end)=(end,x);
        status=RollingSectionEvaluation.Bound(x,end,v,out var radial);if(status!=AlgorithmStatus.Success)return status;
        point=IntervalPoint(variables,0)+radial*radius;return point.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }
}
