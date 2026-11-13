using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Solve the plane equation on the actual fixed-V contact of the Blend.
    // Isolate in U before mapping the entire result into the original native
    // ICurve chord gauge. Adjacent gauge cells share their stored anchor;
    // retain both projections when the one isolated U root spans that seam.
    private static bool TryBlendContactPlaneEvents(in ICurveData data,in ModelingEdge edge,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count,Span<ScalarRootIsolation> contactRoots)
    {
        count=0;var n=data.ChartCount;
        var map=BlendContactMap(data);var station=map[..n];var t=map.Slice(n,n);var scales=map.Slice(2*n,n-1);
        var chords=MemoryMarshal.Cast<double,KernelVector3>(map[(3*n-1)..]);
        var positions=MemoryMarshal.Cast<double,KernelVector3>(HvecSpan(data).Slice(3*data.StartLimit.HvecCount,3*n));
        var support=Surfaces[GetSurfaceSlotByTag(data.Surface1Tag)];
        var blend=BlendedEdgeDataPool[support.DataIndex];
        var program=GetNestedBlendProgram(blend,support);var v=BlendContactV(data);
        var lower=Math.Min(station[0],station[^1]);var upper=Math.Max(station[0],station[^1]);
        if(!(upper>lower))return false;
        var patch=new SurfaceIntersectionPatch(program,blend.ProgramRoot,new(lower,upper,v,v));
        patch=new SurfaceIntersectionPatch(patch,data.Surface1Tag,support.Header.Generation,support.Sense);
        var iso=new SurfaceIntersectionPatch(patch,false,v,lower,upper);
        var found=0;
        var status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,roots,ref found,strictSeparation:true,isolation:isolation,use:RootIsolationUse.Contact);
        if(status!=AlgorithmStatus.Success)return false;
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        Span<KernelVector3> point=stackalloc KernelVector3[1];
        for(var r=0;r<found;r++)
        {
            var contactRoot=isolation[r];var u=contactRoot.Root;IntervalNumber native=default;var first=-1;var shared=double.NaN;
            for(var cell=0;cell<n-1;cell++)
            {
                var increasing=station[cell+1]>station[cell];
                var a=Math.Min(station[cell],station[cell+1]);var b=Math.Max(station[cell],station[cell+1]);
                var lo=Math.Max(a,u.Lower);var hi=Math.Min(b,u.Upper);if(lo>hi)continue;
                if(first>=0&&cell!=first+1)return false;
                var chord=chords[cell];var scale=scales[cell];var anchor=positions[cell];
                if(!CertifyContactGauge(iso,a,b,chord,scale,increasing))return false;
                status=iso.Bound(new(lo,hi,0,0),jet);if(status!=AlgorithmStatus.Success)return false;
                var projected=t[cell]+scale*((jet[0]-anchor.X)*chord.X+(jet[1]-anchor.Y)*chord.Y+(jet[2]-anchor.Z)*chord.Z);
                if(!projected.Finite)return false;
                if(first<0){first=cell;native=projected;}
                else
                {
                    shared=t[cell];
                    native=IntervalNumber.Hull(IntervalNumber.Hull(native,projected),new(shared));
                }
            }
            if(first<0)return false;
            var last=first+(double.IsNaN(shared)?1:2);
            if(native.Lower<=t[first]||native.Upper>=t[last])return false;
            if(native.Upper<edge.Start||native.Lower>edge.End)continue;
            if(native.Lower<=edge.Start||native.Upper>=edge.End)return false;
            var parameter=double.IsNaN(shared)?native.Midpoint:shared;
            status=EvaluateBlendContact(data,parameter,0,point,out _);if(status!=AlgorithmStatus.Success)return false;
            status=iso.Bound(new(u.Lower,u.Upper,0,0),jet);if(status!=AlgorithmStatus.Success)return false;
            var p=point[0];
            var error=new IntervalVector(new(jet[0]-p.X,0),new(jet[1]-p.Y,0),new(jet[2]-p.Z,0)).Norm().Value.Upper;
            if(!double.IsFinite(error)||error>OperationIntersection.DistanceTolerance)return false;
            var jn=count++;
            while(jn>0&&roots[jn-1].First>parameter)
            {
                roots[jn]=roots[jn-1];isolation[jn]=isolation[jn-1];spatialErrors[jn]=spatialErrors[jn-1];contactRoots[jn]=contactRoots[jn-1];jn--;
            }
            roots[jn]=new(){Point=p,First=parameter,Second=u.Midpoint,Kind=IntersectionVectorKind.Simple};
            isolation[jn]=new(){Root=native,Certified=true};spatialErrors[jn]=error;
            contactRoots[jn]=contactRoot;
        }
        for(var r=1;r<count;r++)if(isolation[r-1].Root.Upper>=isolation[r].Root.Lower)return false;
        return true;
    }

    private static bool CertifyContactGauge(in SurfaceIntersectionPatch iso,double first,double last,in KernelVector3 chord,double scale,bool increasing)
    {
        Span<IntervalNumber> pending=stackalloc IntervalNumber[24],jet=stackalloc IntervalNumber[9];
        pending[0]=new(first,last);var count=1;var budget=512;
        while(count>0)
        {
            if(--budget<0)return false;
            var interval=pending[--count];var lo=interval.Lower;var hi=interval.Upper;
            var status=iso.Bound(new(lo,hi,0,0),jet);
            if(status==AlgorithmStatus.Success)
            {
                var slope=scale*(jet[3]*chord.X+jet[4]*chord.Y+jet[5]*chord.Z);
                if(slope.Finite&&(increasing?slope.Lower>0:slope.Upper<0))continue;
            }
            else if(status!=AlgorithmStatus.NotConverged)return false;
            var mid=interval.Midpoint;if(!(mid>lo&&mid<hi)||count+2>pending.Length)return false;
            pending[count++]=new(mid,hi);pending[count++]=new(lo,mid);
        }
        return true;
    }

    private static bool TryFiniteSectionContactEnd(in FiniteSectionChart section,SurfTag plane,in RegularBoundaryRoot root,out double parameter)
    {
        parameter=0;
        if(root.Certified!=NativePlaneCertificate||root.ContactSurface!=section.Surface||root.CuttingSurface!=plane
            ||section.CuttingPlane!=plane||section.PlaneGeneration!=Surfaces[GetSurfaceSlotByTag(plane)].Header.Generation)return false;
        var carrier=Curves[GetCurveSlotByTag(root.Curve)];if(carrier.Class!=CurveClass.ICurve)return false;
        var contact=ICurveDataPool[carrier.DataIndex];
        if(contact.IsBlendContact==0||!SameSheetSupport(section.Surface,contact.Surface1Tag))return false;
        var a=Surfaces[GetSurfaceSlotByTag(section.Surface)];var b=Surfaces[GetSurfaceSlotByTag(contact.Surface1Tag)];
        if(a.UMin!=b.UMin||a.UMax!=b.UMax)return false;
        // The section provider proved strict U monotonicity against this
        // plane for every V in the strip. The source root belongs to this
        // same Blend and its contact V selects a unique section endpoint.
        var v=BlendContactV(contact);
        if(v==section.VFirst)parameter=section.First;
        else if(v==section.VLast)parameter=section.Last;
        else return false;
        Span<KernelVector3> point=stackalloc KernelVector3[1];
        var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(section.Curve)],parameter,0,false,point,out _);
        if(status!=AlgorithmStatus.Success)return false;
        var error=(new IntervalVector(point[0])-new IntervalVector(root.Root.Point)).Norm().Value+root.Root.SpatialError;
        return error.Upper<=OperationIntersection.DistanceTolerance;
    }
}
