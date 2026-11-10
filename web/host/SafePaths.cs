namespace GmProgressHost;

/// <summary>
/// Request-path safety for the two read-only public roots. A request path is
/// only ever resolved against the currently active root and only when every
/// character is in the strict public-file allowlist (letters, digits, dot,
/// underscore, hyphen, slash). Dot segments, empty segments, backslashes,
/// percent signs (encoded dots / double-encoding leftovers), colons, control
/// characters and non-ASCII names are rejected outright.
///
/// The resolution walk covers the ENTIRE chain from the filesystem root: the
/// configured root itself, all of its parent components, every intermediate
/// directory and the final file must not be symlinks. Replacing a serving root
/// (or any parent) with a link into another tree therefore cannot change the
/// serviceable scope: the walk rejects it on the next request. See
/// docs/web-progress-hosting.md for the residual check-to-open race policy.
/// </summary>
internal static class SafePaths
{
    /// <summary>True when the request path only contains allowlisted characters and safe segments.</summary>
    internal static bool IsSafeRequestPath(string path)
    {
        if (path.Length == 0) return true; // root
        var start = path[0] == '/' ? 1 : 0;
        if (start == path.Length) return path[0] == '/';
        var segmentStart = start;
        for (var i = start; i <= path.Length; i++)
        {
            var end = i == path.Length;
            var c = end ? '/' : path[i];
            if (!end && !IsAllowedChar(c)) return false;
            if (end || c == '/')
            {
                var length = i - segmentStart;
                if (length == 0) return false; // empty segment: "//" or trailing "/"
                if (length == 1 && path[segmentStart] == '.') return false;
                if (length == 2 && path[segmentStart] == '.' && path[segmentStart + 1] == '.') return false;
                segmentStart = i + 1;
            }
        }
        return true;
    }

    /// <summary>
    /// Validates the raw request target exactly as received (before server-side
    /// normalization can collapse encoded dot segments) and returns the path
    /// portion. Percent signs, backslashes, semicolons and control characters
    /// are rejected: public file names never require percent-encoding, so any
    /// encoding attempt is invalid. Absolute-form targets keep their path.
    /// </summary>
    internal static bool IsSafeRawTarget(string rawTarget, out string path)
    {
        path = rawTarget;
        var query = path.IndexOf('?');
        if (query >= 0) path = path[..query];
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var slash = path.IndexOf('/', path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? 8 : 7);
            if (slash < 0) { path = "/"; return false; }
            path = path[slash..];
        }
        foreach (var c in path)
        {
            if (c is '%' or '\\' or ';' or '#' || c < 0x20 || c == 0x7f) return false;
        }
        return IsSafeRequestPath(path);
    }

    private static bool IsAllowedChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
        || c is '.' or '_' or '-' or '/';

    /// <summary>
    /// Resolves <paramref name="relative"/> under <paramref name="root"/> and
    /// returns the physical path only when it stays lexically inside the root
    /// and no path component anywhere from the filesystem root down to the
    /// final file is a reparse point / symlink (the root itself and all parents
    /// included). Returns false for anything that could escape the root.
    /// </summary>
    internal static bool TryResolve(string root, string relative, out string physical)
    {
        physical = string.Empty;
        var combined = Path.GetFullPath(relative, root);
        var prefix = root[^1] == Path.DirectorySeparatorChar ? root : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.Ordinal)) return false;
        if (!IsChainSafe(combined)) return false;
        physical = combined;
        return true;
    }

    /// <summary>True when no component of the path (from "/" inclusive) is a symlink.</summary>
    internal static bool IsChainSafe(string path)
    {
        var full = Path.GetFullPath(path);
        for (var i = 1; i <= full.Length; i++)
        {
            if (i == full.Length || full[i] == Path.DirectorySeparatorChar)
            {
                var segmentPath = full[..i];
                if ((File.Exists(segmentPath) || Directory.Exists(segmentPath))
                    && (File.GetAttributes(segmentPath) & FileAttributes.ReparsePoint) != 0) return false;
            }
        }
        return true;
    }
}
