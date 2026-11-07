using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static void ReuseRollingBoundaryCarriers(scoped ReadOnlySpan<SheetSourceEdge> sources,scoped ReadOnlySpan<sbyte> selected,
        scoped ReadOnlySpan<BufferOffset> heads,scoped ReadOnlySpan<BufferOffset> next,scoped ReadOnlySpan<BufferOffset> owners,scoped ref SheetArrangement model)
    {
        Span<byte> edges=stackalloc byte[model.NE],vertices=stackalloc byte[model.NP];edges.Clear();vertices.Clear();
        for(var e=0;e<model.NE;e++)for(var c=heads[e];c>=0;c=next[c])if(selected[owners[c]]!=0)
        {
            edges[e]=1;var edge=model.Edges[e];if(edge.StartVertex>=0)vertices[edge.StartVertex]=1;if(edge.EndVertex>=0)vertices[edge.EndVertex]=1;break;
        }
        // The working pair-resolution is distinct from each entity's
        // precision. Reuse never changes a stored edge/vertex precision.
        const double resolution=OperationIntersection.DistanceTolerance;
        for(var e=0;e<model.NE;e++)
        {
            var edge=model.Edges[e];if(edges[e]==0||edge.StartVertex<0||edge.EndVertex<0||Curves[GetCurveSlotByTag(edge.Curve)].Class!=CurveClass.ICurve)continue;
            var replacement=-1;var moved=-1;var target=-1;var reversed=false;var spatialError=0.0;var support=0;
            foreach(var source in sources)
            {
                if(source.Source<0||Curves[GetCurveSlotByTag(Edges[source.Source].CurveTag)].Class!=CurveClass.Line
                    ||!LocalEdgeFaces(source.Source,out var firstFace,out var secondFace))continue;
                SurfTag sharedSurface=0;
                for(var c=heads[e];c>=0;c=next[c])if(selected[owners[c]]!=0)
                {
                    var face=model.Sources[owners[c]];if(face==firstFace||face==secondFace)sharedSurface=model.Faces[owners[c]].Surface;
                }
                if(sharedSurface==0)continue;
                for(var l=source.First;l<source.First+source.Count;l++)
                {
                    if(edges[l]!=0)continue;var line=model.Edges[l];
                    if(line.StartVertex<0||line.EndVertex<0||!(line.End>line.Start))continue;
                    var common=line.StartVertex==edge.StartVertex||line.StartVertex==edge.EndVertex?line.StartVertex
                        :line.EndVertex==edge.StartVertex||line.EndVertex==edge.EndVertex?line.EndVertex:-1;
                    if(common<0)continue;
                    var from=common==edge.StartVertex?edge.EndVertex:edge.StartVertex;var to=common==line.StartVertex?line.EndVertex:line.StartVertex;
                    if(from==to||vertices[to]!=0||model.OriginalVertices[from]>=0||model.OriginalVertices[to]>=0
                        ||ProfileDistance(model.Points[from],model.Points[to])>=resolution)continue;
                    // The shared end must continue a surviving occurrence
                    // of this exact source line. The other end is an already
                    // computed source-boundary event, never an XYZ inverse.
                    var continuation=false;
                    for(var k=source.First;k<source.First+source.Count;k++)if(edges[k]!=0)
                        continuation|=model.Edges[k].StartVertex==common||model.Edges[k].EndVertex==common;
                    if(!continuation)continue;
                    var flip=(common==edge.StartVertex)!=(common==line.StartVertex);
                    if(!FiniteLineReuseBound(edge,line,flip,resolution,out var bound)
                        ||!RollingReplacementEndpointFits(ref model,edges,e,from,model.Points[to],resolution))continue;
                    if(replacement>=0){replacement=-2;break;}
                    replacement=l;moved=from;target=to;reversed=flip;spatialError=bound;support=sharedSurface;
                }
                if(replacement==-2)break;
            }
            if(replacement<0)continue;
            var reused=model.Edges[replacement];reused.StartVertex=reversed?edge.EndVertex:edge.StartVertex;reused.EndVertex=reversed?edge.StartVertex:edge.EndVertex;
            model.Edges[e]=reused;model.Points[moved]=model.Points[target];
            if(reversed)for(var c=heads[e];c>=0;c=next[c])model.Coedges[c].Forward^=1;
            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.CarrierReuse,Surface=support,Edge=e,Curve=edge.Curve,ProductCurve=reused.Curve,
                Generation=Curves[GetCurveSlotByTag(edge.Curve)].Header.Generation,First=reused.Start,Last=reused.End,
                StartEvent=reused.StartVertex,EndEvent=reused.EndVertex,SpatialError=spatialError});
        }
    }

    private static bool RollingReplacementEndpointFits(scoped ref SheetArrangement model,scoped ReadOnlySpan<byte> selected,BufferOffset replaced,
        BoundaryEventId endpoint,in KernelVector3 point,double resolution)
    {
        for(var e=0;e<model.NE;e++)if(selected[e]!=0&&e!=replaced)
        {
            var edge=model.Edges[e];if(edge.StartVertex!=endpoint&&edge.EndVertex!=endpoint)continue;
            var curve=Curves[GetCurveSlotByTag(edge.Curve)];var t=(edge.StartVertex==endpoint)==(LocalSenseSign(curve.Sense)>0)?edge.Start:edge.End;
            if(TryReadExplicitSpineCurve(curve,out var exact))
            {
                if(SpineParameterMap.Interval(exact,new IntervalDual(t,0),out var value,out _)!=AlgorithmStatus.Success
                    ||(value-new IntervalVector(point)).Norm().Value.Upper>=resolution)return false;
            }
            else if(curve.Class!=CurveClass.ICurve||CertifiedIntersectionCurveExtent(edge.Curve,t,t,out var extent)!=AlgorithmStatus.Success
                ||(new IntervalVector(new(extent.XLo,extent.XHi),new(extent.YLo,extent.YHi),new(extent.ZLo,extent.ZHi))-new IntervalVector(point)).Norm().Value.Upper>=resolution)return false;
        }
        return true;
    }

    // Monotone orthogonal projection pairs both finite curves. The complete
    // normal enclosure and the two axial endpoint differences bound distance
    // in both directions, including portions past either finite endpoint.
    private static bool FiniteLineReuseBound(in ModelingEdge edge,in ModelingEdge line,bool flip,double resolution,out double error)
    {
        error=0;var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var record=Curves[GetCurveSlotByTag(line.Curve)];var curve=Curves[GetCurveSlotByTag(edge.Curve)];
            if(!TryReadExplicitSpineCurve(record,out var definition))return false;
            var compiler=new NestedProgramCompiler();if(!compiler.Ready||compiler.Spine(edge.Curve,out var spine)!=AlgorithmStatus.Success)return false;
            var program=compiler.View;var origin=new IntervalVector(definition.Origin);var axis=new IntervalVector(definition.First);var length=axis.Norm().Value;
            if(!(length.Lower>0))return false;
            var increasing=(LocalSenseSign(curve.Sense)==LocalSenseSign(record.Sense))!=flip;
            Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var axial=0.0;
            for(var end=0;end<2;end++)
            {
                var t=end==0?edge.Start:edge.End;
                if(JointNestedBlendEvaluation.BoundSpine(program,spine,new(t,t,0,0),bounds)!=AlgorithmStatus.Success)return false;
                var lineT=(end==0)==increasing?line.Start:line.End;
                var displacement=new IntervalVector(bounds[0],bounds[1],bounds[2])-origin-axis*new IntervalDual(lineT,0);
                axial=Math.Max(axial,(IntervalVector.Dot(displacement,axis).Value/length).Magnitude);
            }
            Span<IntervalNumber> pending=stackalloc IntervalNumber[32];pending[0]=new(edge.Start,edge.End);var count=1;var budget=256;
            while(count>0)
            {
                if(--budget<0)return false;var interval=pending[--count];
                if(JointNestedBlendEvaluation.BoundSpine(program,spine,new(interval.Lower,interval.Upper,0,0),bounds)==AlgorithmStatus.Success)
                {
                    var slope=IntervalVector.Dot(new IntervalVector(bounds[3],bounds[4],bounds[5]),axis).Value;
                    var perpendicular=IntervalVector.Cross(new IntervalVector(bounds[0],bounds[1],bounds[2])-origin,axis).Norm().Value/length;
                    var squared=IntervalNumber.Square(perpendicular)+IntervalNumber.Square(new(axial));
                    var distance=Math.BitIncrement(Math.Sqrt(squared.Upper));
                    if((increasing?slope.Lower>0:slope.Upper<0)&&distance<resolution){error=Math.Max(error,distance);continue;}
                }
                var mid=interval.Midpoint;if(count+2>pending.Length||!(mid>interval.Lower&&mid<interval.Upper))return false;
                pending[count++]=new(interval.Lower,mid);pending[count++]=new(mid,interval.Upper);
            }
            return error>0;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }
}
