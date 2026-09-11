#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Xt;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath())!;
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var check = args.Contains("--check", StringComparer.Ordinal);
var schemaDirectory = ReadOption(args, "--schema-dir") ?? Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR") ?? Environment.GetEnvironmentVariable("P_SCHEMA");
if (string.IsNullOrWhiteSpace(schemaDirectory))
    throw new InvalidOperationException("Pass --schema-dir or set PARASOLID_SCHEMA_DIR/P_SCHEMA.");
schemaDirectory = Path.GetFullPath(Path.Combine(scriptDirectory, schemaDirectory));
var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();

var versions = catalog.Schemas
    .Where(static info => info.ModelerVersion / 100000 is >= 30 and <= 37)
    .OrderBy(static info => info.ModelerVersion)
    .Select(info => new VersionSchema(info.Identity, catalog.Resolve(info.Identity), info.ModelerVersion, false))
    .ToList();
var v37 = versions.Single(static version => version.Definition.SchemaNumber == 37102);
versions.Add(new VersionSchema("SCH_3800150_37102", v37.Definition, 3800150, true));

// XT schema field value enums. Numeric entries are harvested from the Parasolid
// XT Format Reference, "Schema Definitions" chapter; char entries store single
// character codes for schema type 'c' fields and come from the XT topology
// documentation. Sources map either an explicit "<NODE>.<FIELD>" association or
// a bare "<FIELD>" name (used only where the doc associates that name with
// exactly one enum) to a value table; bare names apply to every node declaring
// the field with the same schema type letter.
var xtEnumSources = new Dictionary<string, (char Type, string Source)>(StringComparer.Ordinal)
{
    ["NURBS_CURVE.knot_type"] = ('u', "SCH_knot_type_t"),
    ["NURBS_CURVE.curve_form"] = ('u', "SCH_curve_form_t"),
    ["CURVE_DATA.self_int"] = ('u', "SCH_self_int_t"),
    ["INTERSECTION_DATA.uv_type"] = ('u', "SCH_intersection_uv_type_t"),
    ["NURBS_SURF.u_knot_type"] = ('u', "SCH_knot_type_t"),
    ["NURBS_SURF.v_knot_type"] = ('u', "SCH_knot_type_t"),
    ["NURBS_SURF.surface_form"] = ('u', "SCH_surface_form_t"),
    ["SURFACE_DATA.self_int"] = ('u', "SCH_self_int_t"),
    ["PSM_MESH.precision"] = ('u', "SCH_mesh_precision_t"),
    ["VECTOR_COMB.encoding"] = ('u', "SCH_vector_encoding_t"),
    ["INTEGER_COMB.encoding"] = ('u', "SCH_comb_encoding_t"),
    ["REAL_COMB.encoding"] = ('u', "SCH_comb_encoding_t"),
    ["INSTANCE.type"] = ('u', "SCH_instance_type"),
    ["FEATURE.type"] = ('u', "SCH_feature_type_t"),
    ["PATTERN_AXIAL.hand"] = ('u', "SCH_axial_hand_t"),
    ["TPMS_SURF.tpms_type"] = ('u', "SCH_TPMS_type_t"),
    ["TPMS_SURF.tpms_shift"] = ('u', "SCH_TPMS_shift_t"),
    ["REGION.type"] = ('c', "REGION.type"),
    ["knot_type"] = ('u', "SCH_knot_type_t"),
    ["u_knot_type"] = ('u', "SCH_knot_type_t"),
    ["v_knot_type"] = ('u', "SCH_knot_type_t"),
    ["curve_form"] = ('u', "SCH_curve_form_t"),
    ["self_int"] = ('u', "SCH_self_int_t"),
    ["uv_type"] = ('u', "SCH_intersection_uv_type_t"),
    ["surface_form"] = ('u', "SCH_surface_form_t"),
    ["precision"] = ('u', "SCH_mesh_precision_t"),
    ["normal_type"] = ('u', "SCH_mesh_normal_type_t"),
    ["ball_type"] = ('u', "SCH_lattice_ball_type_t"),
    ["ball_blend_type"] = ('u', "SCH_lattice_ball_blend_type_t"),
    ["rod_term_type"] = ('u', "SCH_lattice_rod_term_type_t"),
    ["rod_mid_type"] = ('u', "SCH_lattice_rod_mid_type_t"),
    ["hand"] = ('u', "SCH_axial_hand_t"),
    ["state"] = ('u', "SCH_part_state"),
    ["body_type"] = ('u', "SCH_body_type"),
    ["nom_geom_state"] = ('u', "SCH_nom_geom_state_t"),
    ["sense"] = ('c', "sense"),
};
var xtEnumValues = new Dictionary<string, (string Name, long Value)[]>(StringComparer.Ordinal)
{
    ["SCH_axial_hand_t"] = new[] { ("right", 0L), ("left", 1L) },
    ["SCH_assembly_type"] = new[] { ("collective_assembly", 1L), ("conjunctive_assembly", 2L), ("disjunctive_assembly", 3L) },
    ["SCH_body_type"] = new[] { ("solid_body", 1L), ("wire_body", 2L), ("sheet_body", 3L), ("general_body", 6L) },
    ["SCH_comb_encoding_t"] = new[] { ("no_encoding", 1L) },
    ["SCH_curve_form_t"] = new[] { ("unset", 1L), ("arbitrary", 2L), ("polyline", 3L), ("circular_arc", 4L), ("elliptic_arc", 5L), ("parabolic_arc", 6L), ("hyperbolic_arc", 7L), ("helical_arc", 8L) },
    // ("helical_arc", 8) is not covered by the XT Format Reference table (stops
    // at 7); it is observed on b-curves created by PK_POINT_make_helical_curve
    // in the Parasolid corpus (body.wire.helical-curve cases) and follows the
    // documented arc-name pattern.
    ["SCH_feature_type_t"] = new[] { ("instance_fe", 1L), ("face_fe", 2L), ("loop_fe", 3L), ("edge_fe", 4L), ("vertex_fe", 5L), ("surface_fe", 6L), ("curve_fe", 7L), ("point_fe", 8L), ("mixed_fe", 9L), ("region_fe", 10L), ("pf_pline_fe", 11L), ("feature_fe", 12L) },
    ["SCH_instance_type"] = new[] { ("positive_instance", 1L), ("negative_instance", 2L) },
    ["SCH_intersection_uv_type_t"] = new[] { ("none", 1L), ("first", 2L), ("second", 3L), ("both", 4L) },
    ["SCH_knot_type_t"] = new[] { ("unset", 1L), ("non_uniform", 2L), ("uniform", 3L), ("quasi_uniform", 4L), ("piecewise_bezier", 5L), ("bezier_ends", 6L) },
    ["SCH_lattice_ball_blend_type_t"] = new[] { ("none", 0L), ("absolute", 1L), ("relative", 2L) },
    ["SCH_lattice_ball_type_t"] = new[] { ("unset", 0L), ("const", 1L), ("variable", 2L) },
    ["SCH_lattice_rod_mid_type_t"] = new[] { ("unset", 0L), ("none", 1L), ("const", 2L), ("variable", 3L) },
    ["SCH_lattice_rod_term_type_t"] = new[] { ("unset", 0L), ("const", 1L), ("derived", 2L), ("variable_1", 3L), ("variable_2", 4L) },
    ["SCH_mesh_normal_type_t"] = new[] { ("none", 1L), ("per_vertex", 2L), ("per_facet", 3L) },
    ["SCH_mesh_precision_t"] = new[] { ("double", 1L), ("single", 2L) },
    ["SCH_nom_geom_state_t"] = new[] { ("off", 1L), ("on", 2L) },
    ["SCH_part_state"] = new[] { ("new_part", 1L), ("stored_part", 2L), ("modified_part", 3L), ("anonymous_part", 4L), ("unloaded_part", 5L) },
    ["SCH_self_int_t"] = new[] { ("unset", 1L), ("no_self_intersections", 2L), ("self_intersects", 3L), ("checked_ok_in_old_version", 4L) },
    ["SCH_surface_form_t"] = new[] { ("unset", 1L), ("arbitrary", 2L), ("planar", 3L), ("cylindrical", 4L), ("conical", 5L), ("spherical", 6L), ("toroidal", 7L), ("surf_of_revolution", 8L), ("ruled", 9L), ("quadric", 10L), ("swept", 11L) },
    ["SCH_TPMS_shift_t"] = new[] { ("none", 0L), ("half", 1L) },
    ["SCH_TPMS_type_t"] = new[] { ("unset", 0L), ("gyroid", 1L), ("lidinoid", 2L), ("neovius", 3L), ("schoen", 4L), ("schwarz_d", 5L), ("schwarz_p", 6L), ("split_p", 7L), ("schoen_octo", 8L) },
    ["SCH_vector_encoding_t"] = new[] { ("simple", 1L), ("spherical", 2L) },
    ["REGION.type"] = new[] { ("solid", (long)'S'), ("void", (long)'V') },
    ["sense"] = new[] { ("positive", (long)'+'), ("negative", (long)'-') },
};

var bindings = GenerateBindings(versions);
var descriptors = GenerateDescriptors(versions);
var headers = GenerateHeaders(versions);
var nativeExports = GenerateNativeExports(versions);
var mapping = GenerateMapping(versions);

WriteOrCheck(Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt", "Generated", "XtSchemaBindings.generated.cs"), bindings);
WriteOrCheck(Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt", "Generated", "XtBuiltInSchemas.generated.cs"), descriptors);
foreach (var pair in headers)
    WriteOrCheck(Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt.Native", "include", pair.Key), pair.Value);
var headerDirectory = Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt.Native", "include");
var expectedHeaders = headers.Keys.ToHashSet(StringComparer.Ordinal);
foreach (var staleHeader in Directory.EnumerateFiles(headerDirectory, "ProjectGmKernel.Xt.SCH_*.h", SearchOption.TopDirectoryOnly).Where(path => !expectedHeaders.Contains(Path.GetFileName(path))))
{
    if (check) throw new InvalidOperationException($"Stale generated schema header: {Path.GetRelativePath(repositoryRoot, staleHeader)}");
    File.Delete(staleHeader);
}
WriteOrCheck(Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt.Native", "Generated", "XtSchemaExports.generated.cs"), nativeExports);
WriteOrCheck(Path.Combine(repositoryRoot, "docs", "xt_schema_generated_mapping.md"), mapping);
Console.WriteLine($"Generated {versions.Count} V30-V38 schema bindings ({versions.Sum(static version => version.Definition.Nodes.Length)} node types)." );
return;

string GenerateBindings(List<VersionSchema> schemas)
{
    var output = Header("C# schema bindings");
    output.AppendLine("using System.Runtime.CompilerServices;");
    output.AppendLine("using System.Runtime.InteropServices;");
    output.AppendLine("using ProjectGmKernel.Xt;");
    foreach (var version in schemas)
    {
        var ns = Namespace(version.Identity);
        var transmitted = version.Definition.Nodes.ToArray().Where(static node => node.Transmit).ToArray();
        output.Append("namespace ProjectGmKernel.Xt.Schema.").Append(ns).AppendLine();
        output.AppendLine("{");
        output.AppendLine();
        foreach (var target in RefTargets(version))
            output.Append("[StructLayout(LayoutKind.Sequential)] public struct ").Append(target.Name).AppendLine("Ref { public XtNodeIndex Index; }");
        output.AppendLine();
        foreach (var node in version.Definition.Nodes)
        {
            var fields = version.Definition.GetFields(node).ToArray();
            foreach (var field in fields)
            {
                if (XtEnumFor(node.Name, field.Name, field.Type) is not { } spec)
                    continue;
                output.Append("public enum ").Append(node.Name).Append("__").Append(field.Name).Append(" : ").Append(field.Type == 'c' ? "byte" : "ulong").Append(" { ");
                for (var index = 0; index < spec.Values.Length; index++)
                {
                    if (index > 0) output.Append(", ");
                    output.Append(CsName(spec.Values[index].Name)).Append(" = ").Append(EnumValueLiteral(spec.Values[index].Value, field.Type == 'c', cSharp: true));
                }
                output.AppendLine(" }");
            }
            output.AppendLine();
            foreach (var field in fields.Where(static field => field.ElementCount > 1))
                output.Append("[InlineArray(").Append(field.ElementCount).Append(")] public struct ").Append(node.Name).Append("__").Append(field.Name).Append("__ARRAY { private ").Append(ManagedFieldElementType(version, field)).AppendLine(" _element0; }");
            output.AppendLine("[StructLayout(LayoutKind.Sequential)]");
            output.Append("public struct ").Append(node.Name).AppendLine();
            output.AppendLine("{");
            output.AppendLine("    public XtNodeIndex _xt_index;");
            output.AppendLine("    public XtTableIndex _xt_order;");
            if (node.Variable) output.AppendLine("    public XtVariableLength _xt_variable_length;");
            if (node.Transmit) output.AppendLine("    public XtRange _xt_user_fields;");
            foreach (var field in fields)
            {
                var type = field.ElementCount switch
                {
                    1 => "XtRange",
                    > 1 => node.Name + "__" + field.Name + "__ARRAY",
                    _ => ManagedFieldElementType(version, field),
                };
                output.Append("    public ").Append(type).Append(' ').Append(CsName(field.Name)).AppendLine(";");
            }
            output.AppendLine("}");
            output.AppendLine();
        }

        output.AppendLine("[StructLayout(LayoutKind.Sequential)] public struct COUNTS");
        output.AppendLine("{");
        foreach (var node in transmitted)
        {
            output.Append("    public XtTableCount ").Append(node.Name).AppendLine(";");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("    public XtTableCount ").Append(node.Name).Append("__").Append(field.Name).AppendLine(";");
        }
        output.AppendLine("    public XtTableCount _xt_user_fields;");
        output.AppendLine("}");
        output.AppendLine();

        output.AppendLine("internal sealed class STORAGE");
        output.AppendLine("{");
        foreach (var node in transmitted)
        {
            output.Append("    internal readonly ").Append(node.Name).Append("[] ").Append(node.Name).AppendLine(";");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("    internal readonly ").Append(ManagedFieldElementType(version, field)).Append("[] ").Append(node.Name).Append("__").Append(field.Name).AppendLine(";");
        }
        output.AppendLine("    internal readonly int[] _xt_user_fields;");
        output.AppendLine("    internal STORAGE(COUNTS counts)");
        output.AppendLine("    {");
        foreach (var node in transmitted)
        {
            output.Append("        ").Append(node.Name).Append(" = A<").Append(node.Name).Append(">(counts.").Append(node.Name).AppendLine(");");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("        ").Append(node.Name).Append("__").Append(field.Name).Append(" = A<").Append(ManagedFieldElementType(version, field)).Append(">(counts.").Append(node.Name).Append("__").Append(field.Name).AppendLine(");");
        }
        output.AppendLine("        _xt_user_fields = A<int>(counts._xt_user_fields);");
        output.AppendLine("        static T[] A<T>(int count) => count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : GC.AllocateArray<T>(count, pinned: true);");
        output.AppendLine("    }");
        output.AppendLine("}");

        output.AppendLine("public sealed class MODEL : IXtSchemaModel");
        output.AppendLine("{");
        output.AppendLine("    internal readonly STORAGE Storage;");
        output.AppendLine("    internal MODEL(STORAGE storage, string versionText, int userFieldSize) { Storage = storage; VersionText = versionText; UserFieldSize = userFieldSize; }");
        output.AppendLine("    public string VersionText { get; }");
        output.AppendLine("    public int UserFieldSize { get; }");
        output.AppendLine("    public string SchemaIdentity => CODEC.SchemaIdentity;");
        output.AppendLine("    public XtDocument ToDocument() => CODEC.Encode(this);");
        foreach (var node in transmitted)
        {
            output.Append("    public ReadOnlySpan<").Append(node.Name).Append("> ").Append(node.Name).Append(" => Storage.").Append(node.Name).AppendLine(";");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("    public ReadOnlySpan<").Append(ManagedFieldElementType(version, field)).Append("> ").Append(node.Name).Append("__").Append(field.Name).Append(" => Storage.").Append(node.Name).Append("__").Append(field.Name).AppendLine(";");
        }
        output.AppendLine("    public ReadOnlySpan<int> _xt_user_fields => Storage._xt_user_fields;");
        output.AppendLine("}");

        output.AppendLine("public sealed class MODEL_BUILDER");
        output.AppendLine("{");
        output.AppendLine("    internal readonly STORAGE Storage; private bool _finalized;");
        output.AppendLine("    public MODEL_BUILDER(COUNTS counts) { Storage = new STORAGE(counts); }");
        output.Append("    public string VersionText { get; set; } = \": TRANSMIT FILE created by ProjectGmKernel.Xt for modeller version ").Append(version.ProducerVersion).AppendLine("\";");
        output.AppendLine("    public int UserFieldSize { get; set; }");
        foreach (var node in transmitted)
        {
            output.Append("    public Span<").Append(node.Name).Append("> ").Append(node.Name).Append(" => Writable(Storage.").Append(node.Name).AppendLine(");");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("    public Span<").Append(ManagedFieldElementType(version, field)).Append("> ").Append(node.Name).Append("__").Append(field.Name).Append(" => Writable(Storage.").Append(node.Name).Append("__").Append(field.Name).AppendLine(");");
        }
        output.AppendLine("    public Span<int> _xt_user_fields => Writable(Storage._xt_user_fields);");
        output.AppendLine("    public MODEL FinalizeModel() { if (_finalized) throw new InvalidOperationException(\"Model is already finalized.\"); CODEC.Validate(Storage, UserFieldSize); _finalized = true; return new MODEL(Storage, VersionText, UserFieldSize); }");
        output.AppendLine("    private Span<T> Writable<T>(T[] values) { if (_finalized) throw new InvalidOperationException(\"Model is finalized.\"); return values; }");
        output.AppendLine("}");

        GenerateCodec(output, version, transmitted);
        output.AppendLine("}");
    }
    output.AppendLine("namespace ProjectGmKernel.Xt");
    output.AppendLine("{");
    output.AppendLine("public static class XtGeneratedModelCodec");
    output.AppendLine("{");
    output.AppendLine("    public static IXtSchemaModel Decode(XtDocument document)");
    output.AppendLine("    {");
    output.AppendLine("        ArgumentNullException.ThrowIfNull(document); switch(document.Schema.Identity)");
    output.AppendLine("        {");
    foreach(var version in schemas)
        output.Append("            case \"").Append(version.Identity).Append("\": return global::ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).AppendLine(".CODEC.Decode(document);");
    output.AppendLine("        }");
    foreach(var version in schemas.OrderByDescending(static version=>version.ProducerVersion))
        output.Append("        if(document.Schema.Identity.StartsWith(\"").Append(version.Identity).Append("_\",StringComparison.Ordinal)&&XtGeneratedSchemaRuntime.CompatibleShape(document.Schema,global::ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).Append(".DESCRIPTOR.Definition))return global::ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).AppendLine(".CODEC.Decode(document);");
    foreach(var version in schemas.Where(static version=>!version.Alias))
        output.Append("        if(XtGeneratedSchemaRuntime.CompatibleShape(document.Schema,global::ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).Append(".DESCRIPTOR.Definition))return global::ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).AppendLine(".CODEC.Decode(document);");
    output.AppendLine("        throw new XtFormatException(XtErrorCode.UnsupportedVersion, $\"No generated model binding exists for {document.Schema.Identity}.\");");
    output.AppendLine("    }");
    output.AppendLine("    public static XtDocument RoundTrip(XtDocument document) => Decode(document).ToDocument();");
    output.AppendLine("}");
    output.AppendLine("}");
    return output.ToString();
}

void GenerateCodec(StringBuilder output, VersionSchema version, XtNodeDescriptor[] transmitted)
{
    output.AppendLine("public static class CODEC");
    output.AppendLine("{");
    output.Append("    public const string SchemaIdentity = \"").Append(version.Identity).AppendLine("\";");
    output.AppendLine("    public static MODEL Decode(XtDocument document)");
    output.AppendLine("    {");
    output.AppendLine("        ArgumentNullException.ThrowIfNull(document); XtGeneratedSchemaRuntime.RequireShape(document.Schema, DESCRIPTOR.Definition);");
    output.AppendLine("        var counts = new COUNTS();");
    output.AppendLine("        foreach (var node in document.Nodes) switch (node.Type)");
    output.AppendLine("        {");
    foreach (var node in transmitted)
    {
        output.Append("            case ").Append(node.Type).Append(": counts.").Append(node.Name).AppendLine("++;");
        foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
            output.Append("                counts.").Append(node.Name).Append("__").Append(field.Name).AppendLine(" += Math.Max(0, node.VariableLength);");
        output.AppendLine("                counts._xt_user_fields += node.UserFields.Length; break;");
    }
    output.AppendLine("            default: throw new XtFormatException(XtErrorCode.ModelInvalid, $\"Node type {node.Type} is not transmitted by {SchemaIdentity}.\");");
    output.AppendLine("        }");
    output.AppendLine("        var builder = new MODEL_BUILDER(counts) { VersionText = document.VersionText, UserFieldSize = document.UserFieldSize };");
    foreach (var node in transmitted)
    {
        output.Append("        var ").Append(node.Name).AppendLine("_index = 0;");
        foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
            output.Append("        var ").Append(node.Name).Append("__").Append(field.Name).AppendLine("_offset = 0;");
    }
    output.AppendLine("        var userFieldOffset = 0;");
    output.AppendLine("        for (var order = 0; order < document.Nodes.Length; order++)");
    output.AppendLine("        {");
    output.AppendLine("            var node = document.Nodes[order]; switch (node.Type)");
    output.AppendLine("            {");
    foreach (var node in transmitted)
        GenerateDecodeCase(output, version, node);
    output.AppendLine("            }");
    output.AppendLine("        }");
    output.AppendLine("        return builder.FinalizeModel();");
    output.AppendLine("    }");

    output.AppendLine("    public static XtDocument Encode(MODEL model)");
    output.AppendLine("    {");
    output.AppendLine("        ArgumentNullException.ThrowIfNull(model); return EncodeStorage(model.Storage, model.VersionText, model.UserFieldSize);");
    output.AppendLine("    }");
    output.AppendLine("    internal static void Validate(STORAGE storage, int userFieldSize) => _ = EncodeStorage(storage, string.Empty, userFieldSize);");
    output.AppendLine("    private static XtDocument EncodeStorage(STORAGE storage, string versionText, int userFieldSize)");
    output.AppendLine("    {");
    output.AppendLine("        var nodes = new List<XtNode>(); var indexes = new HashSet<int>(); var orders = new HashSet<int>(); var nodeTypes = new Dictionary<int,int>();");
    foreach(var node in transmitted)
        output.Append("        foreach(ref readonly var row in storage.").Append(node.Name).Append(".AsSpan()){XtGeneratedSchemaRuntime.ValidateMetadata(row._xt_index,row._xt_order,").Append(node.Variable?"row._xt_variable_length":"0").Append(',').Append(node.Variable?"true":"false").Append(",row._xt_user_fields,userFieldSize,storage._xt_user_fields.Length,indexes,orders);nodeTypes.Add(row._xt_index,").Append(node.Type).AppendLine(");}");
    foreach (var node in transmitted)
        GenerateEncodeTable(output, version, node);
    output.AppendLine("        nodes.Sort(static (left, right) => left.TransmitOrder.CompareTo(right.TransmitOrder));");
    output.AppendLine("        var document = new XtDocument { VersionText = versionText, HeaderSchemaIdentity = SchemaIdentity, Schema = DESCRIPTOR.Definition, UserFieldSize = userFieldSize, Nodes = nodes.ToArray() }; if(nodes.Count!=0)_=XtPartGraph.GetRootIndexes(document); return document;");
    output.AppendLine("    }");
    output.AppendLine("}");
    output.AppendLine("public static class DESCRIPTOR { public static XtSchemaDefinition Definition => XtBuiltInSchemas.Resolve(SchemaIdentity); private const string SchemaIdentity = CODEC.SchemaIdentity; }");
}

void GenerateDecodeCase(StringBuilder output, VersionSchema version, XtNodeDescriptor node)
{
    var fields = version.Definition.GetFields(node).ToArray();
    output.Append("                case ").Append(node.Type).AppendLine(":");
    output.AppendLine("                {");
    output.Append("                    ref var row = ref builder.").Append(node.Name).Append('[').Append(node.Name).AppendLine("_index++];");
    output.AppendLine("                    row._xt_index = node.Index; row._xt_order = order;");
    if(node.Variable)output.AppendLine("                    row._xt_variable_length = Math.Max(0, node.VariableLength);");
    output.AppendLine("                    row._xt_user_fields = new XtRange { Offset = userFieldOffset, Count = node.UserFields.Length };");
    output.AppendLine("                    node.UserFields.CopyTo(builder._xt_user_fields[userFieldOffset..]); userFieldOffset += node.UserFields.Length; var valueOffset = 0;");
    foreach (var field in fields)
    {
        var member = "row." + CsName(field.Name);
        if (!field.Transmit) continue;
        if (field.ElementCount == 1)
        {
            var offset = node.Name + "__" + field.Name + "_offset";
            var pool = "builder." + node.Name + "__" + field.Name;
            output.Append("                    ").Append(member).Append(" = new XtRange { Offset = ").Append(offset).AppendLine(", Count = Math.Max(0, node.VariableLength) };");
            output.Append("                    for (var item = 0; item < Math.Max(0, node.VariableLength); item++) ").Append(pool).Append('[').Append(offset).Append("++] = ").Append(DecodeValue(version, node, field, "node.Fields[valueOffset++]")).AppendLine(";");
        }
        else if (field.ElementCount > 1)
        {
            output.Append("                    for (var item = 0; item < ").Append(field.ElementCount).Append("; item++) ").Append(member).Append("[item] = ").Append(DecodeValue(version, node, field, "node.Fields[valueOffset++]")).AppendLine(";");
        }
        else
            output.Append("                    ").Append(member).Append(" = ").Append(DecodeValue(version, node, field, "node.Fields[valueOffset++]")).AppendLine(";");
    }
    output.AppendLine("                    if (valueOffset != node.Fields.Length) throw new XtFormatException(XtErrorCode.ModelInvalid, $\"Node {node.Type}/{node.Index} field count mismatch.\"); break;");
    output.AppendLine("                }");
}

void GenerateEncodeTable(StringBuilder output, VersionSchema version, XtNodeDescriptor node)
{
    var fields = version.Definition.GetFields(node).ToArray();
    output.Append("        foreach (ref readonly var row in storage.").Append(node.Name).AppendLine(".AsSpan())");
    output.AppendLine("        {");
    output.AppendLine("            var values = new List<XtFieldValue>();");
    foreach (var field in fields)
    {
        var member = "row." + CsName(field.Name);
        var label = version.Identity + "." + node.Name + "." + field.Name;
        if (!field.Transmit) continue;
        if (field.ElementCount == 1)
        {
            var pool = "storage." + node.Name + "__" + field.Name;
            output.Append("            XtGeneratedSchemaRuntime.ValidateVariableRange(").Append(member).Append(", row._xt_variable_length, ").Append(pool).Append(".Length, \"").Append(label).AppendLine("\");");
            output.Append("            for (var item = 0; item < ").Append(member).Append(".Count; item++) {");
            output.Append(PointerValidation(version, field, pool + "[" + member + ".Offset+item]", label));
            output.Append("values.Add(").Append(EncodeValue(version, field, pool + "[" + member + ".Offset+item]")).AppendLine("); }");
        }
        else if (field.ElementCount > 1)
        {
            output.Append("            for (var item = 0; item < ").Append(field.ElementCount).Append("; item++) { ");
            output.Append(PointerValidation(version, field, member + "[item]", label));
            output.Append("values.Add(").Append(EncodeValue(version, field, member + "[item]")).AppendLine("); }");
        }
        else
        {
            output.Append("            ");
            output.Append(PointerValidation(version, field, member, label));
            output.Append("values.Add(").Append(EncodeValue(version, field, member)).AppendLine(");");
        }
    }
    output.AppendLine("            var users = row._xt_user_fields.Count == 0 ? Array.Empty<int>() : storage._xt_user_fields.AsSpan(row._xt_user_fields.Offset, row._xt_user_fields.Count).ToArray();");
    output.Append("            var generated = new XtNode { Type = ").Append(node.Type).Append(", Index = row._xt_index, VariableLength = ").Append(node.Variable?"row._xt_variable_length":"0").AppendLine(", Fields = values.ToArray(), UserFields = users, TransmitOrder = row._xt_order }; nodes.Add(generated);");
    output.AppendLine("        }");
}

string GenerateDescriptors(List<VersionSchema> schemas)
{
    var output = Header("built-in schema descriptors");
    output.AppendLine("namespace ProjectGmKernel.Xt;");
    output.AppendLine("public static class XtBuiltInSchemas");
    output.AppendLine("{");
    output.AppendLine("    private static readonly Dictionary<string, XtSchemaDefinition> Definitions = Create();");
    output.AppendLine("    public static IReadOnlyCollection<string> Identities => Definitions.Keys;");
    output.AppendLine("    public static bool TryResolve(string identity, out XtSchemaDefinition definition) => Definitions.TryGetValue(identity, out definition!);");
    output.AppendLine("    public static XtSchemaDefinition Resolve(string identity) => TryResolve(identity, out var definition) ? definition : throw new XtFormatException(XtErrorCode.SchemaNotFound, $\"Built-in XT schema {identity} is unavailable.\");");
    output.AppendLine("    private static Dictionary<string, XtSchemaDefinition> Create()");
    output.AppendLine("    {");
    output.AppendLine("        var result = new Dictionary<string, XtSchemaDefinition>(StringComparer.Ordinal);");
    foreach (var version in schemas)
    {
        output.AppendLine("        {");
        output.AppendLine("            var nodes = new XtNodeDescriptor[]");
        output.AppendLine("            {");
        foreach (var node in version.Definition.Nodes)
            output.Append("                new(").Append(node.Type).Append(", \"").Append(Escape(node.Name)).Append("\", \"").Append(Escape(node.Description)).Append("\", ").Append(Bool(node.Transmit)).Append(", ").Append(node.DeclaredFieldCount).Append(", ").Append(Bool(node.Variable)).Append(", ").Append(node.FieldOffset).Append(", ").Append(node.ParsedFieldCount).AppendLine("),");
        output.AppendLine("            };");
        output.AppendLine("            var fields = new XtFieldDescriptor[]");
        output.AppendLine("            {");
        foreach (var field in version.Definition.Fields)
            output.Append("                new(").Append(field.OwnerType).Append(", \"").Append(Escape(field.Name)).Append("\", '").Append(field.Type).Append("', ").Append(Bool(field.Transmit)).Append(", ").Append(field.NodeClass).Append(", ").Append(field.ElementCount).AppendLine("),");
        output.AppendLine("            };");
        output.Append("            result.Add(\"").Append(version.Identity).Append("\", new XtSchemaDefinition(new XtSchemaInfo(\"").Append(version.Identity).Append("\", \"<built-in>\", ").Append(version.ProducerVersion).Append(", ").Append(version.Definition.SchemaNumber).Append(", nodes.Length, fields.Length), nodes, fields));").AppendLine();
        output.AppendLine("        }");
    }
    output.AppendLine("        return result;");
    output.AppendLine("    }");
    output.AppendLine("}");
    return output.ToString();
}

Dictionary<string, string> GenerateHeaders(List<VersionSchema> schemas)
{
    var headers = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var version in schemas)
    {
        var guard = "PROJECT_GM_KERNEL_XT_" + version.Identity + "_H";
        var output = new StringBuilder().AppendLine("/* <auto-generated/> schema-derived types; no schema source text is embedded. */")
            .Append("#ifndef ").AppendLine(guard).Append("#define ").AppendLine(guard)
            .AppendLine("#ifdef PGM_XT_SCHEMA_HEADER_INCLUDED")
            .AppendLine("#error \"Only one ProjectGmKernel.Xt schema header may be included in a translation unit.\"")
            .AppendLine("#endif")
            .AppendLine("#define PGM_XT_SCHEMA_HEADER_INCLUDED 1")
            .Append("#define PGM_XT_SCHEMA_IDENTITY \"").Append(version.Identity).AppendLine("\"")
            .AppendLine("#include \"ProjectGmKernel.Xt.h\"")
            .AppendLine("#ifdef __cplusplus").AppendLine("extern \"C\" {").AppendLine("#endif");
        var prefix = "PGM_XT_" + version.Identity;
        foreach (var target in RefTargets(version))
        {
            output.Append("typedef struct PGM_XT_").Append(target.Name).AppendLine("_ref_s {");
            output.AppendLine("    int32_t index;");
            output.Append("} PGM_XT_").Append(target.Name).AppendLine("_ref_t;");
        }
        foreach (var node in version.Definition.Nodes)
        {
            var fields = version.Definition.GetFields(node).ToArray();
            foreach (var field in fields)
            {
                if (XtEnumFor(node.Name, field.Name, field.Type) is not { } spec)
                    continue;
                output.Append("/* XT schema enum ").Append(spec.Source).AppendLine("; source: Parasolid XT Format Reference */");
                output.Append("enum PGM_XT_").Append(node.Name).Append('_').Append(field.Name).AppendLine("_e {");
                for (var index = 0; index < spec.Values.Length; index++)
                {
                    output.Append("    PGM_XT_").Append(node.Name).Append('_').Append(field.Name).Append('_').Append(spec.Values[index].Name).Append(" = ").Append(EnumValueLiteral(spec.Values[index].Value, field.Type == 'c', cSharp: false)).AppendLine(",");
                }
                output.AppendLine("};");
            }
            output.Append("typedef struct PGM_XT_").Append(node.Name).AppendLine("_s {");
            output.AppendLine("    int32_t _xt_index;");
            output.AppendLine("    int32_t _xt_order;");
            if (node.Variable) output.AppendLine("    int32_t _xt_variable_length;");
            if (node.Transmit) output.AppendLine("    PGM_XT_range_t _xt_user_fields;");
            foreach (var field in fields)
            {
                var type = field.ElementCount == 1 ? "PGM_XT_range_t" : CFieldElementType(version, field);
                var suffix = field.ElementCount > 1 ? "[" + field.ElementCount + "]" : "";
                if (IsCppKeyword(field.Name))
                {
                    output.AppendLine("#ifdef __cplusplus");
                    output.Append("    ").Append(type).Append(' ').Append(field.Name).Append('_').Append(suffix).AppendLine(";");
                    output.AppendLine("#else");
                    output.Append("    ").Append(type).Append(' ').Append(field.Name).Append(suffix).AppendLine(";");
                    output.AppendLine("#endif");
                }
                else output.Append("    ").Append(type).Append(' ').Append(field.Name).Append(suffix).AppendLine(";");
            }
            output.Append("} PGM_XT_").Append(node.Name).AppendLine("_t;");
        }
        output.AppendLine("typedef struct PGM_XT_COUNTS_s {");
        foreach (var node in version.Definition.Nodes.ToArray().Where(static node => node.Transmit))
        {
            output.Append("    int32_t ").Append(node.Name).AppendLine(";");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
                output.Append("    int32_t ").Append(node.Name).Append("__").Append(field.Name).AppendLine(";");
        }
        output.AppendLine("    int32_t _xt_user_fields;");
        output.AppendLine("} PGM_XT_COUNTS_t;");
        output.Append("#define PGM_XT_MODEL_create ").AppendLine(prefix + "_MODEL_create");
        output.Append("#define PGM_XT_MODEL_finalize ").AppendLine(prefix + "_MODEL_finalize");
        output.Append("#define PGM_XT_MODEL_delete ").AppendLine(prefix + "_MODEL_delete");
        output.Append("#define PGM_XT_DOCUMENT_to_MODEL PGM_XT_DOCUMENT_to_").Append(version.Identity).AppendLine("_MODEL");
        output.Append("#define PGM_XT_MODEL_to_DOCUMENT PGM_XT_").Append(version.Identity).AppendLine("_MODEL_to_DOCUMENT");
        output.AppendLine("PGM_XT_API PGM_XT_status_t PGM_XT_MODEL_create(const PGM_XT_COUNTS_t *, PGM_XT_model_t *);");
        output.AppendLine("PGM_XT_API PGM_XT_status_t PGM_XT_MODEL_finalize(PGM_XT_model_t);");
        output.AppendLine("PGM_XT_API PGM_XT_status_t PGM_XT_MODEL_delete(PGM_XT_model_t);");
        output.AppendLine("PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_to_MODEL(PGM_XT_document_t, PGM_XT_model_t *);");
        output.AppendLine("PGM_XT_API PGM_XT_status_t PGM_XT_MODEL_to_DOCUMENT(PGM_XT_model_t, PGM_XT_document_t *);");
        foreach (var node in version.Definition.Nodes.ToArray().Where(static node => node.Transmit))
        {
            output.Append("#define PGM_XT_").Append(node.Name).Append("_get_read_view ").Append(prefix).Append('_').Append(node.Name).AppendLine("_get_read_view");
            output.Append("#define PGM_XT_").Append(node.Name).Append("_get_write_view ").Append(prefix).Append('_').Append(node.Name).AppendLine("_get_write_view");
            output.Append("PGM_XT_API PGM_XT_status_t PGM_XT_").Append(node.Name).Append("_get_read_view(PGM_XT_model_t, const PGM_XT_").Append(node.Name).AppendLine("_t **, int32_t *);");
            output.Append("PGM_XT_API PGM_XT_status_t PGM_XT_").Append(node.Name).Append("_get_write_view(PGM_XT_model_t, PGM_XT_").Append(node.Name).AppendLine("_t **, int32_t *);");
            foreach (var field in version.Definition.GetFields(node).ToArray().Where(static field => field.Transmit && field.ElementCount == 1))
            {
                output.Append("#define PGM_XT_").Append(node.Name).Append('_').Append(field.Name).Append("_get_read_view ").Append(prefix).Append('_').Append(node.Name).Append('_').Append(field.Name).AppendLine("_get_read_view");
                output.Append("#define PGM_XT_").Append(node.Name).Append('_').Append(field.Name).Append("_get_write_view ").Append(prefix).Append('_').Append(node.Name).Append('_').Append(field.Name).AppendLine("_get_write_view");
                output.Append("PGM_XT_API PGM_XT_status_t PGM_XT_").Append(node.Name).Append('_').Append(field.Name).Append("_get_read_view(PGM_XT_model_t, const ").Append(CFieldElementType(version, field)).AppendLine(" **, int32_t *);");
                output.Append("PGM_XT_API PGM_XT_status_t PGM_XT_").Append(node.Name).Append('_').Append(field.Name).Append("_get_write_view(PGM_XT_model_t, ").Append(CFieldElementType(version, field)).AppendLine(" **, int32_t *);");
            }
        }
        output.AppendLine("#ifdef __cplusplus").AppendLine("}").AppendLine("#endif");
        output.Append("#endif /* ").Append(guard).AppendLine(" */");
        headers.Add("ProjectGmKernel.Xt." + version.Identity + ".h", output.ToString());
    }
    return headers;
}

string GenerateNativeExports(List<VersionSchema> schemas)
{
    var output=Header("NativeAOT schema-specific exports");
    output.AppendLine("using System.Runtime.CompilerServices;");
    output.AppendLine("using System.Runtime.InteropServices;");
    output.AppendLine("using ProjectGmKernel.Xt;");
    output.AppendLine("namespace ProjectGmKernel.Xt.Native;");
    output.AppendLine("public static unsafe class GeneratedSchemaExports");
    output.AppendLine("{");
    foreach(var version in schemas)
    {
        var managed="global::ProjectGmKernel.Xt.Schema."+Namespace(version.Identity);
        var prefix="PGM_XT_"+version.Identity;
        output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \""+prefix+"_MODEL_create\", CallConvs = [typeof(CallConvCdecl)])]");
        output.Append("    public static PgmXtStatus ").Append(Safe(prefix)).Append("_MODEL_create(").Append(managed).AppendLine(".COUNTS* counts, PgmXtHandle* model)");
        output.AppendLine("    { if(counts is null||model is null)return NativeExports.InvalidArgument(\"Model counts or output is null.\");try{*model=Handles.Add(new SchemaModelHolder(\""+version.Identity+"\",new "+managed+".MODEL_BUILDER(*counts)));return NativeExports.Ok();}catch(Exception exception){return NativeExports.Fail(exception);} }");
        output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \""+prefix+"_MODEL_finalize\", CallConvs = [typeof(CallConvCdecl)])]");
        output.Append("    public static PgmXtStatus ").Append(Safe(prefix)).AppendLine("_MODEL_finalize(PgmXtHandle model)");
        output.AppendLine("    { if(!NativeExports.TrySchemaHolder(model,\""+version.Identity+"\",out var holder))return NativeExports.InvalidHandle();if(holder!.Model is not null)return NativeExports.InvalidState(\"Model is already finalized.\");try{holder.Model=(("+managed+".MODEL_BUILDER)holder.Builder).FinalizeModel();return NativeExports.Ok();}catch(Exception exception){return NativeExports.Fail(exception);} }");
        output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \""+prefix+"_MODEL_delete\", CallConvs = [typeof(CallConvCdecl)])]");
        output.Append("    public static PgmXtStatus ").Append(Safe(prefix)).AppendLine("_MODEL_delete(PgmXtHandle model) => NativeExports.DeleteSchemaHolder(model,\""+version.Identity+"\");");
        output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \"PGM_XT_DOCUMENT_to_"+version.Identity+"_MODEL\", CallConvs = [typeof(CallConvCdecl)])]");
        output.Append("    public static PgmXtStatus DOCUMENT_to_").Append(Safe(version.Identity)).AppendLine("_MODEL(PgmXtHandle document,PgmXtHandle* model)");
        output.AppendLine("    {if(!Handles.TryGet(document,out DocumentHolder? documentHolder))return NativeExports.InvalidHandle();if(model is null)return NativeExports.InvalidArgument(\"Model output is null.\");try{var typed="+managed+".CODEC.Decode(documentHolder!.Document);*model=Handles.Add(new SchemaModelHolder(\""+version.Identity+"\",typed,true));return NativeExports.Ok();}catch(Exception exception){return NativeExports.Fail(exception);} }");
        output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \"PGM_XT_"+version.Identity+"_MODEL_to_DOCUMENT\", CallConvs = [typeof(CallConvCdecl)])]");
        output.Append("    public static PgmXtStatus ").Append(Safe(version.Identity)).AppendLine("_MODEL_to_DOCUMENT(PgmXtHandle model,PgmXtHandle* document)");
        output.AppendLine("    {if(!NativeExports.TrySchemaHolder(model,\""+version.Identity+"\",out var holder))return NativeExports.InvalidHandle();if(document is null||holder!.Model is not "+managed+".MODEL typed)return NativeExports.InvalidState(\"Finalized model and document output are required.\");try{*document=Handles.Add(new DocumentHolder("+managed+".CODEC.Encode(typed)));return NativeExports.Ok();}catch(Exception exception){return NativeExports.Fail(exception);} }");
        foreach(var node in version.Definition.Nodes.ToArray().Where(static node=>node.Transmit))
        {
            EmitView(output,prefix,managed,node.Name,node.Name,managed+"."+node.Name);
            foreach(var field in version.Definition.GetFields(node).ToArray().Where(static field=>field.Transmit&&field.ElementCount==1))
                EmitView(output,prefix,managed,node.Name+"_"+field.Name,node.Name+"__"+field.Name,ManagedFieldTypeFullName(version,field));
        }
    }
    output.AppendLine("}");return output.ToString();
}

void EmitView(StringBuilder output,string prefix,string managed,string exportSuffix,string property,string managedType)
{
    var safe=Safe(prefix+"_"+exportSuffix);
    output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \""+prefix+"_"+exportSuffix+"_get_read_view\", CallConvs = [typeof(CallConvCdecl)])]");
    output.Append("    public static PgmXtStatus ").Append(safe).Append("_read(PgmXtHandle model,").Append(managedType).AppendLine("** data,int* count)");
    output.AppendLine("    {if(!NativeExports.TrySchemaHolder(model,\""+prefix[7..]+"\",out var holder))return NativeExports.InvalidHandle();if(holder!.Model is not "+managed+".MODEL typed)return NativeExports.InvalidState(\"Model is not finalized.\");return NativeExports.ReadView(typed."+property+",(nint*)data,count);}");
    output.AppendLine("    [UnmanagedCallersOnly(EntryPoint = \""+prefix+"_"+exportSuffix+"_get_write_view\", CallConvs = [typeof(CallConvCdecl)])]");
    output.Append("    public static PgmXtStatus ").Append(safe).Append("_write(PgmXtHandle model,").Append(managedType).AppendLine("** data,int* count)");
    output.AppendLine("    {if(!NativeExports.TrySchemaHolder(model,\""+prefix[7..]+"\",out var holder))return NativeExports.InvalidHandle();if(holder!.Model is not null)return NativeExports.InvalidState(\"Model is finalized.\");var typed=("+managed+".MODEL_BUILDER)holder.Builder;return NativeExports.WriteView(typed."+property+",(nint*)data,count);}");
}

string GenerateMapping(List<VersionSchema> schemas)
{
    var output = new StringBuilder().AppendLine("# XT generated schema mapping").AppendLine().AppendLine("This file is generated from caller-provided schemas; it contains no schema source text.").AppendLine();
    foreach (var version in schemas)
    {
        output.Append("## ").Append(version.Identity).AppendLine().AppendLine().Append("C header: `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.").Append(version.Identity).AppendLine(".h`").AppendLine().AppendLine("| Node | Type | Transmit | Fields | Managed type | C type |").AppendLine("|---|---:|---:|---:|---|---|");
        foreach (var node in version.Definition.Nodes)
            output.Append('|').Append(node.Name).Append('|').Append(node.Type).Append('|').Append(node.Transmit ? 1 : 0).Append('|').Append(node.ParsedFieldCount).Append("|`ProjectGmKernel.Xt.Schema.").Append(Namespace(version.Identity)).Append('.').Append(node.Name).Append("`|`PGM_XT_").Append(node.Name).AppendLine("_t`|");
        output.AppendLine().AppendLine("| Schema field | Type | Transmit | Elements | Managed member | C member | Values | Codec |").AppendLine("|---|---|---:|---:|---|---|---|---|");
        foreach(var node in version.Definition.Nodes)foreach(var field in version.Definition.GetFields(node))
            output.Append('|').Append(node.Name).Append('.').Append(field.Name).Append('|').Append(field.Type).Append('|').Append(field.Transmit?1:0).Append('|').Append(field.ElementCount).Append("|`").Append(node.Name).Append('.').Append(field.Name).Append("`|`PGM_XT_").Append(node.Name).Append("_t.").Append(field.Name).Append(IsCppKeyword(field.Name)?"` / C++ `."+field.Name+"_":"").Append("`|").Append(XtEnumFor(node.Name, field.Name, field.Type)?.Source ?? "-").Append("|").Append(field.Transmit?"encode+decode":"not maintained").AppendLine("|");
        output.AppendLine();
    }
    return output.ToString();
}

void WriteOrCheck(string path, string content)
{
    content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
    if (check)
    {
        if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
            throw new InvalidOperationException($"Generated file is out of date: {Path.GetRelativePath(repositoryRoot, path)}");
        return;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content, new UTF8Encoding(false));
}

bool TryRefTarget(VersionSchema version, XtFieldDescriptor field, out XtNodeDescriptor target)
{
    target = field.Type == 'p' ? version.Definition.GetNode(field.NodeClass) : default;
    return target.Type != 0;
}

List<XtNodeDescriptor> RefTargets(VersionSchema version)
{
    var targets = new List<XtNodeDescriptor>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var node in version.Definition.Nodes)
        foreach (var field in version.Definition.GetFields(node))
            if (TryRefTarget(version, field, out var target) && seen.Add(target.Name))
                targets.Add(target);
    return targets;
}

string ManagedFieldElementType(VersionSchema version, XtFieldDescriptor field)
{
    if (TryRefTarget(version, field, out var target)) return target.Name + "Ref";
    return field.Type switch
    {
        'p' => "XtNodeIndex",
        'd' or 'n' or 'w' or 't' or 'q' => "long",
        'u' => "ulong",
        'f' => "double",
        'c' or 'l' => "byte",
        'v' or 'h' => "XtSchemaVector",
        'i' => "XtSchemaInterval",
        'b' => "XtSchemaBox",
        _ => throw new InvalidOperationException($"Unknown schema field type '{field.Type}'."),
    };
}

string ManagedFieldTypeFullName(VersionSchema version, XtFieldDescriptor field)
{
    if (TryRefTarget(version, field, out var target)) return "global::ProjectGmKernel.Xt.Schema." + Namespace(version.Identity) + "." + target.Name + "Ref";
    var type = ManagedFieldElementType(version, field);
    return type switch
    {
        "XtNodeIndex" => "int",
        "XtSchemaVector" or "XtSchemaInterval" or "XtSchemaBox" => "global::ProjectGmKernel.Xt." + type,
        _ => type,
    };
}

string CFieldElementType(VersionSchema version, XtFieldDescriptor field)
{
    if (TryRefTarget(version, field, out var target)) return "PGM_XT_" + target.Name + "_ref_t";
    return field.Type switch
    {
        'p' => "int32_t",
        'd' or 'n' or 'w' or 't' or 'q' => "int64_t",
        'u' => "uint64_t",
        'f' => "double",
        'c' or 'l' => "uint8_t",
        'v' or 'h' => "PGM_XT_schema_vector_t",
        'i' => "PGM_XT_schema_interval_t",
        'b' => "PGM_XT_schema_box_t",
        _ => throw new InvalidOperationException($"Unknown schema field type '{field.Type}'."),
    };
}

string DecodeValue(VersionSchema version, XtNodeDescriptor node, XtFieldDescriptor field, string source)
{
    var call = "XtSchemaFieldCodec.To_" + field.Type + "(" + source + ", \"" + version.Identity + "." + node.Name + "." + field.Name + "\")";
    return TryRefTarget(version, field, out var target) ? "new " + target.Name + "Ref { Index = " + call + " }" : call;
}

string EncodeValue(VersionSchema version, XtFieldDescriptor field, string value)
    => "XtSchemaFieldCodec.From_" + field.Type + "(" + RefIndex(version, field, value) + ")";

string PointerValidation(VersionSchema version, XtFieldDescriptor field, string value, string label)
    => field.Type == 'p' ? "XtGeneratedSchemaRuntime.ValidatePointer(" + RefIndex(version, field, value) + "," + field.NodeClass + ",nodeTypes,DESCRIPTOR.Definition,\"" + label + "\"); " : string.Empty;

string RefIndex(VersionSchema version, XtFieldDescriptor field, string value)
    => TryRefTarget(version, field, out _) ? value + ".Index" : value;

(string Source, (string Name, long Value)[] Values)? XtEnumFor(string node, string field, char type)
{
    if (!xtEnumSources.TryGetValue(node + "." + field, out var entry) && !xtEnumSources.TryGetValue(field, out entry))
        return null;
    if (entry.Type != type || !xtEnumValues.TryGetValue(entry.Source, out var values))
        return null;
    return (entry.Source, values);
}

string EnumValueLiteral(long value, bool charCode, bool cSharp)
    => !charCode ? value.ToString() : cSharp ? $"(byte)'{(char)value}'" : $"'{(char)value}'";

static StringBuilder Header(string description) => new StringBuilder().AppendLine("// <auto-generated/>").Append("// ").AppendLine(description).AppendLine("#nullable enable").AppendLine();
static string Namespace(string identity) => identity;
static string Safe(string value) => value.Replace('-', '_');
static string Bool(bool value) => value ? "true" : "false";
static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
static string CsName(string name) => name is "class" or "event" or "operator" or "params" or "base" or "ref" or "out" or "in" or "internal" or "public" or "private" or "protected" or "readonly" or "fixed" or "const" or "string" or "object" or "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "sbyte" or "byte" or "char" or "double" or "float" or "decimal" or "bool" or "nint" or "nuint" or "new" or "default" or "return" or "this" or "namespace" or "struct" or "enum" or "interface" or "delegate" or "void" or "null" or "true" or "false" or "is" or "as" or "checked" or "unchecked" or "stackalloc" or "sizeof" or "typeof" or "lock" or "using" or "static" or "virtual" or "override" or "abstract" or "sealed" or "partial" or "record" or "required" or "file" or "scoped" ? "@" + name : name;
static bool IsCppKeyword(string name) => name is "alignas" or "alignof" or "and" or "and_eq" or "asm" or "atomic_cancel" or "atomic_commit" or "atomic_noexcept" or "auto" or "bitand" or "bitor" or "bool" or "break" or "case" or "catch" or "char" or "char8_t" or "char16_t" or "char32_t" or "class" or "compl" or "concept" or "const" or "consteval" or "constexpr" or "constinit" or "const_cast" or "continue" or "co_await" or "co_return" or "co_yield" or "decltype" or "default" or "delete" or "do" or "double" or "dynamic_cast" or "else" or "enum" or "explicit" or "export" or "extern" or "false" or "float" or "for" or "friend" or "goto" or "if" or "inline" or "int" or "long" or "mutable" or "namespace" or "new" or "noexcept" or "not" or "not_eq" or "nullptr" or "operator" or "or" or "or_eq" or "private" or "protected" or "public" or "register" or "reinterpret_cast" or "requires" or "return" or "short" or "signed" or "sizeof" or "static" or "static_assert" or "static_cast" or "struct" or "switch" or "template" or "this" or "thread_local" or "throw" or "true" or "try" or "typedef" or "typeid" or "typename" or "union" or "unsigned" or "using" or "virtual" or "void" or "volatile" or "wchar_t" or "while" or "xor" or "xor_eq";
static string? ReadOption(string[] values, string option) { for (var i = 0; i < values.Length; i++) if (values[i] == option) return i + 1 < values.Length ? values[i + 1] : throw new ArgumentException(option + " requires a value."); return null; }
static string GetScriptPath([CallerFilePath] string path = "") => path;
readonly record struct VersionSchema(string Identity, XtSchemaDefinition Definition, int ProducerVersion, bool Alias);
