using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    [InlineArray(18)] private struct IntervalSplineValues{private IntervalNumber value;}
    private struct IntervalSplineContact
    {
        internal bool Ready;
        internal BufferOffset ScalarOffset;
        internal SplineDegree UDegree,VDegree;
        internal BufferCount UCount,VCount,Dimension;
        internal bool Rational,UPeriodic,VPeriodic;
        internal IntervalNumber U,V;
        internal IntervalSplineValues Values;
    }

    private static bool SplineContactMatches(in IntervalSplineContact contact,in BlendSurfaceInstruction source,IntervalNumber u,IntervalNumber v)
        =>contact.Ready&&contact.U.Lower==u.Lower&&contact.U.Upper==u.Upper&&contact.V.Lower==v.Lower&&contact.V.Upper==v.Upper
            &&contact.ScalarOffset==source.ScalarOffset&&contact.UDegree==source.UDegree&&contact.VDegree==source.VDegree
            &&contact.UCount==source.UCount&&contact.VCount==source.VCount&&contact.Dimension==source.Dimension
            &&contact.Rational==source.Rational&&contact.UPeriodic==source.UPeriodic&&contact.VPeriodic==source.VPeriodic;

    private static AlgorithmStatus CacheSplineContact(in NestedBlendProgram program,in BlendSurfaceInstruction source,IntervalNumber u,IntervalNumber v,ref IntervalSplineContact contact)
    {
        var status=IntervalSplineBounds(program,source,u,v,contact.Values);if(status!=AlgorithmStatus.Success)return status;
        contact.U=u;contact.V=v;contact.ScalarOffset=source.ScalarOffset;contact.UDegree=source.UDegree;contact.VDegree=source.VDegree;
        contact.UCount=source.UCount;contact.VCount=source.VCount;contact.Dimension=source.Dimension;contact.Rational=source.Rational;
        contact.UPeriodic=source.UPeriodic;contact.VPeriodic=source.VPeriodic;contact.Ready=true;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntervalNormal(in NestedBlendProgram program,BlendSurfaceIndex surface,ReadOnlySpan<Occurrence> nodes,
        BufferOffset occurrence,ReadOnlySpan<IntervalDual> variables,out IntervalDual distance,out IntervalVector normal,ReadOnlySpan<IntervalSplineContact> cache=default)
    {
        distance=default;normal=default;var node=program.Surfaces[surface];var point=IntervalPoint(variables,nodes[occurrence].Offset);
        if(node.IsBSpline)
        {
            var uv=SplineUv(program,node,nodes[occurrence]);if(uv<0)return AlgorithmStatus.Unsupported;
            var status=IntervalSpline(program,node,variables[uv],variables[uv+1],out var foot,out var su,out var sv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
            normal=IntervalVector.Cross(su,sv).Unit()*node.NormalSense;distance=IntervalVector.Dot(point-foot,normal)-node.Offset*node.NormalSense;
        }
        else if(node.IsBlend)
        {
            var a=nodes[occurrence].FirstChild;var b=nodes[occurrence].SecondChild;
            var child=a>=0&&nodes[a].Spine==node.Spine?a:b>=0&&nodes[b].Spine==node.Spine?b:-1;if(child<0)return AlgorithmStatus.Unsupported;
            var offset=point-IntervalPoint(variables,nodes[child].Offset);var length=offset.Norm();normal=offset/length*node.NormalSense;distance=(length-Math.Abs(node.Range0))*node.NormalSense;
        }
        else
        {
            var s=node.Analytic;var relative=point-new IntervalVector(s.Origin);var axis=new IntervalVector(s.Axis);var z=IntervalVector.Dot(relative,axis);
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
                        if((s.Radius+s.Secondary*z.Value).Lower<=0)return AlgorithmStatus.NotConverged;
                        var factor=1/Math.Sqrt(1+s.Secondary*s.Secondary);distance=(rho-s.Radius-z*s.Secondary)*factor;normal=(unit-axis*s.Secondary)*factor;
                    }
                    else
                    {
                        if(!(s.Radius>s.Secondary&&s.Secondary>0))return AlgorithmStatus.Unsupported;
                        var delta=rho-s.Radius;var profile=IntervalDual.Sqrt(IntervalDual.Square(delta)+IntervalDual.Square(z));distance=profile-s.Secondary;normal=(unit*delta+axis*z)/profile;
                    }
                    break;
                default:return AlgorithmStatus.Unsupported;
            }
            distance*=node.NormalSense;normal=normal*node.NormalSense;
        }
        return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus IntervalSpline(in NestedBlendProgram program,in BlendSurfaceInstruction node,IntervalDual u,IntervalDual v,
        out IntervalVector point,out IntervalVector du,out IntervalVector dv,ReadOnlySpan<IntervalSplineContact> cache=default,BufferOffset cacheIndex=-1)
    {
        point=du=dv=default;Span<IntervalNumber> local=stackalloc IntervalNumber[18];scoped ReadOnlySpan<IntervalNumber> hull;
        if(cacheIndex>=0&&!cache.IsEmpty&&SplineContactMatches(cache[cacheIndex],node,u.Value,v.Value))hull=cache[cacheIndex].Values;
        else{var status=IntervalSplineBounds(program,node,u.Value,v.Value,local);if(status!=AlgorithmStatus.Success)return status;hull=local;}
        point=IntervalSplineVector(hull,0,3,6,u,v);du=IntervalSplineVector(hull,3,9,12,u,v);dv=IntervalSplineVector(hull,6,12,15,u,v);
        return point.Finite&&du.Finite&&dv.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestSplineContactReuse(in NestedBlendProgram program,in BlendSurfaceInstruction cachedSource,in BlendSurfaceInstruction source,
        IntervalNumber cachedU,IntervalNumber cachedV,IntervalDual u,IntervalDual v,bool reuse,
        out IntervalVector point,out IntervalVector du,out IntervalVector dv,out bool hit)
    {
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[1];cache.Clear();point=du=dv=default;hit=false;
        var status=CacheSplineContact(program,cachedSource,cachedU,cachedV,ref cache[0]);if(status!=AlgorithmStatus.Success)return status;
        hit=reuse&&SplineContactMatches(cache[0],source,u.Value,v.Value);if(!reuse)cache.Clear();
        return IntervalSpline(program,source,u,v,out point,out du,out dv,cache,0);
    }
#endif

    internal static AlgorithmStatus IntervalSplineBounds(in NestedBlendProgram program,in BlendSurfaceInstruction node,IntervalNumber u,IntervalNumber v,Span<IntervalNumber> hull)
    {
        var source=program.Spline(node);Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],local=stackalloc IntervalNumber[27];var found=false;
        var uPeriod=source.UEnd-source.UStart;var vPeriod=source.VEnd-source.VStart;
        var uShift=source.UPeriodic?Math.Floor((u.Midpoint-source.UStart)/uPeriod):0;var vShift=source.VPeriodic?Math.Floor((v.Midpoint-source.VStart)/vPeriod):0;
        if(Math.Abs(uShift)>1e14||Math.Abs(vShift)>1e14)return AlgorithmStatus.NotConverged;
        for(var us=uShift-(source.UPeriodic?1:0);us<=uShift+(source.UPeriodic?1:0);us++)for(var vs=vShift-(source.VPeriodic?1:0);vs<=vShift+(source.VPeriodic?1:0);vs++)
        for(var ue=source.UPeriodic?0:-1;ue<=(source.UPeriodic?0:1);ue++)for(var ve=source.VPeriodic?0:-1;ve<=(source.VPeriodic?0:1);ve++)
        for(var i=source.UDegree;i<source.UVertexCount;i++)for(var j=source.VDegree;j<source.VVertexCount;j++)
        {
            var a=source.UKnots[i];var b=source.UKnots[i+1];var c=source.VKnots[j];var d=source.VKnots[j+1];if(!(b>a&&d>c))continue;
            if(!SplineRegion(u-us*uPeriod,source.UStart,source.UEnd,ue,out var up,out var dx)||!SplineRegion(v-vs*vPeriod,source.VStart,source.VEnd,ve,out var vp,out var dy))continue;
            var lo=Math.Max(a,up.Lower);var hi=Math.Min(b,up.Upper);var low=Math.Max(c,vp.Lower);var high=Math.Min(d,vp.Upper);
            if(lo>hi||low>high)continue;
            var status=BezierSurfaceBounds.Extract(source,i,j,poles);if(status!=AlgorithmStatus.Success)return status;
            status=BezierSurfaceBounds.BoundRectangularSecond(new(poles[..(4*(source.UDegree+1)*(source.VDegree+1))],source.UDegree,source.VDegree,new(a,b,c,d)),new(lo,hi,low,high),local);if(status!=AlgorithmStatus.Success)return status;
            // Exactly the same tensor-product linear extension as the public
            // B-surface evaluator; polynomial extrapolation would be wrong.
            if(ue!=0)for(var k=0;k<=2;k++)for(var axis=0;axis<3;axis++){local[3*k+axis]+=dx*local[9+3*k+axis];local[18+3*k+axis]=0;}
            if(ve!=0)for(var k=0;k<=2;k++)for(var axis=0;axis<3;axis++){local[9*k+axis]+=dy*local[9*k+3+axis];local[9*k+6+axis]=0;}
            for(var k=0;k<6;k++)
            {
                var at=k==0?0:k==1?9:k==2?3:k==3?18:k==4?12:6;
                for(var axis=0;axis<3;axis++)hull[3*k+axis]=found?IntervalNumber.Hull(hull[3*k+axis],local[at+axis]):local[at+axis];
            }
            found=true;
        }
        return found?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static bool SplineRegion(IntervalNumber input,double start,double end,BufferOffset region,out IntervalNumber clamped,out IntervalNumber delta)
    {
        delta=0;
        if(region<0){clamped=new(start);if(input.Lower>start)return false;delta=new(input.Lower-start,Math.Min(input.Upper,start)-start);return true;}
        if(region>0){clamped=new(end);if(input.Upper<end)return false;delta=new(Math.Max(input.Lower,end)-end,input.Upper-end);return true;}
        clamped=new(Math.Max(input.Lower,start),Math.Min(input.Upper,end));return clamped.Finite;
    }

    private static IntervalVector IntervalSplineVector(ReadOnlySpan<IntervalNumber> jets,BufferOffset p,BufferOffset du,BufferOffset dv,IntervalDual u,IntervalDual v)
        =>new(new(jets[p],jets[du]*u.Derivative+jets[dv]*v.Derivative),new(jets[p+1],jets[du+1]*u.Derivative+jets[dv+1]*v.Derivative),new(jets[p+2],jets[du+2]*u.Derivative+jets[dv+2]*v.Derivative));
    private static IntervalVector IntervalPoint(ReadOnlySpan<IntervalDual> values,BufferOffset offset)=>new(values[offset],values[offset+1],values[offset+2]);

    private static AlgorithmStatus IntervalResidual(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,ReadOnlySpan<IntervalDual> variables,
        IntervalDual parameter,Span<IntervalDual> residual,ReadOnlySpan<BufferOffset> fixedCharts,ReadOnlySpan<IntervalSplineContact> cache=default)
    {
        for(var i=nodes.Length-1;i>=0;i--)
        {
            var node=nodes[i];var spine=program.Spines[node.Spine];var point=IntervalPoint(variables,node.Offset);
            var status=IntervalNormal(program,spine.First,nodes,i,variables,out var a,out var n0,cache);if(status!=AlgorithmStatus.Success)return status;
            status=IntervalNormal(program,spine.Second,nodes,i,variables,out var b,out var n1,cache);if(status!=AlgorithmStatus.Success)return status;
            residual[node.Offset]=a;residual[node.Offset+1]=b;
            for(var side=0;side<2;side++)
            {
                var uv=side==0?node.FirstUv:node.SecondUv;if(uv<0)continue;var support=program.Surfaces[side==0?spine.First:spine.Second];
                status=IntervalSpline(program,support,variables[uv],variables[uv+1],out var foot,out var su,out var sv,cache,uv);if(status!=AlgorithmStatus.Success)return status;
                residual[uv]=IntervalVector.Dot(point-foot,su.Unit());residual[uv+1]=IntervalVector.Dot(point-foot,sv.Unit());
            }
            // Child parameters are an internal gauge. Freeze each child's
            // chart plane while certifying the local implicit tube; switching
            // planes inside a box would invalidate a Jacobian enclosure.
            var t=i==0?parameter:variables[node.Offset+3];var charts=Chart(program,node.Spine);var chart=charts[fixedCharts[i]];
            residual[node.Offset+2]=IntervalVector.Dot(point-new IntervalVector(chart.Position),new IntervalVector(chart.Chord))-(t-chart.Parameter)/chart.Scale;
            if(i>0)residual[node.Offset+3]=IntervalVector.Dot(IntervalPoint(variables,nodes[node.Parent].Offset)-point,IntervalVector.Cross(n0,n1).Unit());
        }
        for(var i=0;i<residual.Length;i++)if(!residual[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }
}
