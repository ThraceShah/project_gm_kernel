#!/usr/bin/env dotnet
#:property AllowUnsafeBlocks=true
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Xt;
using Schema37102 = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));var libraryPath=Path.Combine(root,"bin","xt-native","linux-x64","ProjectGmKernel.Xt.Native.so");
if(!File.Exists(libraryPath))throw new FileNotFoundException("Publish the linux-x64 XT native library first.",libraryPath);
var fixture=Path.Combine(root,"tests","ParasolidXtCorpus","Fixtures","body.solid.block.typical","model.x_t");var sourceBytes=File.ReadAllBytes(fixture);var library=NativeLibrary.Load(libraryPath);
unsafe
{
    var contextCreate=(delegate* unmanaged[Cdecl]<ContextOptions*,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_CONTEXT_create");var contextDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_CONTEXT_delete");
    var documentRead=(delegate* unmanaged[Cdecl]<ulong,byte*,nuint,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_read");var documentWrite=(delegate* unmanaged[Cdecl]<ulong,ulong,byte*,Buffer*,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_write");var documentDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_delete");var bufferFree=(delegate* unmanaged[Cdecl]<nint,void>)NativeLibrary.GetExport(library,"PGM_XT_BUFFER_free");
    var toModel=(delegate* unmanaged[Cdecl]<ulong,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_DOCUMENT_to_SCH_3701097_37102_MODEL");var toDocument=(delegate* unmanaged[Cdecl]<ulong,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_to_DOCUMENT");
    var modelCreate=(delegate* unmanaged[Cdecl]<Schema37102.COUNTS*,ulong*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_create");var modelFinalize=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_finalize");var modelDelete=(delegate* unmanaged[Cdecl]<ulong,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_delete");
    var setUserFieldSize=(delegate* unmanaged[Cdecl]<ulong,int,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_set_user_field_size");var askUserFieldSize=(delegate* unmanaged[Cdecl]<ulong,int*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_ask_user_field_size");
    var userFieldsRead=(delegate* unmanaged[Cdecl]<ulong,int**,int*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_user_fields_get_read_view");var userFieldsWrite=(delegate* unmanaged[Cdecl]<ulong,int**,int*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_MODEL_user_fields_get_write_view");
    var bodyRead=(delegate* unmanaged[Cdecl]<ulong,Schema37102.BODY**,int*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_BODY_get_read_view");var bodyWrite=(delegate* unmanaged[Cdecl]<ulong,Schema37102.BODY**,int*,int>)NativeLibrary.GetExport(library,"PGM_XT_SCH_3701097_37102_BODY_get_write_view");
    var options=new ContextOptions{StructSize=(uint)sizeof(ContextOptions),Version=1};ulong context;Check(contextCreate(&options,&context),"context create without external schema");
    fixed(byte* source=sourceBytes)
    {
        ulong document;Check(documentRead(context,source,(nuint)sourceBytes.Length,&document),"document read");ulong model;Check(toModel(document,&model),"document to generated model");Schema37102.BODY* bodies;int bodyCount;Check(bodyRead(model,&bodies,&bodyCount),"BODY read view");if(bodyCount!=1||bodies is null)throw new InvalidOperationException("BODY typed view is invalid.");
        ulong rebuilt;Check(toDocument(model,&rebuilt),"generated model to document");var output=new Buffer();Check(documentWrite(context,rebuilt,null,&output),"document write");if(output.Data==0||output.Size==0)throw new InvalidOperationException("document write returned empty output");bufferFree(output.Data);Check(documentDelete(rebuilt),"rebuilt document delete");Check(modelDelete(model),"model delete");Check(documentDelete(document),"source document delete");
    }
    var counts=new Schema37102.COUNTS{BODY=1};ulong builder;Check(modelCreate(&counts,&builder),"model create");Schema37102.BODY* writable;int writableCount;Check(bodyWrite(builder,&writable,&writableCount),"BODY write view");if(writableCount!=1||writable is null)throw new InvalidOperationException("BODY write view is invalid.");if(modelFinalize(builder)==0)throw new InvalidOperationException("Invalid zero-filled BODY unexpectedly finalized.");Check(modelDelete(builder),"builder delete");
    // User fields: set the per-entity width before finalize, write one entity's
    // range plus the flat payload, finalize, then read everything back and
    // round-trip the serialized document through a fresh model.
    var userFieldCounts=new Schema37102.COUNTS{BODY=1,_xt_user_fields=2};ulong ufBuilder;Check(modelCreate(&userFieldCounts,&ufBuilder),"user-field model create");
    Check(setUserFieldSize(ufBuilder,2),"user-field size set");
    Schema37102.BODY* ufBodies;int ufBodyCount;Check(bodyWrite(ufBuilder,&ufBodies,&ufBodyCount),"user-field BODY write view");ufBodies[0]._xt_index=1;ufBodies[0]._xt_order=0;ufBodies[0]._xt_user_fields=new XtRange{Offset=0,Count=2};
    int* ufPool;int ufPoolCount;Check(userFieldsWrite(ufBuilder,&ufPool,&ufPoolCount),"user-field write view");if(ufPool is null||ufPoolCount!=2)throw new InvalidOperationException("user-field write view is invalid.");ufPool[0]=7;ufPool[1]=9;
    int ufBuilderSize;Check(askUserFieldSize(ufBuilder,&ufBuilderSize),"builder user-field size ask");if(ufBuilderSize!=2)throw new InvalidOperationException($"builder user-field size is {ufBuilderSize}, expected 2.");
    Check(modelFinalize(ufBuilder),"user-field model finalize");
    int ufModelSize;Check(askUserFieldSize(ufBuilder,&ufModelSize),"finalized user-field size ask");if(ufModelSize!=2)throw new InvalidOperationException($"finalized user-field size is {ufModelSize}, expected 2.");
    if(setUserFieldSize(ufBuilder,3)==0)throw new InvalidOperationException("finalized model unexpectedly accepted a user-field size change.");
    int* ufRead;int ufReadCount;Check(userFieldsRead(ufBuilder,&ufRead,&ufReadCount),"user-field read view");if(ufReadCount!=2||ufRead[0]!=7||ufRead[1]!=9)throw new InvalidOperationException("user-field read view returned unexpected data.");
    Schema37102.BODY* ufReadBodies;int ufReadBodyCount;Check(bodyRead(ufBuilder,&ufReadBodies,&ufReadBodyCount),"user-field BODY read view");if(ufReadBodyCount!=1||ufReadBodies[0]._xt_user_fields.Offset!=0||ufReadBodies[0]._xt_user_fields.Count!=2)throw new InvalidOperationException("user-field BODY range is invalid.");
    ulong ufDocument;Check(toDocument(ufBuilder,&ufDocument),"user-field model to document");var ufOutput=new Buffer();Check(documentWrite(context,ufDocument,null,&ufOutput),"user-field document write");
    ulong ufReread;Check(documentRead(context,(byte*)ufOutput.Data,ufOutput.Size,&ufReread),"user-field document reread");
    ulong ufModel2;Check(toModel(ufReread,&ufModel2),"user-field document to model");
    int ufDecodedSize;Check(askUserFieldSize(ufModel2,&ufDecodedSize),"decoded user-field size ask");if(ufDecodedSize!=2)throw new InvalidOperationException($"decoded user-field size is {ufDecodedSize}, expected 2.");
    int* ufDecoded;int ufDecodedCount;Check(userFieldsRead(ufModel2,&ufDecoded,&ufDecodedCount),"decoded user-field read view");if(ufDecodedCount!=2||ufDecoded[0]!=7||ufDecoded[1]!=9)throw new InvalidOperationException("decoded user-field data differs.");
    Check(modelDelete(ufModel2),"decoded user-field model delete");Check(documentDelete(ufReread),"user-field reread delete");bufferFree(ufOutput.Data);Check(documentDelete(ufDocument),"user-field document delete");Check(modelDelete(ufBuilder),"user-field model delete");
    Check(contextDelete(context),"context delete");
}
NativeLibrary.Free(library);VerifyHeader(root);Console.WriteLine("XT schema-specific NativeAOT C ABI smoke passed.");

static void VerifyHeader(string root)
{
    var work=Path.Combine(root,"bin","xt-native-abi-smoke");Directory.CreateDirectory(work);var source=Path.Combine(work,"header-layout.c");var executable=Path.Combine(work,"header-layout");var header=Path.Combine(root,"src","ProjectGmKernel.Xt.Native","include","ProjectGmKernel.Xt.SCH_3701097_37102.h").Replace("\\","/",StringComparison.Ordinal);
    File.WriteAllText(source,$$"""
#include "{{header}}"
_Static_assert(sizeof(PGM_XT_INTERSECTION_t)=={{Unsafe.SizeOf<Schema37102.INTERSECTION>()}},"INTERSECTION layout mismatch");
_Static_assert(sizeof(PGM_XT_BLENDED_EDGE_t)=={{Unsafe.SizeOf<Schema37102.BLENDED_EDGE>()}},"BLENDED_EDGE layout mismatch");
_Static_assert(sizeof(PGM_XT_NURBS_SURF_t)=={{Unsafe.SizeOf<Schema37102.NURBS_SURF>()}},"NURBS_SURF layout mismatch");
_Static_assert(sizeof(PGM_XT_SURFACE_DATA_t)=={{Unsafe.SizeOf<Schema37102.SURFACE_DATA>()}},"SURFACE_DATA layout mismatch");
_Static_assert(_Generic(&PGM_XT_MODEL_create, PGM_XT_status_t (*)(const PGM_XT_COUNTS_t *, PGM_XT_model_t *): 1, default: 0),"short model API mismatch");
_Static_assert(_Generic(&PGM_XT_MODEL_set_user_field_size, PGM_XT_status_t (*)(PGM_XT_model_t, int32_t): 1, default: 0),"user-field size setter mismatch");
_Static_assert(_Generic(&PGM_XT_MODEL_ask_user_field_size, PGM_XT_status_t (*)(PGM_XT_model_t, int32_t *): 1, default: 0),"user-field size asker mismatch");
_Static_assert(_Generic(&PGM_XT_MODEL_user_fields_get_read_view, PGM_XT_status_t (*)(PGM_XT_model_t, const int32_t **, int32_t *): 1, default: 0),"user-field read view mismatch");
_Static_assert(_Generic(&PGM_XT_MODEL_user_fields_get_write_view, PGM_XT_status_t (*)(PGM_XT_model_t, int32_t **, int32_t *): 1, default: 0),"user-field write view mismatch");
int main(void){return 0;}
""");Run("cc",["-std=c11","-Wall","-Werror",source,"-o",executable]);Run(executable,[]);
}
static void Check(int status,string operation){if(status!=0)throw new InvalidOperationException($"{operation} failed with status {status}");}
static void Run(string fileName,string[] arguments){var start=new ProcessStartInfo(fileName){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)??throw new InvalidOperationException($"Cannot start {fileName}.");var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException($"{fileName} failed: {stdout}{stderr}");}
static string GetScriptPath([CallerFilePath]string path="")=>path;
[StructLayout(LayoutKind.Sequential)]unsafe struct ContextOptions{public uint StructSize,Version;public byte* SchemaDirectory;public uint Flags;public int MaximumCachedSchemas;}
[StructLayout(LayoutKind.Sequential)]struct Buffer{public nint Data;public nuint Size;}
