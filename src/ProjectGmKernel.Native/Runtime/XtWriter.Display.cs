using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class XtWriter
{
    internal readonly record struct DisplayBinding(EntityTag Body, EntityTag Entity, PK_CLASS_t Class, XtPersistentIdentifier Identifier, EntityGeneration Generation);

    internal static int CaptureNativeDisplay(EntityTag body,PgmDisplaySnapshot* snapshot)
    {
        if(snapshot==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        *snapshot=default;
        var command=new NativeDisplayCommand { Body=body,Output=snapshot };
        return KernelRuntime.Dispatch(ApiId.PartTransmitB,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,body);
    }
    private struct NativeDisplayCommand:IKernelCommand
    {
        internal EntityTag Body;internal PgmDisplaySnapshot* Output;
        public int Execute()
        {
            var error=CaptureDisplay(Body,out var text,out var bindings,out var session,out var generation);
            if(error!=0)return error;
            var byteCount=System.Text.Encoding.UTF8.GetByteCount(text);
            var bytes=(byte*)KernelRuntime.State.Session->Returns.TryAllocate((nuint)byteCount);
            if(bytes==null)return ParasolidConstants.PK_ERROR_memory_full;
            var map=bindings.Length==0?null:(PgmDisplayBinding*)KernelRuntime.State.Session->Returns.TryAllocate((nuint)(bindings.Length*sizeof(PgmDisplayBinding)));
            if(bindings.Length!=0&&map==null) { KernelRuntime.MemoryFree(bytes);return ParasolidConstants.PK_ERROR_memory_full; }
            System.Text.Encoding.UTF8.GetBytes(text,new Span<byte>(bytes,byteCount));
            for(var i=0;i<bindings.Length;i++)map[i]=new() { Entity=bindings[i].Entity,Class=bindings[i].Class,Identifier=bindings[i].Identifier,Generation=bindings[i].Generation };
            *Output=new() { SessionGeneration=session,Body=Body,BodyGeneration=generation,BindingCount=bindings.Length,Bindings=map,
                Xt=new PK_MEMORY_block_s { bytes=bytes,n_bytes=(nuint)byteCount } };
            return 0;
        }
    }

    /// <summary>XT plus its exact persistent-ID map, captured under one read claim.</summary>
    internal static int CaptureDisplay(EntityTag body, out string text, out DisplayBinding[] bindings, out KernelSessionId sessionGeneration, out EntityGeneration bodyGeneration)
    {
        var command=new CaptureDisplayCommand { Body=body,Text="",Bindings=[] };
        var error=KernelRuntime.Dispatch(ApiId.PartTransmitB,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,body);
        text=command.Text;bindings=command.Bindings;sessionGeneration=command.SessionGeneration;bodyGeneration=command.BodyGeneration;
        return error;
    }
    private struct CaptureDisplayCommand:IKernelCommand
    {
        internal EntityTag Body;
        internal string Text;
        internal DisplayBinding[] Bindings;
        internal KernelSessionId SessionGeneration;
        internal EntityGeneration BodyGeneration;
        public int Execute()
        {
            if(!KernelRuntime.IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
            if(!KernelRuntime.TryResolveBodySlot(Body,out var slot))return ParasolidConstants.PK_ERROR_unknown_class;
            if(KernelRuntime.Xt.IsOpaque(slot))return ParasolidConstants.PK_ERROR_not_implemented;
            var captured=new List<DisplayBinding>();
            var error=WriteText([Body],380,out Text,captured);
            if(error!=0)return error;
            Bindings=captured.ToArray();SessionGeneration=KernelRuntime.State.Session->SessionGeneration;
            BodyGeneration=KernelRuntime.GetBodyRecord(slot).Header.Generation;
            return 0;
        }
    }
    private static void AddDisplayBindings(in BodyRecord body,NodeMap map,List<DisplayBinding> output)
    {
        foreach(var pair in map.FaceSlots)
        {
            var header=KernelRuntime.GetFaceRecord(pair.Key).Header;
            output.Add(new(body.Header.Tag,header.Tag,ParasolidConstants.PK_CLASS_face,NodeId(pair.Value,map),header.Generation));
        }
        foreach(var pair in map.EdgeSlots)
        {
            var header=KernelRuntime.GetEdgeRecord(pair.Key).Header;
            output.Add(new(body.Header.Tag,header.Tag,ParasolidConstants.PK_CLASS_edge,NodeId(pair.Value,map),header.Generation));
        }
        foreach(var pair in map.VertexSlots)
        {
            var header=KernelRuntime.GetVertexRecord(pair.Key).Header;
            output.Add(new(body.Header.Tag,header.Tag,ParasolidConstants.PK_CLASS_vertex,NodeId(pair.Value,map),header.Generation));
        }
    }
    internal static bool IsCurrentDisplayBinding(in DisplayBinding binding,KernelSessionId sessionGeneration,EntityGeneration bodyGeneration)
    {
        var command=new ValidateDisplayBindingCommand { Binding=binding,SessionGeneration=sessionGeneration,BodyGeneration=bodyGeneration };
        return KernelRuntime.Dispatch(ApiId.EntityAskClass,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,binding.Body)==0;
    }
    private struct ValidateDisplayBindingCommand:IKernelCommand
    {
        internal DisplayBinding Binding;internal KernelSessionId SessionGeneration;internal EntityGeneration BodyGeneration;
        public int Execute()
        {
            if(!KernelRuntime.IsSessionStarted || KernelRuntime.State.Session->SessionGeneration!=SessionGeneration
                || !KernelRuntime.TryResolveBodySlot(Binding.Body,out var body)
                || KernelRuntime.GetBodyRecord(body).Header.Generation!=BodyGeneration
                || !KernelRuntime.IsValidTag(Binding.Entity))return ParasolidConstants.PK_ERROR_unknown_class;
            var record=KernelRuntime.ResolveTagRecord(Binding.Entity);
            KernelClassCode actualClass=0;
            var error=KernelRuntime.EntityAskClass(Binding.Entity,&actualClass);
            return record.Generation==Binding.Generation && error==0 && actualClass==Binding.Class?0:ParasolidConstants.PK_ERROR_unknown_class;
        }
    }
}
