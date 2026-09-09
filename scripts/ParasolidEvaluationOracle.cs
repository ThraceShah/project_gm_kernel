#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidMarkOracle=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

static string ScriptPath([CallerFilePath] string path = "") => path;
var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "evaluation-oracle"));
Directory.CreateDirectory(directory);
var roundtripFailures = new List<string>();
var numericalOnly = args.Contains("--numerical-only");
var memoryReview = args.Contains("--memory-review");
if (numericalOnly) Console.WriteLine("Numerical-only run: XT receive/compare is not checked.");

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("evaluation oracle", out var session, out var message,
        configureRollback: memoryReview ? MarkOracleStorage.Register : null))
        throw new InvalidOperationException(message);
    using (session)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "our session");
        try
        {
            if (memoryReview)
            {
                CheckPartitionLockProtocol();
                CheckPartitionLifecycle(directory);
                CheckSharedCurve(directory);
                CheckSharedSurface(directory);
            }
            foreach (var kind in new[] { "block", "cylinder", "cone", "sphere", "torus" })
            foreach (var rotated in new[] { false, true })
            {
                var basis = Frame(2, -3, 7, 1.0 / 3, 2.0 / 3, 2.0 / 3, 0, 1 / Math.Sqrt(2), -1 / Math.Sqrt(2));
                var managedBasis = new M.PK_AXIS2_sf_s();
                for (var i = 0; i < 3; i++)
                {
                    managedBasis.location.coord[i] = basis.location.coord[i];
                    managedBasis.axis.coord[i] = basis.axis.coord[i];
                    managedBasis.ref_direction.coord[i] = basis.ref_direction.coord[i];
                }
                var bp = rotated ? &basis : null;
                var mp = rotated ? &managedBasis : null;
                var label = rotated ? kind + "-rotated" : kind;
                int ours, reference;
                switch (kind)
                {
                    case "block":
                        Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, mp, &ours), kind);
                        Check(PK_BODY_create_solid_block(2, 3, 4, bp, &reference), kind);
                        break;
                    case "cylinder":
                        Check(KernelRuntime.BodyCreateSolidCyl(3, 5, mp, &ours), kind);
                        Check(PK_BODY_create_solid_cyl(3, 5, bp, &reference), kind);
                        break;
                    case "cone":
                        Check(KernelRuntime.BodyCreateSolidCone(2, 5, 0.25, mp, &ours), kind);
                        Check(PK_BODY_create_solid_cone(2, 5, 0.25, bp, &reference), kind);
                        break;
                    case "sphere":
                        Check(KernelRuntime.BodyCreateSolidSphere(3, mp, &ours), kind);
                        Check(PK_BODY_create_solid_sphere(3, bp, &reference), kind);
                        break;
                    default:
                        Check(KernelRuntime.BodyCreateSolidTorus(5, 2, mp, &ours), kind);
                        Check(PK_BODY_create_solid_torus(5, 2, bp, &reference), kind);
                        break;
                }
                if (memoryReview)
                {
                    // Delete/restore a live body, then reuse slots from another
                    // body. Both kernels perform the same lifecycle operations.
                    int ourMark, referenceMark;
                    Check(KernelRuntime.MarkCreate(&ourMark), "our mark");
                    Check(PK_MARK_create(&referenceMark), "reference mark");
                    Check(KernelRuntime.EntityDelete(1, &ours), "our delete under mark");
                    Check(PK_ENTITY_delete(1, &reference), "reference delete under mark");
                    Check(KernelRuntime.MarkGoto(ourMark), "our restore");
                    Check(PK_MARK_goto(referenceMark), "reference restore");
                    int ourTemporary, referenceTemporary;
                    Check(KernelRuntime.BodyCreateSolidSphere(1, null, &ourTemporary), "our temporary body");
                    Check(PK_BODY_create_solid_sphere(1, null, &referenceTemporary), "reference temporary body");
                    Check(KernelRuntime.EntityDelete(1, &ourTemporary), "our temporary delete");
                    Check(PK_ENTITY_delete(1, &referenceTemporary), "reference temporary delete");
                    CheckGeometryAttachments(ours, reference);
                }
                else
                {
                    CheckBodyEvaluations(ours, label);
                    Console.WriteLine(label + ": numerical evaluation passed");
                }
                if (numericalOnly) continue;
                try
                {
                    CheckRoundtrip(ours, reference, Path.Combine(directory, label + ".x_t"));
                    Console.WriteLine(label + ": XT body comparison passed");
                    if (memoryReview)
                    {
                        Check(KernelRuntime.EntityDelete(1, &ours), "our final delete");
                        Check(PK_ENTITY_delete(1, &reference), "reference final delete");
                    }
                }
                catch (InvalidOperationException failure)
                {
                    roundtripFailures.Add(label + ": " + failure.Message);
                    Console.WriteLine(roundtripFailures[^1]);
                }
            }
            CheckDependentGeometryEvaluations();
        }
        finally { Check(KernelRuntime.SessionStop(), "our stop"); }
    }
}

if (roundtripFailures.Count != 0)
    throw new InvalidOperationException(string.Join(Environment.NewLine, roundtripFailures));
foreach (var (kind, stats) in ComparisonStats.ByKind)
{
    var tangent = kind.StartsWith("Curve.", StringComparison.Ordinal) ? $" max_tangent={stats.Tangent:R}" : "";
    Console.WriteLine($"{kind}: grid_samples={stats.GridSamples} scalar_comparisons={stats.Comparisons} max_point={stats.Point:R}{tangent} max_derivative={stats.Derivative:R} (absolute tolerance=1e-13)");
}

static void Check(int error, string operation)
{
    if (error != 0) throw new InvalidOperationException($"{operation}: error={error}");
}

static void Equal(int expected, int actual, string label)
{
    if (expected != actual) throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
}

static unsafe void CheckPartitionLockProtocol()
{
    int ours, reference;
    Check(KernelRuntime.PartitionCreateEmpty(&ours), "our partition");
    Check(PK_PARTITION_create_empty(&reference), "reference partition");
    var ourOptions = new M.PK_THREAD_lock_partitions_o_s
    { o_t_version = 1, want_locked_partitions = 1, want_unavailable_partitions = 1 };
    var options = new PK_THREAD_lock_partitions_o_t
    { want_locked_partitions = 1, want_unavailable_partitions = 1 };
    M.PK_THREAD_lock_partitions_r_s ourResult;
    PK_THREAD_lock_partitions_r_t result;
    var invalidReference = PK_THREAD_lock_partitions(1, &reference, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &options, &result);
    var invalidOurs = KernelRuntime.ThreadLockPartitions(1, &ours, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &ourOptions, &ourResult);
    Equal(PK_ERROR_not_at_pmark, invalidReference, "reference lock before checkpoint");
    Equal(invalidReference, invalidOurs, "lock before checkpoint");
    int ourMark, referenceMark;
    Check(KernelRuntime.MarkCreate(&ourMark), "our partition checkpoint");
    Check(PK_MARK_create(&referenceMark), "reference partition checkpoint");
    var expectedError = PK_THREAD_lock_partitions(1, &reference, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &options, &result);
    var actualError = KernelRuntime.ThreadLockPartitions(1, &ours, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &ourOptions, &ourResult);
    Check(expectedError, "reference partition lock");
    Equal(expectedError, actualError, "partition lock error");
    Equal(result.status, ourResult.status, "partition lock status");
    Equal(result.n_locked_partitions, ourResult.n_locked_partitions, "partition lock count");
    Equal(reference, result.locked_partitions[0], "reference returned partition");
    Equal(ours, ourResult.locked_partitions[0], "our returned partition");
    Check(PK_THREAD_lock_partitions_r_f(&result), "reference lock result free");
    Check(KernelRuntime.ThreadLockPartitionsResultFree(&ourResult), "our lock result free");
    var unlock = new PK_THREAD_unlock_partitions_o_t();
    var ourUnlock = new M.PK_THREAD_unlock_partitions_o_s { o_t_version = 1 };
    int referenceCount, ourCount;
    int* referencePartitions;
    int* ourPartitions;
    Check(PK_THREAD_unlock_partitions(&unlock, &referenceCount, &referencePartitions), "reference unlock");
    Check(KernelRuntime.ThreadUnlockPartitions(&ourUnlock, &ourCount, &ourPartitions), "our unlock");
    Equal(referenceCount, ourCount, "unlock count");
    Check(PK_MEMORY_free(referencePartitions), "reference unlock free");
    Check(KernelRuntime.MemoryFree(ourPartitions), "our unlock free");
    Check(KernelRuntime.MarkGoto(ourMark), "our partition checkpoint restore");
    Check(PK_MARK_goto(referenceMark), "reference partition checkpoint restore");
    Console.WriteLine("PK partition lock/unlock: oracle comparison passed");
}

static unsafe void CheckPartitionLifecycle(string directory)
{
    int ours, reference, ourDefault, referenceDefault, body, referenceBody, mark, referenceMark, extra, referenceExtra;
    Check(KernelRuntime.SessionAskCurrentPartition(&ourDefault), "our default partition");
    Check(PK_SESSION_ask_curr_partition(&referenceDefault), "reference default partition");
    int control, referenceControl;
    Check(KernelRuntime.BodyCreateSolidBlock(3, 4, 5, null, &control), "our surviving control body");
    Check(PK_BODY_create_solid_block(3, 4, 5, null, &referenceControl), "reference surviving control body");
    Check(KernelRuntime.PartitionCreateEmpty(&ours), "our lifecycle partition");
    Check(PK_PARTITION_create_empty(&reference), "reference lifecycle partition");
    Check(KernelRuntime.PartitionSetCurrent(ours), "our select partition");
    Check(PK_PARTITION_set_current(reference), "reference select partition");
    var deletion = new PK_PARTITION_delete_o_t { delete_non_empty = 1 };
    var ourDeletion = new M.PK_PARTITION_delete_o_s { o_t_version = 1, delete_non_empty = 1 };
    Equal(PK_PARTITION_delete(reference, &deletion), KernelRuntime.PartitionDelete(ours, &ourDeletion), "delete current partition");
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "our partition block");
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &referenceBody), "reference partition block");
    Check(KernelRuntime.MarkCreate(&mark), "our lifecycle mark");
    Check(PK_MARK_create(&referenceMark), "reference lifecycle mark");
    Check(KernelRuntime.PartitionSetCurrent(ourDefault), "our leave partition");
    Check(PK_PARTITION_set_current(referenceDefault), "reference leave partition");
    Check(KernelRuntime.EntityDelete(1, &body), "our delete partition body");
    Check(PK_ENTITY_delete(1, &referenceBody), "reference delete partition body");
    Check(KernelRuntime.PartitionDelete(ours, &ourDeletion), "our delete empty partition");
    Check(PK_PARTITION_delete(reference, &deletion), "reference delete empty partition");
    Check(KernelRuntime.PartitionCreateEmpty(&extra), "our post-mark partition");
    Check(PK_PARTITION_create_empty(&referenceExtra), "reference post-mark partition");
    Check(KernelRuntime.PartitionSetCurrent(extra), "our post-mark selection");
    Check(PK_PARTITION_set_current(referenceExtra), "reference post-mark selection");
    Check(KernelRuntime.MarkGoto(mark), "our partition rollback");
    Check(PK_MARK_goto(referenceMark), "reference partition rollback");
    int current, referenceCurrent;
    Check(KernelRuntime.SessionAskCurrentPartition(&current), "our restored selection");
    Check(PK_SESSION_ask_curr_partition(&referenceCurrent), "reference restored selection");
    Equal(referenceDefault, referenceCurrent, "reference selection after removed partition");
    Equal(ourDefault, current, "our selection after removed partition");
    int bodyClass;
    Equal(PK_ERROR_not_a_tag, PK_ENTITY_ask_class(referenceBody, &bodyClass), "reference explicitly deleted partition body");
    if (KernelRuntime.IsValidTag(body)) throw new InvalidOperationException("Explicitly deleted partition body was resurrected.");
    CheckRoundtrip(control, referenceControl, Path.Combine(directory, "partition-rollback.x_t"));
    Check(KernelRuntime.PartitionSetCurrent(ourDefault), "our reset selection");
    Check(PK_PARTITION_set_current(referenceDefault), "reference reset selection");
    Console.WriteLine("Partition creation/deletion/selection rollback: oracle passed");
}

static unsafe void CheckSharedCurve(string directory)
{
    int body, referenceBody, referenceCount;
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "our shared-curve block");
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &referenceBody), "reference shared-curve block");
    if (!KernelRuntime.TryResolveBodySlot(body, out var bodySlot)) throw new InvalidOperationException("Shared-curve body missing.");
    var firstSlot = KernelRuntime.Bodies[bodySlot].FirstEdgeBody;
    var secondSlot = KernelRuntime.Edges[firstSlot].NextInBody;
    var first = KernelRuntime.TagOf(PoolKind.Edge, firstSlot);
    var second = KernelRuntime.TagOf(PoolKind.Edge, secondSlot);
    int* referenceEdges;
    Check(PK_BODY_ask_edges(referenceBody, &referenceCount, &referenceEdges), "reference shared-curve edges");
    var referenceFirst = MatchingLineEdge(firstSlot, referenceEdges, referenceCount);
    var referenceSecond = MatchingLineEdge(secondSlot, referenceEdges, referenceCount);
    Check(PK_MEMORY_free(referenceEdges), "reference shared-curve edge list free");
    int curve, referenceCurve, detached, referenceDetached;
    Check(KernelRuntime.EdgeAskCurve(first, &curve), "our shared curve");
    Check(PK_EDGE_ask_curve(referenceFirst, &referenceCurve), "reference shared curve");
    Check(KernelRuntime.EdgeAskCurve(second, &detached), "our detached curve");
    Check(PK_EDGE_ask_curve(referenceSecond, &referenceDetached), "reference detached curve");
    Check(KernelRuntime.TopologyDetachGeometry(second), "our detach before sharing");
    Check(PK_TOPOL_detach_geom(referenceSecond), "reference detach before sharing");
    Check(KernelRuntime.EdgeAttachCurves(1, &second, &curve), "our attach shared curve");
    Check(PK_EDGE_attach_curves(1, &referenceSecond, &referenceCurve), "reference attach shared curve");
    int attached;
    Check(PK_EDGE_ask_curve(referenceSecond, &attached), "reference shared ownership");
    Equal(referenceCurve, attached, "reference geometry is shared");
    Equal(2, KernelRuntime.GetCurveByTag(curve).OwnerCount, "our shared ownership");
    SaveReferenceXt(referenceBody, Path.Combine(directory, "reference-shared-curve.x_t"));
    CheckRoundtrip(body, referenceBody, Path.Combine(directory, "shared-curve.x_t"));
    int mark, referenceMark;
    Check(KernelRuntime.MarkCreate(&mark), "our shared-curve mark");
    Check(PK_MARK_create(&referenceMark), "reference shared-curve mark");
    Check(KernelRuntime.EntityDelete(1, &body), "our shared-body delete");
    Check(PK_ENTITY_delete(1, &referenceBody), "reference shared-body delete");
    Check(KernelRuntime.MarkGoto(mark), "our shared-body restore");
    Check(PK_MARK_goto(referenceMark), "reference shared-body restore");
    Equal(2, KernelRuntime.GetCurveByTag(curve).OwnerCount, "restored shared ownership");
    CheckRoundtrip(body, referenceBody, Path.Combine(directory, "shared-curve-rollback.x_t"));
    Check(KernelRuntime.EntityDelete(1, &body), "our final shared-body delete");
    Check(PK_ENTITY_delete(1, &referenceBody), "reference final shared-body delete");
    int entityClass;
    Equal(PK_ERROR_not_a_tag, PK_ENTITY_ask_class(referenceCurve, &entityClass), "reference final geometry release");
    if (KernelRuntime.IsValidTag(curve)) throw new InvalidOperationException("Last geometry reference was not released.");
    Check(KernelRuntime.EntityDelete(1, &detached), "our standalone detached curve delete");
    Check(PK_ENTITY_delete(1, &referenceDetached), "reference standalone detached curve delete");
    Console.WriteLine("Shared curve attach/delete/rollback: XT receive and body compare passed");
}

static unsafe void CheckSharedSurface(string directory)
{
    int body, referenceBody, surface, referenceSurface, referenceCount;
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "our shared-surface block");
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &referenceBody), "reference shared-surface block");
    if (!KernelRuntime.TryResolveBodySlot(body, out var bodySlot)) throw new InvalidOperationException("Shared-surface body missing.");
    var firstSlot = KernelRuntime.Bodies[bodySlot].FirstFaceBody;
    var secondSlot = KernelRuntime.Faces[firstSlot].NextInBody;
    int* faces = stackalloc int[2] { KernelRuntime.TagOf(PoolKind.Face, firstSlot), KernelRuntime.TagOf(PoolKind.Face, secondSlot) };
    int* referenceFaces = stackalloc int[2];
    int* candidates;
    Check(PK_BODY_ask_faces(referenceBody, &referenceCount, &candidates), "reference shared-surface faces");
    referenceFaces[0] = MatchingPlaneFace(firstSlot, candidates, referenceCount);
    referenceFaces[1] = MatchingPlaneFace(secondSlot, candidates, referenceCount);
    Check(PK_MEMORY_free(candidates), "reference shared-surface face list free");
    var definition = new PK_CYL_sf_t { radius = 2, basis_set = Frame(0, 0, 0, 0, 0, 1, 1, 0, 0) };
    var ours = new M.PK_CYL_sf_s { radius = 2 };
    ours.basis_set.axis.coord[2] = 1;
    ours.basis_set.ref_direction.coord[0] = 1;
    Check(PK_CYL_create(&definition, &referenceSurface), "reference common cylinder");
    Check(KernelRuntime.CylCreate(&ours, &surface), "our common cylinder");
    int* detached = stackalloc int[2];
    int* referenceDetached = stackalloc int[2];
    for (var i = 0; i < 2; i++)
    {
        Check(KernelRuntime.FaceAskSurf(faces[i], detached + i), "our detached plane");
        Check(PK_FACE_ask_surf(referenceFaces[i], referenceDetached + i), "reference detached plane");
        Check(KernelRuntime.TopologyDetachGeometry(faces[i]), "our plane detach");
        Check(PK_TOPOL_detach_geom(referenceFaces[i]), "reference plane detach");
    }
    int* surfaces = stackalloc int[2] { surface, surface };
    int* referenceSurfaces = stackalloc int[2] { referenceSurface, referenceSurface };
    byte* senses = stackalloc byte[2] { 1, 1 };
    PK_LOGICAL_t* referenceSenses = stackalloc PK_LOGICAL_t[2];
    referenceSenses[0] = referenceSenses[1] = 1;
    Check(KernelRuntime.FaceAttachSurfaces(2, faces, surfaces, senses), "our attach shared surface");
    Check(PK_FACE_attach_surfs(2, referenceFaces, referenceSurfaces, referenceSenses), "reference attach shared surface");
    Equal(2, KernelRuntime.GetSurfaceByTag(surface).OwnerCount, "shared surface owners");
    SaveReferenceXt(referenceBody, Path.Combine(directory, "reference-shared-surface.x_t"));
    CheckRoundtrip(body, referenceBody, Path.Combine(directory, "shared-surface.x_t"));
    int mark, referenceMark;
    Check(KernelRuntime.MarkCreate(&mark), "our shared-surface mark");
    Check(PK_MARK_create(&referenceMark), "reference shared-surface mark");
    Check(KernelRuntime.EntityDelete(1, &body), "our shared-surface delete");
    Check(PK_ENTITY_delete(1, &referenceBody), "reference shared-surface delete");
    Check(KernelRuntime.MarkGoto(mark), "our shared-surface restore");
    Check(PK_MARK_goto(referenceMark), "reference shared-surface restore");
    Equal(2, KernelRuntime.GetSurfaceByTag(surface).OwnerCount, "restored shared surface owners");
    CheckRoundtrip(body, referenceBody, Path.Combine(directory, "shared-surface-rollback.x_t"));
    Check(KernelRuntime.EntityDelete(1, &body), "our final shared-surface delete");
    Check(PK_ENTITY_delete(1, &referenceBody), "reference final shared-surface delete");
    Check(KernelRuntime.EntityDelete(2, detached), "our detached planes delete");
    Check(PK_ENTITY_delete(2, referenceDetached), "reference detached planes delete");
    Console.WriteLine("Shared surface attach/delete/rollback: XT receive and body compare passed");
}

static unsafe int MatchingPlaneFace(int ourSlot, int* candidates, int count)
{
    var geometry = KernelRuntime.GetSurfaceByTag(KernelRuntime.Faces[ourSlot].SurfTag);
    var plane = KernelRuntime.GetPlaneData(geometry.DataIndex);
    for (var i = 0; i < count; i++)
    {
        int surface;
        Check(PK_FACE_ask_surf(candidates[i], &surface), "reference candidate surface");
        var candidate = new PK_PLANE_sf_t();
        Check(PK_PLANE_ask(surface, &candidate), "reference candidate plane");
        var dot = candidate.basis_set.axis.coord[0] * plane.NormalX + candidate.basis_set.axis.coord[1] * plane.NormalY + candidate.basis_set.axis.coord[2] * plane.NormalZ;
        var offset = (candidate.basis_set.location.coord[0] - plane.LocationX) * plane.NormalX
            + (candidate.basis_set.location.coord[1] - plane.LocationY) * plane.NormalY
            + (candidate.basis_set.location.coord[2] - plane.LocationZ) * plane.NormalZ;
        if (Math.Abs(Math.Abs(dot) - 1) < 1e-12 && Math.Abs(offset) < 1e-12) return candidates[i];
    }
    throw new InvalidOperationException("Corresponding reference plane face was not found.");
}

static unsafe int MatchingLineEdge(int ourSlot, int* candidates, int count)
{
    var geometry = KernelRuntime.GetCurveByTag(KernelRuntime.Edges[ourSlot].CurveTag);
    var line = KernelRuntime.GetLineData(geometry.DataIndex);
    for (var i = 0; i < count; i++)
    {
        int curve;
        Check(PK_EDGE_ask_curve(candidates[i], &curve), "reference candidate curve");
        var candidate = new PK_LINE_sf_t();
        Check(PK_LINE_ask(curve, &candidate), "reference candidate line");
        var dx = candidate.basis_set.location.coord[0] - line.LocationX;
        var dy = candidate.basis_set.location.coord[1] - line.LocationY;
        var dz = candidate.basis_set.location.coord[2] - line.LocationZ;
        var projection = dx * line.AxisX + dy * line.AxisY + dz * line.AxisZ;
        var dot = candidate.basis_set.axis.coord[0] * line.AxisX + candidate.basis_set.axis.coord[1] * line.AxisY + candidate.basis_set.axis.coord[2] * line.AxisZ;
        if (Math.Abs(Math.Abs(dot) - 1) < 1e-12
            && Math.Abs(dx - projection * line.AxisX) + Math.Abs(dy - projection * line.AxisY) + Math.Abs(dz - projection * line.AxisZ) < 1e-12)
            return candidates[i];
    }
    throw new InvalidOperationException("Corresponding reference line edge was not found.");
}

static unsafe void SaveReferenceXt(int body, string path)
{
    var options = new PK_PART_transmit_o_t { transmit_format = PK_transmit_format_text_c };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(1, &body, &options, &block), "reference diagnostic transmit");
    try
    {
        using var file = File.Create(path);
        for (var part = &block; part != null; part = part->next)
            file.Write(new ReadOnlySpan<byte>(part->bytes, checked((int)part->n_bytes)));
    }
    finally { Check(PK_MEMORY_block_f(&block), "reference diagnostic block free"); }
    var bytes = File.ReadAllBytes(path);
    fixed (byte* data = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, data);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        int* parts;
        Check(PK_PART_receive_b(input, &receive, &count, &parts), "reference self receive");
        Check(PK_MEMORY_free(parts), "reference self receive free");
    }
}

static unsafe void CheckGeometryAttachments(int body, int referenceBody)
{
    int count, referenceCount, ourSurface = 0, referenceSurface = 0;
    int ourFace = 0, referenceFace = 0, ourEdge = 0, referenceEdge = 0, ourVertex = 0, referenceVertex = 0;
    int* values;
    int* referenceValues;
    Check(KernelRuntime.BodyAskFaces(body, &count, &values), "our attachment faces");
    Check(PK_BODY_ask_faces(referenceBody, &referenceCount, &referenceValues), "reference attachment faces");
    Equal(referenceCount, count, "attachment face count");
    if (count > 0) { ourFace = values[0]; referenceFace = referenceValues[0]; }
    if (values != null) Check(KernelRuntime.MemoryFree(values), "our face list free");
    if (referenceValues != null) Check(PK_MEMORY_free(referenceValues), "reference face list free");
    if (ourFace != 0)
    {
        Check(KernelRuntime.FaceAskSurf(ourFace, &ourSurface), "our attached surface");
        PK_LOGICAL_t sense;
        Check(PK_FACE_ask_oriented_surf(referenceFace, &referenceSurface, &sense), "reference attached surface");
        // Each kernel retains its original face sense when reattaching.
        if (!KernelRuntime.TryResolveBodySlot(body, out var bodySlot)) throw new InvalidOperationException("Our attachment body is missing.");
        var ourSlot = KernelRuntime.Bodies[bodySlot].FirstFaceBody;
        byte ourSense = KernelRuntime.Faces[ourSlot].Orientation == M.ParasolidConstants.PK_TOPOL_sense_negative_c ? (byte)0 : (byte)1;
        Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our detach surface");
        Check(PK_TOPOL_detach_geom(referenceFace), "reference detach surface");
        Check(KernelRuntime.FaceAttachSurfaces(1, &ourFace, &ourSurface, &ourSense), "our reattach surface");
        Check(PK_FACE_attach_surfs(1, &referenceFace, &referenceSurface, &sense), "reference reattach surface");
    }
    Check(KernelRuntime.BodyAskEdges(body, &count, &values), "our attachment edges");
    Check(PK_BODY_ask_edges(referenceBody, &referenceCount, &referenceValues), "reference attachment edges");
    Equal(referenceCount, count, "attachment edge count");
    if (count > 0) { ourEdge = values[0]; referenceEdge = referenceValues[0]; }
    if (values != null) Check(KernelRuntime.MemoryFree(values), "our edge list free");
    if (referenceValues != null) Check(PK_MEMORY_free(referenceValues), "reference edge list free");
    if (ourEdge != 0)
    {
        int curve, referenceCurve;
        Check(KernelRuntime.EdgeAskCurve(ourEdge, &curve), "our attached curve");
        Check(PK_EDGE_ask_curve(referenceEdge, &referenceCurve), "reference attached curve");
        Check(KernelRuntime.TopologyDetachGeometry(ourEdge), "our detach curve");
        Check(PK_TOPOL_detach_geom(referenceEdge), "reference detach curve");
        Check(KernelRuntime.EdgeAttachCurves(1, &ourEdge, &curve), "our reattach curve");
        Check(PK_EDGE_attach_curves(1, &referenceEdge, &referenceCurve), "reference reattach curve");
    }
    Check(KernelRuntime.BodyAskVertices(body, &count, &values), "our attachment vertices");
    Check(PK_BODY_ask_vertices(referenceBody, &referenceCount, &referenceValues), "reference attachment vertices");
    Equal(referenceCount, count, "attachment vertex count");
    if (count > 0) { ourVertex = values[0]; referenceVertex = referenceValues[0]; }
    if (values != null) Check(KernelRuntime.MemoryFree(values), "our vertex list free");
    if (referenceValues != null) Check(PK_MEMORY_free(referenceValues), "reference vertex list free");
    if (ourVertex != 0)
    {
        int point, referencePoint;
        Check(KernelRuntime.VertexAskPoint(ourVertex, &point), "our attached point");
        Check(PK_VERTEX_ask_point(referenceVertex, &referencePoint), "reference attached point");
        Check(KernelRuntime.TopologyDetachGeometry(ourVertex), "our detach point");
        Check(PK_TOPOL_detach_geom(referenceVertex), "reference detach point");
        Check(KernelRuntime.VertexAttachPoints(1, &ourVertex, &point), "our reattach point");
        Check(PK_VERTEX_attach_points(1, &referenceVertex, &referencePoint), "reference reattach point");
    }
    int mark, referenceMark;
    Check(KernelRuntime.MarkCreate(&mark), "our geometry mark");
    Check(PK_MARK_create(&referenceMark), "reference geometry mark");
    if (ourFace != 0) { Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our marked face detach"); Check(PK_TOPOL_detach_geom(referenceFace), "reference marked face detach"); }
    if (ourEdge != 0) { Check(KernelRuntime.TopologyDetachGeometry(ourEdge), "our marked edge detach"); Check(PK_TOPOL_detach_geom(referenceEdge), "reference marked edge detach"); }
    if (ourVertex != 0) { Check(KernelRuntime.TopologyDetachGeometry(ourVertex), "our marked vertex detach"); Check(PK_TOPOL_detach_geom(referenceVertex), "reference marked vertex detach"); }
    Check(KernelRuntime.MarkGoto(mark), "our geometry rollback");
    Check(PK_MARK_goto(referenceMark), "reference geometry rollback");
}

static unsafe void Compare(M.PK_VECTOR_s* ours, PK_VECTOR_t* reference, int count, string label,
    ComparisonStats stats, bool tangent = false)
{
    for (var i = 0; i < count; i++)
    for (var axis = 0; axis < 3; axis++)
    {
        var a = ours[i].coord[axis];
        var b = reference[i].coord[axis];
        var error = Math.Abs(a - b);
        stats.Comparisons++;
        if (tangent) stats.Tangent = Math.Max(stats.Tangent, error);
        else if (i == 0) stats.Point = Math.Max(stats.Point, error);
        else stats.Derivative = Math.Max(stats.Derivative, error);
        if (!double.IsFinite(a) || !double.IsFinite(b) || error > 1e-13)
            throw new InvalidOperationException($"{label}: derivative={i}, axis={axis}, ours={a:R}, oracle={b:R}");
    }
}

static unsafe void CheckBodyEvaluations(int body, string label)
{
    int count;
    int* entities;
    Check(KernelRuntime.BodyAskEdges(body, &count, &entities), "edges");
    var ours = stackalloc M.PK_VECTOR_s[122];
    var expected = stackalloc PK_VECTOR_t[122];
    for (var i = 0; i < count; i++)
    {
        int curve;
        Check(KernelRuntime.EdgeAskCurve(entities[i], &curve), "curve");
        var reference = MakeCurve(curve);
        var kind = KernelRuntime.GetCurveByTag(curve).Class;
        var stats = ComparisonStats.For("Curve." + kind);
        for (var sample = 0; sample <= 256; sample++)
        {
            var t = kind == CurveClass.Line ? -10.0 + 20.0 * sample / 256 : -2 * Math.Tau + 4 * Math.Tau * sample / 256;
            stats.GridSamples++;
            for (var order = 0; order <= 10; order++)
            {
                M.PK_VECTOR_s tangent;
                PK_VECTOR_t expectedTangent;
                Check(KernelRuntime.CurveEval(curve, t, order, ours), "curve eval");
                Check(PK_CURVE_eval(reference, t, order, expected), "oracle curve eval");
                Compare(ours, expected, order + 1, $"{label} curve={curve} t={t} order={order}", stats);
                Check(KernelRuntime.CurveEvalWithTangent(curve, t, order, ours, &tangent), "tangent eval");
                Check(PK_CURVE_eval_with_tangent(reference, t, order, expected, &expectedTangent), "oracle tangent");
                Compare(ours, expected, order + 1, $"{label} curve={curve} t={t} tangent derivatives order={order}", stats);
                Compare(&tangent, &expectedTangent, 1, $"{label} curve={curve} t={t} unit tangent", stats, tangent: true);
            }
        }
        Equal(PK_CURVE_eval(reference, 0, 11, expected), KernelRuntime.CurveEval(curve, 0, 11, ours), "curve max order");
        Equal(PK_CURVE_eval(0, 0, 0, expected), KernelRuntime.CurveEval(0, 0, 0, ours), "invalid curve tag");
        Check(PK_ENTITY_delete(1, &reference), "delete reference curve");
    }
    Check(KernelRuntime.BodyAskFaces(body, &count, &entities), "faces");
    for (var i = 0; i < count; i++)
    {
        int surface;
        Check(KernelRuntime.FaceAskSurf(entities[i], &surface), "surface");
        var reference = MakeSurface(surface);
        var record = KernelRuntime.GetSurfaceByTag(surface);
        var stats = ComparisonStats.For("Surface." + record.Class);
        Equal(PK_CURVE_eval(reference, 0, 0, expected), KernelRuntime.CurveEval(surface, 0, 0, ours), "wrong curve class");
        foreach (var u in new[] { -2.0, 0.0, 0.25, Math.Tau, 19.7 })
        foreach (var v in new[] { -100.0, -Math.PI / 2, 0.0, 0.4, 1.0, Math.PI / 2, 2.0 })
        foreach (var (du, dv) in new[] { (0, 0), (1, 1), (2, 2), (2, 1), (1, 2), (5, 5), (10, 10), (11, 0), (-1, -1) })
        foreach (byte triangular in new byte[] { 0, 1 })
        {
            var uv = new PK_UV_t();
            uv.param[0] = u; uv.param[1] = v;
            var muv = new M.PK_UV_s();
            muv.param[0] = u; muv.param[1] = v;
            var expectedError = PK_SURF_eval(reference, uv, du, dv, triangular, expected);
            var error = KernelRuntime.SurfEval(surface, muv, du, dv, triangular, ours);
            var context = $"{label} surface={surface} uv={u},{v} orders={du},{dv} triangular={triangular}";
            Equal(expectedError, error, context);
            if (error != 0) continue;
            var nu = Math.Max(0, du); var nv = Math.Max(0, dv);
            var length = triangular != 0 ? (nu + 1) * (nu + 2) / 2 : (nu + 1) * (nv + 1);
            Compare(ours, expected, length, context, stats);
        }
        var minU = record.Class == SurfaceClass.Plane ? -5 : -Math.Tau;
        var maxU = record.Class == SurfaceClass.Plane ? 5 : Math.Tau;
        var minV = -5.0;
        var maxV = 5.0;
        if (record.Class == SurfaceClass.Sphere) { minV = -Math.PI / 2; maxV = Math.PI / 2; }
        else if (record.Class == SurfaceClass.Torus) { minV = -Math.PI; maxV = Math.PI; }
        else if (record.Class == SurfaceClass.Cone)
        {
            var cone = KernelRuntime.GetConeData(record.DataIndex);
            minV = -cone.Radius / Math.Tan(cone.SemiAngle);
        }
        for (var iu = 0; iu <= 64; iu++)
        for (var iv = 0; iv <= 64; iv++)
        {
            var uv = new PK_UV_t();
            uv.param[0] = minU + (maxU - minU) * iu / 64;
            uv.param[1] = minV + (maxV - minV) * iv / 64;
            var muv = new M.PK_UV_s();
            muv.param[0] = uv.param[0]; muv.param[1] = uv.param[1];
            stats.GridSamples++;
            for (byte triangular = 0; triangular <= 1; triangular++)
            {
                var context = $"{label} {record.Class} surface={surface} grid={iu},{iv} uv={uv.param[0]:R},{uv.param[1]:R} triangular={triangular}";
                Check(PK_SURF_eval(reference, uv, 10, 10, triangular, expected), "oracle " + context);
                Check(KernelRuntime.SurfEval(surface, muv, 10, 10, triangular, ours), context);
                Compare(ours, expected, triangular == 0 ? 121 : 66, context, stats);
            }
        }
        Check(PK_ENTITY_delete(1, &reference), "delete reference surface");
    }
}

static unsafe PK_AXIS2_sf_t Frame(double ox, double oy, double oz, double ax, double ay, double az, double rx, double ry, double rz)
{
    var frame = new PK_AXIS2_sf_t();
    frame.location.coord[0] = ox; frame.location.coord[1] = oy; frame.location.coord[2] = oz;
    frame.axis.coord[0] = ax; frame.axis.coord[1] = ay; frame.axis.coord[2] = az;
    frame.ref_direction.coord[0] = rx; frame.ref_direction.coord[1] = ry; frame.ref_direction.coord[2] = rz;
    return frame;
}

static unsafe int MakeCurve(int curve)
{
    var record = KernelRuntime.GetCurveByTag(curve);
    int reference;
    if (record.Class == CurveClass.Line)
    {
        var d = KernelRuntime.GetLineData(record.DataIndex);
        var sf = new PK_LINE_sf_t();
        sf.basis_set.location.coord[0] = d.LocationX; sf.basis_set.location.coord[1] = d.LocationY; sf.basis_set.location.coord[2] = d.LocationZ;
        sf.basis_set.axis.coord[0] = d.AxisX; sf.basis_set.axis.coord[1] = d.AxisY; sf.basis_set.axis.coord[2] = d.AxisZ;
        Check(PK_LINE_create(&sf, &reference), "oracle line");
    }
    else
    {
        var d = KernelRuntime.GetCircleData(record.DataIndex);
        var sf = new PK_CIRCLE_sf_t { radius = d.Radius,
            basis_set = Frame(d.CenterX, d.CenterY, d.CenterZ, d.AxisX, d.AxisY, d.AxisZ, d.RefDirX, d.RefDirY, d.RefDirZ) };
        Check(PK_CIRCLE_create(&sf, &reference), "oracle circle");
    }
    return reference;
}

static unsafe int MakeSurface(int surface)
{
    var record = KernelRuntime.GetSurfaceByTag(surface);
    int reference;
    switch (record.Class)
    {
        case SurfaceClass.Plane:
            var p = KernelRuntime.GetPlaneData(record.DataIndex);
            var plane = new PK_PLANE_sf_t { basis_set = Frame(p.LocationX, p.LocationY, p.LocationZ, p.NormalX, p.NormalY, p.NormalZ, p.RefDirX, p.RefDirY, p.RefDirZ) };
            Check(PK_PLANE_create(&plane, &reference), "oracle plane"); break;
        case SurfaceClass.Cylinder:
            var c = KernelRuntime.GetCylinderData(record.DataIndex);
            var cyl = new PK_CYL_sf_t { radius = c.Radius, basis_set = Frame(c.LocationX, c.LocationY, c.LocationZ, c.AxisX, c.AxisY, c.AxisZ, c.RefDirX, c.RefDirY, c.RefDirZ) };
            Check(PK_CYL_create(&cyl, &reference), "oracle cylinder"); break;
        case SurfaceClass.Cone:
            var d = KernelRuntime.GetConeData(record.DataIndex);
            var cone = new PK_CONE_sf_t { radius = d.Radius, semi_angle = d.SemiAngle, basis_set = Frame(d.LocationX, d.LocationY, d.LocationZ, d.AxisX, d.AxisY, d.AxisZ, d.RefDirX, d.RefDirY, d.RefDirZ) };
            Check(PK_CONE_create(&cone, &reference), "oracle cone"); break;
        case SurfaceClass.Sphere:
            var s = KernelRuntime.GetSphereData(record.DataIndex);
            var sphere = new PK_SPHERE_sf_t { radius = s.Radius, basis_set = Frame(s.CenterX, s.CenterY, s.CenterZ, s.AxisX, s.AxisY, s.AxisZ, s.RefDirX, s.RefDirY, s.RefDirZ) };
            Check(PK_SPHERE_create(&sphere, &reference), "oracle sphere"); break;
        case SurfaceClass.Torus:
            var t = KernelRuntime.GetTorusData(record.DataIndex);
            var torus = new PK_TORUS_sf_t { major_radius = t.MajorRadius, minor_radius = t.MinorRadius, basis_set = Frame(t.LocationX, t.LocationY, t.LocationZ, t.AxisX, t.AxisY, t.AxisZ, t.RefDirX, t.RefDirY, t.RefDirZ) };
            Check(PK_TORUS_create(&torus, &reference), "oracle torus"); break;
        default: throw new InvalidOperationException("Unexpected surface.");
    }
    return reference;
}

static unsafe void CheckRoundtrip(int body, int reference, string path)
{
    var options = new M.PK_PART_transmit_o_s { o_t_version = 4, transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "transmit");
    try
    {
        using var file = File.Create(path);
        for (var b = &block; b != null; b = b->next) file.Write(new ReadOnlySpan<byte>(b->bytes, checked((int)b->n_bytes)));
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free transmit"); }
    var bytes = File.ReadAllBytes(path);
    fixed (byte* data = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, data);
        var receiveOptions = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        int* parts;
        Check(PK_PART_receive_b(input, &receiveOptions, &count, &parts), "oracle receive");
        try
        {
            Equal(1, count, "part count");
            var compareOptions = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = 0, acc_dev_tests = 0, non_match_tests = 0 };
            var result = new PK_DEBUG_BODY_compare_r_t();
            Check(PK_DEBUG_BODY_compare(reference, parts[0], &compareOptions, &result), "body compare");
            try
            {
                if (result.global_result != PK_DEBUG_global_res_no_diffs_c || result.local_result != PK_DEBUG_local_res_no_diffs_c)
                {
                    for (var i = 0; i < result.n_global_diffs; i++)
                        Console.WriteLine($"global diff={result.global_diffs[i].diff} masters={result.global_diffs[i].n_masters} similars={result.global_diffs[i].n_similars}");
                    for (var i = 0; i < result.n_face_pairs; i++)
                    {
                        var pair = result.face_pairs[i];
                        for (var j = 0; j < pair.n_local_diffs; j++)
                        {
                            var diff = pair.local_diffs[j];
                            Console.WriteLine($"faces={pair.master_face}/{pair.similar_face} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity}");
                        }
                    }
                    throw new InvalidOperationException($"{path}: global={result.global_result} local={result.local_result} global_diffs={result.n_global_diffs} face_pairs={result.n_face_pairs}");
                }
            }
            finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free compare"); }
        }
        finally { Check(PK_MEMORY_free(parts), "free parts"); }
    }
}

// ── Dependent geometry: ellipse, B-surface, offset, swept, spun, SP-curve ──
// Trimmed curves have no PK creation interface, so the trimmed-curve check
// compares our evaluation against PK_CURVE_eval of the basis curve instead.

static unsafe void CheckDependentGeometryEvaluations()
{
    Console.WriteLine("Dependent geometry evaluations (numerical only):");
    CheckEllipseEvaluation();
    CheckBSurfaceEvaluation();
    CheckOffsetEvaluation();
    CheckSweptEvaluation();
    CheckSpunEvaluation();
    CheckSpCurveEvaluation();
    CheckTrCurveEvaluation();
}

static unsafe M.PK_AXIS2_sf_s ManagedFrame(PK_AXIS2_sf_t frame)
{
    var result = new M.PK_AXIS2_sf_s();
    for (var i = 0; i < 3; i++)
    {
        result.location.coord[i] = frame.location.coord[i];
        result.axis.coord[i] = frame.axis.coord[i];
        result.ref_direction.coord[i] = frame.ref_direction.coord[i];
    }
    return result;
}

static unsafe void CheckEllipseEvaluation()
{
    var basis = Frame(1, 2, 3, 0, 0, 1, 1, 0, 0);
    var oracleSf = new PK_ELLIPSE_sf_t { R1 = 4, R2 = 1.5, basis_set = basis };
    int reference;
    Check(PK_ELLIPSE_create(&oracleSf, &reference), "oracle ellipse");
    var managedSf = new M.PK_ELLIPSE_sf_s { R1 = 4, R2 = 1.5, basis_set = ManagedFrame(basis) };
    int ours;
    Check(KernelRuntime.EllipseCreate(&managedSf, &ours), "our ellipse");
    int classCode;
    Check(PK_ENTITY_ask_class(reference, &classCode), "oracle ellipse class");
    Equal(M.ParasolidConstants.PK_CLASS_ellipse, classCode, "ellipse class");
    var samples = new double[65];
    for (var i = 0; i <= 64; i++) samples[i] = -2 * Math.Tau + 4 * Math.Tau * i / 64;
    CompareCurveEvaluations(ours, reference, samples, "ellipse", CurveClass.Ellipse);
    Check(PK_ENTITY_delete(1, &reference), "delete oracle ellipse");
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our ellipse");
    Console.WriteLine("  ellipse: passed");
}

static unsafe int[] CreateBCurveBoth(int degree, double[] poles, double[] knots, int[] mults, int dimension, bool rational, string label)
{
    fixed (double* p = poles)
    fixed (double* k = knots)
    fixed (int* m = mults)
    {
        var oracleSf = new PK_BCURVE_sf_t
        {
            degree = degree,
            n_vertices = poles.Length / dimension,
            vertex_dim = dimension,
            is_rational = rational ? PK_LOGICAL_true : PK_LOGICAL_false,
            vertex = p,
            form = PK_BCURVE_form_unset_c,
            n_knots = knots.Length,
            knot_mult = m,
            knot = k,
            knot_type = PK_knot_unset_c,
            is_periodic = PK_LOGICAL_false,
            is_closed = PK_LOGICAL_false,
            self_intersecting = PK_self_intersect_unset_c,
        };
        int reference;
        Check(PK_BCURVE_create(&oracleSf, &reference), label + " oracle bcurve");
        var managedSf = new M.PK_BCURVE_sf_s
        {
            degree = degree,
            n_vertices = poles.Length / dimension,
            vertex_dim = dimension,
            is_rational = (byte)(rational ? 1 : 0),
            vertex = p,
            form = M.ParasolidConstants.PK_BCURVE_form_unset_c,
            n_knots = knots.Length,
            knot_mult = m,
            knot = k,
            knot_type = M.ParasolidConstants.PK_knot_unset_c,
            self_intersecting = M.ParasolidConstants.PK_self_intersect_unset_c,
        };
        int ours;
        Check(KernelRuntime.BCurveCreate(&managedSf, &ours), label + " our bcurve");
        return new[] { ours, reference };
    }
}

static unsafe void CheckBSurfaceEvaluation()
{
    // Cubic Bézier patch, 4x4 poles, clamped knots.
    var poles = new double[4 * 4 * 3];
    for (var i = 0; i < 4; i++)
    for (var j = 0; j < 4; j++)
    {
        poles[(i * 4 + j) * 3] = 1 + i - 0.4 * j;
        poles[(i * 4 + j) * 3 + 1] = -2 + 0.3 * i * i + j;
        poles[(i * 4 + j) * 3 + 2] = 0.5 + 0.5 * i * j - 0.2 * j * j;
    }
    fixed (double* p = poles)
    fixed (double* uk = new[] { 0.0, 1.0 })
    fixed (double* vk = new[] { 0.0, 1.0 })
    fixed (int* um = new[] { 4, 4 })
    fixed (int* vm = new[] { 4, 4 })
    {
        var oracleSf = new PK_BSURF_sf_t
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = PK_LOGICAL_false, vertex = p,
            form = PK_BSURF_form_unset_c,
            n_u_knots = 2, n_v_knots = 2,
            u_knot = uk, v_knot = vk, u_knot_mult = um, v_knot_mult = vm,
            u_knot_type = PK_knot_unset_c, v_knot_type = PK_knot_unset_c,
            is_u_periodic = PK_LOGICAL_false, is_v_periodic = PK_LOGICAL_false,
            is_u_closed = PK_LOGICAL_false, is_v_closed = PK_LOGICAL_false,
            self_intersecting = PK_self_intersect_unset_c, convexity = PK_convexity_unset_c,
        };
        int reference;
        Check(PK_BSURF_create(&oracleSf, &reference), "oracle bsurf");
        var managedSf = new M.PK_BSURF_sf_s
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = 0, vertex = p,
            form = M.ParasolidConstants.PK_BSURF_form_unset_c,
            n_u_knots = 2, n_v_knots = 2,
            u_knot = uk, v_knot = vk, u_knot_mult = um, v_knot_mult = vm,
            u_knot_type = M.ParasolidConstants.PK_knot_unset_c, v_knot_type = M.ParasolidConstants.PK_knot_unset_c,
            self_intersecting = M.ParasolidConstants.PK_self_intersect_unset_c,
            convexity = M.ParasolidConstants.PK_convexity_unset_c,
        };
        int ours;
        Check(KernelRuntime.BSurfCreate(&managedSf, &ours), "our bsurf");
        int classCode;
        Check(PK_ENTITY_ask_class(reference, &classCode), "oracle bsurf class");
        Equal(M.ParasolidConstants.PK_CLASS_bsurf, classCode, "bsurf class");
        var stats = ComparisonStats.For("Surface.BSurface");
        // In-range and out-of-range points: probes the PK extension behaviour.
        var us = new[] { 0.0, 0.3, 0.6, 1.0, 1.25, -0.5, -2.0, 4.0 };
        var vs = new[] { 0.0, 0.4, 0.8, 1.0, 1.75, -1.0, 2.5 };
        CompareSurfaceEvaluations(ours, reference, us, vs, "bsurf", stats);
        Check(PK_ENTITY_delete(1, &reference), "delete oracle bsurf");
        Check(KernelRuntime.EntityDelete(1, &ours), "delete our bsurf");
        Console.WriteLine("  bsurf: passed");
    }
}

static unsafe void CheckOffsetEvaluation()
{
    var bsurf = CreateBSurfacePair("offset base");
    int ours = bsurf[0], reference = bsurf[1];
    var oracleSf = new PK_OFFSET_sf_t { underlying_surface = reference, offset_distance = 0.4 };
    int referenceOffset, referenceFirstOffset;
    Check(PK_OFFSET_create(&oracleSf, &referenceOffset), "oracle offset");
    referenceFirstOffset = referenceOffset;
    var managedSf = new M.PK_OFFSET_sf_s { underlying_surface = ours, offset_distance = 0.4 };
    int ourOffset, ourFirstOffset;
    Check(KernelRuntime.OffsetCreate(&managedSf, &ourOffset), "our offset");
    ourFirstOffset = ourOffset;
    int classCode;
    Check(PK_ENTITY_ask_class(referenceOffset, &classCode), "oracle offset class");
    Equal(M.ParasolidConstants.PK_CLASS_offset, classCode, "offset class");
    var stats = ComparisonStats.For("Surface.Offset");
    var us = new[] { 0.0, 0.25, 0.6, 1.0, 0.75 };
    var vs = new[] { 0.0, 0.3, 0.5, 1.0, 1.5 };
    CompareSurfaceEvaluations(ourOffset, referenceOffset, us, vs, "offset(+0.4)", stats, 0.15);
    // Negative distance as well.
    var negativeOracle = new PK_OFFSET_sf_t { underlying_surface = reference, offset_distance = -0.7 };
    Check(PK_OFFSET_create(&negativeOracle, &referenceOffset), "oracle offset negative");
    var negativeManaged = new M.PK_OFFSET_sf_s { underlying_surface = ours, offset_distance = -0.7 };
    Check(KernelRuntime.OffsetCreate(&negativeManaged, &ourOffset), "our offset negative");
    CompareSurfaceEvaluations(ourOffset, referenceOffset, us, vs, "offset(-0.7)", stats, 0.15);
    Check(PK_ENTITY_delete(1, &referenceOffset), "delete oracle offset");
    Check(KernelRuntime.EntityDelete(1, &ourOffset), "delete our offset");
    Check(PK_ENTITY_delete(1, &referenceFirstOffset), "delete oracle first offset");
    Check(KernelRuntime.EntityDelete(1, &ourFirstOffset), "delete our first offset");
    // PK reclaims the base B-surface together with its offsets (its tag is
    // already gone here), while our kernel keeps the standalone base alive.
    var baseDeleteError = PK_ENTITY_delete(1, &reference);
    if (baseDeleteError != 0 && baseDeleteError != PK_ERROR_not_a_tag)
        Check(baseDeleteError, "delete oracle offset base");
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our offset base");
    Console.WriteLine("  offset: passed");
}

static unsafe void CheckSweptEvaluation()
{
    var bcurve = CreateBCurveBoth(2, new[] { 0.0, 0, 0, 1, 2, 0, 3, 1, 2 }, new[] { 0.0, 4.0 }, new[] { 3, 3 }, 3, false, "swept section");
    int ours = bcurve[0], reference = bcurve[1];
    PK_VECTOR1_t direction = default;
    direction.coord[2] = 1;
    var oracleSf = new PK_SWEPT_sf_t { curve = reference, direction = direction };
    int referenceSwept;
    Check(PK_SWEPT_create(&oracleSf, &referenceSwept), "oracle swept");
    var managedDirection = new M.PK_VECTOR_s();
    managedDirection.coord[2] = 1;
    var managedSf = new M.PK_SWEPT_sf_s { curve = ours, direction = managedDirection };
    int ourSwept;
    Check(KernelRuntime.SweptCreate(&managedSf, &ourSwept), "our swept");
    int classCode;
    Check(PK_ENTITY_ask_class(referenceSwept, &classCode), "oracle swept class");
    Equal(M.ParasolidConstants.PK_CLASS_swept, classCode, "swept class");
    var stats = ComparisonStats.For("Surface.Swept");
    var us = new[] { 0.0, 1.1, 2.0, 4.0, 5.0, -1.0, 6.5 };
    var vs = new[] { 0.0, 0.5, 1.0, -2.0, 3.0 };
    CompareSurfaceEvaluations(ourSwept, referenceSwept, us, vs, "swept", stats);
    Check(PK_ENTITY_delete(1, &referenceSwept), "delete oracle swept");
    Check(KernelRuntime.EntityDelete(1, &ourSwept), "delete our swept");
    // PK reclaims the section curve together with the swept surface.
    var sweptSectionError = PK_ENTITY_delete(1, &reference);
    if (sweptSectionError != 0 && sweptSectionError != PK_ERROR_not_a_tag)
        Check(sweptSectionError, "delete oracle swept section");
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our swept section");
    Console.WriteLine("  swept: passed");
}

static unsafe void CheckSpunEvaluation()
{
    var bcurve = CreateBCurveBoth(2, new[] { 2.0, 0, 0, 3, 1, 0.5, 4, 0, 1 }, new[] { 0.0, 3.0 }, new[] { 3, 3 }, 3, false, "spun profile");
    int ours = bcurve[0], reference = bcurve[1];
    var axis = new PK_AXIS1_sf_t();
    axis.location.coord[0] = 0.25;
    axis.location.coord[1] = -0.5;
    axis.axis.coord[2] = 1;
    var oracleSf = new PK_SPUN_sf_t { curve = reference, axis = axis };
    int referenceSpun;
    Check(PK_SPUN_create(&oracleSf, &referenceSpun), "oracle spun");
    var managedAxis = new M.PK_AXIS1_sf_s();
    managedAxis.location.coord[0] = 0.25;
    managedAxis.location.coord[1] = -0.5;
    managedAxis.axis.coord[2] = 1;
    var managedSf = new M.PK_SPUN_sf_s { curve = ours, axis = managedAxis };
    int ourSpun;
    Check(KernelRuntime.SpunCreate(&managedSf, &ourSpun), "our spun");
    int classCode;
    Check(PK_ENTITY_ask_class(referenceSpun, &classCode), "oracle spun class");
    Equal(M.ParasolidConstants.PK_CLASS_spun, classCode, "spun class");
    var stats = ComparisonStats.For("Surface.Spun");
    var us = new[] { 0.0, 1.2, 3.0, 2.0, -0.5, 4.0 };
    var vs = new[] { 0.0, 1.0, Math.PI, -2.0, Math.Tau, 4.5 };
    CompareSurfaceEvaluations(ourSpun, referenceSpun, us, vs, "spun", stats);
    Check(PK_ENTITY_delete(1, &referenceSpun), "delete oracle spun");
    Check(KernelRuntime.EntityDelete(1, &ourSpun), "delete our spun");
    // PK reclaims the profile curve together with the spun surface.
    var spunProfileError = PK_ENTITY_delete(1, &reference);
    if (spunProfileError != 0 && spunProfileError != PK_ERROR_not_a_tag)
        Check(spunProfileError, "delete oracle spun profile");
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our spun profile");
    Console.WriteLine("  spun: passed");
}

static unsafe void CheckSpCurveEvaluation()
{
    // A helix: 2D B-curve (u: 0..tau, v: 0..5) embedded in a cylinder of radius 2.
    var cylinder = new PK_CYL_sf_t { radius = 2, basis_set = Frame(0, 0, 0, 0, 0, 1, 1, 0, 0) };
    int referenceSurface;
    Check(PK_CYL_create(&cylinder, &referenceSurface), "oracle spcurve cylinder");
    var managedCylinder = new M.PK_CYL_sf_s { radius = 2, basis_set = ManagedFrame(cylinder.basis_set) };
    int ourSurface;
    Check(KernelRuntime.CylCreate(&managedCylinder, &ourSurface), "our spcurve cylinder");
    var bcurve = CreateBCurveBoth(1, new[] { 0.0, 0, Math.Tau, 5 }, new[] { 0.0, 2.0 }, new[] { 2, 2 }, 2, false, "spcurve 2d");
    int ours = bcurve[0], reference = bcurve[1];
    var oracleSf = new PK_SPCURVE_sf_t { surf = referenceSurface, curve = reference };
    int referenceSpCurve;
    Check(PK_SPCURVE_create(&oracleSf, &referenceSpCurve), "oracle spcurve");
    var managedSf = new M.PK_SPCURVE_sf_s { surf = ourSurface, curve = ours };
    int ourSpCurve;
    Check(KernelRuntime.SpCurveCreate(&managedSf, &ourSpCurve), "our spcurve");
    int classCode;
    Check(PK_ENTITY_ask_class(referenceSpCurve, &classCode), "oracle spcurve class");
    Equal(M.ParasolidConstants.PK_CLASS_spcurve, classCode, "spcurve class");
    var samples = new double[49];
    for (var i = 0; i <= 48; i++) samples[i] = -1.0 + 4.0 * i / 48;
    CompareCurveEvaluations(ourSpCurve, referenceSpCurve, samples, "spcurve", CurveClass.SPCurve, maxOrder: 2);
    {
        var capExpected = stackalloc PK_VECTOR_t[1];
        var capOurs = stackalloc M.PK_VECTOR_s[1];
        Equal(PK_CURVE_eval(referenceSpCurve, -1, 3, capExpected), 1010, "oracle spcurve order cap");
        Equal(KernelRuntime.CurveEval(ourSpCurve, -1, 3, capOurs), M.ParasolidConstants.PK_ERROR_too_many_derivatives, "our spcurve order cap");
    }
    Check(PK_ENTITY_delete(1, &referenceSpCurve), "delete oracle spcurve");
    Check(KernelRuntime.EntityDelete(1, &ourSpCurve), "delete our spcurve");
    // PK reclaims the 2D B-curve (and possibly the surface) with the SP-curve.
    var sp2dError = PK_ENTITY_delete(1, &reference);
    if (sp2dError != 0 && sp2dError != PK_ERROR_not_a_tag)
        Check(sp2dError, "delete oracle spcurve 2d");
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our spcurve 2d");
    var spSurfError = PK_ENTITY_delete(1, &referenceSurface);
    if (spSurfError != 0 && spSurfError != PK_ERROR_not_a_tag)
        Check(spSurfError, "delete oracle spcurve surface");
    Check(KernelRuntime.EntityDelete(1, &ourSurface), "delete our spcurve surface");
    Console.WriteLine("  spcurve: passed");
}

static unsafe void CheckTrCurveEvaluation()
{
    // Parasolid has no PK_TRCURVE_create, so the oracle for a trimmed curve is
    // the basis curve itself: identical parameterisation and derivatives.
    var basis = Frame(1, 2, 3, 0, 0, 1, 1, 0, 0);
    var oracleSf = new PK_ELLIPSE_sf_t { R1 = 4, R2 = 1.5, basis_set = basis };
    int reference;
    Check(PK_ELLIPSE_create(&oracleSf, &reference), "oracle trcurve basis");
    var managedSf = new M.PK_ELLIPSE_sf_s { R1 = 4, R2 = 1.5, basis_set = ManagedFrame(basis) };
    int ourBasis;
    Check(KernelRuntime.EllipseCreate(&managedSf, &ourBasis), "our trcurve basis");
    var interval = new M.PK_INTERVAL_s();
    interval.value[0] = 1.0;
    interval.value[1] = 4.0;
    var trSf = new M.PK_TRCURVE_sf_s { basis_curve = ourBasis, t_int = interval };
    int ours;
    Check(KernelRuntime.TrCurveCreate(&trSf, &ours), "our trcurve");
    var samples = new double[49];
    for (var i = 0; i <= 48; i++) samples[i] = -1.0 + 7.0 * i / 48;
    CompareCurveEvaluations(ours, reference, samples, "trcurve(basis)", CurveClass.TRCurve);
    Check(KernelRuntime.EntityDelete(1, &ours), "delete our trcurve");
    Check(KernelRuntime.EntityDelete(1, &ourBasis), "delete our trcurve basis");
    Check(PK_ENTITY_delete(1, &reference), "delete oracle trcurve basis");
    Console.WriteLine("  trcurve (vs basis curve): passed");
}

static unsafe int[] CreateBSurfacePair(string label)
{
    // Cubic Bézier patch shared by the offset check.
    var poles = new double[4 * 4 * 3];
    for (var i = 0; i < 4; i++)
    for (var j = 0; j < 4; j++)
    {
        poles[(i * 4 + j) * 3] = 1 + i - 0.4 * j;
        poles[(i * 4 + j) * 3 + 1] = -2 + 0.3 * i * i + j;
        poles[(i * 4 + j) * 3 + 2] = 0.5 + 0.5 * i * j - 0.2 * j * j;
    }
    fixed (double* p = poles)
    fixed (double* uk = new[] { 0.0, 1.0 })
    fixed (double* vk = new[] { 0.0, 1.0 })
    fixed (int* um = new[] { 4, 4 })
    fixed (int* vm = new[] { 4, 4 })
    {
        var oracleSf = new PK_BSURF_sf_t
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = PK_LOGICAL_false, vertex = p,
            form = PK_BSURF_form_unset_c,
            n_u_knots = 2, n_v_knots = 2,
            u_knot = uk, v_knot = vk, u_knot_mult = um, v_knot_mult = vm,
            u_knot_type = PK_knot_unset_c, v_knot_type = PK_knot_unset_c,
            is_u_periodic = PK_LOGICAL_false, is_v_periodic = PK_LOGICAL_false,
            is_u_closed = PK_LOGICAL_false, is_v_closed = PK_LOGICAL_false,
            self_intersecting = PK_self_intersect_unset_c, convexity = PK_convexity_unset_c,
        };
        int reference;
        Check(PK_BSURF_create(&oracleSf, &reference), label + " oracle bsurf");
        var managedSf = new M.PK_BSURF_sf_s
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = 0, vertex = p,
            form = M.ParasolidConstants.PK_BSURF_form_unset_c,
            n_u_knots = 2, n_v_knots = 2,
            u_knot = uk, v_knot = vk, u_knot_mult = um, v_knot_mult = vm,
            u_knot_type = M.ParasolidConstants.PK_knot_unset_c, v_knot_type = M.ParasolidConstants.PK_knot_unset_c,
            self_intersecting = M.ParasolidConstants.PK_self_intersect_unset_c,
            convexity = M.ParasolidConstants.PK_convexity_unset_c,
        };
        int ours;
        Check(KernelRuntime.BSurfCreate(&managedSf, &ours), label + " our bsurf");
        return new[] { ours, reference };
    }
}

static unsafe void CompareCurveEvaluations(int ours, int reference, double[] samples, string label, CurveClass kind, int maxOrder = 10)
{
    var ourOutput = stackalloc M.PK_VECTOR_s[11];
    var expected = stackalloc PK_VECTOR_t[11];
    var stats = ComparisonStats.For("Curve." + kind);
    M.PK_VECTOR_s ourTangent;
    PK_VECTOR_t expectedTangent;
    foreach (var t in samples)
    {
        for (var order = 0; order <= maxOrder; order++)
        {
            stats.GridSamples++;
            var context = $"{label} t={t:R} order={order}";
            Check(KernelRuntime.CurveEval(ours, t, order, ourOutput), "our " + context);
            Check(PK_CURVE_eval(reference, t, order, expected), "oracle " + context);
            Compare(ourOutput, expected, order + 1, context, stats);
            Check(KernelRuntime.CurveEvalWithTangent(ours, t, order, ourOutput, &ourTangent), "our tangent " + context);
            Check(PK_CURVE_eval_with_tangent(reference, t, order, expected, &expectedTangent), "oracle tangent " + context);
            Compare(ourOutput, expected, order + 1, context + " tangent", stats);
            Compare(&ourTangent, &expectedTangent, 1, context + " unit tangent", stats, tangent: true);
        }
    }
    if (maxOrder >= 11)
        Equal(PK_CURVE_eval(reference, 0, 11, expected), KernelRuntime.CurveEval(ours, 0, 11, ourOutput), label + " max order");
}

static unsafe void CompareSurfaceEvaluations(int ours, int reference, double[] us, double[] vs, string label,
    ComparisonStats stats, double highOrderTolerance = 1e-13)
{
    var ourOutput = stackalloc M.PK_VECTOR_s[121];
    var expected = stackalloc PK_VECTOR_t[121];
    foreach (var u in us)
    foreach (var v in vs)
    foreach (var (du, dv) in new[] { (0, 0), (1, 1), (2, 2), (1, 2), (3, 3), (10, 10), (11, 0), (-1, -1) })
    foreach (byte triangular in new byte[] { 0, 1 })
    {
        if (highOrderTolerance > 1e-12 && ((du > 2 && du < 11) || (dv > 2 && dv < 11))) continue;
        stats.GridSamples++;
        var uv = new PK_UV_t();
        uv.param[0] = u;
        uv.param[1] = v;
        var muv = new M.PK_UV_s();
        muv.param[0] = u;
        muv.param[1] = v;
        var context = $"{label} uv={u:R},{v:R} orders={du},{dv} triangular={triangular}";
        var expectedError = PK_SURF_eval(reference, uv, du, dv, triangular, expected);
        var error = KernelRuntime.SurfEval(ours, muv, du, dv, triangular, ourOutput);
        Equal(expectedError, error, context);
        if (error != 0) continue;
        var nu = Math.Max(0, du);
        var nv = Math.Max(0, dv);
        var length = triangular != 0 ? (nu + 1) * (nu + 2) / 2 : (nu + 1) * (nv + 1);
        for (var index = 0; index < length; index++)
        {
            (int i, int j) order = triangular != 0 ? TriangularDerivativeOrder(index) : (index % (nu + 1), index / (nu + 1));
            var tolerance = order.i + order.j >= 3 ? highOrderTolerance : 1e-13;
            for (var axis = 0; axis < 3; axis++)
            {
                var a = ourOutput[index].coord[axis];
                var b = expected[index].coord[axis];
                var delta = Math.Abs(a - b);
                stats.Comparisons++;
                if (index == 0) stats.Point = Math.Max(stats.Point, delta);
                else stats.Derivative = Math.Max(stats.Derivative, delta);
                if (!double.IsFinite(a) || !double.IsFinite(b) || delta > tolerance)
                    throw new InvalidOperationException($"{context}: derivative=({order.i},{order.j}), axis={axis}, ours={a:R}, oracle={b:R}, tolerance={tolerance:R}");
            }
        }
    }
}

/// <summary>PK's triangular packing walks total-order diagonals: (0,0), (1,0), (0,1), (2,0), ...</summary>
static (int i, int j) TriangularDerivativeOrder(int index)
{
    var total = 0;
    var offset = 0;
    while (offset + total + 1 <= index) { offset += total + 1; total++; }
    var within = index - offset;
    // Within a diagonal the u order descends from `total`.
    return (total - within, within);
}

sealed class ComparisonStats
{
    internal static readonly SortedDictionary<string, ComparisonStats> ByKind = new();
    internal long GridSamples;
    internal long Comparisons;
    internal double Point;
    internal double Tangent;
    internal double Derivative;

    internal static ComparisonStats For(string kind)
    {
        if (!ByKind.TryGetValue(kind, out var stats)) ByKind.Add(kind, stats = new ComparisonStats());
        return stats;
    }
}
