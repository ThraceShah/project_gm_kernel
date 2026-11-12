using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // The rolling solve already owns a certified native UV foot. Intersect
    // that UV branch with the parameter ray as one scalar equation, rather
    // than intersecting two independently evaluated spatial curves again.
    private static AlgorithmStatus IntersectSplineContactParameterRay(SurfaceSlot source,in ICurveData contact,
        in NestedBlendProgram program,BlendSurfaceIndex band,in ModelingEdge edge,Generated.PK_UV_s query,double du,double dv,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var map=BlendContactMap(contact);var stations=map[..contact.ChartCount];
        var first=Math.Min(stations[0],stations[^1]);var last=Math.Max(stations[0],stations[^1]);
        if(!double.IsFinite(du)||!double.IsFinite(dv)||du==0&&dv==0||!(last>first))return AlgorithmStatus.InvalidInput;
        var spine=program.Surfaces[band].Spine;
        var coordinate=new SurfaceIntersectionPatch(program,spine,contact.BlendContactSide,first,last,true);
        // This parameter view has a distinct demand from both XYZ and the
        // scalar-u view; it never inherits their cache entries or cell proof.
        var record=Curves[GetCurveSlotByTag(edge.Curve)];coordinate=new(coordinate,edge.Curve,record.Header.Generation,record.Sense);
        var level=new AnalyticSurface(SurfaceClass.Plane,new(){X=query.param[0],Y=query.param[1]},new(){X=dv,Y=-du},new(){Z=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> isolated=stackalloc ScalarRootIsolation[IntersectionPointCapacity];var n=0;
        var status=SurfaceIsoPlaneRoots.Find(coordinate,level,first,last,roots,ref n,0,true,isolated,use:RootIsolationUse.NativeSupportCoordinate);
        if(status!=AlgorithmStatus.Success)return status;
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
        for(var i=0;i<n;i++)
        {
            var root=roots[i];var uv=root.Point;
            var along=Math.Abs(du)>=Math.Abs(dv)?(uv.X-query.param[0])/du:(uv.Y-query.param[1])/dv;
            if(!double.IsFinite(along))return AlgorithmStatus.NotConverged;
            status=EvaluateSurfaceCore(in Surfaces[source],uv.X,uv.Y,layout,point,0);if(status!=AlgorithmStatus.Success)return status;
            if(BlendContactParameter(contact,point[0],root.First,out var t)!=0)return AlgorithmStatus.NotConverged;
            if(!OperationIntersection.InInterval(t,edge.Start,edge.End))continue;
            // Find already deduplicated by the isolated native root. Do not
            // erase two proved distinct crossings with a Cartesian epsilon.
            if(count==output.Length)return AlgorithmStatus.OutputTooSmall;
            output[count++]=new(){Point=point[0],First=along,Second=t,Kind=root.Kind};
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}
