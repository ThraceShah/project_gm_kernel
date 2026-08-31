#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ProjectGmKernel.Xt;

#pragma warning disable IL2026, IL3050 // Verification script intentionally reflects generated blittable types.

var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));var work=Path.Combine(root,"bin","xt-generated-layouts");Directory.CreateDirectory(work);
var assembly=typeof(XtCodec).Assembly;var typeCount=0;var fieldCount=0;
foreach(var identity in XtBuiltInSchemas.Identities.Order(StringComparer.Ordinal))
{
    var sourcePath=Path.Combine(work,"verify-"+identity+".c");var executable=Path.Combine(work,"verify-"+identity);var header=Path.Combine(root,"src","ProjectGmKernel.Xt.Native","include","ProjectGmKernel.Xt."+identity+".h").Replace("\\","/",StringComparison.Ordinal);
    var source=new StringBuilder().Append("#include <stddef.h>\n#include \"").Append(header).AppendLine("\"");
    var schema=XtBuiltInSchemas.Resolve(identity);var ns="ProjectGmKernel.Xt.Schema."+identity+".";
    foreach(var node in schema.Nodes)
    {
        var managed=assembly.GetType(ns+node.Name,throwOnError:true)!;var cType="PGM_XT_"+node.Name+"_t";
        source.Append("_Static_assert(sizeof(").Append(cType).Append(")==").Append(Marshal.SizeOf(managed)).Append(",\"").Append(cType).AppendLine(" size\");");
        AssertOffset("_xt_index");
        AssertOffset("_xt_order");
        if(node.Variable)AssertOffset("_xt_variable_length");
        if(node.Transmit)AssertOffset("_xt_user_fields");
        foreach(var field in schema.GetFields(node))AssertOffset(field.Name);
        typeCount++;
        void AssertOffset(string field){source.Append("_Static_assert(offsetof(").Append(cType).Append(',').Append(field).Append(")==").Append(Marshal.OffsetOf(managed,field).ToInt64()).Append(",\"").Append(cType).Append('.').Append(field).AppendLine(" offset\");");fieldCount++;}
    }
    source.AppendLine("int main(void){return 0;}");File.WriteAllText(sourcePath,source.ToString(),new UTF8Encoding(false));Run("cc",["-std=c11","-Wall","-Werror",sourcePath,"-o",executable]);Run(executable,[]);
    if(identity is "SCH_3000119_30000" or "SCH_3701097_37102")
    {
        var cppSource=Path.Combine(work,"verify-"+identity+".cpp");var cppObject=Path.Combine(work,"verify-"+identity+".o");File.WriteAllText(cppSource,"#include \""+header+"\"\nint main(){return 0;}\n",new UTF8Encoding(false));Run("c++",["-std=c++17","-Wall","-Werror","-c",cppSource,"-o",cppObject]);
    }
}
var identities=XtBuiltInSchemas.Identities.Order(StringComparer.Ordinal).Take(2).ToArray();var conflictSource=Path.Combine(work,"verify-conflicting-headers.c");var conflictObject=Path.Combine(work,"verify-conflicting-headers.o");File.WriteAllText(conflictSource,"#include \""+Header(identities[0])+"\"\n#include \""+Header(identities[1])+"\"\n",new UTF8Encoding(false));RunExpectFailure("cc",["-std=c11","-fmax-errors=1","-c",conflictSource,"-o",conflictObject],"Only one ProjectGmKernel.Xt schema header");
Console.WriteLine($"Verified generated C/C# layout for {typeCount} schema node types and {fieldCount} fields across {XtBuiltInSchemas.Identities.Count} isolated headers.");
return;
string Header(string identity)=>Path.Combine(root,"src","ProjectGmKernel.Xt.Native","include","ProjectGmKernel.Xt."+identity+".h").Replace("\\","/",StringComparison.Ordinal);
static void Run(string fileName,string[] arguments){var start=new ProcessStartInfo(fileName){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)??throw new InvalidOperationException($"Cannot start {fileName}.");var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException($"{fileName} failed: {stdout}{stderr}");}
static void RunExpectFailure(string fileName,string[] arguments,string marker){var start=new ProcessStartInfo(fileName){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)??throw new InvalidOperationException($"Cannot start {fileName}.");var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode==0||!stderr.Contains(marker,StringComparison.Ordinal))throw new InvalidOperationException($"{fileName} did not reject conflicting schema headers as expected: {stdout}{stderr}");}
static string GetScriptPath([CallerFilePath]string path="")=>path;
#pragma warning restore IL2026, IL3050
