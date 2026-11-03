using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct LocalAxialSolid
    {
        internal KernelVector3 Origin,Axis,Reference;
        internal double Radius,Angle,Bottom,Top;
        internal BufferOffset Side,Lower,Upper;
    }

    private static int ReadLocalAxialSolid(BodySlot body,ReadOnlySpan<FaceSlot> faces,out LocalAxialSolid solid)
    {
        solid=default;solid.Side=solid.Lower=solid.Upper=-1;
        if(faces.Length!=3||Bodies[body].EdgeCountBody!=2||Bodies[body].VertexCountBody!=0||Bodies[body].RegionCount!=2)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<faces.Length;i++)
        {
            var record=Surfaces[GetSurfaceSlotByTag(Faces[faces[i]].SurfTag)];
            if(record.Class is SurfaceClass.Cylinder or SurfaceClass.Cone)
            {
                if(solid.Side>=0||LocalSenseSign(record.Sense)*LocalSenseSign(Faces[faces[i]].Orientation)<0)return ParasolidConstants.PK_ERROR_not_implemented;
                solid.Side=i;
                if(record.Class==SurfaceClass.Cylinder)
                {
                    var d=CylinderDataPool[record.DataIndex];solid.Origin=new(){X=d.LocationX,Y=d.LocationY,Z=d.LocationZ};
                    solid.Axis=new(){X=d.AxisX,Y=d.AxisY,Z=d.AxisZ};solid.Reference=new(){X=d.RefDirX,Y=d.RefDirY,Z=d.RefDirZ};solid.Radius=d.Radius;
                }
                else
                {
                    var d=ConeDataPool[record.DataIndex];solid.Origin=new(){X=d.LocationX,Y=d.LocationY,Z=d.LocationZ};
                    solid.Axis=new(){X=d.AxisX,Y=d.AxisY,Z=d.AxisZ};solid.Reference=new(){X=d.RefDirX,Y=d.RefDirY,Z=d.RefDirZ};solid.Radius=d.Radius;solid.Angle=d.SemiAngle;
                }
            }
            else if(record.Class!=SurfaceClass.Plane)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if(solid.Side<0)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<faces.Length;i++)if(i!=solid.Side)
        {
            var record=Surfaces[GetSurfaceSlotByTag(Faces[faces[i]].SurfTag)];var d=PlaneDataPool[record.DataIndex];
            var n=Scale(new(){X=d.NormalX,Y=d.NormalY,Z=d.NormalZ},LocalSenseSign(record.Sense)*LocalSenseSign(Faces[faces[i]].Orientation));
            var dot=Dot(n,solid.Axis);if(Math.Abs(Math.Abs(dot)-1)>1e-10)return ParasolidConstants.PK_ERROR_not_implemented;
            var z=Dot(Sub(new(){X=d.LocationX,Y=d.LocationY,Z=d.LocationZ},solid.Origin),solid.Axis);
            if(dot>0){solid.Upper=i;solid.Top=z;}else{solid.Lower=i;solid.Bottom=z;}
        }
        if(solid.Lower<0||solid.Upper<0||solid.Top<=solid.Bottom)return ParasolidConstants.PK_ERROR_not_implemented;
        return 0;
    }

    private static int BuildAnalyticLocalOffset(BodySlot source,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<double> offsets,
        ReadOnlySpan<byte> pierce,bool hollow,double tolerance,out EntityTag result,Span<FaceSlot> returnedProducts)
    {
        result=0;
        if(faces.Length==1&&Bodies[source].EdgeCountBody==0&&Bodies[source].VertexCountBody==0&&Bodies[source].RegionCount==2)
        {
            var record=Surfaces[GetSurfaceSlotByTag(Faces[faces[0]].SurfTag)];
            if(record.Class is not (SurfaceClass.Sphere or SurfaceClass.Torus))return ParasolidConstants.PK_ERROR_not_implemented;
            if(hollow&&pierce[0]!=0)return ParasolidConstants.PK_ERROR_bad_parameter;
            var error=CloneClosedLocalOffset(faces[0],offsets[0],tolerance,out var moved);if(error!=0)return error;
            Span<ModelingFace> model=stackalloc ModelingFace[hollow?2:1];Span<byte> cavities=stackalloc byte[model.Length];cavities.Clear();
            var sense=NormalizeLocalSense(Faces[faces[0]].Orientation);
            if(hollow)
            {
                error=TryCloneEntityCore(Faces[faces[0]].SurfTag,out var original);if(error!=0)return error;
                var outward=offsets[0]>0;
                model[0]=new(){Surface=original,Sense=outward?FlipLocalSense(sense):sense};
                model[1]=new(){Surface=moved,Sense=outward?sense:FlipLocalSense(sense)};cavities[outward?0:1]=1;
            }
            else model[0]=new(){Surface=moved,Sense=sense};
            Span<FaceSlot> products=stackalloc FaceSlot[model.Length];
            error=BuildModelingBrep(default,default,default,default,model,true,out var product,products,cavities);
            if(error!=0)return error;
            error=ReplaceLocalBody(source,product,faces,products,!hollow);if(error!=0)return error;
            if(!returnedProducts.IsEmpty)products.CopyTo(returnedProducts);
            result=Bodies[source].Header.Tag;return 0;
        }
        var axialError=ReadLocalAxialSolid(source,faces,out var axial);if(axialError!=0)return axialError;
        if(hollow&&pierce[axial.Side]!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var radius=axial.Radius+offsets[axial.Side]/Math.Cos(axial.Angle);
        var lower=axial.Bottom-offsets[axial.Lower];var upper=axial.Top+offsets[axial.Upper];var slope=Math.Tan(axial.Angle);
        if(!double.IsFinite(radius)||!double.IsFinite(lower)||!double.IsFinite(upper)||upper-lower<=tolerance
            ||radius+slope*lower<=tolerance||radius+slope*upper<=tolerance)return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<KernelVector3> points=stackalloc KernelVector3[1];Span<ModelingEdge> edges=stackalloc ModelingEdge[4];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[16];Span<ModelingLoop> loops=stackalloc ModelingLoop[16];
        Span<ModelingFace> modelFaces=stackalloc ModelingFace[6];Span<byte> cavity=stackalloc byte[6];
        var brep=new LocalBrep{Points=points,Edges=edges,Coedges=coedges,Loops=loops,Faces=modelFaces,Cavities=cavity};
        var lowerRing=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,lower)),axial.Axis,axial.Reference,radius+slope*lower);
        var upperRing=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,upper)),axial.Axis,axial.Reference,radius+slope*upper);
        if(lowerRing<0||upperRing<0)return ParasolidConstants.PK_ERROR_memory_full;
        var oldLower=-1;var oldUpper=-1;var positive=false;var closed=hollow;
        for(var i=0;i<offsets.Length;i++){positive|=offsets[i]>0;if(hollow&&pierce[i]!=0)closed=false;}
        if(hollow)
        {
            oldLower=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Bottom)),axial.Axis,axial.Reference,axial.Radius+slope*axial.Bottom);
            oldUpper=brep.AddRing(Add(axial.Origin,Scale(axial.Axis,axial.Top)),axial.Axis,axial.Reference,axial.Radius+slope*axial.Top);
            if(oldLower<0||oldUpper<0)return ParasolidConstants.PK_ERROR_memory_full;
            for(var i=0;i<faces.Length;i++)
            {
                var error=TryCloneEntityCore(Faces[faces[i]].SurfTag,out var surface);if(error!=0)return error;
                var sense=Faces[faces[i]].Orientation;
                if(pierce[i]!=0)
                {
                    var outer=i==axial.Lower?(positive?lowerRing:oldLower):(positive?upperRing:oldUpper);
                    var inner=i==axial.Lower?(positive?oldLower:lowerRing):(positive?oldUpper:upperRing);
                    if(!brep.AddRingFace(surface,sense,outer,i==axial.Upper,inner,i!=axial.Upper))return ParasolidConstants.PK_ERROR_memory_full;
                }
                else if(!AddLocalAxialFace(ref brep,i,in axial,surface,positive?FlipLocalSense(sense):sense,oldLower,oldUpper,positive,(byte)(positive&&closed?1:0)))return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        for(var i=0;i<faces.Length;i++)
        {
            if(hollow&&pierce[i]!=0)continue;
            var error=TryCloneEntityCore(Faces[faces[i]].SurfTag,out var surface);if(error!=0)return error;
            var record=Surfaces[GetSurfaceSlotByTag(surface)];
            if(i==axial.Side)
            {
                if(record.Class==SurfaceClass.Cylinder)CylinderDataPool[record.DataIndex].Radius=radius;
                else ConeDataPool[record.DataIndex].Radius=radius;
            }
            else
            {
                ref var p=ref PlaneDataPool[record.DataIndex];var delta=Scale(axial.Axis,i==axial.Lower?-offsets[i]:offsets[i]);
                p.LocationX+=delta.X;p.LocationY+=delta.Y;p.LocationZ+=delta.Z;
            }
            var reverse=hollow&&!positive;var sense=Faces[faces[i]].Orientation;
            if(!AddLocalAxialFace(ref brep,i,in axial,surface,reverse?FlipLocalSense(sense):sense,lowerRing,upperRing,reverse,(byte)(reverse&&closed?1:0)))return ParasolidConstants.PK_ERROR_memory_full;
        }
        // PK's recomputed offset intersection rings carry negative curve uses.
        // Reorient the edge/coedge pair together, preserving material traversal.
        brep.NegativeCurveUse(lowerRing);brep.NegativeCurveUse(upperRing);
        Span<FaceSlot> outputFaces=stackalloc FaceSlot[brep.FaceCount];
        var finish=brep.Complete(out var output,outputFaces);if(finish!=0)return finish;
        finish=ReplaceLocalBody(source,output,faces,outputFaces,!hollow);if(finish!=0)return finish;
        if(!returnedProducts.IsEmpty)outputFaces.CopyTo(returnedProducts);
        result=Bodies[source].Header.Tag;return 0;
    }

    private static bool AddLocalAxialFace(ref LocalBrep brep,int index,in LocalAxialSolid axial,SurfTag surface,
        KernelSense sense,int lower,int upper,bool reverse,byte cavity)
        =>index==axial.Side?brep.AddRingFace(surface,sense,lower,!reverse,upper,reverse,cavity):
            brep.AddRingFace(surface,sense,index==axial.Lower?lower:upper,(index==axial.Upper)!=reverse,cavity:cavity);

    private static int CloneClosedLocalOffset(FaceSlot face,double distance,double tolerance,out SurfTag surface)
    {
        surface=0;var record=Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)];
        var change=distance*LocalSenseSign(record.Sense)*LocalSenseSign(Faces[face].Orientation);
        if(record.Class==SurfaceClass.Sphere)
        {
            var radius=SphereDataPool[record.DataIndex].Radius+change;
            if(!double.IsFinite(radius)||radius<=tolerance)return ParasolidConstants.PK_ERROR_bad_parameter;
            var error=TryCloneEntityCore(Faces[face].SurfTag,out surface);if(error!=0)return error;
            SphereDataPool[Surfaces[GetSurfaceSlotByTag(surface)].DataIndex].Radius=radius;return 0;
        }
        var torus=TorusDataPool[record.DataIndex];var minor=torus.MinorRadius+change;
        if(!double.IsFinite(minor)||minor<=tolerance||minor>=torus.MajorRadius-tolerance)return ParasolidConstants.PK_ERROR_bad_parameter;
        var result=TryCloneEntityCore(Faces[face].SurfTag,out surface);if(result!=0)return result;
        TorusDataPool[Surfaces[GetSurfaceSlotByTag(surface)].DataIndex].MinorRadius=minor;return 0;
    }
}
