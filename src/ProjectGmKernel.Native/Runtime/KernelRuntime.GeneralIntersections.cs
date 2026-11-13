using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const BufferCount GeneralIntersectionNodeLimit = 8192;
    private const BufferCount GeneralIntersectionBranchLimit = 64;
    internal enum GeneralIntersectionStage:byte{Discovery,Order,Limits,Bind}
    [ThreadStatic] internal static SurfaceIntersectionNode LastGeneralLimitFirst,LastGeneralLimitLast;
    [ThreadStatic] internal static GeneralIntersectionStage LastGeneralIntersectionStage;
    [ThreadStatic] internal static AlgorithmStatus LastGeneralIntersectionStatus;
    [ThreadStatic] internal static BufferCount LastGeneralIntersectionNodes,LastGeneralIntersectionSegments;

    private static AlgorithmStatus DiscoverGeneralSurfaceIntersections(SurfaceSlot first, SurfaceSlot second,
        Span<SurfaceIntersectionNode> nodes, out BufferCount nn, Span<SurfaceIntersectionSegment> segments, out BufferCount ne,
        PK_SURF_intersect_surf_o_s* options=null,bool separatePlaneSections=false)
    {
        nn = ne = 0;
        if(Surfaces[first].Class==SurfaceClass.BlendSurface||Surfaces[second].Class==SurfaceClass.BlendSurface)
            return DiscoverBlendIntersections(first,second,options,nodes,out nn,segments,out ne);
        if (Surfaces[first].Class != SurfaceClass.BSurface && Surfaces[second].Class != SurfaceClass.BSurface)
            return DiscoverAnalyticSurfaceIntersections(first, second, nodes, out nn, segments, out ne);
        var a = Surfaces[first]; var b = Surfaces[second]; var reverse = a.Class != SurfaceClass.BSurface;
        if (reverse) (a, b) = (b, a);
        if (a.Class != SurfaceClass.BSurface) return AlgorithmStatus.Unsupported;
        var av = GetBSurfaceView(BSurfaceDataStore[a.DataIndex]);
        var bv = b.Class == SurfaceClass.BSurface ? GetBSurfaceView(BSurfaceDataStore[b.DataIndex]) : default;
        var analytic = TryPrepareSurface(b, out var support);
        if (!analytic && b.Class != SurfaceClass.BSurface) return AlgorithmStatus.Unsupported;
        // The paired plane publication path already excluded the plane
        // from every internal join before sending its algebraic witnesses.
        if (av.UDegree > 16 || av.VDegree > 16
            || !GeneralSurfaceContinuity(av)&&!(separatePlaneSections&&analytic&&support.Kind==SurfaceClass.Plane)
            || !analytic && !GeneralSurfaceContinuity(bv)) return AlgorithmStatus.Unsupported;
        var extentStatus = NaturalSurfaceExtent(av,out var extent);if(extentStatus!=AlgorithmStatus.Success)return extentStatus;
        if (analytic && !AnalyticBezierPatches.HasDomain(support, extent)) return AlgorithmStatus.Success;
        Span<IntervalNumber> ap = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> bp = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var analyticCount = analytic ? AnalyticBezierPatches.Count(support) : 0;
        if (analytic && analyticCount == 0) return AlgorithmStatus.Unsupported;
        BufferCount remaining=ParametricSurfaceIntersection.MaxCells;
        for (var u = av.UDegree; u < av.UVertexCount; u++) for (var v = av.VDegree; v < av.VVertexCount; v++)
        {
            if (!(av.UKnots[u + 1] > av.UKnots[u]) || !(av.VKnots[v + 1] > av.VKnots[v])) continue;
            var status = BezierSurfaceBounds.Extract(av, u, v, ap); if (status != AlgorithmStatus.Success) return status;
            var patch = new BezierSurfacePatch(ap[..(4 * (av.UDegree + 1) * (av.VDegree + 1))], av.UDegree, av.VDegree,
                new(av.UKnots[u], av.UKnots[u + 1], av.VKnots[v], av.VKnots[v + 1]));
            var uCount = analytic ? analyticCount : bv.UVertexCount - bv.UDegree;
            var vCount = analytic ? 1 : bv.VVertexCount - bv.VDegree;
            for (var s = 0; s < uCount; s++) for (var t = 0; t < vCount; t++)
            {
                SplineDegree p, q; SurfaceParameterBox domain;
                if (analytic)
                {
                    status = AnalyticBezierPatches.Create(support, extent, s, bp, out p, out q, out domain);
                    if (status != AlgorithmStatus.Success) return status;
                }
                else
                {
                    var su = s + bv.UDegree; var tv = t + bv.VDegree;
                    if (!(bv.UKnots[su + 1] > bv.UKnots[su]) || !(bv.VKnots[tv + 1] > bv.VKnots[tv])) continue;
                    status = BezierSurfaceBounds.Extract(bv, su, tv, bp); if (status != AlgorithmStatus.Success) return status;
                    p = bv.UDegree; q = bv.VDegree; domain = new(bv.UKnots[su], bv.UKnots[su + 1], bv.VKnots[tv], bv.VKnots[tv + 1]);
                }
                var other = new BezierSurfacePatch(bp[..(4 * (p + 1) * (q + 1))], p, q, domain);
                // Analytic chart parameters differ from their native angular
                // parameters; keep a pair-local graph until remapping is done.
                var scratch = CommandScratch.Current; var memory = scratch.Take(150000);
                if (memory.IsEmpty) return AlgorithmStatus.WorkspaceTooSmall;
                try
                {
                    var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
                    var localNodes = ModelSpan<SurfaceIntersectionNode>(bytes, ref cursor, GeneralIntersectionNodeLimit);
                    var localEdges = ModelSpan<SurfaceIntersectionSegment>(bytes, ref cursor, GeneralIntersectionNodeLimit);
                    var nodeMap = ModelSpan<BufferOffset>(bytes, ref cursor, GeneralIntersectionNodeLimit);
                    var nodeMotion=ModelSpan<double>(bytes,ref cursor,GeneralIntersectionNodeLimit);
                    var ln = 0; var le = 0;
                    if(analytic&&support.Kind==SurfaceClass.Plane)
                    {
                        status=SurfacePlaneIntersection.Find(new(patch),support,localNodes,ref ln,localEdges,ref le,ref remaining,deferIsolatedPoints:true,tolerance:2e-14,strictSeparation:true);
                        if(status==AlgorithmStatus.NotConverged)
                        {ln=le=0;status=ParametricSurfaceIntersection.FindProcedural(new(patch),new(other),localNodes,ref ln,localEdges,ref le,ref remaining);}
                    }
                    else status = ParametricSurfaceIntersection.Find(patch, other, localNodes, ref ln, localEdges, ref le);
                    if (status != AlgorithmStatus.Success) return status;
                    if(analytic&&support.Kind==SurfaceClass.Plane)
                        for(var i=0;i<ln;i++)
                        {
                            var original=localNodes[i].Point;
                            status=PolishGeneralPlaneBoundaryAnchor(av,patch,support,ref localNodes[i]);if(status!=AlgorithmStatus.Success)return status;
                            nodeMotion[i]=GeneralAnchorMotionUpper(original,localNodes[i].Point);
                            if(!double.IsFinite(nodeMotion[i]))return AlgorithmStatus.NotConverged;
                        }
                    for (var i = 0; i < ln; i++)
                    {
                        if (analytic)
                        {
                            status = OperationIntersection.Parameters(support, localNodes[i].Point, out localNodes[i].S, out localNodes[i].T);
                            if (status != AlgorithmStatus.Success) return status;
                        }
                        if (reverse) { (localNodes[i].U, localNodes[i].S) = (localNodes[i].S, localNodes[i].U); (localNodes[i].V, localNodes[i].T) = (localNodes[i].T, localNodes[i].V); }
                        var found = -1;
                        for (var j = 0; j < nn; j++) if (GeneralIntersectionSameNode(first, second, nodes[j], localNodes[i])) { found = j; break; }
                        if (found < 0) { if (nn == nodes.Length) return AlgorithmStatus.OutputTooSmall; found = nn; nodes[nn++] = localNodes[i]; }
                        if(analytic&&support.Kind==SurfaceClass.Plane)
                        {
                            var mergedMotion=GeneralAnchorMotionUpper(localNodes[i].Point,nodes[found].Point);
                            if(!double.IsFinite(mergedMotion))return AlgorithmStatus.NotConverged;
                            if(mergedMotion>0)nodeMotion[i]=(new IntervalNumber(nodeMotion[i])+mergedMotion).Upper;
                        }
                        nodeMap[i] = found;
                    }
                    for (var i = 0; i < le; i++)
                    {
                        var start = nodeMap[localEdges[i].Start]; var end = nodeMap[localEdges[i].End];
                        var duplicate = false;
                        for (var j = 0; j < ne; j++) if (segments[j].Start == start && segments[j].End == end || segments[j].Start == end && segments[j].End == start) duplicate = true;
                        if (duplicate) continue;
                        if (ne == segments.Length) return AlgorithmStatus.OutputTooSmall;
                        var error=localEdges[i].ChordalError;
                        if(analytic&&support.Kind==SurfaceClass.Plane)
                        {
                            // Linear interpolation of the endpoint motions
                            // bounds the Hausdorff distance between the chords.
                            var motion=Math.Max(nodeMotion[localEdges[i].Start],nodeMotion[localEdges[i].End]);
                            if(motion>0)error=(new IntervalNumber(error)+motion).Upper;
                            if(!double.IsFinite(error))return AlgorithmStatus.NotConverged;
                        }
                        segments[ne++] = new() { Start = start, End = end,ChordalError=error };
                    }
                }
                finally { scratch.Return(memory); }
            }
        }
        return analytic&&support.Kind==SurfaceClass.Plane?SurfacePlaneIntersection.ValidateGraph(nodes[..nn],segments[..ne]):AlgorithmStatus.Success;
    }

    private static double GeneralAnchorMotionUpper(in KernelVector3 original,in KernelVector3 current)
    {
        var dx=new IntervalNumber(current.X)-original.X;var dy=new IntervalNumber(current.Y)-original.Y;var dz=new IntervalNumber(current.Z)-original.Z;
        var squared=IntervalNumber.Square(dx)+IntervalNumber.Square(dy)+IntervalNumber.Square(dz);
        return !squared.Finite?double.NaN:squared.Upper>0?Math.BitIncrement(Math.Sqrt(squared.Upper)):0;
    }

    // Keep natural-boundary anchors on that exact boundary while reducing
    // their plane residual. The connecting interval must stay in the same
    // Bezier span and have a nonzero derivative; discovery owns completeness.
    private static AlgorithmStatus PolishGeneralPlaneBoundaryAnchor(in BSurfaceView surface,in BezierSurfacePatch patch,in AnalyticSurface plane,ref SurfaceIntersectionNode node)
    {
        var fixedU=node.U==surface.UStart||node.U==surface.UEnd;var fixedV=node.V==surface.VStart||node.V==surface.VEnd;
        if(fixedU==fixedV)return AlgorithmStatus.Success;
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);
        Span<double> workspace=stackalloc double[(BufferCount)BSurfaceEvaluation.WorkspaceSize(surface.UDegree,surface.VDegree,layout.UOrder,layout.VOrder)];
        Span<KernelVector3> jets=stackalloc KernelVector3[4];Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        var source=new SurfaceIntersectionPatch(patch);var coordinate=fixedU?node.V:node.U;
        for(var iteration=0;iteration<8;iteration++)
        {
            var status=BSurfaceEvaluation.Evaluate(surface,node.U,node.V,layout,workspace,jets);if(status!=AlgorithmStatus.Success)return status;
            if(!SplinePlaneResidual.TryEvaluate(surface,plane,node.U,node.V,out var residual))return AlgorithmStatus.NotConverged;
            var derivative=Dot(plane.Axis,jets[layout.GetIndex(fixedU?0:1,fixedU?1:0)]);if(derivative==0)break;
            var next=coordinate-residual/derivative;var lower=fixedU?patch.Domain.V0:patch.Domain.U0;var upper=fixedU?patch.Domain.V1:patch.Domain.U1;
            if(!double.IsFinite(next)||next==coordinate||next<lower||next>upper)break;
            var between=fixedU?new SurfaceParameterBox(node.U,node.U,Math.Min(coordinate,next),Math.Max(coordinate,next))
                :new(Math.Min(coordinate,next),Math.Max(coordinate,next),node.V,node.V);
            status=source.Bound(between,bound);if(status!=AlgorithmStatus.Success)return status;
            var offset=fixedU?6:3;var slope=bound[offset]*plane.Axis.X+bound[offset+1]*plane.Axis.Y+bound[offset+2]*plane.Axis.Z;if(slope.ContainsZero)break;
            if(!SplinePlaneResidual.TryEvaluate(surface,plane,fixedU?node.U:next,fixedU?next:node.V,out var trial)||Math.Abs(trial)>=Math.Abs(residual))break;
            coordinate=next;if(fixedU)node.V=next;else node.U=next;
        }
        var final=BSurfaceEvaluation.Evaluate(surface,node.U,node.V,layout,workspace,jets);if(final!=AlgorithmStatus.Success)return final;
        final=source.Bound(new(node.U,node.U,node.V,node.V),bound);if(final!=AlgorithmStatus.Success)return final;
        var distance=(bound[0]-plane.Origin.X)*plane.Axis.X+(bound[1]-plane.Origin.Y)*plane.Axis.Y+(bound[2]-plane.Origin.Z)*plane.Axis.Z;
        if(distance.Magnitude>2e-14&&BezierPlaneResidual.TryBound(patch,plane,new(node.U,node.U,node.V,node.V),out var composed))
            distance=new(Math.Max(distance.Lower,composed.Lower),Math.Min(distance.Upper,composed.Upper));
        if(!distance.Finite||distance.Magnitude>2e-14)return AlgorithmStatus.NotConverged;
        node.Point=jets[0];return OperationIntersection.Parameters(plane,node.Point,out node.S,out node.T);
    }

    private static bool GeneralSurfaceContinuity(in BSurfaceView surface)
    {
        for (var d = 0; d < 2; d++)
        {
            var knots = d == 0 ? surface.UKnots : surface.VKnots; var degree = d == 0 ? surface.UDegree : surface.VDegree;
            var count = d == 0 ? surface.UVertexCount : surface.VVertexCount;
            for (var i = degree + 1; i < count;)
            {
                var j = i + 1; while (j < count && knots[j] == knots[i]) j++;
                if (j - i >= degree) return false; i = j;
            }
        }
        return true;
    }

    // Only consumers which enumerate the native knot spans may use this box.
    private static AlgorithmStatus NaturalSurfaceExtent(in BSurfaceView view,out IntervalBox3 extent)
        =>BSurfaceDomainBounds.Position(view,new(view.UStart,view.UEnd,view.VStart,view.VEnd),false,out extent);

    private static bool GeneralIntersectionSameNode(SurfaceSlot first, SurfaceSlot second, in SurfaceIntersectionNode a, in SurfaceIntersectionNode b)
    {
        if((a.RootAxis!=0||b.RootAxis!=0)&&!a.RemappedParameters&&!b.RemappedParameters)
            return a.RootAxis==b.RootAxis?a.HasCommonScalarRoot(b):a.U==b.U&&a.V==b.V&&a.S==b.S&&a.T==b.T;
        if (OperationIntersection.Norm(Sub(a.Point, b.Point)) > 2e-8) return false;
        for (var d = 0; d < 4; d++)
        {
            var record = Surfaces[d < 2 ? first : second]; var difference = a.Parameter(d) - b.Parameter(d); var scale = 1.0;
            if (record.Class == SurfaceClass.BSurface)
            {
                var view = GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);
                var period = (d & 1) == 0 ? view.UEnd - view.UStart : view.VEnd - view.VStart;
                var periodic = (d & 1) == 0 ? view.UPeriodic : view.VPeriodic;
                if (periodic) difference -= period * Math.Round(difference / period); scale = Math.Max(1, period);
            }
            else if(record.Class==SurfaceClass.BlendSurface&&(d&1)==0)
            {
                var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
                if(spine.StartType==LimitType.Help&&spine.EndType==LimitType.Help&&Dot(Sub(charts[0].Position,charts[^1].Position),Sub(charts[0].Position,charts[^1].Position))==0)
                {var period=record.UMax-record.UMin;difference-=period*Math.Round(difference/period);}
            }
            else if ((d & 1) == 0 && record.Class is SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus || (d & 1) != 0 && record.Class == SurfaceClass.Torus)
                difference -= Math.Tau * Math.Round(difference / Math.Tau);
            if (Math.Abs(difference) > 2e-9 * scale) return false;
        }
        return true;
    }

    private struct GeneralIntersectionBranch
    {
        internal BufferOffset First;
        internal BufferCount Count;
        internal byte Closed;
        internal double ChordalError;
    }

    private static AlgorithmStatus OrderGeneralIntersectionGraph(ReadOnlySpan<SurfaceIntersectionNode> nodes,
        ReadOnlySpan<SurfaceIntersectionSegment> edges, Span<BufferOffset> ordered, Span<GeneralIntersectionBranch> branches,
        out BufferCount branchCount)
    {
        branchCount = 0;
        var scratch = CommandScratch.Current; var memory = scratch.Take(nodes.Length * 2 + edges.Length);
        if (memory.IsEmpty) return AlgorithmStatus.WorkspaceTooSmall;
        try
        {
            var ints = MemoryMarshal.Cast<double, int>(memory); var degrees = ints[..nodes.Length]; degrees.Clear();
            var used = ints.Slice(nodes.Length, edges.Length); used.Clear();
            foreach (var edge in edges) { degrees[edge.Start]++; degrees[edge.End]++; }
            for (var i = 0; i < nodes.Length; i++) if (degrees[i] > 2 || degrees[i] == 0) return AlgorithmStatus.NotConverged;
            var position = 0;
            for (var pass = 0; pass < 2; pass++) for (var seed = 0; seed < nodes.Length; seed++)
            {
                if (pass == 0 && degrees[seed] != 1 || pass == 1 && degrees[seed] != 2) continue;
                var available = false; for (var e = 0; e < edges.Length; e++) if (used[e] == 0 && (edges[e].Start == seed || edges[e].End == seed)) available = true;
                if (!available) continue;
                if (branchCount == branches.Length) return AlgorithmStatus.OutputTooSmall;
                var first = position; var node = seed; var closed = false;var chordalError=0.0;
                while (true)
                {
                    if (position == ordered.Length) return AlgorithmStatus.OutputTooSmall; ordered[position++] = node;
                    var nextEdge = -1;
                    for (var e = 0; e < edges.Length; e++) if (used[e] == 0 && (edges[e].Start == node || edges[e].End == node)) { nextEdge = e; break; }
                    if (nextEdge < 0) break;
                    chordalError=Math.Max(chordalError,edges[nextEdge].ChordalError);
                    used[nextEdge] = 1; node = edges[nextEdge].Start == node ? edges[nextEdge].End : edges[nextEdge].Start;
                    if (node == seed)
                    {
                        if (position == ordered.Length) return AlgorithmStatus.OutputTooSmall; ordered[position++] = seed; closed = true; break;
                    }
                }
                if (position - first < 2) return AlgorithmStatus.NotConverged;
                branches[branchCount++] = new() { First = first, Count = position - first, Closed = closed ? (byte)1 : (byte)0,ChordalError=chordalError };
            }
            return AlgorithmStatus.Success;
        }
        finally { scratch.Return(memory); }
    }

    private static int GeneralSurfIntersectSurf(SurfaceSlot first, SurfaceSlot second, BodySlot owner,
        PK_SURF_intersect_surf_o_s* options, BufferCount* curveCount, CurveTag** curves, PK_INTERVAL_s** intervals, int** kinds,
        bool publishNativeSection=false,ReadOnlySpan<OperationIntersectionBranch> planeSections=default)
    {
        // Store graph order as indices. A second maximum-sized node array
        // would reserve certificate storage twice and exhaust the command
        // arena when discovery is nested inside Boolean or imprint.
        var required=GeneralIntersectionNodeLimit*(sizeof(SurfaceIntersectionNode)+sizeof(SurfaceIntersectionSegment))
            +(GeneralIntersectionNodeLimit+GeneralIntersectionBranchLimit)*sizeof(BufferOffset)+GeneralIntersectionBranchLimit*sizeof(GeneralIntersectionBranch);
        var scratch = CommandScratch.Current; var memory = scratch.Take((required+7)/8);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var nodes = ModelSpan<SurfaceIntersectionNode>(bytes, ref cursor, GeneralIntersectionNodeLimit);
            var edges = ModelSpan<SurfaceIntersectionSegment>(bytes, ref cursor, GeneralIntersectionNodeLimit);
            var ordered = ModelSpan<BufferOffset>(bytes, ref cursor, GeneralIntersectionNodeLimit + GeneralIntersectionBranchLimit);
            var branches = ModelSpan<GeneralIntersectionBranch>(bytes, ref cursor, GeneralIntersectionBranchLimit);
            LastGeneralIntersectionStage=GeneralIntersectionStage.Discovery;
            var status = DiscoverGeneralSurfaceIntersections(first, second, nodes, out var nn, edges, out var ne,options,!planeSections.IsEmpty);
            LastGeneralIntersectionStatus=status;LastGeneralIntersectionNodes=nn;LastGeneralIntersectionSegments=ne;
            if (status != AlgorithmStatus.Success)return IntersectionError(status);
            // L limits describe an artificial chart extent. An open pair
            // cannot certify completeness of the continuation beyond it.
            var unboundedBlendPair=Surfaces[first].Class==SurfaceClass.BlendSurface&&Surfaces[second].Class==SurfaceClass.BlendSurface
                &&(options->have_uvbox_1==0||options->have_uvbox_2==0)&&(BlendHasArtificialLimit(first)||BlendHasArtificialLimit(second));
            if (ne == 0) return unboundedBlendPair||!planeSections.IsEmpty?ParasolidConstants.PK_ERROR_cant_do_intersect:0;
            LastGeneralIntersectionStage=GeneralIntersectionStage.Order;
            status = OrderGeneralIntersectionGraph(nodes[..nn], edges[..ne], ordered, branches, out var nb);LastGeneralIntersectionStatus=status;
            if (status != AlgorithmStatus.Success)return IntersectionError(status);
            LastGeneralIntersectionStage=GeneralIntersectionStage.Limits;
            for (var i = 0; i < nb; i++)
            {
                ref readonly var branch = ref branches[i];
                if (branch.Closed != 0) continue;
                LastGeneralLimitFirst=nodes[ordered[branch.First]];LastGeneralLimitLast=nodes[ordered[branch.First+branch.Count-1]];
                if(unboundedBlendPair)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                if (!GeneralIntersectionBoundary(first, second, nodes[ordered[branch.First]],options)
                    || !GeneralIntersectionBoundary(first, second, nodes[ordered[branch.First + branch.Count - 1]],options))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
            }
            if(!planeSections.IsEmpty&&!MatchFinitePlaneSections(planeSections,nodes,ordered,branches[..nb]))
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if (options->have_vector != 0)
            {
                var seed = Vector(options->vector.coord[0], options->vector.coord[1], options->vector.coord[2]);
                if (!IsFinite(seed)) return ParasolidConstants.PK_ERROR_bad_parameter;
                var selected = -1;
                for (var i = 0; i < nb; i++)
                {
                    ref readonly var branch = ref branches[i];
                    var candidateError = CreateGeneralIntersectionCurve(Surfaces[first].Header.Tag, Surfaces[second].Header.Tag,
                        nodes[..nn],ordered.Slice(branch.First, branch.Count), branch.Closed != 0, out var candidate, branch.ChordalError, ownsSupports:false);
                    if (candidateError != 0) return candidateError;
                    double parameter = 0;
                    candidateError = CurveParameteriseVectorCore(candidate, options->vector, &parameter);
                    var deleted = EntityDelete(1, &candidate);
                    if (deleted != 0) return deleted;
                    if (candidateError == ParasolidConstants.PK_ERROR_not_on_curve) continue;
                    if (candidateError != 0) return candidateError;
                    if (selected >= 0) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    selected = i;
                }
                if (selected < 0) return ParasolidConstants.PK_ERROR_not_on_curve;
                branches[0] = branches[selected]; nb = 1;
            }
            LastGeneralIntersectionStage=GeneralIntersectionStage.Bind;
            var preferEquator=publishNativeSection&&(Surfaces[first].Class==SurfaceClass.BSurface&&Surfaces[second].Class==SurfaceClass.Torus
                ||Surfaces[first].Class==SurfaceClass.Torus&&Surfaces[second].Class==SurfaceClass.BSurface);
            SurfTag a=0,b=0;
            if(!preferEquator)
            {
                var clone=TryCloneEntityCore(Surfaces[first].Header.Tag,out a);if(clone!=0)return clone;
                clone=TryCloneEntityCore(Surfaces[second].Header.Tag,out b);if(clone!=0)return clone;
                if(!planeSections.IsEmpty&&!NormalizePlaneSectionSupport(Surfaces[first].Class==SurfaceClass.BSurface?a:b))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
            }
            var tags = AllocateReturnSlice(nb);
            var ranges = (PK_INTERVAL_s*)State.Session->Returns.TryAllocate((nuint)(nb * sizeof(PK_INTERVAL_s)));
            var types = AllocateReturnSlice(nb);
            if (tags == null || ranges == null || types == null)
            { State.Session->Returns.TryFree(tags); State.Session->Returns.TryFree(ranges); State.Session->Returns.TryFree(types); return ParasolidConstants.PK_ERROR_memory_full; }
            for (var i = 0; i < nb; i++)
            {
                ref readonly var branch = ref branches[i];
                var order=ordered.Slice(branch.First,branch.Count);int error;
                OperationIntersectionBranch circle=default;AnalyticSurface firstSupport=default,secondSupport=default;
                var exact=preferEquator&&TryNativeAffineTorusEquator(first,second,nodes,order,branch,out circle,out firstSupport,out secondSupport);
                if(exact)
                {
                    error=CreateIntersectionBranch(circle,tags+i);
                    if(error==0)error=OrientAnalyticIntersectionCurve(tags[i],circle.Start,circle.End,firstSupport,secondSupport,
                        Surfaces[first].Sense,Surfaces[second].Sense);
                }
                else
                {
                    error=0;
                    if(a==0)
                    {
                        error=TryCloneEntityCore(Surfaces[first].Header.Tag,out a);
                        if(error==0)error=TryCloneEntityCore(Surfaces[second].Header.Tag,out b);
                    }
                    if(error==0)error=CreateGeneralIntersectionCurve(a,b,nodes[..nn],order,branch.Closed!=0,out tags[i],branch.ChordalError);
                }
                if (error == 0 && owner >= 0) error = TryRegisterConstructionGeometry(owner, tags[i]);
                if (error != 0)
                { State.Session->Returns.TryFree(tags); State.Session->Returns.TryFree(ranges); State.Session->Returns.TryFree(types); return error; }
                var record = Curves[GetCurveSlotByTag(tags[i])];
                ranges[i].value[0]=exact?circle.Start:record.TMin;ranges[i].value[1]=exact?circle.End:record.TMax;
                types[i] = ParasolidConstants.PK_intersect_curve_simple_c;
            }
            *curveCount = nb; *curves = tags; *intervals = ranges; *kinds = types; return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static bool NormalizePlaneSectionSupport(SurfTag support)
    {
        var data=BSurfaceDataStore[Surfaces[GetSurfaceSlotByTag(support)].DataIndex];
        if(data.IsRational==0)return true;
        var poles=data.PolesSpan();var weight=0.0;
        for(var i=3;i<poles.Length;i+=4)weight=Math.Max(weight,poles[i]);
        if(!(weight>0)||!double.IsFinite(weight))return false;
        var exponent=Math.ILogB(weight);
        if(exponent==0)return true;
        // The new curve owns this copy. Normalize its common homogeneous
        // gauge exactly, so XT retains the support even for tiny input weights.
        for(var i=0;i<poles.Length;i++)
        {
            var scaled=Math.ScaleB(poles[i],-exponent);
            if(!double.IsFinite(scaled)||Math.ScaleB(scaled,exponent)!=poles[i])return false;
            poles[i]=scaled;
        }
        return true;
    }

    private static bool MatchFinitePlaneSections(ReadOnlySpan<OperationIntersectionBranch> sections,
        ReadOnlySpan<SurfaceIntersectionNode> nodes,ReadOnlySpan<BufferOffset> order,ReadOnlySpan<GeneralIntersectionBranch> branches)
    {
        if(sections.Length!=branches.Length)return false;
        ulong used=0;
        foreach(ref readonly var branch in branches)
        {
            if(branch.Closed!=0)return false;
            var first=nodes[order[branch.First]].Point;var last=nodes[order[branch.First+branch.Count-1]].Point;var match=-1;
            for(var i=0;i<sections.Length;i++)
            {
                var a=PlaneSectionEndpoint(sections[i],false);var b=PlaneSectionEndpoint(sections[i],true);
                if(!(SameEnd(first,a)&&SameEnd(last,b)||SameEnd(first,b)&&SameEnd(last,a)))continue;
                if(match>=0||(used&(1UL<<i))!=0)return false;
                match=i;
            }
            if(match<0)return false;
            used|=1UL<<match;
        }
        return true;

        static bool SameEnd(in KernelVector3 a,in KernelVector3 b)=>GeneralAnchorMotionUpper(a,b)<=OperationIntersection.DistanceTolerance;
    }

    private static bool GeneralIntersectionBoundary(SurfaceSlot first, SurfaceSlot second, in SurfaceIntersectionNode node,PK_SURF_intersect_surf_o_s* options=null)
    {
        for (var i = 0; i < 2; i++)
        {
            var record = Surfaces[i == 0 ? first : second];
            if(record.Class==SurfaceClass.BlendSurface||record.Class==SurfaceClass.BSurface&&options!=null&&(i==0?options->have_uvbox_1:options->have_uvbox_2)!=0)
            {
                var box=BlendIntersectionDomain(record,options,i==0);var paramU=i==0?node.U:node.S;var paramV=i==0?node.V:node.T;
                var lower=Math.Abs(paramU-box.U0)<=2e-9*Math.Max(1,box.U1-box.U0);var upper=Math.Abs(paramU-box.U1)<=2e-9*Math.Max(1,box.U1-box.U0);
                if(record.Class==SurfaceClass.BlendSurface&&(options==null||(i==0?options->have_uvbox_1:options->have_uvbox_2)==0))
                {
                    var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
                    lower&=spine.StartType!=LimitType.Artificial;upper&=spine.EndType!=LimitType.Artificial;
                }
                if(lower||upper||Math.Abs(paramV-box.V0)<=2e-9*Math.Max(1,box.V1-box.V0)||Math.Abs(paramV-box.V1)<=2e-9*Math.Max(1,box.V1-box.V0))return true;
                continue;
            }
            if (record.Class != SurfaceClass.BSurface) continue;
            var view = GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);
            var u = i == 0 ? node.U : node.S; var v = i == 0 ? node.V : node.T;
            if (!view.UPeriodic && (Math.Abs(u-view.UStart)<=2e-10*(view.UEnd-view.UStart)
                || Math.Abs(u-view.UEnd)<=2e-10*(view.UEnd-view.UStart))) return true;
            if (!view.VPeriodic && (Math.Abs(v-view.VStart)<=2e-10*(view.VEnd-view.VStart)
                || Math.Abs(v-view.VEnd)<=2e-10*(view.VEnd-view.VStart))) return true;
        }
        return false;
    }

    private static int CreateGeneralIntersectionCurve(SurfTag first,SurfTag second,ReadOnlySpan<SurfaceIntersectionNode> nodes,ReadOnlySpan<BufferOffset> order,
        bool closed,out CurveTag curve,double chordalError=0,double angularError=Math.PI,bool ownsSupports=true)
    {
        curve=0;var scratch=CommandScratch.Current;var memory=scratch.Take((order.Length*sizeof(SurfaceIntersectionNode)+7)/8);
        if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var samples=MemoryMarshal.Cast<double,SurfaceIntersectionNode>(memory)[..order.Length];
            for(var i=0;i<order.Length;i++)samples[i]=nodes[order[i]];
            return CreateGeneralIntersectionCurve(first,second,samples,closed,out curve,chordalError,angularError,ownsSupports);
        }
        finally{scratch.Return(memory);}
    }

    private static int CreateGeneralIntersectionCurve(SurfTag first, SurfTag second, Span<SurfaceIntersectionNode> samples,
        bool closed, out CurveTag curve,double chordalError=0,double angularError=Math.PI,bool ownsSupports=true)
    {
        curve = 0;
        var checkpoint = CommandScratch.ThreadArenaOffset;
        try
        {
            SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
            Span<KernelVector3> a = stackalloc KernelVector3[4]; Span<KernelVector3> b = stackalloc KernelVector3[4];
            var firstSlot = GetSurfaceSlotByTag(first); var secondSlot = GetSurfaceSlotByTag(second);
            var status = EvaluateSurfaceCore(in Surfaces[firstSlot], samples[0].U, samples[0].V, in layout, a, 0);
            if (status != AlgorithmStatus.Success) return EvaluationError(status);
            status = EvaluateSurfaceCore(in Surfaces[secondSlot], samples[0].S, samples[0].T, in layout, b, 0);
            if (status != AlgorithmStatus.Success) return EvaluationError(status);
            var n0 = ProfileScale(ProfileCross(a[layout.GetIndex(1, 0)], a[layout.GetIndex(0, 1)]), LocalSenseSign(Surfaces[firstSlot].Sense));
            var n1 = ProfileScale(ProfileCross(b[layout.GetIndex(1, 0)], b[layout.GetIndex(0, 1)]), LocalSenseSign(Surfaces[secondSlot].Sense));
            if (Dot(ProfileCross(n0, n1), Sub(samples[1].Point, samples[0].Point)) < 0) samples.Reverse();
            var scratch = CommandScratch.Current;
            var chart = scratch.Take(3 * samples.Length); var uv = scratch.Take(4 * samples.Length);
            if (chart.IsEmpty || uv.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                status = EvaluateSurfaceCore(in Surfaces[firstSlot], sample.U, sample.V, in layout, a, 0);
                if (status != AlgorithmStatus.Success) return EvaluationError(status);
                status = EvaluateSurfaceCore(in Surfaces[secondSlot], sample.S, sample.T, in layout, b, 0);
                if (status != AlgorithmStatus.Success) return EvaluationError(status);
                if (OperationIntersection.Norm(Sub(a[0], b[0])) > 1e-8) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                chart[3 * i] = a[0].X; chart[3 * i + 1] = a[0].Y; chart[3 * i + 2] = a[0].Z;
                uv[4 * i] = sample.U; uv[4 * i + 1] = sample.V; uv[4 * i + 2] = sample.S; uv[4 * i + 3] = sample.T;
            }
            if(Surfaces[firstSlot].Class==SurfaceClass.BlendSurface||Surfaces[secondSlot].Class==SurfaceClass.BlendSurface)
            {
                var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
                status=compiler.Surface(first,out var firstInstruction);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                status=compiler.Surface(second,out var secondInstruction);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                var positions=MemoryMarshal.Cast<double,KernelVector3>(chart);
                status=compiler.ChartSpine(firstInstruction,secondInstruction,positions,0,1,Math.Max(chordalError,1e-12),angularError,closed?LimitType.Help:LimitType.Artificial,closed?LimitType.Help:LimitType.Artificial,out var spine,knownUv:uv);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                if(!closed)
                {
                    status=compiler.RefinePlaneRollingChart(ref spine,positions,uv);
                    if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                }
                CurveTag created=0;var error=BindNestedBlendSpine(first,second,compiler.View,spine,&created,uv);if(error!=0)return error;
                ICurveDataPool[Curves[GetCurveSlotByTag(created)].DataIndex].OwnsSupportSurfaces=ownsSupports?(byte)1:(byte)0;curve=created;return 0;
            }
            var input = new IcurveDecodeInput { Surface0Tag = first, Surface1Tag = second,
                BaseScale = 1, ChartCount = samples.Length, ChartHvecs = chart, UvType = IntersectionUvType.Both, UvValues = uv,
                ChordalError=Math.Max(chordalError,1e-12),AngularError=angularError,
                Start = new() { Type = closed ? LimitType.Help : LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[..3] },
                End = new() { Type = closed ? LimitType.Help : LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[^3..] },
                Sense = ParasolidConstants.PK_TOPOL_sense_positive_c };
            status = DecodeIcurve(input, out var data, out _, out _); if (status != AlgorithmStatus.Success) return IntersectionError(status);
            status = TryBindICurveEntity(data, out curve); if (status != AlgorithmStatus.Success) return IntersectionError(status);
            ICurveDataPool[data].OwnsSupportSurfaces = ownsSupports ? (byte)1 : (byte)0; return 0;
        }
        finally { CommandScratch.ThreadArenaOffset = checkpoint; }
    }
}
