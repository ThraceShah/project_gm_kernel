namespace GmProgressHost;

/// <summary>
/// Request-path safety for the two read-only public roots. A request path is
/// only ever resolved against the currently active root and only when every
/// character is in the strict public-file allowlist (letters, digits, dot,
/// underscore, hyphen, slash). Dot segments, empty segments, backslashes,
/// percent signs (double-encoding leftovers), colons, control characters and
/// non-ASCII names are rejected outright. After lexical containment the
/// resolved path is re-checked component by component so that a symlink that
/// escapes the safe root can never be served, even if one is planted into a
/// previously validated tree ("validated" is not treated as "trusted").
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

    private static bool IsAllowedChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
        || c is '.' or '_' or '-' or '/';

    /// <summary>
    /// Resolves <paramref name="relative"/> under <paramref name="root"/> and
    /// returns the physical path only when it stays lexically inside the root
    /// and no path component (including the final file) is a reparse point /
    /// symlink. Returns false for anything that could escape the root.
    /// </summary>
    internal static bool TryResolve(string root, string relative, out string physical)
    {
        physical = string.Empty;
        var combined = Path.GetFullPath(relative, root);
        var prefix = root[^1] == Path.DirectorySeparatorChar ? root : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.Ordinal)) return false;
        // Per-component reparse point walk: nothing below the root may be a
        // link, so a link planted after validation cannot escape either.
        var current = root.Length + 1;
        while (current <= combined.Length)
        {
            if (current == combined.Length || combined[current] == Path.DirectorySeparatorChar)
            {
                var segmentPath = combined[..current];
                if (File.Exists(segmentPath) || Directory.Exists(segmentPath))
                {
                    if ((File.GetAttributes(segmentPath) & FileAttributes.ReparsePoint) != 0) return false;
                }
                current++;
            }
            else current++;
        }
        physical = combined;
        return true;
    }
}
