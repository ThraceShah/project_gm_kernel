using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyExtrude(EntityTag profile, PK_VECTOR_s path, PK_BODY_extrude_o_s* options,
        EntityTag* body, PK_TOPOL_track_r_s* tracking, PK_TOPOL_local_r_s* results)
    {
        if (Dispatcher.IsExecuting) return BodyExtrudeCore(profile, path, options, body, tracking, results);
        var command = new ProfileExtrudeCommand { Profile=profile, Path=path, Options=options,
            Body=body, Tracking=tracking, Results=results };
        return Dispatch(ApiId.BodyExtrude, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, profile);
    }

    private struct ProfileExtrudeCommand : IKernelCommand
    {
        internal EntityTag Profile;
        internal PK_VECTOR_s Path;
        internal PK_BODY_extrude_o_s* Options;
        internal EntityTag* Body;
        internal PK_TOPOL_track_r_s* Tracking;
        internal PK_TOPOL_local_r_s* Results;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return BodyExtrudeCore(Profile,Path,Options,Body,Tracking,Results);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int BodyExtrudeCore(EntityTag profile, PK_VECTOR_s path, PK_BODY_extrude_o_s* options,
        EntityTag* body, PK_TOPOL_track_r_s* tracking, PK_TOPOL_local_r_s* results)
    {
        if(body!=null)*body=0;
        if(tracking!=null)*tracking=default;
        if(results!=null)*results=default;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||body==null||tracking==null||results==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=7)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(options->start_bound.bound!=ParasolidConstants.PK_bound_distance_c
            ||options->end_bound.bound!=ParasolidConstants.PK_bound_distance_c||options->extruded_body!=0)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var direction=Vector(path.coord[0],path.coord[1],path.coord[2]);
        if(!ProfileUnit(in direction)||options->start_bound.forward>1||options->end_bound.forward>1
            ||!double.IsFinite(options->start_bound.distance)||!double.IsFinite(options->end_bound.distance)
            ||options->start_bound.distance<0||options->end_bound.distance<0)return ParasolidConstants.PK_ERROR_bad_parameter;
        var start=options->start_bound.distance*(options->start_bound.forward!=0?1:-1);
        var end=options->end_bound.distance*(options->end_bound.forward!=0?1:-1);
        if(!(end>start))return ParasolidConstants.PK_ERROR_bad_parameter;
        var error=BuildProfileSweep(profile, false, ProfileScale(direction,start), direction,end-start,out var resultBody);
        if(error!=0)return error;
        error=CreateProfileExtrudeTracking(profile,resultBody,tracking);
        if(error!=0)return error;
        *body=resultBody;
        results->status=ParasolidConstants.PK_local_status_ok_c;
        return 0;
    }

    public static int BodySpin(EntityTag body, PK_AXIS1_sf_s* axis, double angle, byte localCheck,
        int* lateralCount, EntityTag** laterals, EntityTag** bases, int* checkResult)
    {
        if(Dispatcher.IsExecuting)return BodySpinCore(body,axis,angle,localCheck,lateralCount,laterals,bases,checkResult);
        var command=new ProfileSpinCommand{Body=body,Axis=axis,Angle=angle,LocalCheck=localCheck,
            LateralCount=lateralCount,Laterals=laterals,Bases=bases,CheckResult=checkResult};
        return Dispatch(ApiId.BodySpin,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,body);
    }

    private struct ProfileSpinCommand:IKernelCommand
    {
        internal EntityTag Body;
        internal PK_AXIS1_sf_s* Axis;
        internal double Angle;
        internal byte LocalCheck;
        internal BufferCount* LateralCount;
        internal EntityTag** Laterals;
        internal EntityTag** Bases;
        internal KernelClassCode* CheckResult;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return BodySpinCore(Body,Axis,Angle,LocalCheck,LateralCount,Laterals,Bases,CheckResult);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int BodySpinCore(EntityTag body, PK_AXIS1_sf_s* axis, double angle, byte localCheck,
        int* lateralCount, EntityTag** laterals, EntityTag** bases, int* checkResult)
    {
        if(lateralCount!=null)*lateralCount=0;
        if(laterals!=null)*laterals=null;
        if(bases!=null)*bases=null;
        if(checkResult!=null)*checkResult=ParasolidConstants.PK_local_check_ok_c;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(axis==null||lateralCount==null||checkResult==null||localCheck>1
            ||!double.IsFinite(angle)||angle==0||Math.Abs(angle)>Math.Tau)return ParasolidConstants.PK_ERROR_bad_parameter;
        var origin=Vector(axis->location.coord[0],axis->location.coord[1],axis->location.coord[2]);
        var direction=Vector(axis->axis.coord[0],axis->axis.coord[1],axis->axis.coord[2]);
        if(!ProfileFinite(in origin)||!ProfileUnit(in direction))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!TryResolveBodySlot(body,out var sourceSlot))return ParasolidConstants.PK_ERROR_bad_class;
        var n=Bodies[sourceSlot].EdgeCountBody;
        if(n<=0||n>ProfileEdgeLimit||Bodies[sourceSlot].VertexCountBody>ProfileEdgeLimit)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<int> baseProducts=stackalloc int[n];
        Span<int> baseVertices=stackalloc int[Bodies[sourceSlot].VertexCountBody];
        var error=BuildProfileSweep(body,true,origin,direction,angle,out var resultBody,baseProducts,baseVertices);
        if(error!=0)return error;
        var resultSlot=TagRec(resultBody).Slot;
        error=PreserveProfileBaseHandles(sourceSlot,resultSlot,baseProducts,baseVertices);
        if(error!=0)return error;
        var lateralOutput=laterals!=null?AllocateReturnSlice(n):null;
        var baseOutput=bases!=null?AllocateReturnSlice(n):null;
        if(laterals!=null&&lateralOutput==null||bases!=null&&baseOutput==null)return ParasolidConstants.PK_ERROR_memory_full;
        var face=Bodies[resultSlot].FirstFaceBody;
        // Sweep construction publishes lateral faces first, then optional caps.
        for(var i=0;i<n;i++,face=Faces[face].NextInBody)
        {
            if(lateralOutput!=null)lateralOutput[i]=Faces[face].Header.Tag;
            if(baseOutput!=null)baseOutput[i]=Edges[baseProducts[i]].Header.Tag;
        }
        error=ReplaceProfileBody(sourceSlot,resultSlot);
        if(error!=0)return error;
        *lateralCount=n;
        if(laterals!=null)*laterals=lateralOutput;
        if(bases!=null)*bases=baseOutput;
        return 0;
    }

    private static int BuildProfileSweep(EntityTag profile,bool spin,KernelVector3 origin,
        KernelVector3 direction,double extent,out EntityTag result,Span<int> baseProducts=default,Span<int> basePoints=default)
    {
        result=0;
        if(!TryResolveBodySlot(profile,out var source))return ParasolidConstants.PK_ERROR_bad_class;
        ref readonly var input=ref Bodies[source];
        if(input.Header.Partition!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(input.BodyType!=ParasolidConstants.PK_BODY_type_sheet_c||input.FaceCountBody!=1
            ||input.EdgeCountBody<=0||input.EdgeCountBody>ProfileEdgeLimit)
            return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var inputFace=ref Faces[input.FirstFaceBody];
        var planeSlot=GetSurfaceSlotByTag(inputFace.SurfTag);
        if(planeSlot<0||Surfaces[planeSlot].Class!=SurfaceClass.Plane)return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var plane=ref PlaneDataPool[Surfaces[planeSlot].DataIndex];
        var normal=Vector(plane.NormalX,plane.NormalY,plane.NormalZ);
        if(inputFace.Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c)normal=ProfileScale(normal,-1);
        var point=Vector(plane.LocationX,plane.LocationY,plane.LocationZ);
        var full=spin&&Math.Abs(extent)==Math.Tau;
        var orientation=1;
        if(!spin)
        {
            var perpendicular=ProfileDot(direction,normal);
            if(Math.Abs(perpendicular)<=1e-10)return ParasolidConstants.PK_ERROR_bad_parameter;
            orientation=perpendicular>0?1:-1;
        }
        else
        {
            // A planar profile for a solid of revolution must lie in a plane
            // through the axis and entirely on one side of it. Axis crossings
            // require singular-topology splitting and are rejected here.
            if(Math.Abs(ProfileDot(direction,normal))>1e-10
                ||Math.Abs(ProfileDot(ProfileSubtract(origin,point),normal))>1e-8)
                return ParasolidConstants.PK_ERROR_bad_parameter;
            var radial=ProfileCross(normal,direction);
            var first=GetProfileRepresentative(input.FirstEdgeBody);
            var side=ProfileDot(ProfileSubtract(first,origin),radial);
            if(Math.Abs(side)<1e-8)return ParasolidConstants.PK_ERROR_bad_parameter;
            var edge=input.FirstEdgeBody;
            Span<KernelVector3> hull=stackalloc KernelVector3[512];
            for(var i=0;i<input.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
            {
                var hullCount=ProfileControlHull(in Edges[edge],hull);
                if(hullCount==0)return ParasolidConstants.PK_ERROR_not_implemented;
                for(var k=0;k<hullCount;k++)
                    if(ProfileDot(ProfileSubtract(hull[k],origin),radial)*Math.Sign(side)<=1e-8)
                        return ParasolidConstants.PK_ERROR_not_implemented;
            }
            var motion=ProfileCross(direction,ProfileSubtract(first,origin));
            orientation=ProfileDot(motion,normal)*extent>0?1:-1;
        }

        Span<int> inputEdges=stackalloc int[input.EdgeCountBody];
        Span<int> inputVertices=stackalloc int[input.VertexCountBody];
        var e=input.FirstEdgeBody;
        for(var i=0;i<inputEdges.Length;i++,e=Edges[e].NextInBody)inputEdges[i]=e;
        var v=input.FirstVertexBody;
        for(var i=0;i<inputVertices.Length;i++,v=Vertices[v].NextInBody)inputVertices[i]=v;
        Span<int> bottomVertices=stackalloc int[inputVertices.Length];
        Span<int> topVertices=stackalloc int[inputVertices.Length];
        Span<int> vertical=stackalloc int[inputVertices.Length];
        Span<int> bottomEdges=stackalloc int[inputEdges.Length];
        Span<int> topEdges=stackalloc int[inputEdges.Length];
        Span<int> sourceDirections=stackalloc int[inputEdges.Length];
        Span<int> sourceLoops=stackalloc int[inputEdges.Length];
        var loop=inputFace.FirstLoop;
        for(var i=0;i<inputFace.LoopCount;i++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var j=0;j<Loops[loop].FinCount;j++,fin=Fins[fin].NextInLoop)
            {
                var index=inputEdges.IndexOf(Fins[fin].Edge);
                if(index<0)return ParasolidConstants.PK_ERROR_bad_loop;
                sourceDirections[index]=Fins[fin].Sense=='+'?1:-1;
                sourceLoops[index]=i;
            }
        }
        var output=TryAllocateBodies();
        if(output<0)return ParasolidConstants.PK_ERROR_memory_full;
        InitializeBody(ref Bodies[output]);AssignPartition(ref Bodies[output].Header,CurrentPartition);
        if(!CreateSolidRegionsAndShells(output,out var voidShell,out var solidShell))return ParasolidConstants.PK_ERROR_memory_full;
        Span<int> voidShells=stackalloc int[inputFace.LoopCount];
        Span<int> solidShells=stackalloc int[inputFace.LoopCount];
        voidShells.Fill(voidShell);solidShells.Fill(solidShell);
        // A full turn closes each profile hole into an enclosed toroidal void.
        // Its boundary is a distinct shell on each side and a distinct void
        // region; a partial turn remains connected to the exterior through caps.
        if(full)
            for(var i=1;i<inputFace.LoopCount;i++)
            {
                var cavity=AddProfileVoidRegion(output);
                if(cavity<0)return ParasolidConstants.PK_ERROR_memory_full;
                voidShells[i]=AddProfileShell(output,cavity);
                solidShells[i]=AddProfileShell(output,Shells[solidShell].Region);
                if(voidShells[i]<0||solidShells[i]<0)return ParasolidConstants.PK_ERROR_memory_full;
            }
        var startTransform=spin?KernelAffineTransform.Identity:KernelAffineTransform.Translation(in origin);
        var topTranslation=ProfileAdd(origin,ProfileScale(direction,extent));
        var endTransform=spin?KernelAffineTransform.Rotation(in origin,in direction,extent):KernelAffineTransform.Translation(in topTranslation);
        for(var i=0;i<inputVertices.Length;i++)
        {
            var p=Points[GetPointSlotByTag(Vertices[inputVertices[i]].PointTag)].Position;
            var bottom=startTransform.TransformPoint(in p);
            var top=endTransform.TransformPoint(in p);
            if(full)
            {
                if(!AddVertex(output,p.X,p.Y,p.Z,out bottomVertices[i]))return ParasolidConstants.PK_ERROR_memory_full;
                topVertices[i]=bottomVertices[i];
                var axisPoint=ProfileAdd(origin,ProfileScale(direction,ProfileDot(ProfileSubtract(p,origin),direction)));
                var radial=ProfileSubtract(p,axisPoint);var radius=Math.Sqrt(ProfileDot(radial,radial));
                radial=ProfileScale(radial,1/radius);
                var circleAxis=extent>0?direction:ProfileScale(direction,-1);
                var circle=CreateCircleCurveTag(axisPoint.X,axisPoint.Y,axisPoint.Z,circleAxis.X,circleAxis.Y,circleAxis.Z,radial.X,radial.Y,radial.Z,radius);
                if(circle<=0)return ParasolidConstants.PK_ERROR_memory_full;
                vertical[i]=AddProfileEdge(output,circle,bottomVertices[i],bottomVertices[i],0,Math.Tau);
            }
            else
            {
                if(!AddVertex(output,bottom.X,bottom.Y,bottom.Z,out bottomVertices[i])
                    ||!AddVertex(output,top.X,top.Y,top.Z,out topVertices[i]))return ParasolidConstants.PK_ERROR_memory_full;
                if(spin)
                {
                    var axisPoint=ProfileAdd(origin,ProfileScale(direction,ProfileDot(ProfileSubtract(p,origin),direction)));
                    var radial=ProfileSubtract(p,axisPoint);var radius=Math.Sqrt(ProfileDot(radial,radial));
                    radial=ProfileScale(radial,1/radius);
                    var axisDirection=extent>0?direction:ProfileScale(direction,-1);
                    var circle=CreateCircleCurveTag(axisPoint.X,axisPoint.Y,axisPoint.Z,axisDirection.X,axisDirection.Y,axisDirection.Z,radial.X,radial.Y,radial.Z,radius);
                    if(circle<=0)return ParasolidConstants.PK_ERROR_memory_full;
                    vertical[i]=AddProfileEdge(output,circle,bottomVertices[i],topVertices[i],0,Math.Abs(extent));
                }
                else
                {
                    if(!AddLineEdge(output,bottomVertices[i],topVertices[i],bottom.X,bottom.Y,bottom.Z,top.X,top.Y,top.Z,out vertical[i]))
                        return ParasolidConstants.PK_ERROR_memory_full;
                    Edges[vertical[i]].ParameterStart=0;Edges[vertical[i]].ParameterEnd=extent;Edges[vertical[i]].HasParameterRange=1;
                }
            }
            if(vertical[i]<0)return ParasolidConstants.PK_ERROR_memory_full;
        }
        for(var i=0;i<inputEdges.Length;i++)
        {
            ref readonly var sourceEdge=ref Edges[inputEdges[i]];
            if(!TryProfileEdgeRange(in sourceEdge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
            var startIndex=inputVertices.IndexOf(sourceEdge.StartVertex);var endIndex=inputVertices.IndexOf(sourceEdge.EndVertex);
            int error;CurveTag lowCurve;
            if(full)
            {
                error=TryCloneEntityCore(sourceEdge.CurveTag,out lowCurve);
                if(error!=0)return error;
                bottomEdges[i]=AddProfileEdge(output,lowCurve,startIndex<0?-1:bottomVertices[startIndex],endIndex<0?-1:bottomVertices[endIndex],a,b);
                if(bottomEdges[i]<0)return ParasolidConstants.PK_ERROR_memory_full;
                topEdges[i]=bottomEdges[i];
            }
            else
            {
                error=TryCloneEntityCore(sourceEdge.CurveTag,out lowCurve);
                if(error==0)error=TryTransformEntityCore(lowCurve,in startTransform);
                if(error!=0)return error;
                error=TryCloneEntityCore(sourceEdge.CurveTag,out var highCurve);
                if(error==0)error=TryTransformEntityCore(highCurve,in endTransform);
                if(error!=0)return error;
                bottomEdges[i]=AddProfileEdge(output,lowCurve,startIndex<0?-1:bottomVertices[startIndex],endIndex<0?-1:bottomVertices[endIndex],a,b);
                topEdges[i]=AddProfileEdge(output,highCurve,startIndex<0?-1:topVertices[startIndex],endIndex<0?-1:topVertices[endIndex],a,b);
                if(bottomEdges[i]<0||topEdges[i]<0)return ParasolidConstants.PK_ERROR_memory_full;
            }
            if(error!=0)return error;
            var sweepSense=orientation*sourceDirections[i];
            error=CreateProfileLateral(lowCurve,a,b,spin,origin,direction,extent,sweepSense,out var surface,out var faceSense);
            if(error!=0)return error;
            OrientProfileLateral(surface,faceSense,-sweepSense,out faceSense);
            var lateral=AddFace(output,surface);
            if(lateral<0)return ParasolidConstants.PK_ERROR_memory_full;
            Faces[lateral].Orientation=faceSense;
            if(!AttachProfileSolidFace(lateral,voidShells[sourceLoops[i]],solidShells[sourceLoops[i]]))return ParasolidConstants.PK_ERROR_memory_full;
            if(startIndex<0&&endIndex<0)
            {
                var firstLoop=AddLoop(lateral);var secondLoop=AddLoop(lateral);
                if(firstLoop<0||secondLoop<0||AddProfileFin(firstLoop,bottomEdges[i],sweepSense>0)<0
                    ||AddProfileFin(secondLoop,topEdges[i],sweepSense<0)<0)return ParasolidConstants.PK_ERROR_memory_full;
            }
            else
            {
                var sideLoop=AddLoop(lateral);
                if(sideLoop<0)return ParasolidConstants.PK_ERROR_memory_full;
                var first=sweepSense>0?startIndex:endIndex;var last=sweepSense>0?endIndex:startIndex;
                if(AddProfileFin(sideLoop,bottomEdges[i],sweepSense>0)<0||AddProfileFin(sideLoop,vertical[last],true)<0
                    ||AddProfileFin(sideLoop,topEdges[i],sweepSense<0)<0||AddProfileFin(sideLoop,vertical[first],false)<0)
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        if(!full)
        {
            var error=AddProfileCap(input.FirstFaceBody,inputEdges,bottomEdges,output,voidShell,solidShell,startTransform,-orientation);
            if(error==0)error=AddProfileCap(input.FirstFaceBody,inputEdges,topEdges,output,voidShell,solidShell,endTransform,orientation);
            if(error!=0)return error;
        }
        if(!CheckProfileSolidTopology(output))return ParasolidConstants.PK_ERROR_bad_loop;
        EntityTag completed=0;
        var finish=FinishCreatedBody(output,&completed);
        if(finish==0)
        {
            if(!baseProducts.IsEmpty)bottomEdges.CopyTo(baseProducts);
            if(!basePoints.IsEmpty)bottomVertices.CopyTo(basePoints);
        }
        result=completed;
        return finish;
    }

    private static KernelVector3 GetProfileRepresentative(EdgeSlot edge)
    {
        TryProfileEdgeRange(in Edges[edge],out var a,out var b);
        Span<KernelVector3> value=stackalloc KernelVector3[2];
        EvaluateCurveCore(in Curves[GetCurveSlotByTag(Edges[edge].CurveTag)],a+(b-a)*0.5,0,false,value,out _);
        return value[0];
    }

    private static void OrientProfileLateral(SurfTag surface,KernelSense currentFaceSense,int targetFaceSign,
        out KernelSense faceSense)
    {
        var currentSign=currentFaceSense==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1;
        var flip=currentSign*targetFaceSign;
        ref var record=ref Surfaces[GetSurfaceSlotByTag(surface)];
        // PK's sweep construction separates the direction of the generated
        // surface from the profile traversal. Preserve the outward normal
        // while using that same face/surface orientation convention.
        if(flip<0)
        {
            if(record.Class==SurfaceClass.Plane)
            {
                ref var plane=ref PlaneDataPool[record.DataIndex];
                plane.NormalX=-plane.NormalX;plane.NormalY=-plane.NormalY;plane.NormalZ=-plane.NormalZ;
            }
            else
                record.Sense=record.Sense==ParasolidConstants.PK_TOPOL_sense_negative_c
                    ?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        }
        faceSense=targetFaceSign>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
    }

    private static bool AttachProfileSolidFace(FaceSlot face,ShellSlot voidShell,ShellSlot solidShell)
        =>AddFaceUse(solidShell,face,ParasolidConstants.PK_TOPOL_sense_negative_c)>=0
            &&AddFaceUse(voidShell,face,ParasolidConstants.PK_TOPOL_sense_positive_c)>=0;

    private static int AddProfileCap(FaceSlot source,ReadOnlySpan<int> inputEdges,ReadOnlySpan<int> capEdges,
        BodySlot body,ShellSlot voidShell,ShellSlot solidShell,KernelAffineTransform transform,int orientation)
    {
        var error=TryCloneEntityCore(Faces[source].SurfTag,out var surface);
        if(error==0)error=TryTransformEntityCore(surface,in transform);
        if(error!=0)return error;
        var face=AddFace(body,surface);
        if(face<0)return ParasolidConstants.PK_ERROR_memory_full;
        var originalSense=Faces[source].Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1;
        Faces[face].Orientation=orientation*originalSense>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        if(!AttachProfileSolidFace(face,voidShell,solidShell))return ParasolidConstants.PK_ERROR_memory_full;
        var loop=Faces[source].FirstLoop;
        for(var i=0;i<Faces[source].LoopCount;i++,loop=Loops[loop].NextInFace)
        {
            var capLoop=AddLoop(face);
            if(capLoop<0)return ParasolidConstants.PK_ERROR_memory_full;
            var fin=orientation>0?Loops[loop].FirstFin:Loops[loop].LastFin;
            for(var j=0;j<Loops[loop].FinCount;j++,fin=orientation>0?Fins[fin].NextInLoop:Fins[fin].PrevInLoop)
            {
                var index=inputEdges.IndexOf(Fins[fin].Edge);
                if(index<0||AddProfileFin(capLoop,capEdges[index],(Fins[fin].Sense=='+')==(orientation>0))<0)
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        return 0;
    }

    private static int CreateProfileLateral(CurveTag curve,double a,double b,bool spin,KernelVector3 origin,
        KernelVector3 direction,double extent,int traversal,out SurfTag surface,out KernelSense sense)
    {
        surface=0;sense=ParasolidConstants.PK_TOPOL_sense_positive_c;SurfTag generated=0;
        ref readonly var record=ref Curves[GetCurveSlotByTag(curve)];
        if(record.Class==CurveClass.BCurve)
        {
            // PK retains the exact tensor-product spline for a B-curve sweep.
            // The u-path / v-profile ordering supplies its canonical normal.
            sense=traversal>0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
            return CreateProfileSplineLateral(in BCurveDataStore[record.DataIndex],spin,in origin,in direction,extent,out surface);
        }
        if(spin&&record.Class==CurveClass.Line)
        {
            ref readonly var line=ref LineDataPool[record.DataIndex];
            var tangent=Vector(line.AxisX,line.AxisY,line.AxisZ);
            var p=Vector(line.LocationX+a*line.AxisX,line.LocationY+a*line.AxisY,line.LocationZ+a*line.AxisZ);
            var center=ProfileAdd(origin,ProfileScale(direction,ProfileDot(ProfileSubtract(p,origin),direction)));
            var radial=ProfileSubtract(p,center);var radius=Math.Sqrt(ProfileDot(radial,radial));
            if(!(radius>1e-8))return ParasolidConstants.PK_ERROR_bad_parameter;
            radial=ProfileScale(radial,1/radius);
            var axial=ProfileDot(tangent,direction);var dr=ProfileDot(tangent,radial);
            var desired=ProfileScale(ProfileCross(tangent,ProfileCross(direction,radial)),traversal*Math.Sign(extent));
            if(Math.Abs(axial)<1e-10)
            {
                var normal=direction;
                if(ProfileDot(normal,desired)<0)normal=ProfileScale(normal,-1);
                surface=CreatePlaneSurfaceTag(p.X,p.Y,p.Z,normal.X,normal.Y,normal.Z,radial.X,radial.Y,radial.Z);
                return surface>0?0:ParasolidConstants.PK_ERROR_memory_full;
            }
            var basis=new PK_AXIS2_sf_s();
            basis.location.coord[0]=center.X;basis.location.coord[1]=center.Y;basis.location.coord[2]=center.Z;
            basis.ref_direction.coord[0]=radial.X;basis.ref_direction.coord[1]=radial.Y;basis.ref_direction.coord[2]=radial.Z;
            if(Math.Abs(dr)<1e-10)
            {
                basis.axis.coord[0]=direction.X;basis.axis.coord[1]=direction.Y;basis.axis.coord[2]=direction.Z;
                var definition=new PK_CYL_sf_s{basis_set=basis,radius=radius};
                var error=CylCreate(&definition,&generated);surface=generated;
                sense=ProfileDot(radial,desired)>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
                return error;
            }
            var coneAxis=ProfileScale(direction,dr/axial>0?1:-1);
            basis.axis.coord[0]=coneAxis.X;basis.axis.coord[1]=coneAxis.Y;basis.axis.coord[2]=coneAxis.Z;
            var angle=Math.Atan(Math.Abs(dr/axial));
            var cone=new PK_CONE_sf_s{basis_set=basis,radius=radius,semi_angle=angle};
            var coneError=ConeCreate(&cone,&generated);surface=generated;
            var coneNormal=ProfileSubtract(ProfileScale(radial,Math.Cos(angle)),ProfileScale(coneAxis,Math.Sin(angle)));
            sense=ProfileDot(coneNormal,desired)>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
            return coneError;
        }
        if(spin&&record.Class==CurveClass.Circle)
        {
            ref readonly var circle=ref CircleDataPool[record.DataIndex];
            var center=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);
            var basePoint=ProfileAdd(origin,ProfileScale(direction,ProfileDot(ProfileSubtract(center,origin),direction)));
            var radial=ProfileSubtract(center,basePoint);var major=Math.Sqrt(ProfileDot(radial,radial));
            if(major>circle.Radius+1e-8)
            {
                radial=ProfileScale(radial,1/major);
                var definition=new PK_TORUS_sf_s{major_radius=major,minor_radius=circle.Radius};
                definition.basis_set.location.coord[0]=basePoint.X;definition.basis_set.location.coord[1]=basePoint.Y;definition.basis_set.location.coord[2]=basePoint.Z;
                definition.basis_set.axis.coord[0]=direction.X;definition.basis_set.axis.coord[1]=direction.Y;definition.basis_set.axis.coord[2]=direction.Z;
                definition.basis_set.ref_direction.coord[0]=radial.X;definition.basis_set.ref_direction.coord[1]=radial.Y;definition.basis_set.ref_direction.coord[2]=radial.Z;
                Span<KernelVector3> jet=stackalloc KernelVector3[2];
                var status=EvaluateCurveCore(in record,a+(b-a)*0.37,1,false,jet,out _);
                if(status!=Computation.AlgorithmStatus.Success)return ParasolidConstants.PK_ERROR_eval_failure;
                var motion=ProfileCross(direction,ProfileSubtract(jet[0],origin));
                var desired=ProfileScale(ProfileCross(jet[1],motion),traversal*Math.Sign(extent));
                var torusNormal=ProfileSubtract(jet[0],center);
                sense=ProfileDot(torusNormal,desired)>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
                var torusError=TorusCreate(&definition,&generated);surface=generated;return torusError;
            }
        }
        if(!spin&&record.Class==CurveClass.Line)
        {
            ref readonly var line=ref LineDataPool[record.DataIndex];
            var tangent=Vector(line.AxisX,line.AxisY,line.AxisZ);
            var normal=ProfileScale(ProfileCross(tangent,direction),traversal);
            if(!ProfileNormalize(ref normal))return ParasolidConstants.PK_ERROR_bad_parameter;
            surface=CreatePlaneSurfaceTag(line.LocationX,line.LocationY,line.LocationZ,normal.X,normal.Y,normal.Z,
                tangent.X,tangent.Y,tangent.Z);
            return surface>0?0:ParasolidConstants.PK_ERROR_memory_full;
        }
        if(!spin&&record.Class==CurveClass.Circle)
        {
            ref readonly var circle=ref CircleDataPool[record.DataIndex];
            var axis=Vector(circle.AxisX,circle.AxisY,circle.AxisZ);
            var dot=ProfileDot(axis,direction);
            if(Math.Abs(dot)>1-1e-10)
            {
                var definition=new PK_CYL_sf_s();definition.radius=circle.Radius;
                definition.basis_set.location.coord[0]=circle.CenterX;definition.basis_set.location.coord[1]=circle.CenterY;definition.basis_set.location.coord[2]=circle.CenterZ;
                definition.basis_set.axis.coord[0]=direction.X;definition.basis_set.axis.coord[1]=direction.Y;definition.basis_set.axis.coord[2]=direction.Z;
                definition.basis_set.ref_direction.coord[0]=circle.RefDirX;definition.basis_set.ref_direction.coord[1]=circle.RefDirY;definition.basis_set.ref_direction.coord[2]=circle.RefDirZ;
                var error=CylCreate(&definition,&generated);surface=generated;
                sense=dot*traversal>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
                return error;
            }
        }
        if(spin)
        {
            var definition=new PK_SPUN_sf_s{curve=curve};
            definition.axis.location.coord[0]=origin.X;definition.axis.location.coord[1]=origin.Y;definition.axis.location.coord[2]=origin.Z;
            definition.axis.axis.coord[0]=direction.X;definition.axis.axis.coord[1]=direction.Y;definition.axis.axis.coord[2]=direction.Z;
            var error=SpunCreate(&definition,&generated);surface=generated;
            sense=traversal*extent>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
            return error;
        }
        else
        {
            var definition=new PK_SWEPT_sf_s{curve=curve};
            definition.direction.coord[0]=direction.X;definition.direction.coord[1]=direction.Y;definition.direction.coord[2]=direction.Z;
            var error=SweptCreate(&definition,&generated);surface=generated;
            sense=traversal>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
            return error;
        }
    }

    private static bool CheckProfileSolidTopology(BodySlot body)
    {
        var edge=Bodies[body].FirstEdgeBody;
        for(var i=0;i<Bodies[body].EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            if(Edges[edge].FinCount!=2)return false;
            var first=Edges[edge].FirstFinEdge;var last=Edges[edge].LastFinEdge;
            if(Fins[first].Sense==Fins[last].Sense||Fins[first].Other!=last||Fins[last].Other!=first)return false;
        }
        var face=Bodies[body].FirstFaceBody;
        for(var i=0;i<Bodies[body].FaceCountBody;i++,face=Faces[face].NextInBody)
        {
            var loop=Faces[face].FirstLoop;
            for(var j=0;j<Faces[face].LoopCount;j++,loop=Loops[loop].NextInFace)
            {
                var fin=Loops[loop].FirstFin;
                for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
                {
                    var next=Fins[fin].NextInLoop;var nextEdge=Edges[Fins[next].Edge];
                    var nextStart=Fins[next].Sense=='+'?nextEdge.StartVertex:nextEdge.EndVertex;
                    if(Fins[fin].Vertex!=nextStart)return false;
                }
            }
        }
        return true;
    }

    private static int ReplaceProfileBody(BodySlot original,BodySlot replacement)
    {
        if(!SnapshotProfileTopology(original)||!SnapshotProfileTopology(replacement)
            ||!SnapshotConstructionDeletion(PoolKind.Body,original)||!SnapshotConstructionDeletion(PoolKind.Body,replacement))
            return ParasolidConstants.PK_ERROR_memory_full;
        var a=Bodies[original];var b=Bodies[replacement];
        Bodies[original]=b;Bodies[original].Header=a.Header;
        Bodies[original].PrevInPartition=a.PrevInPartition;Bodies[original].NextInPartition=a.NextInPartition;
        Bodies[replacement]=a;Bodies[replacement].Header=b.Header;
        Bodies[replacement].PrevInPartition=b.PrevInPartition;Bodies[replacement].NextInPartition=b.NextInPartition;
        RebindProfileBodyChildren(original);RebindProfileBodyChildren(replacement);
        var oldBody=Bodies[replacement].Header.Tag;
        var error=EntityDelete(1,&oldBody);
        if(error==0)SessionMemoryOwner.DropBodyXt(original);
        return error;
    }

    private static void RebindProfileBodyChildren(BodySlot body)
    {
        var edge=Bodies[body].FirstEdgeBody;
        for(var i=0;i<Bodies[body].EdgeCountBody;i++,edge=Edges[edge].NextInBody)Edges[edge].Body=body;
        var vertex=Bodies[body].FirstVertexBody;
        for(var i=0;i<Bodies[body].VertexCountBody;i++,vertex=Vertices[vertex].NextInBody)Vertices[vertex].Body=body;
        var shell=Bodies[body].FirstShell;
        for(var i=0;i<Bodies[body].ShellCount;i++,shell=Shells[shell].NextInBody)Shells[shell].Body=body;
        var region=Bodies[body].FirstRegion;
        for(var i=0;i<Bodies[body].RegionCount;i++,region=Regions[region].NextInBody)Regions[region].Body=body;
        var surface=Bodies[body].FirstConstructionSurface;
        for(var i=0;i<Bodies[body].ConstructionSurfaceCount;i++)
        {
            ref var geometry=ref Surfaces[surface];geometry.ConstructionBodyTag=Bodies[body].Header.Tag;
            surface=geometry.NextConstruction==0?-1:TagRec(geometry.NextConstruction).Slot;
        }
        var curve=Bodies[body].FirstConstructionCurve;
        for(var i=0;i<Bodies[body].ConstructionCurveCount;i++)
        {
            ref var geometry=ref Curves[curve];geometry.ConstructionBodyTag=Bodies[body].Header.Tag;
            curve=geometry.NextConstruction==0?-1:TagRec(geometry.NextConstruction).Slot;
        }
    }
}
