using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildAxialLocalBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;var error=ReadLocalAxialSolid(body,faces,out var axial);if(error!=0)return error;
        if(axial.Angle!=0||selected.Length>2)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<int> selection=stackalloc int[2]{-1,-1};Span<double> radial=stackalloc double[2];Span<double> height=stackalloc double[2];radial.Clear();height.Clear();
        for(var i=0;i<selected.Length;i++)
        {
            var edge=selected[i];if(!LocalEdgeFaces(edge,out var a,out var b))return ParasolidConstants.PK_ERROR_bad_parameter;
            var cap=a==faces[axial.Side]?b:a;var end=cap==faces[axial.Lower]?0:cap==faces[axial.Upper]?1:-1;
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
        for(var i=0;i<faces.Length;i++)
        {
            error=TryCloneEntityCore(Faces[faces[i]].SurfTag,out var surf);if(error!=0)return error;
            var sense=Faces[faces[i]].Orientation;
            var added=i==axial.Side?brep.AddRingFace(surf,sense,lowerSide,true,upperSide,false):
                brep.AddRingFace(surf,sense,i==axial.Lower?lowerCap:upperCap,i==axial.Upper);
            if(!added)return ParasolidConstants.PK_ERROR_memory_full;
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
            if(!brep.AddRingFace(surface,ParasolidConstants.PK_TOPOL_sense_positive_c,end==0?lowerCap:upperSide,true,end==0?lowerSide:upperCap,false))return ParasolidConstants.PK_ERROR_memory_full;
            descriptions[made++]=new(){Edge=Edges[edge].Header.Tag,First=Faces[faces[axial.Side]].Header.Tag,Second=Faces[cap].Header.Tag};
        }
        if(selection[0]>=0)brep.NegativeCurveUse(lowerSide);
        if(selection[1]>=0)brep.NegativeCurveUse(upperSide);
        error=brep.Complete(out var product,products);if(error!=0)return error;
        return ReplaceLocalBody(body,product,faces,products[..brep.FaceCount]);
    }
}
