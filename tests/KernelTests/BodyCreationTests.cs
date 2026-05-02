using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Tests for body creation.
/// </summary>
public unsafe class BodyCreationTests : IDisposable
{
    public BodyCreationTests()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        KernelRuntime.SessionStart(&options);
    }

    public void Dispose()
    {
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void CreateSimpleBody_Works()
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

        // Relations: shell→face, face→loop, loop→fin, edge→fin, fin→edge
        int nRelations = 5;
        var parents = stackalloc int[nRelations];
        var children = stackalloc int[nRelations];
        var senses = stackalloc int[nRelations];

        // shell[0] → face[1]
        parents[0] = 0; children[0] = 1; senses[0] = 0;
        // face[1] → loop[2]
        parents[1] = 1; children[1] = 2; senses[1] = 0;
        // loop[2] → fin[3]
        parents[2] = 2; children[2] = 3; senses[2] = 0;
        // edge[4] → fin[3]
        parents[3] = 4; children[3] = 3; senses[3] = 0;
        // fin[3] → edge[4]
        parents[4] = 3; children[4] = 4; senses[4] = 0;

        var options = new PK_BODY_create_topology_2_o_s();
        var results = new PK_BODY_create_topology_2_r_s();

        int error = KernelRuntime.BodyCreateTopology2(
            nTopols, classes,
            nRelations, parents, children, senses,
            &options, &results);

        Assert.Equal(0, error);
        Assert.True(results.body > 0, "Body tag should be positive");
    }
}
