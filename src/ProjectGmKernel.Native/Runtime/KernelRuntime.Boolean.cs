using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyBoolean(EntityTag target, int toolCount, EntityTag* tools, PK_BODY_boolean_o_s* options,
        int* count, EntityTag** bodies)
    {
        if (Dispatcher.IsExecuting) return BodyBooleanLegacyCore(target, toolCount, tools, options, count, bodies);
        var command = new BooleanCommand { Target = target, ToolCount = toolCount, Tools = tools, Options = options, Count = count, Bodies = bodies };
        var error = Dispatch(ApiId.BodyBoolean, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, target);
        if (error != 0)
        {
            if (count != null) *count = 0;
            if (bodies != null) *bodies = null;
        }
        return error;
    }

    public static int BodyBoolean2(EntityTag target, int toolCount, EntityTag* tools, PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking, PK_boolean_r_s* result)
    {
        if (Dispatcher.IsExecuting) return BodyBooleanCore(target, toolCount, tools, options, tracking, result);
        var command = new BooleanCommand { Target = target, ToolCount = toolCount, Tools = tools, Options = options, Tracking = tracking, Result = result, Modern = 1 };
        var error = Dispatch(ApiId.BodyBoolean2, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, target);
        if (error != 0)
        {
            if (tracking != null) *tracking = default;
            if (result != null) *result = new PK_boolean_r_s { result = ParasolidConstants.PK_boolean_result_failed_c };
        }
        return error;
    }

    private struct BooleanCommand : IKernelCommand
    {
        internal EntityTag Target;
        internal BufferCount ToolCount;
        internal EntityTag* Tools;
        internal PK_BODY_boolean_o_s* Options;
        internal PK_TOPOL_track_r_s* Tracking;
        internal PK_boolean_r_s* Result;
        internal BufferCount* Count;
        internal EntityTag** Bodies;
        internal byte Modern;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return Modern != 0 ? BodyBooleanCore(Target, ToolCount, Tools, Options, Tracking, Result)
                : BodyBooleanLegacyCore(Target, ToolCount, Tools, Options, Count, Bodies); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BodyBooleanLegacyCore(EntityTag target, int toolCount, EntityTag* tools, PK_BODY_boolean_o_s* options,
        int* count, EntityTag** bodies)
    {
        if (count != null) *count = 0; if (bodies != null) *bodies = null;
        if (count == null || bodies == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s result = default;
        var error = BodyBooleanCore(target, toolCount, tools, options, &tracking, &result);
        if (error != 0) return error;
        error = TopolTrackResultFree(&tracking);
        if (error != 0) return error;
        *count = result.n_bodies; *bodies = result.bodies;
        return 0;
    }

    private static int ValidateBooleanOptions(PK_BODY_boolean_o_s* options)
    {
        if (options == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 19) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->function != ParasolidConstants.PK_boolean_unite_c && options->function != ParasolidConstants.PK_boolean_subtract_c
            && options->function != ParasolidConstants.PK_boolean_intersect_c) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (!double.IsFinite(options->default_tol) || options->default_tol <= 0 || !double.IsFinite(options->max_tol)
            || options->max_tol < 0 || options->merge_imprinted > 1 || options->tracking > 1 || options->allow_disjoint > 1)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->configuration != null || options->selected_topolset != null || options->matched_region != null
            || options->prune_in_solid != 0 || options->prune_in_void != 0 || options->fence != ParasolidConstants.PK_boolean_fence_none_c
            || options->allow_disjoint != 0 || options->selective_merge != 0 || options->merge_attributes != 0 || options->keep_target_edges != 0
            || options->imprint_complete_targ != ParasolidConstants.PK_imprint_complete_no_c || options->imprint_complete_tool != ParasolidConstants.PK_imprint_complete_no_c
            || options->target_material_side != ParasolidConstants.PK_boolean_material_default_c || options->tool_material_side != ParasolidConstants.PK_boolean_material_default_c
            || options->resulting_body_type != ParasolidConstants.PK_boolean_prefer_original_c && options->resulting_body_type != ParasolidConstants.PK_boolean_prefer_solid_c
            || options->flag_no_effect != ParasolidConstants.PK_boolean_no_effect_basic_c || options->nm_edge_repair != ParasolidConstants.PK_nm_edge_repair_no_c
            || options->track_regions != ParasolidConstants.PK_track_regions_no_c || options->target_face_overflow != null || options->tool_face_overflow != null
            || options->imprint_overlapping != 0 || options->update != ParasolidConstants.PK_boolean_update_default_c || options->merge_in_solid != 0
            || options->merge_in_face != 0 || options->merge_in_edge != 0 || options->impose_bodies != ParasolidConstants.PK_boolean_impose_no_c
            || options->merge_tools != ParasolidConstants.PK_merge_tools_no_c
            || options->tracking!=0&&options->tracking_type!=ParasolidConstants.PK_boolean_track_type_basic_c
                &&options->tracking_type!=ParasolidConstants.PK_boolean_track_type_comp_c)
            return ParasolidConstants.PK_ERROR_not_implemented;
        return 0;
    }

    private static int BodyBooleanCore(EntityTag target, int toolCount, EntityTag* tools, PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking, PK_boolean_r_s* result)
        =>BodyBooleanCore(target,toolCount,tools,options,tracking,result,default,out _);

    private static int BodyBooleanCore(EntityTag target,int toolCount,EntityTag* tools,PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_boolean_r_s* result,scoped Span<BooleanTopologyOrigin> lineage,out BufferCount lineageCount)
    {
        lineageCount=0;
        if (tracking != null) *tracking = default;
        if (result != null) *result = new PK_boolean_r_s { result = ParasolidConstants.PK_boolean_result_failed_c };
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (tracking == null || result == null || tools == null || toolCount <= 0) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (toolCount > 15) return ParasolidConstants.PK_ERROR_not_implemented;
        var optionError = ValidateBooleanOptions(options);
        if (optionError != 0) return optionError;
        Span<EntityTag> inputs = stackalloc EntityTag[toolCount + 1]; inputs[0] = target;
        for (var i = 0; i < toolCount; i++) inputs[i + 1] = tools[i];
        for (var i = 0; i < inputs.Length; i++)
        {
            if (!TryResolveBodySlot(inputs[i], out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
            if (Bodies[slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            if (Bodies[slot].BodyType != ParasolidConstants.PK_BODY_type_solid_c) return ParasolidConstants.PK_ERROR_not_implemented;
            for (var j = 0; j < i; j++) if (inputs[j] == inputs[i]) return ParasolidConstants.PK_ERROR_bad_parameter;
        }
        var analyticError = TryAnalyticBoolean(inputs, options, tracking, result, out var analyticHandled);
        if (analyticHandled) return !lineage.IsEmpty?ParasolidConstants.PK_ERROR_not_implemented:analyticError;
        var rotationalError = TryRotationalBoolean(inputs, options, tracking, result, out var rotationalHandled);
        if (rotationalHandled) return !lineage.IsEmpty?ParasolidConstants.PK_ERROR_not_implemented:rotationalError;
        // Native solids have finite material and an infinite void exterior.
        // A complete face enclosure proves no clash without reconstructing
        // unchanged topology, including closed faces with no edge loops.
        if(inputs.Length==2&&TryNativeSolidExtent(inputs[0],options->default_tol,out var firstExtent)
            &&TryNativeSolidExtent(inputs[1],options->default_tol,out var secondExtent)
            &&ThirdFaceBoxesDisjoint(firstExtent,secondExtent,0))
        {
            if(!lineage.IsEmpty)
            {
                var originError=ReadIdentityBooleanOrigins(inputs,lineage,out lineageCount);if(originError!=0)return originError;
            }
            return CompleteSeparatedBoolean(inputs,options->function,result);
        }
        var scratch = CommandScratch.Current;
        var memory = scratch.Take(120000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var faces = ModelSpan<PlanarModelFace>(bytes, ref cursor, PlanarFaceCapacity);
            var loops = ModelSpan<PlanarModelLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var points = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var cells = ModelSpan<PlanarModelPolygon>(bytes, ref cursor, PlanarPolygonCapacity);
            var cellPoints = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var cellWork = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var output = ModelSpan<PlanarModelPolygon>(bytes, ref cursor, PlanarPolygonCapacity);
            var outputPoints = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            var error = ReadPlanarModel(inputs, faces, loops, points, out var nf, out var nl, out var np, out var tolerance);
            if (error != 0)
            {
                if(error!=ParasolidConstants.PK_ERROR_not_implemented)return error;
                scratch.Return(memory);memory=default;
                if(options->function==ParasolidConstants.PK_boolean_subtract_c&&toolCount>1
                    &&options->merge_imprinted==0&&HaveRollingBooleanBoundary(inputs[0])&&HavePlanarBooleanTools(inputs[1..]))
                    return RegularCurvedSubtractPlanarTools(inputs,options,tracking,result);
                return RegularCurvedBoolean(inputs,options,tracking,result);
            }
            faces = faces[..nf]; loops = loops[..nl]; points = points[..np];
            var outputCount = 0; var outputPointCount = 0; var hasClash = false;
            Span<bool> minus = stackalloc bool[inputs.Length]; Span<bool> plus = stackalloc bool[inputs.Length];
            for (var f = 0; f < nf; f++)
            {
                ref readonly var face = ref faces[f];
                error = ArrangePlanarFace(in face, faces, loops, points, cells, cellPoints, cellWork, tolerance, out var nc, out _);
                if (error != 0) return error;
                for (var c = 0; c < nc; c++)
                {
                    var cell = cells[c]; var center = PlanarPolygonCenter(cellPoints.Slice(cell.First, cell.Count));
                    var duplicate = false; uint classification = 0;
                    for (var b = 0; b < inputs.Length; b++)
                    {
                        if (b == face.Body) { minus[b] = true; plus[b] = false; }
                        else
                        {
                            var position = PlanarClassifyPoint(faces, loops, points, b, center, tolerance, out _);
                            if (position == 2) return ParasolidConstants.PK_ERROR_eval_failure;
                            if (position == 0)
                            {
                                if (b < face.Body) duplicate = true;
                                var delta = ProfileScale(face.Normal, tolerance * 32);
                                var below = ProfileSubtract(center, delta); var above = ProfileAdd(center, delta);
                                var cm = PlanarClassifyPoint(faces, loops, points, b, below, tolerance, out _);
                                var cp = PlanarClassifyPoint(faces, loops, points, b, above, tolerance, out _);
                                if (cm is 0 or 2 || cp is 0 or 2) return ParasolidConstants.PK_ERROR_eval_failure;
                                minus[b] = cm > 0; plus[b] = cp > 0;
                            }
                            else minus[b] = plus[b] = position > 0;
                            if ((b == 0 || face.Body == 0) && position >= 0) hasClash = true;
                        }
                        if (minus[b]) classification |= 1u << (2 * b);
                        if (plus[b]) classification |= 2u << (2 * b);
                    }
                    var inside = BooleanMaterial(minus, options->function); var outside = BooleanMaterial(plus, options->function);
                    if (inside == outside || duplicate) continue;
                    if (outputCount == output.Length || outputPointCount > outputPoints.Length - cell.Count)
                        return ParasolidConstants.PK_ERROR_not_implemented;
                    cellPoints.Slice(cell.First, cell.Count).CopyTo(outputPoints[outputPointCount..]);
                    output[outputCount++] = new PlanarModelPolygon { First = outputPointCount, Count = cell.Count,
                        Face = f, Classification = classification, Reversed = inside ? (byte)0 : (byte)1 };
                    outputPointCount += cell.Count;
                }
            }
            // Exactly disjoint target/tool pairs preserve the original bodies
            // on union and the target on subtraction, as the SDK does.
            if (!hasClash && toolCount == 1)
            {
                if(!lineage.IsEmpty)
                {
                    var originError=ReadIdentityBooleanOrigins(inputs,lineage,out lineageCount);if(originError!=0)return originError;
                }
                return CompleteSeparatedBoolean(inputs,options->function,result);
            }
            Span<EntityTag> products = stackalloc EntityTag[PlanarFaceCapacity];
            Span<FaceTag> faceProducts = stackalloc FaceTag[nf]; faceProducts.Clear();
            error = ReconstructPlanarBodies(faces, output[..outputCount], outputPoints[..outputPointCount], tolerance,
                true, options->merge_imprinted != 0, products, out var productCount, faceProducts,lineage);
            if (error != 0) return error;
            if(!lineage.IsEmpty)
            {
                while(lineageCount<lineage.Length&&lineage[lineageCount].Original!=0)lineageCount++;
                error=ReadPlanarEdgeOrigins(products[..productCount],lineage,ref lineageCount);if(error!=0)return error;
            }
            var returnedBodies = productCount == 0 ? null : AllocateReturnSlice(productCount);
            if (productCount > 0 && returnedBodies == null) return ParasolidConstants.PK_ERROR_memory_full;
            if (options->tracking != 0)
            {
                error = CreatePlanarBooleanTracking(faces, loops, points, inputs.Length, products[..productCount], tolerance, tracking);
                if (error != 0) return error;
            }
            error = PreservePlanarFaceHandles(faces, faceProducts, inputs, products[..productCount],lineage[..lineageCount]);
            if (error != 0) return error;
            if (productCount > 0)
            {
                error = ReplaceProfileBody(TagRec(target).Slot, TagRec(products[0]).Slot);
                if (error != 0) return error;
                products[0] = target;
            }
            else
            {
                error = EntityDelete(1, &target);
                if (error != 0) return error;
            }
            error = EntityDelete(toolCount, tools);
            if (error != 0) return error;
            if (productCount > 0) products[..productCount].CopyTo(new Span<EntityTag>(returnedBodies, productCount));
            *result = new PK_boolean_r_s { result = hasClash ? ParasolidConstants.PK_boolean_result_success_c
                : ParasolidConstants.PK_boolean_result_no_clash_c, n_bodies = productCount, bodies = returnedBodies };
            return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static int CompleteSeparatedBoolean(ReadOnlySpan<BodyTag> inputs,KernelBooleanFunction function,PK_boolean_r_s* result)
    {
        var count=function==ParasolidConstants.PK_boolean_unite_c?2:function==ParasolidConstants.PK_boolean_subtract_c?1:0;
        var returned=count==0?null:AllocateReturnSlice(count);
        if(count>0&&returned==null)return ParasolidConstants.PK_ERROR_memory_full;
        var success=false;
        try
        {
            if(count>0)returned[0]=inputs[0];if(count>1)returned[1]=inputs[1];
            if(count<2)
            {
                var tool=inputs[1];var error=EntityDelete(1,&tool);
                if(error==0&&count==0){var target=inputs[0];error=EntityDelete(1,&target);}
                if(error!=0)return error;
            }
            *result=new(){result=ParasolidConstants.PK_boolean_result_no_clash_c,n_bodies=count,bodies=returned};success=true;return 0;
        }
        finally{if(!success&&returned!=null)MemoryFree(returned);}
    }

    private static bool HavePlanarBooleanTools(ReadOnlySpan<BodyTag> tools)
    {
        foreach(var tool in tools)
        {
            var body=Bodies[TagRec(tool).Slot];var face=body.FirstFaceBody;
            for(var i=0;i<body.FaceCountBody;i++,face=Faces[face].NextInBody)
            {
                var surface=GetSurfaceSlotByTag(Faces[face].SurfTag);
                if(surface<0||Surfaces[surface].Class!=SurfaceClass.Plane)return false;
            }
        }
        return true;
    }

    private static bool HaveRollingBooleanBoundary(BodyTag target)
    {
        var body=Bodies[TagRec(target).Slot];var face=body.FirstFaceBody;
        for(var i=0;i<body.FaceCountBody;i++,face=Faces[face].NextInBody)
        {
            var surface=GetSurfaceSlotByTag(Faces[face].SurfTag);
            if(surface>=0&&Surfaces[surface].Class==SurfaceClass.BlendSurface)return true;
        }
        return false;
    }

    // The union's finite exterior is the cutting domain. Internal tool
    // faces must not generate target intersections. The nested operation
    // stays inside the outer command journal, including any consumed tags.
    private static int RegularCurvedSubtractPlanarTools(ReadOnlySpan<BodyTag> inputs,PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_boolean_r_s* result)
    {
        var unite=*options;unite.function=ParasolidConstants.PK_boolean_unite_c;
        unite.tracking=0;
        PK_TOPOL_track_r_s temporaryTracking=default;PK_boolean_r_s toolUnion=default;
        var scratch=CommandScratch.Current;Span<double> memory=default;
        try
        {
            Span<BooleanTopologyOrigin> unionOrigins=default,finalOrigins=default;
            if(options->tracking!=0)
            {
                var capacity=PlanarPolygonCapacity+2*PlanarPointCapacity;
                memory=scratch.Take(capacity*sizeof(BooleanTopologyOrigin)/sizeof(double));
                if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
                var rows=MemoryMarshal.Cast<double,BooleanTopologyOrigin>(memory);rows.Clear();
                unionOrigins=rows;
            }
            int error;
            BufferCount originCount;
            fixed(BodyTag* remaining=inputs[2..])
                error=BodyBooleanCore(inputs[1],inputs.Length-2,remaining,&unite,&temporaryTracking,&toolUnion,unionOrigins,out originCount);
            if(error!=0)return error;
            if(toolUnion.n_bodies<=0||toolUnion.n_bodies>=inputs.Length||toolUnion.bodies==null)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(options->tracking!=0)
            {
                // The union no longer needs its construction capacity. Keep
                // only the actual source links while the curved solver runs.
                // Reacquiring this arena prefix preserves those rows in place.
                scratch.Return(memory);memory=scratch.Take((originCount+SheetEdgeLimit)*sizeof(BooleanTopologyOrigin)/sizeof(double));
                if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
                var rows=MemoryMarshal.Cast<double,BooleanTopologyOrigin>(memory);
                unionOrigins=rows[..originCount];finalOrigins=rows[originCount..];finalOrigins.Clear();
            }
            Span<BodyTag> united=stackalloc BodyTag[toolUnion.n_bodies+1];united[0]=inputs[0];
            new ReadOnlySpan<BodyTag>(toolUnion.bodies,toolUnion.n_bodies).CopyTo(united[1..]);
            error=RegularCurvedBoolean(united,options,tracking,result,finalOrigins);if(error!=0)return error;
            return options->tracking==0?0:ComposeBooleanOrigins(inputs[0],unionOrigins[..originCount],finalOrigins,tracking,result);
        }
        finally
        {
            TopolTrackResultFree(&temporaryTracking);BooleanResultFree(&toolUnion);
            scratch.Return(memory);
        }
    }

    private static bool BooleanMaterial(ReadOnlySpan<bool> bodies, int function)
    {
        var tools = false;
        for (var i = 1; i < bodies.Length; i++) tools |= bodies[i];
        return function == ParasolidConstants.PK_boolean_unite_c ? bodies[0] || tools
            : function == ParasolidConstants.PK_boolean_intersect_c ? bodies[0] && tools : bodies[0] && !tools;
    }

    private static int CreatePlanarBooleanTracking(ReadOnlySpan<PlanarModelFace> faces, ReadOnlySpan<PlanarModelLoop> loops,
        ReadOnlySpan<KernelVector3> points, int inputCount, ReadOnlySpan<BodyTag> bodies, double tolerance, PK_TOPOL_track_r_s* result)
    {
        var count = 0;
        PK_TOPOL_track_record_r_s* records = null; EntityTag* originals = null; int* classes = null; EntityTag* products = null;
        Span<EntityTag> original = stackalloc EntityTag[inputCount]; Span<int> originalClass = stackalloc int[inputCount];
        for (var pass = 0; pass < 2; pass++)
        {
            var recordIndex = 0; var originIndex = 0;
            foreach (var body in bodies)
            {
                var slot = TagRec(body).Slot; var edge = Bodies[slot].FirstEdgeBody;
                for (var e = 0; e < Bodies[slot].EdgeCountBody; e++, edge = Edges[edge].NextInBody)
                {
                    ref readonly var record = ref Edges[edge];
                    var a = Points[GetPointSlotByTag(Vertices[record.StartVertex].PointTag)].Position;
                    var b = Points[GetPointSlotByTag(Vertices[record.EndVertex].PointTag)].Position;
                    var point = ProfileAdd(a, ProfileScale(ProfileSubtract(b, a), .5)); var n = 0;
                    for (var input = 0; input < inputCount; input++)
                    {
                        var tag = LocatePlanarBoundary(faces, loops, points, input, point, tolerance, out var cls);
                        if (tag != 0) { original[n] = tag; originalClass[n++] = cls; }
                    }
                    if (n < 2) continue;
                    if (pass != 0)
                    {
                        original[..n].CopyTo(new Span<EntityTag>(originals + originIndex, n));
                        originalClass[..n].CopyTo(new Span<int>(classes + originIndex, n)); products[recordIndex] = record.Header.Tag;
                        records[recordIndex] = new PK_TOPOL_track_record_r_s { track = ParasolidConstants.PK_TOPOL_track_derive_c,
                            n_original_topols = n, original_topols = originals + originIndex, original_classes = classes + originIndex,
                            n_product_topols = 1, product_topols = products + recordIndex };
                        originIndex += n;
                    }
                    recordIndex++;
                }
            }
            if (pass == 0)
            {
                count = recordIndex;
                if (count == 0) return 0;
                var bytes = (nuint)(count * (sizeof(PK_TOPOL_track_record_r_s) + (2 * inputCount + 1) * sizeof(EntityTag)));
                records = (PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);
                if (records == null) return ParasolidConstants.PK_ERROR_memory_full;
                originals = (EntityTag*)(records + count); classes = originals + count * inputCount; products = classes + count * inputCount;
            }
        }
        *result = new PK_TOPOL_track_r_s { n_track_records = count, track_records = records,
            internal_origs = originals, internal_classes = classes, internal_prods = products };
        return 0;
    }

    public static int BooleanResultFree(PK_boolean_r_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var error = result->bodies == null ? 0 : KernelRuntime.MemoryFree(result->bodies);
        if (error == 0 && result->reports != null) error = KernelRuntime.MemoryFree(result->reports);
        if (error == 0) *result = default;
        return error;
    }
}
