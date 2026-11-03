using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

[StructLayout(LayoutKind.Sequential)]
internal struct PgmDisplayBinding
{
    internal EntityTag Entity;
    internal KernelClassCode Class;
    internal XtPersistentIdentifier Identifier;
    internal EntityGeneration Generation;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PgmDisplaySnapshot
{
    internal KernelSessionId SessionGeneration;
    internal EntityTag Body;
    internal EntityGeneration BodyGeneration;
    internal BufferCount BindingCount;
    internal PgmDisplayBinding* Bindings;
    internal PK_MEMORY_block_s Xt;
}
