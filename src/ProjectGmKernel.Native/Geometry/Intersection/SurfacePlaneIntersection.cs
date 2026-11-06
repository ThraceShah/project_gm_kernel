using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

// A plane contributes one scalar equation in the actual surface parameters.
// A nonzero interval partial proves each admitted cell is a regular graph;
// complete boundary and transverse-section isolation decide its connectivity.
internal static class SurfacePlaneIntersection
{
    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,
        Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount)
    {
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[128];
        Span<BufferCount> depths=stackalloc BufferCount[128];pending[0]=patch.Domain;depths[0]=0;var count=1;var visited=0;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> boundary=stackalloc SurfaceIntersectionNode[64];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[32];
        while(count>0)
        {
            if(++visited>32768)return AlgorithmStatus.NotConverged;
            var index=--count;var cell=pending[index];var depth=depths[index];var free=-1;
            var savedNodes=nodeCount;var savedSegments=segmentCount;
            var status=patch.Bound(cell,bounds);if(status==AlgorithmStatus.NotConverged)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
            var distance=Distance(bounds,plane);if(distance.Lower>1e-10||distance.Upper< -1e-10)continue;
            var du=bounds[3]*plane.Axis.X+bounds[4]*plane.Axis.Y+bounds[5]*plane.Axis.Z;
            var dv=bounds[6]*plane.Axis.X+bounds[7]*plane.Axis.Y+bounds[8]*plane.Axis.Z;
            if(!du.ContainsZero)free=1;else if(!dv.ContainsZero)free=0;
            if(free<0)goto Subdivide;
            // Keep enough exact CHART anchors for the following intersection
            // evaluator; the graph proof, rather than this spacing, is the
            // completeness certificate.
            if(Width(cell,free)>.2*Width(patch.Domain,free))goto Subdivide;
            var n=0;
            for(var axis=0;axis<2;axis++)for(var end=0;end<2;end++)
            {
                var at=axis==0?(end==0?cell.U0:cell.U1):(end==0?cell.V0:cell.V1);
                var lower=axis==0?cell.V0:cell.U0;var upper=axis==0?cell.V1:cell.U1;
                var iso=new SurfaceIntersectionPatch(patch,axis==0,at,lower,upper);
                status=iso.Bound(new(lower,upper,0,0),bounds);if(status!=AlgorithmStatus.Success)goto Retry;
                if(Distance(bounds,plane).Magnitude<=2e-12)
                {
                    // An entire edge is within the geometric root tolerance.
                    // Its two ends belong to the same unique regular graph.
                    status=BoundaryNode(patch,plane,axis,at,lower,boundary,ref n);if(status!=AlgorithmStatus.Success)goto Retry;
                    status=BoundaryNode(patch,plane,axis,at,upper,boundary,ref n);if(status!=AlgorithmStatus.Success)goto Retry;
                    continue;
                }
                var nr=0;status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,roots,ref nr);if(status!=AlgorithmStatus.Success)goto Retry;
                for(var r=0;r<nr;r++)
                {status=BoundaryNode(patch,plane,axis,at,roots[r].First,boundary,ref n);if(status!=AlgorithmStatus.Success)goto Retry;}
            }
            for(var i=1;i<n;i++)for(var j=i;j>0&&boundary[j].Parameter(free)<boundary[j-1].Parameter(free);j--)
                (boundary[j],boundary[j-1])=(boundary[j-1],boundary[j]);
            for(var i=0;i+1<n;i++)
            {
                var lo=boundary[i].Parameter(free);var hi=boundary[i+1].Parameter(free);var at=lo+(hi-lo)*.5;
                if(!(at>lo&&at<hi))
                {
                    if(Same(boundary[i],boundary[i+1]))continue;
                    status=AlgorithmStatus.NotConverged;goto Retry;
                }
                var lower=free==0?cell.V0:cell.U0;var upper=free==0?cell.V1:cell.U1;
                var iso=new SurfaceIntersectionPatch(patch,free==0,at,lower,upper);var nr=0;
                status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,roots,ref nr);if(status!=AlgorithmStatus.Success||nr>1)goto Retry;
                if(nr==0)continue;
                status=AddSegment(patch,cell,boundary[i],boundary[i+1],nodes,ref nodeCount,segments,ref segmentCount);if(status!=AlgorithmStatus.Success)return status;
            }
            continue;
            Retry:
            if(status is not (AlgorithmStatus.Success or AlgorithmStatus.NotConverged))return status;
            Subdivide:
            nodeCount=savedNodes;segmentCount=savedSegments;
            if(depth>=40||count+2>pending.Length)return AlgorithmStatus.NotConverged;
            var split=free>=0?free:Width(cell,0)/Width(patch.Domain,0)>=Width(cell,1)/Width(patch.Domain,1)?0:1;
            var low=split==0?cell.U0:cell.V0;var high=split==0?cell.U1:cell.V1;var middle=low+(high-low)*.4931471805599453;
            if(!(middle>low&&middle<high))return AlgorithmStatus.NotConverged;
            pending[count]=split==0?new(cell.U0,middle,cell.V0,cell.V1):new(cell.U0,cell.U1,cell.V0,middle);depths[count++]=depth+1;
            pending[count]=split==0?new(middle,cell.U1,cell.V0,cell.V1):new(cell.U0,cell.U1,middle,cell.V1);depths[count++]=depth+1;
        }
        return AlgorithmStatus.Success;
    }

    private static double Width(in SurfaceParameterBox box,BufferOffset axis)=>axis==0?box.U1-box.U0:box.V1-box.V0;
    private static IntervalNumber Distance(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface plane)
        =>(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
    private static bool Same(in SurfaceIntersectionNode a,in SurfaceIntersectionNode b)
        =>Math.Abs(a.U-b.U)<2e-10&&Math.Abs(a.V-b.V)<2e-10&&OperationIntersection.Norm(Sub(a.Point,b.Point))<1e-8;

    private static AlgorithmStatus BoundaryNode(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,BufferOffset axis,double at,double parameter,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount count)
    {
        var u=axis==0?at:parameter;var v=axis==0?parameter:at;var node=new SurfaceIntersectionNode{U=u,V=v};
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var status=patch.Bound(new(u,u,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        if(Distance(bounds,plane).Magnitude>1e-10)return AlgorithmStatus.NotConverged;
        node.Point=Vector(bounds[0].Midpoint,bounds[1].Midpoint,bounds[2].Midpoint);
        status=OperationIntersection.Parameters(plane,node.Point,out node.S,out node.T);if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<count;i++)if(Same(nodes[i],node))return AlgorithmStatus.Success;
        if(count==nodes.Length)return AlgorithmStatus.OutputTooSmall;nodes[count++]=node;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AddSegment(in SurfaceIntersectionPatch patch,in SurfaceParameterBox box,in SurfaceIntersectionNode a,in SurfaceIntersectionNode b,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount)
    {
        var start=-1;var end=-1;for(var i=0;i<nodeCount;i++){if(Same(nodes[i],a))start=i;if(Same(nodes[i],b))end=i;}
        if(start<0){if(nodeCount==nodes.Length)return AlgorithmStatus.OutputTooSmall;start=nodeCount;nodes[nodeCount++]=a;}
        if(end<0){if(nodeCount==nodes.Length)return AlgorithmStatus.OutputTooSmall;end=nodeCount;nodes[nodeCount++]=b;}
        if(start==end)return AlgorithmStatus.NumericalFailure;
        for(var i=0;i<segmentCount;i++)if(segments[i].Start==start&&segments[i].End==end||segments[i].Start==end&&segments[i].End==start)return AlgorithmStatus.Success;
        if(segmentCount==segments.Length)return AlgorithmStatus.OutputTooSmall;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var status=patch.Bound(box,bounds);if(status!=AlgorithmStatus.Success)return status;
        var chord=Sub(b.Point,a.Point);var square=Dot(chord,chord);var error=0.0;
        for(var corner=0;corner<8;corner++)
        {
            var point=Vector((corner&1)==0?bounds[0].Lower:bounds[0].Upper,(corner&2)==0?bounds[1].Lower:bounds[1].Upper,(corner&4)==0?bounds[2].Lower:bounds[2].Upper);
            var t=square>0?Math.Clamp(Dot(Sub(point,a.Point),chord)/square,0,1):0;
            error=Math.Max(error,OperationIntersection.Norm(Sub(point,Add(a.Point,Scale(chord,t)))));
        }
        segments[segmentCount++]=new(){Start=start,End=end,ChordalError=error*(1+1e-12)+2e-12*Math.Max(1,OperationIntersection.Norm(a.Point))};return AlgorithmStatus.Success;
    }
}
