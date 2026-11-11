using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using Xunit;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ProfileModelingTests : IDisposable
{
    public ProfileModelingTests()
    {
        KernelRuntime.SessionStop();
        var options=new PK_SESSION_start_o_s{o_t_version=1};
        Assert.Equal(0,KernelRuntime.SessionStart(&options));
    }
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false,0.0)]
    [InlineData(true,0.0)]
    [InlineData(false,0.6)]
    [InlineData(true,0.6)]
    public void PolygonWithOptionalHole_ExtrudesIntoClosedManifold(bool hole,double oblique)
    {
        var profile=MakePolygon(hole,false);
        var before=Record(profile);
        Assert.Equal(hole?2:1,KernelRuntime.GetFaceRecord(before.FirstFaceBody).LoopCount);
        var solid=Extrude(profile,oblique);
        var result=Record(solid);var n=hole?8:4;
        Assert.Equal(ParasolidConstants.PK_BODY_type_solid_c,result.BodyType);
        Assert.Equal(n+2,result.FaceCountBody);Assert.Equal(3*n,result.EdgeCountBody);Assert.Equal(2*n,result.VertexCountBody);
        Assert.Equal(2,result.RegionCount);Assert.Equal(2,result.ShellCount);
        AssertManifold(result);
        AssertPublicCheck(solid);
        Assert.Equal(before.EdgeCountBody,Record(profile).EdgeCountBody);
        var document=XtText.DecodeDocument(Transmit(solid));
        Assert.Equal(n+2,document.Nodes.Count(x=>x.Type==(int)XtNodeTypes.Face));
        Assert.Equal(3*n,document.Nodes.Count(x=>x.Type==(int)XtNodeTypes.Edge));
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(true,true)]
    public void PlanarSheetAndObliqueExtrusionReleaseEachOwnedGeometryOnce(bool extrude,bool productFirst)
    {
        var before=PoolCounts();var pointsBefore=KernelRuntime.Points.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        var profile=MakePolygon(false,false);var product=extrude?Extrude(profile,.35):0;
        if(productFirst&&product!=0)Assert.Equal(0,KernelRuntime.EntityDelete(1,&product));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&profile));
        if(!productFirst&&product!=0){AssertPublicCheck(product);Assert.Equal(0,KernelRuntime.EntityDelete(1,&product));}
        Assert.Equal(before,PoolCounts());Assert.Equal(pointsBefore,KernelRuntime.Points.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(-0.8)]
    [InlineData(6.283185307179586)]
    [InlineData(-6.283185307179586)]
    public void PolygonRevolution_HasCapsOnlyForPartialTurn(double angle)
    {
        var profile=MakePolygon(false,true);
        var original=Record(profile);var originalTags=new int[original.EdgeCountBody];var originalEdge=original.FirstEdgeBody;
        for(var i=0;i<originalTags.Length;i++,originalEdge=KernelRuntime.GetEdgeRecord(originalEdge).NextInBody)
            originalTags[i]=KernelRuntime.GetEdgeRecord(originalEdge).Header.Tag;
        var axis=new PK_AXIS1_sf_s();axis.axis.coord[2]=1;
        int count=0,result=0;int* laterals=null;int* bases=null;
        Assert.Equal(0,KernelRuntime.BodySpin(profile,&axis,angle,1,&count,&laterals,&bases,&result));
        Assert.Equal(4,count);Assert.Equal(ParasolidConstants.PK_local_check_ok_c,result);
        var body=Record(profile);var full=Math.Abs(angle)==Math.Tau;
        Assert.Equal(ParasolidConstants.PK_BODY_type_solid_c,body.BodyType);
        Assert.Equal(full?4:6,body.FaceCountBody);Assert.Equal(full?8:12,body.EdgeCountBody);Assert.Equal(full?4:8,body.VertexCountBody);
        AssertManifold(body);
        AssertPublicCheck(profile);
        for(var i=0;i<count;i++)
        {
            int cls=0;Assert.Equal(0,KernelRuntime.EntityAskClass(laterals[i],&cls));Assert.Equal(ParasolidConstants.PK_CLASS_face,cls);
            Assert.Contains(bases[i],originalTags);Assert.Equal(0,KernelRuntime.EntityAskClass(bases[i],&cls));Assert.Equal(ParasolidConstants.PK_CLASS_edge,cls);
        }
        KernelRuntime.MemoryFree(laterals);KernelRuntime.MemoryFree(bases);
        Assert.NotEmpty(Transmit(profile));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CurvedProfile_WireToPlanarFaceToExtrusionRetainsActualCurve(int type)
    {
        var body=MakeCurvedProfile(type);
        var input=Record(body);
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c,input.BodyType);
        Assert.Equal(type==2?1:2,input.EdgeCountBody);
        var solid=Extrude(body,0);
        AssertManifold(Record(solid));
        var document=XtText.DecodeDocument(Transmit(solid));
        Assert.Contains(document.Nodes,n=>n.Type==(int)(type==1?XtNodeTypes.BCurve:XtNodeTypes.Circle));
        Assert.Contains(document.Nodes,n=>n.Type==(int)(type==1?XtNodeTypes.BSurface:XtNodeTypes.Cylinder));
    }

    [Theory]
    [InlineData(false,0.0)]
    [InlineData(true,0.0)]
    [InlineData(false,0.8)]
    [InlineData(true,0.8)]
    [InlineData(false,6.283185307179586)]
    [InlineData(true,6.283185307179586)]
    [InlineData(false,-6.283185307179586)]
    [InlineData(true,-6.283185307179586)]
    public void SplineSweepUsesExactTensorProduct(bool rational,double angle)
    {
        var spin=angle!=0;var profile=MakeCurvedProfile(1,spin,rational);int body;
        if(spin)
        {
            var axis=new PK_AXIS1_sf_s();axis.axis.coord[2]=1;int count=0,result=0;
            Assert.Equal(0,KernelRuntime.BodySpin(profile,&axis,angle,1,&count,null,null,&result));body=profile;
        }
        else body=Extrude(profile,.25);
        var face=Record(body).FirstFaceBody;var surface=KernelRuntime.GetFaceRecord(face).SurfTag;
        var surfaceRecord=KernelRuntime.GetSurfaceByTag(surface);Assert.Equal(SurfaceClass.BSurface,surfaceRecord.Class);
        var surfaceData=KernelRuntime.BSurfaceDataStore[surfaceRecord.DataIndex];
        Assert.Equal((byte)(spin&&Math.Abs(angle)==Math.Tau?1:0),surfaceData.IsUPeriodic);
        const double v=.25;var weight=rational?.6:1;
        var b0=(1-v)*(1-v);var b1=2*weight*v*(1-v);var b2=v*v;var denominator=b0+b1+b2;
        var radius=(2*b0+3*b1+4*b2)/denominator;
        var profileX=(-2*b0+2*b2)/denominator;var profileY=3*b1/denominator;var profileZ=1.5*b1/denominator;
        foreach(var u in new[]{0.0,.5,1.0})
        {
            var uv=new PK_UV_s();uv.param[0]=u;uv.param[1]=v;PK_VECTOR_s point=default;
            Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&point));
            if(spin)
            {
                // The midpoint of a symmetric rational arc lies at its
                // angular midpoint, independently of its knot parameter.
                var theta=angle*u;
                Assert.Equal(radius*Math.Cos(theta),point.coord[0],10);
                Assert.Equal(radius*Math.Sin(theta),point.coord[1],10);
                Assert.Equal(profileZ,point.coord[2],10);
            }
            else
            {
                Assert.Equal(profileX+.5*u,point.coord[0],11);
                Assert.Equal(profileY,point.coord[1],11);
                Assert.Equal(2*Math.Sqrt(1-.25*.25)*u,point.coord[2],11);
            }
        }
        AssertManifold(Record(body));AssertPublicCheck(body);
    }

    [Fact]
    public void ClosedCircleWire_HasNoArtificialVerticesAndOneVoidShell()
    {
        var definition=new PK_CIRCLE_sf_s{radius=2};definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int curve=0,body=0;Assert.Equal(0,KernelRuntime.CircleCreate(&definition,&curve));
        var bounds=new PK_INTERVAL_s();bounds.value[1]=Math.Tau;
        Assert.Equal(0,KernelRuntime.CurveMakeWireBody(curve,bounds,&body));
        var record=Record(body);
        Assert.Equal(0,record.VertexCountBody);Assert.Equal(1,record.EdgeCountBody);Assert.Equal(1,record.ShellCount);Assert.Equal(1,record.RegionCount);
        var edge=KernelRuntime.GetEdgeRecord(record.FirstEdgeBody);
        Assert.Equal(Math.Tau,edge.ParameterEnd);Assert.Equal(1,edge.HasParameterRange);Assert.NotEqual(0,edge.WireShellTag);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&curve));
        int attachedCurve=0;Assert.Equal(0,KernelRuntime.EdgeAskCurve(edge.Header.Tag,&attachedCurve));
        var value=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(attachedCurve,Math.PI/2,0,&value));
        Assert.InRange(Math.Abs(value.coord[1]-2),0,1e-12);
        var document=XtText.DecodeDocument(Transmit(body));
        Assert.DoesNotContain(document.Nodes,n=>n.Type==(int)XtNodeTypes.Vertex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidPlanarProfilesFailBeforePublishingBody(int invalid)
    {
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[4];
        Set(points,0,0,0,0);Set(points,1,2,0,0);Set(points,2,2,2,0);Set(points,3,0,2,0);
        if(invalid==0){Set(points,1,2,2,0);Set(points,2,2,0,0);}
        if(invalid==1)Set(points,2,2,2,.1);
        if(invalid==2)Set(points,1,0,0,0);
        var options=PlanarOptions(false);int body=0;
        Assert.NotEqual(0,KernelRuntime.BodyCreateSheetPlanar(4,points,&options,&body));
        Assert.Equal(0,KernelRuntime.Bodies.AliveCount);
    }

    [Fact]
    public void ExtrudeInPlaneFailsAndKeepsProfile()
    {
        var profile=MakePolygon(false,false);var before=Record(profile);
        var options=ExtrudeOptions();var path=new PK_VECTOR_s();path.coord[0]=1;
        int result=0;var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.NotEqual(0,KernelRuntime.BodyExtrude(profile,path,&options,&result,&tracking,&local));
        Assert.Equal(0,result);Assert.Equal(before.Header.Tag,Record(profile).Header.Tag);Assert.Equal(1,KernelRuntime.Bodies.AliveCount);
    }

    [Fact]
    public void PublicTopologyCheckReportsBrokenFinSenseAndRejectsUnimplementedGeometricChecks()
    {
        var solid=Extrude(MakePolygon(false,false),0);AssertPublicCheck(solid);
        var edge=KernelRuntime.GetEdgeRecord(Record(solid).FirstEdgeBody);
        KernelRuntime.Fins[edge.LastFinEdge].Sense=KernelRuntime.Fins[edge.FirstFinEdge].Sense;
        var options=CheckOptions();int count=0;PK_check_fault_s* faults=null;
        Assert.Equal(0,KernelRuntime.BodyCheck(solid,&options,&count,&faults));
        Assert.True(count>0);Assert.Equal(ParasolidConstants.PK_EDGE_state_bad_face_order_c,faults[0].state);KernelRuntime.MemoryFree(faults);
        options.fa_fa=ParasolidConstants.PK_check_fa_fa_yes_c;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyCheck(solid,&options,&count,&faults));
        Assert.Equal(0,count);Assert.True(faults==null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(18)]
    public void ExtrusionAllocationBudgetPublishesCompleteSolidOrRollsBack(int after)
    {
        var profile=MakePolygon(true,false);var before=PoolCounts();
        KernelRuntime.InjectAllocationFailure(after);
        var options=ExtrudeOptions();var path=new PK_VECTOR_s();path.coord[2]=1;
        int body=0;var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        var error=KernelRuntime.BodyExtrude(profile,path,&options,&body,&tracking,&local);
        KernelRuntime.InjectAllocationFailure(-1);
        // Fault injection counts allocator growth, not a guaranteed number of
        // pool-slot requests. Existing capacity can complete a command without
        // reaching a later budget; that result must still be complete/deletable.
        if(error==0)
        {
            AssertManifold(Record(body));AssertPublicCheck(body);
            Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,KernelRuntime.TopolLocalResultFree(&local));
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        }
        else Assert.Equal(0,body);
        Assert.Equal(before,PoolCounts());
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c,Record(profile).BodyType);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void TwoSidedExtrusionPreservesSignedDistanceBounds(double direction)
    {
        var profile=MakePolygon(true,false);var options=ExtrudeOptions();
        options.start_bound.forward=0;options.start_bound.distance=1;
        var path=new PK_VECTOR_s();path.coord[2]=direction;
        int body=0;var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyExtrude(profile,path,&options,&body,&tracking,&local));
        var record=Record(body);var vertex=record.FirstVertexBody;
        double min=double.PositiveInfinity,max=double.NegativeInfinity;
        for(var i=0;i<record.VertexCountBody;i++,vertex=KernelRuntime.GetVertexRecord(vertex).NextInBody)
        {
            var tag=KernelRuntime.GetVertexRecord(vertex).PointTag;
            var z=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(tag)].Position.Z;min=Math.Min(min,z);max=Math.Max(max,z);
        }
        Assert.Equal(direction>0?-1:-2,min,12);Assert.Equal(direction>0?2:1,max,12);AssertPublicCheck(body);AssertManifold(record);
        Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,KernelRuntime.TopolLocalResultFree(&local));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtrusionTrackingAssociatesCapsAndLateralFacesWithOriginalTopology(bool hole)
    {
        var profile=MakePolygon(hole,false);var input=Record(profile);
        var options=ExtrudeOptions();var path=new PK_VECTOR_s();path.coord[2]=1;
        int product=0;var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyExtrude(profile,path,&options,&product,&tracking,&local));
        Assert.Equal(input.EdgeCountBody+2,tracking.n_track_records);
        var sources=new HashSet<int>();var products=new HashSet<int>();var capCount=0;
        for(var i=0;i<tracking.n_track_records;i++)
        {
            var record=tracking.track_records[i];
            Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c,record.track);
            Assert.Equal(1,record.n_original_topols);Assert.Equal(1,record.n_product_topols);
            int cls=0;Assert.Equal(0,KernelRuntime.EntityAskClass(record.product_topols[0],&cls));
            Assert.Equal(ParasolidConstants.PK_CLASS_face,cls);Assert.True(products.Add(record.product_topols[0]));
            int surface=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(record.product_topols[0],&surface));
            var plane=KernelRuntime.PlaneDataPool[KernelRuntime.GetSurfaceByTag(surface).DataIndex];
            if(record.original_classes[0]==ParasolidConstants.PK_CLASS_face)
            {
                capCount++;Assert.Equal(KernelRuntime.GetFaceRecord(input.FirstFaceBody).Header.Tag,record.original_topols[0]);
                Assert.Equal(1,Math.Abs(plane.NormalZ),12);Assert.Contains(plane.LocationZ,new[]{0.0,2.0});
            }
            else
            {
                Assert.Equal(ParasolidConstants.PK_CLASS_edge,record.original_classes[0]);Assert.True(sources.Add(record.original_topols[0]));
                var source=input.FirstEdgeBody;var matched=-1;
                for(var j=0;j<input.EdgeCountBody;j++,source=KernelRuntime.GetEdgeRecord(source).NextInBody)
                    if(KernelRuntime.GetEdgeRecord(source).Header.Tag==record.original_topols[0]){matched=source;break;}
                Assert.True(matched>=0);
                var vertex=KernelRuntime.GetVertexRecord(KernelRuntime.GetEdgeRecord(matched).StartVertex);
                var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(vertex.PointTag)].Position;
                Assert.InRange(Math.Abs((point.X-plane.LocationX)*plane.NormalX+(point.Y-plane.LocationY)*plane.NormalY
                    +(point.Z-plane.LocationZ)*plane.NormalZ),0,1e-12);
            }
        }
        Assert.Equal(2,capCount);Assert.Equal(input.EdgeCountBody,sources.Count);
        Assert.Equal(Record(product).FaceCountBody,products.Count);
        Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,tracking.n_track_records);Assert.True(tracking.track_records==null);
        Assert.Equal(0,KernelRuntime.TopolLocalResultFree(&local));Assert.True(local.error_entities==null);
    }

    [Fact]
    public void SpinUndoRestoresOriginalSheetHandleAndTopology()
    {
        var body=MakePolygon(false,true);var before=Record(body);var pools=PoolCounts();
        int mark=0;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var axis=new PK_AXIS1_sf_s();axis.axis.coord[2]=1;int count=0,result=0;
        Assert.Equal(0,KernelRuntime.BodySpin(body,&axis,.8,1,&count,null,null,&result));
        Assert.Equal(ParasolidConstants.PK_BODY_type_solid_c,Record(body).BodyType);
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));
        var restored=Record(body);
        Assert.Equal(before.FirstEdgeBody,restored.FirstEdgeBody);Assert.Equal(before.FirstFaceBody,restored.FirstFaceBody);
        Assert.Equal(before.BodyType,restored.BodyType);Assert.Equal(pools,PoolCounts());AssertPublicCheck(body);
        // MarkGoto consumes the active mark after restoring its snapshot.
    }

    [Fact]
    public void FullRevolutionOfProfileHoleCreatesSeparateEnclosedVoidRegion()
    {
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[8];
        Set(points,0,2,0,0);Set(points,1,6,0,0);Set(points,2,6,0,4);Set(points,3,2,0,4);
        Set(points,4,3,0,1);Set(points,5,4.5,0,1);Set(points,6,4.5,0,2.5);Set(points,7,3,0,2.5);
        int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};var options=PlanarOptions(true);options.loops=loops;
        int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSheetPlanar(8,points,&options,&body));
        var axis=new PK_AXIS1_sf_s();axis.axis.coord[2]=1;int count=0,result=0;
        Assert.Equal(0,KernelRuntime.BodySpin(body,&axis,Math.Tau,1,&count,null,null,&result));
        var record=Record(body);Assert.Equal(3,record.RegionCount);Assert.Equal(4,record.ShellCount);
        Assert.Equal(8,record.FaceCountBody);Assert.Equal(16,record.EdgeCountBody);Assert.Equal(8,record.VertexCountBody);
        var solidRegions=0;var region=record.FirstRegion;
        for(var i=0;i<record.RegionCount;i++,region=KernelRuntime.GetRegionRecord(region).NextInBody)
        {
            var r=KernelRuntime.GetRegionRecord(region);
            if(r.IsSolid!=0){solidRegions++;Assert.Equal(2,r.ShellCount);}else Assert.Equal(1,r.ShellCount);
        }
        Assert.Equal(1,solidRegions);AssertPublicCheck(body);AssertManifold(record);
    }

    [Fact]
    public void PlanarCreatorAppliesIndependentLocalToWorldBasis()
    {
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[3];Set(points,0,0,0,0);Set(points,1,2,0,0);Set(points,2,0,1,0);
        var options=PlanarOptions(false);options.basis_set.axis.coord[2]=0;options.basis_set.axis.coord[1]=1;
        options.basis_set.location.coord[0]=5;options.basis_set.location.coord[1]=6;options.basis_set.location.coord[2]=7;
        int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSheetPlanar(3,points,&options,&body));
        var expected=new[]{(5.0,6.0,7.0),(7.0,6.0,7.0),(5.0,6.0,6.0)};
        var record=Record(body);var vertex=record.FirstVertexBody;
        for(var i=0;i<record.VertexCountBody;i++,vertex=KernelRuntime.GetVertexRecord(vertex).NextInBody)
        {
            var p=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.GetVertexRecord(vertex).PointTag)].Position;
            Assert.Contains((p.X,p.Y,p.Z),expected);
        }
        AssertPublicCheck(body);
    }

    private static int MakePolygon(bool hole,bool spin)
    {
        PK_VECTOR_s* points=stackalloc PK_VECTOR_s[8];
        if(spin){Set(points,0,2,0,0);Set(points,1,4,0,0);Set(points,2,4,0,3);Set(points,3,2,0,3);}
        else{Set(points,0,-3,-2,0);Set(points,1,3,-2,0);Set(points,2,3,2,0);Set(points,3,-3,2,0);}
        Set(points,4,-1,-.5,0);Set(points,5,1,-.5,0);Set(points,6,1,.5,0);Set(points,7,-1,.5,0);
        int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};
        var options=PlanarOptions(spin);if(hole)options.loops=loops;
        int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSheetPlanar(hole?8:4,points,&options,&body));return body;
    }

    private static int MakeCurvedProfile(int type,bool spin=false,bool rational=false)
    {
        int* curves=stackalloc int[2];PK_INTERVAL_s* bounds=stackalloc PK_INTERVAL_s[2];
        if(type==1)
        {
            var dimension=rational?4:3;double* poles=stackalloc double[3*dimension];
            for(var i=0;i<3;i++)
            {
                var weight=rational&&i==1?.6:1;var offset=i*dimension;
                poles[offset]=(spin?2+i:2*i-2)*weight;poles[offset+1]=!spin&&i==1?3*weight:0;
                poles[offset+2]=spin&&i==1?1.5*weight:0;if(rational)poles[offset+3]=weight;
            }
            double* knots=stackalloc double[2]{0,1};int* mults=stackalloc int[2]{3,3};
            var definition=new PK_BCURVE_sf_s{degree=2,n_vertices=3,vertex_dim=dimension,is_rational=(byte)(rational?1:0),vertex=poles,n_knots=2,knot=knots,knot_mult=mults,
                knot_type=ParasolidConstants.PK_knot_bezier_ends_c,form=ParasolidConstants.PK_BCURVE_form_arbitrary_c, self_intersecting=ParasolidConstants.PK_self_intersect_false_c};
            Assert.Equal(0,KernelRuntime.BCurveCreate(&definition,&curves[0]));bounds[0].value[1]=1;
            curves[1]=Line(spin?4:2,0,0,-1,0,0);bounds[1].value[1]=spin?2:4;
        }
        else
        {
            var definition=new PK_CIRCLE_sf_s{radius=2};definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
            Assert.Equal(0,KernelRuntime.CircleCreate(&definition,&curves[0]));bounds[0].value[1]=type==2?Math.Tau:Math.PI;
            curves[1]=Line(-2,0,0,1,0,0);bounds[1].value[1]=4;
        }
        var options=new PK_CURVE_make_wire_body_o_s{o_t_version=3,tolerance=1e-8,check=1,want_edges=1,sequential=ParasolidConstants.PK_CURVE_sequential_no_c};
        int body=0,count=0;int* edges=null;
        Assert.Equal(0,KernelRuntime.CurveMakeWireBody2(type==2?1:2,curves,bounds,&options,&body,&count,&edges,null));
        int seed=edges[0],face=0,shared=-1;byte sense=1;
        Assert.Equal(0,KernelRuntime.EdgeMakeFacesFromWire(1,&seed,&sense,&shared,&face));KernelRuntime.MemoryFree(edges);
        int surface=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(face,&surface));Assert.Equal(0,surface);
        var plane=new PK_PLANE_sf_s();plane.basis_set.axis.coord[spin?1:2]=spin?1:type==1?-1:1;plane.basis_set.ref_direction.coord[0]=1;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&surface));Assert.Equal(0,KernelRuntime.FaceAttachSurfaces(1,&face,&surface,&sense));
        return body;
    }

    private static int Line(double x,double y,double z,double dx,double dy,double dz)
    {
        var definition=new PK_LINE_sf_s();definition.basis_set.location.coord[0]=x;definition.basis_set.location.coord[1]=y;definition.basis_set.location.coord[2]=z;
        definition.basis_set.axis.coord[0]=dx;definition.basis_set.axis.coord[1]=dy;definition.basis_set.axis.coord[2]=dz;
        int line=0;Assert.Equal(0,KernelRuntime.LineCreate(&definition,&line));return line;
    }
    private static PK_BODY_create_sheet_planar_o_s PlanarOptions(bool spin)
    {
        var options=new PK_BODY_create_sheet_planar_o_s{o_t_version=1};options.plane.axis.coord[spin?1:2]=1;
        options.basis_set.axis.coord[2]=1;options.basis_set.ref_direction.coord[0]=1;return options;
    }
    private static PK_BODY_extrude_o_s ExtrudeOptions()
    {
        var options=new PK_BODY_extrude_o_s{o_t_version=7};
        options.start_bound.bound=options.end_bound.bound=ParasolidConstants.PK_bound_distance_c;
        options.start_bound.forward=options.end_bound.forward=1;options.end_bound.distance=2;return options;
    }
    private static PK_BODY_check_o_s CheckOptions()=>new()
    {
        o_t_version=6,max_faults=32,geom=ParasolidConstants.PK_check_geom_no_c,bgeom=ParasolidConstants.PK_check_bgeom_no_c,
        mesh=ParasolidConstants.PK_check_mesh_no_c,top_geo=ParasolidConstants.PK_check_top_geo_no_c,size_box=ParasolidConstants.PK_check_size_box_no_c,
        fa_X=ParasolidConstants.PK_check_fa_X_no_c,fa_fa=ParasolidConstants.PK_check_fa_fa_no_c,nmnl_geom=ParasolidConstants.PK_check_nmnl_geom_no_c,
        attribs=ParasolidConstants.PK_check_attribs_no_c,returns=ParasolidConstants.PK_check_returns_0_c,extra_faults=ParasolidConstants.PK_check_extra_faults_0_c,
        loops=ParasolidConstants.PK_check_loops_yes_c,sh=ParasolidConstants.PK_check_sh_yes_c,corrupt=ParasolidConstants.PK_check_corrupt_yes_c,
        missing_geom=ParasolidConstants.PK_check_missing_geom_yes_c,
    };
    private static void AssertPublicCheck(int body)
    {
        var options=CheckOptions();int count=0;PK_check_fault_s* faults=null;
        Assert.Equal(0,KernelRuntime.BodyCheck(body,&options,&count,&faults));Assert.Equal(0,count);Assert.True(faults==null);
    }
    private static int Extrude(int profile,double x)
    {
        var options=ExtrudeOptions();var path=new PK_VECTOR_s();path.coord[0]=x;path.coord[2]=Math.Sqrt(1-x*x);
        int body=0;var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyExtrude(profile,path,&options,&body,&track,&local));Assert.Equal(ParasolidConstants.PK_local_status_ok_c,local.status);
        Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&track));Assert.Equal(0,KernelRuntime.TopolLocalResultFree(&local));return body;
    }
    private static void Set(PK_VECTOR_s* values,int i,double x,double y,double z){values[i].coord[0]=x;values[i].coord[1]=y;values[i].coord[2]=z;}
    private static BodyRecord Record(int body){Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));return KernelRuntime.GetBodyRecord(slot);}
    private static void AssertManifold(BodyRecord body)
    {
        var edge=body.FirstEdgeBody;
        for(var i=0;i<body.EdgeCountBody;i++,edge=KernelRuntime.GetEdgeRecord(edge).NextInBody)
        {
            var e=KernelRuntime.GetEdgeRecord(edge);Assert.Equal(2,e.FinCount);
            var a=KernelRuntime.GetFinRecord(e.FirstFinEdge);var b=KernelRuntime.GetFinRecord(e.LastFinEdge);
            Assert.NotEqual(a.Sense,b.Sense);Assert.Equal(e.LastFinEdge,a.Other);Assert.Equal(e.FirstFinEdge,b.Other);
        }
    }
    private static string Transmit(int body)
    {
        var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};
        var result=new PK_MEMORY_block_s();Assert.Equal(0,KernelRuntime.PartTransmitB(1,&body,&options,&result));
        try{return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(result.bytes,(int)result.n_bytes));}
        finally{KernelRuntime.MemoryBlockFree(&result);}
    }
    private static (int,int,int,int,int,int,int,int,int,int) PoolCounts()
        =>(KernelRuntime.Bodies.AliveCount,KernelRuntime.Regions.AliveCount,KernelRuntime.Shells.AliveCount,KernelRuntime.Faces.AliveCount,
            KernelRuntime.Loops.AliveCount,KernelRuntime.Edges.AliveCount,KernelRuntime.Vertices.AliveCount,KernelRuntime.Fins.AliveCount,
            KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount);
}
