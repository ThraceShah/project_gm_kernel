using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static parasolid;

internal static unsafe class ParasolidScriptHost
{
    private static string? libraryPath;
    private static nint libraryHandle;
    private static bool resolverRegistered;
    private static string? schemaCacheDirectory;
    private static readonly FileStream?[] OpenSchemaFiles = new FileStream?[64];

    public static bool TryStartSession(
        string label,
        out ParasolidScriptSession? session,
        out string message,
        [CallerFilePath] string scriptPath = "",
        int userFieldLength = 0)
    {
        session = null;
        if (!TryPrepare(label, scriptPath, out message))
            return false;

        try
        {
            RegisterCallbacks();
            var options = new PK_SESSION_start_o_t { user_field = userFieldLength };
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
        var repoRoot = FindRepositoryRoot(scriptDir ?? ".");
        var schemaSourceDir = Path.Combine(repoRoot, "third_party", "parasolid", "schema");
        var candidateLibraryPath = GetDynamicLibraryPath(repoRoot, platform);
        if (!Directory.Exists(schemaSourceDir) || !File.Exists(candidateLibraryPath))
        {
            message = label + " skipped: third_party/parasolid schema or dynamic library is missing.";
            return false;
        }

        var schemaDir = PrepareSchemaCache(repoRoot, schemaSourceDir);
        schemaCacheDirectory = schemaDir;
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

    private static string PrepareSchemaCache(string repoRoot, string sourceDirectory)
    {
        var cacheDirectory = Path.Combine(repoRoot, "bin", "parasolid-schema-cache");
        Directory.CreateDirectory(cacheDirectory);
        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "sch_*.sch_txt", SearchOption.TopDirectoryOnly))
        {
            var stem = Path.GetFileName(source)[..^".sch_txt".Length];
            var destination = Path.Combine(cacheDirectory, stem.ToLowerInvariant() + ".s_t");
            var sourceInfo = new FileInfo(source);
            var destinationInfo = new FileInfo(destination);
            if (!destinationInfo.Exists || destinationInfo.Length != sourceInfo.Length || destinationInfo.LastWriteTimeUtc < sourceInfo.LastWriteTimeUtc)
                File.Copy(source, destination, overwrite: true);
        }
        return cacheDirectory;
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var current = Path.GetFullPath(startDirectory);
        while (true)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, "AGENTS.md")))
                return current;

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
                return current;
            current = parent;
        }
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
            ffoprd = &FrustrumFileOpenRead,
            ffread = &FrustrumFileRead,
            ffclos = &FrustrumFileClose,
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
    private static void FrustrumFileOpenRead(int* guise, int* format, byte* name, int* nameLength, int* skipHeader, int* streamId, int* ifail)
    {
        *streamId = -1;
        *ifail = FR_open_fail;
        try
        {
            if (schemaCacheDirectory is null || *nameLength <= 0)
                return;
            var requested = System.Text.Encoding.ASCII.GetString(new ReadOnlySpan<byte>(name, *nameLength));
            requested = Path.GetFileName(requested).ToLowerInvariant();
            if (!requested.EndsWith(".s_t", StringComparison.Ordinal))
                requested += ".s_t";
            var path = Path.Combine(schemaCacheDirectory, requested);
            var slot = Array.FindIndex(OpenSchemaFiles, static file => file is null);
            if (slot < 0 || !File.Exists(path))
                return;
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (*skipHeader == FFSKHD)
                SkipSchemaHeader(stream);
            OpenSchemaFiles[slot] = stream;
            *streamId = slot;
            *ifail = FR_no_errors;
        }
        catch
        {
            *ifail = FR_open_fail;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrustrumFileRead(int* guise, int* streamId, int* maximum, byte* buffer, int* actual, int* ifail)
    {
        *actual = 0;
        *ifail = FR_read_fail;
        try
        {
            if ((uint)*streamId >= (uint)OpenSchemaFiles.Length || OpenSchemaFiles[*streamId] is not { } stream)
                return;
            *actual = stream.Read(new Span<byte>(buffer, *maximum));
            *ifail = *actual == 0 ? FR_end_of_file : FR_no_errors;
        }
        catch
        {
            *ifail = FR_read_fail;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrustrumFileClose(int* guise, int* streamId, int* action, int* ifail)
    {
        *ifail = FR_close_fail;
        try
        {
            if ((uint)*streamId >= (uint)OpenSchemaFiles.Length || OpenSchemaFiles[*streamId] is not { } stream)
                return;
            OpenSchemaFiles[*streamId] = null;
            stream.Dispose();
            *ifail = FR_no_errors;
        }
        catch
        {
            *ifail = FR_close_fail;
        }
    }

    private static void SkipSchemaHeader(FileStream stream)
    {
        Span<byte> one = stackalloc byte[1];
        var line = new List<byte>(96);
        while (stream.Read(one) != 0)
        {
            if (one[0] == (byte)'\n')
            {
                if (line.Count >= 15 && System.Text.Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(line)).StartsWith("**END_OF_HEADER", StringComparison.Ordinal))
                    return;
                line.Clear();
            }
            else if (one[0] != (byte)'\r')
            {
                line.Add(one[0]);
            }
        }
        stream.Position = 0;
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
