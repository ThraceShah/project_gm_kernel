using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Transfer the source normal section to the band through the midpoint
    // of its two exact contact loci. The section, not its chord, is later
    // imprinted. Source and rolling stations are different parameters.
    private static bool TryEllipticSubdivisionStation(in RollingBandCandidate band,ref RollingSubdivision split)
    {
        var source=Curves[GetCurveSlotByTag(Edges[band.Source].CurveTag)];
        if(source.Class!=CurveClass.Ellipse||!TryReadExplicitSpineCurve(source,out var conic))return false;
        var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(band.Surface)].DataIndex];
        var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];var spine=program.Spines[root.Spine].Curve;
        if(AnalyticSpineContact.Curve(spine,program.Surfaces[root.First],root.Range0,out var first)!=AlgorithmStatus.Success
            ||AnalyticSpineContact.Curve(spine,program.Surfaces[root.Second],root.Range1,out var second)!=AlgorithmStatus.Success
            ||SpineParameterMap.Interval(conic,new(split.Parameter,0),out var vertex,out var tangent)!=AlgorithmStatus.Success)return false;
        var a=IntervalVector.Dot((new IntervalVector(first.First)+new IntervalVector(second.First))*.5,tangent).Value;
        var b=IntervalVector.Dot((new IntervalVector(first.Second)+new IntervalVector(second.Second))*.5,tangent).Value;
        var c=IntervalVector.Dot((new IntervalVector(first.Origin)+new IntervalVector(second.Origin))*.5-vertex,tangent).Value;
        var lower=band.SourceStart;var upper=band.SourceEnd;
        var left=SectionHeight(a,b,c,new(lower));var right=SectionHeight(a,b,c,new(upper));
        var sign=left.Upper<0&&right.Lower>0?1:left.Lower>0&&right.Upper<0?-1:0;
        if(sign==0||!CertifySectionMonotonicity(a,b,new(lower,upper),sign))return false;
        for(var iteration=0;iteration<64;iteration++)
        {
            var t=lower+(upper-lower)*.5;if(!(t>lower&&t<upper))break;
            var height=sign*SectionHeight(a,b,c,new(t));
            if(height.Upper<0)lower=t;
            else if(height.Lower>0)upper=t;
            else
            {
                var slope=SectionSlope(a,b,new(lower,upper));if(slope.ContainsZero)return false;
                var enclosure=t-SectionHeight(a,b,c,new(t))/slope;
                lower=Math.Max(lower,enclosure.Lower);upper=Math.Min(upper,enclosure.Upper);break;
            }
        }
        var station=lower+(upper-lower)*.5;
        if(!(lower>band.SourceStart&&upper<band.SourceEnd&&upper>=lower)
            ||upper-lower>128*SmallLinearSolve.MachineEpsilon*(1+Math.Abs(station)))return false;
        split.Station=station;split.StationLower=lower;split.StationUpper=upper;return true;
    }

    private static IntervalNumber SectionHeight(IntervalNumber a,IntervalNumber b,IntervalNumber c,IntervalNumber t)
    {
        IntervalDual.SinCos(new(t,0),out var s,out var k);return a*k.Value+b*s.Value+c;
    }

    private static IntervalNumber SectionSlope(IntervalNumber a,IntervalNumber b,IntervalNumber t)
    {
        IntervalDual.SinCos(new(t,0),out var s,out var k);return -a*s.Value+b*k.Value;
    }

    private static bool CertifySectionMonotonicity(IntervalNumber a,IntervalNumber b,IntervalNumber domain,int sign)
    {
        Span<(IntervalNumber Domain,BufferCount Depth)> pending=stackalloc (IntervalNumber,BufferCount)[24];
        pending[0]=(domain,0);var count=1;var budget=128;
        while(count>0)
        {
            if(--budget<0)return false;
            var (cell,depth)=pending[--count];if((sign*SectionSlope(a,b,cell)).Lower>0)continue;
            var midpoint=cell.Midpoint;
            if(depth>=20||!(midpoint>cell.Lower&&midpoint<cell.Upper))return false;
            pending[count++]=(new(cell.Lower,midpoint),depth+1);pending[count++]=(new(midpoint,cell.Upper),depth+1);
        }
        return true;
    }
}
