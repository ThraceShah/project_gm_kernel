using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildPlanarLocalBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;Span<LocalPlane> planes=stackalloc LocalPlane[faces.Length];
        var error=ReadLocalPlanes(body,faces,planes);if(error!=0)return error;
        if(selected.Length==Bodies[body].EdgeCountBody)
            return BuildAllPlanarLocalBlends(body,faces,planes,selected,descriptions,products,out made);
        return BuildSeparatePlanarLocalBlends(body,faces,planes,selected,descriptions,products,out made);
    }

    private static int BuildAllPlanarLocalBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<LocalPlane> planes,
        ReadOnlySpan<EdgeSlot> selected,Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;var first=Edges[selected[0]];var radius=first.PendingBlendFirst;var kind=first.PendingBlendKind;
        for(var i=0;i<selected.Length;i++)
        {
            var edge=Edges[selected[i]];
            if(edge.PendingBlendKind!=kind||edge.PendingBlendFirst!=radius||edge.PendingBlendSecond!=radius)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        Span<LocalPlane> inset=stackalloc LocalPlane[planes.Length];
        for(var i=0;i<planes.Length;i++)inset[i]=new(){Normal=planes[i].Normal,Distance=planes[i].Distance-radius};
        Span<KernelVector3> centers=stackalloc KernelVector3[VertexLimit];Span<ulong> mask=stackalloc ulong[VertexLimit];
        Span<int> indices=stackalloc int[IndexLimit];Span<LocalPolygon> polygons=stackalloc LocalPolygon[planes.Length];
        if(!Build(inset,centers,mask,indices,polygons,out var vertexCount,out _,1e-8))return ParasolidConstants.PK_ERROR_radius_too_large;
        if(vertexCount!=Bodies[body].VertexCountBody||faces.Length+selected.Length+vertexCount>LocalFaceLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<VertexSlot> oldVertices=stackalloc VertexSlot[vertexCount];oldVertices.Fill(-1);
        var old=Bodies[body].FirstVertexBody;
        for(var i=0;i<vertexCount;i++,old=Vertices[old].NextInBody)
        {
            var oldMask=LocalVertexFaceMask(old,faces);var found=-1;
            if(System.Numerics.BitOperations.PopCount(oldMask)!=3)return ParasolidConstants.PK_ERROR_not_implemented;
            for(var v=0;v<vertexCount;v++)if(mask[v]==oldMask){if(found>=0)return ParasolidConstants.PK_ERROR_not_implemented;found=v;}
            if(found<0)return ParasolidConstants.PK_ERROR_radius_too_large;oldVertices[found]=old;
        }
        Span<KernelVector3> points=stackalloc KernelVector3[LocalPointLimit];Span<ModelingEdge> edges=stackalloc ModelingEdge[LocalEdgeLimit];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[LocalCoedgeLimit];Span<ModelingLoop> loops=stackalloc ModelingLoop[LocalFaceLimit];
        Span<ModelingFace> modelFaces=stackalloc ModelingFace[LocalFaceLimit];Span<byte> cavity=stackalloc byte[LocalFaceLimit];
        var brep=new LocalBrep{Points=points,Edges=edges,Coedges=coedges,Loops=loops,Faces=modelFaces,Cavities=cavity};
        Span<int> boundary=stackalloc int[VertexLimit];
        for(var f=0;f<faces.Length;f++)
        {
            var polygon=polygons[f];if(polygon.Count<3)return ParasolidConstants.PK_ERROR_radius_too_large;
            for(var i=0;i<polygon.Count;i++)
            {
                boundary[i]=brep.AddPoint(Add(centers[indices[polygon.First+i]],Scale(planes[f].Normal,radius)));
                if(boundary[i]<0)return ParasolidConstants.PK_ERROR_memory_full;
            }
            var error=TryCloneEntityCore(Faces[faces[f]].SurfTag,out var surface);if(error!=0)return error;
            if(!brep.AddFace(surface,Faces[faces[f]].Orientation,boundary[..polygon.Count]))return ParasolidConstants.PK_ERROR_memory_full;
        }
        Span<int> quad=stackalloc int[4];
        for(var e=0;e<selected.Length;e++)
        {
            var slot=selected[e];if(!LocalEdgeFaces(slot,out var fa,out var fb))return ParasolidConstants.PK_ERROR_bad_parameter;
            var a=faces.IndexOf(fa);var b=faces.IndexOf(fb);var edgeMask=(1UL<<a)|(1UL<<b);var va=-1;var vb=-1;
            for(var v=0;v<vertexCount;v++)if((mask[v]&edgeMask)==edgeMask){if(va<0)va=v;else if(vb<0)vb=v;else return ParasolidConstants.PK_ERROR_not_implemented;}
            if(va<0||vb<0)return ParasolidConstants.PK_ERROR_radius_too_large;
            var ca=centers[va];var cb=centers[vb];var axis=Unit(Sub(cb,ca));var na=planes[a].Normal;var nb=planes[b].Normal;
            quad[0]=brep.AddPoint(Add(ca,Scale(na,radius)));quad[1]=brep.AddPoint(Add(cb,Scale(na,radius)));
            quad[2]=brep.AddPoint(Add(cb,Scale(nb,radius)));quad[3]=brep.AddPoint(Add(ca,Scale(nb,radius)));
            for(var j=0;j<4;j++)if(quad[j]<0)return ParasolidConstants.PK_ERROR_memory_full;
            var outward=Add(na,nb);OrientLocalPolygon(brep.Points,quad,outward);
            SurfTag surface;
            if(kind==1)surface=CreateCylinderSurfaceTag(ca.X,ca.Y,ca.Z,axis.X,axis.Y,axis.Z,na.X,na.Y,na.Z,radius);
            else{var error=LocalPlaneTag(Unit(outward),brep.Points[quad[0]],out surface);if(error!=0)return error;}
            if(!brep.AddFace(surface,ParasolidConstants.PK_TOPOL_sense_positive_c,quad))return ParasolidConstants.PK_ERROR_memory_full;
            if(kind==1)
                for(var j=0;j<4;j++)
                {
                    var p=quad[j];var q=quad[(j+1)%4];var delta=Sub(brep.Points[q],brep.Points[p]);
                    if(Math.Abs(Dot(Unit(delta),axis))<.5)
                    {
                        var center=Math.Abs(Dot(Sub(brep.Points[p],ca),axis))<Math.Abs(Dot(Sub(brep.Points[p],cb),axis))?ca:cb;
                        var error=AddLocalArc(ref brep,p,q,center,axis,radius);if(error!=0)return error;
                    }
                }
            descriptions[made++]=new(){Edge=Edges[slot].Header.Tag,First=Faces[fa].Header.Tag,Second=Faces[fb].Header.Tag};
        }
        Span<int> triangle=stackalloc int[3];Span<int> adjacent=stackalloc int[3];
        for(var v=0;v<vertexCount;v++)
        {
            var n=0;var outward=new KernelVector3();
            for(var f=0;f<planes.Length;f++)if((mask[v]&(1UL<<f))!=0)
            {
                if(n>=3)return ParasolidConstants.PK_ERROR_not_implemented;
                adjacent[n]=f;triangle[n++]=brep.AddPoint(Add(centers[v],Scale(planes[f].Normal,radius)));outward=Add(outward,planes[f].Normal);
            }
            if(n!=3)return ParasolidConstants.PK_ERROR_not_implemented;
            OrientLocalPolygon(brep.Points,triangle,outward);var center=centers[v];SurfTag surface;
            if(kind==1)surface=CreateSphereSurfaceTag(center.X,center.Y,center.Z,0,0,1,1,0,0,radius);
            else
            {
                var normal=Unit(ConvexLocalModel.Cross(Sub(brep.Points[triangle[1]],brep.Points[triangle[0]]),Sub(brep.Points[triangle[2]],brep.Points[triangle[0]])));
                var error=LocalPlaneTag(normal,brep.Points[triangle[0]],out surface);if(error!=0)return error;
            }
            if(!brep.AddFace(surface,ParasolidConstants.PK_TOPOL_sense_positive_c,triangle))return ParasolidConstants.PK_ERROR_memory_full;
            if(kind==1)for(var i=0;i<3;i++)
            {
                var a=triangle[i];var b=triangle[(i+1)%3];var normal=Unit(ConvexLocalModel.Cross(Sub(brep.Points[a],center),Sub(brep.Points[b],center)));
                var error=AddLocalArc(ref brep,a,b,center,normal,radius);if(error!=0)return error;
            }
            var sourceVertex=oldVertices[v];EntityTag sourceEdge=0;
            for(var i=0;i<selected.Length;i++)if(Edges[selected[i]].StartVertex==sourceVertex||Edges[selected[i]].EndVertex==sourceVertex){sourceEdge=Edges[selected[i]].Header.Tag;break;}
            descriptions[made++]=new(){Edge=sourceEdge,Vertex=Vertices[sourceVertex].Header.Tag,
                First=Faces[faces[adjacent[0]]].Header.Tag,Second=Faces[faces[adjacent[1]]].Header.Tag,Third=Faces[faces[adjacent[2]]].Header.Tag};
        }
        var finish=brep.Complete(out var product,products);if(finish!=0)return finish;
        return ReplaceLocalBody(body,product,faces,products[..brep.FaceCount]);
    }

    private static void OrientLocalPolygon(ReadOnlySpan<KernelVector3> points,Span<int> polygon,KernelVector3 outward)
    {
        var normal=new KernelVector3();var origin=points[polygon[0]];
        for(var i=1;i+1<polygon.Length;i++)normal=Add(normal,ConvexLocalModel.Cross(Sub(points[polygon[i]],origin),Sub(points[polygon[i+1]],origin)));
        if(Dot(normal,outward)<0)polygon.Reverse();
    }

    private static int BuildSeparatePlanarLocalBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<LocalPlane> input,
        ReadOnlySpan<EdgeSlot> selected,Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;if(input.Length+selected.Length>PlaneLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        // Interacting vertex blends have their own complete all-edge path.
        // Reject arbitrary partial corner junctions instead of leaving gaps.
        for(var i=0;i<selected.Length;i++)for(var j=0;j<i;j++)
            if(Edges[selected[i]].StartVertex==Edges[selected[j]].StartVertex||Edges[selected[i]].StartVertex==Edges[selected[j]].EndVertex
                ||Edges[selected[i]].EndVertex==Edges[selected[j]].StartVertex||Edges[selected[i]].EndVertex==Edges[selected[j]].EndVertex)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<LocalPlane> planes=stackalloc LocalPlane[input.Length+selected.Length];input.CopyTo(planes);
        Span<KernelVector3> centers=stackalloc KernelVector3[selected.Length];Span<KernelVector3> axes=stackalloc KernelVector3[selected.Length];
        for(var i=0;i<selected.Length;i++)
        {
            var edge=Edges[selected[i]];if(!LocalEdgeFaces(selected[i],out var fa,out var fb))return ParasolidConstants.PK_ERROR_bad_parameter;
            if(Faces[fb].Header.Tag==edge.PendingBlendFirstFace)(fa,fb)=(fb,fa);
            var a=faces.IndexOf(fa);var b=faces.IndexOf(fb);
            var start=Points[GetPointSlotByTag(Vertices[edge.StartVertex].PointTag)].Position;var end=Points[GetPointSlotByTag(Vertices[edge.EndVertex].PointTag)].Position;
            axes[i]=Unit(Sub(end,start));var error=LocalBlendCentre(input[a],input[b],start,edge.PendingBlendFirst,edge.PendingBlendSecond,out centers[i]);if(error!=0)return error;
            var pa=Add(centers[i],Scale(input[a].Normal,edge.PendingBlendFirst));var pb=Add(centers[i],Scale(input[b].Normal,edge.PendingBlendSecond));
            var normal=Unit(ConvexLocalModel.Cross(axes[i],Sub(pb,pa)));if(Dot(normal,Add(input[a].Normal,input[b].Normal))<0)normal=Scale(normal,-1);
            planes[input.Length+i]=new(){Normal=normal,Distance=Dot(normal,pa)};
            descriptions[i]=new(){Edge=edge.Header.Tag,First=Faces[fa].Header.Tag,Second=Faces[fb].Header.Tag};
        }
        Span<KernelVector3> vertices=stackalloc KernelVector3[VertexLimit];Span<ulong> mask=stackalloc ulong[VertexLimit];
        Span<int> indices=stackalloc int[IndexLimit];Span<LocalPolygon> polygons=stackalloc LocalPolygon[planes.Length];
        if(!Build(planes,vertices,mask,indices,polygons,out var vertexCount,out _,1e-8))return ParasolidConstants.PK_ERROR_radius_too_large;
        for(var i=0;i<planes.Length;i++)if(polygons[i].Count<3||i>=input.Length&&polygons[i].Count!=4)return ParasolidConstants.PK_ERROR_radius_too_large;
        for(var v=0;v<vertexCount;v++)if(System.Numerics.BitOperations.PopCount(mask[v]>>input.Length)>1)return ParasolidConstants.PK_ERROR_blends_overlap;
        Span<KernelVector3> points=stackalloc KernelVector3[LocalPointLimit];Span<ModelingEdge> edges=stackalloc ModelingEdge[LocalEdgeLimit];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[LocalCoedgeLimit];Span<ModelingLoop> loops=stackalloc ModelingLoop[LocalFaceLimit];
        Span<ModelingFace> modelFaces=stackalloc ModelingFace[LocalFaceLimit];Span<byte> cavity=stackalloc byte[LocalFaceLimit];
        var brep=new LocalBrep{Points=points,Edges=edges,Coedges=coedges,Loops=loops,Faces=modelFaces,Cavities=cavity};
        for(var i=0;i<planes.Length;i++)
        {
            SurfTag surface;KernelSense sense;int error;
            if(i<input.Length){error=TryCloneEntityCore(Faces[faces[i]].SurfTag,out surface);sense=Faces[faces[i]].Orientation;}
            else
            {
                var index=i-input.Length;var edge=Edges[selected[index]];var center=centers[index];var axis=axes[index];sense=ParasolidConstants.PK_TOPOL_sense_positive_c;
                if(edge.PendingBlendKind==1)
                {
                    LocalEdgeFaces(selected[index],out var a,out _);var normal=input[faces.IndexOf(a)].Normal;
                    surface=CreateCylinderSurfaceTag(center.X,center.Y,center.Z,axis.X,axis.Y,axis.Z,normal.X,normal.Y,normal.Z,edge.PendingBlendFirst);
                    error=surface<=0?ParasolidConstants.PK_ERROR_memory_full:0;
                }
                else error=LocalPlaneTag(planes[i].Normal,vertices[indices[polygons[i].First]],out surface);
            }
            if(error!=0)return error;
            if(!AddLocalPolyFace(ref brep,vertices,indices,polygons[i],surface,sense))return ParasolidConstants.PK_ERROR_memory_full;
        }
        for(var i=0;i<selected.Length;i++)if(Edges[selected[i]].PendingBlendKind==1)
        {
            var polygon=polygons[input.Length+i];var axis=axes[i];var center=centers[i];var radius=Edges[selected[i]].PendingBlendFirst;
            for(var j=0;j<polygon.Count;j++)
            {
                var a=indices[polygon.First+j];var b=indices[polygon.First+(j+1)%polygon.Count];var direction=Unit(Sub(vertices[b],vertices[a]));
                if(Math.Abs(Dot(direction,axis))>1-1e-8)continue;
                var capMask=mask[a]&mask[b]&((1UL<<input.Length)-1);var cap=-1;
                for(var f=0;f<input.Length;f++)if((capMask&(1UL<<f))!=0){cap=f;break;}
                if(cap<0)return ParasolidConstants.PK_ERROR_bad_parameter;
                var pa=brep.AddPoint(vertices[a]);var pb=brep.AddPoint(vertices[b]);
                var error=AddLocalCylinderSection(ref brep,pa,pb,center,axis,radius,input[cap]);if(error!=0)return error;
            }
        }
        var finish=brep.Complete(out var product,products);if(finish!=0)return finish;
        finish=ReplaceLocalBody(body,product,faces,products[..brep.FaceCount]);if(finish==0)made=selected.Length;return finish;
    }

    private static int AddLocalCylinderSection(ref LocalBrep brep,int first,int second,KernelVector3 axisPoint,KernelVector3 axis,double radius,LocalPlane cap)
    {
        var dot=Dot(axis,cap.Normal);if(Math.Abs(dot)<1e-8)return ParasolidConstants.PK_ERROR_not_implemented;
        var center=Add(axisPoint,Scale(axis,(cap.Distance-Dot(cap.Normal,axisPoint))/dot));
        if(Math.Abs(Math.Abs(dot)-1)<1e-10)return AddLocalArc(ref brep,first,second,center,axis,radius);
        var radial=Unit(Sub(cap.Normal,Scale(axis,dot)));var major=Unit(Sub(radial,Scale(axis,Dot(cap.Normal,radial)/dot)));
        var minor=ConvexLocalModel.Cross(axis,radial);var normal=Unit(ConvexLocalModel.Cross(major,minor));var majorRadius=radius/Math.Abs(dot);
        var edge=brep.FindEdge(first,second);if(edge<0)return ParasolidConstants.PK_ERROR_bad_parameter;
        first=brep.Edges[edge].StartVertex;second=brep.Edges[edge].EndVertex;
        var a=Sub(brep.Points[first],center);var b=Sub(brep.Points[second],center);
        var start=Math.Atan2(Dot(a,minor)/radius,Dot(a,major)/majorRadius);var end=Math.Atan2(Dot(b,minor)/radius,Dot(b,major)/majorRadius);
        while(end<=start)end+=Math.Tau;
        if(end-start>Math.PI){normal=Scale(normal,-1);start=-start;end=-end;while(end<=start)end+=Math.Tau;}
        var sf=new PK_ELLIPSE_sf_s{R1=majorRadius,R2=radius};
        sf.basis_set.location.coord[0]=center.X;sf.basis_set.location.coord[1]=center.Y;sf.basis_set.location.coord[2]=center.Z;
        sf.basis_set.axis.coord[0]=normal.X;sf.basis_set.axis.coord[1]=normal.Y;sf.basis_set.axis.coord[2]=normal.Z;
        sf.basis_set.ref_direction.coord[0]=major.X;sf.basis_set.ref_direction.coord[1]=major.Y;sf.basis_set.ref_direction.coord[2]=major.Z;
        int curve=0;var error=EllipseCreate(&sf,&curve);if(error!=0)return error;
        brep.Edges[edge].Curve=curve;brep.Edges[edge].Start=start;brep.Edges[edge].End=end;return 0;
    }
}
