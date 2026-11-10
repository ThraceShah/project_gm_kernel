using System.Net;
using System.Text;
using GmProgressHost;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// GmProgressHost: read-only product host for the GM Kernel progress workbench.
//
// It serves exactly two safe public roots and nothing else:
//   1. the currently activated immutable UI release (site/ui-releases/<id>), and
//   2. the validated data cache root (absolute path recorded in the activation
//      pointer) whose runs/ tree carries all mesh/evidence assets, including
//      cross-run references between measurement runs.
//
// There is no directory listing, no write/upload/API endpoint and no control
// surface: activation is performed by scripts/ActivateHostedWebProgressSite.cs
// which atomically replaces the site.json pointer. Only GET and HEAD are
// accepted. Request paths are restricted to a strict character allowlist and
// every path component is re-checked against symlink escapes, so a validated
// root is never blindly trusted. Streaming is delegated to the mature
// ASP.NET Core static file middleware (kernel send-file, no full-file loads).
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

// Content types: mature provider defaults plus the two project-specific text
// types. Unknown extensions are never served (ServeUnknownFileTypes stays off).
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".log"] = "text/plain; charset=utf-8";
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

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

// 3. Path guard: strict allowlist, no dot segments / empty segments / encoded
//    leftovers, plus per-component symlink escape rejection against the root
//    that would actually serve the request.
app.Use(async (context, next) =>
{
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
    var root = isData ? roots.DataRoot : roots.UiRoot;
    var relative = isData ? probe["/data".Length..].TrimStart('/') : probe.TrimStart('/');
    if (!SafePaths.TryResolve(root, relative, out _))
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

// 5. Data root at /data: isolated branch, never falls through into the UI
//    release, no directory index for /data itself.
app.Map("/data", dataBranch =>
{
    dataBranch.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new SwitchableFileProvider(() => state.Current()?.DataProvider),
        ContentTypeProvider = contentTypes,
        ServeUnknownFileTypes = false,
        OnPrepareResponse = CacheControlFor(isData: true),
    });
    dataBranch.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await WriteTextAsync(context, "Not Found\n");
    });
});

// 6. UI release at "/": index.html for the root, static files elsewhere.
var uiProvider = new SwitchableFileProvider(() => state.Current()?.UiProvider);
var defaultFiles = new DefaultFilesOptions { FileProvider = uiProvider };
defaultFiles.DefaultFileNames.Clear();
defaultFiles.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFiles);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = uiProvider,
    ContentTypeProvider = contentTypes,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = CacheControlFor(isData: false),
});
app.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    await WriteTextAsync(context, "Not Found\n");
});

app.Run();
return 0;

static Action<StaticFileResponseContext> CacheControlFor(bool isData) => context =>
{
    // index.json / manifest.json / index.html must always revalidate so a new
    // activation is picked up immediately; immutable run assets and hashed UI
    // build assets are content-stable and cache forever.
    var name = context.File.Name;
    var cache = name is "index.html" or "index.json" or "manifest.json" or "collection-manifest.json"
        ? "no-cache"
        : isData || context.Context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "public, max-age=300";
    context.Context.Response.Headers.CacheControl = cache;
};

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
