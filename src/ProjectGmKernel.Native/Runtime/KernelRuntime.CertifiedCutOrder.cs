using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static IntervalNumber IntersectStationBounds(IntervalNumber first,IntervalNumber second)
        =>new(Math.Max(first.Lower,second.Lower),Math.Min(first.Upper,second.Upper));

    private static int ReconcileCertifiedCutRoot(CurveTag carrier,BufferOffset current,ReadOnlySpan<RegularBoundaryRoot> roots,
        Span<double> rootParameters,ReadOnlySpan<IntervalNumber> locators,Span<double> stations,out bool joined)
    {
        joined=false;
        var incoming=locators[current];var joint=incoming;var parameter=rootParameters[current];var existing=double.NaN;
        ref readonly var root=ref roots[current];
        for(var i=0;i<current;i++)
        {
            var previous=locators[i];if(!previous.Finite)continue;
            ref readonly var other=ref roots[i];
            var same=root.Witness!=0&&root.Witness==other.Witness&&root.InputEdge==other.InputEdge
                &&root.Curve==other.Curve&&root.CuttingSurface==other.CuttingSurface;
            var overlap=IntersectStationBounds(incoming,previous).Finite;
            if(!same&&!overlap)continue;
            joint=IntersectStationBounds(joint,previous);
            if(!same||!joint.Finite||root.Root.Lower>other.Root.Upper||other.Root.Lower>root.Root.Upper
                ||double.IsFinite(existing)&&existing!=rootParameters[i])
                return UnresolvedBoundaryStation(carrier,-1,parameter,incoming,rootParameters[i],previous);
            existing=rootParameters[i];
        }
        if(!double.IsFinite(existing))return 0;
        // Refine the common enclosure. Keep the representative when it is
        // still in that enclosure so a rediscovery does not move an anchor.
        var resolved=Math.Clamp(existing,joint.Lower,joint.Upper);
        if(resolved!=existing)
        {
            var station=stations.IndexOf(existing);
            if(station<0||stations.Contains(resolved))
                return UnresolvedBoundaryStation(carrier,-1,parameter,incoming,existing,joint);
            stations[station]=resolved;
            for(var i=0;i<current;i++)if(rootParameters[i]==existing)rootParameters[i]=resolved;
        }
        rootParameters[current]=resolved;joined=true;return 0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestCertifiedCutRediscovery(IntervalNumber first,IntervalNumber second,bool sameEdge,bool sameWitness,
        out bool joined,out double representative)
    {
        Span<RegularBoundaryRoot> roots=stackalloc RegularBoundaryRoot[2];
        roots[0]=new(){Curve=1,CuttingSurface=2,InputEdge=0,Witness=1,Root=new(){Lower=.4,Upper=.6}};
        roots[1]=new(){Curve=1,CuttingSurface=2,InputEdge=sameEdge?0:1,Witness=sameWitness?1:2,Root=new(){Lower=.5,Upper=.7}};
        Span<double> rootParameters=stackalloc double[2]{first.Midpoint,second.Midpoint};
        Span<IntervalNumber> locators=stackalloc IntervalNumber[2]{first,second};
        Span<double> stations=stackalloc double[1]{first.Midpoint};
        var error=ReconcileCertifiedCutRoot(3,1,roots,rootParameters,locators,stations,out joined);
        representative=stations[0];return error;
    }
#endif
}
