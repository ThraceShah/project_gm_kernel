global using SurfTag=System.Int32;
global using CurveTag=System.Int32;
using System.Reflection;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RollingBlendContinuationTests:IDisposable
{
    private delegate AlgorithmStatus ChartWitness(in NestedBlendProgram program,BlendSurfaceIndex surface,SurfTag tag,
        in KernelVector3 point,double u,double v,out TaylorJet distance,out TaylorVector normal);
    private static readonly ChartWitness CheckWitness=typeof(KernelRuntime).GetNestedType("NestedProgramCompiler",BindingFlags.NonPublic)!
        .GetMethod("ChartSupportWitness",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<ChartWitness>();
    public RollingBlendContinuationTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    private struct ContinuationCommand:IKernelCommand
    {
        internal SurfTag First,Second;
        internal SurfTag* Result;
        internal CurveTag* Contact;
        internal double Radius;
        public int Execute()
        {
            var owns=KernelRuntime.EnsureCommandScratch();
            try
            {
                var options=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1};options.help.coord[1]=.5;options.help.coord[2]=1;
                Span<AnalyticSurface> planes=stackalloc AnalyticSurface[2];planes[0]=new(SurfaceClass.Plane,default,Vector(0,-1,0),Vector(1,0,0));planes[1]=new(SurfaceClass.Plane,Vector(0,1,0),Vector(0,1,0),Vector(1,0,0));
                var error=KernelRuntime.CreateFiniteRollingBlend(First,-Radius,Second,-Radius,Vector(0,0,1),Vector(0,1,1),&options,Result,planes);if(error!=0)return error;
                AssertChartWitnesses(*Result,First);
                if(Contact!=null){error=KernelRuntime.CreateBlendContactCurve(*Result,0,out var curve);if(error!=0)return error;*Contact=curve;}
                return 0;
            }
            finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
        }
    }

    [Theory]
    [InlineData(.2,.05)] [InlineData(.4,.1)] [InlineData(.8,.15)]
    public void FiniteCapDomainExtendsTheRollingBranchPastNaturalSplineEnds(double bulge,double radius)
    {
        var body=RegularSolidBlendTests.Roof(bulge);Supports(body,out var roof,out var wall);int blend=0,contact=0;
        var command=new ContinuationCommand{First=roof,Second=wall,Result=&blend,Radius=radius,Contact=&contact};
        Assert.Equal(0,KernelRuntime.Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,roof));
        var data=KernelRuntime.BlendedEdgeDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)].DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        Assert.True(Math.Min(charts[0].Position.Y,charts[^1].Position.Y)<-radius);Assert.True(Math.Max(charts[0].Position.Y,charts[^1].Position.Y)>1+radius);
        Assert.Contains(charts.ToArray(),c=>c.FirstV>1||c.FirstV<0);
        var contactData=KernelRuntime.ICurveDataPool[KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(contact)].DataIndex];
        var uvValues=new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(contactData.UvValueBlock),contactData.UvValueCount);var outside=false;
        for(var i=1;i<uvValues.Length;i+=2)outside|=uvValues[i]<0||uvValues[i]>1;Assert.True(outside);
        var original=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(roof)];Assert.Equal(0,original.VMin);Assert.Equal(1,original.VMax);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&contact));if(KernelRuntime.IsValidTag(blend))Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void ClosedSplineTraceKeepsTheInitialPointAndUvOnItsClosingRow()
    {
        double* poles=stackalloc double[27];double* knots=stackalloc double[2]{0,1};int* multiplicities=stackalloc int[2]{3,3};
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {var at=3*(3*i+j);poles[at]=i-1;poles[at+1]=j-1;poles[at+2]=(i==1?-1:1)+(j==1?-1:1);}
        var definition=new PK_BSURF_sf_s{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=3,vertex=poles,
            n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=multiplicities,v_knot_mult=multiplicities,
            form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        SurfTag bowl=0,plane=0,blend=0;Assert.Equal(0,KernelRuntime.BSurfCreate(&definition,&bowl));
        var planeDefinition=new PK_PLANE_sf_s();planeDefinition.basis_set.location.coord[2]=.5;planeDefinition.basis_set.axis.coord[2]=1;planeDefinition.basis_set.ref_direction.coord[0]=1;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&planeDefinition,&plane));
        var start=new PK_VECTOR_s();start.coord[0]=.7;start.coord[2]=.49;var end=new PK_VECTOR_s();end.coord[1]=.7;end.coord[2]=.49;
        var options=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1,help=start};
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(bowl,.05,plane,.05,start,end,&options,&blend));
        var owns=KernelRuntime.EnsureCommandScratch();
        try
        {
            AssertChartWitnesses(blend,bowl);
            var data=KernelRuntime.BlendedEdgeDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)].DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);
            var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
            Assert.Equal(LimitType.Help,spine.StartType);Assert.Equal(LimitType.Help,spine.EndType);
            Assert.Equal(charts[0].Position,charts[^1].Position);Assert.Equal(charts[0].FirstU,charts[^1].FirstU);Assert.Equal(charts[0].FirstV,charts[^1].FirstV);
        }
        finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&blend));Assert.Equal(0,KernelRuntime.EntityDelete(1,&bowl));Assert.Equal(0,KernelRuntime.EntityDelete(1,&plane));
        Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    private static void AssertChartWitnesses(SurfTag blend,SurfTag roof)
    {
        var data=KernelRuntime.BlendedEdgeDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)].DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        var carrier=KernelRuntime.ICurveDataPool[KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(data.SpineTag)].DataIndex];
        foreach(var chart in charts)
            Assert.Equal(AlgorithmStatus.Success,CheckWitness(program,spine.First,carrier.Surface0Tag,chart.Position,chart.FirstU,chart.FirstV,out _,out _));
        var middle=charts[charts.Length/2];var basis=program.Spline(program.Surfaces[spine.First]);
        Assert.NotEqual(AlgorithmStatus.Success,CheckWitness(program,spine.First,carrier.Surface0Tag,middle.Position,
            middle.FirstU+.2*(basis.UEnd-basis.UStart),middle.FirstV,out _,out _));
        Assert.NotEqual(AlgorithmStatus.Success,CheckWitness(program,spine.First,roof,middle.Position,middle.FirstU,middle.FirstV,out _,out _));
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
