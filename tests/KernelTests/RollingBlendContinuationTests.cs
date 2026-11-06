global using SurfTag=System.Int32;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RollingBlendContinuationTests:IDisposable
{
    public RollingBlendContinuationTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    private struct ContinuationCommand:IKernelCommand
    {
        internal SurfTag First,Second;
        internal SurfTag* Result;
        internal double Radius;
        public int Execute()
        {
            var options=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1};options.help.coord[1]=.5;options.help.coord[2]=1;
            Span<AnalyticSurface> planes=stackalloc AnalyticSurface[2];planes[0]=new(SurfaceClass.Plane,default,Vector(0,-1,0),Vector(1,0,0));planes[1]=new(SurfaceClass.Plane,Vector(0,1,0),Vector(0,1,0),Vector(1,0,0));
            return KernelRuntime.CreateFiniteRollingBlend(First,-Radius,Second,-Radius,Vector(0,0,1),Vector(0,1,1),&options,Result,planes);
        }
    }

    [Theory]
    [InlineData(.2,.05)] [InlineData(.4,.1)] [InlineData(.8,.15)]
    public void FiniteCapDomainExtendsTheRollingBranchPastNaturalSplineEnds(double bulge,double radius)
    {
        var body=RegularSolidBlendTests.Roof(bulge);Supports(body,out var roof,out var wall);int blend=0;
        var command=new ContinuationCommand{First=roof,Second=wall,Result=&blend,Radius=radius};
        Assert.Equal(0,KernelRuntime.Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,roof));
        var data=KernelRuntime.BlendedEdgeDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)].DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        Assert.True(Math.Min(charts[0].Position.Y,charts[^1].Position.Y)<-radius);Assert.True(Math.Max(charts[0].Position.Y,charts[^1].Position.Y)>1+radius);
        Assert.Contains(charts.ToArray(),c=>c.FirstV>1||c.FirstV<0);
        var original=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(roof)];Assert.Equal(0,original.VMin);Assert.Equal(1,original.VMax);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    private static void Supports(int body,out int roof,out int wall)
    {
        roof=wall=0;KernelRuntime.TryResolveBodySlot(body,out var slot);var face=KernelRuntime.Bodies[slot].FirstFaceBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var tag=KernelRuntime.Faces[face].SurfTag;var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)];
            if(surface.Class==SurfaceClass.BSurface)roof=tag;
            else{var p=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(tag,&p));if(p.basis_set.axis.coord[0]<-.9)wall=tag;}
        }
        Assert.NotEqual(0,roof);Assert.NotEqual(0,wall);
    }
}
