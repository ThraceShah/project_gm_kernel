using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class GeometryTransformTests : IDisposable
{
    public GeometryTransformTests()
    {
        KernelRuntime.SessionStop();
        var options=new PK_SESSION_start_o_s { o_t_version=1 };
        Assert.Equal(0,KernelRuntime.SessionStart(&options));
    }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop(); }

    [Fact]
    public void HomogeneousScale_UsesSdkDenominatorAndDefaultNewGeometryContract()
    {
        var pointSf=new PK_POINT_sf_s();pointSf.position.coord[0]=2;pointSf.position.coord[1]=3;pointSf.position.coord[2]=4;
        int point;Assert.Equal(0,KernelRuntime.PointCreate(&pointSf,&point));
        var sf=Identity();sf.matrix[3]=-.5;sf.matrix[7]=-1;sf.matrix[11]=-1.5;sf.matrix[15]=.5;
        var transform=CreateTransform(sf);
        var options=TransformOptions(false);int output=-1;byte exact=0;
        delegate* unmanaged<int,int*,int,PK_GEOM_transform_o_s*,int*,byte*,int> apply=&KernelExports.PK_GEOM_transform_2;
        Assert.Equal(0,apply(1,&point,transform,&options,&output,&exact));
        Assert.NotEqual(point,output);Assert.Equal((byte)1,exact);
        var result=new PK_POINT_sf_s();Assert.Equal(0,KernelRuntime.PointAsk(output,&result));
        Vector(result.position,3,4,5);
        Assert.Equal(0,KernelRuntime.PointAsk(point,&result));Vector(result.position,2,3,4);
        var asked=new PK_TRANSF_sf_s();Assert.Equal(0,KernelRuntime.TransfAsk(transform,&asked));
        for(var i=0;i<16;i++)Assert.Equal(sf.matrix[i],asked.matrix[i]);
    }

    [Theory]
    [InlineData("plane",1)] [InlineData("bsurface",-1)] [InlineData("swept",-1)] [InlineData("spun",1)]
    [InlineData("cylinder",1)] [InlineData("cone",1)] [InlineData("sphere",1)] [InlineData("torus",1)]
    public void ReflectedOffset_UsesParameterNormalOrientation(string kind,int offsetSign)
    {
        var basis=CreateSurface(kind);var offsetSf=new PK_OFFSET_sf_s { underlying_surface=basis,offset_distance=.25 };
        int offset;Assert.Equal(0,KernelRuntime.OffsetCreate(&offsetSf,&offset));
        var before=EvalSurface(offset,.3,.4);
        var sf=Similarity(-2,2,2,3,-4,5);var transform=CreateTransform(sf);
        int copy;byte exact;Assert.Equal(0,KernelRuntime.GeomTransform(offset,transform,0,&copy,&exact));
        var u=kind is "cylinder" or "cone" or "sphere" or "torus"?-.3:kind is "plane" or "spun"?.6:.3;
        var v=kind=="plane"?-.8:kind=="spun"?-.4:kind is "cylinder" or "cone" or "swept"?.8:.4;
        var after=EvalSurface(copy,u,v);Vector(after,-2*before.coord[0]+3,2*before.coord[1]-4,2*before.coord[2]+5);
        var asked=new PK_OFFSET_sf_s();Assert.Equal(0,KernelRuntime.OffsetAsk(copy,&asked));
        Assert.Equal(offsetSign*.5,asked.offset_distance);
        Vector(EvalSurface(offset,.3,.4),before.coord[0],before.coord[1],before.coord[2]);
    }

    [Fact]
    public void BatchModify_SharedDependencyTransformsExactlyOnce()
    {
        var basis=CreateCurve();var firstSf=new PK_SWEPT_sf_s { curve=basis };firstSf.direction.coord[2]=1;
        var secondSf=new PK_SWEPT_sf_s { curve=basis };secondSf.direction.coord[1]=1;
        int first,second;Assert.Equal(0,KernelRuntime.SweptCreate(&firstSf,&first));Assert.Equal(0,KernelRuntime.SweptCreate(&secondSf,&second));
        var before=EvalCurve(basis,.4);var transform=CreateTransform(Similarity(1,1,1,3,5,7));
        int* inputs=stackalloc int[3] { first,second,basis };int* outputs=stackalloc int[3];byte* exact=stackalloc byte[3];
        var options=TransformOptions(true);
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_sharing,KernelRuntime.GeomTransform2(1,inputs,transform,&options,outputs,exact));
        int* missingOwner=stackalloc int[2] { first,basis };
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_sharing,KernelRuntime.GeomTransform2(2,missingOwner,transform,&options,outputs,exact));
        Vector(EvalCurve(basis,.4),before.coord[0],before.coord[1],before.coord[2]);
        Assert.Equal(0,KernelRuntime.GeomTransform2(3,inputs,transform,&options,outputs,exact));
        Assert.Equal(first,outputs[0]);Assert.Equal(second,outputs[1]);
        Vector(EvalCurve(basis,.4),before.coord[0]+3,before.coord[1]+5,before.coord[2]+7);
        Assert.Equal(2,KernelRuntime.GetCurveByTag(basis).GeometricOwnerCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchNew_PreservesSharedSectionAndExplicitBasisIdentity(bool includeBasis)
    {
        var basis = CreateCurve();
        var firstDefinition = new PK_SWEPT_sf_s { curve = basis };
        firstDefinition.direction.coord[2] = 1;
        var secondDefinition = new PK_SWEPT_sf_s { curve = basis };
        secondDefinition.direction.coord[0] = .6;
        secondDefinition.direction.coord[2] = .8;
        int first, second;
        Assert.Equal(0, KernelRuntime.SweptCreate(&firstDefinition, &first));
        Assert.Equal(0, KernelRuntime.SweptCreate(&secondDefinition, &second));
        var before = EvalCurve(basis, .4);
        var transform = CreateTransform(Similarity(1, 1, 1, 3, 5, 7));
        int* inputs = stackalloc int[3] { first, second, basis };
        int* outputs = stackalloc int[3];
        byte* exact = stackalloc byte[3];
        var count = includeBasis ? 3 : 2;
        var options = TransformOptions(false);
        Assert.Equal(0, KernelRuntime.GeomTransform2(count, inputs, transform, &options, outputs, exact));
        var firstCopy = new PK_SWEPT_sf_s();
        var secondCopy = new PK_SWEPT_sf_s();
        Assert.Equal(0, KernelRuntime.SweptAsk(outputs[0], &firstCopy));
        Assert.Equal(0, KernelRuntime.SweptAsk(outputs[1], &secondCopy));
        Assert.Equal(firstCopy.curve, secondCopy.curve);
        Assert.NotEqual(basis, firstCopy.curve);
        if (includeBasis) Assert.Equal(firstCopy.curve, outputs[2]);
        for (var i = 0; i < count; i++)
        {
            Assert.NotEqual(inputs[i], outputs[i]);
            Assert.Equal((byte)1, exact[i]);
        }
        Assert.Equal(2, KernelRuntime.Curves.AliveCount);
        Assert.Equal(2, KernelRuntime.GetCurveByTag(firstCopy.curve).GeometricOwnerCount);
        Vector(EvalCurve(firstCopy.curve, .4), before.coord[0] + 3, before.coord[1] + 5, before.coord[2] + 7);
        Vector(EvalCurve(basis, .4), before.coord[0], before.coord[1], before.coord[2]);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &first));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &second));
        Vector(EvalCurve(firstCopy.curve, .4), before.coord[0] + 3, before.coord[1] + 5, before.coord[2] + 7);
    }

    [Fact]
    public void ModifySpCurve_MapsExplicitUvDependencyListedBeforeItsOwner()
    {
        var plane=CreateSurface("plane");double* poles=stackalloc double[4] {0,0,1,.5};double* knots=stackalloc double[2] {0,1};int* mult=stackalloc int[2] {2,2};
        var uvSf=new PK_BCURVE_sf_s {degree=1,n_vertices=2,vertex_dim=2,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c};
        int uv;Assert.Equal(0,KernelRuntime.BCurveCreate(&uvSf,&uv));var spSf=new PK_SPCURVE_sf_s {surf=plane,curve=uv};int sp;Assert.Equal(0,KernelRuntime.SpCurveCreate(&spSf,&sp));
        var before=EvalCurve(sp,.3);var transform=CreateTransform(Similarity(-2,2,2,3,5,7));
        int* inputs=stackalloc int[3] {uv,plane,sp};int* outputs=stackalloc int[3];byte* exact=stackalloc byte[3];var options=TransformOptions(true);
        Assert.Equal(0,KernelRuntime.GeomTransform2(3,inputs,transform,&options,outputs,exact));
        Vector(EvalCurve(sp,.3),-2*before.coord[0]+3,2*before.coord[1]+5,2*before.coord[2]+7);
    }

    [Fact]
    public void RationalCopy_IndependentPayloadSurvivesSourceDeleteAndUndoRestoresInPlaceChange()
    {
        var source=CreateCurve();var original=EvalCurve(source,.4);int copy;
        delegate* unmanaged<int,int*,int> clone=&KernelExports.PK_ENTITY_copy;
        Assert.Equal(0,clone(source,&copy));
        var a=KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(source).DataIndex];
        var b=KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(copy).DataIndex];
        Assert.NotEqual(a.VertexBlock,b.VertexBlock);Assert.NotEqual(a.KnotBlock,b.KnotBlock);Assert.NotEqual(a.KnotMultBlock,b.KnotMultBlock);
        var transform=CreateTransform(Similarity(-2,2,2,3,5,7));int mark;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        Modify(copy,transform);Vector(EvalCurve(copy,.4),-2*original.coord[0]+3,2*original.coord[1]+5,2*original.coord[2]+7);
        Vector(EvalCurve(source,.4),original.coord[0],original.coord[1],original.coord[2]);
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));Vector(EvalCurve(copy,.4),original.coord[0],original.coord[1],original.coord[2]);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&source));
        Modify(copy,transform);Vector(EvalCurve(copy,.4),-2*original.coord[0]+3,2*original.coord[1]+5,2*original.coord[2]+7);
    }

    [Theory]
    [InlineData("circle")] [InlineData("ellipse")] [InlineData("plane")] [InlineData("cylinder")]
    [InlineData("cone")] [InlineData("sphere")] [InlineData("torus")]
    public void OverflowedAnalyticLocation_FailsWithoutPublishingOrMutating(string kind)
    {
        var geometry=CreateAnalyticAtLargeOrigin(kind);
        var before=kind is "circle" or "ellipse"?EvalCurve(geometry,0):EvalSurface(geometry,0,0);
        var transform=CreateTransform(Similarity(1,1,1,1e308,0,0));
        var options=TransformOptions(true);int output=-17;byte exact=23;
        Assert.Equal(ParasolidConstants.PK_ERROR_failed_to_transform,KernelRuntime.GeomTransform2(1,&geometry,transform,&options,&output,&exact));
        Assert.Equal(-17,output);Assert.Equal((byte)23,exact);
        var after=kind is "circle" or "ellipse"?EvalCurve(geometry,0):EvalSurface(geometry,0,0);
        Assert.Equal(before.coord[0],after.coord[0]);Assert.True(double.IsFinite(after.coord[0]));
    }

    [Fact]
    public void RadiusUnderflow_IsRejectedAndSubnormalDirectionStaysFinite()
    {
        var lineSf=new PK_LINE_sf_s();lineSf.basis_set.axis.coord[0]=1;
        int line;Assert.Equal(0,KernelRuntime.LineCreate(&lineSf,&line));
        var transform=CreateTransform(Similarity(double.Epsilon,double.Epsilon,double.Epsilon,0,0,0));
        Modify(line,transform);
        var lineData=KernelRuntime.LineDataPool[KernelRuntime.GetCurveByTag(line).DataIndex];
        Assert.Equal(1,lineData.AxisX);Assert.Equal(0,lineData.AxisY);Assert.Equal(0,lineData.AxisZ);
        var circleSf=new PK_CIRCLE_sf_s { basis_set=Axis(),radius=.125 };int circle;Assert.Equal(0,KernelRuntime.CircleCreate(&circleSf,&circle));
        var options=TransformOptions(true);int output=0;byte exact=0;
        Assert.Equal(ParasolidConstants.PK_ERROR_failed_to_transform,KernelRuntime.GeomTransform2(1,&circle,transform,&options,&output,&exact));
        Assert.Equal(.125,KernelRuntime.CircleDataPool[KernelRuntime.GetCurveByTag(circle).DataIndex].Radius);
    }

    [Fact]
    public void BodyCopyAndTransform_PreserveSourceAndUndoAllGeometryAndTolerance()
    {
        int body;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(1,2,3,null,&body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var sourceSlot));
        var vertex=KernelRuntime.Bodies[sourceSlot].FirstVertexBody;
        var sourcePoint=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position;
        int copy;Assert.Equal(0,KernelRuntime.EntityCopy(body,&copy));Assert.True(KernelRuntime.TryResolveBodySlot(copy,out var copySlot));
        var copyVertex=KernelRuntime.Bodies[copySlot].FirstVertexBody;var edge=KernelRuntime.Bodies[copySlot].FirstEdgeBody;var face=KernelRuntime.Bodies[copySlot].FirstFaceBody;
        var copyPoint=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[copyVertex].PointTag)].Position;
        KernelRuntime.Vertices[copyVertex].Tolerance=1e-7;KernelRuntime.Edges[edge].Tolerance=2e-7;KernelRuntime.Faces[face].Tolerance=3e-7;
        int mark;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var transform=CreateTransform(Similarity(-2,2,2,3,5,7));int count;int* replaced;byte* exact;
        Assert.Equal(0,KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact));
        var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[copyVertex].PointTag)].Position;
        Assert.Equal(-2*copyPoint.X+3,point.X,12);Assert.Equal(2*copyPoint.Y+5,point.Y,12);Assert.Equal(2*copyPoint.Z+7,point.Z,12);
        Assert.Equal(2e-7,KernelRuntime.Vertices[copyVertex].Tolerance);Assert.Equal(4e-7,KernelRuntime.Edges[edge].Tolerance);Assert.Equal(6e-7,KernelRuntime.Faces[face].Tolerance);
        Assert.Equal(sourcePoint.X,KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position.X);
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));
        point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[copyVertex].PointTag)].Position;
        Assert.Equal(copyPoint.X,point.X);Assert.Equal(1e-7,KernelRuntime.Vertices[copyVertex].Tolerance);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.True(KernelRuntime.IsValidTag(copy));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.Equal(0,KernelRuntime.Points.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public void FailedCopyOrTransform_UnwindsNewGeometryAndKeepsSource(int fault)
    {
        var source=CreateSurface("bsurface");var before=EvalSurface(source,.3,.4);var transform=CreateTransform(Similarity(-2,2,2,3,5,7));
        var surfaceCount=KernelRuntime.Surfaces.AliveCount;var dataCount=KernelRuntime.BSurfaceDataStore.AliveCount;
        int result,error;byte exact;KernelRuntime.InjectAllocationFailure(fault);
        try { error=KernelRuntime.GeomTransform(source,transform,0,&result,&exact); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if(error==0)Assert.Equal(0,KernelRuntime.EntityDelete(1,&result));
        else { Assert.Equal(ParasolidConstants.PK_ERROR_memory_full,error);Assert.Equal(0,result);Assert.Equal((byte)0,exact); }
        Assert.Equal(surfaceCount,KernelRuntime.Surfaces.AliveCount);Assert.Equal(dataCount,KernelRuntime.BSurfaceDataStore.AliveCount);
        Vector(EvalSurface(source,.3,.4),before.coord[0],before.coord[1],before.coord[2]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ICurveCopyAndSimilarity_ScaleNativeLengthParameterAndPreserveAllUvWitnesses(bool reflection)
    {
        var source=CreateICurve();var t=.4;var before=stackalloc PK_VECTOR_s[3];Assert.Equal(0,KernelRuntime.CurveEval(source,t,2,before));
        var transform=CreateTransform(Similarity(reflection?-2:2,2,2,3,5,7));int copy;byte exact;
        Assert.Equal(0,KernelRuntime.GeomTransform(source,transform,0,&copy,&exact));
        var data=KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(copy).DataIndex];
        var original=KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(source).DataIndex];
        Assert.Equal(original.BaseParameter*2,data.BaseParameter);Assert.Equal(original.BaseScale,data.BaseScale);
        Assert.NotEqual(original.HvecBlock,data.HvecBlock);Assert.NotEqual(original.UvValueBlock,data.UvValueBlock);
        Assert.NotEqual(original.Surface0Tag,data.Surface0Tag);Assert.NotEqual(original.Surface1Tag,data.Surface1Tag);
        Assert.Equal(8,data.UvValueCount);
        var uv=new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.UvValueBlock),data.UvValueCount);
        var bsIndex=reflection?2:0;var planeIndex=reflection?0:2;
        Assert.Equal(0,uv[bsIndex]);Assert.Equal(.5,uv[bsIndex+1]);Assert.Equal(1,uv[bsIndex+4]);Assert.Equal(.5,uv[bsIndex+5]);
        Assert.Equal(2,uv[planeIndex+4]);Assert.Equal(reflection?2:-2,uv[planeIndex+5]);
        var after=stackalloc PK_VECTOR_s[3];var error=KernelRuntime.CurveEval(copy,t*2,2,after);
        Assert.True(error==0,$"ICurve transform evaluation error={error}, chart={KernelRuntime.LastChartBuildFailure}, report={KernelRuntime.LastICurveEvalReport.Status}/{KernelRuntime.LastICurveEvalReport.Detail}");
        for(var order=0;order<3;order++){var factor=2/Math.Pow(2,order);Vector(after[order],(reflection?-factor:factor)*before[order].coord[0]+(order==0?3:0),factor*before[order].coord[1]+(order==0?5:0),factor*before[order].coord[2]+(order==0?7:0));}
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&source));Assert.Equal(0,KernelRuntime.CurveEval(copy,t*2,2,after));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.False(KernelRuntime.IsValidTag(data.Surface0Tag));Assert.False(KernelRuntime.IsValidTag(data.Surface1Tag));
    }

    private static void Modify(int geometry,int transform)
    { var options=TransformOptions(true);int result;byte exact;Assert.Equal(0,KernelRuntime.GeomTransform2(1,&geometry,transform,&options,&result,&exact));Assert.Equal(geometry,result);Assert.Equal((byte)1,exact); }
    private static PK_GEOM_transform_o_s TransformOptions(bool modify)=>new() { o_t_version=1,tolerance=0,modify=modify?ParasolidConstants.PK_GEOM_transform_modify_c:ParasolidConstants.PK_GEOM_transform_new_c,want_out_geoms=1,want_exact=1 };
    private static PK_TRANSF_sf_s Identity()=>Similarity(1,1,1,0,0,0);
    private static PK_TRANSF_sf_s Similarity(double x,double y,double z,double tx,double ty,double tz)
    { var sf=new PK_TRANSF_sf_s();sf.matrix[0]=x;sf.matrix[5]=y;sf.matrix[10]=z;sf.matrix[3]=tx;sf.matrix[7]=ty;sf.matrix[11]=tz;sf.matrix[15]=1;return sf; }
    private static int CreateTransform(PK_TRANSF_sf_s sf) { int tag;Assert.Equal(0,KernelRuntime.TransfCreate(&sf,&tag));return tag; }
    private static PK_AXIS2_sf_s Axis() { var result=new PK_AXIS2_sf_s();result.axis.coord[2]=1;result.ref_direction.coord[0]=1;return result; }
    private static PK_VECTOR_s EvalCurve(int curve,double t) { var result=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(curve,t,0,&result));return result; }
    private static PK_VECTOR_s EvalSurface(int surface,double u,double v)
    { var uv=new PK_UV_s();uv.param[0]=u;uv.param[1]=v;var result=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(surface,uv,0,0,0,&result));return result; }
    private static void Vector(PK_VECTOR_s value,double x,double y,double z)
    { Assert.InRange(Math.Abs(value.coord[0]-x),0,2e-8);Assert.InRange(Math.Abs(value.coord[1]-y),0,2e-8);Assert.InRange(Math.Abs(value.coord[2]-z),0,2e-8); }
    private static int CreateCurve()
    {
        double* poles=stackalloc double[12] {0,0,0,1, 2,2,0,2, 2,0,1,1};double* knots=stackalloc double[2] {0,1};int* mult=stackalloc int[2] {3,3};
        var sf=new PK_BCURVE_sf_s {degree=2,n_vertices=3,vertex_dim=4,is_rational=1,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c};
        int curve;Assert.Equal(0,KernelRuntime.BCurveCreate(&sf,&curve));return curve;
    }
    private static int CreateSurface(string kind)
    {
        var axis=Axis();int surface=0;
        switch(kind)
        {
            case "plane":var plane=new PK_PLANE_sf_s {basis_set=axis};Assert.Equal(0,KernelRuntime.PlaneCreate(&plane,&surface));break;
            case "cylinder":var cylinder=new PK_CYL_sf_s {basis_set=axis,radius=2};Assert.Equal(0,KernelRuntime.CylCreate(&cylinder,&surface));break;
            case "cone":var cone=new PK_CONE_sf_s {basis_set=axis,radius=2,semi_angle=.3};Assert.Equal(0,KernelRuntime.ConeCreate(&cone,&surface));break;
            case "sphere":var sphere=new PK_SPHERE_sf_s {basis_set=axis,radius=2};Assert.Equal(0,KernelRuntime.SphereCreate(&sphere,&surface));break;
            case "torus":var torus=new PK_TORUS_sf_s {basis_set=axis,major_radius=3,minor_radius=1};Assert.Equal(0,KernelRuntime.TorusCreate(&torus,&surface));break;
            case "swept":
                var ellipse=new PK_ELLIPSE_sf_s {basis_set=axis,R1=2,R2=1};int section;Assert.Equal(0,KernelRuntime.EllipseCreate(&ellipse,&section));
                var swept=new PK_SWEPT_sf_s {curve=section};swept.direction.coord[2]=1;Assert.Equal(0,KernelRuntime.SweptCreate(&swept,&surface));break;
            case "spun":
                var line=new PK_LINE_sf_s();line.basis_set.location.coord[0]=2;line.basis_set.axis.coord[2]=1;int profile;Assert.Equal(0,KernelRuntime.LineCreate(&line,&profile));
                var spun=new PK_SPUN_sf_s {curve=profile};spun.axis.axis.coord[2]=1;Assert.Equal(0,KernelRuntime.SpunCreate(&spun,&surface));break;
            case "bsurface":
                double* poles=stackalloc double[18] {0,-1,0,0,1,0, .5,-1,0,.5,1,0, 1,-1,1,1,1,1};
                double* knots=stackalloc double[2] {0,1};int* um=stackalloc int[2] {3,3};int* vm=stackalloc int[2] {2,2};
                var bs=new PK_BSURF_sf_s {u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
                    u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,u_knot_type=ParasolidConstants.PK_knot_bezier_ends_c,v_knot_type=ParasolidConstants.PK_knot_bezier_ends_c,
                    form=ParasolidConstants.PK_BSURF_form_arbitrary_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
                Assert.Equal(0,KernelRuntime.BSurfCreate(&bs,&surface));break;
        }
        return surface;
    }
    private static int CreateAnalyticAtLargeOrigin(string kind)
    {
        if(kind is "circle" or "ellipse")
        {
            var axis=Axis();axis.location.coord[0]=1e308;int curve;
            if(kind=="circle") {var sf=new PK_CIRCLE_sf_s {basis_set=axis,radius=2};Assert.Equal(0,KernelRuntime.CircleCreate(&sf,&curve));}
            else {var sf=new PK_ELLIPSE_sf_s {basis_set=axis,R1=3,R2=2};Assert.Equal(0,KernelRuntime.EllipseCreate(&sf,&curve));}
            return curve;
        }
        var surface=CreateSurface(kind);var transform=CreateTransform(Similarity(1,1,1,1e308,0,0));Modify(surface,transform);return surface;
    }
    private static int CreateICurve()
    {
        var bs=CreateSurface("bsurface");var sf=new PK_PLANE_sf_s();sf.basis_set.axis.coord[1]=1;sf.basis_set.ref_direction.coord[0]=1;
        int plane;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&plane));KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(plane)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
        var input=new IcurveDecodeInput {Surface0Tag=bs,Surface1Tag=plane,BaseParameter=0,BaseScale=1,ChartCount=2,ChartHvecs=[0,0,0,1,0,1],
            Start=new IcurveLimitInput {Type=LimitType.Help,TermUse=LimitTermUse.Unset,Hvecs=[0,0,0]},End=new IcurveLimitInput {Type=LimitType.Help,TermUse=LimitTermUse.Unset,Hvecs=[1,0,1]},
            UvType=IntersectionUvType.Both,UvValues=[0,.5,0,0,1,.5,1,-1],ChordalError=1e-5,AngularError=1e-5,Sense=ParasolidConstants.PK_TOPOL_sense_positive_c};
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.DecodeIcurve(input,out var data,out _,out _));Assert.Equal(AlgorithmStatus.Success,KernelRuntime.TryBindICurveEntity(data,out var curve));return curve;
    }
}
