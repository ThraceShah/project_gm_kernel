#!/usr/bin/env dotnet run
#:property UseNativeRuntimePolicy=true
using System.Diagnostics;
using System.Runtime.CompilerServices;

if(args.Length==0)throw new ArgumentException("Oracle file name followed by its arguments");
var scripts=Path.GetDirectoryName(Source())!;var name=args[0];
if(Path.GetFileName(name)!=name||!name.EndsWith(".cs",StringComparison.Ordinal)||!File.Exists(Path.Combine(scripts,name)))throw new ArgumentException("One existing C# oracle in scripts");
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.GetFullPath(Path.Combine(scripts,"..")),UseShellExecute=false};
NativeRuntimePolicy.Prepare(start);
foreach(var value in new[]{"run",Path.Combine(scripts,name),"-c","Release","--"})start.ArgumentList.Add(value);
foreach(var value in args[1..])start.ArgumentList.Add(value);
using var child=Process.Start(start)??throw new InvalidOperationException("Start oracle");await child.WaitForExitAsync();Environment.ExitCode=child.ExitCode;
static string Source([CallerFilePath]string file="")=>file;
