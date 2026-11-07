using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int SheetCycleWitness(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        out PK_UV_s left,out byte positive)
    {
        left=default;positive=0;var support=Surfaces[GetSurfaceSlotByTag(surface)];
        if(support.Class==SurfaceClass.Sphere)return SphericalCycleWitness(surface,edges,loop,out left,out positive);
        var extent=Math.Min(support.UMax-support.UMin,support.VMax-support.VMin);
        if(support.Class is SurfaceClass.Plane or SurfaceClass.Cylinder)extent=PlanarLoopParameterScale(surface,edges,loop);
        var tries=support.Class is SurfaceClass.Plane or SurfaceClass.Cylinder?36:24;
        for(var attempt=0;attempt<tries;attempt++)
        {
            var coedge=loop[attempt%loop.Length];var fraction=.2931471805599453+(attempt%5)*.071;
            var error=SheetCoedgePoint(surface,edges[coedge.Edge],coedge.Forward,fraction,out var uv,out var tangent,out _);if(error!=0)return error;
            var length=Math.Sqrt(tangent.param[0]*tangent.param[0]+tangent.param[1]*tangent.param[1]);if(!(length>0))continue;
            // For a small complete cell the usual probe step can already
            // be inside the ray's boundary uncertainty. Try a larger
            // fraction of this local chart extent; winding must still
            // validate the two sides with the same acceptance rules.
            var epsilon=extent*(attempt<24?1e-5*Math.Pow(.2,attempt/6):attempt<30?.001:.02);
            var a=uv;var b=uv;
            a.param[0]-=epsilon*tangent.param[1]/length;a.param[1]+=epsilon*tangent.param[0]/length;
            b.param[0]+=epsilon*tangent.param[1]/length;b.param[1]-=epsilon*tangent.param[0]/length;
            error=SheetLoopWinding(surface,edges,loop,a,out var wa);if(error!=0)continue;
            error=SheetLoopWinding(surface,edges,loop,b,out var wb);if(error!=0)continue;
            if(wa==1&&wb==0){left=a;positive=1;return 0;}
            if(wa==0&&wb==-1){left=a;positive=0;return 0;}
        }
        return ParasolidConstants.PK_ERROR_eval_failure;
    }

    private static int PublishSheetCycles(FaceSlot source,SurfTag plane,bool reverse,ref SheetArrangement model,
        ReadOnlySpan<ModelingCoedge> coedges,Span<SheetCycle> cycles,bool borrowGeometry=false)
    {
        var surface=Faces[source].SurfTag;
        return PublishFiniteSheetCycles(surface,NormalizeLocalSense(Faces[source].Orientation),source,reverse,ref model,coedges,cycles,plane,borrowGeometry);
    }

    private static int PublishFiniteSheetCycles(SurfTag surface,KernelSense sense,FaceSlot source,bool reverse,ref SheetArrangement model,
        ReadOnlySpan<ModelingCoedge> coedges,Span<SheetCycle> cycles,SurfTag plane=0,bool borrowGeometry=true)
    {
        for(var i=0;i<cycles.Length;i++)if(cycles[i].Positive!=0)
        {
            var depth=0;
            for(var j=0;j<cycles.Length;j++)if(i!=j&&cycles[j].Positive!=0)
            {
                var other=cycles[j];var error=SheetLoopWinding(surface,model.Edges[..model.NE],coedges.Slice(other.First,other.Count),cycles[i].Left,out var winding);if(error!=0)return error;
                if(winding!=0)depth++;
            }
            cycles[i].Depth=depth;
        }
        for(var i=0;i<cycles.Length;i++)if(cycles[i].Positive==0)
        {
            var parent=-1;var depth=-1;
            for(var j=0;j<cycles.Length;j++)if(cycles[j].Positive!=0)
            {
                var outer=cycles[j];var error=SheetLoopWinding(surface,model.Edges[..model.NE],coedges.Slice(outer.First,outer.Count),cycles[i].Left,out var winding);if(error!=0)return error;
                if(winding!=0&&outer.Depth>depth){depth=outer.Depth;parent=j;}
            }
            if(parent<0){LastSheetPartitionStage=SheetPartitionStage.CycleNesting;LastSheetParameter=i;return ParasolidConstants.PK_ERROR_bad_loop;}cycles[i].Parent=parent;
        }
        var ownedSurface=surface;
        if(!borrowGeometry){var cloneError=TryCloneEntityCore(surface,out ownedSurface);if(cloneError!=0)return cloneError;}
        Geometry.Evaluation.AnalyticSurface cutter=default;
        if(!borrowGeometry)TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)],out cutter);
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
        for(var i=0;i<cycles.Length;i++)if(cycles[i].Positive!=0)
        {
            if(model.NF==model.Faces.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var firstLoop=model.NL;
            var error=AppendSheetCycle(cycles[i],coedges,reverse,ref model);if(error!=0)return error;
            for(var j=0;j<cycles.Length;j++)if(cycles[j].Parent==i)
            { error=AppendSheetCycle(cycles[j],coedges,reverse,ref model);if(error!=0)return error; }
            var uv=cycles[i].Left;
            // The side witness must belong to the material cell, including
            // its holes, and be separated from the cutting plane by tolerance.
            var valid=true;
            for(var j=0;j<cycles.Length;j++)if(cycles[j].Parent==i)
            {
                error=SheetLoopWinding(surface,model.Edges[..model.NE],coedges.Slice(cycles[j].First,cycles[j].Count),uv,out var winding);if(error!=0)return error;
                if(winding!=0)valid=false;
            }
            if(!valid)return ParasolidConstants.PK_ERROR_eval_failure;
            var status=EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(surface)],uv.param[0],uv.param[1],in layout,point,0);
            if(status!=Computation.AlgorithmStatus.Success)return EvaluationError(status);
            var distance=borrowGeometry?1:Dot(Sub(point[0],cutter.Origin),cutter.Axis)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(plane)].Sense);
            if(Math.Abs(distance)<=model.Tolerance)return ParasolidConstants.PK_ERROR_eval_failure;
            model.Sources[model.NF]=source;if(!model.Sides.IsEmpty)model.Sides[model.NF]=distance>0?(sbyte)1:(sbyte)-1;
            if(!model.Witnesses.IsEmpty)model.Witnesses[model.NF]=uv;
            if(TraceFiniteBoundary!=null)
                for(var l=firstLoop;l<model.NL;l++)
                {
                    var loop=model.Loops[l];
                    foreach(var coedge in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                    {
                        var use=FiniteBoundaryUse.Create(surface,model.Edges[coedge.Edge],coedge.Edge,coedge.Forward,source);
                        TraceBoundary(BoundaryTraceStage.Arrangement,use,model.NF);
                    }
                }
            model.Faces[model.NF++]=new(){Surface=ownedSurface,Sense=sense,FirstLoop=firstLoop,LoopCount=model.NL-firstLoop};
        }
        return 0;
    }

    private static int AppendSheetCycle(in SheetCycle cycle,ReadOnlySpan<ModelingCoedge> coedges,bool reverse,ref SheetArrangement model)
    {
        if(model.NL==model.Loops.Length||model.NC>model.Coedges.Length-cycle.Count)return ParasolidConstants.PK_ERROR_not_implemented;
        model.Loops[model.NL++]=new(){FirstCoedge=model.NC,CoedgeCount=cycle.Count};
        for(var i=0;i<cycle.Count;i++)
        {
            var coedge=coedges[cycle.First+(reverse?cycle.Count-1-i:i)];if(reverse)coedge.Forward^=1;
            model.Coedges[model.NC++]=coedge;
        }
        return 0;
    }

    private static int SheetPointOnCoincidentBoundary(FaceSlot face,ReadOnlySpan<SheetSourceEdge> sources,in KernelVector3 point,out bool on,bool allBoundaries=false)
    {
        on=false;var loop=Faces[face].FirstLoop;var vector=new PK_VECTOR_s();WriteVector(&vector,point);
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            if(RegularVertexLoop(loop,out _))continue;
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {
                var edge=Fins[fin].Edge;var coincident=false;
                foreach(ref readonly var source in sources)if(source.Source==edge&&(allBoundaries||source.InPlane!=0))coincident=true;
                if(!coincident)continue;double parameter=0;var error=CurveParameteriseVectorCore(Edges[edge].CurveTag,vector,&parameter);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                if(!TryProfileEdgeRange(Edges[edge],out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
                LiftPeriodicEdgeParameter(Edges[edge].CurveTag,first,last,ref parameter);
                if(parameter>=first-1e-10&&parameter<=last+1e-10){on=true;return 0;}
            }
        }
        return 0;
    }
}
