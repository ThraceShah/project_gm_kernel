using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class SphericalFaceDomainTests:IDisposable
{
    public SphericalFaceDomainTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,false)]
    [InlineData(0,true)] [InlineData(1,true)] [InlineData(2,true)]
    public void CertifiedSphericalOctantUsesNoNativeSeamOrPole(int frame,bool reversed)
    {
        var axis=frame==0?Vector(0,0,1):frame==1?Vector(1,0,0):Vector(1/Math.Sqrt(3),1/Math.Sqrt(3),1/Math.Sqrt(3));
        var reference=frame==0?Vector(1,0,0):frame==1?Vector(0,0,1):Vector(1/Math.Sqrt(2),-1/Math.Sqrt(2),0);
        var sphereDefinition=new PK_SPHERE_sf_s{radius=1};Write(&sphereDefinition.basis_set.axis,axis);Write(&sphereDefinition.basis_set.ref_direction,reference);int sphere;
        Assert.Equal(0,KernelRuntime.SphereCreate(&sphereDefinition,&sphere));
        Span<KernelRuntime.ModelingEdge> edges=stackalloc KernelRuntime.ModelingEdge[3];Span<KernelRuntime.ModelingCoedge> coedges=stackalloc KernelRuntime.ModelingCoedge[3];
        for(var i=0;i<3;i++)
        {
            var sf=new PK_CIRCLE_sf_s{radius=1};Write(&sf.basis_set.axis,i==0?Vector(0,0,1):i==1?Vector(1,0,0):Vector(0,1,0));
            Write(&sf.basis_set.ref_direction,i==0?Vector(1,0,0):i==1?Vector(0,1,0):Vector(0,0,1));int curve;
            Assert.Equal(0,KernelRuntime.CircleCreate(&sf,&curve));edges[i]=new(){Curve=curve,Start=0,End=Math.PI*.5,StartVertex=i,EndVertex=(i+1)%3};
            coedges[i]=new(){Edge=reversed?2-i:i,Forward=reversed?(byte)0:(byte)1};
        }
        foreach(var inside in new[]{true,false})
        {
            var point=inside?Vector(1/Math.Sqrt(3),1/Math.Sqrt(3),1/Math.Sqrt(3)):Vector(-1/Math.Sqrt(3),1/Math.Sqrt(3),1/Math.Sqrt(3));
            var xx=Dot(point,reference);var yy=Dot(point,Cross(axis,reference));var zz=Dot(point,axis);var uv=new PK_UV_s();uv.param[0]=Math.Atan2(yy,xx);uv.param[1]=Math.Atan2(zz,Math.Sqrt(xx*xx+yy*yy));
            Assert.Equal(0,KernelRuntime.SphericalLoopWinding(sphere,edges,coedges,uv,out var winding));Assert.Equal(inside?(reversed?-1:1):0,winding);
            var before=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.SphericalLoopWinding(sphere,edges,coedges,uv,out winding);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(0,error);Assert.Equal(0,allocated);
        }
        Assert.Equal(0,KernelRuntime.SphericalCycleWitness(sphere,edges,coedges,out _,out var positive));Assert.Equal(reversed?0:1,positive);
    }

    [Fact]
    public void UnprovedGlobalSphericalRegionIsRejected()
    {
        var sf=new PK_SPHERE_sf_s{radius=1};sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int sphere;Assert.Equal(0,KernelRuntime.SphereCreate(&sf,&sphere));
        var circle=new PK_CIRCLE_sf_s{basis_set=sf.basis_set,radius=1};int curve;Assert.Equal(0,KernelRuntime.CircleCreate(&circle,&curve));
        Span<KernelRuntime.ModelingEdge> edges=stackalloc KernelRuntime.ModelingEdge[1];edges[0]=new(){Curve=curve,Start=0,End=Math.Tau,StartVertex=-1,EndVertex=-1};
        Span<KernelRuntime.ModelingCoedge> coedges=stackalloc KernelRuntime.ModelingCoedge[1];coedges[0]=new(){Edge=0,Forward=1};
        var uv=new PK_UV_s();uv.param[1]=.5;Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SphericalLoopWinding(sphere,edges,coedges,uv,out _));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SphericalFiniteDomainImprintBuildsSharedArcsAcrossNativePole(bool pole)
    {
        var body=Sheet(pole);var planeDefinition=new PK_PLANE_sf_s();planeDefinition.basis_set.location.coord[0]=.65;
        planeDefinition.basis_set.axis.coord[0]=1;planeDefinition.basis_set.ref_direction.coord[1]=1;int plane;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&planeDefinition,&plane));int count;int* edges;
        var error=KernelRuntime.BodyImprintPlane(body,plane,1e-8,&count,&edges);
        Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter}");
        try{Assert.Equal(1,count);BooleanOperationTests.Counts(body,2,7,6);}finally{if(edges!=null)KernelRuntime.MemoryFree(edges);}
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var reloaded));BooleanOperationTests.Counts(reloaded,2,7,6);
    }

    internal static int Sheet(bool pole)
    {
        var definition=new PK_SPHERE_sf_s{radius=1};definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;int sphere;
        Assert.Equal(0,KernelRuntime.SphereCreate(&definition,&sphere));var box=new PK_UVBOX_s();box.param[0]=.15;box.param[1]=.2;box.param[2]=1.15;box.param[3]=.9;int body;
        Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(sphere,box,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&sphere));
        if(pole)
        {
            Write(&definition.basis_set.axis,Vector(Math.Cos(.55)*Math.Cos(.65),Math.Cos(.55)*Math.Sin(.65),Math.Sin(.55)));
            Write(&definition.basis_set.ref_direction,Vector(-Math.Sin(.65),Math.Cos(.65),0));Assert.Equal(0,KernelRuntime.SphereCreate(&definition,&sphere));
            Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].Header.Tag;var old=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag;byte positive=1;
            Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(face));Assert.Equal(0,KernelRuntime.FaceAttachSurfaces(1,&face,&sphere,&positive));Assert.Equal(0,KernelRuntime.EntityDelete(1,&old));
        }
        return body;
    }
    private static void Write(PK_VECTOR_s* target,KernelVector3 value){target->coord[0]=value.X;target->coord[1]=value.Y;target->coord[2]=value.Z;}
}
