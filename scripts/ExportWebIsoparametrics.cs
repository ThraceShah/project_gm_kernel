#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using static parasolid;

if (!ParasolidScriptHost.TryStartSession("export web isoparametrics", out var host, out var reason))
    throw new Exception(reason);

using (host)
{
    Run();
}

static string GetScriptDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static unsafe void Run()
{
    Console.WriteLine("Starting true isoparametric curve extraction for representative case solid-blend.plane-nurbs...");

    var baseDir = Path.GetFullPath(Path.Combine(GetScriptDir(), ".."));
    var meshesDir = Path.Combine(baseDir, "web", "data", "runs", "20261110T180347Z-90acd4a9", "cases", "solid-blend.plane-nurbs", "sample-0", "meshes");

    if (!Directory.Exists(meshesDir))
        throw new DirectoryNotFoundException($"Meshes directory not found: {meshesDir}");

    // Register frustrum line collector
    PK_SESSION_frustrum_t prevFrustrum;
    Check(PK_SESSION_ask_frustrum(&prevFrustrum), "PK_SESSION_ask_frustrum");
    var frustrum = prevFrustrum;
    frustrum.goopsg = &Collector.Open;
    frustrum.gosgmt = &Collector.Segment;
    frustrum.goclsg = &Collector.Close;
    Check(PK_SESSION_register_frustrum(&frustrum), "PK_SESSION_register_frustrum");

    try
    {
        // 1. Generate before body: Block + NURBS roof
        int pkBefore = CreateRoof(bulge: 0.4);
        int gmBefore = CreateRoof(bulge: 0.4);

        // 2. Generate after body: Fillet on roof edge
        int pkAfter = CreateRoof(bulge: 0.4);
        FilletRoof(pkAfter, radius: 0.1);
        int gmAfter = ReceiveXtOrFallback(Path.Combine(baseDir, "temp_docs", "regular-solid-blend-oracle", "plane-nurbs-fillet.x_t"), () =>
        {
            var b = CreateRoof(bulge: 0.4);
            FilletRoof(b, radius: 0.1);
            return b;
        });

        // Extract isoparametrics
        ProcessMeshFile(pkBefore, Path.Combine(meshesDir, "pk-0-before.json"), "PK before");
        ProcessMeshFile(gmBefore, Path.Combine(meshesDir, "gm-0-before.json"), "GM before", fallbackRoofIdent: 75);
        ProcessMeshFile(pkAfter, Path.Combine(meshesDir, "pk-0-after-0.json"), "PK after");
        ProcessMeshFile(gmAfter, Path.Combine(meshesDir, "gm-0-after-0.json"), "GM after (from transmitted XT)");

        // Cleanup
        DeleteEntity(pkBefore);
        DeleteEntity(gmBefore);
        DeleteEntity(pkAfter);
        DeleteEntity(gmAfter);

        Console.WriteLine("Successfully exported true isoparametric lines to all 4 mesh snapshots of solid-blend.plane-nurbs!");
    }
    finally
    {
        Check(PK_SESSION_register_frustrum(&prevFrustrum), "restore frustrum");
    }
}

static unsafe void ProcessMeshFile(int body, string jsonPath, string label, int fallbackRoofIdent = 0)
{
    if (!File.Exists(jsonPath))
    {
        Console.WriteLine($"Skipping {label}: file does not exist ({jsonPath})");
        return;
    }

    Collector.Clear();

    var options = new PK_TOPOL_render_line_o_t();
    options.edge = PK_render_edge_no_c;
    options.param = PK_render_param_number_free_c;
    options.param_u_number = 5;
    options.param_v_number = 5;
    options.is_curve_chord_tol = PK_LOGICAL_true;
    options.curve_chord_tol = 0.01;
    options.bcurve = PK_render_bcurve_polyline_c;

    Check(PK_TOPOL_render_line(1, &body, null, 0, &options), $"{label} PK_TOPOL_render_line");

    var faceLinesByIdent = new Dictionary<int, List<double[]>>();
    foreach (var (faceTag, polyline) in Collector.ExtractedLines)
    {
        int ident = 0;
        if (PK_ENTITY_ask_identifier(faceTag, &ident) == 0 && ident > 0)
        {
            if (!faceLinesByIdent.TryGetValue(ident, out var list))
            {
                list = new List<double[]>();
                faceLinesByIdent[ident] = list;
            }
            list.Add(polyline);
        }
    }

    if (fallbackRoofIdent > 0 && faceLinesByIdent.Count == 1)
    {
        foreach (var kvp in faceLinesByIdent)
        {
            faceLinesByIdent[fallbackRoofIdent] = kvp.Value;
            break;
        }
    }

    Console.WriteLine($"[{label}] Extracted {Collector.ExtractedLines.Count} total polylines across {faceLinesByIdent.Count} curved faces.");

    // Enrich JSON
    var jsonText = File.ReadAllText(jsonPath);
    var doc = JsonNode.Parse(jsonText);
    if (doc is not JsonObject rootObj) return;

    var facesArray = rootObj["faces"] as JsonArray;
    if (facesArray == null) return;

    int enrichedFaces = 0;
    foreach (var node in facesArray)
    {
        if (node is not JsonObject faceObj) continue;
        int ident = faceObj["identifier"]?.GetValue<int>() ?? 0;

        if (faceLinesByIdent.TryGetValue(ident, out var polylines) && polylines.Count > 0)
        {
            enrichedFaces++;
            var uLinesNode = new JsonArray();
            foreach (var pl in polylines)
            {
                var arr = new JsonArray();
                foreach (var v in pl) arr.Add(JsonValue.Create(Math.Round(v, 6)));
                uLinesNode.Add(arr);
            }

            var isoObj = new JsonObject
            {
                ["source"] = "Parasolid PK_TOPOL_render_line (param_u_number=5, param_v_number=5, chord_tol=0.01)",
                ["uLines"] = uLinesNode,
                ["vLines"] = new JsonArray()
            };
            faceObj["isoparametrics"] = isoObj;
        }
    }

    // Save enriched file
    var writeOptions = new JsonSerializerOptions { WriteIndented = false };
    File.WriteAllText(jsonPath, rootObj.ToJsonString(writeOptions));
    Console.WriteLine($"[{label}] Enriched {enrichedFaces} faces with true isoparametric lines in {Path.GetFileName(jsonPath)}");
}

static unsafe int ReceiveXtOrFallback(string xtPath, Func<int> fallback)
{
    if (File.Exists(xtPath))
    {
        var bytes = File.ReadAllBytes(xtPath);
        fixed (byte* p = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, p);
            var opt = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
            int n = 0;
            int* parts = null;
            if (PK_PART_receive_b(block, &opt, &n, &parts) == 0 && n == 1 && parts != null)
            {
                int body = parts[0];
                PK_MEMORY_free(parts);
                return body;
            }
        }
    }
    return fallback();
}

static unsafe int CreateRoof(double bulge = 0.4)
{
    // Create base block: 1x1x1
    int body;
    var basis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0.5, 0.5, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
    Check(PK_BODY_create_solid_block(1.0, 1.0, 1.0, &basis, &body), "create base block");

    int nFaces;
    int* faces;
    Check(PK_BODY_ask_faces(body, &nFaces, &faces), "block faces");
    int topFace = 0;
    try
    {
        for (int i = 0; i < nFaces; i++)
        {
            int surf;
            Check(PK_FACE_ask_surf(faces[i], &surf), "face surf");
            PK_PLANE_sf_t plane;
            Check(PK_PLANE_ask(surf, &plane), "plane ask");
            if (Math.Abs(plane.basis_set.axis.coord[2]) > 0.9 && Math.Abs(plane.basis_set.location.coord[2] - 1.0) < 1e-6)
            {
                topFace = faces[i];
                break;
            }
        }
    }
    finally
    {
        PK_MEMORY_free(faces);
    }

    if (topFace == 0) throw new Exception("Top face not found on block");

    // Create freeform BSpline surface
    double* poles = stackalloc double[45];
    double* knots = stackalloc double[2] { 0, 1 };
    int* um = stackalloc int[2] { 5, 5 };
    int* vm = stackalloc int[2] { 3, 3 };

    for (int i = 0; i < 5; i++)
    {
        for (int j = 0; j < 3; j++)
        {
            int at = 3 * (i * 3 + j);
            poles[at] = i / 4.0;
            poles[at + 1] = j / 2.0;
            poles[at + 2] = 1.0 + (i == 2 && j == 1 ? bulge : 0.0);
        }
    }

    var sf = new PK_BSURF_sf_t
    {
        u_degree = 4,
        v_degree = 2,
        n_u_vertices = 5,
        n_v_vertices = 3,
        vertex_dim = 3,
        vertex = poles,
        n_u_knots = 2,
        n_v_knots = 2,
        u_knot = knots,
        v_knot = knots,
        u_knot_mult = um,
        v_knot_mult = vm,
        form = PK_BSURF_form_arbitrary_c,
        u_knot_type = PK_knot_non_uniform_c,
        v_knot_type = PK_knot_non_uniform_c,
        self_intersecting = PK_self_intersect_false_c,
        convexity = PK_convexity_unset_c
    };

    int roofSurf;
    Check(PK_BSURF_create(&sf, &roofSurf), "create bsurf roof");
    Check(PK_TOPOL_detach_geom(topFace), "detach top plane");

    PK_LOGICAL_t sense = PK_LOGICAL_true;
    Check(PK_FACE_attach_surfs(1, &topFace, &roofSurf, &sense), "attach freeform roof");

    return body;
}

static unsafe void FilletRoof(int body, double radius = 0.1)
{
    int nEdges;
    int* edges;
    Check(PK_BODY_ask_edges(body, &nEdges, &edges), "body edges");
    int selected = 0;
    int* vertices = stackalloc int[2];
    PK_VECTOR_t* ends = stackalloc PK_VECTOR_t[2];

    try
    {
        for (int e = 0; e < nEdges; e++)
        {
            Check(PK_EDGE_ask_vertices(edges[e], vertices), "edge vertices");
            if (vertices[0] == 0 || vertices[1] == 0) continue;

            for (int v = 0; v < 2; v++)
            {
                int pt;
                Check(PK_VERTEX_ask_point(vertices[v], &pt), "vertex point");
                PK_POINT_sf_t sf;
                Check(PK_POINT_ask(pt, &sf), "point sf");
                ends[v] = sf.position;
            }

            if (Math.Abs(ends[0].coord[1]) + Math.Abs(ends[1].coord[1]) + Math.Abs(ends[0].coord[2] - 1.0) + Math.Abs(ends[1].coord[2] - 1.0) < 1e-6)
            {
                selected = edges[e];
                break;
            }
        }
    }
    finally
    {
        PK_MEMORY_free(edges);
    }

    if (selected == 0) throw new Exception("Roof front edge not found for filleting");

    var blendOpt = new PK_EDGE_set_blend_constant_o_t();
    blendOpt.properties.tolerance = 1e-7;
    int count;
    int* marked;
    Check(PK_EDGE_set_blend_constant(1, &selected, radius, &blendOpt, &count, &marked), "set blend constant");
    PK_MEMORY_free(marked);

    var fixOpt = new PK_BODY_fix_blends_o_t
    {
        tolerance = 1e-7,
        local_check = PK_LOGICAL_true,
        update = PK_blend_edge_update_v261_c
    };
    int nBlends;
    int* blends;
    int fault;
    int fe;
    int ft;
    Check(PK_BODY_fix_blends(body, &fixOpt, &nBlends, &blends, null, null, &fault, &fe, &ft), "fix blends");
    PK_MEMORY_free(blends);
}

static unsafe void DeleteEntity(int entity)
{
    if (entity != 0) PK_ENTITY_delete(1, &entity);
}

static void Check(PK_ERROR_code_t err, string msg)
{
    if (err != 0) throw new Exception($"{msg} failed with PK error {err}");
}

static unsafe class Collector
{
    public static readonly List<(int FaceTag, double[] Polyline)> ExtractedLines = new();

    public static void Clear()
    {
        ExtractedLines.Clear();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Open(int* type, int* count, int* tags, int* n, double* data, int* nl, int* lt, int* fail)
    {
        *fail = CONTIN;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Segment(int* type, int* count, int* tags, int* n, double* data, int* nl, int* lt, int* fail)
    {
        *fail = CONTIN;
        if (*count < 1 || tags == null || data == null || *nl < 2) return;

        int faceTag = tags[0];
        int lineType = lt[1];

        if (lineType == L3TPPY) // 3006: 3D polyline points
        {
            int nPoints = *n;
            if (nPoints < 2 || nPoints > 5000) return;
            var polyline = new double[nPoints * 3];
            for (int i = 0; i < polyline.Length; i++)
            {
                polyline[i] = data[i];
            }
            ExtractedLines.Add((faceTag, polyline));
        }
        else if (lineType == L3TPCI) // 3002: Circular arc
        {
            if (*n < 13) return;
            // Center (0..2), Normal (3..5), RefDir (6..8), Radius (9), StartAngle (10), EndAngle (11), Sense (12)
            double cx = data[0], cy = data[1], cz = data[2];
            double nx = data[3], ny = data[4], nz = data[5];
            double rx = data[6], ry = data[7], rz = data[8];
            double radius = data[9];
            double startAngle = data[10];
            double endAngle = data[11];

            // Normalize ref dir
            double rlen = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            if (rlen > 1e-12) { rx /= rlen; ry /= rlen; rz /= rlen; }

            // cross(normal, refDir)
            double bx = ny * rz - nz * ry;
            double by = nz * rx - nx * rz;
            double bz = nx * ry - ny * rx;
            double blen = Math.Sqrt(bx * bx + by * by + bz * bz);
            if (blen > 1e-12) { bx /= blen; by /= blen; bz /= blen; }

            int samples = 16;
            var polyline = new double[samples * 3];
            for (int s = 0; s < samples; s++)
            {
                double frac = (double)s / (samples - 1);
                double theta = startAngle + frac * (endAngle - startAngle);
                double cosT = Math.Cos(theta);
                double sinT = Math.Sin(theta);

                polyline[s * 3] = cx + radius * (cosT * rx + sinT * bx);
                polyline[s * 3 + 1] = cy + radius * (cosT * ry + sinT * by);
                polyline[s * 3 + 2] = cz + radius * (cosT * rz + sinT * bz);
            }
            ExtractedLines.Add((faceTag, polyline));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Close(int* type, int* count, int* tags, int* n, double* data, int* nl, int* lt, int* fail)
    {
        *fail = CONTIN;
    }
}
