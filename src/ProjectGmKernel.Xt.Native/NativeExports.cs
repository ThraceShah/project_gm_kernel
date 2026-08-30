using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Xt.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PgmXtContextOptions
{
    public uint StructSize;
    public uint Version;
    public byte* SchemaDirectoryUtf8;
    public uint Flags;
    public PgmXtElementCount MaximumCachedSchemas;
}

[StructLayout(LayoutKind.Sequential)]
public struct PgmXtBuffer { public nint Data; public nuint Size; }

public enum PgmXtTableKind : PgmXtTableKindValue
{
    Parts, Bodies, Regions, Shells, Faces, Loops, Fins, Edges, Vertices, Points,
    Geometries, Transforms, Frames, Assemblies, Instances, AttributeDefinitions,
    AttributeFieldDefinitions, Attributes, AttributeValues, UserFields, Meshes,
    Lattices, Connections, ControlPoints, Scalars, Charts, Limits, IntersectionData, HVectors, GeometricOwners, TransmitOrder, Strings, Payload,
}

internal sealed class ContextHolder(XtSchemaCatalog catalog) { internal XtSchemaCatalog Catalog { get; } = catalog; }
internal sealed class DocumentHolder(XtDocument document) { internal XtDocument Document { get; } = document; }
internal sealed class BrepHolder
{
    internal BrepHolder(XtBrepBuilder builder,XtBrepCounts counts) { Builder = builder;Counts=counts; }
    internal BrepHolder(XtBrepModel model) { Model = model;Counts=model.Counts; }
    internal XtBrepBuilder? Builder { get; }
    internal XtBrepModel? Model { get; set; }
    internal XtBrepCounts Counts { get; }
}

public static unsafe class NativeExports
{
    private const uint AbiVersion = 1;

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_CONTEXT_create", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextCreate(PgmXtContextOptions* options, PgmXtHandle* context)
    {
        if (options is null || context is null || options->StructSize < (uint)sizeof(PgmXtContextOptions) || options->Version != AbiVersion || options->SchemaDirectoryUtf8 is null)
            return Fail(XtErrorCode.InvalidArgument, "Invalid XT context options.");
        try
        {
            var path = Marshal.PtrToStringUTF8((nint)options->SchemaDirectoryUtf8);
            if (string.IsNullOrWhiteSpace(path)) return Fail(XtErrorCode.InvalidArgument, "Schema directory is empty.");
            *context = Handles.Add(new ContextHolder(XtSchemaCatalog.OpenDirectory(path)));
            return Ok();
        }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_CONTEXT_load_all", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextLoadAll(PgmXtHandle context)
    {
        if (!Handles.TryGet(context, out ContextHolder? holder)) return InvalidHandle();
        try { holder!.Catalog.LoadAll(); return Ok(); } catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_CONTEXT_delete", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus ContextDelete(PgmXtHandle context) => Handles.Delete<ContextHolder>(context) ? Ok() : InvalidHandle();

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_DOCUMENT_read", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentRead(PgmXtHandle context, byte* source, nuint size, PgmXtHandle* document)
    {
        if (!Handles.TryGet(context, out ContextHolder? holder)) return InvalidHandle();
        if (source is null || document is null || size > int.MaxValue) return Fail(XtErrorCode.InvalidArgument, "Invalid XT input buffer.");
        try
        {
            *document = Handles.Add(new DocumentHolder(XtCodec.Read(holder!.Catalog, new ReadOnlySpan<byte>(source, (int)size))));
            return Ok();
        }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_DOCUMENT_write", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentWrite(PgmXtHandle context, PgmXtHandle document, byte* targetSchemaIdentityUtf8, PgmXtBuffer* output)
    {
        if (!Handles.TryGet(context, out ContextHolder? contextHolder) || !Handles.TryGet(document, out DocumentHolder? documentHolder)) return InvalidHandle();
        if (output is null) return Fail(XtErrorCode.InvalidArgument, "Output buffer is null.");
        try
        {
            var identity = targetSchemaIdentityUtf8 is null ? null : Marshal.PtrToStringUTF8((nint)targetSchemaIdentityUtf8);
            var bytes = XtCodec.Write(contextHolder!.Catalog, documentHolder!.Document, new XtWriteOptions(identity));
            output->Data = Allocate(bytes);
            output->Size = (nuint)bytes.Length;
            return Ok();
        }
        catch (Exception exception) { output->Data = 0; output->Size = 0; return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_DOCUMENT_delete", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentDelete(PgmXtHandle document) => Handles.Delete<DocumentHolder>(document) ? Ok() : InvalidHandle();

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_DOCUMENT_to_brep", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus DocumentToBrep(PgmXtHandle document, PgmXtHandle* brep)
    {
        if (!Handles.TryGet(document, out DocumentHolder? holder)) return InvalidHandle();
        if (brep is null) return Fail(XtErrorCode.InvalidArgument, "B-rep output handle is null.");
        try { *brep = Handles.Add(new BrepHolder(XtBrepConverter.Decode(holder!.Document))); return Ok(); }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_to_document", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepToDocument(PgmXtHandle context, PgmXtHandle brep, byte* targetSchemaIdentityUtf8, PgmXtHandle* document)
    {
        if (!Handles.TryGet(context, out ContextHolder? contextHolder) || !Handles.TryGet(brep, out BrepHolder? brepHolder)) return InvalidHandle();
        if (targetSchemaIdentityUtf8 is null || document is null || brepHolder!.Model is null) return Fail(XtErrorCode.InvalidState, "Finalized B-rep, target schema, and output handle are required.");
        try
        {
            var identity = Marshal.PtrToStringUTF8((nint)targetSchemaIdentityUtf8)!;
            *document = Handles.Add(new DocumentHolder(XtBrepConverter.Encode(contextHolder!.Catalog, brepHolder.Model, identity)));
            return Ok();
        }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_create", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepCreate(XtBrepCounts* counts, PgmXtHandle* brep)
    {
        if (counts is null || brep is null) return Fail(XtErrorCode.InvalidArgument, "B-rep counts or output handle is null.");
        try { *brep = Handles.Add(new BrepHolder(new XtBrepBuilder(*counts),*counts)); return Ok(); }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_finalize", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepFinalize(PgmXtHandle brep)
    {
        if (!Handles.TryGet(brep, out BrepHolder? holder)) return InvalidHandle();
        if (holder!.Model is not null) return Fail(XtErrorCode.InvalidState, "B-rep is already finalized.");
        try { holder.Model = holder.Builder!.FinalizeModel(); return Ok(); } catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_validate", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepValidate(PgmXtHandle brep)
    {
        if (!Handles.TryGet(brep, out BrepHolder? holder)) return InvalidHandle();
        if (holder!.Model is null) return Fail(XtErrorCode.InvalidState, "B-rep is not finalized.");
        try { XtBrepValidator.Validate(holder.Model); return Ok(); } catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_get_counts", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepGetCounts(PgmXtHandle brep,XtBrepCounts* counts)
    {
        if(!Handles.TryGet(brep,out BrepHolder? holder))return InvalidHandle();if(counts is null)return Fail(XtErrorCode.InvalidArgument,"B-rep counts output is null.");*counts=holder!.Counts;return Ok();
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_get_table_view", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepGetTableView(PgmXtHandle brep, PgmXtTableKindValue tableKind, nint* data, PgmXtElementCount* count, PgmXtElementSize* stride, byte* readOnly)
    {
        if (!Handles.TryGet(brep, out BrepHolder? holder)) return InvalidHandle();
        if (data is null || count is null || stride is null || readOnly is null) return Fail(XtErrorCode.InvalidArgument, "Table view output is null.");
        try
        {
            var model = holder!.Model;
            switch ((PgmXtTableKind)tableKind)
            {
                case PgmXtTableKind.Parts: View(model is null ? holder.Builder!.Parts : default, model is null ? default : model.Parts, data, count, stride); break;
                case PgmXtTableKind.Bodies: View(model is null ? holder.Builder!.Bodies : default, model is null ? default : model.Bodies, data, count, stride); break;
                case PgmXtTableKind.Regions: View(model is null ? holder.Builder!.Regions : default, model is null ? default : model.Regions, data, count, stride); break;
                case PgmXtTableKind.Shells: View(model is null ? holder.Builder!.Shells : default, model is null ? default : model.Shells, data, count, stride); break;
                case PgmXtTableKind.Faces: View(model is null ? holder.Builder!.Faces : default, model is null ? default : model.Faces, data, count, stride); break;
                case PgmXtTableKind.Loops: View(model is null ? holder.Builder!.Loops : default, model is null ? default : model.Loops, data, count, stride); break;
                case PgmXtTableKind.Fins: View(model is null ? holder.Builder!.Fins : default, model is null ? default : model.Fins, data, count, stride); break;
                case PgmXtTableKind.Edges: View(model is null ? holder.Builder!.Edges : default, model is null ? default : model.Edges, data, count, stride); break;
                case PgmXtTableKind.Vertices: View(model is null ? holder.Builder!.Vertices : default, model is null ? default : model.Vertices, data, count, stride); break;
                case PgmXtTableKind.Points: View(model is null ? holder.Builder!.Points : default, model is null ? default : model.Points, data, count, stride); break;
                case PgmXtTableKind.Geometries: View(model is null ? holder.Builder!.Geometries : default, model is null ? default : model.Geometries, data, count, stride); break;
                case PgmXtTableKind.Transforms: View(model is null ? holder.Builder!.Transforms : default, model is null ? default : model.Transforms, data, count, stride); break;
                case PgmXtTableKind.Frames: View(model is null ? holder.Builder!.Frames : default, model is null ? default : model.Frames, data, count, stride); break;
                case PgmXtTableKind.Assemblies: View(model is null ? holder.Builder!.Assemblies : default, model is null ? default : model.Assemblies, data, count, stride); break;
                case PgmXtTableKind.Instances: View(model is null ? holder.Builder!.Instances : default, model is null ? default : model.Instances, data, count, stride); break;
                case PgmXtTableKind.AttributeDefinitions: View(model is null ? holder.Builder!.AttributeDefinitions : default, model is null ? default : model.AttributeDefinitions, data, count, stride); break;
                case PgmXtTableKind.AttributeFieldDefinitions: View(model is null ? holder.Builder!.AttributeFieldDefinitions : default, model is null ? default : model.AttributeFieldDefinitions, data, count, stride); break;
                case PgmXtTableKind.Attributes: View(model is null ? holder.Builder!.Attributes : default, model is null ? default : model.Attributes, data, count, stride); break;
                case PgmXtTableKind.AttributeValues: View(model is null ? holder.Builder!.AttributeValues : default, model is null ? default : model.AttributeValues, data, count, stride); break;
                case PgmXtTableKind.UserFields: View(model is null ? holder.Builder!.UserFields : default, model is null ? default : model.UserFields, data, count, stride); break;
                case PgmXtTableKind.Meshes: View(model is null ? holder.Builder!.Meshes : default, model is null ? default : model.Meshes, data, count, stride); break;
                case PgmXtTableKind.Lattices: View(model is null ? holder.Builder!.Lattices : default, model is null ? default : model.Lattices, data, count, stride); break;
                case PgmXtTableKind.Connections: View(model is null ? holder.Builder!.Connections : default, model is null ? default : model.Connections, data, count, stride); break;
                case PgmXtTableKind.ControlPoints: View(model is null ? holder.Builder!.ControlPoints : default, model is null ? default : model.ControlPoints, data, count, stride); break;
                case PgmXtTableKind.Scalars: View(model is null ? holder.Builder!.Scalars : default, model is null ? default : model.Scalars, data, count, stride); break;
                case PgmXtTableKind.Charts: View(model is null ? holder.Builder!.Charts : default, model is null ? default : model.Charts, data, count, stride); break;
                case PgmXtTableKind.Limits: View(model is null ? holder.Builder!.Limits : default, model is null ? default : model.Limits, data, count, stride); break;
                case PgmXtTableKind.IntersectionData: View(model is null ? holder.Builder!.IntersectionData : default, model is null ? default : model.IntersectionData, data, count, stride); break;
                case PgmXtTableKind.HVectors: View(model is null ? holder.Builder!.HVectors : default, model is null ? default : model.HVectors, data, count, stride); break;
                case PgmXtTableKind.GeometricOwners: View(model is null ? holder.Builder!.GeometricOwners : default, model is null ? default : model.GeometricOwners, data, count, stride); break;
                case PgmXtTableKind.TransmitOrder: View(model is null ? holder.Builder!.TransmitOrder : default, model is null ? default : model.TransmitOrder, data, count, stride); break;
                case PgmXtTableKind.Strings: View(model is null ? holder.Builder!.Strings : default, model is null ? default : model.Strings, data, count, stride); break;
                case PgmXtTableKind.Payload: View(model is null ? holder.Builder!.Payload : default, model is null ? default : model.Payload, data, count, stride); break;
                default: return Fail(XtErrorCode.InvalidArgument, "Unknown B-rep table kind.");
            }
            *readOnly = model is null ? (byte)0 : (byte)1;
            return Ok();
        }
        catch (Exception exception) { return Fail(exception); }
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BREP_delete", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus BrepDelete(PgmXtHandle brep) => Handles.Delete<BrepHolder>(brep) ? Ok() : InvalidHandle();

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_BUFFER_free", CallConvs = [typeof(CallConvCdecl)])]
    public static void BufferFree(nint data) { if (data != 0) NativeMemory.Free((void*)data); }

    [UnmanagedCallersOnly(EntryPoint = "PGM_XT_ERROR_get", CallConvs = [typeof(CallConvCdecl)])]
    public static PgmXtStatus ErrorGet(PgmXtBuffer* output)
    {
        if (output is null) return (PgmXtStatus)XtErrorCode.InvalidArgument;
        var bytes = System.Text.Encoding.UTF8.GetBytes(ErrorState.Message ?? string.Empty);
        output->Data = Allocate(bytes); output->Size = (nuint)bytes.Length; return Ok();
    }

    private static void View<T>(Span<T> writable, ReadOnlySpan<T> frozen, nint* data, PgmXtElementCount* count, PgmXtElementSize* stride) where T : unmanaged
    {
        var span = frozen.IsEmpty ? writable : frozen;
        *count = span.Length; *stride = sizeof(T);
        *data = span.IsEmpty ? 0 : (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
    }

    private static nint Allocate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return 0;
        var pointer = NativeMemory.Alloc((nuint)bytes.Length);
        bytes.CopyTo(new Span<byte>(pointer, bytes.Length));
        return (nint)pointer;
    }

    private static PgmXtStatus Ok() { ErrorState.Message = null; return 0; }
    private static PgmXtStatus InvalidHandle() => Fail(XtErrorCode.InvalidHandle, "Invalid or deleted XT handle.");
    private static PgmXtStatus Fail(Exception exception) => Fail(exception is XtFormatException format ? format.Code : XtErrorCode.InvalidData, exception.Message);
    private static PgmXtStatus Fail(XtErrorCode code, string message) { ErrorState.Message = message; return (PgmXtStatus)code; }
}

internal static class ErrorState { [ThreadStatic] internal static string? Message; }

internal static class Handles
{
    private sealed class Entry { internal object? Value; internal PgmXtGeneration Generation = 1; }
    private static readonly List<Entry> Entries = [];
    private static readonly object Sync = new();

    internal static PgmXtHandle Add(object value)
    {
        lock (Sync)
        {
            for (var slot = 0; slot < Entries.Count; slot++)
                if (Entries[slot].Value is null) { Entries[slot].Value = value; return Token(slot, Entries[slot].Generation); }
            Entries.Add(new Entry { Value = value }); return Token(Entries.Count - 1, 1);
        }
    }

    internal static bool TryGet<T>(PgmXtHandle token, out T? value) where T : class
    {
        Decode(token, out var slot, out var generation);
        lock (Sync)
        {
            if ((uint)slot < (uint)Entries.Count && Entries[slot].Generation == generation && Entries[slot].Value is T typed) { value = typed; return true; }
        }
        value = null; return false;
    }

    internal static bool Delete<T>(PgmXtHandle token) where T : class
    {
        Decode(token, out var slot, out var generation);
        lock (Sync)
        {
            if ((uint)slot >= (uint)Entries.Count || Entries[slot].Generation != generation || Entries[slot].Value is not T) return false;
            Entries[slot].Value = null; Entries[slot].Generation++; if (Entries[slot].Generation == 0) Entries[slot].Generation = 1; return true;
        }
    }

    private static PgmXtHandle Token(PgmXtSlot slot, PgmXtGeneration generation) => ((ulong)generation << 32) | (uint)(slot + 1);
    private static void Decode(PgmXtHandle token, out PgmXtSlot slot, out PgmXtGeneration generation) { slot = unchecked((int)(uint)token) - 1; generation = (uint)(token >> 32); }
}
