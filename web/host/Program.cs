using System.Net;
using System.Text;
using GmProgressHost;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;

// GmProgressHost: read-only product host for the GM Kernel progress workbench.
//
// It serves exactly two safe public roots and nothing else:
//   1. the currently activated immutable UI release (site/ui-releases/<id>), and
//   2. the validated data root recorded in the activation pointer, whose runs/
//      tree carries all mesh/evidence assets, including cross-run references.
//
// There is no directory listing, no write/upload/API endpoint and no control
// surface: activation is performed by scripts/ActivateHostedWebProgressSite.cs
// which atomically replaces the site.json pointer. Only GET and HEAD are
// accepted. Request paths are restricted to a strict character allowlist
// (raw-target level included), every path component from the filesystem root
// down to the final file is re-checked against symlink escapes, and the file
// open uses the checked object (O_NOFOLLOW leaf, post-open chain re-verify),
// so a validated root is never blindly trusted. Streaming uses kernel
// send-file from the opened handle (no full-file loads).
//
// Usage:
//   dotnet run --project web/host -- --site <siteRoot> --listen <ip:port> [--listen <ip:port>]
// Wildcard binds (0.0.0.0 / ::) are refused: production binds the node's
// Tailscale IP plus a loopback address for health probing.

var siteOption = Option("--site");
var listens = Options("--listen");
if (siteOption is null || listens.Count == 0)
{
    Console.Error.WriteLine("Usage: GmProgressHost --site <siteRoot> --listen <ip:port> [--listen <ip:port>]");
    return 2;
}
var siteRoot = Path.GetFullPath(siteOption);
if (!Directory.Exists(siteRoot))
{
    Console.Error.WriteLine("GmProgressHost: site root does not exist: " + siteRoot);
    return 2;
}
var endpoints = new List<(IPAddress Address, int Port)>();
foreach (var listen in listens)
{
    var separator = listen.LastIndexOf(':');
    if (separator <= 0 || !int.TryParse(listen[(separator + 1)..], out var port) || port is < 1 or > 65535)
    {
        Console.Error.WriteLine("GmProgressHost: invalid --listen value: " + listen);
        return 2;
    }
    if (!IPAddress.TryParse(listen[..separator], out var address))
    {
        Console.Error.WriteLine("GmProgressHost: --listen requires a literal IP address: " + listen);
        return 2;
    }
    if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
    {
        Console.Error.WriteLine("GmProgressHost: wildcard binds are refused: " + listen);
        return 2;
    }
    endpoints.Add((address, port));
}

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    foreach (var (address, port) in endpoints) kestrel.Listen(address, port);
});

var app = builder.Build();
var state = new SiteState(siteRoot);

// 1. Security headers on every response, including errors.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(static state =>
    {
        var response = (HttpResponse)state!;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        return Task.CompletedTask;
    }, context.Response);
    await next(context);
});

// 2. Read-only verb gate: GET and HEAD only.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
    {
        await next(context);
        return;
    }
    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
    context.Response.Headers.Allow = "GET, HEAD";
    await WriteTextAsync(context, "Method Not Allowed\n");
});

// 3. Path guard: strict allowlist on the RAW request target (before server
//    normalization can collapse encoded dot segments) and on the decoded path:
//    no percent-encoding, dot segments, empty segments, backslashes or control
//    characters anywhere.
app.Use(async (context, next) =>
{
    var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? context.Request.Path.Value ?? "/";
    if (!SafePaths.IsSafeRawTarget(rawTarget, out _))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await WriteTextAsync(context, "Bad Request\n");
        return;
    }
    var path = context.Request.Path.Value ?? "/";
    var probe = path == "/" ? "/index.html" : path;
    if (!SafePaths.IsSafeRequestPath(probe))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await WriteTextAsync(context, "Bad Request\n");
        return;
    }
    if (probe == "/healthz")
    {
        await next(context);
        return;
    }
    var roots = state.Current();
    if (roots is null)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await WriteTextAsync(context, "No active site\n");
        return;
    }
    var isData = probe == "/data" || probe.StartsWith("/data/", StringComparison.Ordinal);
    if (isData)
    {
        // Data resolution is authoritative in the /data branch below: it maps
        // through the active generation's run hashes into the object store.
        await next(context);
        return;
    }
    var relative = probe.TrimStart('/');
    if (relative.Length == 0 || !SafePaths.TryResolve(roots.UiRoot, relative, out _))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await WriteTextAsync(context, "Not Found\n");
        return;
    }
    await next(context);
});

// 4. Health probe for deployment/portal checks: minimal, no site internals.
app.Map("/healthz", health =>
{
    health.Run(async context =>
    {
        var roots = state.Current();
        context.Response.StatusCode = roots is null ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
        context.Response.ContentType = "text/plain; charset=utf-8";
        if (roots is not null)
        {
            context.Response.Headers["X-Gm-Ui-Release"] = roots.UiRelease;
            context.Response.Headers["X-Gm-Data-Run"] = roots.DataRunId;
        }
        await WriteTextAsync(context, roots is null ? "no active site\n" : "ok\n");
    });
});

// 5. Data root at /data: the exact activated generation (index snapshot) plus
//    the content-addressed store for run files; isolated branch that never
//    falls through into the UI release and never serves directory indexes.
app.Map("/data", dataBranch =>
{
    dataBranch.Run(async context =>
    {
        var roots = state.Current();
        if (roots is null) { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; await WriteTextAsync(context, "No active site\n"); return; }
        var relative = (context.Request.Path.Value ?? "").TrimStart('/');
        if (relative == "index.json")
        {
            if (await SafeFiles.ServeAsync(context, roots.GenerationDir, "index.json", "no-cache")) return;
        }
        else if (relative.StartsWith("runs/", StringComparison.Ordinal))
        {
            var rest = relative["runs/".Length..];
            var slash = rest.IndexOf('/');
            if (slash > 0 && slash < rest.Length - 1)
            {
                var runId = rest[..slash];
                var file = rest[(slash + 1)..];
                if (SafePaths.IsSafeRequestPath("/" + runId + "/" + file)
                    && roots.Store.TryResolveRunFile(runId, file, out var objectPath))
                {
                    // Hashed run assets are immutable per generation; the two
                    // manifest views may evolve and always revalidate.
                    var immutable = Path.GetFileName(file) is not ("manifest.json" or "collection-manifest.json" or "index.json");
                    var cache = immutable ? "public, max-age=31536000, immutable" : "no-cache";
                    if (await SafeFiles.ServeAsync(context, Path.GetDirectoryName(objectPath)!, Path.GetFileName(objectPath), cache, file)) return;
                }
            }
        }
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await WriteTextAsync(context, "Not Found\n");
    });
});

// 6. UI release at "/": index.html for the root, static files elsewhere.
app.Run(async context =>
{
    var roots = state.Current();
    if (roots is null) { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; await WriteTextAsync(context, "No active site\n"); return; }
    var path = context.Request.Path.Value ?? "/";
    var relative = path == "/" ? "index.html" : path.TrimStart('/');
    if (!await SafeFiles.ServeAsync(context, roots.UiRoot, relative, CacheControl(isData: false, path, Path.GetFileName(relative))))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await WriteTextAsync(context, "Not Found\n");
    }
});

app.Run();
return 0;

static string CacheControl(bool isData, string requestPath, string fileName)
{
    // index.json / manifest.json / index.html must always revalidate so a new
    // activation is picked up immediately; immutable run assets and hashed UI
    // build assets are content-stable and cache forever.
    return fileName is "index.html" or "index.json" or "manifest.json" or "collection-manifest.json"
        ? "no-cache"
        : isData || requestPath.StartsWith("/assets/")
            ? "public, max-age=31536000, immutable"
            : "public, max-age=300";
}

static Task WriteTextAsync(HttpContext context, string text)
{
    var bytes = Encoding.ASCII.GetBytes(text);
    context.Response.ContentLength = bytes.Length;
    return HttpMethods.IsHead(context.Request.Method) ? Task.CompletedTask : context.Response.Body.WriteAsync(bytes).AsTask();
}

string? Option(string name)
{
    var at = Array.IndexOf(args, name);
    return at < 0 || at + 1 >= args.Length ? null : args[at + 1];
}

List<string> Options(string name)
{
    var values = new List<string>();
    for (var at = 0; at < args.Length; at++) if (args[at] == name && at + 1 < args.Length) values.Add(args[at + 1]);
    return values;
}
