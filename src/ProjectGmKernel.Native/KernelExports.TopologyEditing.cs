using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_EDGE_split_at_param")]
    public static int PK_EDGE_split_at_param(EntityTag edge,double parameter,EntityTag* vertex,EntityTag* addedEdge)
        =>KernelRuntime.EdgeSplitAtParam(edge,parameter,vertex,addedEdge);
}
