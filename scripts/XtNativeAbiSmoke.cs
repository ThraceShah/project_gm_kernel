#!/usr/bin/env dotnet
#:property AllowUnsafeBlocks=true
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ProjectGmKernel.Xt;

var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));var libraryPath=Path.Combine(root,"bin","xt-native","linux-x64","ProjectGmKernel.Xt.Native.so");
if(!File.Exists(libraryPath))throw new FileNotFoundException("Publish the linux-x64 XT native library first.",libraryPath);
var work=Path.Combine(root,"bin","xt-native-abi-smoke");var schemas=Path.Combine(work,"schema");Directory.CreateDirectory(schemas);File.WriteAllText(Path.Combine(schemas,"sch_test.sch_txt"),Data.Schema);
var library=NativeLibrary.Load(libraryPath);
unsafe
{
    var contextCreate=(delegate* unmanaged[Cdecl]<ContextOptions*,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_CONTEXT_create");var contextLoad=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_CONTEXT_load_all");var contextDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_CONTEXT_delete");
    var documentRead=(delegate* unmanaged[Cdecl]<ulong,byte*,nuint,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_read");var documentWrite=(delegate* unmanaged[Cdecl]<ulong,ulong,byte*,Buffer*,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_write");var documentDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_delete");var bufferFree=(delegate* unmanaged[Cdecl]<nint,void>)NativeLibrary.GetExport(library,"PGM_XT_BUFFER_free");
    var brepCreate=(delegate* unmanaged[Cdecl]<BrepCounts*,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_BREP_create");var brepCounts=(delegate* unmanaged[Cdecl]<ulong,BrepCounts*,int>)NativeLibrary.GetExport(library,"PGM_XT_BREP_get_counts");var brepFinalize=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_BREP_finalize");var brepDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_BREP_delete");
    var path=Encoding.UTF8.GetBytes(schemas+'\0');fixed(byte* pathPointer=path)
    {
        var options=new ContextOptions{StructSize=(uint)sizeof(ContextOptions),Version=1,SchemaDirectory=pathPointer};ulong context;Check(contextCreate(&options,&context),"context create");Check(contextLoad(context),"context load all");
        var version=": TRANSMIT FILE created by test modeller version 3000000";var identity="SCH_3000000_30000";var text=$"T{version.Length} {version}{identity.Length} {identity}0 12 1 42 0 1 0 ";var bytes=Encoding.UTF8.GetBytes(text);fixed(byte* source=bytes)
        {
            ulong document;Check(documentRead(context,source,(nuint)bytes.Length,&document),"document read");var output=new Buffer();Check(documentWrite(context,document,null,&output),"document write");if(output.Data==0||output.Size==0)throw new InvalidOperationException("document write returned an empty buffer");bufferFree(output.Data);Check(documentDelete(document),"document delete");if(documentDelete(document)==0)throw new InvalidOperationException("duplicate document delete unexpectedly succeeded");
        }
        var counts=default(BrepCounts);ulong brep;Check(brepCreate(&counts,&brep),"brep create");var returned=default(BrepCounts);Check(brepCounts(brep,&returned),"brep get counts");Check(brepFinalize(brep),"brep finalize");if(brepFinalize(brep)==0)throw new InvalidOperationException("duplicate finalize unexpectedly succeeded");Check(brepDelete(brep),"brep delete");Check(contextDelete(context),"context delete");
    }
}
NativeLibrary.Free(library);VerifyHeaderLayout(root,work);Console.WriteLine("XT NativeAOT C ABI smoke passed.");return;
static void Check(int status,string operation){if(status!=0)throw new InvalidOperationException($"{operation} failed with status {status}");}
static string GetScriptPath([CallerFilePath]string path="")=>path;

static void VerifyHeaderLayout(string root,string work)
{
    var header=Path.Combine(root,"src","ProjectGmKernel.Xt.Native","include","ProjectGmKernel.Xt.h");
    var source=Path.Combine(work,"header-layout.c");var executable=Path.Combine(work,"header-layout");
    var assertions=new (string C,int ManagedSize)[]{
        ("PGM_XT_part_row_t",Unsafe.SizeOf<XtPartRow>()),("PGM_XT_body_row_t",Unsafe.SizeOf<XtBodyRow>()),("PGM_XT_region_row_t",Unsafe.SizeOf<XtRegionRow>()),
        ("PGM_XT_shell_row_t",Unsafe.SizeOf<XtShellRow>()),("PGM_XT_face_row_t",Unsafe.SizeOf<XtFaceRow>()),("PGM_XT_loop_row_t",Unsafe.SizeOf<XtLoopRow>()),
        ("PGM_XT_fin_row_t",Unsafe.SizeOf<XtFinRow>()),("PGM_XT_edge_row_t",Unsafe.SizeOf<XtEdgeRow>()),("PGM_XT_vertex_row_t",Unsafe.SizeOf<XtVertexRow>()),
        ("PGM_XT_point_row_t",Unsafe.SizeOf<XtPointRow>()),("PGM_XT_geometry_row_t",Unsafe.SizeOf<XtGeometryRow>()),("PGM_XT_transform_row_t",Unsafe.SizeOf<XtTransformRow>()),
        ("PGM_XT_frame_row_t",Unsafe.SizeOf<XtFrameRow>()),("PGM_XT_assembly_row_t",Unsafe.SizeOf<XtAssemblyRow>()),("PGM_XT_instance_row_t",Unsafe.SizeOf<XtInstanceRow>()),
        ("PGM_XT_attribute_definition_row_t",Unsafe.SizeOf<XtAttributeDefinitionRow>()),("PGM_XT_attribute_field_definition_row_t",Unsafe.SizeOf<XtAttributeFieldDefinitionRow>()),
        ("PGM_XT_attribute_row_t",Unsafe.SizeOf<XtAttributeRow>()),("PGM_XT_attribute_value_row_t",Unsafe.SizeOf<XtAttributeValueRow>()),
        ("PGM_XT_user_field_row_t",Unsafe.SizeOf<XtUserFieldRow>()),("PGM_XT_mesh_row_t",Unsafe.SizeOf<XtMeshRow>()),("PGM_XT_lattice_row_t",Unsafe.SizeOf<XtLatticeRow>()),
        ("PGM_XT_connection_row_t",Unsafe.SizeOf<XtConnectionRow>()),("PGM_XT_hvector_row_t",Unsafe.SizeOf<XtHVectorRow>()),("PGM_XT_chart_row_t",Unsafe.SizeOf<XtChartRow>()),
        ("PGM_XT_limit_row_t",Unsafe.SizeOf<XtLimitRow>()),("PGM_XT_intersection_data_row_t",Unsafe.SizeOf<XtIntersectionDataRow>()),
        ("PGM_XT_geometric_owner_row_t",Unsafe.SizeOf<XtGeometricOwnerRow>()),("PGM_XT_string_row_t",Unsafe.SizeOf<XtStringRow>()),
        ("PGM_XT_scalar_row_t",Unsafe.SizeOf<XtScalarRow>()),("PGM_XT_transmit_order_row_t",Unsafe.SizeOf<XtTransmitOrderRow>()),
    };
    var text=new StringBuilder("#include \"").Append(header.Replace("\\","/",StringComparison.Ordinal)).AppendLine("\"");
    foreach(var assertion in assertions)text.Append("_Static_assert(sizeof(").Append(assertion.C).Append(")==").Append(assertion.ManagedSize).Append(",\"").Append(assertion.C).AppendLine(" layout mismatch\");");
    text.AppendLine("int main(void){return 0;}");File.WriteAllText(source,text.ToString());
    Run("cc",["-std=c11","-Wall","-Werror",source,"-o",executable]);Run(executable,[]);
}

static void Run(string fileName,string[] arguments)
{
    var start=new ProcessStartInfo(fileName){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);
    using var process=Process.Start(start)??throw new InvalidOperationException($"Could not start {fileName}.");var output=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException($"{fileName} failed ({process.ExitCode}): {output}{error}");
}

[StructLayout(LayoutKind.Sequential)]unsafe struct ContextOptions{public uint StructSize,Version;public byte* SchemaDirectory;public uint Flags;public int MaximumCachedSchemas;}
[StructLayout(LayoutKind.Sequential)]struct Buffer{public nint Data;public nuint Size;}
[StructLayout(LayoutKind.Sequential)]struct BrepCounts{public int Parts,Bodies,Regions,Shells,Faces,Loops,Fins,Edges,Vertices,Points,Geometries,Transforms,Frames,Assemblies,Instances,AttributeDefinitions,AttributeFieldDefinitions,Attributes,AttributeValues,UserFields,Meshes,Lattices,Connections,ControlPoints,Scalars,Charts,Limits,IntersectionData,HVectors,GeometricOwners,TransmitOrder,Strings,PayloadBytes;}
static class Data { internal const string Schema="""
T
1
: SCHEMA FILE created by modeller version 3000000/30000;
2 2 2 0
1 NULLP; Null; 1 0 0
12 BODY; Body; 1 2 0
value; d; 1 0 0
owner; p; 1 12 0
** end of schema SCH_3000000_30000
"""; }
