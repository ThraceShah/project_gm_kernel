#!/usr/bin/env dotnet

using System.Diagnostics;
using System.Runtime.CompilerServices;

var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
Run("dotnet",["pack",Path.Combine(root,"src","ProjectGmKernel.Xt","ProjectGmKernel.Xt.csproj"),"-c","Release"]);
var directory=Path.Combine(root,"bin","xt-nuget-consumer");Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory,"NuGet.Config"),$"<configuration><packageSources><clear/><add key=\"local\" value=\"{Path.Combine(root,"bin","nuget")}\"/></packageSources></configuration>");
File.WriteAllText(Path.Combine(directory,"Consumer.csproj"),"""
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="ProjectGmKernel.Xt" Version="0.1.0" /></ItemGroup></Project>
""");
File.WriteAllText(Path.Combine(directory,"Program.cs"),"""
using ProjectGmKernel.Xt;
if (typeof(XtSchemaCatalog).Assembly.GetReferencedAssemblies().Any(a => a.Name is "ProjectGmKernel.Native" or "PskernelSharp")) return 1;
Console.WriteLine(typeof(XtCodec).Assembly.FullName);
return 0;
""");
Run("dotnet",["restore",Path.Combine(directory,"Consumer.csproj"),"--configfile",Path.Combine(directory,"NuGet.Config")]);
Run("dotnet",["run","--project",Path.Combine(directory,"Consumer.csproj"),"--no-restore"]);
var deps=File.ReadAllText(Path.Combine(directory,"bin","Debug","net10.0","Consumer.deps.json"));if(deps.Contains("ProjectGmKernel.Native",StringComparison.Ordinal)||deps.Contains("PskernelSharp",StringComparison.Ordinal)||deps.Contains("pskernel",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Independent consumer dependency graph contains a forbidden kernel dependency.");
Console.WriteLine("Independent NuGet consumer verification passed.");
return;
static void Run(string file,IEnumerable<string> arguments){var start=new ProcessStartInfo(file){UseShellExecute=false};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)??throw new InvalidOperationException($"Cannot start {file}.");process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException($"{file} exited with {process.ExitCode}.");}
static string GetScriptPath([CallerFilePath]string path="")=>path;
