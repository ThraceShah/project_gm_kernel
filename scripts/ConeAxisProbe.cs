#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=ConeAxisProbe
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Cone axis XT/PK convention probe and assertive cross-verification (spec §8.3 / GPT Review 5).
// 1. Real Parasolid creates a tilted solid cone; PK_CONE_ask reports PK-interface convention.
// 2. PK_PART_transmit_b writes text XT (transmit_version=371).
// 3. Schema-level parsing verifies raw CONE node fields (axis, ref, radius, angles, location).
// 4. Direction B: Real PK receives the XT bytes and re-evaluates S(0, 1), asserting exact roundtrip.
// 5. Direction C: Our kernel imports real PK XT and materializes the cone, asserting exact match.
// 6. Direction D: Our kernel creates cone, transmits XT, and real PK receives it, asserting exact match.
// 7. Saves minimal XT fixture and evidence log to docs/reviews/cone_probe_evidence/.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Cone axis probe", out var session, out var message))
{
    Console.WriteLine("NotRun: " + message);
    return;
}
using var _ = session;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var evidenceDir = Path.Combine(scriptDir, "..", "docs", "reviews", "cone_probe_evidence");
Directory.CreateDirectory(evidenceDir);

var evidenceLog = new StringBuilder();
void Log(string line)
{
    Console.WriteLine(line);
    evidenceLog.AppendLine(line);
}

RunProbe(evidenceDir, Log);
File.WriteAllText(Path.Combine(evidenceDir, "cone_probe_evidence.txt"), evidenceLog.ToString());
Console.WriteLine($"\nEvidence written to {evidenceDir}/cone_probe_evidence.txt");

static unsafe void RunProbe(string evidenceDir, Action<string> log)
{
    SelfTestAssertNear();
    log("AssertNear self-test passed (NaN/Inf/negative tolerance correctly rejected).");

    log("=== Cone Axis XT/PK Convention Probe & Cross-Verification ===");
    log($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    log("Target Parasolid Build: v380 (transmit_version=371, schema SCH_37102)");

    // Tilted, non-world-aligned axis; ref built orthogonal to it by cross product.
    var axis = Normalize((0.3, -0.5, 0.806225774829855));
    var refDir = Normalize(Cross(axis, (0.0, 1.0, 0.0)));
    log($"Placement basis: axis=({axis.Item1}, {axis.Item2}, {axis.Item3})");
    log($"Placement basis: ref=({refDir.Item1}, {refDir.Item2}, {refDir.Item3})");

    PK_AXIS2_sf_t basis;
    basis.location = new PK_VECTOR_t(1.0, 2.0, 3.0);
    basis.axis = new PK_VECTOR1_t(axis.Item1, axis.Item2, axis.Item3);
    basis.ref_direction = new PK_VECTOR1_t(refDir.Item1, refDir.Item2, refDir.Item3);

    var body = 0;
    ParasolidScriptHost.Check(PK_BODY_create_solid_cone(0.5, 2.0, 0.25, &basis, &body), "PK_BODY_create_solid_cone");

    int faceCount;
    PK_FACE_t* faces = null;
    ParasolidScriptHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces");

    PK_CONE_sf_t origConeSf = default;
    PK_VECTOR_t origEvalM1 = default;
    PK_VECTOR_t origEval0 = default;
    PK_VECTOR_t origEval1 = default;
    var origConeFaceCount = 0;

    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            PK_SURF_t surf = 0;
            ParasolidScriptHost.Check(PK_FACE_ask_surf(faces[i], &surf), "PK_FACE_ask_surf");
            PK_CLASS_t cls = 0;
            ParasolidScriptHost.Check(PK_ENTITY_ask_class(surf, &cls), "PK_ENTITY_ask_class");
            if (cls != PK_CLASS_cone) continue;
            origConeFaceCount++;

            var sfVal = default(PK_CONE_sf_t);
            ParasolidScriptHost.Check(PK_CONE_ask(surf, &sfVal), "PK_CONE_ask");
            origConeSf = sfVal;
            log($"Face {i}: PK_CONE_ask axis = ({origConeSf.basis_set.axis.coord[0]}, {origConeSf.basis_set.axis.coord[1]}, {origConeSf.basis_set.axis.coord[2]})");
            log($"Face {i}: PK_CONE_ask ref  = ({origConeSf.basis_set.ref_direction.coord[0]}, {origConeSf.basis_set.ref_direction.coord[1]}, {origConeSf.basis_set.ref_direction.coord[2]})");
            log($"Face {i}: PK_CONE_ask loc  = ({origConeSf.basis_set.location.coord[0]}, {origConeSf.basis_set.location.coord[1]}, {origConeSf.basis_set.location.coord[2]})");
            log($"Face {i}: radius = {origConeSf.radius}, semi_angle = {origConeSf.semi_angle}");

            var evalM1 = default(PK_VECTOR_t);
            var eval0 = default(PK_VECTOR_t);
            var eval1 = default(PK_VECTOR_t);
            ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, -1.0), 0, 0, PK_LOGICAL_false, &evalM1), "PK_SURF_eval -1");
            ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, 0.0), 0, 0, PK_LOGICAL_false, &eval0), "PK_SURF_eval 0");
            ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, 1.0), 0, 0, PK_LOGICAL_false, &eval1), "PK_SURF_eval 1");
            origEvalM1 = evalM1;
            origEval0 = eval0;
            origEval1 = eval1;

            log($"Face {i}: S(0, -1) = ({origEvalM1.coord[0]}, {origEvalM1.coord[1]}, {origEvalM1.coord[2]})");
            log($"Face {i}: S(0,  0) = ({origEval0.coord[0]}, {origEval0.coord[1]}, {origEval0.coord[2]})");
            log($"Face {i}: S(0,  1) = ({origEval1.coord[0]}, {origEval1.coord[1]}, {origEval1.coord[2]})");
        }
        if (origConeFaceCount != 1)
            throw new InvalidOperationException($"Expected exactly 1 cone face in original body, found {origConeFaceCount}");
        log($"Verified {origConeFaceCount} cone face(s) in original PK body.");
    }
    finally
    {
        if (faces != null) PK_MEMORY_free(faces);
    }

    // Transmit to XT text format
    var transmitOptions = new PK_PART_transmit_o_t
    {
        o_t_version = 4,
        transmit_format = PK_transmit_format_text_c,
        transmit_version = 371,
    };
    var block = new PK_MEMORY_block_t();
    ParasolidScriptHost.Check(PK_PART_transmit_b(1, &body, &transmitOptions, &block), "PK_PART_transmit_b");
    byte[] xtBytes;
    try
    {
        xtBytes = new byte[checked((int)block.n_bytes)];
        new ReadOnlySpan<byte>(block.bytes, xtBytes.Length).CopyTo(xtBytes);
    }
    finally
    {
        PK_MEMORY_free(block.bytes);
    }

    // Save minimal XT fixture to tracked path
    var fixturePath = Path.Combine(evidenceDir, "probe_cone_v371.x_t");
    File.WriteAllBytes(fixturePath, xtBytes);
    log($"Saved minimal XT fixture ({xtBytes.Length} bytes) to {fixturePath}");

    // --- 1. Schema-Level Node Field Verification ---
    log("\n--- 1. XT Schema Node Field Inspection & Bit-Level Assertions ---");
    var text = Encoding.ASCII.GetString(xtBytes);
    var doc = XtText.DecodeDocument(text);
    var coneNode = doc.Nodes.Single(n => n.Type == (int)XtNodeTypes.Cone);

    // Schema field layout for CONE:
    // [7]=pvec, [8]=axis, [9]=radius, [10]=sin_half_angle, [11]=cos_half_angle, [12]=x_axis
    AssertNear(origConeSf.basis_set.location.coord[0], coneNode.Fields[7].Vector.X, 1e-14, "XT CONE location X");
    AssertNear(origConeSf.basis_set.location.coord[1], coneNode.Fields[7].Vector.Y, 1e-14, "XT CONE location Y");
    AssertNear(origConeSf.basis_set.location.coord[2], coneNode.Fields[7].Vector.Z, 1e-14, "XT CONE location Z");

    AssertNear(origConeSf.basis_set.axis.coord[0], coneNode.Fields[8].Vector.X, 1e-14, "XT CONE axis X");
    AssertNear(origConeSf.basis_set.axis.coord[1], coneNode.Fields[8].Vector.Y, 1e-14, "XT CONE axis Y");
    AssertNear(origConeSf.basis_set.axis.coord[2], coneNode.Fields[8].Vector.Z, 1e-14, "XT CONE axis Z");

    AssertNear(origConeSf.radius, coneNode.Fields[9].Real, 1e-14, "XT CONE radius");
    AssertNear(Math.Sin(origConeSf.semi_angle), coneNode.Fields[10].Real, 1e-14, "XT CONE sin_half_angle");
    AssertNear(Math.Cos(origConeSf.semi_angle), coneNode.Fields[11].Real, 1e-14, "XT CONE cos_half_angle");

    AssertNear(origConeSf.basis_set.ref_direction.coord[0], coneNode.Fields[12].Vector.X, 1e-14, "XT CONE ref X");
    AssertNear(origConeSf.basis_set.ref_direction.coord[1], coneNode.Fields[12].Vector.Y, 1e-14, "XT CONE ref Y");
    AssertNear(origConeSf.basis_set.ref_direction.coord[2], coneNode.Fields[12].Vector.Z, 1e-14, "XT CONE ref Z");

    log("ASSERTION PASSED: All raw XT CONE node fields match PK_CONE_ask within 1e-14 tolerance (no sign flip in XT).");

    // --- 2. Direction B: Real PK Receive Round-Trip Assertions ---
    log("\n--- 2. Direction B: Real Parasolid Receive Round-Trip Assertions ---");
    var receivedConeFaceCountB = 0;
    fixed (byte* bytes = xtBytes)
    {
        var receiveBlock = new PK_MEMORY_block_t(null, (ulong)xtBytes.Length, bytes);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int receivedCount;
        PK_PART_t* receivedParts = null;
        ParasolidScriptHost.Check(PK_PART_receive_b(receiveBlock, &receive, &receivedCount, &receivedParts), "PK_PART_receive_b");
        try
        {
            for (var p = 0; p < receivedCount; p++)
            {
                int rcFaceCount;
                PK_FACE_t* rcFaces = null;
                ParasolidScriptHost.Check(PK_BODY_ask_faces(receivedParts[p], &rcFaceCount, &rcFaces), "receive PK_BODY_ask_faces");
                try
                {
                    for (var i = 0; i < rcFaceCount; i++)
                    {
                        PK_SURF_t surf = 0;
                        ParasolidScriptHost.Check(PK_FACE_ask_surf(rcFaces[i], &surf), "receive PK_FACE_ask_surf");
                        PK_CLASS_t cls = 0;
                        ParasolidScriptHost.Check(PK_ENTITY_ask_class(surf, &cls), "receive PK_ENTITY_ask_class");
                        if (cls != PK_CLASS_cone) continue;
                        receivedConeFaceCountB++;

                        PK_CONE_sf_t sf;
                        ParasolidScriptHost.Check(PK_CONE_ask(surf, &sf), "receive PK_CONE_ask");
                        AssertNear(origConeSf.basis_set.axis.coord[0], sf.basis_set.axis.coord[0], 1e-14, "Received PK axis X");
                        AssertNear(origConeSf.basis_set.axis.coord[1], sf.basis_set.axis.coord[1], 1e-14, "Received PK axis Y");
                        AssertNear(origConeSf.basis_set.axis.coord[2], sf.basis_set.axis.coord[2], 1e-14, "Received PK axis Z");

                        AssertNear(origConeSf.basis_set.ref_direction.coord[0], sf.basis_set.ref_direction.coord[0], 1e-14, "Received PK ref X");
                        AssertNear(origConeSf.basis_set.ref_direction.coord[1], sf.basis_set.ref_direction.coord[1], 1e-14, "Received PK ref Y");
                        AssertNear(origConeSf.basis_set.ref_direction.coord[2], sf.basis_set.ref_direction.coord[2], 1e-14, "Received PK ref Z");

                        var vec = default(PK_VECTOR_t);
                        ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, 1.0), 0, 0, PK_LOGICAL_false, &vec), "receive PK_SURF_eval");
                        AssertNear(origEval1.coord[0], vec.coord[0], 1e-14, "Received PK S(0, 1) X");
                        AssertNear(origEval1.coord[1], vec.coord[1], 1e-14, "Received PK S(0, 1) Y");
                        AssertNear(origEval1.coord[2], vec.coord[2], 1e-14, "Received PK S(0, 1) Z");

                        log("ASSERTION PASSED: Real PK receive reproduces exact axis, ref, and S(0, 1) coordinates.");
                    }
                }
                finally
                {
                    if (rcFaces != null) PK_MEMORY_free(rcFaces);
                }
            }
            if (receivedConeFaceCountB != 1)
                throw new InvalidOperationException($"Direction B: Expected exactly 1 received cone face, got {receivedConeFaceCountB}");
            log($"Direction B: Verified {receivedConeFaceCountB} received cone face(s).");
        }
        finally
        {
            if (receivedParts != null) PK_MEMORY_free(receivedParts);
        }
    }

    // --- 3. Direction C: Real PK XT -> Our Kernel Materialization ---
    log("\n--- 3. Direction C: Real PK XT -> Our Kernel Materialization ---");
    KernelRuntime.SessionStop();
    var sessionOpts = new M.PK_SESSION_start_o_s { o_t_version = 1 };
    if (KernelRuntime.SessionStart(&sessionOpts) != 0)
        throw new InvalidOperationException("Failed to start our kernel session");

    try
    {
        var matStatus = KernelRuntime.TryMaterializeAnalyticSurfaceFromXt(doc, coneNode.Index, out var ourSurfTag);
        if (matStatus != AlgorithmStatus.Success)
            throw new InvalidOperationException($"TryMaterializeAnalyticSurfaceFromXt failed: {matStatus}");

        var askedCone = new M.PK_CONE_sf_s();
        if (KernelRuntime.ConeAsk(ourSurfTag, &askedCone) != 0)
            throw new InvalidOperationException("KernelRuntime.ConeAsk failed");

        AssertNear(origConeSf.basis_set.axis.coord[0], askedCone.basis_set.axis.coord[0], 1e-14, "Our materialized axis X");
        AssertNear(origConeSf.basis_set.axis.coord[1], askedCone.basis_set.axis.coord[1], 1e-14, "Our materialized axis Y");
        AssertNear(origConeSf.basis_set.axis.coord[2], askedCone.basis_set.axis.coord[2], 1e-14, "Our materialized axis Z");

        AssertNear(origConeSf.basis_set.ref_direction.coord[0], askedCone.basis_set.ref_direction.coord[0], 1e-14, "Our materialized ref X");
        AssertNear(origConeSf.basis_set.ref_direction.coord[1], askedCone.basis_set.ref_direction.coord[1], 1e-14, "Our materialized ref Y");
        AssertNear(origConeSf.basis_set.ref_direction.coord[2], askedCone.basis_set.ref_direction.coord[2], 1e-14, "Our materialized ref Z");

        AssertNear(origConeSf.radius, askedCone.radius, 1e-14, "Our materialized radius");
        AssertNear(origConeSf.semi_angle, askedCone.semi_angle, 1e-14, "Our materialized semi_angle");

        log("ASSERTION PASSED: Our kernel correctly materializes real PK XT into matching analytic surface.");
        log("Direction C: Verified 1 cone surface materialized from XT.");
    }
    finally
    {
        KernelRuntime.SessionStop();
    }

    // --- 4. Direction D: Our Kernel XT -> Real PK Receive ---
    log("\n--- 4. Direction D: Our Kernel XT -> Real PK Receive ---");
    if (KernelRuntime.SessionStart(&sessionOpts) != 0)
        throw new InvalidOperationException("Failed to start our kernel session for Direction D");

    byte[] ourXtBytes;
    try
    {
        var ourBasis = new M.PK_AXIS2_sf_s();
        ourBasis.location.coord[0] = 1.0;
        ourBasis.location.coord[1] = 2.0;
        ourBasis.location.coord[2] = 3.0;
        ourBasis.axis.coord[0] = axis.Item1;
        ourBasis.axis.coord[1] = axis.Item2;
        ourBasis.axis.coord[2] = axis.Item3;
        ourBasis.ref_direction.coord[0] = refDir.Item1;
        ourBasis.ref_direction.coord[1] = refDir.Item2;
        ourBasis.ref_direction.coord[2] = refDir.Item3;

        int ourBody = 0;
        if (KernelRuntime.BodyCreateSolidCone(0.5, 2.0, 0.25, &ourBasis, &ourBody) != 0)
            throw new InvalidOperationException("BodyCreateSolidCone failed in our kernel");

        var parts = stackalloc int[1] { ourBody };
        var transmitOptionsOur = new M.PK_PART_transmit_o_s
        {
            o_t_version = 4,
            transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371,
            transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
        };
        var memBlock = new M.PK_MEMORY_block_s();
        if (KernelRuntime.PartTransmitB(1, parts, &transmitOptionsOur, &memBlock) != 0)
            throw new InvalidOperationException("KernelRuntime.PartTransmitB failed");

        try
        {
            ourXtBytes = new byte[checked((int)memBlock.n_bytes)];
            new ReadOnlySpan<byte>(memBlock.bytes, ourXtBytes.Length).CopyTo(ourXtBytes);
        }
        finally
        {
            KernelRuntime.MemoryBlockFree(&memBlock);
        }
    }
    finally
    {
        KernelRuntime.SessionStop();
    }

    log($"Our kernel transmitted cone XT ({ourXtBytes.Length} bytes); passing to real PK_PART_receive_b...");
    var receivedConeFaceCountD = 0;
    fixed (byte* bytes = ourXtBytes)
    {
        var receiveBlock = new PK_MEMORY_block_t(null, (ulong)ourXtBytes.Length, bytes);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int receivedCount;
        PK_PART_t* receivedParts = null;
        ParasolidScriptHost.Check(PK_PART_receive_b(receiveBlock, &receive, &receivedCount, &receivedParts), "Real PK receive our XT");
        try
        {
            for (var p = 0; p < receivedCount; p++)
            {
                int rcFaceCount;
                PK_FACE_t* rcFaces = null;
                ParasolidScriptHost.Check(PK_BODY_ask_faces(receivedParts[p], &rcFaceCount, &rcFaces), "receive our XT PK_BODY_ask_faces");
                try
                {
                    for (var i = 0; i < rcFaceCount; i++)
                    {
                        PK_SURF_t surf = 0;
                        ParasolidScriptHost.Check(PK_FACE_ask_surf(rcFaces[i], &surf), "receive our XT PK_FACE_ask_surf");
                        PK_CLASS_t cls = 0;
                        ParasolidScriptHost.Check(PK_ENTITY_ask_class(surf, &cls), "receive our XT PK_ENTITY_ask_class");
                        if (cls != PK_CLASS_cone) continue;
                        receivedConeFaceCountD++;

                        PK_CONE_sf_t sf;
                        ParasolidScriptHost.Check(PK_CONE_ask(surf, &sf), "receive our XT PK_CONE_ask");
                        AssertNear(axis.Item1, sf.basis_set.axis.coord[0], 1e-14, "Real PK received our axis X");
                        AssertNear(axis.Item2, sf.basis_set.axis.coord[1], 1e-14, "Real PK received our axis Y");
                        AssertNear(axis.Item3, sf.basis_set.axis.coord[2], 1e-14, "Real PK received our axis Z");

                        AssertNear(refDir.Item1, sf.basis_set.ref_direction.coord[0], 1e-14, "Real PK received our ref X");
                        AssertNear(refDir.Item2, sf.basis_set.ref_direction.coord[1], 1e-14, "Real PK received our ref Y");
                        AssertNear(refDir.Item3, sf.basis_set.ref_direction.coord[2], 1e-14, "Real PK received our ref Z");

                        var vec = default(PK_VECTOR_t);
                        ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, 1.0), 0, 0, PK_LOGICAL_false, &vec), "receive our XT PK_SURF_eval");
                        AssertNear(origEval1.coord[0], vec.coord[0], 1e-14, "Real PK received our S(0, 1) X");
                        AssertNear(origEval1.coord[1], vec.coord[1], 1e-14, "Real PK received our S(0, 1) Y");
                        AssertNear(origEval1.coord[2], vec.coord[2], 1e-14, "Real PK received our S(0, 1) Z");

                        log("ASSERTION PASSED: Real Parasolid successfully received our XT cone and matched axis/ref/eval.");
                    }
                }
                finally
                {
                    if (rcFaces != null) PK_MEMORY_free(rcFaces);
                }
            }
            if (receivedConeFaceCountD != 1)
                throw new InvalidOperationException($"Direction D: Expected exactly 1 received cone face, got {receivedConeFaceCountD}");
            log($"Direction D: Verified {receivedConeFaceCountD} received cone face(s).");
        }
        finally
        {
            if (receivedParts != null) PK_MEMORY_free(receivedParts);
        }
    }

    log("\n=== ALL ASSERTIONS COMPLETED SUCCESSFULLY ===");
}

static void AssertNear(double expected, double actual, double tol, string context)
{
    if (!double.IsFinite(expected) || !double.IsFinite(actual) || !double.IsFinite(tol) || tol < 0)
        throw new InvalidOperationException($"Assertion argument invalid for {context}: expected={expected}, actual={actual}, tol={tol}");
    var diff = Math.Abs(expected - actual);
    if (!double.IsFinite(diff) || diff > tol)
        throw new InvalidOperationException($"Assertion failed for {context}: expected {expected:R}, got {actual:R}, diff {diff:E} > {tol:E}");
}

static void SelfTestAssertNear()
{
    AssertThrows(() => AssertNear(1.0, double.NaN, 1e-14, "self-test actual NaN"));
    AssertThrows(() => AssertNear(double.NaN, 1.0, 1e-14, "self-test expected NaN"));
    AssertThrows(() => AssertNear(1.0, double.PositiveInfinity, 1e-14, "self-test actual Inf"));
    AssertThrows(() => AssertNear(double.NegativeInfinity, 1.0, 1e-14, "self-test expected -Inf"));
    AssertThrows(() => AssertNear(1.0, 1.0, double.NaN, "self-test tol NaN"));
    AssertThrows(() => AssertNear(1.0, 1.0, -1e-14, "self-test negative tol"));
    AssertThrows(() => AssertNear(1.0, 2.0, 1e-14, "self-test mismatch"));
    AssertNear(1.0, 1.0 + 1e-15, 1e-14, "self-test match");
}

static void AssertThrows(Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException("AssertThrows failed: expected InvalidOperationException was not thrown.");
}

static (double, double, double) Normalize((double, double, double) v)
{
    var n = Math.Sqrt(v.Item1 * v.Item1 + v.Item2 * v.Item2 + v.Item3 * v.Item3);
    return (v.Item1 / n, v.Item2 / n, v.Item3 / n);
}

static (double, double, double) Cross((double, double, double) a, (double, double, double) b)
    => (a.Item2 * b.Item3 - a.Item3 * b.Item2, a.Item3 * b.Item1 - a.Item1 * b.Item3, a.Item1 * b.Item2 - a.Item2 * b.Item1);

static string GetScriptPath([CallerFilePath] string path = "") => path;
