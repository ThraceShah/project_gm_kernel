using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus MaterializeXtBrepGeometry(XtDocument document, XtNode node,
        ReadOnlySpan<XtNode> geometry, ReadOnlySpan<int> tags, out EntityTag tag)
    {
        tag = 0;
        var fields = node.Fields;
        var type = (XtNodeTypes)node.Type;
        if (type is XtNodeTypes.Plane or XtNodeTypes.Cylinder or XtNodeTypes.Cone or XtNodeTypes.Sphere or XtNodeTypes.Torus)
            return TryMaterializeAnalyticSurfaceFromXt(document, node.Index, out tag);
        if (type == XtNodeTypes.BSurface) return MaterializeICurveBSurface(document, node, out tag);
        if (type == XtNodeTypes.BCurve) return MaterializeXtBCurve(document, node, out tag);
        EntityTag created = 0;
        int error;
        switch (type)
        {
            case XtNodeTypes.Point:
            {
                var sf = new PK_POINT_sf_s();
                XtWriteVector(ref sf.position, fields[5]);
                error = PointCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.Line:
            {
                var sf = new PK_LINE_sf_s();
                XtWriteVector(ref sf.basis_set.location, fields[7]);
                XtWriteVector(ref sf.basis_set.axis, fields[8]);
                error = LineCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.Circle:
            {
                var sf = new PK_CIRCLE_sf_s { radius = fields[10].Real };
                FillAxis2(ref sf.basis_set, fields[7].Vector, fields[8].Vector, fields[9].Vector);
                error = CircleCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.Ellipse:
            {
                var sf = new PK_ELLIPSE_sf_s { R1 = fields[10].Real, R2 = fields[11].Real };
                FillAxis2(ref sf.basis_set, fields[7].Vector, fields[8].Vector, fields[9].Vector);
                error = EllipseCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.TrimmedCurve:
            {
                var sf = new PK_TRCURVE_sf_s { basis_curve = XtMapped(geometry, tags, fields[7].Pointer, 0) };
                sf.t_int.value[0] = fields[10].Real;
                sf.t_int.value[1] = fields[11].Real;
                error = TrCurveCreate(&sf, &created);
                if (error == 0)
                {
                    ref var data = ref TrCurveDataPool[Curves[GetCurveSlotByTag(created)].DataIndex];
                    data.Point1 = XtKernelVector(fields[8]);
                    data.Point2 = XtKernelVector(fields[9]);
                }
                break;
            }
            case XtNodeTypes.SpCurve:
            {
                if (fields[9].Pointer != 0 || fields[10].Kind != XtFieldKind.Empty) return AlgorithmStatus.Unsupported;
                var sf = new PK_SPCURVE_sf_s
                {
                    surf = XtMapped(geometry, tags, fields[7].Pointer, 0),
                    curve = XtMapped(geometry, tags, fields[8].Pointer, 0),
                };
                error = SpCurveCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.SweptSurf:
            {
                if (fields[9].Kind != XtFieldKind.Empty) return AlgorithmStatus.Unsupported;
                var sf = new PK_SWEPT_sf_s { curve = XtMapped(geometry, tags, fields[7].Pointer, 0) };
                XtWriteVector(ref sf.direction, fields[8]);
                error = SweptCreate(&sf, &created);
                break;
            }
            case XtNodeTypes.SpunSurf:
            {
                if (fields[15].Kind != XtFieldKind.Empty) return AlgorithmStatus.Unsupported;
                var sf = new PK_SPUN_sf_s { curve = XtMapped(geometry, tags, fields[7].Pointer, 0) };
                XtWriteVector(ref sf.axis.location, fields[8]);
                XtWriteVector(ref sf.axis.axis, fields[9]);
                error = SpunCreate(&sf, &created);
                if (error == 0)
                {
                    ref var data = ref SpunDataPool[Surfaces[GetSurfaceSlotByTag(created)].DataIndex];
                    data.Start = XtKernelVector(fields[10]); data.End = XtKernelVector(fields[11]);
                    data.StartParam = XtTolerance(fields[12]); data.EndParam = XtTolerance(fields[13]);
                    data.XAxis = XtKernelVector(fields[14]);
                }
                break;
            }
            case XtNodeTypes.OffsetSurf:
            {
                if (fields[8].Integer != 0 || fields[11].Kind != XtFieldKind.Empty) return AlgorithmStatus.Unsupported;
                var support = XtMapped(geometry, tags, fields[9].Pointer, 0);
                var supportSlot = GetSurfaceSlotByTag(support);
                if (supportSlot < 0) return AlgorithmStatus.InvalidInput;
                // XT distance uses the sensed support normal. OFFSET_create
                // and evaluation use the parameter normal Su x Sv.
                var distance = Surfaces[supportSlot].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c
                    ? -fields[10].Real : fields[10].Real;
                var sf = new PK_OFFSET_sf_s
                {
                    underlying_surface = support,
                    offset_distance = distance,
                };
                error = OffsetCreate(&sf, &created);
                if (error == 0) OffsetDataPool[Surfaces[GetSurfaceSlotByTag(created)].DataIndex].Check = (OffsetCheckState)fields[7].Character;
                break;
            }
            case XtNodeTypes.Intersection:
            {
                var first=FindNodeByIndex(document.Nodes,fields[7].Pointer);var second=FindNodeByIndex(document.Nodes,fields[8].Pointer);
                var reversed=first?.Type==(int)XtNodeTypes.BlendBound;
                var boundary=reversed?first:second?.Type==(int)XtNodeTypes.BlendBound?second:null;
                var a=XtMapped(geometry,tags,fields[reversed?8:7].Pointer,0);
                var b=XtMapped(geometry,tags,boundary!=null?boundary.Fields[8].Pointer:fields[8].Pointer,0);
                var status=boundary!=null?MaterializeBlendContactXt(document,node,boundary,a,b,out created)
                    :MaterializeNestedBlendSpine(document,node,a,b,out created);
                if(status!=AlgorithmStatus.Success)return status;error=0;break;
            }
            case XtNodeTypes.BlendedEdge:
            {
                if(fields[7].Character!='R'||fields[13].Real!=1||fields[14].Real!=1||fields[15].Pointer!=0||fields[16].Pointer!=0)
                    return AlgorithmStatus.Unsupported;
                if(!(Math.Abs(fields[11].Real)>0&&Math.Abs(fields[11].Real)==Math.Abs(fields[12].Real)))return AlgorithmStatus.Unsupported;
                var a=XtMapped(geometry,tags,fields[8].Pointer,0);var b=XtMapped(geometry,tags,fields[9].Pointer,0);var curve=XtMapped(geometry,tags,fields[10].Pointer,0);
                Span<double> limits=stackalloc double[6];var firstLimit=default(LimitRecord);var lastLimit=default(LimitRecord);
                var lower=double.NaN;var upper=double.NaN;
                if(fields[17].Pointer!=0||fields[18].Pointer!=0)
                {
                    var firstNode=FindNodeByIndex(document.Nodes,fields[17].Pointer);var lastNode=FindNodeByIndex(document.Nodes,fields[18].Pointer);
                    if(firstNode==null||lastNode==null)return AlgorithmStatus.Unsupported;
                    if(!TryReadLimitNode(firstNode,limits[..3],out firstLimit.Type,out firstLimit.TermUse,out var n0)
                        ||!TryReadLimitNode(lastNode,limits[3..],out lastLimit.Type,out lastLimit.TermUse,out var n1))return AlgorithmStatus.InvalidInput;
                    if(n0!=1||n1!=1||firstLimit.Type!=LimitType.Terminator||lastLimit.Type!=LimitType.Terminator
                        ||firstLimit.TermUse!=LimitTermUse.Unset||lastLimit.TermUse!=LimitTermUse.Unset)return AlgorithmStatus.Unsupported;
                    firstLimit.HvecIndex=0;firstLimit.HvecCount=1;lastLimit.HvecIndex=1;lastLimit.HvecCount=1;
                    if(!TryFiniteBlendXtParameters(curve,limits[..3],limits[3..],out lower,out upper))return AlgorithmStatus.InvalidInput;
                }
                error=CompileAndBindNestedBlend(a,b,curve,fields[11].Real,fields[12].Real,&created,lower,upper);
                if(error==0&&firstLimit.HvecCount!=0)
                {
                    ref var data=ref BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(created)].DataIndex];
                    var payload=data.HvecBlock>0&&data.HvecCount==2?DereferenceBlock(data.HvecBlock):State.Session->Blocks.TryAllocate(6*8);if(payload==null)return AlgorithmStatus.WorkspaceTooSmall;
                    data.HvecBlock=State.Session->Blocks.HandleOf(payload);data.HvecCount=2;limits.CopyTo(new Span<double>(payload,6));
                    data.StartLimit=firstLimit;data.EndLimit=lastLimit;
                }
                break;
            }
            default: return AlgorithmStatus.Unsupported;
        }
        if (error == ParasolidConstants.PK_ERROR_memory_full || error < 0) return AlgorithmStatus.WorkspaceTooSmall;
        if (error == ParasolidConstants.PK_ERROR_not_implemented || error == ParasolidConstants.PK_ERROR_eval_failure)
            return AlgorithmStatus.Unsupported;
        if (error != 0) return AlgorithmStatus.InvalidInput;
        tag = created;
        if (type != XtNodeTypes.Point)
        {
            var sense = XtSense(fields[6].Character);
            if ((EntityClass)TagRec(tag).ClassCode == EntityClass.Curve) Curves[TagRec(tag).Slot].Sense = sense;
            else Surfaces[TagRec(tag).Slot].Sense = sense;
        }
        return AlgorithmStatus.Success;
    }

    private static void XtWriteVector(ref PK_VECTOR_s vector, XtFieldValue value)
    {
        vector.coord[0] = value.Vector.X; vector.coord[1] = value.Vector.Y; vector.coord[2] = value.Vector.Z;
    }

    private static KernelVector3 XtKernelVector(XtFieldValue value) => value.Kind == XtFieldKind.Empty
        ? default : EvaluationMath.Vector(value.Vector.X, value.Vector.Y, value.Vector.Z);

    private static AlgorithmStatus MaterializeXtBCurve(XtDocument document, XtNode node, out CurveTag tag)
    {
        tag = 0;
        var nurbs = FindNodeByIndex(document.Nodes, node.Fields[7].Pointer);
        if (nurbs is null || nurbs.Type != (int)XtNodeTypes.NurbsCurve || nurbs.Fields.Length != 12)
            return AlgorithmStatus.InvalidInput;
        ReadOnlySpan<XtFieldKind> kinds =
        [XtFieldKind.Integer, XtFieldKind.Integer, XtFieldKind.Integer, XtFieldKind.Integer,
            XtFieldKind.Unsigned, XtFieldKind.Logical, XtFieldKind.Logical, XtFieldKind.Logical, XtFieldKind.Unsigned];
        Span<int> values = stackalloc int[9];
        for (var i = 0; i < values.Length; i++)
        {
            var field = nurbs.Fields[i];
            if (field.Kind != kinds[i] || field.Integer < 0 || field.Integer > int.MaxValue
                || (kinds[i] == XtFieldKind.Logical && field.Integer > 1)) return AlgorithmStatus.InvalidInput;
            values[i] = (int)field.Integer;
        }
        if (values[0] < 1 || values[1] <= values[0] || values[3] < 2
            || values[2] != 2 + values[7] && values[2] != 3 + values[7]
            || values[4] is < 1 or > 6 || values[8] is < 1 or > 8) return AlgorithmStatus.InvalidInput;
        if (values[8] == 8) return AlgorithmStatus.Unsupported; // helical form has no supported PK constructor form
        var scalarCount = (long)values[1] * values[2];
        if (scalarCount > int.MaxValue) return AlgorithmStatus.InvalidInput;
        var poles = FindNodeByIndex(document.Nodes, nurbs.Fields[9].Pointer);
        var mults = FindNodeByIndex(document.Nodes, nurbs.Fields[10].Pointer);
        var knots = FindNodeByIndex(document.Nodes, nurbs.Fields[11].Pointer);
        if (!IsBSurfaceArray(poles, XtNodeTypes.BSplineVertices, (int)scalarCount)
            || !IsBSurfaceArray(mults, XtNodeTypes.KnotMultiplicities, values[3])
            || !IsBSurfaceArray(knots, XtNodeTypes.KnotSet, values[3])) return AlgorithmStatus.InvalidInput;
        var self = ParasolidConstants.PK_self_intersect_unset_c;
        if (node.Fields[8].Pointer != 0)
        {
            var data = FindNodeByIndex(document.Nodes, node.Fields[8].Pointer);
            if (data is null || data.Type != (int)XtNodeTypes.CurveData || data.Fields.Length != 2
                || data.Fields[0].Kind != XtFieldKind.Unsigned || data.Fields[0].Integer is < 1 or > 4)
                return AlgorithmStatus.InvalidInput;
            if (data.Fields[0].Integer == 4 || data.Fields[1].Pointer != 0) return AlgorithmStatus.Unsupported;
            self += (int)data.Fields[0].Integer - 1;
        }
        var workspaceCount = scalarCount + values[3] + ((long)values[3] + 1) / 2;
        if (workspaceCount > int.MaxValue) return AlgorithmStatus.WorkspaceTooSmall;
        var ownsScratch = EnsureCommandScratch();
        var checkpoint = CommandScratch.ThreadArenaOffset;
        try
        {
            var scratch = CommandScratch.Current.Take((int)workspaceCount);
            if (scratch.Length < workspaceCount) return AlgorithmStatus.WorkspaceTooSmall;
            var vertexValues = scratch[..(int)scalarCount];
            var knotValues = scratch.Slice((int)scalarCount, values[3]);
            var multValues = MemoryMarshal.Cast<double, int>(scratch[((int)scalarCount + values[3])..])[..values[3]];
            if (!ReadBSurfaceReals(poles!, vertexValues) || !ReadBSurfaceReals(knots!, knotValues)
                || !ReadBSurfaceMultiplicities(mults!, multValues)) return AlgorithmStatus.InvalidInput;
            fixed (double* vertex = vertexValues, knot = knotValues)
            fixed (int* mult = multValues)
            {
                var sf = new PK_BCURVE_sf_s
                {
                    degree = values[0], n_vertices = values[1], vertex_dim = values[2], n_knots = values[3],
                    knot_type = BSurfaceKnotTypeFromXt(values[4]), is_periodic = (byte)values[5],
                    is_closed = (byte)values[6], is_rational = (byte)values[7],
                    form = ParasolidConstants.PK_BCURVE_form_unset_c + values[8] - 1,
                    vertex = vertex, knot = knot, knot_mult = mult, self_intersecting = self,
                };
                CurveTag created = 0;
                var error = BCurveCreate(&sf, &created);
                if (error == ParasolidConstants.PK_ERROR_memory_full || error < 0) return AlgorithmStatus.WorkspaceTooSmall;
                if (error == ParasolidConstants.PK_ERROR_not_implemented) return AlgorithmStatus.Unsupported;
                if (error != 0) return AlgorithmStatus.InvalidInput;
                tag = created;
                Curves[GetCurveSlotByTag(tag)].Sense = XtSense(node.Fields[6].Character);
                return AlgorithmStatus.Success;
            }
        }
        finally
        {
            CommandScratch.ThreadArenaOffset = checkpoint;
            if (ownsScratch) ReleaseCommandScratch(State.Session);
        }
    }
}
