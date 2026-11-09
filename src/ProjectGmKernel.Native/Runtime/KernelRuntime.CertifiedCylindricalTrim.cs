using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A ray in either axial direction starts outside if it cannot
    // meet any trim. Prove that for the whole isolated spatial root box.
    // A ray that can meet a trim is left unresolved; this is not a winding
    // approximation and does not classify roots in the interior.
    private static AlgorithmStatus CertifyCylindricalRootOutsideTrim(in RegularBoundaryView input,BufferOffset face,
        in SurfaceRecord surface,in IntervalBox3 root,double precision,bool increasing=true)
    {
        var record=input.Faces[face];
        if(record.LoopCount==0||!TryPrepareSurface(surface,out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder)
            return AlgorithmStatus.Unsupported;
        var a=new IntervalVector(cylinder.Axis);var x=new IntervalVector(cylinder.X);var y=new IntervalVector(cylinder.Y);
        // Dual frame, without its common positive denominator. This uses
        // the represented frame, including a rounded or tilted axis.
        var u=IntervalVector.Cross(y,a);var v=IntervalVector.Cross(a,x);var h=IntervalVector.Cross(x,y);
        if(!(IntervalVector.Dot(h,a).Value.Lower>0))return AlgorithmStatus.Unsupported;
        var origin=new IntervalVector(cylinder.Origin);
        var p=Box(root,0)-origin;var pu=IntervalVector.Dot(p,u).Value;
        var pv=IntervalVector.Dot(p,v).Value;var ph=IntervalVector.Dot(p,h).Value;
        if(!pu.Finite||!pv.Finite||!ph.Finite||pu.ContainsZero&&pv.ContainsZero)return AlgorithmStatus.NotConverged;
        var margin=2*new IntervalNumber(-precision,precision);
        Span<IntervalNumber> pending=stackalloc IntervalNumber[32];
        var visited=0;
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            if(loop.CoedgeCount==0)return AlgorithmStatus.Unsupported;
            foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                var edge=input.Edges[use.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];
                if(curve.Class!=CurveClass.ICurve&&!TryReadExplicitSpineCurve(curve,out _))return AlgorithmStatus.Unsupported;
                var n=1;pending[0]=new(edge.Start,edge.End);
                while(n>0)
                {
                    var t=pending[--n];
                    if(++visited>512){ModelingPerformance.BudgetFailure();return AlgorithmStatus.NotConverged;}
                    if(TryFiniteCarrierExtent(new(){Curve=edge.Curve,First=t.Lower,Last=t.Upper},curve,out var bound))
                    {
                        var q=Box(bound,margin)-origin;
                        var qh=IntervalVector.Dot(q,h).Value;
                        if(increasing?qh.Upper<ph.Lower:qh.Lower>ph.Upper)continue;
                        var qu=IntervalVector.Dot(q,u).Value;var qv=IntervalVector.Dot(q,v).Value;
                        var cross=pu*qv-pv*qu;var radial=pu*qu+pv*qv;
                        if(cross.Finite&&!cross.ContainsZero||radial.Upper<0)continue;
                    }
                    var mid=t.Midpoint;
                    if(!(mid>t.Lower&&mid<t.Upper)||n+2>pending.Length)return AlgorithmStatus.NotConverged;
                    pending[n++]=new(t.Lower,mid);pending[n++]=new(mid,t.Upper);
                }
            }
        }
        return AlgorithmStatus.Success;

        static IntervalVector Box(in IntervalBox3 b,IntervalNumber radius)
            =>new(new IntervalNumber(b.XLo,b.XHi)+radius,new IntervalNumber(b.YLo,b.YHi)+radius,new IntervalNumber(b.ZLo,b.ZHi)+radius);
    }
}
