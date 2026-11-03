using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Exact plane section of a complete sphere, preserving native body and source-face identity.</summary>
    private static int TrySectionSphere(EntityTag body,SurfTag surface,PK_BODY_section_o_s* options,PK_section_r_s* result,out bool handled)
    {
        handled=false;if(!TryReadBooleanSphere(body,out var sphere))return 0;
        var slot=GetSurfaceSlotByTag(surface);if(slot<0||Surfaces[slot].Class!=SurfaceClass.Plane)return 0;
        handled=true;ref readonly var plane=ref PlaneDataPool[Surfaces[slot].DataIndex];
        var origin=Vector(plane.LocationX,plane.LocationY,plane.LocationZ);
        var sign=Surfaces[slot].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1;
        var normal=Vector(plane.NormalX*sign,plane.NormalY*sign,plane.NormalZ*sign);
        var height=Dot(Sub(sphere.Center,origin),normal);
        if(!double.IsFinite(height))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(Math.Abs(Math.Abs(height)-sphere.Radius)<=OperationIntersection.DistanceTolerance)
            return ParasolidConstants.PK_ERROR_not_implemented; // Native tangent sections create a point-vertex loop.
        if(Math.Abs(height)>sphere.Radius)
        {
            var front=height>0;
            if(front&&options->fence==ParasolidConstants.PK_section_fence_back_c
                ||!front&&options->fence==ParasolidConstants.PK_section_fence_front_c)return EntityDelete(1,&body);
            var returned=AllocateReturnSlice(1);if(returned==null)return ParasolidConstants.PK_ERROR_memory_full;returned[0]=body;
            if(front)result->front_bodies=new(){length=1,array=returned};else result->back_bodies=new(){length=1,array=returned};return 0;
        }
        var keepFront=options->fence!=ParasolidConstants.PK_section_fence_back_c;
        var keepBack=options->fence!=ParasolidConstants.PK_section_fence_front_c;
        PK_section_r_s output=default;var published=false;
        try
        {
            if(keepFront)
            {
                output.front_bodies=new(){length=1,array=AllocateReturnSlice(1)};
                output.front_faces=new(){length=1,array=AllocateReturnSlice(1)};
                if(output.front_bodies.array==null||output.front_faces.array==null)return ParasolidConstants.PK_ERROR_memory_full;
            }
            if(keepBack)
            {
                output.back_bodies=new(){length=1,array=AllocateReturnSlice(1)};
                output.back_faces=new(){length=1,array=AllocateReturnSlice(1)};
                if(output.back_bodies.array==null||output.back_faces.array==null)return ParasolidConstants.PK_ERROR_memory_full;
            }
            var rr=(sphere.Radius-height)*(sphere.Radius+height);
            if(!(rr>0)||!double.IsFinite(rr))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var center=Sub(sphere.Center,Scale(normal,height));var radius=Math.Sqrt(rr);
            EntityTag frontBody=0,backBody=0;FaceSlot frontSphere=-1,backSphere=-1;var error=0;
            if(keepFront)
            {
                error=BuildSphereSectionCap(sphere,surface,center,normal,radius,true,out frontBody,out frontSphere,out var cap);if(error!=0)return error;
                output.front_bodies.array[0]=frontBody;output.front_faces.array[0]=cap;
            }
            if(keepBack)
            {
                error=BuildSphereSectionCap(sphere,surface,center,normal,radius,false,out backBody,out backSphere,out var cap);if(error!=0)return error;
                output.back_bodies.array[0]=backBody;output.back_faces.array[0]=cap;
            }
            // The native both-sides operation retains the source identity on
            // the front side; a single-side operation retains it on that side.
            var primary=keepFront?frontBody:backBody;Span<FaceSlot> original=stackalloc FaceSlot[1]{sphere.Face};
            Span<FaceSlot> product=stackalloc FaceSlot[1]{keepFront?frontSphere:backSphere};
            error=PreserveModelingFaceHandles(sphere.Body,TagRec(primary).Slot,original,product);if(error!=0)return error;
            error=ReplaceProfileBody(sphere.Body,TagRec(primary).Slot);if(error!=0)return error;
            if(keepFront)output.front_bodies.array[0]=body;else output.back_bodies.array[0]=body;
            *result=output;published=true;return 0;
        }
        finally
        {
            if(!published)
            {
                State.Session->Returns.TryFree(output.front_bodies.array);State.Session->Returns.TryFree(output.front_faces.array);
                State.Session->Returns.TryFree(output.back_bodies.array);State.Session->Returns.TryFree(output.back_faces.array);
            }
        }
    }

    private static int BuildSphereSectionCap(in BooleanSphere sphere,SurfTag plane,in KernelVector3 center,in KernelVector3 normal,double radius,bool front,
        out EntityTag body,out FaceSlot sphereFace,out FaceTag capFace)
    {
        body=0;sphereFace=-1;capFace=0;var error=CloneBooleanSphereSurface(sphere.Surface,out var spherical);if(error!=0)return error;
        error=TryCloneEntityCore(plane,out var planar);if(error!=0)return error;
        var branch=OperationIntersection.Circle(center,normal,OperationIntersection.Reference(normal),radius);CurveTag curve=0;
        error=CreateIntersectionBranch(branch,&curve);if(error!=0)return error;
        // Native plane section stores a negative edge/curve sense. Reverse
        // both fins with it so the material boundary direction is unchanged.
        Curves[GetCurveSlotByTag(curve)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[1];edges[0]=new(){Curve=curve,StartVertex=-1,EndVertex=-1,Start=0,End=Math.Tau};
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2];
        coedges[0]=new(){Edge=0,Forward=(byte)(front?0:1)};coedges[1]=new(){Edge=0,Forward=(byte)(front?1:0)};
        Span<ModelingLoop> loops=stackalloc ModelingLoop[2];loops[0]=new(){FirstCoedge=0,CoedgeCount=1};loops[1]=new(){FirstCoedge=1,CoedgeCount=1};
        Span<ModelingFace> faces=stackalloc ModelingFace[2];
        faces[0]=new(){Surface=spherical,Sense=ParasolidConstants.PK_TOPOL_sense_positive_c,FirstLoop=0,LoopCount=1};
        faces[1]=new(){Surface=planar,Sense=front?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c,FirstLoop=1,LoopCount=1};
        Span<FaceSlot> products=stackalloc FaceSlot[2];
        error=BuildModelingBrep(ReadOnlySpan<KernelVector3>.Empty,edges,coedges,loops,faces,true,out body,products);if(error!=0)return error;
        sphereFace=products[0];capFace=Faces[products[1]].Header.Tag;return 0;
    }

    private static int TryClassifySphereCap(BodySlot body,in KernelVector3 point,int* enclosure,EntityTag* topology,out bool handled)
    {
        handled=false;ref readonly var record=ref Bodies[body];
        if(record.BodyType!=ParasolidConstants.PK_BODY_type_solid_c||record.FaceCountBody!=2||record.EdgeCountBody!=1||record.VertexCountBody!=0)return 0;
        var spherical=record.FirstFaceBody;var planar=Faces[spherical].NextInBody;
        if(!TryReadSphericalFace(spherical,out var center,out var radius,out var outward))
        {var swap=spherical;spherical=planar;planar=swap;if(!TryReadSphericalFace(spherical,out center,out radius,out outward))return 0;}
        if(!outward)return 0;var slot=GetSurfaceSlotByTag(Faces[planar].SurfTag);if(slot<0||Surfaces[slot].Class!=SurfaceClass.Plane)return 0;
        if(!TryReadAnalyticRing(record.FirstEdgeBody,spherical,planar,out var circle,out var direction))return 0;
        ref readonly var plane=ref PlaneDataPool[Surfaces[slot].DataIndex];var origin=Vector(plane.LocationX,plane.LocationY,plane.LocationZ);
        var negative=(Faces[planar].Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c)!=(Surfaces[slot].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c);
        var normal=Vector(plane.NormalX,plane.NormalY,plane.NormalZ);if(negative)normal=Scale(normal,-1);
        var height=Dot(Sub(center,origin),normal);if(!(Math.Abs(height)<radius))return 0;
        var expectedCenter=Sub(center,Scale(normal,height));var expectedRadius=Math.Sqrt((radius-height)*(radius+height));
        var circleCenter=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);var circleAxis=Vector(circle.AxisX,circle.AxisY,circle.AxisZ);
        if(OperationIntersection.Norm(Sub(expectedCenter,circleCenter))>OperationIntersection.DistanceTolerance
            ||Math.Abs(expectedRadius-circle.Radius)>OperationIntersection.DistanceTolerance
            ||Math.Abs(Dot(circleAxis,normal))<1-OperationIntersection.AngularTolerance||Dot(circleAxis,normal)*direction>=0)return 0;
        handled=true;var sphereDistance=OperationIntersection.Norm(Sub(point,center))-radius;var planeDistance=Dot(Sub(point,origin),normal);
        if(!double.IsFinite(sphereDistance)||!double.IsFinite(planeDistance))return ParasolidConstants.PK_ERROR_bad_parameter;
        var onSphere=Math.Abs(sphereDistance)<=OperationIntersection.DistanceTolerance;var onPlane=Math.Abs(planeDistance)<=OperationIntersection.DistanceTolerance;
        if(onSphere&&onPlane){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Edges[record.FirstEdgeBody].Header.Tag;return 0;}
        if(onSphere&&planeDistance<0||onPlane&&sphereDistance<0)
        {*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Faces[onSphere?spherical:planar].Header.Tag;return 0;}
        var inside=sphereDistance<0&&planeDistance<0;*enclosure=inside?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c;
        var region=record.FirstRegion;for(var i=0;i<record.RegionCount;i++,region=Regions[region].NextInBody)
            if((Regions[region].IsSolid!=0)==inside){*topology=Regions[region].Header.Tag;return 0;}
        return ParasolidConstants.PK_ERROR_eval_failure;
    }
}
