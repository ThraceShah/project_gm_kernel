using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Regression tests for Phase 2 correctness fixes.
/// </summary>
[Collection("KernelTests")]
public unsafe class KernelRegressionTests : IDisposable
{
    public KernelRegressionTests()
    {
        // Stop any existing session first
        KernelRuntime.SessionStop();

        // Start a fresh session before each test
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose()
    {
        KernelRuntime.SessionStop();
    }

    // ── Test 1: Repeated query does not increase handle count ─────

    [Fact]
    public void RepeatedQuery_DoesNotIncreaseHandleCount()
    {
        // Create a point
        int pointTag;
        var sf = new PK_POINT_sf_s();
        sf.position.coord[0] = 1.0;
        sf.position.coord[1] = 2.0;
        sf.position.coord[2] = 3.0;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &pointTag));

        // Create a body with shell, face, loop, fin, edge, vertex
        int bodyTag = CreateSimpleBody(out _, out _, out _, out _, out _, out _);

        // Record handle count before queries
        int handleCountBefore = GetNextTag();

        // Query shells multiple times
        int nShells;
        int* shells;
        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
        Assert.True(nShells > 0);

        // Query again
        int nShells2;
        int* shells2;
        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells2, &shells2));
        Assert.Equal(nShells, nShells2);

        // Handle count should not increase for stable tags
        int handleCountAfter = GetNextTag();
        Assert.Equal(handleCountBefore, handleCountAfter);
    }

    // ── Test 2: Delete + Mark/Goto preserves alive slots ──────────

    [Fact]
    public void DeleteMarkGoto_PreservesAliveSlots()
    {
        // Create two points
        int pointTag1, pointTag2;
        var sf1 = new PK_POINT_sf_s();
        sf1.position.coord[0] = 1.0;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf1, &pointTag1));

        var sf2 = new PK_POINT_sf_s();
        sf2.position.coord[0] = 2.0;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf2, &pointTag2));

        // Create mark
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));

        // Delete point 2
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &pointTag2));

        // Point 2 should be dead
        int cls;
        Assert.NotEqual(0, KernelRuntime.EntityAskClass(pointTag2, &cls));

        // Goto mark — point 2 should be restored
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));

        // Point 2 should be alive again
        Assert.Equal(0, KernelRuntime.EntityAskClass(pointTag2, &cls));
        Assert.Equal((int)EntityClass.Point, cls);

        // Allocate a new point — should NOT reuse point 2's slot
        int pointTag3;
        var sf3 = new PK_POINT_sf_s();
        sf3.position.coord[0] = 3.0;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf3, &pointTag3));

        // Point 2 should still be alive (not reused)
        Assert.Equal(0, KernelRuntime.EntityAskClass(pointTag2, &cls));
        Assert.Equal((int)EntityClass.Point, cls);
    }

    // ── Test 3: FIN_ask_face returns correct face ─────────────────

    [Fact]
    public void FinAskFace_ReturnsCorrectFace()
    {
        int bodyTag = CreateSimpleBody(out int shellTag, out int faceTag, out int loopTag, out int finTag, out int edgeTag, out int vertexTag);

        // Ask fin for its face
        int faceFromFin;
        Assert.Equal(0, KernelRuntime.FinAskFace(finTag, &faceFromFin));

        // The face should match the one we created
        Assert.Equal(faceTag, faceFromFin);
    }

    // ── Test 4: BODY_ask_shells / FACE_ask_loops return valid results ──

    [Fact]
    public void BodyAskShells_ReturnsValidResults()
    {
        int bodyTag = CreateSimpleBody(out int shellTag, out int faceTag, out int loopTag, out int finTag, out int edgeTag, out int vertexTag);

        // Ask body for shells
        int nShells;
        int* shells;
        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
        Assert.Equal(1, nShells);
        Assert.Equal(shellTag, shells[0]);
    }

    [Fact]
    public void FaceAskLoops_ReturnsValidResults()
    {
        int bodyTag = CreateSimpleBody(out int shellTag, out int faceTag, out int loopTag, out int finTag, out int edgeTag, out int vertexTag);

        // Ask face for loops
        int nLoops;
        int* loops;
        Assert.Equal(0, KernelRuntime.FaceAskLoops(faceTag, &nLoops, &loops));
        Assert.Equal(1, nLoops);
        Assert.Equal(loopTag, loops[0]);
    }

    // ── Test 5: EdgeAskFins traverses NextOfEdge chain ────────────

    [Fact]
    public void EdgeAskFins_ReturnsAllEdgeFinsInEdgeChain()
    {
        // Create a body with: 1 shell, 1 face, 1 loop, 2 fins, 1 edge, 1 vertex
        // Topologies: [0]=shell [1]=face [2]=loop [3]=fin0 [4]=fin1 [5]=edge [6]=vertex
        int nTopols = 7;
        var classes = stackalloc int[nTopols];
        classes[0] = ParasolidConstants.PK_CLASS_shell;
        classes[1] = ParasolidConstants.PK_CLASS_face;
        classes[2] = ParasolidConstants.PK_CLASS_loop;
        classes[3] = ParasolidConstants.PK_CLASS_fin;
        classes[4] = ParasolidConstants.PK_CLASS_fin;
        classes[5] = ParasolidConstants.PK_CLASS_edge;
        classes[6] = ParasolidConstants.PK_CLASS_vertex;

        // Relations: body→shell, shell→face, face→loop,
        //            loop→fin0, loop→fin1, edge→fin0, edge→fin1, fin0→edge, fin1→edge
        int nRelations = 9;
        var parents = stackalloc int[nRelations];
        var children = stackalloc int[nRelations];
        var senses = stackalloc int[nRelations];

        parents[0] = -1; children[0] = 0; senses[0] = 0;  // body → shell
        parents[1] = 0;  children[1] = 1; senses[1] = 0;  // shell → face
        parents[2] = 1;  children[2] = 2; senses[2] = 0;  // face → loop
        parents[3] = 2;  children[3] = 3; senses[3] = 0;  // loop → fin0
        parents[4] = 2;  children[4] = 4; senses[4] = 0;  // loop → fin1
        parents[5] = 5;  children[5] = 3; senses[5] = 0;  // edge → fin0
        parents[6] = 5;  children[6] = 4; senses[6] = 0;  // edge → fin1
        parents[7] = 3;  children[7] = 5; senses[7] = 0;  // fin0 → edge
        parents[8] = 4;  children[8] = 5; senses[8] = 0;  // fin1 → edge

        var options = new PK_BODY_create_topology_2_o_s();
        var results = new PK_BODY_create_topology_2_r_s();
        Assert.Equal(0, KernelRuntime.BodyCreateTopology2(
            nTopols, classes, nRelations, parents, children, senses, &options, &results));

        int bodyTag = results.body;

        // Get the edge tag
        int nEdges;
        int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(bodyTag, &nEdges, &edges));
        Assert.Equal(1, nEdges);
        int edgeTag = edges[0];

        // Get fins via edge — must use NextOfEdge chain
        int nFins;
        int* fins;
        Assert.Equal(0, KernelRuntime.EdgeAskFins(edgeTag, &nFins, &fins));
        Assert.Equal(2, nFins);

        // Get fins via loop — must use NextInLoop chain
        int nFaces;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(bodyTag, &nFaces, &faces));
        int faceTag = faces[0];

        int nLoops;
        int* loops;
        Assert.Equal(0, KernelRuntime.FaceAskLoops(faceTag, &nLoops, &loops));
        int loopTag = loops[0];

        int nLoopFins;
        int* loopFins;
        Assert.Equal(0, KernelRuntime.LoopAskFins(loopTag, &nLoopFins, &loopFins));
        Assert.Equal(2, nLoopFins);

        // Both chains must return the same 2 fin tags (order may differ)
        var edgeFinSet = new HashSet<int> { fins[0], fins[1] };
        var loopFinSet = new HashSet<int> { loopFins[0], loopFins[1] };
        Assert.Equal(edgeFinSet, loopFinSet);
    }

    // ── Test 6: Return arena lifetime ─────────────────────────────

    [Fact]
    public void ReturnArena_ConsecutiveQueriesDoNotClobber()
    {
        int bodyTag = CreateSimpleBody(out int shellTag, out int faceTag, out int loopTag, out int finTag, out int edgeTag, out int vertexTag);

        // First query: body → shells
        int nShells;
        int* shells;
        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
        Assert.Equal(1, nShells);
        int savedShellTag = shells[0];

        // Second query: body → faces (allocates a new slice in the return arena)
        int nFaces;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(bodyTag, &nFaces, &faces));
        Assert.Equal(1, nFaces);
        int savedFaceTag = faces[0];

        // Third query: face → loops
        int nLoops;
        int* loops;
        Assert.Equal(0, KernelRuntime.FaceAskLoops(faceTag, &nLoops, &loops));
        Assert.Equal(1, nLoops);

        // The first pointer must still be valid and unchanged
        Assert.Equal(savedShellTag, shells[0]);
        // The second pointer must still be valid and unchanged
        Assert.Equal(savedFaceTag, faces[0]);
    }

    [Fact]
    public void ReturnArena_RepeatedQueriesDoNotExhaustSession()
    {
        int bodyTag = CreateSimpleBody(out int shellTag, out _, out _, out _, out _, out _);

        int nShells;
        int* shells = null;
        int* firstShells = null;

        for (int i = 0; i < 70000; i++)
        {
            Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
            Assert.Equal(1, nShells);

            if (i == 0)
                firstShells = shells;
        }

        Assert.True(firstShells is not null);
        Assert.Equal(shellTag, firstShells[0]);
        Assert.Equal(shellTag, shells[0]);
    }

    // ── Helper: Create a simple body ──────────────────────────────

    private int CreateSimpleBody(
        out int shellTag,
        out int faceTag,
        out int loopTag,
        out int finTag,
        out int edgeTag,
        out int vertexTag)
    {
        // Classes: [0]=shell, [1]=face, [2]=loop, [3]=fin, [4]=edge, [5]=vertex
        int nTopols = 6;
        var classes = stackalloc int[nTopols];
        classes[0] = ParasolidConstants.PK_CLASS_shell;
        classes[1] = ParasolidConstants.PK_CLASS_face;
        classes[2] = ParasolidConstants.PK_CLASS_loop;
        classes[3] = ParasolidConstants.PK_CLASS_fin;
        classes[4] = ParasolidConstants.PK_CLASS_edge;
        classes[5] = ParasolidConstants.PK_CLASS_vertex;

        // Relations: body(-1)→shell, shell→face, face→loop, loop→fin, edge→fin, fin→edge
        int nRelations = 6;
        var parents = stackalloc int[nRelations];
        var children = stackalloc int[nRelations];
        var senses = stackalloc int[nRelations];

        // body(-1) → shell[0]
        parents[0] = -1; children[0] = 0; senses[0] = 0;
        // shell[0] → face[1]
        parents[1] = 0; children[1] = 1; senses[1] = 0;
        // face[1] → loop[2]
        parents[2] = 1; children[2] = 2; senses[2] = 0;
        // loop[2] → fin[3]
        parents[3] = 2; children[3] = 3; senses[3] = 0;
        // edge[4] → fin[3]
        parents[4] = 4; children[4] = 3; senses[4] = 0;
        // fin[3] → edge[4]
        parents[5] = 3; children[5] = 4; senses[5] = 0;

        var options = new PK_BODY_create_topology_2_o_s();
        var results = new PK_BODY_create_topology_2_r_s();

        Assert.Equal(0, KernelRuntime.BodyCreateTopology2(
            nTopols, classes,
            nRelations, parents, children, senses,
            &options, &results));

        int bodyTag = results.body;

        // Resolve tags for the created entities
        // ReturnArena is a session-level bump allocator — all pointers remain valid
        int nShells;
        int* shells;
        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
        shellTag = shells[0];

        int nFaces;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(bodyTag, &nFaces, &faces));
        faceTag = faces[0];

        int nLoops;
        int* loops;
        Assert.Equal(0, KernelRuntime.FaceAskLoops(faceTag, &nLoops, &loops));
        loopTag = loops[0];

        int nFins;
        int* fins;
        Assert.Equal(0, KernelRuntime.LoopAskFins(loopTag, &nFins, &fins));
        finTag = fins[0];

        int nEdges;
        int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(bodyTag, &nEdges, &edges));
        edgeTag = edges[0];

        int nVertices;
        int* vertices;
        Assert.Equal(0, KernelRuntime.BodyAskVertices(bodyTag, &nVertices, &vertices));
        vertexTag = vertices[0];

        return bodyTag;
    }

    // ── Reflection helper to access nextTag ───────────────────────

    private static int GetNextTag()
    {
        // Use reflection to access the private nextTag field
        var field = typeof(KernelRuntime).GetField("nextTag",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        return (int)field!.GetValue(null)!;
    }
}
