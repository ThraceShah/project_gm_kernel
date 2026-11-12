using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int TryClassifyClosedTorus(BodySlot body,in KernelVector3 point,KernelClassCode* enclosure,EntityTag* topology,out bool handled)
    {
        handled=false;var record=Bodies[body];
        if(record.BodyType!=ParasolidConstants.PK_BODY_type_solid_c||record.FaceCountBody!=1
            ||record.EdgeCountBody!=0||record.VertexCountBody!=0||record.RegionCount!=2)return 0;
        var face=record.FirstFaceBody;var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0||Faces[face].LoopCount!=0||Surfaces[slot].Class!=SurfaceClass.Torus
            ||(Faces[face].Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c)!=(Surfaces[slot].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c)
            ||!TryPrepareSurface(Surfaces[slot],out var torus)||!(torus.Radius>torus.Secondary&&torus.Secondary>0))return 0;
        handled=true;var delta=Sub(point,torus.Origin);var axial=Dot(delta,torus.Axis);
        var radial=OperationIntersection.Norm(Sub(delta,Scale(torus.Axis,axial)));
        // Native ring torus: its meridian is the circle of radius b about
        // (a,0). The profile distance also works on the symmetry axis.
        var distance=OperationIntersection.Norm(Vector(radial-torus.Radius,axial,0))-torus.Secondary;
        if(!double.IsFinite(distance))return ParasolidConstants.PK_ERROR_eval_failure;
        var tolerance=Math.Max(OperationIntersection.DistanceTolerance,Faces[face].Tolerance);
        if(!double.IsFinite(tolerance))return ParasolidConstants.PK_ERROR_eval_failure;
        if(Math.Abs(distance)<=tolerance){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Faces[face].Header.Tag;return 0;}
        var inside=distance<0;*enclosure=inside?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c;
        var region=record.FirstRegion;
        for(var i=0;i<record.RegionCount;i++,region=Regions[region].NextInBody)
            if((Regions[region].IsSolid!=0)==inside){*topology=Regions[region].Header.Tag;return 0;}
        return ParasolidConstants.PK_ERROR_eval_failure;
    }
}
