using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation.Numerics;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildAxialLocalBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;var error=ReadLocalAxialSolid(body,faces,out var axial);if(error!=0)return error;
        if(axial.Angle!=0||selected.Length>2)return ParasolidConstants.PK_ERROR_not_implemented;
        var side=faces[axial.Side];
        if(!TryAxialSourceRing(side,faces[axial.Lower],axial.Bottom,out var lowerSource)
            ||!TryAxialSourceRing(side,faces[axial.Upper],axial.Top,out var upperSource)||lowerSource==upperSource)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<int> selection=stackalloc int[2]{-1,-1};Span<double> radial=stackalloc double[2];Span<double> height=stackalloc double[2];radial.Clear();height.Clear();
        for(var i=0;i<selected.Length;i++)
        {
            var edge=selected[i];var end=edge==lowerSource?0:edge==upperSource?1:-1;
            if(end<0)return ParasolidConstants.PK_ERROR_not_implemented;
            selection[end]=i;ref var e=ref Edges[edge];
            if(e.PendingBlendKind==1)radial[end]=height[end]=e.PendingBlendFirst;
            else
            {
                var firstSide=e.PendingBlendFirstFace==Faces[faces[axial.Side]].Header.Tag;
                radial[end]=firstSide?e.PendingBlendFirst:e.PendingBlendSecond;
                height[end]=firstSide?e.PendingBlendSecond:e.PendingBlendFirst;
            }
        }
        if(radial[0]>=axial.Radius||radial[1]>=axial.Radius||height[0]+height[1]>=axial.Top-axial.Bottom)return ParasolidConstants.PK_ERROR_radius_too_large;
        Span<KernelVector3> points=stackalloc KernelVector3[1];Span<ModelingEdge> modelEdges=stackalloc ModelingEdge[4];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[10];Span<ModelingLoop> loops=stackalloc ModelingLoop[10];
        Span<ModelingFace> modelFaces=stackalloc ModelingFace[5];Span<byte> cavity=stackalloc byte[5];
        var brep=new LocalBrep{Points=points,Edges=modelEdges,Coedges=coedges,Loops=loops,Faces=modelFaces,Cavities=cavity};
        var lowerSide=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Bottom+height[0])),axial.Axis,axial.Reference,axial.Radius);
        var upperSide=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Top-height[1])),axial.Axis,axial.Reference,axial.Radius);
        var lowerCap=selection[0]<0?lowerSide:brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Bottom)),axial.Axis,axial.Reference,axial.Radius-radial[0]);
        var upperCap=selection[1]<0?upperSide:brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Top)),axial.Axis,axial.Reference,axial.Radius-radial[1]);
        if(lowerSide<0||upperSide<0||lowerCap<0||upperCap<0)return ParasolidConstants.PK_ERROR_memory_full;
        if(selection[0]>=0)brep.NegativeCurveUse(lowerSide);
        if(selection[1]>=0)brep.NegativeCurveUse(upperSide);
        var arrangement=new SheetArrangement{Points=points,Edges=modelEdges,Coedges=coedges,Loops=loops,Faces=modelFaces,NE=brep.EdgeCount,
            Sources=stackalloc FaceSlot[5],Witnesses=stackalloc PK_UV_s[5]};
        Span<ModelingCoedge> boundary=stackalloc ModelingCoedge[2],cycleCoedges=stackalloc ModelingCoedge[2];
        Span<SheetHalfedge> halfedges=stackalloc SheetHalfedge[2];Span<SheetCycle> cycles=stackalloc SheetCycle[2];
        Span<BufferOffset> productCells=stackalloc BufferOffset[2];
        for(var i=0;i<faces.Length;i++)
        {
            error=TryCloneEntityCore(Faces[faces[i]].SurfTag,out var surf);if(error!=0)return error;
            var sense=NormalizeLocalSense(Faces[faces[i]].Orientation);
            var count=1;
            if(i==axial.Side)
            {
                boundary[0]=AxialRingUse(modelEdges,upperSide,false);boundary[1]=AxialRingUse(modelEdges,lowerSide,true);count=2;
            }
            else boundary[0]=AxialRingUse(modelEdges,i==axial.Lower?lowerCap:upperCap,i==axial.Upper);
            error=ArrangeAxialBand(surf,sense,faces[i],boundary[..count],ref arrangement,halfedges,cycleCoedges,cycles);if(error!=0)return error;
        }
        for(var end=0;end<2;end++)if(selection[end]>=0)
        {
            var edge=selected[selection[end]];var cap=faces[end==0?axial.Lower:axial.Upper];var r=radial[end];SurfTag surface;
            if(Edges[edge].PendingBlendKind==1)
            {
                var center=Add(axial.Origin,Scale(axial.Axis,end==0?axial.Bottom+r:axial.Top-r));
                surface=CreateTorusSurfaceTag(center.X,center.Y,center.Z,axial.Axis.X,axial.Axis.Y,axial.Axis.Z,
                    axial.Reference.X,axial.Reference.Y,axial.Reference.Z,axial.Radius-r,r);
            }
            else
            {
                var axis=end==0?axial.Axis:Scale(axial.Axis,-1);var center=Add(axial.Origin,Scale(axial.Axis,end==0?axial.Bottom:axial.Top));
                surface=CreateConeSurfaceTag(center.X,center.Y,center.Z,axis.X,axis.Y,axis.Z,axial.Reference.X,axial.Reference.Y,axial.Reference.Z,
                    axial.Radius-r,Math.Atan2(r,height[end]));
            }
            if(surface<=0)return ParasolidConstants.PK_ERROR_memory_full;
            boundary[0]=AxialRingUse(modelEdges,end==0?lowerSide:upperCap,false);boundary[1]=AxialRingUse(modelEdges,end==0?lowerCap:upperSide,true);
            productCells[made]=arrangement.NF;
            error=ArrangeAxialBand(surface,ParasolidConstants.PK_TOPOL_sense_positive_c,-1,boundary,ref arrangement,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            descriptions[made++]=new(){Edge=Edges[edge].Header.Tag,First=Faces[faces[axial.Side]].Header.Tag,Second=Faces[cap].Header.Tag};
        }
        error=BuildArrangedLocalBody(ref arrangement,out var product,products);if(error!=0)return error;
        for(var i=0;i<made;i++)descriptions[i].Result=Faces[products[productCells[i]]].Header.Tag;
        return ReplaceLocalBody(body,product,faces,products[..arrangement.NF]);
    }

    private static bool TryAxialSourceRing(FaceSlot side,FaceSlot cap,double expected,out EdgeSlot edge)
    {
        edge=-1;var face=Faces[cap];if(face.LoopCount!=1||Loops[face.FirstLoop].FinCount!=1)return false;
        var slot=Fins[Loops[face.FirstLoop].FirstFin].Edge;if(slot<0)return false;
        var ring=Edges[slot];
        if(ring.StartVertex>=0||ring.EndVertex>=0||!LocalEdgeFaces(slot,out var a,out var b)||!(a==cap&&b==side||a==side&&b==cap)
            ||!TryProfileEdgeRange(ring,out var lo,out var hi)||hi-lo!=Math.Tau
            ||!TryCoaxialCircleChart(Faces[side].SurfTag,Curves[GetCurveSlotByTag(ring.CurveTag)],out _,out _,out var station))return false;
        if(Math.Abs(station-expected)>64*SmallLinearSolve.MachineEpsilon*Math.Max(1,Math.Max(Math.Abs(station),Math.Abs(expected))))return false;
        edge=slot;return true;
    }

    private static ModelingCoedge AxialRingUse(ReadOnlySpan<ModelingEdge> edges,BufferOffset edge,bool naturalForward)
        =>new(){Edge=edge,Forward=naturalForward==(LocalSenseSign(Curves[GetCurveSlotByTag(edges[edge].Curve)].Sense)>0)?(byte)1:(byte)0};

    private static int ArrangeAxialBand(SurfTag surface,KernelSense sense,FaceSlot source,scoped ReadOnlySpan<ModelingCoedge> boundary,
        scoped ref SheetArrangement model,scoped Span<SheetHalfedge> halfedges,scoped Span<ModelingCoedge> coedges,scoped Span<SheetCycle> cycles)
    {
        var count=model.NF;var error=ArrangeFiniteReplacementFace(surface,sense,source,boundary,ref model,halfedges,coedges,cycles);
        return error!=0?error:model.NF==count+1?0:ParasolidConstants.PK_ERROR_bad_loop;
    }
}
