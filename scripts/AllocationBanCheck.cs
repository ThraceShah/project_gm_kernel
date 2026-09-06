#!/usr/bin/env dotnet run
#:property PublishAot=false
#:property PublishTrimmed=false
#:property EnableTrimAnalyzer=false

// Inspect actual emitted IL, not source regexes: value-type constructors are
// not heap allocations; compiler-generated closures, boxing and newarr are.
// This is a verification guard, not a proof about the entire BCL call graph.
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

var scriptDirectory = Path.GetDirectoryName(ScriptPath())!;
var assemblyPath = Path.GetFullPath(Path.Combine(scriptDirectory,
    args.Length == 0 ? "../src/ProjectGmKernel.Native/bin/Release/net10.0/ProjectGmKernel.Native.dll" : args[0]));
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dependency = Path.Combine(Path.GetDirectoryName(assemblyPath)!, name.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(c => unchecked((ushort)c.Value));

// The user-approved managed XT boundary, expressed as exact type names.
var adapters = new HashSet<string>(StringComparer.Ordinal)
{
    "XtAssociationTable", "XtReader", "XtWriter", "XtSchemaRegistry", "XtSchemaRegistration",
    "XtCorpusInspection", "XtText", "XtPartGraph", "XtSchemaLayout", "XtFields", "XtSchemaRegistryCache",
    "XtCorpusSchemaNode", "XtCorpusSchemaDependency", "XtCorpusInventory", "XtCorpusSupportedSchema",
};

// Exact symbols only. No Runtime/Geometry file or namespace exemption.
var exceptions = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["KernelRuntime.PartTransmitBImplementation"] = "managed XT encoding boundary",
    ["KernelRuntime.PartReceiveBImplementation"] = "managed XT decoding boundary",
    ["KernelRuntime.TryEncodeReceivedParts"] = "managed XT conversion boundary",
    ["KernelRuntime.AttachReceivedXt"] = "managed XT association boundary",
    ["MemoryStatistics.ToString"] = "generated diagnostic formatting, outside operation paths",
    ["MemoryStatistics.PrintMembers"] = "generated diagnostic formatting, outside operation paths",
    ["BlockStatistics.ToString"] = "generated diagnostic formatting, outside operation paths",
    ["BlockStatistics.PrintMembers"] = "generated diagnostic formatting, outside operation paths",
};

var violations = new List<string>();
var methodCount = 0;
foreach (var type in assembly.GetTypes())
{
    if (type.Namespace is not { } ns || !(ns.StartsWith("ProjectGmKernel.Native.Runtime", StringComparison.Ordinal)
        || ns.StartsWith("ProjectGmKernel.Native.Geometry", StringComparison.Ordinal)
        || ns.StartsWith("ProjectGmKernel.Native.Computation", StringComparison.Ordinal))) continue;
    var outer = type;
    while (outer.DeclaringType != null) outer = outer.DeclaringType;
    if (adapters.Contains(outer.Name)) continue;
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    if (type.TypeInitializer is { } initializer) methods = methods.Append(initializer);
    foreach (var method in methods)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (il == null) continue;
        methodCount++;
        var symbol = type.Name + "." + method.Name;
        if (exceptions.ContainsKey(symbol)) continue;
        for (var position = 0; position < il.Length;)
        {
            var offset = position;
            ushort value = il[position++];
            if (value == 0xfe) value = (ushort)(0xfe00 | il[position++]);
            var code = codes[value];
            if (code == OpCodes.Newarr || code == OpCodes.Box)
                Report(code.Name!);
            if (code == OpCodes.Newobj || code == OpCodes.Call || code == OpCodes.Callvirt)
            {
                var token = BitConverter.ToInt32(il, position);
                var target = method.Module.ResolveMethod(token, type.IsGenericType ? type.GetGenericArguments() : null,
                    method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (code == OpCodes.Newobj && target?.DeclaringType?.IsValueType == false)
                    Report("new " + target.DeclaringType.FullName);
                if (target?.DeclaringType == typeof(string) && target.Name is "Concat" or "Format" or "Create")
                    Report("string." + target.Name);
                if (target?.DeclaringType?.FullName == "System.Linq.Enumerable"
                    && target.Name is "ToArray" or "ToList" or "ToDictionary" or "Select" or "Where" or "OrderBy")
                    Report("LINQ." + target.Name);
                if (target?.DeclaringType?.FullName == "System.Runtime.InteropServices.NativeMemory"
                    && target.Name is "Alloc" or "AllocZeroed" or "AlignedAlloc" or "Realloc" or "AlignedRealloc"
                    && type.Name != "SessionMemory")
                    Report("system allocation outside SessionMemory");
            }
            position += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, position) * 4,
                _ => 4,
            };
            void Report(string reason)
            {
                if (symbol == "KernelRuntime..cctor" && reason is "new System.Object"
                    or "new ProjectGmKernel.Native.Runtime.SessionDispatchState"
                    or "new ProjectGmKernel.Native.Runtime.XtAssociationTable"
                    or "system allocation outside SessionMemory") return;
                violations.Add($"{type.FullName}.{method.Name} IL_{offset:x4}: {reason}");
            }
        }
    }
}
Console.WriteLine($"allocation IL guard: checked {methodCount} methods");
foreach (var violation in violations) Console.WriteLine("VIOLATION " + violation);
Console.WriteLine(violations.Count == 0 ? "allocation IL guard: CLEAN" : $"allocation IL guard: {violations.Count} violations");
return violations.Count == 0 ? 0 : 1;

static string ScriptPath([CallerFilePath] string path = "") => path;
