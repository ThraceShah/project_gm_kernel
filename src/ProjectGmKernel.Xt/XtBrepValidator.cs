namespace ProjectGmKernel.Xt;

public static class XtBrepValidator
{
    public static void Validate(XtBrepModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model.Storage);
    }

    public static bool TryValidate(XtBrepModel model, out XtDiagnostic diagnostic)
    {
        try
        {
            Validate(model);
            diagnostic = default;
            return true;
        }
        catch (XtFormatException exception)
        {
            diagnostic = new XtDiagnostic(exception.Code, exception.Message);
            return false;
        }
    }

    internal static void Validate(XtBrepStorage s)
    {
        for (var i = 0; i < s.Parts.Length; i++)
        {
            ref readonly var row = ref s.Parts[i];
            if (row.Kind == XtPartKind.Body) Index(row.Body, s.Bodies.Length, "part body");
            else Index(row.Assembly, s.Assemblies.Length, "part assembly");
            OptionalIndex(row.Name, s.Strings.Length, "part name");
            OptionalIndex(row.Next,s.Parts.Length,"part next");OptionalIndex(row.Previous,s.Parts.Length,"part previous");
        }
        for (var i = 0; i < s.Bodies.Length; i++)
        {
            ref readonly var row = ref s.Bodies[i];
            Range(row.Regions, s.Regions.Length, "body regions"); Range(row.Shells, s.Shells.Length, "body shells");
            Range(row.Edges, s.Edges.Length, "body edges"); Range(row.Vertices, s.Vertices.Length, "body vertices"); Range(row.Attributes, s.Attributes.Length, "body attributes");
            OptionalIndex(row.ReferenceInstance,s.Instances.Length,"body reference instance");
            OptionalIndex(row.Owner,s.Bodies.Length,"body owner");OptionalIndex(row.Child,s.Bodies.Length,"body child");
            OptionalIndex(row.Surface,s.Geometries.Length,"body surface");OptionalIndex(row.Curve,s.Geometries.Length,"body curve");OptionalIndex(row.Point,s.Points.Length,"body point");
            OptionalIndex(row.BoundarySurface,s.Geometries.Length,"body boundary surface");OptionalIndex(row.BoundaryCurve,s.Geometries.Length,"body boundary curve");OptionalIndex(row.BoundaryPoint,s.Points.Length,"body boundary point");
            OptionalIndex(row.BoundaryMesh,s.Meshes.Length,"body boundary mesh");OptionalIndex(row.BoundaryPolyline,s.Meshes.Length,"body boundary polyline");
        }
        ValidateBodyAcyclic(s);
        for (var i = 0; i < s.Regions.Length; i++) { OptionalIndex(s.Regions[i].Body, s.Bodies.Length, "region body");OptionalIndex(s.Regions[i].OwnerBody,s.Bodies.Length,"region owner body"); OptionalIndex(s.Regions[i].FirstShell, s.Shells.Length, "region shell"); OptionalIndex(s.Regions[i].Next, s.Regions.Length, "region next"); OptionalIndex(s.Regions[i].Previous, s.Regions.Length, "region previous"); }
        for (var i = 0; i < s.Shells.Length; i++) { OptionalIndex(s.Shells[i].Body, s.Bodies.Length, "shell body"); OptionalIndex(s.Shells[i].Region, s.Regions.Length, "shell region"); OptionalIndex(s.Shells[i].FirstFace, s.Faces.Length, "shell face"); OptionalIndex(s.Shells[i].FrontFace, s.Faces.Length, "shell front face");OptionalIndex(s.Shells[i].FirstEdge,s.Edges.Length,"shell edge");OptionalIndex(s.Shells[i].FirstVertex,s.Vertices.Length,"shell vertex"); OptionalIndex(s.Shells[i].Next, s.Shells.Length, "shell next"); }
        for (var i = 0; i < s.Faces.Length; i++) { OptionalIndex(s.Faces[i].Shell, s.Shells.Length, "face shell"); OptionalIndex(s.Faces[i].FirstLoop, s.Loops.Length, "face loop"); OptionalIndex(s.Faces[i].Surface, s.Geometries.Length, "face surface");if(s.Faces[i].SurfaceKind!=XtOwnerKind.None)Owner(s.Faces[i].SurfaceKind,s.Faces[i].SurfaceEntity,s,"face surface entity"); OptionalIndex(s.Faces[i].Next, s.Faces.Length, "face next"); OptionalIndex(s.Faces[i].Previous, s.Faces.Length, "face previous"); OptionalIndex(s.Faces[i].NextFront, s.Faces.Length, "face next front"); OptionalIndex(s.Faces[i].PreviousFront, s.Faces.Length, "face previous front");OptionalIndex(s.Faces[i].NextOnSurface,s.Faces.Length,"face next on surface");OptionalIndex(s.Faces[i].PreviousOnSurface,s.Faces.Length,"face previous on surface"); OptionalIndex(s.Faces[i].FrontShell, s.Shells.Length, "face front shell"); Sense(s.Faces[i].Sense, "face sense"); }
        for (var i = 0; i < s.Loops.Length; i++) { Index(s.Loops[i].Face, s.Faces.Length, "loop face"); OptionalIndex(s.Loops[i].FirstFin, s.Fins.Length, "loop fin"); OptionalIndex(s.Loops[i].Next, s.Loops.Length, "loop next"); }
        for (var i = 0; i < s.Fins.Length; i++) { OptionalIndex(s.Fins[i].Loop, s.Loops.Length, "fin loop"); OptionalIndex(s.Fins[i].Edge, s.Edges.Length, "fin edge"); OptionalIndex(s.Fins[i].Vertex, s.Vertices.Length, "fin vertex"); OptionalIndex(s.Fins[i].Curve, s.Geometries.Length, "fin curve");if(s.Fins[i].CurveKind!=XtOwnerKind.None)Owner(s.Fins[i].CurveKind,s.Fins[i].CurveEntity,s,"fin curve entity"); OptionalIndex(s.Fins[i].Forward, s.Fins.Length, "fin forward"); OptionalIndex(s.Fins[i].Backward, s.Fins.Length, "fin backward"); OptionalIndex(s.Fins[i].Other, s.Fins.Length, "fin other"); OptionalIndex(s.Fins[i].NextAtVertex, s.Fins.Length, "fin next at vertex"); Sense(s.Fins[i].Sense, "fin sense"); }
        for (var i = 0; i < s.Edges.Length; i++) { OptionalIndex(s.Edges[i].Start, s.Vertices.Length, "edge start"); OptionalIndex(s.Edges[i].End, s.Vertices.Length, "edge end"); OptionalIndex(s.Edges[i].Curve, s.Geometries.Length, "edge curve");if(s.Edges[i].CurveKind!=XtOwnerKind.None)Owner(s.Edges[i].CurveKind,s.Edges[i].CurveEntity,s,"edge curve entity"); OptionalIndex(s.Edges[i].FirstFin, s.Fins.Length, "edge fin"); OptionalIndex(s.Edges[i].Next, s.Edges.Length, "edge next"); OptionalIndex(s.Edges[i].Previous, s.Edges.Length, "edge previous");OptionalIndex(s.Edges[i].NextOnCurve,s.Edges.Length,"edge next on curve");OptionalIndex(s.Edges[i].PreviousOnCurve,s.Edges.Length,"edge previous on curve"); OptionalIndex(s.Edges[i].Body, s.Bodies.Length, "edge body");if(s.Edges[i].OwnerKind!=XtOwnerKind.None)Owner(s.Edges[i].OwnerKind,s.Edges[i].Owner,s,"edge owner");if(s.Edges[i].TolerancePresent!=0&&(!double.IsFinite(s.Edges[i].Tolerance)||s.Edges[i].Tolerance<0))Invalid("edge tolerance must be finite and non-negative"); }
        for (var i = 0; i < s.Vertices.Length; i++) { OptionalIndex(s.Vertices[i].Point, s.Points.Length, "vertex point"); OptionalIndex(s.Vertices[i].FirstFin, s.Fins.Length, "vertex fin"); OptionalIndex(s.Vertices[i].FirstEdge, s.Edges.Length, "vertex edge"); OptionalIndex(s.Vertices[i].Next, s.Vertices.Length, "vertex next"); OptionalIndex(s.Vertices[i].Previous, s.Vertices.Length, "vertex previous"); OptionalIndex(s.Vertices[i].Body, s.Bodies.Length, "vertex body");if(s.Vertices[i].OwnerKind!=XtOwnerKind.None)Owner(s.Vertices[i].OwnerKind,s.Vertices[i].Owner,s,"vertex owner");if(s.Vertices[i].TolerancePresent!=0&&(!double.IsFinite(s.Vertices[i].Tolerance)||s.Vertices[i].Tolerance<0))Invalid("vertex tolerance must be finite and non-negative"); }
        for (var i = 0; i < s.Geometries.Length; i++)
        {
            ref readonly var row = ref s.Geometries[i];
            Range(row.ControlPoints, s.ControlPoints.Length, "geometry control points"); Range(row.Weights, s.Scalars.Length, "geometry weights");
            Range(row.Knots, s.Scalars.Length, "geometry knots"); Range(row.Multiplicities, s.Scalars.Length, "geometry multiplicities");
            Range(row.KnotsU,s.Scalars.Length,"geometry U knots");Range(row.KnotsV,s.Scalars.Length,"geometry V knots");Range(row.MultiplicitiesU,s.Scalars.Length,"geometry U multiplicities");Range(row.MultiplicitiesV,s.Scalars.Length,"geometry V multiplicities");
            OptionalIndex(row.Basis, s.Geometries.Length, "geometry basis"); OptionalIndex(row.Profile, s.Geometries.Length, "geometry profile");
            OptionalIndex(row.Spine,s.Geometries.Length,"geometry spine");OptionalIndex(row.Chart,s.Charts.Length,"geometry chart");OptionalIndex(row.StartLimit,s.Limits.Length,"geometry start limit");OptionalIndex(row.EndLimit,s.Limits.Length,"geometry end limit");OptionalIndex(row.IntersectionData,s.IntersectionData.Length,"geometry intersection data");OptionalIndex(row.GeometricOwner,s.GeometricOwners.Length,"geometry geometric owner");OptionalIndex(row.Next,s.Geometries.Length,"geometry next");OptionalIndex(row.Previous,s.Geometries.Length,"geometry previous");
            if (row.Rational != 0 && row.Weights.Count != row.ControlPoints.Count) Invalid("rational geometry weight count must equal control point count");
            if(row.ScalePresent!=0&&!double.IsFinite(row.Scale))Invalid("geometry scale must be finite");
            Owner(row.OwnerKind,row.Owner,s,"geometry owner");
        }
        for(var i=0;i<s.Points.Length;i++)Owner(s.Points[i].OwnerKind,s.Points[i].Owner,s,"point owner");
        for(var i=0;i<s.Frames.Length;i++){ref readonly var row=ref s.Frames[i];OptionalIndex(row.Geometry,s.Geometries.Length,"frame geometry");if(row.GeometryKind!=XtOwnerKind.None)Owner(row.GeometryKind,row.GeometryEntity,s,"frame geometry entity");OptionalIndex(row.Next,s.Frames.Length,"frame next");OptionalIndex(row.Previous,s.Frames.Length,"frame previous");OptionalIndex(row.NextOnGeometry,s.Frames.Length,"frame next on geometry");OptionalIndex(row.PreviousOnGeometry,s.Frames.Length,"frame previous on geometry");Owner(row.OwnerKind,row.Owner,s,"frame owner");Sense(row.Sense,"frame sense");}
        for (var i = 0; i < s.Instances.Length; i++) { Index(s.Instances[i].Owner, s.Assemblies.Length, "instance owner"); if(s.Instances[i].HasExternalPart==0)Index(s.Instances[i].Part, s.Parts.Length, "instance part"); OptionalIndex(s.Instances[i].Transform, s.Transforms.Length, "instance transform");OptionalIndex(s.Instances[i].NextInPart,s.Instances.Length,"instance next in part");OptionalIndex(s.Instances[i].PreviousInPart,s.Instances.Length,"instance previous in part");OptionalIndex(s.Instances[i].NextOfPart,s.Instances.Length,"instance next of part");OptionalIndex(s.Instances[i].PreviousOfPart,s.Instances.Length,"instance previous of part"); Range(s.Instances[i].Attributes, s.Attributes.Length, "instance attributes"); }
        for (var i = 0; i < s.Assemblies.Length; i++) { Range(s.Assemblies[i].Instances, s.Instances.Length, "assembly instances"); Range(s.Assemblies[i].Attributes, s.Attributes.Length, "assembly attributes"); OptionalIndex(s.Assemblies[i].Name, s.Strings.Length, "assembly name");OptionalIndex(s.Assemblies[i].ReferenceInstance,s.Instances.Length,"assembly reference instance");OptionalIndex(s.Assemblies[i].Next,s.Assemblies.Length,"assembly next");OptionalIndex(s.Assemblies[i].Previous,s.Assemblies.Length,"assembly previous"); }
        ValidateAssemblyAcyclic(s);
        for(var i=0;i<s.AttributeDefinitions.Length;i++){OptionalIndex(s.AttributeDefinitions[i].Name,s.Strings.Length,"attribute definition name");Range(s.AttributeDefinitions[i].FieldDefinitions,s.AttributeFieldDefinitions.Length,"attribute field definitions");}
        for(var i=0;i<s.AttributeFieldDefinitions.Length;i++)OptionalIndex(s.AttributeFieldDefinitions[i].Name,s.Strings.Length,"attribute field name");
        for(var i=0;i<s.Attributes.Length;i++){Index(s.Attributes[i].Definition,s.AttributeDefinitions.Length,"attribute definition");Owner(s.Attributes[i].OwnerKind,s.Attributes[i].Owner,s,"attribute owner");Range(s.Attributes[i].Values,s.AttributeValues.Length,"attribute values");}
        for(var i=0;i<s.AttributeValues.Length;i++)Range(s.AttributeValues[i].Data,s.Payload.Length,"attribute value data");
        for (var i = 0; i < s.Strings.Length; i++) Range(new XtRange { Offset=s.Strings[i].Offset, Count=s.Strings[i].ByteCount }, s.Payload.Length, "string bytes");
        for (var i = 0; i < s.UserFields.Length; i++){Owner(s.UserFields[i].OwnerKind,s.UserFields[i].Owner,s,"user field owner");Range(s.UserFields[i].Payload, s.Payload.Length, "user field payload");}
        for(var i=0;i<s.Meshes.Length;i++){ref readonly var row=ref s.Meshes[i];Owner(row.OwnerKind,row.Owner,s,"mesh owner");OptionalIndex(row.Next,s.Meshes.Length,"mesh next");OptionalIndex(row.Previous,s.Meshes.Length,"mesh previous");OptionalIndex(row.Data,s.Meshes.Length,"mesh data");OptionalIndex(row.PsmMesh,s.Meshes.Length,"mesh PSM mesh");OptionalIndex(row.PositionPool,s.Meshes.Length,"mesh position pool");OptionalIndex(row.NormalPool,s.Meshes.Length,"mesh normal pool");OptionalIndex(row.PositionIndices,s.Meshes.Length,"mesh position indices");OptionalIndex(row.NormalIndices,s.Meshes.Length,"mesh normal indices");Range(row.References,s.Connections.Length,"mesh references");Range(row.Values,s.Scalars.Length,"mesh values");Range(row.Vertices,s.Connections.Length,"mesh vertices");Range(row.Topology,s.Connections.Length,"mesh topology");Range(row.Facets,s.Connections.Length,"mesh facets");Range(row.Fins,s.Connections.Length,"mesh fins");}
        for(var i=0;i<s.Lattices.Length;i++){Owner(s.Lattices[i].OwnerKind,s.Lattices[i].Owner,s,"lattice owner");Range(s.Lattices[i].Vertices,s.Connections.Length,"lattice vertices");Range(s.Lattices[i].Rods,s.Connections.Length,"lattice rods");Range(s.Lattices[i].Balls,s.Connections.Length,"lattice balls");Range(s.Lattices[i].IjkBoxes,s.Connections.Length,"lattice IJK boxes");}
        for(var i=0;i<s.Charts.Length;i++)Range(s.Charts[i].HVectors,s.HVectors.Length,"chart h-vectors");
        for(var i=0;i<s.Limits.Length;i++)Range(s.Limits[i].HVectors,s.HVectors.Length,"limit h-vectors");
        for(var i=0;i<s.IntersectionData.Length;i++)Range(s.IntersectionData[i].Values,s.Scalars.Length,"intersection values");
        for(var i=0;i<s.GeometricOwners.Length;i++){Owner(s.GeometricOwners[i].OwnerKind,s.GeometricOwners[i].Owner,s,"geometric owner");OptionalIndex(s.GeometricOwners[i].Next,s.GeometricOwners.Length,"geometric owner next");OptionalIndex(s.GeometricOwners[i].Previous,s.GeometricOwners.Length,"geometric owner previous");if(s.GeometricOwners[i].SharedGeometryKind!=XtOwnerKind.None)Owner(s.GeometricOwners[i].SharedGeometryKind,s.GeometricOwners[i].SharedGeometry,s,"shared geometry");}
        for(var i=0;i<s.Scalars.Length;i++)if(s.Scalars[i].Present!=0&&!double.IsFinite(s.Scalars[i].Value))Invalid("scalar value must be finite");
    }

    public static void ValidateRepresentable(XtSchemaCatalog catalog, XtBrepModel model, string targetSchemaIdentity)
    {
        ArgumentNullException.ThrowIfNull(catalog); Validate(model); _ = catalog.Resolve(targetSchemaIdentity);
        // Detailed row-to-node checks are performed by XtBrepConverter.Encode.
    }

    private static void ValidateAssemblyAcyclic(XtBrepStorage s)
    {
        var state = new byte[s.Assemblies.Length];
        for (var root = 0; root < state.Length; root++) Visit(root);
        void Visit(XtAssemblyIndex assembly)
        {
            if (state[assembly] == 2) return;
            if (state[assembly] == 1) Invalid("assembly graph contains a cycle");
            state[assembly] = 1;
            var range = s.Assemblies[assembly].Instances;
            for (var i = range.Offset; i < range.Offset + range.Count; i++)
            {
                var part = s.Instances[i].Part;
                if ((uint)part >= (uint)s.Parts.Length) continue;
                if (s.Parts[part].Kind == XtPartKind.Assembly) Visit(s.Parts[part].Assembly);
            }
            state[assembly] = 2;
        }
    }

    private static void ValidateBodyAcyclic(XtBrepStorage s)
    {
        var state=new byte[s.Bodies.Length];
        for(var root=0;root<state.Length;root++)Visit(root);
        void Visit(XtBodyIndex body)
        {
            if(state[body]==2)return;if(state[body]==1)Invalid("compound body graph contains a cycle");state[body]=1;
            var child=s.Bodies[body].Child;if(child>=0){Index(child,s.Bodies.Length,"body child");Visit(child);}
            state[body]=2;
        }
    }

    private static void Sense(sbyte value, string name) { if (value is < -1 or > 1) Invalid($"{name} must be -1, 0, or 1"); }
    private static void Index(XtTableIndex value, XtTableCount length, string name) { if ((uint)value >= (uint)length) Invalid($"{name} index {value} is out of range {length}"); }
    private static void OptionalIndex(XtTableIndex value, XtTableCount length, string name) { if (value != -1) Index(value, length, name); }
    private static void Range(XtRange range, XtTableCount length, string name) { if (range.Offset < 0 || range.Count < 0 || range.Offset > length - range.Count) Invalid($"{name} range {range.Offset}+{range.Count} is out of range {length}"); }
    private static void Owner(XtOwnerKind kind,XtTableIndex index,XtBrepStorage s,string name)
    {
        if(kind==XtOwnerKind.None){if(index!=-1)Invalid($"{name} without a kind must be -1");return;}var length=kind switch{XtOwnerKind.Part=>s.Parts.Length,XtOwnerKind.Body=>s.Bodies.Length,XtOwnerKind.Region=>s.Regions.Length,XtOwnerKind.Shell=>s.Shells.Length,XtOwnerKind.Face=>s.Faces.Length,XtOwnerKind.Loop=>s.Loops.Length,XtOwnerKind.Fin=>s.Fins.Length,XtOwnerKind.Edge=>s.Edges.Length,XtOwnerKind.Vertex=>s.Vertices.Length,XtOwnerKind.Point=>s.Points.Length,XtOwnerKind.Geometry=>s.Geometries.Length,XtOwnerKind.Assembly=>s.Assemblies.Length,XtOwnerKind.Instance=>s.Instances.Length,XtOwnerKind.Transform=>s.Transforms.Length,XtOwnerKind.Frame=>s.Frames.Length,XtOwnerKind.Attribute=>s.Attributes.Length,XtOwnerKind.Mesh=>s.Meshes.Length,XtOwnerKind.Lattice=>s.Lattices.Length,_=>0};Index(index,length,name);
    }
    private static void Invalid(string message) => throw new XtFormatException(XtErrorCode.ModelInvalid, message);
}
