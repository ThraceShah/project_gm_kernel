using ProjectGmKernel.Native.Generated;
namespace ProjectGmKernel.Native.Runtime;
internal static unsafe partial class KernelRuntime
{
    public static int BCurveCreate(PK_BCURVE_sf_s* sf, CurveTag* curve)
    {
        if (Dispatcher.IsExecuting) return BCurveCreateImplementation(sf, curve);
        var command = new BCurveCreateCommand { Definition = sf, Curve = curve };
        return Dispatch(ApiId.BCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int CurveEval(CurveTag curve, double t, DerivativeOrder order, PK_VECTOR_s* output)
    {
        if (Dispatcher.IsExecuting) return CurveEvalImplementation(curve, t, order, output);
        var command = new CurveEvalCommand { Curve = curve, Parameter = t, Order = order, Output = output };
        return Dispatch(ApiId.CurveEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, curve);
    }
    public static int CurveEvalWithTangent(CurveTag curve, double t, DerivativeOrder order,
        PK_VECTOR_s* output, PK_VECTOR_s* tangent)
    {
        if (Dispatcher.IsExecuting) return CurveEvalWithTangentImplementation(curve, t, order, output, tangent);
        var command = new CurveEvalWithTangentCommand { Curve = curve, Parameter = t, Order = order, Output = output, Tangent = tangent };
        return Dispatch(ApiId.CurveEvalWithTangent, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, curve);
    }
    public static int SurfEval(SurfTag surface, PK_UV_s uv, DerivativeOrder uOrder, DerivativeOrder vOrder,
        KernelLogical triangular, PK_VECTOR_s* output)
    {
        if (Dispatcher.IsExecuting) return SurfEvalImplementation(surface, uv, uOrder, vOrder, triangular, output);
        var command = new SurfEvalCommand { Surface = surface, Parameter = uv, UOrder = uOrder, VOrder = vOrder, Triangular = triangular, Output = output };
        return Dispatch(ApiId.SurfEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, surface);
    }
    public static int SessionStart(PK_SESSION_start_o_s* options)
    {
        if (Dispatcher.IsExecuting) return SessionStartImplementation(options);
        var command = new SessionStartCommand { Options = options };
        return Dispatch(ApiId.SessionStart, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int SessionStop()
    {
        if (Dispatcher.IsExecuting)
        {
            var context = ThreadContext();
            if (context != null && context->InKernel != 0) return ParasolidConstants.PK_ERROR_bad_value;
            return SessionStopImplementation();
        }
        var command = new SessionStopCommand {  };
        return Dispatch(ApiId.SessionStop, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int PointCreate(PK_POINT_sf_s* pointSf, int* pointTag)
    {
        if (Dispatcher.IsExecuting) return PointCreateImplementation(pointSf, pointTag);
        var command = new PointCreateCommand { PointSf = pointSf, Point = pointTag };
        return Dispatch(ApiId.PointCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int EntityAskClass(int entityTag, int* classCode)
    {
        if (Dispatcher.IsExecuting) return EntityAskClassImplementation(entityTag, classCode);
        var command = new EntityAskClassCommand { Entity = entityTag, Class = classCode };
        return Dispatch(ApiId.EntityAskClass, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, entityTag);
    }
    public static int EntityDelete(int nEntities, int* entities)
    {
        if (Dispatcher.IsExecuting) return EntityDeleteImplementation(nEntities, entities);
        var command = new EntityDeleteCommand { EntityCount = nEntities, Entities = entities };
        return Dispatch(ApiId.EntityDelete, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int EntityAskPartition(EntityTag entityTag, PartitionSlot* partition)
    {
        if (Dispatcher.IsExecuting) return EntityAskPartitionImplementation(entityTag, partition);
        var command = new EntityAskPartitionCommand { Entity = entityTag, Partition = partition };
        return Dispatch(ApiId.EntityAskPartition, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, entityTag);
    }
    public static int SessionAskCurrentPartition(PartitionSlot* partition)
    {
        if (Dispatcher.IsExecuting) return SessionAskCurrentPartitionImplementation(partition);
        var command = new SessionAskCurrentPartitionCommand { Partition = partition };
        return Dispatch(ApiId.SessionAskCurrentPartition, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, 0);
    }
    public static int BodyCreateTopology2(
        int nTopols, PK_CLASS_t* classes,
        int nRelations, int* parents, int* children, int* senses,
        PK_BODY_create_topology_2_o_s* options,
        PK_BODY_create_topology_2_r_s* results)
    {
        if (Dispatcher.IsExecuting) return BodyCreateTopology2Implementation(nTopols, classes, nRelations, parents, children, senses, options, results);
        var command = new BodyCreateTopology2Command { TopologyCount = nTopols, Classes = classes, RelationCount = nRelations, Parents = parents, Children = children, Senses = senses, Options = options, Results = results };
        return Dispatch(ApiId.BodyCreateTopology2, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyAskShells(int bodyTag, int* nShells, int** shells)
    {
        if (Dispatcher.IsExecuting) return BodyAskShellsImplementation(bodyTag, nShells, shells);
        var command = new BodyAskShellsCommand { Body = bodyTag, ShellCount = nShells, Shells = shells };
        return Dispatch(ApiId.BodyAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int BodyAskFaces(int bodyTag, int* nFaces, int** faces)
    {
        if (Dispatcher.IsExecuting) return BodyAskFacesImplementation(bodyTag, nFaces, faces);
        var command = new BodyAskFacesCommand { Body = bodyTag, FaceCount = nFaces, Faces = faces };
        return Dispatch(ApiId.BodyAskFaces, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int BodyAskEdges(int bodyTag, int* nEdges, int** edges)
    {
        if (Dispatcher.IsExecuting) return BodyAskEdgesImplementation(bodyTag, nEdges, edges);
        var command = new BodyAskEdgesCommand { Body = bodyTag, EdgeCount = nEdges, Edges = edges };
        return Dispatch(ApiId.BodyAskEdges, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int BodyAskVertices(int bodyTag, int* nVertices, int** vertices)
    {
        if (Dispatcher.IsExecuting) return BodyAskVerticesImplementation(bodyTag, nVertices, vertices);
        var command = new BodyAskVerticesCommand { Body = bodyTag, VertexCount = nVertices, Vertices = vertices };
        return Dispatch(ApiId.BodyAskVertices, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int BodyAskRegions(int bodyTag, int* nRegions, int** regions)
    {
        if (Dispatcher.IsExecuting) return BodyAskRegionsImplementation(bodyTag, nRegions, regions);
        var command = new BodyAskRegionsCommand { Body = bodyTag, RegionCount = nRegions, Regions = regions };
        return Dispatch(ApiId.BodyAskRegions, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int BodyAskTopology(
        int bodyTag,
        PK_BODY_ask_topology_o_s* options,
        int* nTopols,
        nint* topols,
        nint* classes,
        int* nRelations,
        nint* parents,
        nint* children,
        nint* senses)
    {
        if (Dispatcher.IsExecuting) return BodyAskTopologyImplementation(bodyTag, options, nTopols, topols, classes, nRelations, parents, children, senses);
        var command = new BodyAskTopologyCommand { Body = bodyTag, Options = options, TopologyCount = nTopols, Topologies = topols, Classes = classes, RelationCount = nRelations, Parents = parents, Children = children, Senses = senses };
        return Dispatch(ApiId.BodyAskTopology, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, bodyTag);
    }
    public static int FaceAskLoops(int faceTag, int* nLoops, int** loops)
    {
        if (Dispatcher.IsExecuting) return FaceAskLoopsImplementation(faceTag, nLoops, loops);
        var command = new FaceAskLoopsCommand { Face = faceTag, LoopCount = nLoops, Loops = loops };
        return Dispatch(ApiId.FaceAskLoops, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, faceTag);
    }
    public static int FaceAskSurf(int faceTag, int* surfTag)
    {
        if (Dispatcher.IsExecuting) return FaceAskSurfImplementation(faceTag, surfTag);
        var command = new FaceAskSurfCommand { Face = faceTag, Surf = surfTag };
        return Dispatch(ApiId.FaceAskSurf, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, faceTag);
    }
    public static int FaceAskShells(int faceTag, int* shells)
    {
        if (Dispatcher.IsExecuting) return FaceAskShellsImplementation(faceTag, shells);
        var command = new FaceAskShellsCommand { Face = faceTag, Shells = shells };
        return Dispatch(ApiId.FaceAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, faceTag);
    }
    public static int RegionIsSolid(int regionTag, KernelLogical* isSolid)
    {
        if (Dispatcher.IsExecuting) return RegionIsSolidImplementation(regionTag, isSolid);
        var command = new RegionIsSolidCommand { Region = regionTag, IsSolid = isSolid };
        return Dispatch(ApiId.RegionIsSolid, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, regionTag);
    }
    public static int LoopAskFace(int loopTag, int* faceTag)
    {
        if (Dispatcher.IsExecuting) return LoopAskFaceImplementation(loopTag, faceTag);
        var command = new LoopAskFaceCommand { Loop = loopTag, Face = faceTag };
        return Dispatch(ApiId.LoopAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, loopTag);
    }
    public static int LoopAskFins(int loopTag, int* nFins, int** fins)
    {
        if (Dispatcher.IsExecuting) return LoopAskFinsImplementation(loopTag, nFins, fins);
        var command = new LoopAskFinsCommand { Loop = loopTag, FinCount = nFins, Fins = fins };
        return Dispatch(ApiId.LoopAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, loopTag);
    }
    public static int EdgeAskFins(int edgeTag, int* nFins, int** fins)
    {
        if (Dispatcher.IsExecuting) return EdgeAskFinsImplementation(edgeTag, nFins, fins);
        var command = new EdgeAskFinsCommand { Edge = edgeTag, FinCount = nFins, Fins = fins };
        return Dispatch(ApiId.EdgeAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, edgeTag);
    }
    public static int EdgeAskCurve(int edgeTag, int* curveTag)
    {
        if (Dispatcher.IsExecuting) return EdgeAskCurveImplementation(edgeTag, curveTag);
        var command = new EdgeAskCurveCommand { Edge = edgeTag, Curve = curveTag };
        return Dispatch(ApiId.EdgeAskCurve, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, edgeTag);
    }
    public static int VertexAskPoint(int vertexTag, int* pointTag)
    {
        if (Dispatcher.IsExecuting) return VertexAskPointImplementation(vertexTag, pointTag);
        var command = new VertexAskPointCommand { Vertex = vertexTag, Point = pointTag };
        return Dispatch(ApiId.VertexAskPoint, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, vertexTag);
    }
    public static int FinAskEdge(int finTag, int* edgeTag)
    {
        if (Dispatcher.IsExecuting) return FinAskEdgeImplementation(finTag, edgeTag);
        var command = new FinAskEdgeCommand { Fin = finTag, Edge = edgeTag };
        return Dispatch(ApiId.FinAskEdge, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, finTag);
    }
    public static int FinAskLoop(int finTag, int* loopTag)
    {
        if (Dispatcher.IsExecuting) return FinAskLoopImplementation(finTag, loopTag);
        var command = new FinAskLoopCommand { Fin = finTag, Loop = loopTag };
        return Dispatch(ApiId.FinAskLoop, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, finTag);
    }
    public static int FinAskFace(int finTag, int* faceTag)
    {
        if (Dispatcher.IsExecuting) return FinAskFaceImplementation(finTag, faceTag);
        var command = new FinAskFaceCommand { Fin = finTag, Face = faceTag };
        return Dispatch(ApiId.FinAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, finTag);
    }
    public static int TransfCreate(PK_TRANSF_sf_s* transfSf, int* transfTag)
    {
        if (Dispatcher.IsExecuting) return TransfCreateImplementation(transfSf, transfTag);
        var command = new TransfCreateCommand { TransfSf = transfSf, Transf = transfTag };
        return Dispatch(ApiId.TransfCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidBlock(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidBlockImplementation(x, y, z, basisSet, bodyTag);
        var command = new BodyCreateSolidBlockCommand { X = x, Y = y, Z = z, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidBlock, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidCyl(double radius, double height, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidCylImplementation(radius, height, basisSet, bodyTag);
        var command = new BodyCreateSolidCylCommand { Radius = radius, Height = height, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidCyl, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidCone(double radius, double height, double semiAngle, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidConeImplementation(radius, height, semiAngle, basisSet, bodyTag);
        var command = new BodyCreateSolidConeCommand { Radius = radius, Height = height, SemiAngle = semiAngle, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidCone, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidPrism(double radius, double height, int nSides, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidPrismImplementation(radius, height, nSides, basisSet, bodyTag);
        var command = new BodyCreateSolidPrismCommand { Radius = radius, Height = height, SideCount = nSides, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidPrism, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidSphere(double radius, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidSphereImplementation(radius, basisSet, bodyTag);
        var command = new BodyCreateSolidSphereCommand { Radius = radius, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidSphere, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BodyCreateSolidTorus(double majorRadius, double minorRadius, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSolidTorusImplementation(majorRadius, minorRadius, basisSet, bodyTag);
        var command = new BodyCreateSolidTorusCommand { MajorRadius = majorRadius, MinorRadius = minorRadius, BasisSet = basisSet, Body = bodyTag };
        return Dispatch(ApiId.BodyCreateSolidTorus, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int CylCreate(PK_CYL_sf_s* cylSf, int* cylTag)
    {
        if (Dispatcher.IsExecuting) return CylCreateImplementation(cylSf, cylTag);
        var command = new CylCreateCommand { CylinderSf = cylSf, Cylinder = cylTag };
        return Dispatch(ApiId.CylCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int EllipseCreate(PK_ELLIPSE_sf_s* ellipseSf, int* ellipseTag)
    {
        if (Dispatcher.IsExecuting) return EllipseCreateImplementation(ellipseSf, ellipseTag);
        var command = new EllipseCreateCommand { EllipseSf = ellipseSf, Ellipse = ellipseTag };
        return Dispatch(ApiId.EllipseCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int TrCurveCreate(PK_TRCURVE_sf_s* trCurveSf, int* trCurveTag)
    {
        if (Dispatcher.IsExecuting) return TrCurveCreateImplementation(trCurveSf, trCurveTag);
        var command = new TrCurveCreateCommand { TrCurveSf = trCurveSf, TrCurve = trCurveTag };
        return Dispatch(ApiId.TrCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int SpCurveCreate(PK_SPCURVE_sf_s* spCurveSf, int* spCurveTag)
    {
        if (Dispatcher.IsExecuting) return SpCurveCreateImplementation(spCurveSf, spCurveTag);
        var command = new SpCurveCreateCommand { SpCurveSf = spCurveSf, SpCurve = spCurveTag };
        return Dispatch(ApiId.SpCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int BSurfCreate(PK_BSURF_sf_s* bsurfSf, int* bsurfTag)
    {
        if (Dispatcher.IsExecuting) return BSurfCreateImplementation(bsurfSf, bsurfTag);
        var command = new BSurfCreateCommand { BSurfSf = bsurfSf, BSurf = bsurfTag };
        return Dispatch(ApiId.BSurfCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int OffsetCreate(PK_OFFSET_sf_s* offsetSf, int* offsetTag)
    {
        if (Dispatcher.IsExecuting) return OffsetCreateImplementation(offsetSf, offsetTag);
        var command = new OffsetCreateCommand { OffsetSf = offsetSf, Offset = offsetTag };
        return Dispatch(ApiId.OffsetCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int SweptCreate(PK_SWEPT_sf_s* sweptSf, int* sweptTag)
    {
        if (Dispatcher.IsExecuting) return SweptCreateImplementation(sweptSf, sweptTag);
        var command = new SweptCreateCommand { SweptSf = sweptSf, Swept = sweptTag };
        return Dispatch(ApiId.SweptCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int SpunCreate(PK_SPUN_sf_s* spunSf, int* spunTag)
    {
        if (Dispatcher.IsExecuting) return SpunCreateImplementation(spunSf, spunTag);
        var command = new SpunCreateCommand { SpunSf = spunSf, Spun = spunTag };
        return Dispatch(ApiId.SpunCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int LineCreate(PK_LINE_sf_s* lineSf, int* lineTag)
    {
        if (Dispatcher.IsExecuting) return LineCreateImplementation(lineSf, lineTag);
        var command = new LineCreateCommand { LineSf = lineSf, Line = lineTag };
        return Dispatch(ApiId.LineCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int LineAsk(int lineTag, PK_LINE_sf_s* lineSf)
    {
        if (Dispatcher.IsExecuting) return LineAskImplementation(lineTag, lineSf);
        var command = new LineAskCommand { Line = lineTag, LineSf = lineSf };
        return Dispatch(ApiId.LineAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Line);
    }
    public static int CircleCreate(PK_CIRCLE_sf_s* circleSf, int* circleTag)
    {
        if (Dispatcher.IsExecuting) return CircleCreateImplementation(circleSf, circleTag);
        var command = new CircleCreateCommand { CircleSf = circleSf, Circle = circleTag };
        return Dispatch(ApiId.CircleCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int CircleAsk(int circleTag, PK_CIRCLE_sf_s* circleSf)
    {
        if (Dispatcher.IsExecuting) return CircleAskImplementation(circleTag, circleSf);
        var command = new CircleAskCommand { Circle = circleTag, CircleSf = circleSf };
        return Dispatch(ApiId.CircleAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Circle);
    }
    public static int PlaneCreate(PK_PLANE_sf_s* planeSf, int* planeTag)
    {
        if (Dispatcher.IsExecuting) return PlaneCreateImplementation(planeSf, planeTag);
        var command = new PlaneCreateCommand { PlaneSf = planeSf, Plane = planeTag };
        return Dispatch(ApiId.PlaneCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int PlaneAsk(int planeTag, PK_PLANE_sf_s* planeSf)
    {
        if (Dispatcher.IsExecuting) return PlaneAskImplementation(planeTag, planeSf);
        var command = new PlaneAskCommand { Plane = planeTag, PlaneSf = planeSf };
        return Dispatch(ApiId.PlaneAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Plane);
    }
    public static int ConeCreate(PK_CONE_sf_s* coneSf, int* coneTag)
    {
        if (Dispatcher.IsExecuting) return ConeCreateImplementation(coneSf, coneTag);
        var command = new ConeCreateCommand { ConeSf = coneSf, Cone = coneTag };
        return Dispatch(ApiId.ConeCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int ConeAsk(int coneTag, PK_CONE_sf_s* coneSf)
    {
        if (Dispatcher.IsExecuting) return ConeAskImplementation(coneTag, coneSf);
        var command = new ConeAskCommand { Cone = coneTag, ConeSf = coneSf };
        return Dispatch(ApiId.ConeAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Cone);
    }
    public static int SphereCreate(PK_SPHERE_sf_s* sphereSf, int* sphereTag)
    {
        if (Dispatcher.IsExecuting) return SphereCreateImplementation(sphereSf, sphereTag);
        var command = new SphereCreateCommand { SphereSf = sphereSf, Sphere = sphereTag };
        return Dispatch(ApiId.SphereCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int SphereAsk(int sphereTag, PK_SPHERE_sf_s* sphereSf)
    {
        if (Dispatcher.IsExecuting) return SphereAskImplementation(sphereTag, sphereSf);
        var command = new SphereAskCommand { Sphere = sphereTag, SphereSf = sphereSf };
        return Dispatch(ApiId.SphereAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Sphere);
    }
    public static int TorusCreate(PK_TORUS_sf_s* torusSf, int* torusTag)
    {
        if (Dispatcher.IsExecuting) return TorusCreateImplementation(torusSf, torusTag);
        var command = new TorusCreateCommand { TorusSf = torusSf, Torus = torusTag };
        return Dispatch(ApiId.TorusCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int TorusAsk(int torusTag, PK_TORUS_sf_s* torusSf)
    {
        if (Dispatcher.IsExecuting) return TorusAskImplementation(torusTag, torusSf);
        var command = new TorusAskCommand { Torus = torusTag, TorusSf = torusSf };
        return Dispatch(ApiId.TorusAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Torus);
    }
    public static int CylAsk(int cylTag, PK_CYL_sf_s* cylSf)
    {
        if (Dispatcher.IsExecuting) return CylAskImplementation(cylTag, cylSf);
        var command = new CylAskCommand { Cylinder = cylTag, CylinderSf = cylSf };
        return Dispatch(ApiId.CylAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, cylTag);
    }
    public static int PartTransmitB(int nParts, EntityTag* parts, PK_PART_transmit_o_s* options, PK_MEMORY_block_t* block)
    {
        if (Dispatcher.IsExecuting) return PartTransmitBImplementation(nParts, parts, options, block);
        var command = new PartTransmitBCommand { PartCount = nParts, Parts = parts, Options = options, Block = block };
        return Dispatch(ApiId.PartTransmitB, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int PartReceiveB(PK_MEMORY_block_t block, PK_PART_receive_o_s* options, int* nParts, EntityTag** parts)
    {
        if (Dispatcher.IsExecuting) return PartReceiveBImplementation(block, options, nParts, parts);
        var command = new PartReceiveBCommand { Block = block, Options = options, PartCount = nParts, Parts = parts };
        return Dispatch(ApiId.PartReceiveB, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command, 0);
    }
    public static int MemoryBlockFree(PK_MEMORY_block_t* block)
    {
        if (Dispatcher.IsExecuting) return MemoryBlockFreeImplementation(block);
        var command = new MemoryBlockFreeCommand { Block = block };
        return Dispatch(ApiId.MemoryBlockFree, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int MemoryFree(void* pointer)
    {
        if (Dispatcher.IsExecuting) return MemoryFreeImplementation(pointer);
        var command = new MemoryFreeCommand { Pointer = pointer };
        return Dispatch(ApiId.MemoryFree, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int MarkCreate(int* mark)
    {
        if (Dispatcher.IsExecuting)
        {
            var error = ModelControlEntryError();
            return error != 0 ? error : MarkCreateImplementation(mark);
        }
        var command = new MarkCreateCommand { Mark = mark };
        return Dispatch(ApiId.MarkCreate, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int MarkGoto(int mark)
    {
        if (Dispatcher.IsExecuting)
        {
            var error = ModelControlEntryError();
            return error != 0 ? error : MarkGotoImplementation(mark);
        }
        var command = new MarkGotoCommand { Mark = mark };
        return Dispatch(ApiId.MarkGoto, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int MarkDelete(int mark)
    {
        if (Dispatcher.IsExecuting)
        {
            var error = ModelControlEntryError();
            return error != 0 ? error : MarkDeleteImplementation(mark);
        }
        var command = new MarkDeleteCommand { Mark = mark };
        return Dispatch(ApiId.MarkDelete, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }

    private static int ModelControlEntryError()
    {
        if (State.Session == null) return 0;
        var context = ThreadContext();
        if (context == null) return ParasolidConstants.PK_ERROR_memory_full;
        return context->InKernel != 0 ? ParasolidConstants.PK_ERROR_bad_value : 0;
    }
    public static int PartitionCreateEmpty(PartitionSlot* partition)
    {
        if (Dispatcher.IsExecuting) return PartitionCreateEmptyImplementation(partition);
        var command = new PartitionCreateEmptyCommand { Partition = partition };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int PartitionSetCurrent(PartitionSlot partition)
    {
        if (Dispatcher.IsExecuting) return PartitionSetCurrentImplementation(partition);
        var command = new PartitionSetCurrentCommand { Partition = partition };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Local, AccessKind.SessionControl, ref command, 0);
    }
    public static int PartitionDelete(PartitionSlot partition, PK_PARTITION_delete_o_s* options)
    {
        if (Dispatcher.IsExecuting) return PartitionDeleteImplementation(partition, options);
        var command = new PartitionDeleteCommand { Partition = partition, Options = options };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int PartitionAskType(PartitionSlot partition, int* partitionType)
    {
        if (Dispatcher.IsExecuting) return PartitionAskTypeImplementation(partition, partitionType);
        var command = new PartitionAskTypeCommand { Partition = partition, PartitionType = partitionType };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadLockPartitions(int nPartitions, PartitionSlot* partitions,
        int lockType, int waitType, PK_THREAD_lock_partitions_o_s* options,
        PK_THREAD_lock_partitions_r_s* result)
    {
        if (Dispatcher.IsExecuting) return ThreadLockPartitionsImplementation(nPartitions, partitions, lockType, waitType, options, result);
        var command = new ThreadLockPartitionsCommand { Count = nPartitions, Partitions = partitions, LockType = lockType, WaitType = waitType, Options = options, Result = result };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int ThreadLockPartitionsResultFree(PK_THREAD_lock_partitions_r_s* result)
    {
        if (Dispatcher.IsExecuting) return ThreadLockPartitionsResultFreeImplementation(result);
        var command = new ThreadLockPartitionsResultFreeCommand { Result = result };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int ThreadUnlockPartitions(PK_THREAD_unlock_partitions_o_s* options,
        int* nPartitions, PartitionSlot** partitions)
    {
        if (Dispatcher.IsExecuting) return ThreadUnlockPartitionsImplementation(options, nPartitions, partitions);
        var command = new ThreadUnlockPartitionsCommand { Options = options, NPartitions = nPartitions, Partitions = partitions };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command, 0);
    }
    public static int ThreadAskLockedPartitions(PK_THREAD_ask_partitions_o_s* options,
        int* nPartitions, PartitionSlot** partitions)
    {
        if (Dispatcher.IsExecuting) return ThreadAskLockedPartitionsImplementation(options, nPartitions, partitions);
        var command = new ThreadAskLockedPartitionsCommand { Options = options, NPartitions = nPartitions, Partitions = partitions };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadSetId(int threadId, PK_THREAD_set_id_o_s* options, PK_THREAD_set_id_r_s* result)
    {
        if (Dispatcher.IsExecuting) return ThreadSetIdImplementation(threadId, options, result);
        var command = new ThreadSetIdCommand { ThreadId = threadId, Options = options, Result = result };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadAskId(int* threadId, int* parasolidId, byte* isSubthread)
    {
        if (Dispatcher.IsExecuting) return ThreadAskIdImplementation(threadId, parasolidId, isSubthread);
        var command = new ThreadAskIdCommand { ThreadId = threadId, ParasolidId = parasolidId, IsSubthread = isSubthread };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadChainStart(int type, PK_THREAD_chain_start_o_s* options)
    {
        if (Dispatcher.IsExecuting) return ThreadChainStartImplementation(type, options);
        var command = new ThreadChainStartCommand { Type = type, Options = options };
        return Dispatch(ApiId.ThreadChainStart, type == ParasolidConstants.PK_THREAD_chain_exclusive_c ? ConcurrencyKind.Exclusive : ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadChainStop(PK_THREAD_chain_stop_o_s* options)
    {
        if (Dispatcher.IsExecuting) return ThreadChainStopImplementation(options);
        var command = new ThreadChainStopCommand { Options = options };
        return Dispatch(ApiId.ThreadChainStop, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadIsInChain(int* type, int* length, int* remaining)
    {
        if (Dispatcher.IsExecuting) return ThreadIsInChainImplementation(type, length, remaining);
        var command = new ThreadIsInChainCommand { Type = type, Length = length, Remaining = remaining };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadIsInKernel(byte* inKernel, byte* isProtected, byte* isSubthread, byte* isExcluding)
    {
        if (Dispatcher.IsExecuting) return ThreadIsInKernelImplementation(inKernel, isProtected, isSubthread, isExcluding);
        var command = new ThreadIsInKernelCommand { InKernel = inKernel, IsProtected = isProtected, IsSubthread = isSubthread, IsExcluding = isExcluding };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadRegisterMemoryCbs(PK_MEMORY_frustrum_s cbs)
    {
        if (Dispatcher.IsExecuting) return ThreadRegisterMemoryCbsImplementation(cbs);
        var command = new ThreadRegisterMemoryCbsCommand { Cbs = cbs };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
    public static int ThreadAskMemoryCbs(PK_MEMORY_frustrum_s* cbs)
    {
        if (Dispatcher.IsExecuting) return ThreadAskMemoryCbsImplementation(cbs);
        var command = new ThreadAskMemoryCbsCommand { Cbs = cbs };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command, 0);
    }
}
