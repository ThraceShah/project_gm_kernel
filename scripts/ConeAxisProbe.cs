#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=ConeAxisProbe
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// Cone axis XT/PK convention probe (spec §8.3).
// Direction A: real Parasolid creates a tilted solid cone; PK_CONE_ask of the
// conical face surface reports the PK-interface convention; PK_PART_transmit_b
// then writes the same surface into text XT, whose raw CONE node fields report
// the stored XT convention. PK_SURF_eval at v = ±1 identifies which half the
// stored axis points away from. Output goes to stdout only.
// Usage: PARASOLID_LIBRARY=<libpskernel.so> dotnet run scripts/ConeAxisProbe.cs

using System.Text;
using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Cone axis probe", out var session, out var message))
{
    Console.WriteLine("NotRun: " + message);
    return;
}
using var _ = session;
RunProbe();

static unsafe void RunProbe()
{
    // Tilted, non-world-aligned axis; ref built orthogonal to it by cross product.
    var axis = Normalize((0.3, -0.5, 0.806225774829855));
    var refDir = Normalize(Cross(axis, (0.0, 1.0, 0.0)));
    Console.WriteLine($"placement axis = {axis}");
    Console.WriteLine($"placement ref   = {refDir}");

    PK_AXIS2_sf_t basis;
    basis.location = new PK_VECTOR_t(1.0, 2.0, 3.0);
    basis.axis = new PK_VECTOR1_t(axis.Item1, axis.Item2, axis.Item3);
    basis.ref_direction = new PK_VECTOR1_t(refDir.Item1, refDir.Item2, refDir.Item3);

    var body = 0;
    ParasolidScriptHost.Check(PK_BODY_create_solid_cone(0.5, 2.0, 0.25, &basis, &body), "PK_BODY_create_solid_cone");

    int faceCount;
    PK_FACE_t* faces = null;
    ParasolidScriptHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces");
    try
    {
        Console.WriteLine($"face count = {faceCount}");
        for (var i = 0; i < faceCount; i++)
        {
            PK_SURF_t surf = 0;
            ParasolidScriptHost.Check(PK_FACE_ask_surf(faces[i], &surf), "PK_FACE_ask_surf");
            PK_CLASS_t cls = 0;
            ParasolidScriptHost.Check(PK_ENTITY_ask_class(surf, &cls), "PK_ENTITY_ask_class");
            if (cls != PK_CLASS_cone) continue;

            PK_CONE_sf_t sf;
            ParasolidScriptHost.Check(PK_CONE_ask(surf, &sf), "PK_CONE_ask");
            Console.WriteLine($"face {i}: PK_CONE_ask axis = ({sf.basis_set.axis.coord[0]}, {sf.basis_set.axis.coord[1]}, {sf.basis_set.axis.coord[2]})");
            Console.WriteLine($"face {i}: PK_CONE_ask ref  = ({sf.basis_set.ref_direction.coord[0]}, {sf.basis_set.ref_direction.coord[1]}, {sf.basis_set.ref_direction.coord[2]})");
            Console.WriteLine($"face {i}: PK_CONE_ask loc  = ({sf.basis_set.location.coord[0]}, {sf.basis_set.location.coord[1]}, {sf.basis_set.location.coord[2]})");
            Console.WriteLine($"face {i}: radius = {sf.radius}, semi_angle = {sf.semi_angle}");

            // Which half does the surface parameterization cover?
            for (var v = -1.0; v <= 1.0; v += 1.0)
            {
                var vec = default(PK_VECTOR_t);
                ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, v), 0, 0, PK_LOGICAL_false, &vec), "PK_SURF_eval");
                Console.WriteLine($"face {i}: S(0, {v}) = ({vec.coord[0]}, {vec.coord[1]}, {vec.coord[2]})");
            }
        }
    }
    finally
    {
        if (faces != null) PK_MEMORY_free(faces);
    }

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

    // Direction B: receive the exact bytes back and ask the cone surface again.
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
                        PK_CONE_sf_t sf;
                        ParasolidScriptHost.Check(PK_CONE_ask(surf, &sf), "receive PK_CONE_ask");
                        Console.WriteLine($"received face {i}: axis = ({sf.basis_set.axis.coord[0]}, {sf.basis_set.axis.coord[1]}, {sf.basis_set.axis.coord[2]})");
                        Console.WriteLine($"received face {i}: ref  = ({sf.basis_set.ref_direction.coord[0]}, {sf.basis_set.ref_direction.coord[1]}, {sf.basis_set.ref_direction.coord[2]})");
                        var vec = default(PK_VECTOR_t);
                        ParasolidScriptHost.Check(PK_SURF_eval(surf, new PK_UV_t(0.0, 1.0), 0, 0, PK_LOGICAL_false, &vec), "receive PK_SURF_eval");
                        Console.WriteLine($"received face {i}: S(0, 1) = ({vec.coord[0]}, {vec.coord[1]}, {vec.coord[2]})");
                    }
                }
                finally
                {
                    if (rcFaces != null) PK_MEMORY_free(rcFaces);
                }
            }
        }
        finally
        {
            if (receivedParts != null) PK_MEMORY_free(receivedParts);
        }
    }

    var text = Encoding.ASCII.GetString(xtBytes);
    foreach (var line in text.Split('\n'))
    {
        if (line.StartsWith("52 ") || line.Contains(" 52 "))
            Console.WriteLine("XT CONE record: " + line.TrimEnd('\r'));
    }
}

static (double, double, double) Normalize((double, double, double) v)
{
    var n = Math.Sqrt(v.Item1 * v.Item1 + v.Item2 * v.Item2 + v.Item3 * v.Item3);
    return (v.Item1 / n, v.Item2 / n, v.Item3 / n);
}

static (double, double, double) Cross((double, double, double) a, (double, double, double) b)
    => (a.Item2 * b.Item3 - a.Item3 * b.Item2, a.Item3 * b.Item1 - a.Item1 * b.Item3, a.Item1 * b.Item2 - a.Item2 * b.Item1);
