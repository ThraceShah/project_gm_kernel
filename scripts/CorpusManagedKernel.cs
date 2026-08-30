using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

internal static unsafe class CorpusManagedKernel
{
    public static byte[] Transmit(Func<PK_BODY_t> createBody)
    {
        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&startOptions), "managed PK_SESSION_start");
        try
        {
            var body = createBody();
            var transmitOptions = new PK_PART_transmit_o_s
            {
                o_t_version = 1,
                transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                transmit_version = 371,
            };
            var block = new PK_MEMORY_block_s();
            Check(KernelRuntime.PartTransmitB(1, &body, &transmitOptions, &block), "managed PK_PART_transmit_b");
            try
            {
                using var stream = new MemoryStream();
                for (var current = &block; current is not null; current = current->next)
                {
                    if (current->bytes is not null && current->n_bytes != 0)
                        stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
                }

                return stream.ToArray();
            }
            finally
            {
                Check(KernelRuntime.MemoryBlockFree(&block), "managed PK_MEMORY_block_f");
            }
        }
        finally
        {
            Check(KernelRuntime.SessionStop(), "managed PK_SESSION_stop");
        }
    }

    private static void Check(int error, string operation)
    {
        if (error != ParasolidConstants.PK_ERROR_no_errors)
            throw new InvalidOperationException(operation + " failed with error " + error);
    }
}
