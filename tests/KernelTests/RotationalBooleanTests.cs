using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RotationalBooleanTests : IDisposable
{
    public RotationalBooleanTests() { KernelRuntime.SessionStop(); var o = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&o)); }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Theory]
    [InlineData(15903, 5, 4)] [InlineData(15901, 3, 2)] [InlineData(15902, 5, 4)]
    public void CoaxialOverlapProducesExactCylinderPlaneBrepAndTracksIntersection(int operation, int faces, int edges)
    {
        var a = Cylinder(3, 0, 4); var b = Cylinder(2, 2, 4); var o = BooleanOperationTests.Options(operation); o.tracking = 1;
        PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodyBoolean2(a, 1, &b, &o, &tracking, &result));
        try
        {
            Assert.Equal(1, result.n_bodies); Assert.Equal(a, result.bodies[0]); Assert.False(KernelRuntime.IsValidTag(b));
            BooleanOperationTests.Counts(a, faces, edges, 0); Assert.Equal(1, tracking.n_track_records);
            Assert.Equal(2, tracking.track_records[0].n_original_topols); Assert.Equal(1, tracking.track_records[0].n_product_topols);
            Assert.Equal(ParasolidConstants.PK_enclosure_on_c, Classify(a, 2, 4, out var entity));
            Assert.Equal(entity, tracking.track_records[0].product_topols[0]);
            Assert.Equal(operation == 15901 ? ParasolidConstants.PK_enclosure_outside_c : ParasolidConstants.PK_enclosure_inside_c, Classify(a, 2.5, 1, out _));
            Assert.Equal(operation == 15902 ? ParasolidConstants.PK_enclosure_outside_c : ParasolidConstants.PK_enclosure_inside_c, Classify(a, 1, 3, out _));
        }
        finally { KernelRuntime.TopolTrackResultFree(&tracking); KernelRuntime.BooleanResultFree(&result); }
    }

    [Fact]
    public void ClosedCavityHasIndependentVoidRegionAndRemainsBooleanEditable()
    {
        var a = Cylinder(3, 0, 6); var b = Cylinder(1, 2, 2);
        Boolean(a, b, 15902); BooleanOperationTests.Counts(a, 6, 4, 0);
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(a, 0, 3, out var cavity));
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(a, 4, 3, out var exterior)); Assert.NotEqual(cavity, exterior);
        Assert.Equal(ParasolidConstants.PK_enclosure_inside_c, Classify(a, 2, 3, out _));
        var cutter = Cylinder(2, 3, 4); Boolean(a, cutter, 15902);
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(a, 0, 3.5, out var opened));
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(a, 4, 3.5, out exterior)); Assert.Equal(opened, exterior);
        Assert.Equal(ParasolidConstants.PK_enclosure_inside_c, Classify(a, 2.5, 3.5, out _));
    }

    [Theory]
    [InlineData(0, 5)] [InlineData(1, 3)]
    public void CoincidentCylinderSplitsRespectMergeImprintedOption(byte merge, int faces)
    {
        var a = Cylinder(2, 0, 4); var b = Cylinder(2, 2, 4); var o = BooleanOperationTests.Options(15903); o.merge_imprinted = merge;
        int n = 0; int* output = null; Assert.Equal(0, KernelRuntime.BodyBoolean(a, 1, &b, &o, &n, &output));
        try { Assert.Equal(1, n); BooleanOperationTests.Counts(a, faces, faces - 1, 0); Assert.Equal(ParasolidConstants.PK_enclosure_inside_c, Classify(a, 1, 2, out _)); }
        finally { KernelRuntime.MemoryFree(output); }
    }

    [Fact]
    public void MultipleToolsUseTheirUnionAndCanProduceDisconnectedSolids()
    {
        var a = Cylinder(3, 0, 8); var tools = stackalloc int[2] { Cylinder(2, 1, 2), Cylinder(2, 5, 2) };
        var o = BooleanOperationTests.Options(15901); int n = 0; int* output = null;
        Assert.Equal(0, KernelRuntime.BodyBoolean(a, 2, tools, &o, &n, &output));
        try { Assert.Equal(2, n); for (var i = 0; i < n; i++) BooleanOperationTests.Counts(output[i], 3, 2, 0); }
        finally { KernelRuntime.MemoryFree(output); }
    }

    [Theory]
    [InlineData(15903, 2)] [InlineData(15901, 0)] [InlineData(15902, 1)]
    public void DisjointCylindersPreserveNativeBodyLifetimes(int operation, int count)
    {
        var a = Cylinder(3, 0, 2); var b = Cylinder(1, 3, 2); var o = BooleanOperationTests.Options(operation);
        PK_TOPOL_track_r_s tr = default; PK_boolean_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodyBoolean2(a, 1, &b, &o, &tr, &result));
        try { Assert.Equal(count, result.n_bodies); Assert.Equal(ParasolidConstants.PK_boolean_result_no_clash_c, result.result); Assert.Equal(count > 0, KernelRuntime.IsValidTag(a)); Assert.Equal(count == 2, KernelRuntime.IsValidTag(b)); }
        finally { KernelRuntime.TopolTrackResultFree(&tr); KernelRuntime.BooleanResultFree(&result); }
    }

    [Fact]
    public void AllocationFailureRollsBackTopologyAndReturnsNoPartialBuffers()
    {
        var success = false;
        for (var budget = 0; budget < 90; budget++)
        {
            var a = Cylinder(3, 0, 4); var b = Cylinder(2, 2, 4); var before = KernelRuntime.ReturnOutstandingBlocks;
            var o = BooleanOperationTests.Options(15902); o.tracking = 1; PK_TOPOL_track_r_s tr = default; PK_boolean_r_s result = default;
            KernelRuntime.InjectAllocationFailure(budget); var error = KernelRuntime.BodyBoolean2(a, 1, &b, &o, &tr, &result); KernelRuntime.InjectAllocationFailure(-1);
            if (error != 0)
            {
                Assert.Equal(0, result.n_bodies); Assert.True(result.bodies == null); Assert.True(tr.track_records == null);
                BooleanOperationTests.Counts(a, 3, 2, 0); BooleanOperationTests.Counts(b, 3, 2, 0);
            }
            else { success = true; KernelRuntime.TopolTrackResultFree(&tr); KernelRuntime.BooleanResultFree(&result); }
            Assert.Equal(before, KernelRuntime.ReturnOutstandingBlocks); Assert.Equal(0, KernelRuntime.EntityDelete(1, &a));
            if (KernelRuntime.IsValidTag(b)) Assert.Equal(0, KernelRuntime.EntityDelete(1, &b));
            Assert.Equal(0, KernelRuntime.Bodies.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        }
        Assert.True(success);
    }

    private static int Cylinder(double radius, double z, double height)
    {
        var basis = new PK_AXIS2_sf_s(); basis.location.coord[2] = z; basis.axis.coord[2] = 1; basis.ref_direction.coord[0] = 1;
        int body = 0; Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(radius, height, &basis, &body)); return body;
    }

    [Fact]
    public void AlmostCoaxialThinWideCylinderRemainsUnresolvedWithoutRotatingItsGeometry()
    {
        var a = Cylinder(20, 0, .01); var basis = new PK_AXIS2_sf_s(); basis.axis.coord[0] = 1e-8;
        basis.axis.coord[2] = Math.Sqrt(1 - 1e-16); basis.ref_direction.coord[0] = basis.axis.coord[2]; basis.ref_direction.coord[2] = -1e-8;
        int b = 0; Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(20, .01, &basis, &b));
        var o = BooleanOperationTests.Options(15901); int n = 5; int* result = (int*)1;
        Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect, KernelRuntime.BodyBoolean(a, 1, &b, &o, &n, &result));
        Assert.Equal(ProjectGmKernel.Native.Computation.AlgorithmStatus.NotConverged,KernelRuntime.LastGeneralIntersectionStatus);
        Assert.Equal(0, n); Assert.True(result == null); BooleanOperationTests.Counts(a, 3, 2, 0); BooleanOperationTests.Counts(b, 3, 2, 0);
    }

    [Fact]
    public void AlmostPerpendicularCuttingPlaneIsNotSilentlyReplacedWithAnAxialPlane()
    {
        var body = Cylinder(20, 0, .01); var definition = new PK_PLANE_sf_s();
        definition.basis_set.location.coord[2] = .005; definition.basis_set.axis.coord[0] = 1e-8;
        definition.basis_set.axis.coord[2] = Math.Sqrt(1 - 1e-16);
        definition.basis_set.ref_direction.coord[0] = definition.basis_set.axis.coord[2]; definition.basis_set.ref_direction.coord[2] = -1e-8;
        int plane = 0; Assert.Equal(0, KernelRuntime.PlaneCreate(&definition, &plane));
        var o = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-5 }; PK_section_r_s result = default;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodySectionWithSurf(body, plane, &o, &result));
        Assert.True(result.front_bodies.array == null && result.back_bodies.array == null); BooleanOperationTests.Counts(body, 3, 2, 0);
        Assert.True(KernelRuntime.IsValidTag(plane));
    }

    [Theory]
    [InlineData(0, 18200)] [InlineData(0, 18201)] [InlineData(0, 18202)]
    [InlineData(1, 18200)] [InlineData(1, 18201)] [InlineData(1, 18202)]
    [InlineData(2, 18200)] [InlineData(2, 18201)] [InlineData(2, 18202)]
    public void AxialSectionOfSolidTubeAndCavityReturnsClosedCaps(int kind, int fence)
    {
        var body = Cylinder(3, 0, 6);
        if (kind != 0) Boolean(body, Cylinder(1, kind == 1 ? -1 : 2, kind == 1 ? 8 : 2), 15902);
        var plane = Plane(3); var o = new PK_BODY_section_o_s { o_t_version = 5, fence = fence, default_tol = 1e-5 };
        PK_section_r_s result = default; Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &o, &result));
        try
        {
            Assert.Equal(fence == 18201 ? 0 : 1, result.front_bodies.length); Assert.Equal(result.front_bodies.length, result.front_faces.length);
            Assert.Equal(fence == 18200 ? 0 : 1, result.back_bodies.length); Assert.Equal(result.back_bodies.length, result.back_faces.length);
            if (result.front_bodies.length != 0)
            {
                Assert.Equal(body, result.front_bodies.array[0]);
                Assert.Equal(ParasolidConstants.PK_enclosure_inside_c, Classify(result.front_bodies.array[0], 2, 4, out _));
                Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(result.front_bodies.array[0], 2, 2, out _));
            }
            if (result.back_bodies.length != 0)
            {
                Assert.Equal(ParasolidConstants.PK_enclosure_inside_c, Classify(result.back_bodies.array[0], 2, 2, out _));
                Assert.Equal(ParasolidConstants.PK_enclosure_outside_c, Classify(result.back_bodies.array[0], 2, 4, out _));
            }
            Assert.True(KernelRuntime.IsValidTag(plane));
        }
        finally { KernelRuntime.SectionResultFree(&result); }
    }

    [Fact]
    public void IsolatedToolsRetainTheirBodyFaceAndEdgeHandlesDuringMultiToolUnion()
    {
        var a = Cylinder(2, 0, 2); var tools = stackalloc int[2] { Cylinder(2, 1, 2), Cylinder(1, 7, 2) };
        var isolated = tools[1]; int oldCount = 0; int* oldEdges = null; Assert.Equal(0, KernelRuntime.BodyAskEdges(isolated, &oldCount, &oldEdges));
        var oldEdge = oldEdges[0]; KernelRuntime.MemoryFree(oldEdges);
        var o = BooleanOperationTests.Options(15903); int n = 0; int* results = null;
        Assert.Equal(0, KernelRuntime.BodyBoolean(a, 2, tools, &o, &n, &results));
        try { Assert.Equal(2, n); Assert.Equal(a, results[0]); Assert.Equal(isolated, results[1]); Assert.True(KernelRuntime.IsValidTag(oldEdge)); Assert.False(KernelRuntime.IsValidTag(tools[0])); }
        finally { KernelRuntime.MemoryFree(results); }
    }

    [Fact]
    public void SectionAllocationFailuresRestoreSourceAndFreeAllReturnBlocks()
    {
        var success = false;
        for (var budget = 0; budget < 200; budget += 7)
        {
            var body = Cylinder(3, 0, 6); var plane = Plane(3); var before = KernelRuntime.ReturnOutstandingBlocks;
            var o = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-5 }; PK_section_r_s result = default;
            KernelRuntime.InjectAllocationFailure(budget); var error = KernelRuntime.BodySectionWithSurf(body, plane, &o, &result); KernelRuntime.InjectAllocationFailure(-1);
            if (error != 0)
            {
                Assert.True(result.front_bodies.array == null && result.back_bodies.array == null && result.front_faces.array == null && result.back_faces.array == null);
                BooleanOperationTests.Counts(body, 3, 2, 0); Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            }
            else
            {
                success = true; Assert.Equal(1, result.front_bodies.length); Assert.Equal(1, result.back_bodies.length);
                Assert.Equal(0, KernelRuntime.EntityDelete(result.front_bodies.length, result.front_bodies.array));
                Assert.Equal(0, KernelRuntime.EntityDelete(result.back_bodies.length, result.back_bodies.array)); KernelRuntime.SectionResultFree(&result);
            }
            Assert.Equal(before, KernelRuntime.ReturnOutstandingBlocks); Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane));
            Assert.Equal(0, KernelRuntime.Bodies.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        }
        Assert.True(success);
    }

    private static int Plane(double z)
    {
        var d = new PK_PLANE_sf_s(); d.basis_set.location.coord[2] = z; d.basis_set.axis.coord[2] = 1; d.basis_set.ref_direction.coord[0] = 1;
        int plane = 0; Assert.Equal(0, KernelRuntime.PlaneCreate(&d, &plane)); return plane;
    }

    [Theory]
    [InlineData(false, 18200, false)] [InlineData(false, 18201, true)] [InlineData(false, 18202, true)]
    [InlineData(true, 18200, true)] [InlineData(true, 18201, false)] [InlineData(true, 18202, true)]
    public void OwnedCuttingSurfaceLifetimeMatchesTheSurvivingOriginalBoundary(bool cavity, int fence, bool alive)
    {
        var body = Cylinder(3, 0, 6); if (cavity) Boolean(body, Cylinder(1, 2, 2), 15902);
        var tool = OwnedPlane(body, cavity ? 2 : 6); var o = new PK_BODY_section_o_s { o_t_version = 5, fence = fence, default_tol = 1e-5 };
        PK_section_r_s result = default; Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, tool, &o, &result));
        try
        {
            Assert.Equal(alive, KernelRuntime.IsValidTag(tool));
            int toolClass = 0;
            Assert.Equal(alive ? ParasolidConstants.PK_ERROR_no_errors : ParasolidConstants.PK_ERROR_not_a_tag, KernelRuntime.EntityAskClass(tool, &toolClass));
            if (alive)
            {
                var surface = KernelRuntime.GetSurfaceSlotByTag(tool); Assert.True(KernelRuntime.Surfaces[surface].OwnerCount > 0);
                Assert.Equal(0, KernelRuntime.Surfaces[surface].GeometricOwnerCount);
            }
        }
        finally { KernelRuntime.SectionResultFree(&result); }
    }

    [Fact]
    public void BorrowedCuttingSupportRollsBackOwnershipAndDoesNotLeakTemporaryClones()
    {
        var success = false;
        for (var budget = 0; budget < 250; budget += 9)
        {
            var body = Cylinder(3, 0, 6); Boolean(body, Cylinder(1, 2, 2), 15902); var tool = OwnedPlane(body, 2);
            var slot = KernelRuntime.GetSurfaceSlotByTag(tool); var owner = KernelRuntime.Surfaces[slot].OwnerFace;
            var outstanding = KernelRuntime.ReturnOutstandingBlocks;
            var o = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-5 }; PK_section_r_s result = default;
            KernelRuntime.InjectAllocationFailure(budget); var error = KernelRuntime.BodySectionWithSurf(body, tool, &o, &result); KernelRuntime.InjectAllocationFailure(-1);
            if (error != 0)
            {
                Assert.True(KernelRuntime.IsValidTag(tool)); Assert.Equal(owner, KernelRuntime.Surfaces[slot].OwnerFace);
                Assert.Equal(1, KernelRuntime.Surfaces[slot].OwnerCount); Assert.Equal(0, KernelRuntime.Surfaces[slot].GeometricOwnerCount);
                BooleanOperationTests.Counts(body, 6, 4, 0); Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
                Assert.True(result.front_bodies.array == null && result.back_bodies.array == null);
            }
            else
            {
                success = true; Assert.True(KernelRuntime.IsValidTag(tool));
                Assert.Equal(0, KernelRuntime.EntityDelete(result.front_bodies.length, result.front_bodies.array));
                Assert.Equal(0, KernelRuntime.EntityDelete(result.back_bodies.length, result.back_bodies.array)); KernelRuntime.SectionResultFree(&result);
            }
            Assert.Equal(outstanding, KernelRuntime.ReturnOutstandingBlocks);
            Assert.Equal(0, KernelRuntime.Bodies.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        }
        Assert.True(success);
    }

    private static int OwnedPlane(int body, double height)
    {
        int count = 0; int* faces = null; Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        try
        {
            for (var i = 0; i < count; i++)
            {
                int surface = 0, cls = 0; Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[i], &surface)); Assert.Equal(0, KernelRuntime.EntityAskClass(surface, &cls));
                if (cls != ParasolidConstants.PK_CLASS_plane) continue; PK_PLANE_sf_s p = default; Assert.Equal(0, KernelRuntime.PlaneAsk(surface, &p));
                if (Math.Abs(p.basis_set.location.coord[2] - height) < 1e-10) return surface;
            }
        }
        finally { KernelRuntime.MemoryFree(faces); }
        throw new InvalidOperationException("Missing owned support plane");
    }
    private static void Boolean(int target, int tool, int operation)
    {
        var o = BooleanOperationTests.Options(operation); int n = 0; int* output = null; Assert.Equal(0, KernelRuntime.BodyBoolean(target, 1, &tool, &o, &n, &output));
        try { Assert.Equal(1, n); Assert.Equal(target, output[0]); } finally { KernelRuntime.MemoryFree(output); }
    }
    private static int Classify(int body, double r, double z, out int topol)
    {
        var point = new PK_VECTOR_s(); point.coord[0] = r; point.coord[2] = z; int classification = 0, t = 0;
        Assert.Equal(0, KernelRuntime.BodyContainsVector(body, point, &classification, &t)); topol = t; return classification;
    }
}
