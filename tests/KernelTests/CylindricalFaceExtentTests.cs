using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class CylindricalFaceExtentTests:IDisposable
{
    public CylindricalFaceExtentTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FullCylinderFaceIncludesItsFiniteAxialRangeWithoutVertexSamples(bool rotated)
    {
        var basis=new PK_AXIS2_sf_s();basis.location.coord[0]=2;basis.location.coord[1]=-3;basis.location.coord[2]=4;
        basis.axis.coord[rotated?0:2]=1;basis.ref_direction.coord[rotated?1:0]=1;int body;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(.75,2,&basis,&body));var face=Face(body);
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedCylindricalFaceExtent(face,out var box));
        if(rotated){Assert.True(box.XLo<=2&&box.XHi>=4&&box.YLo<=-3.75&&box.YHi>=-2.25&&box.ZLo<=3.25&&box.ZHi>=4.75);Assert.True(box.XHi<4.000001);}
        else{Assert.True(box.ZLo<=4&&box.ZHi>=6&&box.XLo<=1.25&&box.XHi>=2.75&&box.YLo<=-3.75&&box.YHi>=-2.25);Assert.True(box.ZHi<6.000001);}
        var before=GC.GetAllocatedBytesForCurrentThread();var status=KernelRuntime.CertifiedCylindricalFaceExtent(face,out _);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Fact]
    public void PartialCylinderAcrossThePeriodicSeamIsEnclosed()
    {
        var body=Sheet(5.8,6.8,-1,2);var face=Face(body);
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedCylindricalFaceExtent(face,out var box));Assert.True(box.ZLo<=-1&&box.ZHi>=2&&box.XLo<=-1&&box.XHi>=1&&box.YLo<=-1&&box.YHi>=1);
    }

    [Fact]
    public void RationalBoundaryInteriorAxialExtremumIsNotBoundByItsVertices()
    {
        var body=Sheet(0,Math.PI*.5,0,1);var face=Face(body);var top=TopEdge(body);var old=KernelRuntime.Edges[top].CurveTag;var edge=KernelRuntime.Edges[top];
        double* poles=stackalloc double[12]{1,0,1,1,Math.Sqrt(.5),Math.Sqrt(.5),3*Math.Sqrt(.5),Math.Sqrt(.5),0,1,1,1};
        if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(old)].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c)for(var d=0;d<4;d++)(poles[d],poles[8+d])=(poles[8+d],poles[d]);
        double* knots=stackalloc double[2]{edge.ParameterStart,edge.ParameterEnd};int* mult=stackalloc int[2]{3,3};
        var definition=new PK_BCURVE_sf_s{degree=2,n_vertices=3,vertex_dim=4,is_rational=1,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type=ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=ParasolidConstants.PK_self_intersect_unset_c};int curve;Assert.Equal(0,KernelRuntime.BCurveCreate(&definition,&curve));
        var tag=edge.Header.Tag;Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(tag));Assert.Equal(0,KernelRuntime.EdgeAttachCurves(1,&tag,&curve));
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedCylindricalFaceExtent(face,out var box));
        var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,(knots[0]+knots[1])*.5,0,&point));Assert.True(point.coord[2]>1.8&&box.ZHi>=point.coord[2]);
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var v=KernelRuntime.Bodies[slot].FirstVertexBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].VertexCountBody;i++,v=KernelRuntime.Vertices[v].NextInBody)Assert.True(KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[v].PointTag)].Position.Z<=1);
    }

    internal static int Sheet(double first,double last,double lower,double upper)
    {
        var sf=new PK_CYL_sf_s{radius=1};sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;int cylinder;Assert.Equal(0,KernelRuntime.CylCreate(&sf,&cylinder));
        var uv=new PK_UVBOX_s();uv.param[0]=first;uv.param[1]=lower;uv.param[2]=last;uv.param[3]=upper;int body;Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(cylinder,uv,&body));return body;
    }
    internal static int Face(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;
        for(var f=0;f<KernelRuntime.Bodies[slot].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].Class==SurfaceClass.Cylinder)return face;
        throw new InvalidOperationException("No cylinder face");
    }
    internal static int TopEdge(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var e=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,e=KernelRuntime.Edges[e].NextInBody)
        {
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[e].CurveTag)];if(curve.Class!=CurveClass.Circle)continue;
            if(KernelRuntime.CircleDataPool[curve.DataIndex].CenterZ==1)return e;
        }
        throw new InvalidOperationException("No upper circle");
    }
}
