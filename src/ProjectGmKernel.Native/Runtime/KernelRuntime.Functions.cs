using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal static int FunctionFind(BufferCount count, byte** names, PK_FUNCTION_find_o_s* options, KernelFunctionId* functions)
    {
        var command = new FunctionFindCommand { Count = count, Names = names, Options = options, Functions = functions };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command);
    }

    internal static int ThreadAskFunctionRun(BufferCount count, KernelFunctionId* functions,
        PK_THREAD_ask_function_run_o_s* options, int* runValues)
    {
        var command = new FunctionRunCommand { Count = count, Functions = functions, Options = options, RunValues = runValues };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command);
    }

    internal static int ThreadAskLocalLevel(PK_THREAD_ask_local_level_o_s* options, int* level)
    {
        var command = new LocalLevelCommand { Options = options, Level = level };
        return Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Unprotected, AccessKind.ReadOnly, ref command);
    }

    private struct FunctionFindCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal byte** Names;
        internal PK_FUNCTION_find_o_s* Options;
        internal KernelFunctionId* Functions;

        public int Execute()
        {
            if (Count < 0 || (Count != 0 && (Names == null || Functions == null)))
                return ParasolidConstants.PK_ERROR_bad_field_number;
            if (Options != null && Options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            // Validate the entire array before publishing any result.
            for (var i = 0; i < Count; i++)
                if (Find(Names[i]) == 0) return ParasolidConstants.PK_ERROR_bad_name;
            for (var i = 0; i < Count; i++) Functions[i] = Find(Names[i]);
            return 0;
        }

        private static KernelFunctionId Find(byte* name)
        {
            if (name == null) return 0;
            var length = 0;
            while (length < 128 && name[length] != 0) length++;
            if (length == 128) return 0;
            var value = new ReadOnlySpan<byte>(name, length);
            for (var id = 1; id <= FunctionCatalog.Count; id++)
                if (value.SequenceEqual(FunctionCatalog.Name(id))) return id;
            return 0;
        }
    }

    private struct FunctionRunCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal KernelFunctionId* Functions;
        internal PK_THREAD_ask_function_run_o_s* Options;
        internal int* RunValues;

        public int Execute()
        {
            if (Count < 0 || (Count != 0 && (Functions == null || RunValues == null)))
                return ParasolidConstants.PK_ERROR_bad_field_number;
            if (Options != null && Options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
            for (var i = 0; i < Count; i++)
                if (Functions[i] <= 0 || Functions[i] > FunctionCatalog.Count) return ParasolidConstants.PK_ERROR_bad_value;
            var context = ThreadContext();
            if (context == null) return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = 0; i < Count; i++)
            {
                var kind = FunctionCatalog.Concurrency(Functions[i]);
                if (FunctionCatalog.Name(Functions[i]).SequenceEqual("PK_THREAD_chain_start"u8))
                {
                    RunValues[i] = ParasolidConstants.PK_FUNCTION_run_dynamic_c;
                    continue;
                }
                var exclusive = kind != ConcurrencyKind.Unprotected
                    && (context->ChainType == ParasolidConstants.PK_THREAD_chain_exclusive_c
                        || kind == ConcurrencyKind.Exclusive || (kind == ConcurrencyKind.Local && context->LockCount == 0));
                RunValues[i] = exclusive ? ParasolidConstants.PK_FUNCTION_run_exclusive_c : ParasolidConstants.PK_FUNCTION_run_concurrent_c;
            }
            return 0;
        }
    }

    private struct LocalLevelCommand : IKernelCommand
    {
        internal PK_THREAD_ask_local_level_o_s* Options;
        internal int* Level;
        public int Execute()
        {
            if (Level == null) return ParasolidConstants.PK_ERROR_bad_field_number;
            if (Options != null && Options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
            *Level = ParasolidConstants.PK_THREAD_local_none_c;
            return 0;
        }
    }
}
