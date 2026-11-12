#!/usr/bin/env dotnet run
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

if(args.Length!=2||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("new snapshot name and portal-relative resource required");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));var output=Path.Combine(root,"temp_docs","query-numerics-closure",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Online snapshots are immutable");Directory.CreateDirectory(output);
var portal=new Uri("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/");var uri=new Uri(portal,args[1]);
if(uri.Host!=portal.Host||!uri.AbsolutePath.StartsWith(portal.AbsolutePath,StringComparison.Ordinal))throw new ArgumentException("Read only the existing GM portal");
using var handler=new HttpClientHandler{UseProxy=false};
using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(30)};using var response=await client.GetAsync(uri);var bytes=await response.Content.ReadAsByteArrayAsync();
File.WriteAllBytes(Path.Combine(output,"response.json"),bytes);
File.WriteAllText(Path.Combine(output,"receipt.json"),new JsonObject{["url"]=uri.ToString(),["utc"]=DateTimeOffset.UtcNow.ToString("O"),["httpStatus"]=(int)response.StatusCode,
    ["bytes"]=bytes.Length,["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)),["sourceSha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Source())))}.ToJsonString());
response.EnsureSuccessStatusCode();Console.WriteLine($"ONLINE_READ status={(int)response.StatusCode} bytes={bytes.Length} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
static string Source([CallerFilePath]string path="")=>path;
