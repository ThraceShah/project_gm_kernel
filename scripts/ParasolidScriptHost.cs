using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static parasolid;

internal static unsafe class ParasolidScriptHost
{
    private static string? libraryPath;
    private static nint libraryHandle;
    private static bool resolverRegistered;

    public static bool TryStartSession(
        string label,
        out ParasolidScriptSession? session,
        out string message,
        [CallerFilePath] string scriptPath = "")
    {
        session = null;
        if (!TryPrepare(label, scriptPath, out message))
            return false;

        try
        {
            RegisterCallbacks();
            var options = new PK_SESSION_start_o_t();
            Check(PK_SESSION_start(&options), "PK_SESSION_start");
            session = new ParasolidScriptSession();
            message = "";
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            message = label + " skipped: local Parasolid runtime is unavailable: " + ex.Message;
            return false;
        }
    }

    public static void Check(PK_ERROR_code_t error, string name)
    {
        if (error != 0)
            throw new InvalidOperationException($"{name} failed with error {error}");
    }

    private static bool TryPrepare(string label, string scriptPath, out string message)
    {
        var platform = GetPlatform();
        if (platform.Length == 0)
        {
            message = $"{label} skipped: unsupported host {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}.";
            return false;
        }

        var scriptDir = Path.GetDirectoryName(scriptPath);
        var repoRoot = Path.GetFullPath(Path.Combine(scriptDir ?? ".", ".."));
        var schemaDir = Path.Combine(repoRoot, "third_party", "parasolid", "schema");
        var candidateLibraryPath = GetDynamicLibraryPath(repoRoot, platform);
        if (!Directory.Exists(schemaDir) || !File.Exists(candidateLibraryPath))
        {
            message = label + " skipped: third_party/parasolid schema or dynamic library is missing.";
            return false;
        }

        Environment.SetEnvironmentVariable("P_SCHEMA", schemaDir);
        libraryPath = candidateLibraryPath;
        RegisterResolver();

        try
        {
            if (libraryHandle == 0)
                libraryHandle = NativeLibrary.Load(candidateLibraryPath);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            message = label + " skipped: local Parasolid runtime is unavailable: " + ex.Message;
            return false;
        }

        message = "";
        return true;
    }

    private static string GetPlatform()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
            Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
            Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
            Architecture.Arm64 when OperatingSystem.IsMacOS() => "mac-arm64",
            _ => "",
        };
    }

    private static string GetDynamicLibraryPath(string repoRoot, string platform)
    {
        var libraryName = platform switch
        {
            "win-x64" => "pskernel.dll",
            "linux-x64" or "linux-arm64" => "libpskernel.so",
            "mac-arm64" => "libpskernel.dylib",
            _ => "",
        };

        return libraryName.Length == 0
            ? ""
            : Path.Combine(repoRoot, "third_party", "parasolid", "lib", platform, libraryName);
    }

    private static void RegisterResolver()
    {
        if (resolverRegistered)
            return;

        NativeLibrary.SetDllImportResolver(typeof(parasolid).Assembly, ResolveImport);
        resolverRegistered = true;
    }

    private static nint ResolveImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "pskernel", StringComparison.Ordinal))
            return 0;

        if (libraryHandle != 0)
            return libraryHandle;

        return libraryPath is { Length: > 0 }
            ? NativeLibrary.Load(libraryPath)
            : 0;
    }

    private static void RegisterCallbacks()
    {
        var frustrum = new PK_SESSION_frustrum_t
        {
            fstart = &FrustrumOk,
            fabort = &FrustrumOk,
            fstop = &FrustrumOk,
            fmallo = &FrustrumAlloc,
            fmfree = &FrustrumFree,
        };
        Check(PK_SESSION_register_frustrum(&frustrum), "PK_SESSION_register_frustrum");

        var memory = new PK_MEMORY_frustrum_t(&NativeAlloc, &NativeFree);
        Check(PK_MEMORY_register_callbacks(memory), "PK_MEMORY_register_callbacks");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrustrumOk(int* ifail)
    {
        *ifail = FR_no_errors;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrustrumAlloc(int* nbytes, byte** memory, int* ifail)
    {
        *memory = (byte*)NativeMemory.Alloc((nuint)(*nbytes));
        *ifail = *memory is null ? FR_memory_full : FR_no_errors;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrustrumFree(int* nbytes, byte** memory, int* ifail)
    {
        NativeMemory.Free(*memory);
        *memory = null;
        *ifail = FR_no_errors;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* NativeAlloc(ulong bytes)
    {
        return NativeMemory.Alloc((nuint)bytes);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void NativeFree(void* memory)
    {
        NativeMemory.Free(memory);
    }
}

internal sealed class ParasolidScriptSession : IDisposable
{
    private bool disposed;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ParasolidScriptHost.Check(parasolid.PK_SESSION_stop(), "PK_SESSION_stop");
    }
}
