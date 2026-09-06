#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidMarkOracle=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("thread oracle", out var session, out var message, configureRollback: MarkOracleStorage.Register))
        throw new InvalidOperationException(message);
    using (session)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "our session");
        try
        {
            CheckCallbackDefaultsAndPointOwnership();
            var idOptions = new PK_THREAD_set_id_o_t { o_t_version = 1 };
            var ourIdOptions = new M.PK_THREAD_set_id_o_s { o_t_version = 1 };
            PK_THREAD_set_id_r_t idResult;
            M.PK_THREAD_set_id_r_s ourIdResult;
            Check(PK_THREAD_set_id(4711, &idOptions, &idResult), "reference set id");
            Check(KernelRuntime.ThreadSetId(4711, &ourIdOptions, &ourIdResult), "our set id");
            int id, ourId;
            PK_LOGICAL_t sub;
            byte ourSub;
            Check(PK_THREAD_ask_id(&id, null, &sub), "reference ask id with optional null");
            Check(KernelRuntime.ThreadAskId(&ourId, null, &ourSub), "our ask id with optional null");
            Equal(id, ourId, "thread id");
            Equal((byte)sub, ourSub, "subthread");
            CheckChain();
            CheckKernel();
            foreach (var type in new[] { M.ParasolidConstants.PK_THREAD_chain_concurrent_c, M.ParasolidConstants.PK_THREAD_chain_exclusive_c })
            foreach (var length in new[] { 0, 1, 3 })
            {
                var options = new PK_THREAD_chain_start_o_t { o_t_version = 2, length = length, local_level = PK_THREAD_local_none_c };
                var ours = new M.PK_THREAD_chain_start_o_s { o_t_version = 2, length = length, local_level = PK_THREAD_local_none_c };
                Check(PK_THREAD_chain_start(type, &options), "reference chain start");
                Check(KernelRuntime.ThreadChainStart(type, &ours), "our chain start");
                for (var i = 0; i < 7; i++)
                {
                    CheckChain();
                    CheckKernel();
                    int partition, ourPartition;
                    Check(PK_SESSION_ask_curr_partition(&partition), "reference protected query");
                    Check(KernelRuntime.SessionAskCurrentPartition(&ourPartition), "our protected query");
                }
                var stop = new PK_THREAD_chain_stop_o_t { o_t_version = 1 };
                var ourStop = new M.PK_THREAD_chain_stop_o_s { o_t_version = 1 };
                Check(PK_THREAD_chain_stop(&stop), "reference chain stop");
                Check(KernelRuntime.ThreadChainStop(&ourStop), "our chain stop");
                CheckChain();
                CheckKernel();
                Console.WriteLine($"thread chain type={type} length={length}: oracle passed");
            }
            PK_MEMORY_frustrum_t callbacks;
            M.PK_MEMORY_frustrum_s oursCallbacks;
            Check(PK_THREAD_ask_memory_cbs(&callbacks), "reference unset callbacks");
            Check(KernelRuntime.ThreadAskMemoryCbs(&oursCallbacks), "our unset callbacks");
            Equal(callbacks.alloc_fn == null ? 0 : 1, oursCallbacks.alloc_fn == null ? 0 : 1, "unset allocator");
            Equal(callbacks.free_fn == null ? 0 : 1, oursCallbacks.free_fn == null ? 0 : 1, "unset free callback");
            CheckFunctionCatalog();
            Console.WriteLine("Thread identity, chain queries, protection flags and callback queries: oracle passed");
        }
        finally { KernelRuntime.SessionStop(); }
    }
}

static unsafe void CheckChain()
{
    int type, length, remaining, ourType, ourLength, ourRemaining;
    Check(PK_THREAD_is_in_chain(&type, &length, &remaining), "reference chain query");
    Check(KernelRuntime.ThreadIsInChain(&ourType, &ourLength, &ourRemaining), "our chain query");
    Equal(type, ourType, "chain type");
    Equal(length, ourLength, "chain length");
    Equal(remaining, ourRemaining, "chain remaining");
}

static unsafe void CheckCallbackDefaultsAndPointOwnership()
{
    PK_MEMORY_frustrum_t saved, after;
    M.PK_MEMORY_frustrum_s ourSaved, ourAfter;
    Check(PK_MEMORY_ask_callbacks(&saved), "saved global callbacks");
    Check(KernelRuntime.MemoryAskCallbacks(&ourSaved), "our saved global callbacks");
    var partial = new PK_MEMORY_frustrum_t { alloc_fn = saved.alloc_fn };
    var ourPartial = new M.PK_MEMORY_frustrum_s
    { alloc_fn = (delegate* unmanaged[Cdecl]<nuint, nint>)(void*)saved.alloc_fn };
    Check(PK_MEMORY_register_callbacks(partial), "reference partial global callbacks");
    Check(KernelRuntime.MemoryRegisterCallbacks(ourPartial), "our partial global callbacks");
    Check(PK_MEMORY_ask_callbacks(&after), "reference normalized global callbacks");
    Check(KernelRuntime.MemoryAskCallbacks(&ourAfter), "our normalized global callbacks");
    Equal(after.alloc_fn == null ? 0 : 1, ourAfter.alloc_fn == null ? 0 : 1, "normalized global allocator");
    Equal(after.free_fn == null ? 0 : 1, ourAfter.free_fn == null ? 0 : 1, "normalized global free");
    Check(PK_MEMORY_register_callbacks(saved), "restore global callbacks");
    Check(KernelRuntime.MemoryRegisterCallbacks(ourSaved), "restore our global callbacks");
    Check(PK_THREAD_register_memory_cbs(partial), "reference partial thread callbacks");
    Check(KernelRuntime.ThreadRegisterMemoryCbs(ourPartial), "our partial thread callbacks");
    Check(PK_THREAD_ask_memory_cbs(&after), "reference normalized thread callbacks");
    Check(KernelRuntime.ThreadAskMemoryCbs(&ourAfter), "our normalized thread callbacks");
    Equal(after.alloc_fn == null ? 0 : 1, ourAfter.alloc_fn == null ? 0 : 1, "normalized thread allocator");
    Equal(after.free_fn == null ? 0 : 1, ourAfter.free_fn == null ? 0 : 1, "normalized thread free");
    int body, ourBody, count, ourCount, point, ourPoint, oldPoint, ourOldPoint;
    int* vertices;
    int* ourVertices;
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "point ownership block");
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &ourBody), "our point ownership block");
    Check(PK_BODY_ask_vertices(body, &count, &vertices), "point ownership vertices");
    Check(KernelRuntime.BodyAskVertices(ourBody, &ourCount, &ourVertices), "our point ownership vertices");
    Equal(count, ourCount, "point ownership vertex count");
    Check(PK_VERTEX_ask_point(vertices[0], &point), "owned point");
    Check(KernelRuntime.VertexAskPoint(ourVertices[0], &ourPoint), "our owned point");
    Check(PK_VERTEX_ask_point(vertices[1], &oldPoint), "target point");
    Check(KernelRuntime.VertexAskPoint(ourVertices[1], &ourOldPoint), "our target point");
    Check(PK_TOPOL_detach_geom(vertices[1]), "detach target point");
    Check(KernelRuntime.TopologyDetachGeometry(ourVertices[1]), "our detach target point");
    Equal(PK_VERTEX_attach_points(1, vertices + 1, &point),
        KernelRuntime.VertexAttachPoints(1, ourVertices + 1, &ourPoint), "point cannot have two parents");
    Check(PK_VERTEX_attach_points(1, vertices + 1, &oldPoint), "restore target point");
    Check(KernelRuntime.VertexAttachPoints(1, ourVertices + 1, &ourOldPoint), "restore our target point");
    Check(PK_MEMORY_free(vertices), "free vertices");
    Check(KernelRuntime.MemoryFree(ourVertices), "free our vertices");
    Console.WriteLine("Partial callback normalization and unique point ownership: oracle passed");
}

static unsafe void CheckKernel()
{
    PK_LOGICAL_t inside, protection, subthread, exclusion;
    byte ourInside, ourProtection, ourSubthread, ourExclusion;
    Check(PK_THREAD_is_in_kernel(&inside, &protection, &subthread, &exclusion), "reference kernel query");
    Check(KernelRuntime.ThreadIsInKernel(&ourInside, &ourProtection, &ourSubthread, &ourExclusion), "our kernel query");
    Equal((byte)inside, ourInside, "inside kernel");
    Equal((byte)protection, ourProtection, "protected region");
    Equal((byte)subthread, ourSubthread, "internal subthread");
    Equal((byte)exclusion, ourExclusion, "exclusion");
}

static unsafe void CheckFunctionCatalog()
{
    Span<byte> buffer = stackalloc byte[128];
    var referenceUnavailable = 0;
    for (var i = 1; i <= FunctionCatalog.Count; i++)
    {
        buffer.Clear();
        FunctionCatalog.Name(i).CopyTo(buffer);
        fixed (byte* p = buffer)
        {
            var name = p;
            int function, referenceFunction, run, referenceRun;
            var options = new PK_FUNCTION_find_o_t { o_t_version = 1 };
            var ours = new M.PK_FUNCTION_find_o_s { o_t_version = 1 };
            var ask = new PK_THREAD_ask_function_run_o_t { o_t_version = 1 };
            var ourAsk = new M.PK_THREAD_ask_function_run_o_s { o_t_version = 1 };
            Check(KernelRuntime.FunctionFind(1, &name, &ours, &function), "our function lookup");
            Equal(i, function, "function token");
            Check(KernelRuntime.ThreadAskFunctionRun(1, &function, &ourAsk, &run), "our classification");
            var error = PK_FUNCTION_find(1, &name, &options, &referenceFunction);
            if (error == PK_ERROR_bad_name) { referenceUnavailable++; continue; }
            Check(error, "reference function lookup");
            error = PK_THREAD_ask_function_run(1, &referenceFunction, &ask, &referenceRun);
            if (error == PK_ERROR_not_implemented) { referenceUnavailable++; continue; }
            Check(error, "reference classification");
            Equal(referenceRun, run, "function classification");
        }
    }
    Console.WriteLine($"Function catalog: {FunctionCatalog.Count} supported exports checked; reference classification unavailable for {referenceUnavailable} (SDK reports missing name or not_implemented).");
}

static void Check(int error, string name)
{
    if (error != 0) throw new InvalidOperationException($"{name}: error={error}");
}
static void Equal(int expected, int actual, string name)
{
    if (expected != actual) throw new InvalidOperationException($"{name}: expected={expected} actual={actual}");
}
