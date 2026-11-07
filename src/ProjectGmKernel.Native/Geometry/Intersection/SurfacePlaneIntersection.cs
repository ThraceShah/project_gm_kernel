using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

// Plane/cylinder/sphere supports contribute one scalar equation in the actual surface parameters.
// A nonzero interval partial proves each admitted cell is a regular graph;
// complete boundary and transverse-section isolation decide its connectivity.
internal static class SurfacePlaneIntersection
{
    [ThreadStatic] internal static SurfaceParameterBox LastUnresolvedBox;
    [ThreadStatic] internal static BufferCount LastUnresolvedDepth;
    [ThreadStatic] internal static BufferOffset LastUnresolvedFreeAxis;
    [ThreadStatic] internal static bool LastUnresolvedBudget;
    internal enum BoundaryFailure:byte{None,Bound,Roots,Node,Transverse,Order}
    [ThreadStatic] internal static BoundaryFailure LastBoundaryFailure;
    [ThreadStatic] internal static IntervalNumber LastBoundaryResidual;
    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,
        Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount)
    {
        BufferCount remaining=32768;
        return Find(patch,plane,nodes,ref nodeCount,segments,ref segmentCount,ref remaining);
    }

    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,
        Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount,ref BufferCount remaining,bool deferIsolatedPoints=false,double tolerance=1e-10,bool strictSeparation=false)
    {
        if(!AnalyticLevelSet.Supported(plane))return AlgorithmStatus.Unsupported;
        LastUnresolvedBudget=false;LastBoundaryFailure=BoundaryFailure.None;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[128];
        Span<BufferCount> depths=stackalloc BufferCount[128];pending[0]=patch.Domain;depths[0]=0;var count=1;var visited=0;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Span<IntervalNumber> centerBounds=stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> boundary=stackalloc SurfaceIntersectionNode[64];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[32];
        Span<ScalarRootIsolation> isolations=stackalloc ScalarRootIsolation[32];
        var certifiedRoots=plane.Kind!=SurfaceClass.Plane;
        while(count>0)
        {
            ModelingPerformance.Cell(false);
            if(++visited>32768||remaining--<=0)
            {LastUnresolvedBudget=true;LastUnresolvedBox=pending[count-1];LastUnresolvedDepth=depths[count-1];ModelingPerformance.BudgetFailure();return AlgorithmStatus.NotConverged;}
            var index=--count;var cell=pending[index];var depth=depths[index];var free=-1;var failedBoundary=false;
            var savedNodes=nodeCount;var savedSegments=segmentCount;
            var status=patch.Bound(cell,bounds);if(status==AlgorithmStatus.NotConverged)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
            var distance=Distance(bounds,plane);
            var du=AnalyticLevelSet.Derivative(bounds,plane,3);
            var dv=AnalyticLevelSet.Derivative(bounds,plane,6);
            if(plane.Kind!=SurfaceClass.Plane)
            {
                // The scalar equation is composed with one parameterized
                // patch. A mean-value enclosure preserves cancellations
                // which the independent XYZ interval hull cannot express.
                var cu=cell.U0+(cell.U1-cell.U0)*.5;var cv=cell.V0+(cell.V1-cell.V0)*.5;
                status=patch.Bound(new(cu,cu,cv,cv),centerBounds);if(status!=AlgorithmStatus.Success)goto Subdivide;
                var centered=Distance(centerBounds,plane)+du*(new IntervalNumber(cell.U0,cell.U1)-cu)+dv*(new IntervalNumber(cell.V0,cell.V1)-cv);
                distance=new(Math.Max(distance.Lower,centered.Lower),Math.Min(distance.Upper,centered.Upper));
                if(!distance.Finite)goto Subdivide;
            }
            var exclusion=strictSeparation?0:tolerance;if(distance.Lower>exclusion||distance.Upper< -exclusion)continue;
            var uQuality=Math.Min(Math.Abs(du.Lower),Math.Abs(du.Upper))*Width(patch.Domain,0);
            var vQuality=Math.Min(Math.Abs(dv.Lower),Math.Abs(dv.Upper))*Width(patch.Domain,1);
            if(!du.ContainsZero&&(dv.ContainsZero||uQuality>=vQuality))free=1;else if(!dv.ContainsZero)free=0;
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
                status=iso.Bound(new(lower,upper,0,0),bounds);if(status!=AlgorithmStatus.Success){LastBoundaryFailure=BoundaryFailure.Bound;goto Retry;}
                if(Distance(bounds,plane).Magnitude==0||!certifiedRoots&&Distance(bounds,plane).Magnitude<=Math.Min(2e-12,tolerance))
                {
                    // An entire edge is within the geometric root tolerance.
                    // Its two ends belong to the same unique regular graph.
                    status=BoundaryNode(patch,plane,axis,at,lower,boundary,ref n,tolerance);if(status!=AlgorithmStatus.Success)goto Retry;
                    status=BoundaryNode(patch,plane,axis,at,upper,boundary,ref n,tolerance);if(status!=AlgorithmStatus.Success)goto Retry;
                    continue;
                }
                var nr=0;status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,roots,ref nr,tolerance,strictSeparation,certifiedRoots?isolations:default);if(status!=AlgorithmStatus.Success){LastBoundaryFailure=BoundaryFailure.Roots;goto Retry;}
                for(var r=0;r<nr;r++)
                {status=BoundaryNode(patch,plane,axis,at,roots[r].First,boundary,ref n,tolerance,certifiedRoots?isolations[r]:default);if(status!=AlgorithmStatus.Success){LastBoundaryFailure=BoundaryFailure.Node;goto Retry;}}
            }
            // A cell/chart corner can belong to an arc in its neighbour.
            // Keep every candidate until the complete graph is assembled;
            // only globally unconnected points are isolated contacts.
            for(var i=0;i<n;i++){status=AddNode(boundary[i],nodes,ref nodeCount,out _);if(status!=AlgorithmStatus.Success)return status;}
            for(var i=1;i<n;i++)for(var j=i;j>0&&boundary[j].Parameter(free)<boundary[j-1].Parameter(free);j--)
                (boundary[j],boundary[j-1])=(boundary[j-1],boundary[j]);
            for(var i=0;i+1<n;i++)
            {
                var lo=boundary[i].Parameter(free);var hi=boundary[i+1].Parameter(free);var at=lo+(hi-lo)*.5;
                if(!(at>lo&&at<hi))
                {
                    if(Same(boundary[i],boundary[i+1]))continue;
                    status=AlgorithmStatus.NotConverged;LastBoundaryFailure=BoundaryFailure.Order;goto Retry;
                }
                var lower=free==0?cell.V0:cell.U0;var upper=free==0?cell.V1:cell.U1;
                var iso=new SurfaceIntersectionPatch(patch,free==0,at,lower,upper);var nr=0;
                status=SurfaceIsoPlaneRoots.Find(iso,plane,lower,upper,roots,ref nr,tolerance,strictSeparation,certifiedRoots?isolations:default);if(status!=AlgorithmStatus.Success||nr>1){LastBoundaryFailure=BoundaryFailure.Transverse;goto Retry;}
                if(nr==0)continue;
                status=AddSegment(patch,cell,boundary[i],boundary[i+1],nodes,ref nodeCount,segments,ref segmentCount);if(status!=AlgorithmStatus.Success)return status;
            }
            continue;
            Retry:
            failedBoundary=true;
            if(status is not (AlgorithmStatus.Success or AlgorithmStatus.NotConverged))return status;
            Subdivide:
            nodeCount=savedNodes;segmentCount=savedSegments;
            if(depth>=40||count+2>pending.Length)
            {LastUnresolvedBox=cell;LastUnresolvedDepth=depth;LastUnresolvedFreeAxis=free;return AlgorithmStatus.NotConverged;}
            // A failed section proof can be caused by the graph's dependent
            // coordinate. Repeatedly shrinking only the free coordinate
            // leaves that conditioning unchanged and eventually reaches an
            // unresolvable floating-point strip around a cell corner.
            var split=free>=0&&!failedBoundary?free:Width(cell,0)/Width(patch.Domain,0)>=Width(cell,1)/Width(patch.Domain,1)?0:1;
            var low=split==0?cell.U0:cell.V0;var high=split==0?cell.U1:cell.V1;var middle=low+(high-low)*.4931471805599453;
            if(!(middle>low&&middle<high))return AlgorithmStatus.NotConverged;
            pending[count]=split==0?new(cell.U0,middle,cell.V0,cell.V1):new(cell.U0,cell.U1,cell.V0,middle);depths[count++]=depth+1;
            pending[count]=split==0?new(middle,cell.U1,cell.V0,cell.V1):new(cell.U0,cell.U1,middle,cell.V1);depths[count++]=depth+1;
        }
        return deferIsolatedPoints?AlgorithmStatus.Success:ValidateGraph(nodes[..nodeCount],segments[..segmentCount]);
    }

    internal static AlgorithmStatus ValidateGraph(ReadOnlySpan<SurfaceIntersectionNode> nodes,ReadOnlySpan<SurfaceIntersectionSegment> segments)
    {
        for(var i=0;i<nodes.Length;i++)
        {
            var connected=false;foreach(var segment in segments)if(segment.Start==i||segment.End==i){connected=true;break;}
            if(!connected)return AlgorithmStatus.NotConverged;
        }
        return AlgorithmStatus.Success;
    }

    private static double Width(in SurfaceParameterBox box,BufferOffset axis)=>axis==0?box.U1-box.U0:box.V1-box.V0;
    private static IntervalNumber Distance(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface plane)
        =>AnalyticLevelSet.Value(bounds,plane);
    private static bool Same(in SurfaceIntersectionNode a,in SurfaceIntersectionNode b)
        =>a.RootAxis!=0||b.RootAxis!=0
            ?a.RootAxis==b.RootAxis?a.HasCommonScalarRoot(b):a.U==b.U&&a.V==b.V
            :Math.Abs(a.U-b.U)<2e-10&&Math.Abs(a.V-b.V)<2e-10&&OperationIntersection.Norm(Sub(a.Point,b.Point))<1e-8;

    private static AlgorithmStatus BoundaryNode(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,BufferOffset axis,double at,double parameter,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount count,double tolerance,ScalarRootIsolation isolation=default)
    {
        var u=axis==0?at:parameter;var v=axis==0?parameter:at;var node=new SurfaceIntersectionNode{U=u,V=v};
        if(isolation.Certified){node.Isolation=isolation;node.RootAxis=axis==0?(byte)2:(byte)1;}
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var status=patch.Bound(new(u,u,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        LastBoundaryResidual=Distance(bounds,plane);
        if(isolation.Certified)
        {
            // The sign bracket and monotone derivative already establish a
            // unique root. Keep floating evaluation uncertainty separately
            // from the computed residual and propagate the root box into XYZ.
            if(Math.Abs(LastBoundaryResidual.Midpoint)>tolerance)return AlgorithmStatus.NotConverged;
        }
        else if(LastBoundaryResidual.Magnitude>tolerance)return AlgorithmStatus.NotConverged;
        node.Point=Vector(bounds[0].Midpoint,bounds[1].Midpoint,bounds[2].Midpoint);
        if(isolation.Certified)
        {
            var box=axis==0?new SurfaceParameterBox(at,at,isolation.Root.Lower,isolation.Root.Upper)
                :new(isolation.Root.Lower,isolation.Root.Upper,at,at);
            status=patch.Bound(box,bounds);if(status!=AlgorithmStatus.Success)return status;
            var squared=IntervalNumber.Square(bounds[0]-node.Point.X)+IntervalNumber.Square(bounds[1]-node.Point.Y)+IntervalNumber.Square(bounds[2]-node.Point.Z);
            node.SpatialError=Math.BitIncrement(Math.Sqrt(squared.Upper));
            // Keep the existing ICurve point-on-support budget. This bound
            // controls the discovery witness only; it never welds topology
            // or changes an entity's precision or the caller's fix tolerance.
            if(!double.IsFinite(node.SpatialError)||node.SpatialError>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NotConverged;
        }
        status=OperationIntersection.Parameters(plane,node.Point,out node.S,out node.T);if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<count;i++)if(Same(nodes[i],node))return AlgorithmStatus.Success;
        if(count==nodes.Length)return AlgorithmStatus.OutputTooSmall;nodes[count++]=node;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AddSegment(in SurfaceIntersectionPatch patch,in SurfaceParameterBox box,in SurfaceIntersectionNode a,in SurfaceIntersectionNode b,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount)
    {
        var added=AddNode(a,nodes,ref nodeCount,out var start);if(added!=AlgorithmStatus.Success)return added;
        added=AddNode(b,nodes,ref nodeCount,out var end);if(added!=AlgorithmStatus.Success)return added;
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
        segments[segmentCount++]=new(){Start=start,End=end,ChordalError=error*(1+1e-12)+Math.Max(a.SpatialError,b.SpatialError)+2e-12*Math.Max(1,OperationIntersection.Norm(a.Point))};return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AddNode(in SurfaceIntersectionNode node,Span<SurfaceIntersectionNode> nodes,ref BufferCount count,out BufferOffset index)
    {
        for(var i=0;i<count;i++)if(Same(nodes[i],node)){index=i;return AlgorithmStatus.Success;}
        index=count;if(count==nodes.Length)return AlgorithmStatus.OutputTooSmall;nodes[count++]=node;return AlgorithmStatus.Success;
    }
}
