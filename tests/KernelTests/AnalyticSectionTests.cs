using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class AnalyticSectionTests:IDisposable
{
    public AnalyticSectionTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(18202,-.5)] [InlineData(18202,0)] [InlineData(18202,.5)]
    [InlineData(18200,-.5)] [InlineData(18200,.5)]
    [InlineData(18201,-.5)] [InlineData(18201,.5)]
    public void SphereSection_CreatesClosedAnalyticCapsAndPreservesNativeSourceIdentity(int fence,double height)
    {
        var body=AnalyticBooleanTests.Sphere(0,0,0,2);var oldFace=FirstFace(body);var plane=Plane(height);var options=Options(fence);PK_section_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodySectionWithSurf(body,plane,&options,&result));
        try
        {
            var front=fence!=18201;var back=fence!=18200;
            Assert.Equal(front?1:0,result.front_bodies.length);Assert.Equal(back?1:0,result.back_bodies.length);
            Assert.Equal(front?1:0,result.front_faces.length);Assert.Equal(back?1:0,result.back_faces.length);
            Assert.Equal(body,front?result.front_bodies.array[0]:result.back_bodies.array[0]);Assert.True(KernelRuntime.IsValidTag(oldFace));
            if(front)CheckCap(result.front_bodies.array[0],result.front_faces.array[0],height,true,oldFace);
            if(back)CheckCap(result.back_bodies.array[0],result.back_faces.array[0],height,false,front?0:oldFace);
            Assert.True(KernelRuntime.IsValidTag(plane));
        }
        finally{Assert.Equal(0,KernelRuntime.SectionResultFree(&result));}
    }

    [Theory]
    [InlineData(-3,18202,true)] [InlineData(-3,18200,true)] [InlineData(-3,18201,false)]
    [InlineData(3,18202,true)] [InlineData(3,18200,false)] [InlineData(3,18201,true)]
    public void DisjointPlane_PreservesOrConsumesWholeSphereWithoutInventingCaps(double height,int fence,bool keep)
    {
        var body=AnalyticBooleanTests.Sphere(0,0,0,2);var face=FirstFace(body);var plane=Plane(height);var options=Options(fence);PK_section_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodySectionWithSurf(body,plane,&options,&result));
        try
        {
            Assert.Equal(keep?1:0,result.front_bodies.length+result.back_bodies.length);Assert.Equal(0,result.front_faces.length+result.back_faces.length);
            Assert.Equal(keep,KernelRuntime.IsValidTag(body));Assert.Equal(keep,KernelRuntime.IsValidTag(face));if(keep)BooleanOperationTests.Counts(body,1,0,0);
        }
        finally{KernelRuntime.SectionResultFree(&result);}
    }

    [Fact]
    public void AllocationFailuresRestoreTheSourceAndReleaseAllFourReturnArrays()
    {
        var succeeded=false;var failed=0;
        for(var fault=0;fault<160;fault++)
        {
            var body=AnalyticBooleanTests.Sphere(0,0,0,2);var face=FirstFace(body);var plane=Plane(.5);var options=Options(18202);PK_section_r_s result=default;
            var before=KernelRuntime.ReturnOutstandingBlocks;KernelRuntime.InjectAllocationFailure(fault);
            var error=KernelRuntime.BodySectionWithSurf(body,plane,&options,&result);KernelRuntime.InjectAllocationFailure(-1);
            if(error!=0)
            {
                failed++;Assert.True(result.front_bodies.array==null&&result.back_bodies.array==null&&result.front_faces.array==null&&result.back_faces.array==null);
                Assert.True(KernelRuntime.IsValidTag(face));BooleanOperationTests.Counts(body,1,0,0);
            }
            else
            {
                succeeded=true;var back=result.back_bodies.array[0];Assert.NotEqual(body,back);Assert.Equal(0,KernelRuntime.SectionResultFree(&result));Assert.Equal(0,KernelRuntime.EntityDelete(1,&back));
            }
            Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&plane));
            Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
            if(succeeded)break;
        }
        Assert.True(succeeded);Assert.True(failed>0);
    }

    [Theory]
    [InlineData(18202)] [InlineData(18200)] [InlineData(18201)]
    public void TangentPlane_IsExplicitlyUnsupportedAndAtomic(int fence)
    {
        var body=AnalyticBooleanTests.Sphere(0,0,0,2);var plane=Plane(2);var options=Options(fence);PK_section_r_s result=default;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodySectionWithSurf(body,plane,&options,&result));
        Assert.True(result.front_bodies.array==null&&result.back_bodies.array==null);BooleanOperationTests.Counts(body,1,0,0);
    }

    private static void CheckCap(int body,int cap,double height,bool front,int preservedSphereFace)
    {
        BooleanOperationTests.Counts(body,2,1,0);var inside=front?(height+2)*.5:(height-2)*.5;
        Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,Enclosure(body,0,0,inside,out _));
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,Enclosure(body,0,0,-inside+2*height,out _));
        Assert.Equal(ParasolidConstants.PK_enclosure_on_c,Enclosure(body,0,0,height,out var boundary));Assert.Equal(cap,boundary);
        Assert.Equal(ParasolidConstants.PK_enclosure_on_c,Enclosure(body,0,0,front?2:-2,out boundary));if(preservedSphereFace!=0)Assert.Equal(preservedSphereFace,boundary);
        Assert.Equal(ParasolidConstants.PK_enclosure_on_c,Enclosure(body,Math.Sqrt(4-height*height),0,height,out boundary));int cls=0;
        Assert.Equal(0,KernelRuntime.EntityAskClass(boundary,&cls));Assert.Equal(ParasolidConstants.PK_CLASS_edge,cls);
    }
    private static int FirstFace(int body){int n=0;int* faces=null;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&faces));var face=faces[0];KernelRuntime.MemoryFree(faces);return face;}
    private static int Enclosure(int body,double x,double y,double z,out int topology)
    {var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;int kind=0,tag=0;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,p,&kind,&tag));topology=tag;return kind;}
    private static int Plane(double height){var p=new PK_PLANE_sf_s();p.basis_set.location.coord[2]=height;p.basis_set.axis.coord[2]=1;p.basis_set.ref_direction.coord[0]=1;int plane=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&p,&plane));return plane;}
    internal static PK_BODY_section_o_s Options(int fence)=>new(){o_t_version=5,fence=fence,default_tol=1e-5,check_fa=ParasolidConstants.PK_section_check_fa_yes_c,keep_as_facet=ParasolidConstants.PK_BODY_keep_as_facet_yes_c};
}
