#!/usr/bin/env dotnet run
#:package ClangSharp.Interop@18.1.0
#:package libclang@21.1.8
#:property AllowUnsafeBlocks=true
// Parse Parasolid C headers via libclang and generate the complete C# API layer.
//
// Generates:
//   src/ProjectGmKernel.Native/Generated/ParasolidHeader.generated.cs   (ABI types/constants)
//   src/ProjectGmKernel.Native/Generated/KernelExports.generated.cs     (export stubs)
//   temp_docs/unresolved.md                                              (generation diagnostics)
//
// Usage: dotnet run scripts/GenerateApiLayer.cs -p:AllowUnsafeBlocks=true [-- --allow-partial]

using ClangSharp.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// ─────────────────────────────────────────────────────────────────────────────
// Command-line args
// ─────────────────────────────────────────────────────────────────────────────

var cmdLineArgs = Environment.GetCommandLineArgs();
var allowPartial = Array.Exists(cmdLineArgs, a => a == "--allow-partial");

// ─────────────────────────────────────────────────────────────────────────────
// Paths (relative to script file)
// ─────────────────────────────────────────────────────────────────────────────

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var incDir = Path.Combine(repoRoot, "docs", "parasolid_inc");
var nativeOut = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "Generated", "ParasolidHeader.generated.cs");
var exportsOut = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "Generated", "KernelExports.generated.cs");
var unresolvedPath = Path.Combine(repoRoot, "temp_docs", "unresolved.md");
var abiCheckDir = Path.Combine(repoRoot, "temp_docs", "abi_check");

var mainHeader = Path.Combine(incDir, "parasolid_kernel.h");
string[] tokenHeaders = [
    Path.Combine(incDir, "parasolid_tokens.h"),
    Path.Combine(incDir, "parasolid_ifails.h"),
    Path.Combine(incDir, "frustrum_tokens.h"),
    Path.Combine(incDir, "frustrum_ifails.h"),
];

static string FindSysroot()
{
    if (!OperatingSystem.IsMacOS())
        return "";

    var psi = new System.Diagnostics.ProcessStartInfo("xcrun", "--show-sdk-path")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    var proc = System.Diagnostics.Process.Start(psi)!;
    proc.WaitForExit();
    return proc.StandardOutput.ReadToEnd().Trim();
}
var sysroot = FindSysroot();

// ─────────────────────────────────────────────────────────────────────────────
// Cursor kind & type kind constants (from libclang C API)
// ─────────────────────────────────────────────────────────────────────────────

const CXCursorKind CXCursor_StructDecl = CXCursorKind.CXCursor_StructDecl;
const CXCursorKind CXCursor_UnionDecl = CXCursorKind.CXCursor_UnionDecl;
const CXCursorKind CXCursor_FieldDecl = CXCursorKind.CXCursor_FieldDecl;
const CXCursorKind CXCursor_FunctionDecl = CXCursorKind.CXCursor_FunctionDecl;
const CXCursorKind CXCursor_ParmDecl = CXCursorKind.CXCursor_ParmDecl;
const CXCursorKind CXCursor_TypedefDecl = CXCursorKind.CXCursor_TypedefDecl;

const CXTypeKind CXType_Void = CXTypeKind.CXType_Void;
const CXTypeKind CXType_Bool = CXTypeKind.CXType_Bool;
const CXTypeKind CXType_Char_U = CXTypeKind.CXType_Char_U;
const CXTypeKind CXType_UChar = CXTypeKind.CXType_UChar;
const CXTypeKind CXType_UShort = CXTypeKind.CXType_UShort;
const CXTypeKind CXType_UInt = CXTypeKind.CXType_UInt;
const CXTypeKind CXType_ULong = CXTypeKind.CXType_ULong;
const CXTypeKind CXType_ULongLong = CXTypeKind.CXType_ULongLong;
const CXTypeKind CXType_Char_S = CXTypeKind.CXType_Char_S;
const CXTypeKind CXType_SChar = CXTypeKind.CXType_SChar;
const CXTypeKind CXType_Short = CXTypeKind.CXType_Short;
const CXTypeKind CXType_Int = CXTypeKind.CXType_Int;
const CXTypeKind CXType_Long = CXTypeKind.CXType_Long;
const CXTypeKind CXType_LongLong = CXTypeKind.CXType_LongLong;
const CXTypeKind CXType_Float = CXTypeKind.CXType_Float;
const CXTypeKind CXType_Double = CXTypeKind.CXType_Double;
const CXTypeKind CXType_LongDouble = CXTypeKind.CXType_LongDouble;
const CXTypeKind CXType_Pointer = CXTypeKind.CXType_Pointer;
const CXTypeKind CXType_Record = CXTypeKind.CXType_Record;
const CXTypeKind CXType_Enum = CXTypeKind.CXType_Enum;
const CXTypeKind CXType_Typedef = CXTypeKind.CXType_Typedef;
const CXTypeKind CXType_FunctionNoProto = CXTypeKind.CXType_FunctionNoProto;
const CXTypeKind CXType_FunctionProto = CXTypeKind.CXType_FunctionProto;
const CXTypeKind CXType_ConstantArray = CXTypeKind.CXType_ConstantArray;
const CXTypeKind CXType_IncompleteArray = CXTypeKind.CXType_IncompleteArray;
const CXTypeKind CXType_Elaborated = CXTypeKind.CXType_Elaborated;

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static unsafe string ToManagedString(CXString cx)
{
    var ptr = clang.getCString(cx);
    var result = ptr == null ? "" : Marshal.PtrToStringAnsi((nint)ptr) ?? "";
    clang.disposeString(cx);
    return result;
}

static string GetSpelling(CXCursor cursor) => ToManagedString(clang.getCursorSpelling(cursor));

static string GetTypeSpelling(CXType type)
{
    var s = ToManagedString(clang.getTypeSpelling(type));
    s = Regex.Replace(s, @"\bconst\b\s*", "");
    s = s.Replace("*[]", "*");
    return s;
}

static string StripAggregateKeyword(string typeName)
{
    typeName = typeName.Trim();
    if (typeName.StartsWith("struct "))
        return typeName[7..];
    if (typeName.StartsWith("union "))
        return typeName[6..];
    return typeName;
}

static string NormalizeTypeName(string typeName)
{
    if (string.IsNullOrWhiteSpace(typeName))
        return typeName;

    typeName = Regex.Replace(typeName.Trim(), @"\s+", " ");
    typeName = Regex.Replace(typeName, @"\s*\*\s*", "*");
    return StripAggregateKeyword(typeName);
}

static int CountPointerDepth(string typeName)
{
    var count = 0;
    for (var i = typeName.Length - 1; i >= 0 && typeName[i] == '*'; i--)
        count++;
    return count;
}

static string SanitizeName(string name)
{
    string[] keywords = [
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else",
        "enum","event","explicit","extern","false","finally","fixed","float","for",
        "foreach","goto","if","implicit","in","int","interface","internal","is","lock",
        "long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed","short",
        "sizeof","stackalloc","static","string","struct","switch","this","throw","true",
        "try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual",
        "void","volatile","while",
    ];
    return Array.Exists(keywords, k => k == name) ? $"@{name}" : name;
}

static int StructSortKeyByName(string name)
{
    if (name.Contains("_array_t") || name.Contains("_array_s")) return 0;
    if (name.Contains("_sf_t") || name.Contains("_sf_s")) return 1;
    if (name.Contains("_o_t") || name.Contains("_o_s")) return 2;
    if (name.Contains("_r_t") || name.Contains("_r_s")) return 3;
    return 4;
}

static string NormalizeReportText(string text, string repoRoot)
{
    return text.Replace(repoRoot + Path.DirectorySeparatorChar, "")
               .Replace(repoRoot + Path.AltDirectorySeparatorChar, "");
}

static void PreloadLibClang()
{
    var env = Environment.GetEnvironmentVariable("LIBCLANG_PATH");
    if (!string.IsNullOrWhiteSpace(env))
    {
        if (Directory.Exists(env))
        {
            foreach (var fileName in CandidateFileNames())
            {
                var candidate = Path.Combine(env, fileName);
                if (NativeLibrary.TryLoad(candidate, out _))
                    return;
            }
        }
        else if (NativeLibrary.TryLoad(env, out _))
        {
            return;
        }
    }

    foreach (var candidate in CandidateLibraryPaths())
    {
        if (NativeLibrary.TryLoad(candidate, out _))
            return;
    }
}

static IEnumerable<string> CandidateFileNames()
{
    if (OperatingSystem.IsMacOS())
        return ["libclang.dylib"];
    if (OperatingSystem.IsWindows())
        return ["libclang.dll", "clang.dll"];
    return ["libclang.so", "libclang-21.so", "libclang-20.so", "libclang-18.so"];
}

static IEnumerable<string> CandidateLibraryPaths()
{
    if (OperatingSystem.IsMacOS())
    {
        return [
            "/Library/Developer/CommandLineTools/usr/lib/libclang.dylib",
            "/opt/homebrew/opt/llvm/lib/libclang.dylib",
            "/opt/homebrew/Cellar/llvm/22.1.4/lib/libclang.dylib",
            "/opt/homebrew/Cellar/llvm@21/21.1.8/lib/libclang.dylib",
            "/opt/homebrew/Cellar/llvm@20/20.1.8/lib/libclang.dylib",
        ];
    }

    if (OperatingSystem.IsWindows())
    {
        return [
            "libclang.dll",
            @"C:\Program Files\LLVM\bin\libclang.dll",
            @"C:\LLVM\bin\libclang.dll",
        ];
    }

    return [
        "libclang.so",
        "/usr/lib/llvm-21/lib/libclang.so",
        "/usr/lib/llvm-20/lib/libclang.so",
        "/usr/lib/llvm-18/lib/libclang.so",
        "/usr/lib/x86_64-linux-gnu/libclang.so",
        "/usr/lib/aarch64-linux-gnu/libclang.so",
        "/usr/local/lib/libclang.so",
    ];
}

static FileSnapshot CaptureSnapshot(string path)
{
    if (!File.Exists(path))
    {
        return new FileSnapshot
        {
            Path = path,
            Existed = false,
            LastWriteTimeUtc = DateTime.MinValue,
        };
    }

    return new FileSnapshot
    {
        Path = path,
        Existed = true,
        Content = File.ReadAllText(path),
        LastWriteTimeUtc = File.GetLastWriteTimeUtc(path),
    };
}

static void RestoreSnapshot(FileSnapshot snapshot)
{
    if (!snapshot.Existed)
    {
        if (File.Exists(snapshot.Path))
            File.Delete(snapshot.Path);
        return;
    }

    File.WriteAllText(snapshot.Path, snapshot.Content!, new UTF8Encoding(false));
    File.SetLastWriteTimeUtc(snapshot.Path, snapshot.LastWriteTimeUtc);
}

static (bool Ok, string Output) RunProcess(string fileName, string arguments, string workingDirectory)
{
    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    });

    process!.WaitForExit();
    var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    return (process.ExitCode == 0, output);
}

static bool TryGetClangTypeSize(CXType type, out long size)
{
    var current = type;
    if (current.kind == CXType_Elaborated)
        current = clang.Type_getNamedType(current);

    var value = clang.Type_getSizeOf(current);
    if (value >= 0)
    {
        size = value;
        return true;
    }

    size = 0;
    return false;
}

// (Removed old GetFixedElementSize — replaced by GetAbiSize above)

// ─────────────────────────────────────────────────────────────────────────────
// Unresolved type tracking
// ─────────────────────────────────────────────────────────────────────────────

var unresolvedTypes = new List<(string Type, string Context, string Location)>();

void RecordUnresolved(string typeName, string context, string location)
{
    unresolvedTypes.Add((typeName, context, location));
}

// Forward-declare so ResolveCType can reference it
var funcPtrTypedefs = new List<(string Name, string RetType, List<(string Name, string Type)> Params)>();

// Direct function pointer delegates (generated for struct fields that are raw function pointers)
var directFuncPtrs = new List<(string Name, string RetType, List<(string Name, string Type)> Params)>();

// ─────────────────────────────────────────────────────────────────────────────
// C primitive → C# type mapping (canonical, preserves signedness)
// ─────────────────────────────────────────────────────────────────────────────
//
// Mapping table (LP64 — macOS arm64 / Linux x86_64):
//   C type            → C# type   ABI size
//   ─────────────────────────────────────
//   void              → void      -
//   _Bool / bool      → byte      1
//   char / signed char → byte     1
//   unsigned char     → byte      1
//   short             → short     2
//   unsigned short    → ushort    2
//   int               → int       4
//   unsigned int      → uint      4
//   long              → nint      8 (LP64)
//   unsigned long     → nuint     8 (LP64)
//   long long         → long      8
//   unsigned long long → ulong    8
//   float             → float     4
//   double            → double    8
//   long double       → double    8 (mapped to double for ABI)
//   enum              → int       4
//

static string CTypeKindToCSharp(CXTypeKind kind) => kind switch
{
    CXType_Void => "void",
    CXType_Bool => "byte",
    CXType_Char_U or CXType_UChar or CXType_SChar or CXType_Char_S => "byte",
    CXType_UShort => "ushort",
    CXType_Short => "short",
    CXType_UInt => "uint",
    CXType_Int => "int",
    CXType_ULong => "nuint",
    CXType_Long => "nint",        // LP64: 8 bytes on macOS/Linux
    CXType_ULongLong => "ulong",
    CXType_LongLong => "long",
    CXType_Float => "float",
    CXType_Double or CXType_LongDouble => "double",
    CXType_Enum => "int",
    _ => "",
};

// ABI size for each C# primitive type (used for array element sizing)
static int GetAbiSize(string csType) => csType switch
{
    "double" or "long" or "ulong" => 8,
    "float" or "int" or "uint" => 4,
    "short" or "ushort" => 2,
    "byte" or "sbyte" or "bool" => 1,
    "nint" or "nuint" => 8,       // LP64: always 8 on 64-bit
    _ => -1
};

// ABI size of a generated field (used for offset calculation in pre-scan)
static long GetFieldAbiSize(string csType, bool isPtr, bool isArray, long arrSize)
{
    if (isPtr) return 8;
    if (isArray && arrSize > 0)
    {
        var elem = GetAbiSize(csType);
        return elem > 0 ? elem * arrSize : arrSize; // byte[] fallback
    }
    var s = GetAbiSize(csType);
    return s > 0 ? s : 8; // fallback to pointer size for unknown types
}

// Resolve a pointer's pointee type to the correct C# pointer type string.
// Preserves signedness: int* vs uint*, short* vs ushort*, etc.
static string ResolvePointerType(CXTypeKind pointeeKind, string pointeeSpelling)
{
    var scalar = CTypeKindToCSharp(pointeeKind);
    if (scalar.Length > 0 && scalar != "void")
        return scalar + "*";
    if (pointeeKind == CXType_Void)
        return "nint";
    return "";
}

// ─────────────────────────────────────────────────────────────────────────────
// C type → C# type resolution
// ─────────────────────────────────────────────────────────────────────────────

string ResolveCType(CXType type, bool inField, string context = "")
{
    var kind = type.kind;

    if (kind == CXType_Elaborated)
    {
        var named = clang.Type_getNamedType(type);
        return ResolveCType(named, inField, context);
    }

    var mapped = CTypeKindToCSharp(kind);
    if (mapped.Length > 0) return mapped;

    // Typedef
    if (kind == CXType_Typedef)
    {
        var spelling = GetTypeSpelling(type);

        // Check if this typedef is a known function pointer type FIRST
        if (funcPtrTypedefs.Exists(f => f.Name == spelling))
            return spelling;

        var canonical = clang.getCanonicalType(type);

        // Scalar typedef: preserve exact signedness
        if (canonical.kind is CXType_Int or CXType_UInt or CXType_UShort
            or CXType_Short or CXType_Long or CXType_ULong
            or CXType_LongLong or CXType_ULongLong
            or CXType_Float or CXType_Double or CXType_LongDouble
            or CXType_Bool or CXType_Char_U or CXType_UChar or CXType_SChar
            or CXType_Enum)
        {
            return CTypeKindToCSharp(canonical.kind);
        }

        if (canonical.kind == CXType_Pointer)
            return ResolveCType(canonical, inField, context);

        if (canonical.kind == CXType_Record)
        {
            return StripAggregateKeyword(spelling);
        }

        return spelling;
    }

    // Pointer
    if (kind == CXType_Pointer)
    {
        var pointee = clang.getPointeeType(type);

        // Pointer to function → delegate type or nint
        if (pointee.kind is CXType_FunctionProto or CXType_FunctionNoProto)
        {
            // This is a direct function pointer (not typedef'd).
            // In ResolveCType we can't generate a delegate name — that's done at field level.
            // Return nint as placeholder; field-level code will override with typed delegate.
            return "nint";
        }

        // void* → nint
        if (pointee.kind == CXType_Void)
            return "nint";

        // Pointer-to-pointer → nint (opaque)
        if (pointee.kind == CXType_Pointer)
            return "nint";

        // Scalar pointer: preserve signedness via ResolvePointerType
        var ptrResult = ResolvePointerType(pointee.kind, "");
        if (ptrResult.Length > 0)
            return ptrResult;

        // Typedef pointee: resolve through canonical type
        if (pointee.kind == CXType_Typedef)
        {
            var ptSpell = GetTypeSpelling(pointee);

            // Check if typedef is a known function pointer type
            if (funcPtrTypedefs.Exists(f => f.Name == ptSpell))
                return ptSpell; // named delegate type

            var ptCanon = clang.getCanonicalType(pointee);

            // Canonical is a scalar → preserve signedness
            var scalarPtr = ResolvePointerType(ptCanon.kind, "");
            if (scalarPtr.Length > 0)
                return scalarPtr;

            if (ptCanon.kind == CXType_Pointer)
                return "nint";

            if (ptCanon.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                return ptSpell; // named delegate type

            if (ptCanon.kind == CXType_Record)
            {
                var recordSpell = ptSpell.StartsWith("struct ") ? ptSpell[7..] : ptSpell;
                return $"{recordSpell}*";
            }

            return $"{ptSpell}*";
        }

        if (pointee.kind == CXType_Elaborated)
        {
            var inner = clang.Type_getNamedType(pointee);
            return ResolveCType(inner, inField, context) + "*";
        }

        if (pointee.kind == CXType_Record)
        {
            var ptSpell = StripAggregateKeyword(GetTypeSpelling(pointee));
            return $"{ptSpell}*";
        }

        RecordUnresolved(GetTypeSpelling(type), context, "pointer");
        return "nint";
    }

    // Constant array — resolved at field level (not here)
    if (kind == CXType_ConstantArray)
    {
        var elemType = clang.getArrayElementType(type);
        return ResolveCType(elemType, inField, context);
    }

    // Incomplete array (pointer semantics)
    if (kind == CXType_IncompleteArray)
    {
        var elemType = clang.getArrayElementType(type);
        return ResolveCType(elemType, inField, context) + "*";
    }

    // Record (struct/union)
    if (kind == CXType_Record)
    {
        return StripAggregateKeyword(GetTypeSpelling(type));
    }

    // Fallback — record as unresolved
    var sp = GetTypeSpelling(type);
    var loc = $"fallback(kind={kind})";
    if (sp.Length > 0)
    {
        RecordUnresolved(sp, context, loc);
        return sp;
    }
    RecordUnresolved("<empty>", context, loc);
    return "nint";
}

// ─────────────────────────────────────────────────────────────────────────────
// Data model
// ─────────────────────────────────────────────────────────────────────────────

var typedefs = new List<(string Name, string Underlying)>();
var structs = new List<(string Name, List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsArray, long ArrSize, string Comment)> Fields)>();
var unions = new List<(string Name, List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsArray, long ArrSize, string Comment)> Fields)>();
var functions = new List<(string Name, string RetType, List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsDoublePtr)> Params)>();
var constants = new List<(string Name, string Value, long IntValue)>();
// Per-struct anonymous union member offsets: structName -> (fieldName -> offset)
var structUnionOffsets = new Dictionary<string, Dictionary<string, long>>();

// ─────────────────────────────────────────────────────────────────────────────
// Parsing
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("=== Parasolid Header -> C# API Layer Generator ===");
Console.WriteLine("libclang: ClangSharp.Interop");
if (!string.IsNullOrEmpty(sysroot))
    Console.WriteLine($"sysroot:  {sysroot}");
Console.WriteLine($"partial:  {allowPartial}");
Console.WriteLine();

PreloadLibClang();

Console.Write("Parsing parasolid_kernel.h ... ");

nint idx;
unsafe
{
    idx = (nint)clang.createIndex(0, 0);
}

unsafe
{
    var parseArgs = new List<string>
    {
        "-xc",
        "-std=c11",
        "-I" + incDir,
        "-Wno-everything",
    };
    if (!string.IsNullOrEmpty(sysroot))
    {
        parseArgs.Add("-isysroot");
        parseArgs.Add(sysroot);
    }

    var argHandles = parseArgs.Select(Marshal.StringToHGlobalAnsi).ToArray();
    var headerPtr = Marshal.StringToHGlobalAnsi(mainHeader);

    var cmdArgs = stackalloc sbyte*[argHandles.Length];
    for (var i = 0; i < argHandles.Length; i++)
        cmdArgs[i] = (sbyte*)argHandles[i];

    var tu = clang.parseTranslationUnit((void*)idx, (sbyte*)headerPtr, cmdArgs, argHandles.Length, null, 0, 0);
    if (tu == null)
    {
        Console.WriteLine("FAILED to parse translation unit");
        return 1;
    }

    var cursor = clang.getTranslationUnitCursor(tu);

    // Pass 1: Collect all function pointer typedefs first (struct fields may reference them)
    CXCursorVisitor funcPtrCollector = (child, parent, _) =>
    {
        var kind = clang.getCursorKind(child);
        if (kind != CXCursor_TypedefDecl) return CXChildVisitResult.CXChildVisit_Continue;

        var underlying = clang.getTypedefDeclUnderlyingType(child);
        var funcType = underlying;
        if (underlying.kind == CXType_Pointer)
        {
            var pt = clang.getPointeeType(underlying);
            if (pt.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                funcType = pt;
        }

        if (funcType.kind is CXType_FunctionProto or CXType_FunctionNoProto)
        {
            var name = NormalizeTypeName(GetSpelling(child));
            var retType = clang.getResultType(funcType);
            var csRet = NormalizeTypeName(ResolveCType(retType, false, $"{name}(return)"));

            var fparams = new List<(string Name, string Type)>();
            var numArgs = clang.getNumArgTypes(funcType);

            var paramNames = new List<string>();
            CXCursorVisitor fpParamVisitor = (pc, _, _) =>
            {
                if (clang.getCursorKind(pc) == CXCursor_ParmDecl)
                    paramNames.Add(GetSpelling(pc));
                return CXChildVisitResult.CXChildVisit_Continue;
            };
            child.VisitChildren(fpParamVisitor, default);

            for (uint i = 0; i < (uint)numArgs; i++)
            {
                var argType = clang.getArgType(funcType, i);
                var pname = i < paramNames.Count ? paramNames[(int)i] : $"arg{i}";
                if (string.IsNullOrEmpty(pname)) pname = $"arg{i}";
                var csType = NormalizeTypeName(ResolveCType(argType, false, $"{name}({pname})"));
                fparams.Add((pname, csType));
            }

            funcPtrTypedefs.Add((name, csRet, fparams));
        }
        return CXChildVisitResult.CXChildVisit_Continue;
    };
    cursor.VisitChildren(funcPtrCollector, default);

    // Pass 2: Collect typedefs, structs, functions, and constants
    CXCursorVisitor topLevelVisitor = (child, parent, _) =>
    {
        var kind = clang.getCursorKind(child);
        var spelling = NormalizeTypeName(GetSpelling(child));

        if (kind == CXCursor_TypedefDecl)
        {
            var underlying = clang.getTypedefDeclUnderlyingType(child);

            // Skip function pointer typedefs — already collected in Pass 1
            if (funcPtrTypedefs.Exists(f => f.Name == spelling))
                return CXChildVisitResult.CXChildVisit_Continue;

            var canonical = clang.getCanonicalType(underlying);
            var underlyingSp = NormalizeTypeName(GetTypeSpelling(underlying));

            if (canonical.kind == CXType_Pointer)
            {
                typedefs.Add((spelling, "nint"));
            }
            else if (canonical.kind == CXType_Record)
            {
                var recordName = StripAggregateKeyword(underlyingSp);
                typedefs.Add((spelling, recordName));
            }
            else
            {
                var csType = CTypeKindToCSharp(canonical.kind);
                typedefs.Add((spelling, NormalizeTypeName(csType.Length > 0 ? csType : underlyingSp)));
            }
        }
        else if (kind == CXCursor_StructDecl || kind == CXCursor_UnionDecl)
        {
            if (spelling.Length == 0)
                return CXChildVisitResult.CXChildVisit_Continue;

            var fields = new List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsArray, long ArrSize, string Comment)>();
            var unionMemberOffsets = new Dictionary<string, long>(); // union member name -> shared FieldOffset

            CXCursorVisitor fieldVisitor = (fieldCursor, _, _) =>
            {
                var fieldKind = clang.getCursorKind(fieldCursor);

                // Handle anonymous union: collect members, record their shared offset, add placeholder
                if (fieldKind == CXCursor_UnionDecl)
                {
                    // Calculate offset: sum of all non-union fields so far, aligned to union's alignment
                    long rawOffset = 0;
                    foreach (var f in fields)
                    {
                        if (!unionMemberOffsets.ContainsKey(f.Name))
                            rawOffset += GetFieldAbiSize(f.Type, f.IsPtr, f.IsArray, f.ArrSize);
                    }
                    // Align to union's natural alignment
                    var unionAlign = clang.Type_getAlignOf(clang.getCursorType(fieldCursor));
                    var unionOffset = unionAlign > 0 ? (rawOffset + unionAlign - 1) / unionAlign * unionAlign : rawOffset;

                    // Collect union members and add them as typed fields with shared offset
                    CXCursorVisitor memberCollector = (member, _, _) =>
                    {
                        if (clang.getCursorKind(member) == CXCursor_FieldDecl)
                        {
                            var mname = GetSpelling(member);
                            var mtype = clang.getCursorType(member);
                            var mkind = mtype.kind;
                            var mIsConst = clang.isConstQualifiedType(mtype) != 0;
                            if (mkind == CXType_Elaborated) { mtype = clang.Type_getNamedType(mtype); mkind = mtype.kind; }

                            if (mkind == CXType_Pointer)
                            {
                                var pointee = clang.getPointeeType(mtype);
                                var ptCs = NormalizeTypeName(ResolveCType(pointee, true, $"{spelling}.{mname}"));
                                if (ptCs.Length == 0) ptCs = "nint";
                                var ptSpell = NormalizeTypeName(GetTypeSpelling(pointee));
                                if (ptSpell == "void") ptCs = "nint";
                                fields.Add((mname, ptCs, true, mIsConst, false, 0, ""));
                            }
                            else
                            {
                                var csType = NormalizeTypeName(ResolveCType(mtype, true, $"{spelling}.{mname}"));
                                if (csType.Length == 0) csType = "nint";
                                fields.Add((mname, csType, false, mIsConst, false, 0, ""));
                            }
                            unionMemberOffsets[mname] = unionOffset;
                        }
                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    fieldCursor.VisitChildren(memberCollector, default);

                    return CXChildVisitResult.CXChildVisit_Continue;
                }

                // Skip the named FieldDecl for an anonymous union (e.g. "record" in PK_REPORT_record_s)
                // The union members are already collected above and will be emitted with [FieldOffset].
                if (fieldKind == CXCursor_FieldDecl)
                {
                    var ft = clang.getCursorType(fieldCursor);
                    if (ft.kind == CXType_Elaborated) ft = clang.Type_getNamedType(ft);
                    if (ft.kind == CXType_Record)
                    {
                        var rt = clang.getTypeDeclaration(ft);
                        var rtSpelling = NormalizeTypeName(GetSpelling(rt));
                        if (string.IsNullOrEmpty(rtSpelling) || rtSpelling.Contains(' ') || rtSpelling.Contains('('))
                            return CXChildVisitResult.CXChildVisit_Continue;
                    }
                }

                if (fieldKind != CXCursor_FieldDecl)
                    return CXChildVisitResult.CXChildVisit_Continue;

                var fname = NormalizeTypeName(GetSpelling(fieldCursor));
                var ftype = clang.getCursorType(fieldCursor);
                var fkind = ftype.kind;
                var isConst = clang.isConstQualifiedType(ftype) != 0;

                if (fkind == CXType_Elaborated)
                {
                    ftype = clang.Type_getNamedType(ftype);
                    fkind = ftype.kind;
                }

                // Step 4a: Flatten multi-dimensional arrays
                if (fkind == CXType_ConstantArray)
                {
                    long totalSize = 1;
                    var currentType = ftype;
                    while (currentType.kind == CXType_ConstantArray)
                    {
                        totalSize *= clang.getArraySize(currentType);
                        currentType = clang.getArrayElementType(currentType);
                    }
                    // Unwrap elaborated element type
                    if (currentType.kind == CXType_Elaborated)
                        currentType = clang.Type_getNamedType(currentType);
                    var csElem = NormalizeTypeName(ResolveCType(currentType, true, $"{spelling}.{fname}"));
                    if (TryGetClangTypeSize(currentType, out var elementSize))
                    {
                        var primitiveSize = GetAbiSize(csElem);
                        if (primitiveSize > 0)
                        {
                            fields.Add((fname, csElem, false, false, true, totalSize, ""));
                        }
                        else
                        {
                            var byteCount = checked(totalSize * elementSize);
                            fields.Add((fname, "byte", false, false, true, byteCount,
                                $"ABI storage for {csElem}[{totalSize}] ({elementSize} bytes/element)"));
                        }
                        return CXChildVisitResult.CXChildVisit_Continue;
                    }

                    if (csElem.Contains("union") || csElem.Contains("unnamed") || csElem.Contains('('))
                    {
                        RecordUnresolved(GetTypeSpelling(currentType), $"{spelling}.{fname}", "array-element-union");
                    }
                    else
                    {
                        RecordUnresolved(csElem, $"{spelling}.{fname}", "array-element-size-unknown");
                    }
                    fields.Add((fname, "byte", false, false, true, 1,
                        $"ABI-BLOCKING FALLBACK for {csElem}[{totalSize}]"));
                    return CXChildVisitResult.CXChildVisit_Continue;
                }

                // Check for function pointer field (pointer to function or pointer to function typedef)
                if (fkind == CXType_Pointer)
                {
                    var pointee = clang.getPointeeType(ftype);

                    // Direct function pointer: void (*)(int) — generate a named delegate
                    if (pointee.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                    {
                        var delegateName = $"{spelling}_{fname}_f_t";
                        var retType = clang.getResultType(pointee);
                        var csRet = NormalizeTypeName(ResolveCType(retType, false, $"{delegateName}(return)"));

                        var fpParams = new List<(string Name, string Type)>();
                        var numArgs = clang.getNumArgTypes(pointee);

                        // Collect parameter names from cursor children
                        var paramNames = new List<string>();
                        CXCursorVisitor fpParamVisitor = (pc, _, _) =>
                        {
                            if (clang.getCursorKind(pc) == CXCursor_ParmDecl)
                                paramNames.Add(GetSpelling(pc));
                            return CXChildVisitResult.CXChildVisit_Continue;
                        };
                        fieldCursor.VisitChildren(fpParamVisitor, default);

                        for (uint ai = 0; ai < (uint)numArgs; ai++)
                        {
                            var argType = clang.getArgType(pointee, ai);
                            var pname = ai < paramNames.Count ? paramNames[(int)ai] : $"arg{ai}";
                            if (string.IsNullOrEmpty(pname)) pname = $"arg{ai}";
                            var csArgType = CTypeKindToCSharp(argType.kind);
                            if (csArgType.Length == 0)
                            {
                                if (argType.kind is CXType_ConstantArray or CXType_IncompleteArray)
                                {
                                    var elemType = clang.getArrayElementType(argType);
                                    var elemCs = CTypeKindToCSharp(elemType.kind);
                                    csArgType = elemCs.Length > 0 ? elemCs + "*" : "nint";
                                }
                                else if (argType.kind == CXType_Pointer)
                                {
                                    var pt = clang.getPointeeType(argType);
                                    if (pt.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                                        csArgType = "nint";
                                    else
                                        csArgType = NormalizeTypeName(ResolveCType(argType, false, $"{spelling}.{fname}"));
                                }
                                else
                                {
                                    csArgType = NormalizeTypeName(ResolveCType(argType, false, $"{delegateName}(arg{ai})"));
                                }
                            }
                            fpParams.Add((pname, csArgType));
                        }

                        directFuncPtrs.Add((delegateName, csRet, fpParams));
                        fields.Add((fname, delegateName, true, isConst, false, 0, ""));
                        return CXChildVisitResult.CXChildVisit_Continue;
                    }

                    // Typedef'd function pointer: PK_SESSION_start_f_t
                    if (pointee.kind == CXType_Typedef)
                    {
                        var ptSpell = NormalizeTypeName(GetTypeSpelling(pointee));
                        if (funcPtrTypedefs.Exists(f => f.Name == ptSpell))
                        {
                            fields.Add((fname, ptSpell, true, isConst, false, 0, ""));
                            return CXChildVisitResult.CXChildVisit_Continue;
                        }
                        // Typedef resolves to function pointer?
                        var ptCanon = clang.getCanonicalType(pointee);
                        if (ptCanon.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                        {
                            fields.Add((fname, ptSpell, true, isConst, false, 0, ""));
                            return CXChildVisitResult.CXChildVisit_Continue;
                        }
                    }
                }

                var csType = NormalizeTypeName(ResolveCType(ftype, true, $"{spelling}.{fname}"));
                if (csType.Contains("union") || csType.Contains("unnamed") || csType.Contains('('))
                {
                    if (TryGetClangTypeSize(ftype, out var unionSize))
                    {
                        fields.Add((fname, "byte", false, isConst, true, unionSize,
                            $"ABI storage for {NormalizeReportText(GetTypeSpelling(ftype), repoRoot)} ({unionSize} bytes)"));
                        return CXChildVisitResult.CXChildVisit_Continue;
                    }

                    RecordUnresolved(GetTypeSpelling(ftype), $"{spelling}.{fname}", "struct-field");
                    csType = "nint";
                }
                var isPtr = csType.Contains('*') || csType == "nint";
                fields.Add((fname, csType, isPtr, isConst, false, 0, ""));

                return CXChildVisitResult.CXChildVisit_Continue;
            };
            child.VisitChildren(fieldVisitor, default);

            if (kind == CXCursor_StructDecl)
            {
                structs.Add((spelling, fields));
                if (unionMemberOffsets.Count > 0)
                    structUnionOffsets[spelling] = new Dictionary<string, long>(unionMemberOffsets);
            }
            else
                unions.Add((spelling, fields));
        }
        else if (kind == CXCursor_FunctionDecl)
        {
            if (!spelling.StartsWith("PK_"))
                return CXChildVisitResult.CXChildVisit_Continue;

            var funcType = clang.getCursorType(child);
            var retType = clang.getResultType(funcType);
            var csRet = NormalizeTypeName(ResolveCType(retType, false, $"{spelling}(return)"));

            var numArgs = clang.getNumArgTypes(funcType);

            var paramNames = new List<string>();
            CXCursorVisitor paramVisitor = (paramCursor, _, _) =>
            {
                if (clang.getCursorKind(paramCursor) == CXCursor_ParmDecl)
                    paramNames.Add(GetSpelling(paramCursor));
                return CXChildVisitResult.CXChildVisit_Continue;
            };
            child.VisitChildren(paramVisitor, default);

            var parameters = new List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsDoublePtr)>();

            for (uint i = 0; i < (uint)numArgs; i++)
            {
                var argType = clang.getArgType(funcType, i);
                var pname = i < paramNames.Count ? paramNames[(int)i] : $"arg{i}";
                if (string.IsNullOrEmpty(pname)) pname = $"arg{i}";

                var isConst = clang.isConstQualifiedType(argType) != 0;
                var isDoublePtr = false;

                if (argType.kind == CXType_Pointer)
                {
                    var pointee = clang.getPointeeType(argType);
                    if (pointee.kind == CXType_Pointer)
                        isDoublePtr = true;
                }

                var pCsType = NormalizeTypeName(ResolveCType(argType, false, $"{spelling}({pname})"));
                if (isDoublePtr)
                    pCsType = "nint*";
                if (pCsType.Contains("union") || pCsType.Contains("unnamed") || pCsType.Contains('('))
                {
                    RecordUnresolved(GetTypeSpelling(argType), $"{spelling}({pname})", "func-param");
                    pCsType = "nint";
                }
                var isPtr = pCsType.Contains('*') || isDoublePtr || pCsType == "nint";

                parameters.Add((pname, pCsType, isPtr, isConst, isDoublePtr));
            }

            functions.Add((spelling, csRet, parameters));
        }

        return CXChildVisitResult.CXChildVisit_Continue;
    };
    cursor.VisitChildren(topLevelVisitor, default);

    clang.disposeTranslationUnit(tu);

    foreach (var argHandle in argHandles)
        Marshal.FreeHGlobal(argHandle);
    Marshal.FreeHGlobal(headerPtr);
}

unsafe
{
    clang.disposeIndex((void*)idx);
}

Console.WriteLine($"OK  typedefs={typedefs.Count}  funcPtrs={funcPtrTypedefs.Count}  structs={structs.Count}  functions={functions.Count}");

// ─────────────────────────────────────────────────────────────────────────────
// Parse constants (two-pass: collect simple, then resolve expressions)
// ─────────────────────────────────────────────────────────────────────────────

Console.Write("Parsing constants ... ");

var rawDefines = new Dictionary<string, long>();
var constSeen = new HashSet<string>();

// Regex patterns
var reCast = new Regex(@"#define\s+(\w+)\s+\(\s*\([^\)]*\)\s*(-?\d+)\s*\)");
var reSimple = new Regex(@"^#define\s+(\w+)\s+(-?\d+)\s*(/\*.*\*/)?\s*$", RegexOptions.Multiline);
var reKI = new Regex(@"^#define\s+(KI_\w+)\s+(-?\d+)\s", RegexOptions.Multiline);
var reMaxToken = new Regex(@"#define\s+(PK_max_token_size)\s+(\d+)");
// ((type)(REF + N)) or ((type)(REF - N))
var reExprArith = new Regex(@"#define\s+(\w+)\s+\(\s*\([^\)]*\)\s*\(\s*(\w+)\s*([+-])\s*(\d+)\s*\)\s*\)");
// ((type)REF) where REF is not a number
var reExprRef = new Regex(@"#define\s+(\w+)\s+\(\s*\([^\)]*\)\s*([A-Za-z_]\w*)\s*\)");

void ParseConstantsPass1(string path)
{
    if (!File.Exists(path)) return;
    var text = File.ReadAllText(path);

    foreach (var m in reSimple.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Add(name) && long.TryParse(m.Groups[2].Value, out var val))
            rawDefines[name] = val;
    }

    foreach (var m in reKI.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Add(name) && long.TryParse(m.Groups[2].Value, out var val))
            rawDefines[name] = val;
    }

    foreach (var m in reMaxToken.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Add(name) && long.TryParse(m.Groups[2].Value, out var val))
            rawDefines[name] = val;
    }

    foreach (var m in reCast.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Add(name) && long.TryParse(m.Groups[2].Value, out var val))
            rawDefines[name] = val;
    }
}

// Collect unresolved expression defines for pass 2
var unresolvedExprs = new List<(string Name, string Ref, string Op, int Offset)>();
var unresolvedRefs = new List<(string Name, string Ref)>();

void ParseConstantsPass2(string path)
{
    if (!File.Exists(path)) return;
    var text = File.ReadAllText(path);

    foreach (var m in reExprArith.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Contains(name)) continue;
        var refName = m.Groups[2].Value;
        var op = m.Groups[3].Value;
        if (int.TryParse(m.Groups[4].Value, out var offset))
            unresolvedExprs.Add((name, refName, op, offset));
    }

    foreach (var m in reExprRef.Matches(text).Cast<Match>())
    {
        var name = m.Groups[1].Value;
        if (constSeen.Contains(name)) continue;
        var refName = m.Groups[2].Value;
        unresolvedRefs.Add((name, refName));
    }
}

// Pass 1: collect simple defines
foreach (var h in tokenHeaders) ParseConstantsPass1(h);
ParseConstantsPass1(mainHeader);

// Pass 2: collect expression defines
foreach (var h in tokenHeaders) ParseConstantsPass2(h);
ParseConstantsPass2(mainHeader);

// Resolve expressions iteratively
for (int pass = 0; pass < 10; pass++)
{
    var resolved = 0;

    foreach (var expr in unresolvedExprs.ToList())
    {
        if (rawDefines.TryGetValue(expr.Ref, out var refVal))
        {
            rawDefines[expr.Name] = expr.Op == "+" ? refVal + expr.Offset : refVal - expr.Offset;
            constSeen.Add(expr.Name);
            unresolvedExprs.Remove(expr);
            resolved++;
        }
    }

    foreach (var r in unresolvedRefs.ToList())
    {
        if (rawDefines.TryGetValue(r.Ref, out var refVal))
        {
            rawDefines[r.Name] = refVal;
            constSeen.Add(r.Name);
            unresolvedRefs.Remove(r);
            resolved++;
        }
    }

    if (resolved == 0) break;
}

// Populate constants list
foreach (var kv in rawDefines.OrderBy(k => k.Key))
    constants.Add((kv.Key, kv.Value.ToString(), kv.Value));

var unresolvedConstCount = unresolvedExprs.Count + unresolvedRefs.Count;
Console.WriteLine($"{constants.Count} constants ({unresolvedConstCount} unresolved expressions)");

// ─────────────────────────────────────────────────────────────────────────────
// Deduplicate
// ─────────────────────────────────────────────────────────────────────────────

var typedefSeen = new HashSet<string>();
typedefs = typedefs.Where(t => typedefSeen.Add(t.Name)).ToList();

var structSeen = new HashSet<string>();
structs = structs.Where(s => structSeen.Add(s.Name)).ToList();

var funcSeen = new HashSet<string>();
functions = functions.Where(f => funcSeen.Add(f.Name)).ToList();

var funcPtrSeen = new HashSet<string>();
funcPtrTypedefs = funcPtrTypedefs.Where(f => funcPtrSeen.Add(f.Name)).ToList();

var directFuncPtrSeen = new HashSet<string>();
directFuncPtrs = directFuncPtrs.Where(f => directFuncPtrSeen.Add(f.Name)).ToList();

// Build typedef map for chain resolution
var typedefMap = new Dictionary<string, string>();
foreach (var td in typedefs)
    typedefMap[td.Name] = td.Underlying;
// Also add func ptr typedefs so they resolve by name
foreach (var fp in funcPtrTypedefs)
    typedefMap[fp.Name] = fp.Name; // self-reference = already resolved
// Add direct func ptrs too
foreach (var fp in directFuncPtrs)
    typedefMap[fp.Name] = fp.Name;

Console.WriteLine($"\nAfter dedup: typedefs={typedefs.Count}  funcPtrs={funcPtrTypedefs.Count}  directFuncPtrs={directFuncPtrs.Count}  structs={structs.Count}  functions={functions.Count}  constants={constants.Count}");

// ─────────────────────────────────────────────────────────────────────────────
// Typedef chain resolution
// ─────────────────────────────────────────────────────────────────────────────

string ResolveChain(string name, int depth = 0)
{
    name = NormalizeTypeName(name);
    if (depth > 10) return name;
    if (!typedefMap.TryGetValue(name, out var target)) return name;
    target = NormalizeTypeName(target);
    if (target is "int" or "uint" or "nuint" or "byte" or "short" or "ushort"
        or "long" or "ulong" or "float" or "double" or "nint")
        return target;
    if (typedefMap.ContainsKey(target) && target != name)
        return ResolveChain(target, depth + 1);
    return target;
}

// ─────────────────────────────────────────────────────────────────────────────
// Code generation helpers
// ─────────────────────────────────────────────────────────────────────────────

bool IsGeneratedAggregateTypeName(string typeName)
{
    typeName = NormalizeTypeName(typeName);
    return structs.Any(s => s.Name == typeName) || unions.Any(u => u.Name == typeName);
}

string QualifyForGlobalAlias(string typeName, string ns)
{
    typeName = NormalizeTypeName(typeName);
    if (typeName.EndsWith('*'))
    {
        var depth = CountPointerDepth(typeName);
        var core = NormalizeTypeName(typeName[..^depth]);
        return $"{QualifyForGlobalAlias(core, ns)}{new string('*', depth)}";
    }

    return IsGeneratedAggregateTypeName(typeName) ? $"{ns}.{typeName}" : typeName;
}

void GenerateUsingAliases(StringBuilder sb, string? ns)
{
    foreach (var td in typedefs)
    {
        var target = ResolveChain(td.Name);
        if (target == td.Name) continue;
        var emittedTarget = ns is null ? target : QualifyForGlobalAlias(target, ns);

        if (target is "int" or "uint" or "nuint" or "byte" or "short" or "ushort"
            or "long" or "ulong" or "float" or "double" or "nint")
        {
            sb.AppendLine($"global using {td.Name} = {emittedTarget};");
        }
        else if (!target.Contains('*'))
        {
            if (target.Contains(' ') || target.Contains("union") || target.Contains("unnamed") || target.Contains('(')
                || target.StartsWith("struct "))
            {
                sb.AppendLine($"global using {td.Name} = nint;");
                RecordUnresolved(target, td.Name, "typedef-alias");
            }
            else
            {
                sb.AppendLine($"global using {td.Name} = {emittedTarget};");
            }
        }
    }
}

string QualifySignatureType(string typeName, string ns)
{
    typeName = NormalizeTypeName(typeName);
    if (typeName.EndsWith('*'))
    {
        var depth = CountPointerDepth(typeName);
        var core = NormalizeTypeName(typeName[..^depth]);
        return $"{QualifySignatureType(core, ns)}{new string('*', depth)}";
    }

    var resolved = ResolveChain(typeName);
    if (resolved is "void" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "nint" or "nuint" or "float" or "double")
        return resolved;

    if (IsGeneratedAggregateTypeName(resolved))
        return $"{ns}.{resolved}";

    return resolved;
}

string ToFunctionPointerSignature(string retType, List<(string Name, string Type)> parameters, string ns)
{
    var types = parameters.Select(p => QualifySignatureType(p.Type, ns)).Append(QualifySignatureType(retType, ns));
    return $"delegate* unmanaged[Cdecl]<{string.Join(", ", types)}>";
}

bool IsFunctionPointerAlias(string typeName)
{
    typeName = NormalizeTypeName(typeName);
    return funcPtrTypedefs.Any(f => f.Name == typeName) || directFuncPtrs.Any(f => f.Name == typeName);
}

bool RequiresUnsafeFieldType(string typeName, bool isArray)
{
    typeName = NormalizeTypeName(typeName);
    if (isArray)
        return true;
    if (typeName.Contains('*'))
        return true;
    if (typeName == "nint")
        return true;
    if (IsFunctionPointerAlias(typeName))
        return true;
    return false;
}

void GenerateFunctionPointerAliases(StringBuilder sb, string ns)
{
    foreach (var fp in funcPtrTypedefs)
    {
        sb.AppendLine($"global using unsafe {fp.Name} = {ToFunctionPointerSignature(fp.RetType, fp.Params, ns)};");
    }

    foreach (var fp in directFuncPtrs)
    {
        sb.AppendLine($"global using unsafe {fp.Name} = {ToFunctionPointerSignature(fp.RetType, fp.Params, ns)};");
    }
}

void GenerateStructs(StringBuilder sb, string access)
{
    foreach (var s in structs.OrderBy(s => StructSortKeyByName(s.Name)).ThenBy(s => s.Name))
    {
        var isUnsafe = s.Fields.Exists(f => RequiresUnsafeFieldType(f.Type, f.IsArray));
        var hasUnionMembers = structUnionOffsets.TryGetValue(s.Name, out var unionOffsets);

        if (hasUnionMembers)
            sb.AppendLine("    [StructLayout(LayoutKind.Explicit)]");
        else
            sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
        sb.AppendLine(isUnsafe ? $"    {access} unsafe struct {s.Name}" : $"    {access} struct {s.Name}");
        sb.AppendLine("    {");

        if (hasUnionMembers)
        {
            // Explicit layout: calculate offsets, union members share the same offset
            long offset = 0;
            long maxUnionEnd = 0;
            foreach (var f in s.Fields)
            {
                var fname = SanitizeName(f.Name);
                long fieldOffset;
                if (unionOffsets!.TryGetValue(f.Name, out var uo))
                {
                    fieldOffset = uo; // union member — use pre-computed shared offset
                    var memberSize = GetFieldAbiSize(f.Type, f.IsPtr, f.IsArray, f.ArrSize);
                    maxUnionEnd = Math.Max(maxUnionEnd, uo + memberSize);
                }
                else
                {
                    offset = Math.Max(offset, maxUnionEnd);
                    fieldOffset = offset;
                    offset += GetFieldAbiSize(f.Type, f.IsPtr, f.IsArray, f.ArrSize);
                }
                sb.AppendLine($"        [FieldOffset({fieldOffset})]");
                EmitField(sb, f, fname);
            }
        }
        else
        {
            foreach (var f in s.Fields)
            {
                var fname = SanitizeName(f.Name);
                EmitField(sb, f, fname);
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }
}

static void EmitField(StringBuilder sb, (string Name, string Type, bool IsPtr, bool IsConst, bool IsArray, long ArrSize, string Comment) f, string fname)
{
    if (f.IsArray && f.ArrSize > 0)
    {
        var commentSuffix = string.IsNullOrEmpty(f.Comment) ? "" : $" // {f.Comment}";
        sb.AppendLine($"        public fixed {f.Type} {fname}[{f.ArrSize}];{commentSuffix}");
    }
    else if (f.IsPtr)
    {
        sb.AppendLine(f.IsConst
            ? $"        public readonly {f.Type} {fname};"
            : $"        public {f.Type} {fname};");
    }
    else
    {
        sb.AppendLine($"        public {f.Type} {fname};");
    }
}

void GenerateUnions(StringBuilder sb, string access)
{
    foreach (var u in unions.OrderBy(u => u.Name))
    {
        var isUnsafe = u.Fields.Exists(f => RequiresUnsafeFieldType(f.Type, f.IsArray));
        sb.AppendLine("    [StructLayout(LayoutKind.Explicit)]");
        sb.AppendLine(isUnsafe ? $"    {access} unsafe struct {u.Name}" : $"    {access} struct {u.Name}");
        sb.AppendLine("    {");
        foreach (var f in u.Fields)
        {
            var fname = SanitizeName(f.Name);
            sb.AppendLine("        [FieldOffset(0)]");

            if (f.IsArray && f.ArrSize > 0)
            {
                var commentSuffix = string.IsNullOrEmpty(f.Comment) ? "" : $" // {f.Comment}";
                sb.AppendLine($"        public fixed {f.Type} {fname}[{f.ArrSize}];{commentSuffix}");
            }
            else if (f.IsPtr)
            {
                sb.AppendLine(f.IsConst
                    ? $"        public readonly {f.Type} {fname};"
                    : $"        public {f.Type} {fname};");
            }
            else
            {
                sb.AppendLine($"        public {f.Type} {fname};");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}

void GenerateConstants(StringBuilder sb, string access)
{
    sb.AppendLine($"    {access} static class ParasolidConstants");
    sb.AppendLine("    {");
    foreach (var c in constants.OrderBy(c => c.Name))
        sb.AppendLine($"        public const int {c.Name} = {c.IntValue};");
    sb.AppendLine("    }");
    sb.AppendLine();
}

// ─────────────────────────────────────────────────────────────────────────────
// Native file generator (NO DllImport — this IS the native library)
// ─────────────────────────────────────────────────────────────────────────────

void GenerateNativeFile(string outputPath, string ns)
{
    var sb = new StringBuilder(1 << 20);
    sb.AppendLine("// <auto-generated />");
    sb.AppendLine("// Parasolid ABI types, function pointer aliases, and constants.");
    sb.AppendLine("// This file is the primary generated artifact for the NativeAOT shared library.");
    sb.AppendLine();

    // Using aliases
    GenerateUsingAliases(sb, ns);
    if (funcPtrTypedefs.Count > 0 || directFuncPtrs.Count > 0)
    {
        GenerateFunctionPointerAliases(sb, ns);
        sb.AppendLine();
    }

    sb.AppendLine("using System.Runtime.InteropServices;");
    sb.AppendLine();
    sb.AppendLine($"namespace {ns};");
    sb.AppendLine();

    if (unions.Count > 0)
    {
        sb.AppendLine("    // ABI union definitions");
        GenerateUnions(sb, "internal");
    }

    // Structs
    sb.AppendLine("    // ABI struct definitions");
    GenerateStructs(sb, "internal");

    // Constants
    GenerateConstants(sb, "internal");

    // ABI validation (compile-time sizeof checks — causes build errors on mismatch)
    sb.AppendLine("    internal static unsafe class AbiValidation");
    sb.AppendLine("    {");
    sb.AppendLine("        // These cause compile errors if struct sizes don't match expected ABI");
    sb.AppendLine("        static readonly int _v = sizeof(PK_VECTOR_s) == 24 ? 0 : throw new System.Exception(\"PK_VECTOR_s size mismatch\");");
    sb.AppendLine("        static readonly int _i = sizeof(PK_INTERVAL_s) == 16 ? 0 : throw new System.Exception(\"PK_INTERVAL_s size mismatch\");");
    sb.AppendLine("        static readonly int _b = sizeof(PK_BOX_s) == 48 ? 0 : throw new System.Exception(\"PK_BOX_s size mismatch\");");
    sb.AppendLine("        static readonly int _p = sizeof(PK_POINT_sf_s) == 24 ? 0 : throw new System.Exception(\"PK_POINT_sf_s size mismatch\");");
    sb.AppendLine("        static readonly int _t = sizeof(PK_TRANSF_sf_s) == 128 ? 0 : throw new System.Exception(\"PK_TRANSF_sf_s size mismatch\");");
    sb.AppendLine("    }");
    sb.AppendLine();

    File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"  {Path.GetRelativePath(repoRoot, outputPath)} ({sb.Length / 1024}KB)");
}

static bool IsReadOnlyApi(string functionName)
{
    return functionName.Contains("_ask_")
        || functionName.Contains("_is_")
        || functionName.Contains("_eval_")
        || functionName.Contains("_find_")
        || functionName.Contains("_contains_")
        || functionName.Contains("_range_");
}

bool IsUnmanagedCallersOnlyType(string typeName)
{
    if (typeName.EndsWith('*'))
        return true;

    var resolved = ResolveChain(typeName);
    return resolved is "void" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "nint" or "nuint" or "float" or "double";
}

bool CanGenerateExportStub((string Name, string RetType, List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsDoublePtr)> Params) func)
{
    if (!IsUnmanagedCallersOnlyType(func.RetType))
        return false;

    foreach (var parameter in func.Params)
    {
        if (!IsUnmanagedCallersOnlyType(parameter.Type))
            return false;
    }

    return true;
}

void GenerateExportsFile(string outputPath)
{
    var implemented = new HashSet<string>(
        Regex.Matches(File.ReadAllText(Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "KernelExports.cs")),
            @"EntryPoint\s*=\s*""(PK_[A-Za-z0-9_]+)""")
        .Select(m => m.Groups[1].Value));

    var sb = new StringBuilder(1 << 20);
    sb.AppendLine("// <auto-generated />");
    sb.AppendLine("// Parasolid export stubs for APIs not yet manually implemented.");
    sb.AppendLine("using System.Runtime.InteropServices;");
    sb.AppendLine("using ProjectGmKernel.Native.Generated;");
    sb.AppendLine("using ProjectGmKernel.Native.Runtime;");
    sb.AppendLine();
    sb.AppendLine("namespace ProjectGmKernel.Native;");
    sb.AppendLine();
    sb.AppendLine("internal static unsafe partial class KernelExports");
    sb.AppendLine("{");

    foreach (var func in functions.OrderBy(f => f.Name))
    {
        if (implemented.Contains(func.Name))
            continue;
        if (!CanGenerateExportStub(func))
            continue;

        var parms = string.Join(", ", func.Params.Select(p =>
        {
            return $"{p.Type} {SanitizeName(p.Name)}";
        }));
        sb.AppendLine($"    [UnmanagedCallersOnly(EntryPoint = \"{func.Name}\")]");
        sb.AppendLine($"    public static {func.RetType} {func.Name}({parms})");
        sb.AppendLine("    {");
        sb.AppendLine("        return KernelRuntime.NotImplemented();");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    sb.AppendLine("}");
    File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"  {Path.GetRelativePath(repoRoot, outputPath)} ({sb.Length / 1024}KB)");
}

void WriteAbiCheckProject()
{
    Directory.CreateDirectory(abiCheckDir);

    var projectPath = Path.Combine(abiCheckDir, "AbiCheck.csproj");
    var programPath = Path.Combine(abiCheckDir, "Program.cs");

    var project = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj" />
  </ItemGroup>
</Project>
""";

    var program = """
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

static void AssertSize(string name, int actual, int expected)
{
    Console.WriteLine($"ASSERT {name}: actual={actual} expected={expected} {(actual == expected ? "OK" : "FAIL")}");
    if (actual != expected)
        Environment.ExitCode = 1;
}

static void ObserveSize(string name, int actual)
{
    Console.WriteLine($"OBSERVE {name}: actual={actual}");
}

AssertSize(nameof(PK_VECTOR_s), Marshal.SizeOf<PK_VECTOR_s>(), 24);
AssertSize(nameof(PK_POINT_sf_s), Marshal.SizeOf<PK_POINT_sf_s>(), 24);
AssertSize(nameof(PK_SESSION_start_o_s), Marshal.SizeOf<PK_SESSION_start_o_s>(), 24);
AssertSize(nameof(PK_BOX_s), Marshal.SizeOf<PK_BOX_s>(), 48);
AssertSize(nameof(PK_INTERVAL_s), Marshal.SizeOf<PK_INTERVAL_s>(), 16);
AssertSize(nameof(PK_UVBOX_s), Marshal.SizeOf<PK_UVBOX_s>(), 32);

ObserveSize(nameof(PK_SESSION_applio_s), Marshal.SizeOf<PK_SESSION_applio_s>());
ObserveSize(nameof(PK_SESSION_indexio_s), Marshal.SizeOf<PK_SESSION_indexio_s>());
""";

    File.WriteAllText(projectPath, project, new UTF8Encoding(false));
    File.WriteAllText(programPath, program, new UTF8Encoding(false));
}

// ─────────────────────────────────────────────────────────────────────────────
// Generate
// ─────────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// Phase 1: Generate to temp files
// ─────────────────────────────────────────────────────────────────────────────

var nativeTmp = nativeOut + ".tmp";
var exportsTmp = exportsOut + ".tmp";

Console.WriteLine("\nGenerating to temp files:");
GenerateNativeFile(nativeTmp, "ProjectGmKernel.Native.Generated");
GenerateExportsFile(exportsTmp);

// ─────────────────────────────────────────────────────────────────────────────
// Unresolved report with grading (AbiBlocking vs WarningOnly)
// ─────────────────────────────────────────────────────────────────────────────

Directory.CreateDirectory(Path.GetDirectoryName(unresolvedPath)!);

// Grade each unresolved entry
var abiBlocking = new List<(string Type, string Context, string Location)>();
var warningOnly = new List<(string Type, string Context, string Location)>();

foreach (var entry in unresolvedTypes.Distinct())
{
    var (type, context, location) = entry;
    // Blocking: struct/union size unknown, array element size unknown,
    //           pointer signedness lost, function param unresolvable
    if (location is "array-element-size-unknown" or "array-element-union"
        or "func-param" or "pointer" or "struct-field")
        abiBlocking.Add(entry);
    else
        warningOnly.Add(entry);
}

var report = new StringBuilder();
report.AppendLine("# Unresolved Types Report");
report.AppendLine();
report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
report.AppendLine();

if (abiBlocking.Count == 0 && warningOnly.Count == 0 && unresolvedConstCount == 0)
{
    report.AppendLine("No unresolved types or constants.");
}
else
{
    if (abiBlocking.Count > 0)
    {
        report.AppendLine($"## ABI-Blocking ({abiBlocking.Count})");
        report.AppendLine();
        report.AppendLine("These must be resolved before the generated code is ABI-correct.");
        report.AppendLine();
        report.AppendLine("| Type | Context | Location |");
        report.AppendLine("|------|---------|----------|");
        foreach (var (type, context, location) in abiBlocking.OrderBy(t => t.Type))
            report.AppendLine($"| `{NormalizeReportText(type, repoRoot)}` | `{context}` | {location} |");
        report.AppendLine();
    }

    if (warningOnly.Count > 0)
    {
        report.AppendLine($"## Warning-Only ({warningOnly.Count})");
        report.AppendLine();
        report.AppendLine("These do not affect ABI correctness.");
        report.AppendLine();
        report.AppendLine("| Type | Context | Location |");
        report.AppendLine("|------|---------|----------|");
        foreach (var (type, context, location) in warningOnly.OrderBy(t => t.Type))
            report.AppendLine($"| `{NormalizeReportText(type, repoRoot)}` | `{context}` | {location} |");
        report.AppendLine();
    }

    if (unresolvedConstCount > 0)
    {
        report.AppendLine($"## Unresolved Constants ({unresolvedConstCount})");
        report.AppendLine();
        foreach (var expr in unresolvedExprs)
            report.AppendLine($"- `{expr.Name}` = `(({expr.Ref} {expr.Op} {expr.Offset}))`");
        foreach (var r in unresolvedRefs)
            report.AppendLine($"- `{r.Name}` = `{r.Ref}`");
        report.AppendLine();
    }
}

File.WriteAllText(unresolvedPath, report.ToString(), new UTF8Encoding(false));
Console.WriteLine($"\n  {Path.GetRelativePath(repoRoot, unresolvedPath)}");

// ─────────────────────────────────────────────────────────────────────────────
// Phase 2: Temporary promotion for validation
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== Validation Staging ===");
var nativeSnapshot = CaptureSnapshot(nativeOut);
var exportsSnapshot = CaptureSnapshot(exportsOut);

File.Copy(nativeTmp, nativeOut, true);
File.Copy(exportsTmp, exportsOut, true);
Console.WriteLine("  Promoted temp outputs for validation build.");

// ─────────────────────────────────────────────────────────────────────────────
// Phase 3: Build validation
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== Build Validation ===");

var buildOk = true;

Console.Write("  Building ProjectGmKernel.Native ... ");
var nativeBuild = RunProcess("dotnet", "build src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj --no-restore -v q", repoRoot);
if (nativeBuild.Ok)
{
    Console.WriteLine("OK");
}
else
{
    Console.WriteLine("FAILED");
    Console.WriteLine(nativeBuild.Output);
    buildOk = false;
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase 4: ABI sizeof validation (real execution)
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== ABI Sizeof Validation ===");
WriteAbiCheckProject();
Console.WriteLine($"  Wrote {Path.GetRelativePath(repoRoot, Path.Combine(abiCheckDir, "Program.cs"))}");
var abiCheck = RunProcess("dotnet", "run --project temp_docs/abi_check/AbiCheck.csproj -v q", repoRoot);
var abiValidationOk = abiCheck.Ok;
if (abiCheck.Output.Length > 0)
    Console.Write(abiCheck.Output);
if (!abiValidationOk && abiCheck.Output.Length == 0)
    Console.WriteLine("ABI check failed with no output.");

var canCommit = allowPartial || (abiBlocking.Count == 0 && buildOk && abiValidationOk);

if (canCommit)
{
    File.Delete(nativeTmp);
    File.Delete(exportsTmp);
    Console.WriteLine("  Validation passed commit gate; promoted outputs kept.");
}
else
{
    RestoreSnapshot(nativeSnapshot);
    RestoreSnapshot(exportsSnapshot);
    File.Delete(nativeTmp);
    File.Delete(exportsTmp);
    Console.WriteLine("  Validation failed commit gate; restored formal generated outputs.");
}

// ─────────────────────────────────────────────────────────────────────────────
// Summary and exit decision
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine("=== Summary ===");
Console.WriteLine($"Typedefs:         {typedefs.Count}");
Console.WriteLine($"Func ptrs:        {funcPtrTypedefs.Count}");
Console.WriteLine($"Direct func ptrs: {directFuncPtrs.Count}");
Console.WriteLine($"Structs:          {structs.Count}");
Console.WriteLine($"Functions:        {functions.Count}");
Console.WriteLine($"Constants:        {constants.Count}");
Console.WriteLine($"Blocking:         {abiBlocking.Count}");
Console.WriteLine($"Warnings:         {warningOnly.Count}");
Console.WriteLine($"Build:            {(buildOk ? "PASS" : "FAIL")}");
Console.WriteLine($"ABI Check:        {(abiValidationOk ? "PASS" : "FAIL")}");
Console.WriteLine($"Committed:        {(canCommit ? "YES" : "NO")}");

if (abiBlocking.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"BLOCKING: {abiBlocking.Count} ABI-blocking unresolved items. See {Path.GetRelativePath(repoRoot, unresolvedPath)}");
    if (!allowPartial)
    {
        Console.WriteLine("Use --allow-partial to keep generated files despite blocking items.");
        return 1;
    }
}

if (!buildOk)
{
    Console.WriteLine();
    Console.WriteLine("BUILD FAILED.");
    if (!allowPartial)
    {
        Console.WriteLine("Use --allow-partial to keep generated files despite build failures.");
        return 1;
    }
}

if (!abiValidationOk)
{
    Console.WriteLine();
    Console.WriteLine("ABI SIZE CHECK FAILED.");
    if (!allowPartial)
    {
        Console.WriteLine("Use --allow-partial to keep generated files despite ABI validation failures.");
        return 1;
    }
}

Console.WriteLine();
Console.WriteLine(canCommit
    ? "Generated files committed."
    : "Generated files were not committed; formal outputs were restored.");
return 0;

sealed class FileSnapshot
{
    public required string Path { get; init; }
    public required bool Existed { get; init; }
    public string? Content { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
}
