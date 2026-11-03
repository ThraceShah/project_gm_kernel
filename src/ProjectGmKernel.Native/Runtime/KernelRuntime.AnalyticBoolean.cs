using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private readonly struct BooleanSphere(BodySlot body,FaceSlot face,SurfTag surface,KernelVector3 center,double radius)
    {
        internal readonly BodySlot Body=body;
        internal readonly FaceSlot Face=face;
        internal readonly SurfTag Surface=surface;
        internal readonly KernelVector3 Center=center;
        internal readonly double Radius=radius;
    }
    private enum SphereBooleanCase:byte{Empty,KeepTarget,TakeTool,DisjointUnion,Cavity,Transverse}

    /// <summary>
    /// Exact regular Boolean of two complete analytic sphere solids. A native
    /// ring edge separates the retained spherical patches; nested subtraction
    /// has an explicitly oriented cavity component. No faceting or real PK call
    /// participates in construction.
    /// </summary>
    private static int TryAnalyticBoolean(ReadOnlySpan<EntityTag> inputs,PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_boolean_r_s* result,out bool handled)
    {
        handled=false;
        if(inputs.Length!=2||!TryReadBooleanSphere(inputs[0],out var a)||!TryReadBooleanSphere(inputs[1],out var b))return 0;
        handled=true;
        var delta=Sub(b.Center,a.Center);var distance=OperationIntersection.Norm(delta);
        if(!double.IsFinite(distance))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var sum=a.Radius+b.Radius;var difference=Math.Abs(a.Radius-b.Radius);
        if(!double.IsFinite(sum))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var operation=options->function;var status=ParasolidConstants.PK_boolean_result_success_c;
        SphereBooleanCase relation;
        if(distance==0&&a.Radius==b.Radius)
        {
            relation=operation==ParasolidConstants.PK_boolean_subtract_c?SphereBooleanCase.Empty:SphereBooleanCase.KeepTarget;
            if(operation==ParasolidConstants.PK_boolean_intersect_c)status=ParasolidConstants.PK_boolean_result_no_clash_c;
        }
        else if(distance>sum+OperationIntersection.DistanceTolerance)
        {
            relation=operation==ParasolidConstants.PK_boolean_unite_c?SphereBooleanCase.DisjointUnion
                :operation==ParasolidConstants.PK_boolean_subtract_c?SphereBooleanCase.KeepTarget:SphereBooleanCase.Empty;
            status=ParasolidConstants.PK_boolean_result_no_clash_c;
        }
        else if(distance<difference-OperationIntersection.DistanceTolerance)
        {
            var toolInside=a.Radius>b.Radius;
            relation=operation==ParasolidConstants.PK_boolean_unite_c?(toolInside?SphereBooleanCase.KeepTarget:SphereBooleanCase.TakeTool)
                :operation==ParasolidConstants.PK_boolean_intersect_c?(toolInside?SphereBooleanCase.TakeTool:SphereBooleanCase.KeepTarget)
                :toolInside?SphereBooleanCase.Cavity:SphereBooleanCase.Empty;
            if(operation==ParasolidConstants.PK_boolean_intersect_c&&!toolInside)status=ParasolidConstants.PK_boolean_result_no_clash_c;
        }
        else if(Math.Abs(distance-sum)<=OperationIntersection.DistanceTolerance||Math.Abs(distance-difference)<=OperationIntersection.DistanceTolerance)
        {
            // PK tangencies introduce degenerate vertex loops, and internal
            // tangent subtraction can return a failure result without consuming
            // either input. Those singular-topology cases are not a regular cap.
            return ParasolidConstants.PK_ERROR_not_implemented;
        }
        else relation=SphereBooleanCase.Transverse;

        var count=relation==SphereBooleanCase.Empty?0:relation==SphereBooleanCase.DisjointUnion?2:1;
        var returned=count==0?null:AllocateReturnSlice(count);
        if(count>0&&returned==null)return ParasolidConstants.PK_ERROR_memory_full;
        PK_TOPOL_track_r_s localTracking=default;var completed=false;
        try
        {
            var target=inputs[0];var tool=inputs[1];var error=0;
            switch(relation)
            {
                case SphereBooleanCase.Empty:
                    error=EntityDelete(1,&target);if(error==0)error=EntityDelete(1,&tool);break;
                case SphereBooleanCase.KeepTarget:
                    error=EntityDelete(1,&tool);break;
                case SphereBooleanCase.TakeTool:
                    // Moving the tool topology preserves its surviving face
                    // identity while retaining the original target body tag.
                    error=ReplaceProfileBody(a.Body,b.Body);break;
                case SphereBooleanCase.DisjointUnion:
                    returned[1]=tool;break;
                default:
                    error=BuildSphereBoolean(a,b,operation,relation==SphereBooleanCase.Cavity,delta,distance,
                        options->tracking!=0,&localTracking,out var created);
                    if(error!=0)break;
                    error=ReplaceProfileBody(a.Body,TagRec(created).Slot);
                    if(error==0)error=EntityDelete(1,&tool);
                    break;
            }
            if(error!=0)return error;
            if(count>0)returned[0]=target;
            *tracking=localTracking;*result=new PK_boolean_r_s{result=status,n_bodies=count,bodies=returned};completed=true;return 0;
        }
        finally
        {
            if(!completed)
            {
                State.Session->Returns.TryFree(returned);
                if(localTracking.track_records!=null)TopolTrackResultFree(&localTracking);
            }
        }
    }

    private static bool TryReadBooleanSphere(EntityTag tag,out BooleanSphere sphere)
    {
        sphere=default;if(!TryResolveBodySlot(tag,out var body))return false;
        ref readonly var record=ref Bodies[body];
        if(record.BodyType!=ParasolidConstants.PK_BODY_type_solid_c||record.FaceCountBody!=1||record.EdgeCountBody!=0||record.VertexCountBody!=0)return false;
        var face=record.FirstFaceBody;if(Faces[face].LoopCount!=0||!TryReadSphericalFace(face,out var center,out var radius,out var outward)||!outward)return false;
        sphere=new(body,face,Faces[face].SurfTag,center,radius);return true;
    }

    private static int BuildSphereBoolean(in BooleanSphere a,in BooleanSphere b,int operation,bool cavity,
        KernelVector3 delta,double distance,bool wantTracking,PK_TOPOL_track_r_s* tracking,out EntityTag output)
    {
        output=0;
        var error=CloneBooleanSphereSurface(a.Surface,out var first);if(error!=0)return error;
        error=CloneBooleanSphereSurface(b.Surface,out var second);if(error!=0)return error;
        Span<ModelingFace> faces=stackalloc ModelingFace[2];Span<ModelingLoop> loops=stackalloc ModelingLoop[cavity?0:2];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[cavity?0:2];Span<ModelingEdge> edges=stackalloc ModelingEdge[cavity?0:1];
        Span<FaceSlot> products=stackalloc FaceSlot[2];Span<byte> cavityFaces=stackalloc byte[2]{0,(byte)(cavity?1:0)};
        faces[0]=new(){Surface=first,Sense=ParasolidConstants.PK_TOPOL_sense_positive_c,FirstLoop=0,LoopCount=cavity?0:1};
        faces[1]=new(){Surface=second,Sense=operation==ParasolidConstants.PK_boolean_subtract_c?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c,
            FirstLoop=cavity?0:1,LoopCount=cavity?0:1};
        if(!cavity)
        {
            if(!(distance>0))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var normal=Scale(delta,1/distance);
            var h=(distance*distance+(a.Radius-b.Radius)*(a.Radius+b.Radius))/(2*distance);
            var rr=(a.Radius-h)*(a.Radius+h);if(!(rr>0)||!double.IsFinite(rr))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var center=Add(a.Center,Scale(normal,h));var circle=OperationIntersection.Circle(center,normal,OperationIntersection.Reference(normal),Math.Sqrt(rr));
            int curve=0;error=CreateIntersectionBranch(circle,&curve);if(error!=0)return error;
            edges[0]=new(){Curve=curve,StartVertex=-1,EndVertex=-1,Start=0,End=Math.Tau};
            var forward=operation==ParasolidConstants.PK_boolean_intersect_c?(byte)1:(byte)0;
            coedges[0]=new(){Edge=0,Forward=forward};coedges[1]=new(){Edge=0,Forward=(byte)(1-forward)};
            loops[0]=new(){FirstCoedge=0,CoedgeCount=1};loops[1]=new(){FirstCoedge=1,CoedgeCount=1};
        }
        error=BuildModelingBrep(ReadOnlySpan<KernelVector3>.Empty,edges,coedges,loops,faces,true,out output,products,cavityFaces);if(error!=0)return error;
        var built=TagRec(output).Slot;
        Span<FaceSlot> original=stackalloc FaceSlot[1];original[0]=a.Face;
        error=PreserveModelingFaceHandles(a.Body,built,original,products[..1]);if(error!=0)return error;
        original[0]=b.Face;error=PreserveModelingFaceHandles(b.Body,built,original,products[1..]);if(error!=0)return error;
        if(!cavity&&wantTracking)
            error=CreateSphereBooleanTracking(Faces[a.Face].Header.Tag,Faces[b.Face].Header.Tag,Edges[Bodies[built].FirstEdgeBody].Header.Tag,tracking);
        return error;
    }

    private static int CloneBooleanSphereSurface(SurfTag original,out SurfTag copy)
    {
        copy=0;var definition=new PK_SPHERE_sf_s();var error=SphereAsk(original,&definition);if(error!=0)return error;
        int created=0;error=SphereCreate(&definition,&created);copy=created;return error;
    }

    private static int CreateSphereBooleanTracking(FaceTag first,FaceTag second,EntityTag edge,PK_TOPOL_track_r_s* result)
    {
        var record=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate((nuint)sizeof(PK_TOPOL_track_record_r_s)+5*sizeof(EntityTag));
        if(record==null)return ParasolidConstants.PK_ERROR_memory_full;
        var originals=(EntityTag*)(record+1);var classes=originals+2;var products=classes+2;
        originals[0]=first;originals[1]=second;classes[0]=classes[1]=ParasolidConstants.PK_CLASS_face;products[0]=edge;
        *record=new(){track=ParasolidConstants.PK_TOPOL_track_derive_c,n_original_topols=2,original_topols=originals,original_classes=classes,n_product_topols=1,product_topols=products};
        *result=new(){n_track_records=1,track_records=record,internal_origs=originals,internal_classes=classes,internal_prods=products};return 0;
    }

    /// <summary>Exact material classification of full spheres and regular two-sphere Boolean results.</summary>
    private static int TryClassifySphereBoolean(BodySlot body,in KernelVector3 point,int* enclosure,EntityTag* topology,out bool handled)
    {
        handled=false;ref readonly var record=ref Bodies[body];
        if(record.BodyType!=ParasolidConstants.PK_BODY_type_solid_c||record.FaceCountBody is <1 or >2||record.VertexCountBody!=0||record.EdgeCountBody>1)return 0;
        Span<FaceSlot> face=stackalloc FaceSlot[2];Span<KernelVector3> centers=stackalloc KernelVector3[2];Span<double> radii=stackalloc double[2];Span<byte> outward=stackalloc byte[2];
        var current=record.FirstFaceBody;
        for(var i=0;i<record.FaceCountBody;i++,current=Faces[current].NextInBody)
        {
            face[i]=current;if(!TryReadSphericalFace(current,out centers[i],out radii[i],out var positive))
                return TryClassifySphereCap(body,in point,enclosure,topology,out handled);
            outward[i]=(byte)(positive?1:0);
        }
        var operation=ParasolidConstants.PK_boolean_intersect_c;
        if(record.FaceCountBody==1)
        {
            if(record.EdgeCountBody!=0||Faces[face[0]].LoopCount!=0||outward[0]==0)return 0;
        }
        else
        {
            if(outward[0]==0)
            {(face[0],face[1])=(face[1],face[0]);(centers[0],centers[1])=(centers[1],centers[0]);(radii[0],radii[1])=(radii[1],radii[0]);(outward[0],outward[1])=(outward[1],outward[0]);}
            if(outward[0]==0)return 0;
            var displacement=Sub(centers[1],centers[0]);var separation=OperationIntersection.Norm(displacement);
            if(record.EdgeCountBody==0)
            {
                if(outward[1]!=0||Faces[face[0]].LoopCount!=0||Faces[face[1]].LoopCount!=0
                    ||!(separation+radii[1]<radii[0]-OperationIntersection.DistanceTolerance))return 0;
                operation=ParasolidConstants.PK_boolean_subtract_c;
            }
            else
            {
                if(!(separation>Math.Abs(radii[0]-radii[1]))||!(separation<radii[0]+radii[1]))return 0;
                if(!TryReadAnalyticRing(record.FirstEdgeBody,face[0],face[1],out var circle,out var firstDirection))return 0;
                var expectedHeight=(separation*separation+(radii[0]-radii[1])*(radii[0]+radii[1]))/(2*separation);
                var expectedCenter=Add(centers[0],Scale(displacement,expectedHeight/separation));
                var expectedRadius=Math.Sqrt((radii[0]-expectedHeight)*(radii[0]+expectedHeight));
                var circleCenter=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);var axis=Vector(circle.AxisX,circle.AxisY,circle.AxisZ);
                if(OperationIntersection.Norm(Sub(expectedCenter,circleCenter))>OperationIntersection.DistanceTolerance
                    ||Math.Abs(expectedRadius-circle.Radius)>OperationIntersection.DistanceTolerance
                    ||Math.Abs(Dot(displacement,axis))/separation<1-OperationIntersection.AngularTolerance)return 0;
                var orientation=Dot(displacement,axis)*firstDirection;
                operation=outward[1]==0?ParasolidConstants.PK_boolean_subtract_c
                    :orientation>0?ParasolidConstants.PK_boolean_intersect_c:ParasolidConstants.PK_boolean_unite_c;
                if(outward[1]==0&&orientation>=0)return 0;
            }
        }
        handled=true;var da=OperationIntersection.Norm(Sub(point,centers[0]))-radii[0];
        var db=record.FaceCountBody==2?OperationIntersection.Norm(Sub(point,centers[1]))-radii[1]:-1;
        if(!double.IsFinite(da)||!double.IsFinite(db))return ParasolidConstants.PK_ERROR_bad_parameter;
        var onA=Math.Abs(da)<=OperationIntersection.DistanceTolerance;var onB=record.FaceCountBody==2&&Math.Abs(db)<=OperationIntersection.DistanceTolerance;
        var insideA=da<0;var insideB=db<0;var boundary=0;
        if(onA&&onB&&record.EdgeCountBody==1)boundary=Edges[record.FirstEdgeBody].Header.Tag;
        else if(onA&&(record.FaceCountBody==1||SphereBooleanMaterial(false,insideB,operation)!=SphereBooleanMaterial(true,insideB,operation)))boundary=Faces[face[0]].Header.Tag;
        else if(onB&&SphereBooleanMaterial(insideA,false,operation)!=SphereBooleanMaterial(insideA,true,operation))boundary=Faces[face[1]].Header.Tag;
        if(boundary!=0){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=boundary;return 0;}
        var inside=record.FaceCountBody==1?insideA:SphereBooleanMaterial(insideA,insideB,operation);
        *enclosure=inside?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c;
        if(!inside&&operation==ParasolidConstants.PK_boolean_subtract_c&&record.EdgeCountBody==0&&insideB)
        {
            var shell=Faces[face[1]].FrontShell;
            if(shell<0)return ParasolidConstants.PK_ERROR_eval_failure;
            *topology=Regions[Shells[shell].Region].Header.Tag;return 0;
        }
        var region=record.FirstRegion;
        for(var i=0;i<record.RegionCount;i++,region=Regions[region].NextInBody)
            if((Regions[region].IsSolid!=0)==inside){*topology=Regions[region].Header.Tag;return 0;}
        return ParasolidConstants.PK_ERROR_eval_failure;
    }
    private static bool TryReadSphericalFace(FaceSlot face,out KernelVector3 center,out double radius,out bool outward)
    {
        center=default;radius=0;outward=false;var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0||Surfaces[slot].Class!=SurfaceClass.Sphere)return false;
        ref readonly var sphere=ref SphereDataPool[Surfaces[slot].DataIndex];center=Vector(sphere.CenterX,sphere.CenterY,sphere.CenterZ);radius=sphere.Radius;
        outward=(Faces[face].Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c)==(Surfaces[slot].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c);
        return true;
    }

    private static bool TryReadAnalyticRing(EdgeSlot edge,FaceSlot first,FaceSlot second,out CircleData circle,out double firstDirection)
    {
        circle=default;firstDirection=0;
        if(Edges[edge].StartVertex>=0||Edges[edge].EndVertex>=0||Edges[edge].FinCount!=2
            ||Faces[first].LoopCount!=1||Faces[second].LoopCount!=1)return false;
        var firstLoop=Faces[first].FirstLoop;var secondLoop=Faces[second].FirstLoop;
        if(Loops[firstLoop].FinCount!=1||Loops[secondLoop].FinCount!=1)return false;
        var a=Loops[firstLoop].FirstFin;var b=Loops[secondLoop].FirstFin;
        if(Fins[a].Edge!=edge||Fins[b].Edge!=edge||Fins[a].Sense==Fins[b].Sense)return false;
        var curve=GetCurveSlotByTag(Edges[edge].CurveTag);if(curve<0||Curves[curve].Class!=CurveClass.Circle)return false;
        if(Edges[edge].HasParameterRange!=0&&Math.Abs(Math.Abs(Edges[edge].ParameterEnd-Edges[edge].ParameterStart)-Math.Tau)>1e-10)return false;
        circle=CircleDataPool[Curves[curve].DataIndex];
        firstDirection=(Fins[a].Sense=='+'?1:-1)*(Curves[curve].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1);return true;
    }

    private static bool SphereBooleanMaterial(bool a,bool b,int operation)
        =>operation==ParasolidConstants.PK_boolean_unite_c?a||b:operation==ParasolidConstants.PK_boolean_intersect_c?a&&b:a&&!b;
}
