using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Xt.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PgmXtContextOptions { public uint StructSize; public uint Version; public byte* SchemaDirectoryUtf8; public uint Flags; public int MaximumCachedSchemas; }
[StructLayout(LayoutKind.Sequential)] public struct PgmXtBuffer { public nint Data; public nuint Size; }

internal sealed class ContextHolder(XtSchemaCatalog catalog) { internal XtSchemaCatalog Catalog { get; } = catalog; }
internal sealed class DocumentHolder(XtDocument document) { internal XtDocument Document { get; } = document; }
internal sealed class SchemaModelHolder
{
    internal SchemaModelHolder(string identity, object value, bool finalized=false) { Identity=identity;if(finalized){Builder=null!;Model=value;}else Builder=value; }
    internal string Identity { get; }
    internal object Builder { get; }
    internal object? Model { get; set; }
}

public static unsafe class NativeExports
{
    private const uint AbiVersion=1;

    [UnmanagedCallersOnly(EntryPoint="PGM_XT_CONTEXT_create",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextCreate(PgmXtContextOptions* options,PgmXtHandle* context)
    {
        if(options is null||context is null||options->StructSize<(uint)sizeof(PgmXtContextOptions)||options->Version!=AbiVersion)return InvalidArgument("Invalid XT context options.");
        try
        {
            var path=options->SchemaDirectoryUtf8 is null?null:Marshal.PtrToStringUTF8((nint)options->SchemaDirectoryUtf8);
            var catalog=string.IsNullOrWhiteSpace(path)?XtSchemaCatalog.OpenBuiltIn():XtSchemaCatalog.OpenDirectory(path);
            *context=Handles.Add(new ContextHolder(catalog));return Ok();
        }
        catch(Exception exception){return Fail(exception);}
    }
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_CONTEXT_load_all",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextLoadAll(PgmXtHandle context){if(!Handles.TryGet(context,out ContextHolder? holder))return InvalidHandle();try{holder!.Catalog.LoadAll();return Ok();}catch(Exception exception){return Fail(exception);}}
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_CONTEXT_delete",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextDelete(PgmXtHandle context)=>Handles.Delete<ContextHolder>(context)?Ok():InvalidHandle();

    [UnmanagedCallersOnly(EntryPoint="PGM_XT_DOCUMENT_read",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentRead(PgmXtHandle context,byte* source,nuint size,PgmXtHandle* document)
    {
        if(!Handles.TryGet(context,out ContextHolder? holder))return InvalidHandle();if(source is null||document is null||size>int.MaxValue)return InvalidArgument("Invalid XT input buffer.");
        try{*document=Handles.Add(new DocumentHolder(XtCodec.Read(holder!.Catalog,new ReadOnlySpan<byte>(source,(int)size))));return Ok();}catch(Exception exception){return Fail(exception);}
    }
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_DOCUMENT_write",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentWrite(PgmXtHandle context,PgmXtHandle document,byte* targetIdentity,PgmXtBuffer* output)
    {
        if(!Handles.TryGet(context,out ContextHolder? contextHolder)||!Handles.TryGet(document,out DocumentHolder? documentHolder))return InvalidHandle();if(output is null)return InvalidArgument("Output buffer is null.");
        try{var identity=targetIdentity is null?null:Marshal.PtrToStringUTF8((nint)targetIdentity);var bytes=XtCodec.Write(contextHolder!.Catalog,documentHolder!.Document,new XtWriteOptions(identity));output->Data=Allocate(bytes);output->Size=(nuint)bytes.Length;return Ok();}catch(Exception exception){output->Data=0;output->Size=0;return Fail(exception);}
    }
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_DOCUMENT_delete",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentDelete(PgmXtHandle document)=>Handles.Delete<DocumentHolder>(document)?Ok():InvalidHandle();
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_BUFFER_free",CallConvs=[typeof(CallConvCdecl)])]
    public static void BufferFree(nint data){if(data!=0)NativeMemory.Free((void*)data);}
    [UnmanagedCallersOnly(EntryPoint="PGM_XT_ERROR_get",CallConvs=[typeof(CallConvCdecl)])]
    public static PgmXtStatus ErrorGet(PgmXtBuffer* output){if(output is null)return(PgmXtStatus)XtErrorCode.InvalidArgument;var bytes=System.Text.Encoding.UTF8.GetBytes(ErrorState.Message??string.Empty);output->Data=Allocate(bytes);output->Size=(nuint)bytes.Length;return Ok();}

    internal static bool TrySchemaHolder(PgmXtHandle handle,string identity,out SchemaModelHolder? holder)=>Handles.TryGet(handle,out holder)&&string.Equals(holder!.Identity,identity,StringComparison.Ordinal);
    internal static PgmXtStatus DeleteSchemaHolder(PgmXtHandle handle,string identity)=>TrySchemaHolder(handle,identity,out _)&&Handles.Delete<SchemaModelHolder>(handle)?Ok():InvalidHandle();
    internal static PgmXtStatus ReadView<T>(ReadOnlySpan<T> span,nint* data,int* count)where T:unmanaged{if(data is null||count is null)return InvalidArgument("View output is null.");*count=span.Length;*data=span.IsEmpty?0:(nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));return Ok();}
    internal static PgmXtStatus WriteView<T>(Span<T> span,nint* data,int* count)where T:unmanaged{if(data is null||count is null)return InvalidArgument("View output is null.");*count=span.Length;*data=span.IsEmpty?0:(nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));return Ok();}
    internal static PgmXtStatus Ok(){ErrorState.Message=null;return 0;}
    internal static PgmXtStatus InvalidHandle()=>Fail(XtErrorCode.InvalidHandle,"Invalid or deleted XT handle.");
    internal static PgmXtStatus InvalidArgument(string message)=>Fail(XtErrorCode.InvalidArgument,message);
    internal static PgmXtStatus InvalidState(string message)=>Fail(XtErrorCode.InvalidState,message);
    internal static PgmXtStatus Fail(Exception exception)=>Fail(exception is XtFormatException format?format.Code:XtErrorCode.InvalidData,exception.Message);
    internal static PgmXtStatus Fail(XtErrorCode code,string message){ErrorState.Message=message;return(PgmXtStatus)code;}
    private static nint Allocate(ReadOnlySpan<byte> bytes){if(bytes.IsEmpty)return 0;var pointer=NativeMemory.Alloc((nuint)bytes.Length);bytes.CopyTo(new Span<byte>(pointer,bytes.Length));return(nint)pointer;}
}

internal static class ErrorState { [ThreadStatic] internal static string? Message; }
internal static class Handles
{
    private sealed class Entry { internal object? Value; internal uint Generation=1; }
    private static readonly List<Entry> Entries=[];private static readonly object Sync=new();
    internal static ulong Add(object value){lock(Sync){for(var slot=0;slot<Entries.Count;slot++)if(Entries[slot].Value is null){Entries[slot].Value=value;return Token(slot,Entries[slot].Generation);}Entries.Add(new Entry{Value=value});return Token(Entries.Count-1,1);}}
    internal static bool TryGet<T>(ulong token,out T? value)where T:class{Decode(token,out var slot,out var generation);lock(Sync){if((uint)slot<(uint)Entries.Count&&Entries[slot].Generation==generation&&Entries[slot].Value is T typed){value=typed;return true;}}value=null;return false;}
    internal static bool Delete<T>(ulong token)where T:class{Decode(token,out var slot,out var generation);lock(Sync){if((uint)slot>=(uint)Entries.Count||Entries[slot].Generation!=generation||Entries[slot].Value is not T)return false;Entries[slot].Value=null;Entries[slot].Generation++;if(Entries[slot].Generation==0)Entries[slot].Generation=1;return true;}}
    private static ulong Token(int slot,uint generation)=>((ulong)generation<<32)|(uint)(slot+1);
    private static void Decode(ulong token,out int slot,out uint generation){slot=unchecked((int)(uint)token)-1;generation=(uint)(token>>32);}
}
