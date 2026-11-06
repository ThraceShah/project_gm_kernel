using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ConstructionGeometryTests : IDisposable
{
    public ConstructionGeometryTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Fact]
    public void BodyOwnedDependencies_CopyTransformDeleteAndUndoKeepIndependentParts()
    {
        var body = Wire(out var profile);
        var swept = Swept(profile);
        var offsetSf = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = .1 };
        int offset; Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetSf, &offset));
        var trimSf = new PK_TRCURVE_sf_s { basis_curve = profile }; trimSf.t_int.value[0] = .1; trimSf.t_int.value[1] = .8;
        int trim; Assert.Equal(0, KernelRuntime.TrCurveCreate(&trimSf, &trim));
        Assert.Equal(new[] { swept, offset, trim }.Order(), Members(body).Order());
        int mark; Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var extra = Swept(profile);
        Assert.Equal(4, Members(body).Length);
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.False(KernelRuntime.IsValidTag(extra));
        Assert.Equal(new[] { swept, offset, trim }.Order(), Members(body).Order());

        var original = Eval(offset);
        int copy; Assert.Equal(0, KernelRuntime.EntityCopy(body, &copy));
        var members = Members(copy);
        Assert.Equal(3, members.Length);
        foreach (var tag in members) { Assert.NotEqual(swept, tag); Assert.NotEqual(offset, tag); Assert.NotEqual(trim, tag); }
        var copiedOffset = FindClass(members, ParasolidConstants.PK_CLASS_offset);
        var copiedSwept = FindClass(members, ParasolidConstants.PK_CLASS_swept);
        var copiedTrim = FindClass(members, ParasolidConstants.PK_CLASS_trcurve);
        var copiedOffsetSf = new PK_OFFSET_sf_s(); Assert.Equal(0, KernelRuntime.OffsetAsk(copiedOffset, &copiedOffsetSf));
        Assert.Equal(copiedSwept, copiedOffsetSf.underlying_surface);
        var copiedTrimSf = new PK_TRCURVE_sf_s(); Assert.Equal(0, KernelRuntime.TrCurveAsk(copiedTrim, &copiedTrimSf));
        var copiedSweptSf = new PK_SWEPT_sf_s(); Assert.Equal(0, KernelRuntime.SweptAsk(copiedSwept, &copiedSweptSf));
        Assert.Equal(copiedTrimSf.basis_curve, copiedSweptSf.curve);
        Assert.NotEqual(profile, copiedSweptSf.curve);
        var sourceData = KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(profile).DataIndex];
        var copiedData = KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(copiedSweptSf.curve).DataIndex];
        Assert.NotEqual(sourceData.VertexBlock, copiedData.VertexBlock);

        var transformSf = new PK_TRANSF_sf_s(); transformSf.matrix[0] = transformSf.matrix[5] = transformSf.matrix[10] = transformSf.matrix[15] = 1;
        transformSf.matrix[3] = 3; transformSf.matrix[7] = 5; transformSf.matrix[11] = 7;
        int transform; Assert.Equal(0, KernelRuntime.TransfCreate(&transformSf, &transform));
        int count; int* replacements; byte* exact;
        Assert.Equal(0, KernelRuntime.BodyTransform(copy, transform, 0, &count, &replacements, &exact));
        var moved = Eval(copiedOffset);
        for (var i = 0; i < 3; i++) Assert.Equal(original.coord[i] + (i == 0 ? 3 : i == 1 ? 5 : 7), moved.coord[i], 10);
        var unchanged = Eval(offset);
        for (var i = 0; i < 3; i++) Assert.Equal(original.coord[i], unchanged.coord[i], 12);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(swept)); Assert.False(KernelRuntime.IsValidTag(offset)); Assert.False(KernelRuntime.IsValidTag(trim));
        Assert.False(KernelRuntime.IsValidTag(profile)); Assert.True(KernelRuntime.IsValidTag(copiedSweptSf.curve));
        var retained = Eval(copiedOffset);
        for (var i = 0; i < 3; i++) Assert.Equal(moved.coord[i], retained.coord[i], 12);

        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &copy));
        foreach (var tag in members) Assert.False(KernelRuntime.IsValidTag(tag));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(members.Order(), Members(copy).Order());
        var restored = Eval(copiedOffset);
        for (var i = 0; i < 3; i++) Assert.Equal(moved.coord[i], restored.coord[i], 12);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &copy));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &transform));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(0, KernelRuntime.Points.AliveCount); Assert.Equal(0, KernelRuntime.Bodies.AliveCount);
    }

    [Fact]
    public void DeleteConstructionParent_CascadesIntermediateAndUnlinksEveryMember()
    {
        var body = Wire(out var profile); var swept = Swept(profile);
        var sf = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = .1 };
        int offset; Assert.Equal(0, KernelRuntime.OffsetCreate(&sf, &offset));
        Assert.Equal(2, Members(body).Length);
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &swept));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &offset));
        Assert.Empty(Members(body)); Assert.False(KernelRuntime.IsValidTag(swept)); Assert.True(KernelRuntime.IsValidTag(profile));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void AttachAndDetachConstructionMember_KeepSeparateBoundaryLinks()
    {
        int body; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var profile = KernelRuntime.Edges[KernelRuntime.Bodies[slot].FirstEdgeBody].CurveTag;
        var swept = Swept(profile);
        Assert.Equal(new[] { swept }, Members(body));
        var faceSlot = KernelRuntime.Bodies[slot].FirstFaceBody;
        var face = KernelRuntime.TagOf(PoolKind.Face, faceSlot); var old = KernelRuntime.Faces[faceSlot].SurfTag;
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face)); byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &swept, &sense)); Assert.Empty(Members(body));
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(swept).OwnerCount);
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face)); Assert.Equal(new[] { swept }, Members(body));
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(swept).OwnerCount);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(profile).OwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &swept)); Assert.Empty(Members(body));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &old)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void AttachedDependentGeometry_ExcludesItsDefiningClosureButNotReverseOwners()
    {
        var wire = Wire(out var source);
        int profile; Assert.Equal(0, KernelRuntime.EntityCopy(source, &profile));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &wire));
        var swept = Swept(profile);
        var offsetSf = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = .1 };
        int offset; Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetSf, &offset));
        int body; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var faceSlot = KernelRuntime.Bodies[slot].FirstFaceBody;
        var face = KernelRuntime.TagOf(PoolKind.Face, faceSlot); var old = KernelRuntime.Faces[faceSlot].SurfTag;
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face)); byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &offset, &sense));
        Assert.Empty(Members(body));
        Assert.Equal(-1, KernelRuntime.GetConstructionBody(swept));
        Assert.Equal(-1, KernelRuntime.GetConstructionBody(profile));

        var reverseOwner = Swept(profile);
        Assert.Equal(new[] { reverseOwner }, Members(body));
        int mark; Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face));
        Assert.Equal(new[] { profile, swept, offset, reverseOwner }.Order(), Members(body).Order());
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &offset, &sense));
        Assert.Equal(new[] { reverseOwner }, Members(body));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(offset, KernelRuntime.Faces[faceSlot].SurfTag);
        Assert.Equal(new[] { reverseOwner }, Members(body));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &old));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(profile)); Assert.False(KernelRuntime.IsValidTag(swept));
        Assert.False(KernelRuntime.IsValidTag(offset)); Assert.False(KernelRuntime.IsValidTag(reverseOwner));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void PartialStandaloneSharingCannotEnterPart()
    {
        var body = Wire(out var profile);
        int standalone; Assert.Equal(0, KernelRuntime.EntityCopy(profile, &standalone));
        var first = Swept(standalone); var second = Swept(standalone);
        int target; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &target));
        Assert.True(KernelRuntime.TryResolveBodySlot(target, out var slot)); var faceSlot = KernelRuntime.Bodies[slot].FirstFaceBody;
        var face = KernelRuntime.TagOf(PoolKind.Face, faceSlot); Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face));
        byte sense = 1;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_shared_entity, KernelRuntime.FaceAttachSurfaces(1, &face, &first, &sense));
        Assert.Equal(0, KernelRuntime.Faces[faceSlot].SurfTag); Assert.Empty(Members(target));
        Assert.Equal(-1, KernelRuntime.GetConstructionBody(first)); Assert.Equal(-1, KernelRuntime.GetConstructionBody(second));
    }

    [Fact]
    public void BodyReplacementRebindsBothConstructionChainsBeforeDeletingOldTopology()
    {
        var target=AnalyticBooleanTests.Sphere(0,0,0,1);var tool=AnalyticBooleanTests.Sphere(.5,0,0,3);
        var targetSlot=KernelRuntime.ResolveTagRecord(target).Slot;var toolSlot=KernelRuntime.ResolveTagRecord(tool).Slot;
        var firstDefinition=new PK_OFFSET_sf_s{underlying_surface=KernelRuntime.Faces[KernelRuntime.Bodies[targetSlot].FirstFaceBody].SurfTag,offset_distance=.1};
        var secondDefinition=new PK_OFFSET_sf_s{underlying_surface=KernelRuntime.Faces[KernelRuntime.Bodies[toolSlot].FirstFaceBody].SurfTag,offset_distance=.2};
        int first,second;Assert.Equal(0,KernelRuntime.OffsetCreate(&firstDefinition,&first));Assert.Equal(0,KernelRuntime.OffsetCreate(&secondDefinition,&second));
        int mark;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_unite_c);int n;int* bodies;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&bodies));
        try{Assert.Equal(1,n);Assert.Equal(target,bodies[0]);}finally{KernelRuntime.MemoryFree(bodies);}
        Assert.False(KernelRuntime.IsValidTag(first));Assert.True(KernelRuntime.IsValidTag(second));
        Assert.Equal(targetSlot,KernelRuntime.GetConstructionBody(second));Assert.Equal(new[]{second},Members(target));
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));
        Assert.Equal(targetSlot,KernelRuntime.GetConstructionBody(first));Assert.Equal(toolSlot,KernelRuntime.GetConstructionBody(second));
        Assert.Equal(new[]{first},Members(target));Assert.Equal(new[]{second},Members(tool));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));Assert.Equal(0,KernelRuntime.EntityDelete(1,&tool));
        Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Bodies.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(12)] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void ConstructionOwnerReplacementAllocationFailureIsAtomic(int budget)
    {
        var target=AnalyticBooleanTests.Sphere(0,0,0,1);var tool=AnalyticBooleanTests.Sphere(.5,0,0,3);
        var targetSlot=KernelRuntime.ResolveTagRecord(target).Slot;var toolSlot=KernelRuntime.ResolveTagRecord(tool).Slot;
        var definition=new PK_OFFSET_sf_s{underlying_surface=KernelRuntime.Faces[KernelRuntime.Bodies[targetSlot].FirstFaceBody].SurfTag,offset_distance=.1};
        int first,second;Assert.Equal(0,KernelRuntime.OffsetCreate(&definition,&first));
        definition.underlying_surface=KernelRuntime.Faces[KernelRuntime.Bodies[toolSlot].FirstFaceBody].SurfTag;
        Assert.Equal(0,KernelRuntime.OffsetCreate(&definition,&second));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_unite_c);var outstanding=KernelRuntime.ReturnOutstandingBlocks;int n;int* bodies;
        KernelRuntime.InjectAllocationFailure(budget);int error;
        try{error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&bodies);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error!=0)
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full,error);Assert.Equal(0,n);Assert.True(bodies==null);
            Assert.Equal(targetSlot,KernelRuntime.GetConstructionBody(first));Assert.Equal(toolSlot,KernelRuntime.GetConstructionBody(second));
            Assert.Equal(new[]{first},Members(target));Assert.Equal(new[]{second},Members(tool));Assert.Equal(4,KernelRuntime.Surfaces.AliveCount);
        }
        else{Assert.Equal(1,n);Assert.Equal(targetSlot,KernelRuntime.GetConstructionBody(second));Assert.Equal(0,KernelRuntime.MemoryFree(bodies));}
        Assert.Equal(outstanding,KernelRuntime.ReturnOutstandingBlocks);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));if(KernelRuntime.IsValidTag(tool))Assert.Equal(0,KernelRuntime.EntityDelete(1,&tool));
        Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Bodies.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)] [InlineData(12)]
    public void FailedDependentCreation_RestoresPartChainAndReferences(int fault)
    {
        var body = Wire(out var profile);
        var sf = new PK_SWEPT_sf_s { curve = profile }; sf.direction.coord[2] = 1;
        int surface = 0, error;
        KernelRuntime.InjectAllocationFailure(fault);
        try { error = KernelRuntime.SweptCreate(&sf, &surface); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0)
        {
            Assert.Equal(new[] { surface }, Members(body));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &surface));
        }
        else { Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, error); Assert.Equal(0, surface); }
        Assert.Empty(Members(body)); Assert.Equal(0, KernelRuntime.GetCurveByTag(profile).GeometricOwnerCount);
        Assert.True(KernelRuntime.IsValidTag(profile)); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body)); Assert.Equal(0, KernelRuntime.Curves.AliveCount);
    }

    private static int Wire(out int profile)
    {
        var poles = stackalloc double[9] { -2, 0, 0, 0, 3, 0, 2, 0, 0 };
        var knots = stackalloc double[2] { 0, 1 }; var mults = stackalloc int[2] { 3, 3 };
        var sf = new PK_BCURVE_sf_s { degree = 2, n_vertices = 3, vertex_dim = 3, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = mults, knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
            form = ParasolidConstants.PK_BCURVE_form_arbitrary_c, self_intersecting = ParasolidConstants.PK_self_intersect_false_c };
        int input; Assert.Equal(0, KernelRuntime.BCurveCreate(&sf, &input));
        var bounds = new PK_INTERVAL_s(); bounds.value[1] = 1; int body;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(input, bounds, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot)); profile = KernelRuntime.Edges[KernelRuntime.Bodies[slot].FirstEdgeBody].CurveTag;
        if (input != profile) Assert.Equal(0, KernelRuntime.EntityDelete(1, &input));
        return body;
    }
    private static int Swept(int profile)
    {
        var sf = new PK_SWEPT_sf_s { curve = profile }; sf.direction.coord[2] = 1;
        int result; Assert.Equal(0, KernelRuntime.SweptCreate(&sf, &result)); return result;
    }
    private static PK_VECTOR_s Eval(int surface)
    {
        var uv = new PK_UV_s(); uv.param[0] = .37; uv.param[1] = .43; var point = new PK_VECTOR_s();
        Assert.Equal(0, KernelRuntime.SurfEval(surface, uv, 0, 0, 0, &point)); return point;
    }
    private static int[] Members(int body)
    {
        int count; int* tags;
        delegate* unmanaged<int, int*, int**, int> ask = &KernelExports.PK_PART_ask_geoms;
        Assert.Equal(0, ask(body, &count, &tags));
        try { return new ReadOnlySpan<int>(tags, count).ToArray(); }
        finally { if (tags != null) Assert.Equal(0, KernelRuntime.MemoryFree(tags)); }
    }
    private static int FindClass(int[] members, int expected)
    {
        foreach (var tag in members) { int actual; Assert.Equal(0, KernelRuntime.EntityAskClass(tag, &actual)); if (actual == expected) return tag; }
        throw new InvalidOperationException("Missing construction class " + expected);
    }
}
