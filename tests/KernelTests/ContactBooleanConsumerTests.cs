using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ContactBooleanConsumerTests:IDisposable
{
    public ContactBooleanConsumerTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(24)] [InlineData(64)] [InlineData(128)]
    public void ContactPointAllocationFailureRestoresBothBodiesAndNativeBlocks(int budget)
    {
        var target=Bowl();var tool=BooleanOperationTests.Block(0,0,-1,2,2,1.5);var before=XtBrepImportTests.Transmit(target);var toolBefore=XtBrepImportTests.Transmit(tool);
        var blocks=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var options=BooleanOperationTests.Options(15902);int count;int* products;
        KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products);KernelRuntime.InjectAllocationFailure(-1);
        if(error==0){Assert.Equal(1,count);BooleanOperationTests.Counts(target,6,12,9);KernelRuntime.MemoryFree(products);return;}
        Assert.Equal(0,count);Assert.True(products==null);Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(before,XtBrepImportTests.Transmit(target));Assert.Equal(toolBefore,XtBrepImportTests.Transmit(tool));
    }

    [Theory]
    [InlineData(15902,false)] [InlineData(15901,false)]
    [InlineData(15902,true)] [InlineData(15901,true)]
    public void ExteriorIsolatedTouchHasNoRegularizedMaterialVolume(int function,bool reload)
    {
        var target=Bowl();
        if(reload){Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(target),out var loaded));Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));target=loaded;}
        var tool=BooleanOperationTests.Block(0,0,-1,2,2,1.5);
        var options=BooleanOperationTests.Options(function);int count;int* products;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products));
        try
        {
            Assert.Equal(function==15902?1:0,count);
            if(count!=0)
            {
                Assert.Equal(target,products[0]);BooleanOperationTests.Counts(target,6,12,9);
                Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(target),out var restored));Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));
                Assert.True(KernelRuntime.TryResolveBodySlot(restored,out var slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));BooleanOperationTests.Counts(restored,6,12,9);
                var p=new PK_VECTOR_s();p.coord[2]=.5;int enclosure,topology;Assert.Equal(0,KernelRuntime.BodyContainsVector(restored,p,&enclosure,&topology));
                Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);int cls;Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&cls));Assert.Equal(ParasolidConstants.PK_CLASS_vertex,cls);
                Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));
            }
        }
        finally{if(products!=null)KernelRuntime.MemoryFree(products);}
        Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void UnrepresentedPointTouchUnionRejectsWithoutChangingTheInputs()
    {
        var target=Bowl();var tool=BooleanOperationTests.Block(0,0,-1,2,2,1.5);
        var before=XtBrepImportTests.Transmit(target);var beforeTool=XtBrepImportTests.Transmit(tool);
        var options=BooleanOperationTests.Options(15903);int count;int* products;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products));
        Assert.Equal(0,count);Assert.True(products==null);
        Assert.Equal(before,XtBrepImportTests.Transmit(target));Assert.Equal(beforeTool,XtBrepImportTests.Transmit(tool));
    }

    private static int Bowl()
    {
        int* sheets=stackalloc int[6];double* poles=stackalloc double[27];double* a=stackalloc double[3]{.25,-.25,.25};
        double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2];int* vm=stackalloc int[2];var box=new PK_UVBOX_s();box.param[2]=box.param[3]=1;
        for(var face=0;face<6;face++)
        {
            var nu=face<2?(face==0?3:2):3;var nv=face==0?3:2;um[0]=um[1]=nu;vm[0]=vm[1]=nv;
            for(var i=0;i<nu;i++)for(var j=0;j<nv;j++)
            {
                var at=3*(i*nv+j);var u=(double)i/(nu-1);var v=(double)j/(nv-1);
                if(face==0){poles[at]=u-.5;poles[at+1]=.5-v;poles[at+2]=.5+a[i]+a[j];}
                else if(face==1){poles[at]=u-.5;poles[at+1]=v-.5;poles[at+2]=2;}
                else{poles[at]=face switch{2=>u-.5,3=>.5-u,4=>-.5,_=>.5};poles[at+1]=face switch{2=>-.5,3=>.5,4=>.5-u,_=>u-.5};poles[at+2]=(1-v)*(.75+a[i])+2*v;}
            }
            var definition=new PK_BSURF_sf_s{u_degree=nu-1,v_degree=nv-1,n_u_vertices=nu,n_v_vertices=nv,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
                u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
                self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};int surface;
            Assert.Equal(0,KernelRuntime.BSurfCreate(&definition,&surface));Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(surface,box,sheets+face));Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));
        }
        var options=new PK_BODY_sew_bodies_o_s{o_t_version=8,prefered_body_type=ParasolidConstants.PK_BODY_sewing_solid_c,treat_as_manifold=1,number_of_iterations=1,
            duplicate_removal=ParasolidConstants.PK_BODY_sewing_remove_none_c,assembly_sewing=ParasolidConstants.PK_BODY_sewing_assy_no_c,general_sewing=ParasolidConstants.PK_BODY_sewing_gen_no_c,
            sew_up_inner_loops=ParasolidConstants.PK_LOOP_sew_up_loop_c,reduce_edge_tolerance=ParasolidConstants.PK_EDGE_reduce_tol_no_c};
        int count,remainingCount,problemCount;int* bodies;int* remaining;PK_BODY_problem_group_s* problems;
        Assert.Equal(0,KernelRuntime.BodySewBodies(6,sheets,1e-9,&options,&count,&bodies,&remainingCount,&remaining,&problemCount,&problems));
        try{Assert.Equal(1,count);Assert.Equal(0,remainingCount);Assert.Equal(0,problemCount);return bodies[0];}
        finally{if(bodies!=null)KernelRuntime.MemoryFree(bodies);if(remaining!=null)KernelRuntime.MemoryFree(remaining);if(problems!=null)KernelRuntime.MemoryFree(problems);}
    }
}
