#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Parasolid primitive counts", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        Print("block", CreateBlock());
        Print("cyl", CreateCyl());
        Print("cone", CreateCone());
        Print("cone0", CreateCone0());
        Print("prism", CreatePrism());
        Print("sphere", CreateSphere());
        Print("torus", CreateTorus());
    }
}

return 0;

static unsafe PK_BODY_t CreateBlock()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_block(1, 2, 3, null, &body), "PK_BODY_create_solid_block");
    return body;
}

static unsafe PK_BODY_t CreateCyl()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_cyl(2, 5, null, &body), "PK_BODY_create_solid_cyl");
    return body;
}

static unsafe PK_BODY_t CreateCone()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_cone(1, 5, 0.25, null, &body), "PK_BODY_create_solid_cone");
    return body;
}

static unsafe PK_BODY_t CreateCone0()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_cone(0, 5, 0.25, null, &body), "PK_BODY_create_solid_cone(radius=0)");
    return body;
}

static unsafe PK_BODY_t CreatePrism()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_prism(2, 5, 5, null, &body), "PK_BODY_create_solid_prism");
    return body;
}

static unsafe PK_BODY_t CreateSphere()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_sphere(2, null, &body), "PK_BODY_create_solid_sphere");
    return body;
}

static unsafe PK_BODY_t CreateTorus()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_torus(5, 1, null, &body), "PK_BODY_create_solid_torus");
    return body;
}

static unsafe void Print(string label, PK_BODY_t body)
{
    Console.Write(label);
    Console.Write(' ');
    Console.Write(CountRegions(body));
    Console.Write(' ');
    Console.Write(CountShells(body));
    Console.Write(' ');
    Console.Write(CountFaces(body));
    Console.Write(' ');
    Console.Write(CountLoops(body));
    Console.Write(' ');
    Console.Write(CountFins(body));
    Console.Write(' ');
    Console.Write(CountEdges(body));
    Console.Write(' ');
    Console.WriteLine(CountVertices(body));
}

static unsafe int CountRegions(PK_BODY_t body)
{
    int count;
    PK_REGION_t* values;
    Check(PK_BODY_ask_regions(body, &count, &values), "PK_BODY_ask_regions");
    Free(values);
    return count;
}

static unsafe int CountShells(PK_BODY_t body)
{
    int count;
    PK_SHELL_t* values;
    Check(PK_BODY_ask_shells(body, &count, &values), "PK_BODY_ask_shells");
    Free(values);
    return count;
}

static unsafe int CountFaces(PK_BODY_t body)
{
    int count;
    PK_FACE_t* values;
    Check(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces");
    Free(values);
    return count;
}

static unsafe int CountEdges(PK_BODY_t body)
{
    int count;
    PK_EDGE_t* values;
    Check(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges");
    Free(values);
    return count;
}

static unsafe int CountLoops(PK_BODY_t body)
{
    int faceCount;
    PK_FACE_t* faces;
    Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces");
    try
    {
        var total = 0;
        for (int i = 0; i < faceCount; i++)
        {
            int loopCount;
            PK_LOOP_t* loops;
            Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops");
            total += loopCount;
            Free(loops);
        }
        return total;
    }
    finally
    {
        Free(faces);
    }
}

static unsafe int CountFins(PK_BODY_t body)
{
    int faceCount;
    PK_FACE_t* faces;
    Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces");
    try
    {
        var total = 0;
        for (int i = 0; i < faceCount; i++)
        {
            int loopCount;
            PK_LOOP_t* loops;
            Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops");
            try
            {
                for (int j = 0; j < loopCount; j++)
                {
                    int finCount;
                    PK_FIN_t* fins;
                    Check(PK_LOOP_ask_fins(loops[j], &finCount, &fins), "PK_LOOP_ask_fins");
                    total += finCount;
                    Free(fins);
                }
            }
            finally
            {
                Free(loops);
            }
        }
        return total;
    }
    finally
    {
        Free(faces);
    }
}

static unsafe int CountVertices(PK_BODY_t body)
{
    int count;
    PK_VERTEX_t* values;
    Check(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices");
    Free(values);
    return count;
}

static unsafe void Free<T>(T* values)
    where T : unmanaged
{
    if (values is not null)
        Check(PK_MEMORY_free(values), "PK_MEMORY_free");
}

static void Check(PK_ERROR_code_t error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}
