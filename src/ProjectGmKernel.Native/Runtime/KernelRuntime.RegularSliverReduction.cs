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
    private struct PlanarSliver
    {
        internal BufferOffset Cell,PlaneFace,WallFace,CrossFace;
        internal BufferOffset PlaneSide,WallSide,CrossSide;
        internal BoundaryEventId Original,PlaneEnd,WallEnd;
    }

    private static int ReduceProjectedRollingCells(BodySlot target,in RegularBoundaryView input,ReadOnlySpan<FaceSlot> inputSources,
        ref SheetArrangement model,Span<byte> selected,ReadOnlySpan<byte> reversed,Span<SheetCutCurve> owned,ref BufferCount ownedCount,
        Span<SheetHalfedge> halfedges,Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles)
    {
        if(model.TangencyCount==0)return 0;
        // Reserve the rest of the working resolution for the projected
        // boundary reattachment and the two adjacent face representations.
        var precision=model.Tolerance*.25;
        Span<ModelingCoedge> boundary=stackalloc ModelingCoedge[SheetEdgeLimit];
        Span<BufferOffset> involved=stackalloc BufferOffset[6];
        for(var f=0;f<model.NF;f++)
        {
            if(!FindPlanarSliver(model,selected,reversed,target,f,out var cell))continue;
            var flat=model.Faces[cell.PlaneFace].Surface;var wall=model.Faces[cell.WallFace].Surface;var cross=model.Faces[cell.CrossFace].Surface;
            if(!RegularAnalyticPlane(flat,out var a)||!RegularAnalyticPlane(wall,out var b)||!RegularAnalyticPlane(cross,out var c))continue;
            var original=model.Points[cell.Original];
            if(!ExactProjectionEqual(original,a.Origin,a.Axis)||!ExactProjectionEqual(original,b.Origin,b.Axis)
                ||!CertifyPlanarSliver(model,cell,a,b,c,precision,out var footprint)
                ||!TryProjectedPlaneVertex(a,b,c,original,out var corner)
                ||!SliverOuterEdge(model,selected,cell,cell.PlaneEnd,cell.PlaneFace,cell.CrossFace,out var planeEdge)
                ||!SliverOuterEdge(model,selected,cell,cell.WallEnd,cell.WallFace,cell.CrossFace,out var wallEdge)
                ||!SliverOuterEdge(model,selected,cell,cell.Original,cell.PlaneFace,cell.WallFace,out var continuation)
                ||!SliverLeavesOriginalLine(model,continuation,cell.Original,a,b,corner)
                ||!TryRetargetSliverLine(model.Edges[planeEdge],cell.PlaneEnd,a,c,corner,precision,out var newPlaneEdge,out var planeAtStart)
                ||!TryRetargetSliverLine(model.Edges[wallEdge],cell.WallEnd,b,c,corner,precision,out var newWallEdge,out var wallAtStart))continue;
            var wallInput=inputSources.IndexOf(model.Sources[cell.WallFace]);var crossInput=inputSources.IndexOf(model.Sources[cell.CrossFace]);
            if(wallInput<0||crossInput<0||!SliverInsideToolTrim(input,wallInput,crossInput,b,c,footprint))continue;
            involved[0]=cell.PlaneSide;involved[1]=cell.WallSide;involved[2]=cell.CrossSide;
            involved[3]=planeEdge;involved[4]=wallEdge;involved[5]=continuation;
            if(!SliverExcludesOtherBoundaries(model,selected,involved,footprint))continue;

            var point=Vector(corner.X.Value.Midpoint,corner.Y.Value.Midpoint,corner.Z.Value.Midpoint);
            var direction=Sub(point,original);var length=OperationIntersection.Norm(direction);
            if(!(length>0)||!double.IsFinite(length))continue;
            if(ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_memory_full;
            var error=model.AddPoint(point,-1,out var endpoint,requireProof:true);if(error!=0)return error;
            direction=Scale(direction,1/length);
            var curve=CreateLineCurveTag(original.X,original.Y,original.Z,direction.X,direction.Y,direction.Z);
            if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;
            owned[ownedCount++]=new(){Curve=curve};
            error=model.AppendEdge(curve,0,length,cell.Original,endpoint,true,out var replacement);if(error!=0)return error;
            if(planeAtStart)newPlaneEdge.StartVertex=endpoint;else newPlaneEdge.EndVertex=endpoint;
            if(wallAtStart)newWallEdge.StartVertex=endpoint;else newWallEdge.EndVertex=endpoint;
            model.Edges[planeEdge]=newPlaneEdge;model.Edges[wallEdge]=newWallEdge;
            error=RearrangeSliverNeighbor(ref model,cell.PlaneFace,cell.PlaneSide,replacement,cell.Original,boundary,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            error=RearrangeSliverNeighbor(ref model,cell.WallFace,cell.WallSide,replacement,cell.Original,boundary,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            error=RearrangeSliverNeighbor(ref model,cell.CrossFace,cell.CrossSide,-1,cell.Original,boundary,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            selected[f]=0;
            var sourceCurve=model.Edges[cell.WallSide].Curve;
            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.ProjectedCellReplacement,Source=model.Sources[f],Face=f,Surface=model.Faces[f].Surface,
                OtherSurface=flat,Curve=sourceCurve,ProductCurve=curve,Edge=replacement,Generation=Curves[GetCurveSlotByTag(sourceCurve)].Header.Generation,
                StartEvent=cell.Original,EndEvent=endpoint,First=0,Last=length,StartPoint=original,EndPoint=point,SpatialError=precision});
        }
        return 0;
    }

    private static bool FindPlanarSliver(in SheetArrangement model,ReadOnlySpan<byte> selected,ReadOnlySpan<byte> reversed,
        BodySlot target,BufferOffset cell,out PlanarSliver candidate)
    {
        candidate=default;var face=model.Faces[cell];
        if(selected[cell]==0||reversed[cell]!=0||face.LoopCount!=1||Surfaces[GetSurfaceSlotByTag(face.Surface)].Class!=SurfaceClass.BlendSurface
            ||SourceBody(model.Sources[cell])!=target)return false;
        var loop=model.Loops[face.FirstLoop];if(loop.CoedgeCount!=3)return false;
        var sides=model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount);
        for(var i=0;i<3;i++)
        {
            var a=sides[i];var b=sides[(i+1)%3];var c=sides[(i+2)%3];
            var ea=model.Edges[a.Edge];var eb=model.Edges[b.Edge];
            var original=SliverEnd(ea,a.Forward);var pa=SliverStart(ea,a.Forward);var pb=SliverEnd(eb,b.Forward);
            if(original<0||pa<0||pb<0||pa==pb||model.OriginalVertices[original]<0||model.OriginalVertices[pa]>=0||model.OriginalVertices[pb]>=0
                ||original!=SliverStart(eb,b.Forward)||pb!=SliverStart(model.Edges[c.Edge],c.Forward)||pa!=SliverEnd(model.Edges[c.Edge],c.Forward)
                ||!model.KnownTangency(original,ea.Curve,eb.Curve,face.Surface))continue;
            if(!SliverNeighbor(model,selected,reversed,cell,a,out var fa)||!SliverNeighbor(model,selected,reversed,cell,b,out var fb)
                ||!SliverNeighbor(model,selected,reversed,cell,c,out var fc)||fa==fb||fa==fc||fb==fc)continue;
            if(SourceBody(model.Sources[fb])==target){(fa,fb)=(fb,fa);(a,b)=(b,a);(pa,pb)=(pb,pa);}
            var tool=SourceBody(model.Sources[fb]);
            if(SourceBody(model.Sources[fa])!=target||tool<0||tool==target||SourceBody(model.Sources[fc])!=tool
                ||reversed[fa]!=0||reversed[fb]==0||reversed[fc]==0)continue;
            if(!RegularAnalyticPlane(model.Faces[fa].Surface,out _)||!RegularAnalyticPlane(model.Faces[fb].Surface,out _)
                ||!RegularAnalyticPlane(model.Faces[fc].Surface,out _))continue;
            if(!SliverCurveOnFace(model,a.Edge,face.Surface,fa)||!SliverCurveOnFace(model,b.Edge,face.Surface,fb)
                ||!SliverCurveOnFace(model,c.Edge,face.Surface,fc))continue;
            candidate=new(){Cell=cell,PlaneFace=fa,WallFace=fb,CrossFace=fc,PlaneSide=a.Edge,WallSide=b.Edge,CrossSide=c.Edge,
                Original=original,PlaneEnd=pa,WallEnd=pb};return true;
        }
        return false;
    }

    private static BodySlot SourceBody(FaceSlot face)=>face<0?-1:Shells[Faces[face].BackShell].Body;
    private static BoundaryEventId SliverStart(in ModelingEdge edge,byte forward)=>forward!=0?edge.StartVertex:edge.EndVertex;
    private static BoundaryEventId SliverEnd(in ModelingEdge edge,byte forward)=>forward!=0?edge.EndVertex:edge.StartVertex;

    private static bool SliverNeighbor(in SheetArrangement model,ReadOnlySpan<byte> selected,ReadOnlySpan<byte> reversed,
        BufferOffset face,in ModelingCoedge boundary,out BufferOffset neighbor)
    {
        neighbor=-1;
        for(var f=0;f<model.NF;f++)if(selected[f]!=0&&f!=face)
        {
            var candidate=model.Faces[f];
            for(var l=0;l<candidate.LoopCount;l++)
            {
                var loop=model.Loops[candidate.FirstLoop+l];
                foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))if(use.Edge==boundary.Edge)
                {
                    if(neighbor>=0||(use.Forward^reversed[f])==(boundary.Forward^reversed[face]))return false;
                    neighbor=f;
                }
            }
        }
        return neighbor>=0;
    }

    private static bool RegularAnalyticPlane(SurfTag surface,out AnalyticSurface plane)
        =>TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out plane)&&plane.Kind==SurfaceClass.Plane;

    private static bool SliverCurveOnFace(in SheetArrangement model,BufferOffset edge,SurfTag rolling,BufferOffset neighbor)
        =>OtherBoundarySupport(rolling,model.Edges[edge].Curve,out var plane)&&SameSheetSupport(plane,model.Faces[neighbor].Surface);

    private static bool TrySliverRollingCurve(in ModelingEdge edge,SurfTag rolling,out ICurveData data,out BufferOffset side)
    {
        data=default;side=-1;var record=Curves[GetCurveSlotByTag(edge.Curve)];if(record.Class!=CurveClass.ICurve)return false;
        data=ICurveDataPool[record.DataIndex];
        if(data.ProgramBlock<=0||data.IsBlendContact!=0||!OtherBoundarySupport(rolling,edge.Curve,out var plane)||!RegularAnalyticPlane(plane,out _))return false;
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var definition=program.Spines[data.ProgramSpine];side=program.Surfaces[definition.First].IsBlend?0:1;
        return program.Surfaces[side==0?definition.First:definition.Second].IsBlend;
    }

    private static bool CertifyPlanarSliver(in SheetArrangement model,in PlanarSliver candidate,
        in AnalyticSurface plane,in AnalyticSurface wall,in AnalyticSurface cross,double precision,out IntervalBox3 footprint)
    {
        footprint=default;var face=model.Faces[candidate.Cell];var support=Surfaces[GetSurfaceSlotByTag(face.Surface)];
        var retained=model.Faces[candidate.PlaneFace];var flat=Surfaces[GetSurfaceSlotByTag(retained.Surface)];
        var data=BlendedEdgeDataPool[support.DataIndex];var program=GetNestedBlendProgram(data);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var u=new IntervalNumber(double.PositiveInfinity,double.NegativeInfinity);var v=u;
        var loop=model.Loops[face.FirstLoop];
        foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
        {
            var edge=model.Edges[use.Edge];if(!TrySliverRollingCurve(edge,face.Surface,out var curve,out var side))return false;
            var pair=GetNestedBlendProgram(curve.ProgramBlock,curve.ProgramSurfaceCount,curve.ProgramSpineCount,curve.ProgramChartCount,curve.ProgramScalarCount);
            if(JointNestedBlendEvaluation.BoundSpineSupportUv(pair,curve.ProgramSpine,edge.Start,edge.End,side,bounds[..4])!=AlgorithmStatus.Success)return false;
            u=IntervalNumber.Hull(u,bounds[0]);v=IntervalNumber.Hull(v,bounds[2]);
        }
        var blade=model.Edges[candidate.WallSide];var positive=LocalSenseSign(Curves[GetCurveSlotByTag(blade.Curve)].Sense)>0;
        var station=(blade.StartVertex==candidate.WallEnd)==positive?blade.Start:blade.End;
        if(SheetCurveUV(face.Surface,blade.Curve,station,true,out var corner,out _,out _)!=0)return false;
        var cu=corner.param[0];var cv=corner.param[1];var du=(u.Upper-u.Lower)/16;var dv=(v.Upper-v.Lower)/16;
        // This is only a search window around the existing event. The
        // walls must bracket a unique corner over the entire homotopy.
        var transition=new SurfaceParameterBox(cu-du,cu+du,cv-dv,cv+dv);
        var domain=new SurfaceParameterBox(Math.Min(u.Lower,transition.U0),Math.Max(u.Upper,transition.U1),
            Math.Min(v.Lower,transition.V0),Math.Max(v.Upper,transition.V1));
        if(JointNestedBlendEvaluation.CertifyProjectedPlaneCorner(program,data.ProgramRoot,transition,plane,wall,cross,domain)!=AlgorithmStatus.Success)return false;
        if(JointNestedBlendEvaluation.CertifyPlanarProjection(program,data.ProgramRoot,domain,plane,precision,out var onPlane,out var sameOrientation)!=AlgorithmStatus.Success
            ||!onPlane||sameOrientation!=(LocalSenseSign(face.Sense)*LocalSenseSign(support.Sense)==LocalSenseSign(retained.Sense)*LocalSenseSign(flat.Sense)))return false;
        if(JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,domain,bounds)!=AlgorithmStatus.Success)return false;
        var margin=new IntervalNumber(-precision,precision);
        var x=bounds[0]+margin;var y=bounds[1]+margin;var z=bounds[2]+margin;
        footprint=new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);return x.Finite&&y.Finite&&z.Finite;
    }

    private static bool TryProjectedPlaneVertex(in AnalyticSurface a,in AnalyticSurface b,in AnalyticSurface c,
        in KernelVector3 original,out IntervalVector corner)
    {
        corner=default;var anchor=new IntervalVector(original);var n=new IntervalVector(a.Axis);var m=new IntervalVector(b.Axis);var k=new IntervalVector(c.Axis);
        var line=IntervalVector.Cross(n,m);var det=IntervalVector.Dot(line,k).Value;
        if(!det.Finite||det.ContainsZero)return false;
        // The original event already satisfies the first two equations.
        corner=anchor+line*(IntervalVector.Dot(new IntervalVector(c.Origin)-anchor,k).Value/det);
        return corner.Finite;
    }

    private static bool SliverOuterEdge(in SheetArrangement model,ReadOnlySpan<byte> selected,in PlanarSliver cell,
        BoundaryEventId endpoint,BufferOffset a,BufferOffset b,out BufferOffset edge)
    {
        edge=-1;var count=0;
        for(var f=0;f<model.NF;f++)if(selected[f]!=0)
        {
            var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(use.Edge==cell.PlaneSide||use.Edge==cell.WallSide||use.Edge==cell.CrossSide)continue;
                    var candidate=model.Edges[use.Edge];if(candidate.StartVertex!=endpoint&&candidate.EndVertex!=endpoint)continue;
                    if(f!=a&&f!=b||edge>=0&&edge!=use.Edge)return false;
                    edge=use.Edge;count++;
                }
            }
        }
        return count==2;
    }

    private static bool TryRetargetSliverLine(in ModelingEdge edge,BoundaryEventId endpoint,in AnalyticSurface first,in AnalyticSurface second,
        in IntervalVector corner,double precision,out ModelingEdge adapted,out bool atStart)
    {
        adapted=edge;atStart=edge.StartVertex==endpoint;
        var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(record.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(record,out var line))return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];
        if(!BoundRoundedPlaneLine(new(){Curve=edge.Curve,First=edge.Start,Last=edge.End},line,first,second,poles))return false;
        var parameter=SliverLineParameter(line,corner);
        if(!parameter.Finite)return false;
        var representative=new IntervalVector(line.Origin)+new IntervalVector(line.First)*parameter;
        if((representative-corner).Norm().Value.Upper>precision/16)return false;
        var lower=atStart==(LocalSenseSign(record.Sense)>0);
        if(lower){if(parameter.Upper>=edge.End)return false;adapted.Start=parameter.Midpoint;}
        else{if(parameter.Lower<=edge.Start)return false;adapted.End=parameter.Midpoint;}
        return true;
    }

    private static IntervalNumber SliverLineParameter(in ParametricSpineCurve line,in IntervalVector point)
    {
        var axis=new IntervalVector(line.First);
        return IntervalVector.Dot(point-new IntervalVector(line.Origin),axis).Value/IntervalVector.Dot(axis,axis).Value;
    }

    private static bool SliverLeavesOriginalLine(in SheetArrangement model,BufferOffset continuation,BoundaryEventId original,
        in AnalyticSurface a,in AnalyticSurface b,in IntervalVector corner)
    {
        var edge=model.Edges[continuation];var opposite=edge.StartVertex==original?edge.EndVertex:edge.StartVertex;
        var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(opposite<0||record.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(record,out var line))return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];
        if(!BoundRoundedPlaneLine(new(){Curve=edge.Curve,First=edge.Start,Last=edge.End},line,a,b,poles))return false;
        var root=new IntervalVector(model.Points[original]);var existing=new IntervalVector(model.Points[opposite])-root;
        return IntervalVector.Dot(corner-root,existing).Value.Upper<0;
    }

    private static bool SliverExcludesOtherBoundaries(in SheetArrangement model,ReadOnlySpan<byte> selected,
        ReadOnlySpan<BufferOffset> involved,in IntervalBox3 footprint)
    {
        Span<byte> tested=stackalloc byte[model.NE];tested.Clear();
        Span<IntervalNumber> pending=stackalloc IntervalNumber[48];var visited=0;
        for(var f=0;f<model.NF;f++)if(selected[f]!=0)
        {
            var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(tested[use.Edge]!=0||involved.Contains(use.Edge))continue;tested[use.Edge]=1;
                    var edge=model.Edges[use.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];
                    var count=1;pending[0]=new(edge.Start,edge.End);
                    while(count>0)
                    {
                        if(++visited>2048)return false;
                        var t=pending[--count];
                        if(TryFiniteCarrierExtent(new(){Curve=edge.Curve,First=t.Lower,Last=t.Upper},curve,out var extent)
                            &&ThirdFaceBoxesDisjoint(footprint,extent,0))continue;
                        var mid=t.Midpoint;if(!(mid>t.Lower&&mid<t.Upper)||count+2>pending.Length)return false;
                        pending[count++]=new(t.Lower,mid);pending[count++]=new(mid,t.Upper);
                    }
                }
            }
        }
        return true;
    }

    private static bool SliverInsideToolTrim(in RegularBoundaryView input,BufferOffset wall,BufferOffset cross,
        in AnalyticSurface a,in AnalyticSurface b,in IntervalBox3 footprint)
    {
        var first=input.Faces[wall];var second=input.Faces[cross];BufferOffset shared=-1;
        for(var l=0;l<first.LoopCount;l++)
        {
            var loop=input.Loops[first.FirstLoop+l];
            foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                if(!FaceHasEdge(input,second,use.Edge))continue;
                if(shared>=0&&shared!=use.Edge)return false;
                shared=use.Edge;
            }
        }
        if(shared<0)return false;
        var edge=input.Edges[shared];var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(record.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(record,out var line))return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];
        if(!BoundRoundedPlaneLine(new(){Curve=edge.Curve,First=edge.Start,Last=edge.End},line,a,b,poles))return false;
        var stations=SliverLineParameter(line,new(new IntervalNumber(footprint.XLo,footprint.XHi),new IntervalNumber(footprint.YLo,footprint.YHi),
            new IntervalNumber(footprint.ZLo,footprint.ZHi)));
        if(!stations.Finite||stations.Lower<=edge.Start||stations.Upper>=edge.End)return false;
        for(var side=0;side<2;side++)
        {
            var face=side==0?first:second;
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=input.Loops[face.FirstLoop+l];
                foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(use.Edge==shared)continue;
                    var source=input.Edges[use.Edge];
                    if(!TryFiniteCarrierExtent(new(){Curve=source.Curve,First=source.Start,Last=source.End},Curves[GetCurveSlotByTag(source.Curve)],out var trim)
                        ||!ThirdFaceBoxesDisjoint(footprint,trim,0))return false;
                }
            }
        }
        return true;

        static bool FaceHasEdge(in RegularBoundaryView input,in ModelingFace face,BufferOffset edge)
        {
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=input.Loops[face.FirstLoop+l];
                foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))if(use.Edge==edge)return true;
            }
            return false;
        }
    }

    private static int RearrangeSliverNeighbor(scoped ref SheetArrangement model,BufferOffset index,BufferOffset removed,BufferOffset replacement,
        BoundaryEventId original,scoped Span<ModelingCoedge> boundary,scoped Span<SheetHalfedge> halfedges,
        scoped Span<ModelingCoedge> cycleCoedges,scoped Span<SheetCycle> cycles)
    {
        var face=model.Faces[index];var start=model.NF;var count=0;var found=false;
        for(var l=0;l<face.LoopCount;l++)
        {
            var loop=model.Loops[face.FirstLoop+l];
            foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                if(use.Edge==removed)
                {
                    if(found)return ParasolidConstants.PK_ERROR_bad_loop;found=true;
                    if(replacement<0)continue;
                    var forward=SliverStart(model.Edges[removed],use.Forward)==original;
                    if(count==boundary.Length)return ParasolidConstants.PK_ERROR_memory_full;
                    boundary[count++]=new(){Edge=replacement,Forward=forward?(byte)1:(byte)0};
                }
                else
                {
                    if(count==boundary.Length)return ParasolidConstants.PK_ERROR_memory_full;
                    boundary[count++]=use;
                }
            }
        }
        if(!found)return ParasolidConstants.PK_ERROR_bad_loop;
        var error=ArrangeFiniteReplacementFace(face.Surface,face.Sense,model.Sources[index],boundary[..count],ref model,halfedges,cycleCoedges,cycles);
        if(error!=0)return error;
        if(model.NF!=start+1||model.Faces[start].LoopCount!=face.LoopCount)return ParasolidConstants.PK_ERROR_bad_loop;
        model.Faces[index]=model.Faces[start];model.Witnesses[index]=model.Witnesses[start];model.NF=start;return 0;
    }
}
