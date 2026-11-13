using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class SurfaceBlendTests:IDisposable
{
    public SurfaceBlendTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(.2)] [InlineData(-.2)]
    public void PlaneBlendHasTheRequestedRollingBallContactsAndOwnsItsGeometry(double radius)
    {
        var a=Plane(false);var b=Plane(true);var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=0;
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(a,radius,b,radius,Point(0,0,0),Point(0,1,0),&options,&blend));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&a));Assert.Equal(0,KernelRuntime.EntityDelete(1,&b));
        var uv=new PK_UV_s();uv.param[0]=radius>0?Math.PI:0;uv.param[1]=.37;PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[4];
        Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,1,1,0,jet));Near(0,jet[0].coord[2]);Near(0,jet[1].coord[2]);Near(0,jet[2].coord[2]);
        uv.param[0]=radius>0?Math.PI*1.5:Math.PI*.5;
        Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,1,1,0,jet));Near(0,jet[0].coord[0]);Near(0,jet[1].coord[0]);Near(0,jet[2].coord[0]);
        var sf=new PK_CYL_sf_s();Assert.Equal(0,KernelRuntime.CylAsk(blend,&sf));Near(Math.Abs(radius),sf.radius);Near(radius,sf.basis_set.location.coord[0]);Near(radius,sf.basis_set.location.coord[2]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SpherePlaneBlendIsAnExactTorusInEitherSupportOrder(bool sphereFirst)
    {
        var plane=Plane(false);var definition=new PK_SPHERE_sf_s{radius=1};definition.basis_set.location.coord[2]=1;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int sphere=0;Assert.Equal(0,KernelRuntime.SphereCreate(&definition,&sphere));var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=0;
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(sphereFirst?sphere:plane,.2,sphereFirst?plane:sphere,.2,Point(1,0,0),Point(0,sphereFirst?-1:1,0),&options,&blend));
        var actual=new PK_TORUS_sf_s();Assert.Equal(0,KernelRuntime.TorusAsk(blend,&actual));Near(Math.Sqrt(.8),actual.major_radius);Near(.2,actual.minor_radius);Near(.2,actual.basis_set.location.coord[2]);
        var uv=new PK_UV_s();uv.param[0]=.73;uv.param[1]=-Math.PI/2;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,0,0,0,&point));Near(0,point.coord[2]);
        uv.param[1]=Math.Atan2(2.0/3,-Math.Sqrt(5)/3);Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,0,0,0,&point));
        Near(1,point.coord[0]*point.coord[0]+point.coord[1]*point.coord[1]+(point.coord[2]-1)*(point.coord[2]-1));
    }

    [Theory]
    [InlineData(false,.2,.2,.6)] [InlineData(true,.2,.2,.6)]
    [InlineData(false,-.2,-.2,.6)] [InlineData(true,-.2,-.2,.6)]
    [InlineData(false,.2,.2,.9)] [InlineData(true,.2,.2,.9)]
    [InlineData(false,.2,-.2,.6)] [InlineData(true,.2,-.2,.6)]
    [InlineData(false,-.2,.2,.6)] [InlineData(true,-.2,.2,.6)]
    public void ClosedEllipseBlendKeepsNativeParametersContactsAndItsCopiedDefinition(bool reverse,double radius,double secondRadius,double tilt)
    {
        var cylinder=new PK_CYL_sf_s{radius=1};cylinder.basis_set.axis.coord[2]=1;cylinder.basis_set.ref_direction.coord[0]=1;
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[1]=tilt;plane.basis_set.axis.coord[2]=Math.Sqrt(1-tilt*tilt);plane.basis_set.ref_direction.coord[0]=1;
        int a,b,blend;Assert.Equal(0,KernelRuntime.CylCreate(&cylinder,&a));Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        Assert.Equal(-1,KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(a)].OwnerFace);
        var options=new PK_SURF_create_blend_o_s{o_t_version=1};
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(reverse?b:a,radius,reverse?a:b,secondRadius,Point(1.2,0,.25),Point(0,1.2,-.65),&options,&blend));
        PK_BLENDSF_sf_s definition;Assert.Equal(0,KernelRuntime.BlendSfAsk(blend,&definition));
        Assert.Equal(CurveClass.Ellipse,KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(definition.spine)].Class);
        Near(0,definition.spine_extent.value[0]);Near(Math.Tau,definition.spine_extent.value[1]);
        var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)];var data=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];
        var program=KernelRuntime.GetNestedBlendProgram(data);
        Assert.True(ProjectGmKernel.Native.Geometry.Evaluation.SpineParameterMap.Periodic(program.Spines[program.Surfaces[data.ProgramRoot].Spine]));
        var reverseSection=program.Surfaces[data.ProgramRoot].ReverseSection;
        int copy;Assert.Equal(0,KernelRuntime.EntityCopy(blend,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[2];
        foreach(var station in new[]{0,.7,2.4,4.8,Math.Tau})
        {
            var uv=new PK_UV_s();uv.param[0]=station;
            Assert.Equal(0,KernelRuntime.SurfEval(copy,uv,0,0,0,points));uv.param[1]=1;Assert.Equal(0,KernelRuntime.SurfEval(copy,uv,0,0,0,points+1));
            var first=reverseSection?0:1;var second=1-first;
            var cylinderEnd=reverse?second:first;var planeEnd=1-cylinderEnd;
            Near(1,points[cylinderEnd].coord[0]*points[cylinderEnd].coord[0]+points[cylinderEnd].coord[1]*points[cylinderEnd].coord[1]);
            Near(0,points[planeEnd].coord[1]*tilt+points[planeEnd].coord[2]*plane.basis_set.axis.coord[2]);
        }
        var box=new PK_UVBOX_s();box.param[0]=.5;box.param[1]=.15;box.param[2]=1.1;box.param[3]=.85;
        int body;Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(copy,box,&body));var text=XtBrepImportTests.Transmit(body);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,XtBrepImportTests.Receive(text,out body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bodySlot));Assert.False(KernelRuntime.Xt.IsOpaque(bodySlot));
        var edge=KernelRuntime.Bodies[bodySlot].FirstEdgeBody;var spCount=0;
        for(var e=0;e<KernelRuntime.Bodies[bodySlot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var boundary=KernelRuntime.Edges[edge];Assert.True(KernelRuntime.TryProfileEdgeRange(boundary,out var first,out var last));Assert.True(last>first);
            if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(boundary.CurveTag)].Class!=CurveClass.SPCurve)continue;
            spCount++;var at=(first+last)*.5;var point=new PK_VECTOR_s();double restored;
            Assert.Equal(0,KernelRuntime.CurveEval(boundary.CurveTag,at,0,&point));Assert.Equal(0,KernelRuntime.CurveParameteriseVector(boundary.CurveTag,point,&restored));Near(at,restored);
            point.coord[2]+=1;Assert.NotEqual(0,KernelRuntime.CurveParameteriseVector(boundary.CurveTag,point,&restored));
        }
        Assert.Equal(2,spCount);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
    }

    [Fact]
    public void EllipseBlendRejectsUnprovedWholeTubeAndRollsBackFailedPublication()
    {
        var cylinder=new PK_CYL_sf_s{radius=1};cylinder.basis_set.axis.coord[2]=1;cylinder.basis_set.ref_direction.coord[0]=1;
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[1]=.6;plane.basis_set.axis.coord[2]=.8;plane.basis_set.ref_direction.coord[0]=1;
        int a,b,blend;Assert.Equal(0,KernelRuntime.CylCreate(&cylinder,&a));Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&b));
        var options=new PK_SURF_create_blend_o_s{o_t_version=1};var before=(KernelRuntime.Surfaces.AliveCount,KernelRuntime.Curves.AliveCount);
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfCreateBlend(a,-.9,b,-.9,Point(1.2,0,.25),Point(0,1.2,-.65),&options,&blend));Assert.Equal(0,blend);Assert.Equal(before,(KernelRuntime.Surfaces.AliveCount,KernelRuntime.Curves.AliveCount));
        var failed=0;var succeeded=false;
        for(var budget=0;budget<64;budget++)
        {
            KernelRuntime.InjectAllocationFailure(budget);
            var error=KernelRuntime.SurfCreateBlend(a,.2,b,.2,Point(1.2,0,.25),Point(0,1.2,-.65),&options,&blend);
            KernelRuntime.InjectAllocationFailure(-1);
            if(error==0){succeeded=true;Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));break;}
            failed++;Assert.Equal(ParasolidConstants.PK_ERROR_memory_full,error);Assert.Equal(0,blend);Assert.Equal(before,(KernelRuntime.Surfaces.AliveCount,KernelRuntime.Curves.AliveCount));
        }
        Assert.True(succeeded);Assert.True(failed>0);
    }

    [Fact]
    public void ReversedStraightSpineFailsWithoutPublishingGeometry()
    {
        var a=Plane(false);var b=Plane(true);var surfaces=KernelRuntime.Surfaces.AliveCount;var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=999;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_value,KernelRuntime.SurfCreateBlend(a,.2,b,.2,Point(0,0,0),Point(0,-1,0),&options,&blend));Assert.Equal(0,blend);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
    }

    private static int Plane(bool x)
    {var sf=new PK_PLANE_sf_s();sf.basis_set.axis.coord[x?0:2]=1;sf.basis_set.ref_direction.coord[x?1:0]=1;int tag=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&tag));return tag;}
    private static PK_VECTOR_s Point(double x,double y,double z){var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;return p;}
    private static void Near(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,1e-9);
}
