using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BooleanOperationTests : IDisposable
{
    public BooleanOperationTests()
    { KernelRuntime.SessionStop(); var options = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&options)); }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Theory]
    [InlineData(15903, 0.5, 0.5, 1, 14, 32, 20)]
    [InlineData(15901, 0.5, 0.5, 1, 6, 12, 8)]
    [InlineData(15902, 0.5, 0.5, 1, 8, 18, 12)]
    [InlineData(15903, 1.0, 0.0, 1, 14, 28, 16)]
    [InlineData(15901, 1.0, 0.0, 1, 6, 12, 8)]
    [InlineData(15902, 1.0, 0.0, 1, 6, 12, 8)]
    [InlineData(15903, 2.0, 0.0, 1, 10, 20, 12)]
    [InlineData(15901, 2.0, 0.0, 0, 0, 0, 0)]
    [InlineData(15902, 2.0, 0.0, 1, 6, 12, 8)]
    public void OverlappingAndTouchingBlocks_MatchSdkTopology(int operation, double x, double y,
        int bodyCount, int faces, int edges, int vertices)
    {
        var target = Block(0, 0, 0, 2, 2, 2); var tool = Block(x, y, 0, 2, 2, 2);
        var options = Options(operation); PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s result = default;
        var error = KernelRuntime.BodyBoolean2(target, 1, &tool, &options, &tracking, &result);
        Assert.Equal(0, error); Assert.Equal(bodyCount, result.n_bodies);
        if (bodyCount != 0) { Assert.Equal(target, result.bodies[0]); Counts(target, faces, edges, vertices); }
        Assert.Equal(0, KernelRuntime.TopolTrackResultFree(&tracking)); Assert.Equal(0, KernelRuntime.BooleanResultFree(&result));
    }

    [Theory]
    [InlineData(15903, 1)] [InlineData(15901, 1)] [InlineData(15902, 0)]
    public void CoincidentBlocks_UseMaterialOnBothSides(int operation, int count)
    {
        var a = Block(0, 0, 0, 2, 2, 2); var b = Block(0, 0, 0, 2, 2, 2); var options = Options(operation);
        int n = 0; int* result = null; Assert.Equal(0, KernelRuntime.BodyBoolean(a, 1, &b, &options, &n, &result));
        Assert.Equal(count, n); if (n > 0) Counts(result[0], 6, 12, 8); KernelRuntime.MemoryFree(result);
    }

    [Theory]
    [InlineData(15903, 2)] [InlineData(15901, 0)] [InlineData(15902, 1)]
    public void DisjointBlocks_ReportNoClashAndPreserveRequiredHandles(int operation, int count)
    {
        var a = Block(0, 0, 0, 2, 2, 2); var b = Block(4, 0, 0, 2, 2, 2); var options = Options(operation);
        PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodyBoolean2(a, 1, &b, &options, &tracking, &result));
        Assert.Equal(ParasolidConstants.PK_boolean_result_no_clash_c, result.result); Assert.Equal(count, result.n_bodies);
        if (count > 0) Assert.Equal(a, result.bodies[0]); if (count > 1) Assert.Equal(b, result.bodies[1]);
        KernelRuntime.BooleanResultFree(&result);
    }

    [Fact]
    public void InternalSubtraction_BuildsSeparateCavityRegionAndShells()
    {
        var target = Block(0, 0, 0, 4, 4, 4); var tool = Block(0, 0, 1, 2, 2, 2); var options = Options(15902);
        int count = 0; int* results = null; Assert.Equal(0, KernelRuntime.BodyBoolean(target, 1, &tool, &options, &count, &results));
        Assert.Equal(1, count); Counts(target, 12, 24, 16);
        int n = 0; int* tags = null; Assert.Equal(0, KernelRuntime.BodyAskRegions(target, &n, &tags)); Assert.Equal(3, n); KernelRuntime.MemoryFree(tags);
        Assert.Equal(0, KernelRuntime.BodyAskShells(target, &n, &tags)); Assert.Equal(4, n); KernelRuntime.MemoryFree(tags); KernelRuntime.MemoryFree(results);
    }

    [Fact]
    public void MultiToolIntersection_IntersectsWithUnionAndReturnsBothComponents()
    {
        var target = Block(0, 0, 0, 10, 4, 4); var tools = stackalloc int[2];
        tools[0] = Block(-3, 0, 0, 2, 2, 4); tools[1] = Block(3, 0, 0, 2, 2, 4);
        var options = Options(15901); int n = 0; int* results = null;
        Assert.Equal(0, KernelRuntime.BodyBoolean(target, 2, tools, &options, &n, &results));
        Assert.Equal(2, n); Counts(results[0], 6, 12, 8); Counts(results[1], 6, 12, 8); KernelRuntime.MemoryFree(results);
    }

    [Fact]
    public void UnsupportedCurvedBoolean_IsAtomicAndReturnsNoBodies()
    {
        var a = Block(0, 0, 0, 2, 2, 2); var basis = Axis(0, 0, 1); int b = 0;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(1, &basis, &b)); var options = Options(15902);
        int n = 99; int* result = (int*)1;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyBoolean(a, 1, &b, &options, &n, &result));
        Assert.Equal(0, n); Assert.True(result == null); Counts(a, 6, 12, 8);
        int code = 0; Assert.Equal(0, KernelRuntime.EntityAskClass(b, &code));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CompleteTrackingDoesNotEnableUnsupportedCompletionOrGeneralRegionRequests(int request)
    {
        var target=Block(0,0,0,2,2,2);var tool=Block(.5,.5,0,2,2,2);
        var targetBytes=XtBrepImportTests.Transmit(target);var toolBytes=XtBrepImportTests.Transmit(tool);
        var options=Options(ParasolidConstants.PK_boolean_subtract_c);options.tracking=1;
        options.tracking_type=ParasolidConstants.PK_boolean_track_type_comp_c;
        if(request==0)options.imprint_complete_targ=ParasolidConstants.PK_imprint_complete_edge_c;
        else if(request==1)options.imprint_complete_tool=ParasolidConstants.PK_imprint_complete_edge_c;
        else options.track_regions=ParasolidConstants.PK_track_regions_basic_c;
        PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        Assert.Equal(0,tracking.n_track_records);Assert.True(tracking.track_records==null);
        Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);
        Assert.Equal(targetBytes,XtBrepImportTests.Transmit(target));Assert.Equal(toolBytes,XtBrepImportTests.Transmit(tool));
    }

    internal static PK_BODY_boolean_o_s Options(int operation) => new()
    {
        o_t_version = 19, function = operation, default_tol = 1e-5,
        fence = ParasolidConstants.PK_boolean_fence_none_c, check_fa = ParasolidConstants.PK_boolean_check_fa_yes_c,
        imprint_complete_targ = ParasolidConstants.PK_imprint_complete_no_c, imprint_complete_tool = ParasolidConstants.PK_imprint_complete_no_c,
        target_material_side = ParasolidConstants.PK_boolean_material_default_c, tool_material_side = ParasolidConstants.PK_boolean_material_default_c,
        resulting_body_type = ParasolidConstants.PK_boolean_prefer_original_c, flag_no_effect = ParasolidConstants.PK_boolean_no_effect_basic_c,
        nm_edge_repair = ParasolidConstants.PK_nm_edge_repair_no_c, blend_radius = 1e-5,
        tracking_type = ParasolidConstants.PK_boolean_track_type_basic_c, track_regions = ParasolidConstants.PK_track_regions_no_c,
        update = ParasolidConstants.PK_boolean_update_default_c, merge_in_solid_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c,
        merge_in_face_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c, impose_bodies = ParasolidConstants.PK_boolean_impose_no_c,
        merge_tools = ParasolidConstants.PK_merge_tools_no_c,
    };

    internal static int Block(double x, double y, double z, double dx, double dy, double dz)
    { var basis = Axis(x, y, z); int body = 0; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(dx, dy, dz, &basis, &body)); return body; }
    private static PK_AXIS2_sf_s Axis(double x, double y, double z)
    { var a = new PK_AXIS2_sf_s(); a.location.coord[0] = x; a.location.coord[1] = y; a.location.coord[2] = z; a.axis.coord[2] = 1; a.ref_direction.coord[0] = 1; return a; }
    internal static void Counts(int body, int faces, int edges, int vertices)
    {
        int n = 0; int* tags = null; Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &n, &tags)); Assert.Equal(faces, n); KernelRuntime.MemoryFree(tags);
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &n, &tags)); Assert.Equal(edges, n); KernelRuntime.MemoryFree(tags);
        Assert.Equal(0, KernelRuntime.BodyAskVertices(body, &n, &tags)); Assert.Equal(vertices, n); KernelRuntime.MemoryFree(tags);
    }
}
