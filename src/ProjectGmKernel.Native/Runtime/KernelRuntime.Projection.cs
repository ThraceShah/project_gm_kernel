using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Projection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int SurfParameteriseVector(SurfTag surface, PK_VECTOR_s vector, PK_UV_s* uv)
    {
        if (Dispatcher.IsExecuting) return SurfParameteriseVectorCore(surface, vector, uv);
        var command = new GeometryParameterCommand { Geometry = surface, Point = vector, UV = uv };
        return Dispatch(ApiId.SurfParameteriseVector, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, surface);
    }
    public static int CurveParameteriseVector(CurveTag curve, PK_VECTOR_s vector, double* parameter)
    {
        if (Dispatcher.IsExecuting) return CurveParameteriseVectorCore(curve, vector, parameter);
        var command = new GeometryParameterCommand { Geometry = curve, Point = vector, Parameter = parameter, Curve = 1 };
        return Dispatch(ApiId.CurveParameteriseVector, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, curve);
    }
    private struct GeometryParameterCommand : IKernelCommand
    {
        internal GeomTag Geometry;
        internal PK_VECTOR_s Point;
        internal PK_UV_s* UV;
        internal double* Parameter;
        internal byte Curve;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return Curve != 0 ? CurveParameteriseVectorCore(Geometry, Point, Parameter) : SurfParameteriseVectorCore(Geometry, Point, UV); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int SurfParameteriseVectorCore(SurfTag surface, PK_VECTOR_s vector, PK_UV_s* uv)
    {
        if (uv != null) *uv = default;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var point = Vector(vector.coord[0], vector.coord[1], vector.coord[2]);
        if (uv == null || !IsFinite(point)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var slot = GetSurfaceSlotByTag(surface); if (slot < 0) return ParasolidConstants.PK_ERROR_bad_class;
        if (Surfaces[slot].Class != SurfaceClass.BSurface)
        {
            var status = IntersectionSurfaceParameters(Surfaces[slot], point, out var value);
            if (status != AlgorithmStatus.Success) return status == AlgorithmStatus.InvalidInput ? ParasolidConstants.PK_ERROR_not_on_surface : IntersectionError(status);
            *uv = value; return 0;
        }
        var projection = new ProjectionPoint { DistanceUpper = double.PositiveInfinity };
        var result = ClosestBSurface(GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]), point, 1e-10, ref projection);
        if (result != AlgorithmStatus.Success) return IntersectionError(result);
        if (projection.DistanceUpper > 1e-8) return ParasolidConstants.PK_ERROR_not_on_surface;
        uv->param[0] = projection.U; uv->param[1] = projection.V; return 0;
    }

    internal static AlgorithmStatus ClosestBSurface(in BSurfaceView surface, in KernelVector3 point,
        double tolerance, ref ProjectionPoint best)
    {
        Span<IntervalNumber> poles = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        for (var u = surface.UDegree; u < surface.UVertexCount; u++) for (var v = surface.VDegree; v < surface.VVertexCount; v++)
        {
            if (!(surface.UKnots[u + 1] > surface.UKnots[u]) || !(surface.VKnots[v + 1] > surface.VKnots[v])) continue;
            var status = BezierSurfaceBounds.Extract(surface, u, v, poles); if (status != AlgorithmStatus.Success) return status;
            var patch = new BezierSurfacePatch(poles[..(4 * (surface.UDegree + 1) * (surface.VDegree + 1))], surface.UDegree, surface.VDegree,
                new(surface.UKnots[u], surface.UKnots[u + 1], surface.VKnots[v], surface.VKnots[v + 1]));
            status = BSplineProjection.Closest(patch, point, tolerance, ref best); if (status != AlgorithmStatus.Success) return status;
        }
        return double.IsFinite(best.DistanceUpper) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static int CurveParameteriseVectorCore(CurveTag curve, PK_VECTOR_s vector, double* parameter)
    {
        if (parameter != null) *parameter = 0;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var point = Vector(vector.coord[0], vector.coord[1], vector.coord[2]);
        if (parameter == null || !IsFinite(point)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var slot = GetCurveSlotByTag(curve); if (slot < 0) return ParasolidConstants.PK_ERROR_bad_class;
        var record = Curves[slot]; double value;
        if (record.Class == CurveClass.Line) { GetIntersectionLine(record, out var origin, out var direction); value = Dot(Sub(point, origin), direction); }
        else if (TryGetIntersectionConic(record, out var conic))
        {
            var delta = Sub(point, conic.Origin); value = Math.Atan2(Dot(delta, ProfileCross(conic.Axis, conic.Reference)) / conic.Minor, Dot(delta, conic.Reference) / conic.Radius);
            if (value < 0) value += Math.Tau;
        }
        else if (record.Class == CurveClass.BCurve)
        {
            var view = GetBCurveView(BCurveDataStore[record.DataIndex]);
            var best = new ProjectionPoint { DistanceUpper = double.PositiveInfinity };
            Span<IntervalNumber> poles = stackalloc IntervalNumber[4 * 17];
            for (var span = view.Degree; span < view.VertexCount; span++)
            {
                if (!(view.Knots[span + 1] > view.Knots[span])) continue;
                var status = BezierCurveBounds.Extract(view, span, poles); if (status != AlgorithmStatus.Success) return IntersectionError(status);
                var patch = new BezierSurfacePatch(poles[..(4 * (view.Degree + 1))], view.Degree, 0, new(view.Knots[span], view.Knots[span + 1], 0, 1));
                status = BSplineProjection.Closest(patch, point, 1e-10, ref best); if (status != AlgorithmStatus.Success) return IntersectionError(status);
            }
            if (best.DistanceUpper > 1e-8) return ParasolidConstants.PK_ERROR_not_on_curve; value = best.U;
        }
        else if(TryBlendIsoparam(slot,out var blendSupport,out _))
        {
            // This native UV definition is exactly (u=t, v=constant).
            // Reuse the parent inverse and retain the finite parameter curve;
            // the common D0 check below accepts only an on-curve result.
            var status=ParameteriseNestedSurface(Surfaces[blendSupport],point,out var uv);
            if(status!=AlgorithmStatus.Success)return status==AlgorithmStatus.InvalidInput?ParasolidConstants.PK_ERROR_not_on_curve:IntersectionError(status);
            value=Math.Clamp(uv.param[0],record.TMin,record.TMax);
        }
        else if(record.Class==CurveClass.SPCurve)
        {
            var data=SpCurveDataPool[record.DataIndex];var support=GetSurfaceSlotByTag(data.SurfTag);var basis=GetCurveSlotByTag(data.BCurveTag);
            if(support<0||basis<0||Surfaces[support].Class!=SurfaceClass.BSurface||Curves[basis].Class!=CurveClass.BCurve)return ParasolidConstants.PK_ERROR_not_implemented;
            var surface=GetBSurfaceView(BSurfaceDataStore[Surfaces[support].DataIndex]);var uv=GetBCurveView(BCurveDataStore[Curves[basis].DataIndex]);
            if(uv.Periodic)return ParasolidConstants.PK_ERROR_not_implemented;
            var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17];
            for(var knot=uv.Degree;knot<uv.VertexCount;knot++)
            {
                var first=uv.Knots[knot];var last=uv.Knots[knot+1];if(!(last>first))continue;
                var status=SpCurveBezier.FromSpline(surface,uv,knot,first,last,poles,out var degree);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                status=BSplineProjection.Closest(new(poles[..(4*(degree+1))],degree,0,new(first,last,0,1)),point,1e-10,ref best);
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            }
            if(best.DistanceUpper>1e-8)return ParasolidConstants.PK_ERROR_not_on_curve;value=best.U;
        }
        else if (record.Class == CurveClass.ICurve)
        {
            ref readonly var procedural=ref ICurveDataPool[record.DataIndex];
            if(procedural.IsBlendContact!=0)
            {
                var status=ParameteriseNestedSurface(Surfaces[GetSurfaceSlotByTag(procedural.Surface1Tag)],point,out var uv);if(status!=AlgorithmStatus.Success)return ParasolidConstants.PK_ERROR_not_on_curve;
                if(Math.Abs(uv.param[1]-BlendContactV(procedural))>1e-8)return ParasolidConstants.PK_ERROR_not_on_curve;
                var error=BlendContactParameter(procedural,point,uv.param[0],out value);if(error!=0)return error;*parameter=value;return 0;
            }
            if(procedural.ProgramBlock>0)
                return ParameteriseNestedSpine(procedural,point,parameter);
            var checkpoint = CommandScratch.ThreadArenaOffset;
            try
            {
                var status = TryPrepareICurveView(record, out var view, out _); if (status != AlgorithmStatus.Success) return IntersectionError(status);
                Span<KernelVector3> result = stackalloc KernelVector3[1];
                for(var end=0;end<2;end++)
                {
                    var limit=end==0?view.StartTerminator:view.EndTerminator;
                    var boundary=end==0?view.ChartParameters[0]:view.ChartParameters[^1];
                    if(!SplineTerminalSection.TryParameter(limit,boundary,end!=0,out var terminal))continue;
                    var chord=Sub(limit.Endpoint,limit.BranchPoint);
                    var fraction=Dot(Sub(point,limit.BranchPoint),chord)/Dot(chord,chord);
                    if(!(fraction>=0&&fraction<=1))continue;
                    value=boundary+(terminal-boundary)*fraction;
                    status=ICurveEvaluation.Evaluate(view,value,0,result,out _);
                    if(status==AlgorithmStatus.Success&&OperationIntersection.Norm(Sub(result[0],point))<=OperationIntersection.DistanceTolerance)
                    {*parameter=value;return 0;}
                }
                for (var i = 0; i < view.ChartScales.Length; i++)
                {
                    value = OriginalChartParameterMap.InverseParameter(view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits, i, point);
                    if (value < view.ChartParameters[i] - 1e-10 || value > view.ChartParameters[i + 1] + 1e-10) continue;
                    value = Math.Clamp(value, view.ChartParameters[i], view.ChartParameters[i + 1]);
                    status = ICurveEvaluation.Evaluate(view, value, 0, result, out _);
                    if (status == AlgorithmStatus.Success && OperationIntersection.Norm(Sub(result[0], point)) <= 1e-8) { *parameter = value; return 0; }
                }
                return ParasolidConstants.PK_ERROR_not_on_curve;
            }
            finally { CommandScratch.ThreadArenaOffset = checkpoint; }
        }
        else if (record.Class == CurveClass.TRCurve)
        {
            var trim = TrCurveDataPool[record.DataIndex]; double basis = 0;
            var error = CurveParameteriseVectorCore(trim.BasisCurveTag, vector, &basis); if (error != 0) return error;
            value = (basis - trim.Parm1) * (trim.Parm2 >= trim.Parm1 ? 1 : -1);
            if (value < record.TMin - 1e-10 || value > record.TMax + 1e-10) return ParasolidConstants.PK_ERROR_not_on_curve;
        }
        else return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> evaluated = stackalloc KernelVector3[2];
        var evaluation = EvaluateCurveCore(in record, value, 0, false, evaluated, out _);
        if (evaluation != AlgorithmStatus.Success) return EvaluationError(evaluation);
        if (OperationIntersection.Norm(Sub(evaluated[0], point)) > 1e-8) return ParasolidConstants.PK_ERROR_not_on_curve;
        *parameter = value; return 0;
    }
}
