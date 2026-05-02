using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Simple session tests to verify basic functionality.
/// </summary>
public unsafe class SessionTests
{
    [Fact]
    public void SessionStartStop_Works()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
        Assert.Equal(0, KernelRuntime.SessionStop());
    }

    [Fact]
    public void PointCreate_Works()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        KernelRuntime.SessionStart(&options);

        int pointTag;
        var sf = new PK_POINT_sf_s();
        sf.position.coord[0] = 1.0;
        sf.position.coord[1] = 2.0;
        sf.position.coord[2] = 3.0;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &pointTag));
        Assert.True(pointTag > 0);

        KernelRuntime.SessionStop();
    }

    [Fact]
    public void TransfCreate_Works()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        KernelRuntime.SessionStart(&options);

        int transfTag;
        var sf = new PK_TRANSF_sf_s();
        // Identity matrix
        sf.matrix[0] = 1; sf.matrix[5] = 1; sf.matrix[10] = 1; sf.matrix[15] = 1;
        Assert.Equal(0, KernelRuntime.TransfCreate(&sf, &transfTag));
        Assert.True(transfTag > 0);

        KernelRuntime.SessionStop();
    }

    [Fact]
    public void BodyCreateSolidBlock_Works()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        KernelRuntime.SessionStart(&options);

        int bodyTag;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(10, 20, 30, null, &bodyTag));
        Assert.True(bodyTag > 0);

        // Verify topology counts
        int nShells, nFaces, nEdges, nVertices;
        int* shells; int* faces; int* edges; int* vertices;

        Assert.Equal(0, KernelRuntime.BodyAskShells(bodyTag, &nShells, &shells));
        Assert.Equal(1, nShells);

        Assert.Equal(0, KernelRuntime.BodyAskFaces(bodyTag, &nFaces, &faces));
        Assert.Equal(6, nFaces);

        Assert.Equal(0, KernelRuntime.BodyAskEdges(bodyTag, &nEdges, &edges));
        Assert.Equal(12, nEdges);

        Assert.Equal(0, KernelRuntime.BodyAskVertices(bodyTag, &nVertices, &vertices));
        Assert.Equal(8, nVertices);

        KernelRuntime.SessionStop();
    }
}
