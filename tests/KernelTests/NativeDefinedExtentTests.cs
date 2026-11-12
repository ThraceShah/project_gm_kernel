using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NativeDefinedExtentTests:IDisposable
{
    public NativeDefinedExtentTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Theory]
    [InlineData(15903,2)] [InlineData(15901,0)] [InlineData(15902,1)]
    public void SeparatedClosedSolidsPreserveNativeIdentityStatusTrackingAndRollback(int operation,int expected)
    {
        int target,tool,mark;Assert.Equal(0,KernelRuntime.BodyCreateSolidSphere(1,null,&target));
        var basis=new PK_AXIS2_sf_s();basis.location.coord[0]=10;basis.axis.coord[2]=1;basis.ref_direction.coord[0]=1;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidTorus(2,.5,&basis,&tool));
        Assert.Equal(0,KernelRuntime.MarkCreate(&mark));var options=BooleanOperationTests.Options(operation);options.tracking=1;
        var tracking=new PK_TOPOL_track_r_s();var result=new PK_boolean_r_s();
        Assert.Equal(0,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        Assert.Equal(ParasolidConstants.PK_boolean_result_no_clash_c,result.result);Assert.Equal(expected,result.n_bodies);Assert.Equal(0,tracking.n_track_records);
        if(expected>0)Assert.Equal(target,result.bodies[0]);if(expected>1)Assert.Equal(tool,result.bodies[1]);
        Assert.Equal(expected>0,KernelRuntime.IsValidTag(target));Assert.Equal(expected>1,KernelRuntime.IsValidTag(tool));
        Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,KernelRuntime.BooleanResultFree(&result));
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));Assert.True(KernelRuntime.IsValidTag(target));Assert.True(KernelRuntime.IsValidTag(tool));
        var point=new PK_VECTOR_s();point.coord[0]=12;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(tool,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
    }

    [Theory]
    [InlineData(.125)] [InlineData(1.7)] [InlineData(8.0)]
    public void ClosedRingTorusClassifiesMaterialHoleAndBoundaryWithoutLoopsOrManagedHeap(double scale)
    {
        int body;Assert.Equal(0,KernelRuntime.BodyCreateSolidTorus(2*scale,.5*scale,null,&body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bodySlot));var exterior=KernelRuntime.Bodies[bodySlot].FirstRegion;
        var point=new PK_VECTOR_s();point.coord[0]=2*scale;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
        var bytes=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology);bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Assert.Equal(0,error);Assert.Equal(0,bytes);Assert.Equal(KernelRuntime.Regions[KernelRuntime.Regions[exterior].NextInBody].Header.Tag,topology);
        point.coord[0]=0;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,enclosure);
        Assert.Equal(KernelRuntime.Regions[exterior].Header.Tag,topology);
        point.coord[0]=2.5*scale;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);
        int cls;Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&cls));Assert.Equal(ParasolidConstants.PK_CLASS_face,cls);
    }

    [Theory]
    [InlineData(false,.125)] [InlineData(false,1.7)] [InlineData(false,8.0)]
    [InlineData(true,.125)] [InlineData(true,1.7)] [InlineData(true,8.0)]
    public void NativeFiniteSupportRejectsOnlyBeyondActualPrecisionAndRefusesExtrapolatedDefinition(bool rational,double scale)
    {
        double* poles=stackalloc double[36];var dim=rational?4:3;
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {
            var at=(3*i+j)*dim;var w=rational?1+.25*i+.5*j:1;
            poles[at]=(3+.5*i*scale)*w;poles[at+1]=(5+.5*j*scale)*w;poles[at+2]=(7+(i==2&&j==2?.15:0)*scale)*w;if(rational)poles[at+3]=w;
        }
        double* uk=stackalloc double[2]{2,5};double* vk=stackalloc double[2]{-3,-1};int* mult=stackalloc int[2]{3,3};
        var definition=new PK_BSURF_sf_s{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=dim,is_rational=rational?(byte)1:(byte)0,vertex=poles,
            n_u_knots=2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=mult,v_knot_mult=mult,form=ParasolidConstants.PK_BSURF_form_arbitrary_c,
            u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        int surface,body;Assert.Equal(0,KernelRuntime.BSurfCreate(&definition,&surface));
        var box=new PK_UVBOX_s();box.param[0]=2.6;box.param[1]=-2.6;box.param[2]=4.4;box.param[3]=-1.4;
        Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(surface,box,&body));Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bodySlot));
        var face=KernelRuntime.Bodies[bodySlot].FirstFaceBody;var slot=KernelRuntime.GetSurfaceSlotByTag(surface);
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.NativeDefinedSurfaceExtent(slot,out var extent));
        var margin=.02*scale;var near=new KernelVector3{X=extent.XHi+.5*margin,Y=5+.5*scale,Z=7};var far=near;far.X=extent.XHi+2*margin;
        Assert.True(KernelRuntime.RegularPointOutsideNativeSupport(face,near,1e-8));
        // Runtime precision record, as populated by native XT receive. The
        // real PK authoring/receive path is checked by NativeSurfaceDomainOracle.
        KernelRuntime.Vertices[KernelRuntime.Bodies[bodySlot].FirstVertexBody].Tolerance=margin;
        Assert.False(KernelRuntime.RegularPointOutsideNativeSupport(face,near,1e-8));Assert.True(KernelRuntime.RegularPointOutsideNativeSupport(face,far,1e-8));
        var bytes=GC.GetAllocatedBytesForCurrentThread();var status=KernelRuntime.NativeDefinedSurfaceExtent(slot,out _);
        var rejected=KernelRuntime.RegularPointOutsideNativeSupport(face,near,1e-8);bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Assert.Equal(0,bytes);Assert.Equal(AlgorithmStatus.Success,status);Assert.False(rejected);
        // A malformed record is a certificate negative, not a valid model fixture.
        KernelRuntime.Surfaces[slot].UMax=6;
        Assert.Equal(AlgorithmStatus.InvalidInput,KernelRuntime.NativeDefinedSurfaceExtent(slot,out _));
        Assert.False(KernelRuntime.RegularPointOutsideNativeSupport(face,far,1e-8));
    }
}
