using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>All regular intersections of an existing two-support intersection
/// curve with a plane. Four parameters, three coincidence equations and the
/// plane equation; an ICURVE chart is used only to select the requested branch.</summary>
internal static class SurfacePairPlaneRoots
{
    internal static AlgorithmStatus Find(in BezierSurfacePatch first, in BezierSurfacePatch second, in AnalyticSurface plane,
        Span<SurfaceIntersectionNode> roots, out BufferCount count)
    {
        count = 0;
        var domain = new ParameterCell { Lower = new() { U=first.Domain.U0,V=first.Domain.V0,S=second.Domain.U0,T=second.Domain.V0 },
            Upper = new() { U=first.Domain.U1,V=first.Domain.V1,S=second.Domain.U1,T=second.Domain.V1 } };
        Span<ParameterCell> pending = stackalloc ParameterCell[256]; pending[0] = domain; var n = 1; var visited = 0;
        Span<IntervalNumber> f = stackalloc IntervalNumber[4], jac = stackalloc IntervalNumber[16];
        Span<IntervalNumber> cf = stackalloc IntervalNumber[4], cj = stackalloc IntervalNumber[16];
        Span<IntervalNumber> position = stackalloc IntervalNumber[9]; Span<double> inverse = stackalloc double[16];
        while (n > 0)
        {
            if (++visited > 100000) return AlgorithmStatus.NotConverged;
            var cell = pending[--n]; var status = Bounds(first,second,plane,cell,f,jac); if (status != AlgorithmStatus.Success) return status;
            var empty = false; for (var r=0;r<4;r++) if (!f[r].ContainsZero) empty=true; if (empty) continue;
            var point = cell.Center;
            if (Invert(jac,inverse) && Contraction(jac,inverse,cell)<.8)
            {
                for (var iteration=0;iteration<14;iteration++)
                {
                    status=Bounds(first,second,plane,new(){Lower=point,Upper=point},cf,cj); if(status!=AlgorithmStatus.Success)return status;
                    if(!Invert(cj,inverse))break;var change=0.0;
                    for(var r=0;r<4;r++)
                    {
                        var step=0.0;for(var k=0;k<4;k++)step+=inverse[4*r+k]*cf[k].Midpoint;
                        var next=Math.Clamp(point.Parameter(r)-step,cell.Lower.Parameter(r),cell.Upper.Parameter(r));
                        change=Math.Max(change,Math.Abs(next-point.Parameter(r)));point.SetParameter(r,next);
                    }
                    if(change<1e-14)break;
                }
                var singleton=new ParameterCell{Lower=point,Upper=point};
                status=Bounds(first,second,plane,singleton,cf,cj);if(status!=AlgorithmStatus.Success)return status;
                var extended=cell;
                for(var d=0;d<4;d++)
                {
                    var pad=Math.Max(cell.Width(d)*.05,1e-11*domain.Width(d));
                    extended.Lower.SetParameter(d,cell.Lower.Parameter(d)-pad);extended.Upper.SetParameter(d,cell.Upper.Parameter(d)+pad);
                }
                status=Bounds(first,second,plane,extended,f,jac);if(status!=AlgorithmStatus.Success)return status;
                var unique=Invert(cj,inverse);empty=false;
                for(var r=0;r<4&&unique;r++)
                {
                    IntervalNumber image=point.Parameter(r);
                    for(var k=0;k<4;k++)image-=inverse[4*r+k]*cf[k];
                    for(var c=0;c<4;c++)
                    {
                        IntervalNumber m=r==c?1:0;for(var k=0;k<4;k++)m-=inverse[4*r+k]*jac[4*k+c];
                        image+=m*(new IntervalNumber(extended.Lower.Parameter(c),extended.Upper.Parameter(c))-point.Parameter(c));
                    }
                    empty|=image.Upper<cell.Lower.Parameter(r)||image.Lower>cell.Upper.Parameter(r);
                    unique&=image.Lower>extended.Lower.Parameter(r)&&image.Upper<extended.Upper.Parameter(r);
                }
                if(empty)continue;
                var residual=0.0;for(var r=0;r<4;r++)residual=Math.Max(residual,cf[r].Magnitude);
                if(unique&&residual<1e-9)
                {
                    status=BezierSurfaceBounds.Bound(first,singleton.First,position);if(status!=AlgorithmStatus.Success)return status;
                    point.Point=new(){X=position[0].Midpoint,Y=position[1].Midpoint,Z=position[2].Midpoint};
                    var duplicate=false;
                    for(var i=0;i<count;i++)
                    {
                        var same=true;for(var d=0;d<4;d++)same&=Math.Abs(roots[i].Parameter(d)-point.Parameter(d))<2e-9*Math.Max(1,domain.Width(d));
                        duplicate|=same;
                    }
                    if(!duplicate){if(count==roots.Length)return AlgorithmStatus.OutputTooSmall;roots[count++]=point;}
                    continue;
                }
            }
            if(cell.Depth>=52||n+2>pending.Length)return AlgorithmStatus.NotConverged;
            var split=0;var widest=0.0;
            for(var d=0;d<4;d++)if(cell.Width(d)/domain.Width(d)>widest){widest=cell.Width(d)/domain.Width(d);split=d;}
            var middle=cell.Lower.Parameter(split)+cell.Width(split)*.4931471805599453;
            var right=cell;cell.Upper.SetParameter(split,middle);right.Lower.SetParameter(split,middle);cell.Depth++;right.Depth=cell.Depth;
            pending[n++]=right;pending[n++]=cell;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Bounds(in BezierSurfacePatch first,in BezierSurfacePatch second,in AnalyticSurface plane,
        in ParameterCell cell,Span<IntervalNumber> f,Span<IntervalNumber> jac)
    {
        var status=ParametricSurfaceIntersection.Bounds(first,second,cell,f,jac);if(status!=AlgorithmStatus.Success)return status;
        Span<IntervalNumber> a=stackalloc IntervalNumber[9];status=BezierSurfaceBounds.Bound(first,cell.First,a);if(status!=AlgorithmStatus.Success)return status;
        f[3]=(a[0]-plane.Origin.X)*plane.Axis.X+(a[1]-plane.Origin.Y)*plane.Axis.Y+(a[2]-plane.Origin.Z)*plane.Axis.Z;
        jac[12]=a[3]*plane.Axis.X+a[4]*plane.Axis.Y+a[5]*plane.Axis.Z;
        jac[13]=a[6]*plane.Axis.X+a[7]*plane.Axis.Y+a[8]*plane.Axis.Z;jac[14]=jac[15]=0;
        return AlgorithmStatus.Success;
    }
    private static bool Invert(ReadOnlySpan<IntervalNumber> matrix,Span<double> inverse)
    {
        Span<double> lu=stackalloc double[16],rhs=stackalloc double[4];Span<BufferOffset> pivots=stackalloc BufferOffset[4];
        for(var i=0;i<16;i++)lu[i]=matrix[i].Midpoint;
        if(SmallLinearSolve.LuFactorize(lu,4,pivots)!=AlgorithmStatus.Success)return false;
        for(var c=0;c<4;c++)
        {
            rhs.Clear();rhs[c]=1;if(SmallLinearSolve.LuSolveInPlace(lu,4,pivots,rhs)!=AlgorithmStatus.Success)return false;
            for(var r=0;r<4;r++)inverse[4*r+c]=rhs[r];
        }
        return true;
    }
    private static double Contraction(ReadOnlySpan<IntervalNumber> matrix,ReadOnlySpan<double> inverse,in ParameterCell cell)
    {
        var norm=0.0;
        for(var r=0;r<4;r++)
        {
            var row=0.0;
            for(var c=0;c<4;c++)
            {
                IntervalNumber m=r==c?1:0;for(var k=0;k<4;k++)m-=inverse[4*r+k]*matrix[4*k+c];
                row=Math.BitIncrement(row+(new IntervalNumber(m.Magnitude)
                    *(new IntervalNumber(cell.Upper.Parameter(c))-cell.Lower.Parameter(c))/Math.Max(cell.Width(r),1e-30)).Upper);
            }
            norm=Math.Max(norm,row);
        }
        return norm;
    }
}
