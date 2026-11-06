using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Geometry.Bounds;

internal static class ParametricPatchSeparation
{
    private struct Cell
    {
        internal SurfaceParameterBox First,Second;
        internal BufferCount Depth;
    }

    // Success proves every pair of points is separated by the requested
    // Cartesian clearance. Overlapping interval boxes are unresolved, never
    // evidence of either a geometric intersection or a successful empty SSI.
    internal static AlgorithmStatus CertifyDisjoint(in SurfaceIntersectionPatch first,in SurfaceIntersectionPatch second,
        in SurfaceParameterBox firstDomain,in SurfaceParameterBox secondDomain,double clearance,ref BufferCount visited,BufferCount limit)
    {
        if(!firstDomain.Valid||!secondDomain.Valid||!double.IsFinite(clearance)||clearance<0)return AlgorithmStatus.InvalidInput;
        Span<Cell> pending=stackalloc Cell[64];var count=1;pending[0]=new(){First=firstDomain,Second=secondDomain};
        Span<IntervalNumber> a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];
        while(count>0)
        {
            if(++visited>limit)return AlgorithmStatus.NotConverged;
            var cell=pending[--count];var sa=first.Bound(cell.First,a);var sb=second.Bound(cell.Second,b);
            if(sa!=AlgorithmStatus.Success&&sa!=AlgorithmStatus.NotConverged)return sa;
            if(sb!=AlgorithmStatus.Success&&sb!=AlgorithmStatus.NotConverged)return sb;
            if(sa==AlgorithmStatus.Success&&sb==AlgorithmStatus.Success)
            {
                var separated=false;
                for(var axis=0;axis<3;axis++)separated|=(a[axis]+clearance).Upper<b[axis].Lower||(b[axis]+clearance).Upper<a[axis].Lower;
                if(separated)continue;
            }
            if(cell.Depth>=48||count+2>pending.Length)return AlgorithmStatus.NotConverged;
            BufferOffset split=0;var size=-1.0;
            for(var axis=0;axis<4;axis++)
            {
                var box=axis<2?cell.First:cell.Second;var status=axis<2?sa:sb;var bounds=axis<2?a:b;
                var width=axis%2==0?box.U1-box.U0:box.V1-box.V0;if(!(width>0))continue;
                // A failed implicit tube must shrink in its spine direction.
                if(status==AlgorithmStatus.NotConverged&&axis%2!=0)continue;
                var estimate=status==AlgorithmStatus.NotConverged?double.PositiveInfinity:
                    width*(bounds[3+3*(axis%2)].Magnitude+bounds[4+3*(axis%2)].Magnitude+bounds[5+3*(axis%2)].Magnitude);
                if(estimate>size){size=estimate;split=axis;}
            }
            if(size<0)return AlgorithmStatus.NotConverged;
            var chosen=split<2?cell.First:cell.Second;var lo=split%2==0?chosen.U0:chosen.V0;var hi=split%2==0?chosen.U1:chosen.V1;
            var mid=lo+(hi-lo)*.5;if(!(mid>lo&&mid<hi))return AlgorithmStatus.NotConverged;
            var lower=split%2==0?new SurfaceParameterBox(chosen.U0,mid,chosen.V0,chosen.V1):new(chosen.U0,chosen.U1,chosen.V0,mid);
            var upper=split%2==0?new SurfaceParameterBox(mid,chosen.U1,chosen.V0,chosen.V1):new(chosen.U0,chosen.U1,mid,chosen.V1);
            var left=cell;var right=cell;left.Depth=right.Depth=cell.Depth+1;
            if(split<2){left.First=lower;right.First=upper;}else{left.Second=lower;right.Second=upper;}
            pending[count++]=right;pending[count++]=left;
        }
        return AlgorithmStatus.Success;
    }
}
