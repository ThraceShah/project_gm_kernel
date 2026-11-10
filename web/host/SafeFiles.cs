using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Win32.SafeHandles;

namespace GmProgressHost;

/// <summary>
/// Minimal read-only file sender for the two public roots. It replaces the
/// generic static-file middleware so the object that is opened is exactly the
/// object that was safety-checked: the leaf is opened with O_NOFOLLOW (a link
/// swapped in at the leaf fails the open), the full component chain is
/// re-verified after the open, and the streamed bytes come from that opened
/// handle without ever loading the file into memory. Unknown extensions are
/// never served; HEAD returns headers only; conditional GET is supported.
/// </summary>
internal static class SafeFiles
{
    private static readonly FileExtensionContentTypeProvider Types = CreateTypes();

    private static FileExtensionContentTypeProvider CreateTypes()
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".log"] = "text/plain; charset=utf-8";
        types.Mappings[".webmanifest"] = "application/manifest+json";
        return types;
    }

    /// <summary>
    /// Resolves and serves <paramref name="relative"/> under
    /// <paramref name="root"/>. <paramref name="mimeName"/> optionally derives
    /// the content type from the public URL name instead of the storage name
    /// (the object store keys files by hash). Returns true when a response was
    /// written.
    /// </summary>
    internal static async Task<bool> ServeAsync(HttpContext context, string root, string relative, string cacheControl, string? mimeName = null)
    {
        if (!SafePaths.TryResolve(root, relative, out var physical) || !File.Exists(physical)) return false;
        if (!Types.TryGetContentType(mimeName ?? physical, out var contentType)) return false; // unknown extensions are not public
        var handle = OpenNoFollow(physical);
        if (handle is null || handle.IsInvalid)
        {
            handle?.Dispose();
            return false;
        }
        using var opened = handle;
        // The checked path and the opened object must agree: re-verify the
        // chain after the open (see docs/web-progress-hosting.md for the
        // residual same-user race policy).
        if (!SafePaths.TryResolve(root, relative, out _)) return false;
        // Length comes from the opened descriptor itself (lseek on the same
        // object that will be streamed); nothing is buffered in full.
        var fd = (int)opened.DangerousGetHandle();
        var length = LSeek(fd, 0, SEEK_END);
        if (length < 0 || LSeek(fd, 0, SEEK_SET) < 0) return false;
        await using var stream = new FileStream(opened, FileAccess.Read, bufferSize: 128 * 1024, isAsync: false);
        var modified = File.GetLastWriteTimeUtc(physical);
        var response = context.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = contentType;
        response.Headers.CacheControl = cacheControl;
        response.Headers.LastModified = modified.ToString("R");
        response.ContentLength = length;

        if (HttpMethods.IsHead(context.Request.Method)) return true;
        if (ModifiedSince(context.Request, modified))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            response.ContentLength = null;
            return true;
        }
        // Streamed in bounded buffers; nothing is loaded in full.
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        return true;
    }

    private static bool ModifiedSince(HttpRequest request, DateTime modified)
    {
        if (!request.Headers.TryGetValue("If-Modified-Since", out var raw)
            || !DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var since)) return false;
        return modified <= since.AddSeconds(1);
    }

    // O_RDONLY|O_NOFOLLOW|O_CLOEXEC on Linux; the open itself refuses symlink
    // leaves (ELOOP). Non-Linux falls back to a plain open (the component walk
    // still applies); deployment targets are Linux.
    private static SafeFileHandle? OpenNoFollow(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            try { return File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous | FileOptions.SequentialScan); }
            catch { return null; }
        }
        var fd = Open(path, O_RDONLY | O_NOFOLLOW | O_CLOEXEC);
        return fd < 0 ? null : new SafeFileHandle((IntPtr)fd, ownsHandle: true);
    }

    private const int O_RDONLY = 0x0000;
    private const int O_NOFOLLOW = 0x20000;  // 0400000 (asm-generic/fcntl.h)
    private const int O_CLOEXEC = 0x80000;   // 02000000
    private const int SEEK_SET = 0;
    private const int SEEK_END = 2;

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int Open(string path, int flags);

    [DllImport("libc", SetLastError = true, EntryPoint = "lseek")]
    private static extern long LSeek(int fd, long offset, int whence);
}
