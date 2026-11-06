using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendContactEndpointTests:IDisposable
{
    public BlendContactEndpointTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();
    private struct EndpointContactCommand:IKernelCommand
    {
        internal SurfTag First,Second;
        internal SurfTag* Blend;
        internal CurveTag* Contact;
        public int Execute()
        {
            var options=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1};options.help.coord[1]=.5;options.help.coord[2]=1;
            Span<AnalyticSurface> planes=stackalloc AnalyticSurface[2]{new(SurfaceClass.Plane,default,Vector(0,-1,0),Vector(1,0,0)),new(SurfaceClass.Plane,Vector(0,1,0),Vector(0,1,0),Vector(1,0,0))};
            var error=KernelRuntime.CreateFiniteRollingBlend(First,-.1,Second,-.1,Vector(0,0,1),Vector(0,1,1),&options,Blend,planes);if(error!=0)return error;
            error=KernelRuntime.CreateBlendContactCurve(*Blend,1,out var curve);if(error==0)*Contact=curve;return error;
        }
    }
    [Fact]
    public void ContactEndpointClosureCannotSeedOutsideItsKnownStationBracket()
    {
        var body=RegularSolidBlendTests.Roof(.4);Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;var roof=0;var wall=0;
        for(var f=0;f<KernelRuntime.Bodies[slot].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var tag=KernelRuntime.Faces[face].SurfTag;var sf=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)];
            if(sf.Class==SurfaceClass.BSurface)roof=tag;else{var p=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(tag,&p));if(p.basis_set.axis.coord[0]<-.9)wall=tag;}
        }
        int blend=0,contact=0;var command=new EndpointContactCommand{First=roof,Second=wall,Blend=&blend,Contact=&contact};Assert.Equal(0,KernelRuntime.Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,roof));
        var cs=KernelRuntime.GetCurveSlotByTag(contact);ref var record=ref KernelRuntime.Curves[cs];ref var data=ref KernelRuntime.ICurveDataPool[record.DataIndex];var n=data.ChartCount;
        var map=new Span<double>(KernelRuntime.DereferenceBlock(data.ContactChartBlock),6*n-4);var stations=map[..n];var parameters=map.Slice(n,n);var scales=map.Slice(2*n,n-1);var factor=0.0;
        for(var i=1;i<100000;i++)
        {
            var candidate=1+i/257.0;var delta=parameters[^1]*candidate-parameters[^2]*candidate;
            var seed=stations[^2]+(stations[^1]-stations[^2])*delta/delta;
            if(seed<Math.Min(stations[^2],stations[^1])){factor=candidate;break;}
        }
        Assert.True(factor>0,"manufactured positive affine chart map must exercise endpoint rounding");
        for(var i=0;i<n;i++)parameters[i]*=factor;for(var i=0;i<n-1;i++)scales[i]*=factor;
        data.BaseParameter*=factor;data.BaseScale*=factor;record.TMin*=factor;record.TMax*=factor;
        var expected=new PK_VECTOR_s();var actual=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(contact,record.TMin,0,&expected));Assert.Equal(0,KernelRuntime.CurveEval(contact,record.TMax,0,&actual));
        var anchors=new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.HvecBlock),3*(data.StartLimit.HvecCount+n));var last=3*(data.StartLimit.HvecCount+n-1);
        for(var d=0;d<3;d++)Assert.True(Math.Abs(actual.coord[d]-anchors[last+d])<1e-8);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&contact));if(KernelRuntime.IsValidTag(blend))Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }
}
