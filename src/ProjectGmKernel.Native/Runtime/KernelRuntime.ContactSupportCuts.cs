using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Point contacts travel separately from the cut curves. The caller must
    // clip their two UV pairs to the actual faces before material assembly.
    private static int TryPrepareRegularContactSupportCuts(SurfTag first,SurfTag second,Span<SheetCutCurve> cuts,out BufferCount count,
        Span<SurfaceIntersectionNode> points,out BufferCount pointCount,out SurfacePairRelation relation,out bool handled)
    {
        count=pointCount=0;relation=SurfacePairRelation.Unresolved;handled=false;
        var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);if(a<0||b<0)return ParasolidConstants.PK_ERROR_missing_geom;
        var spline=Surfaces[a].Class==SurfaceClass.BSurface?a:Surfaces[b].Class==SurfaceClass.BSurface?b:-1;
        var other=spline==a?b:a;
        if(spline<0||!TryPrepareSurface(Surfaces[other],out var plane)||plane.Kind!=SurfaceClass.Plane)return 0;
        Span<KernelVector3> contacts=stackalloc KernelVector3[64];
        Span<SurfaceIntersectionNode> locations=stackalloc SurfaceIntersectionNode[64];
        var status=ConvexPlaneContacts.Find(GetBSurfaceView(BSurfaceDataStore[Surfaces[spline].DataIndex]),plane,contacts,locations,out var found);
        if(status==AlgorithmStatus.Unsupported&&found==0)return 0;
        handled=true;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(found>points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<found;i++)
        {
            var location=locations[i];
            if(spline!=a){(location.U,location.S)=(location.S,location.U);(location.V,location.T)=(location.T,location.V);}
            points[i]=location;
        }
        pointCount=found;relation=found==0?SurfacePairRelation.Empty:SurfacePairRelation.PointContact;return 0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestPrepareRegularContactSupportCuts(SurfTag first,SurfTag second,Span<SurfaceIntersectionNode> points,
        out BufferCount pointCount,out SurfacePairRelation relation,out bool handled)
    {
        Span<SheetCutCurve> cuts=stackalloc SheetCutCurve[64];
        return TryPrepareRegularContactSupportCuts(first,second,cuts,out _,points,out pointCount,out relation,out handled);
    }
#endif
}
