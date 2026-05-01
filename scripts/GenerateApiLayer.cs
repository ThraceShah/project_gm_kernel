#!/usr/bin/env dotnet run
// Parse Parasolid C headers via libclang and generate the complete C# API layer.
//
// Generates:
//   src/ProjectGmKernel.Native/Generated/ParasolidHeader.generated.cs  (export types, NO DllImport)
//   src/ProjectGmKernel.Interop/Generated/ParasolidNative.generated.cs (test/validation DllImport)
//
// Usage: dotnet run scripts/GenerateApiLayer.cs -p:AllowUnsafeBlocks=true [-- --allow-partial]

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// ─────────────────────────────────────────────────────────────────────────────
// Command-line args
// ─────────────────────────────────────────────────────────────────────────────

var cmdLineArgs = Environment.GetCommandLineArgs();
var allowPartial = Array.Exists(cmdLineArgs, a => a == "--allow-partial");

// ─────────────────────────────────────────────────────────────────────────────
// Paths (relative to repo root = cwd)
// ─────────────────────────────────────────────────────────────────────────────

var repoRoot = Directory.GetCurrentDirectory();
var incDir = Path.Combine(repoRoot, "docs", "parasolid_inc");
var nativeOut = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "Generated", "ParasolidHeader.generated.cs");
var interopOut = Path.Combine(repoRoot, "src", "ProjectGmKernel.Interop", "Generated", "ParasolidNative.generated.cs");
var unresolvedPath = Path.Combine(repoRoot, "temp_docs", "unresolved.md");
var abiCheckDir = Path.Combine(repoRoot, "temp_docs", "abi_check");

var mainHeader = Path.Combine(incDir, "parasolid_kernel.h");
string[] tokenHeaders = [
    Path.Combine(incDir, "parasolid_tokens.h"),
    Path.Combine(incDir, "parasolid_ifails.h"),
    Path.Combine(incDir, "frustrum_tokens.h"),
    Path.Combine(incDir, "frustrum_ifails.h"),
];

// ─────────────────────────────────────────────────────────────────────────────
// libclang discovery
// ─────────────────────────────────────────────────────────────────────────────

static string FindLibClang()
{
    // Environment variable takes priority
    var envPath = Environment.GetEnvironmentVariable("LIBCLANG_PATH");
    if (!string.IsNullOrEmpty(envPath))
    {
        if (File.Exists(envPath)) return envPath;
        // If it's a directory, look for the library inside
        if (Directory.Exists(envPath))
        {
            var dylib = Path.Combine(envPath, "libclang.dylib");
            var so = Path.Combine(envPath, "libclang.so");
            if (File.Exists(dylib)) return dylib;
            if (File.Exists(so)) return so;
        }
    }

    string[] candidates = [
        "/Library/Developer/CommandLineTools/usr/lib/libclang.dylib",
        "/opt/homebrew/Cellar/llvm/22.1.4/lib/libclang.dylib",
        "/opt/homebrew/Cellar/llvm@21/21.1.8/lib/libclang.dylib",
        "/opt/homebrew/Cellar/llvm@20/20.1.8/lib/libclang.dylib",
    ];
    foreach (var c in candidates)
        if (File.Exists(c)) return c;
    throw new FileNotFoundException(
        "libclang not found. Set LIBCLANG_PATH environment variable or install LLVM.");
}

static string FindSysroot()
{
    var psi = new System.Diagnostics.ProcessStartInfo("xcrun", "--show-sdk-path")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    var proc = System.Diagnostics.Process.Start(psi)!;
    proc.WaitForExit();
    return proc.StandardOutput.ReadToEnd().Trim();
}

var libclangPath = FindLibClang();
var sysroot = FindSysroot();

NativeLibrary.Load(libclangPath);

// ─────────────────────────────────────────────────────────────────────────────
// Cursor kind & type kind constants (from libclang C API)
// ─────────────────────────────────────────────────────────────────────────────

const int CXCursor_StructDecl = 2;
const int CXCursor_UnionDecl = 3;
const int CXCursor_FieldDecl = 6;
const int CXCursor_FunctionDecl = 8;
const int CXCursor_ParmDecl = 10;
const int CXCursor_TypedefDecl = 20;

const int CXType_Void = 2;
const int CXType_Bool = 3;
const int CXType_Char_U = 4;
const int CXType_UChar = 5;
const int CXType_UShort = 8;
const int CXType_UInt = 9;
const int CXType_ULong = 10;
const int CXType_ULongLong = 11;
const int CXType_Char_S = 13;
const int CXType_SChar = 14;
const int CXType_Short = 16;
const int CXType_Int = 17;
const int CXType_Long = 18;      // 8 bytes on LP64 (macOS arm64)
const int CXType_LongLong = 19;
const int CXType_Float = 21;
const int CXType_Double = 22;
const int CXType_LongDouble = 23;
const int CXType_Pointer = 101;
const int CXType_Record = 105;
const int CXType_Enum = 106;
const int CXType_Typedef = 107;
const int CXType_FunctionNoProto = 110;
const int CXType_FunctionProto = 111;
const int CXType_ConstantArray = 112;
const int CXType_IncompleteArray = 114;
const int CXType_Elaborated = 119;

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static string ToManagedString(CXString cx)
{
    var ptr = LibClang.clang_getCString(cx);
    var result = ptr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(ptr) ?? "";
    LibClang.clang_disposeString(cx);
    return result;
}

static string GetSpelling(CXCursor cursor) => ToManagedString(LibClang.clang_getCursorSpelling(cursor));

static string GetTypeSpelling(CXType type)
{
    var s = ToManagedString(LibClang.clang_getTypeSpelling(type));
    s = Regex.Replace(s, @"\bconst\b\s*", "");
    s = s.Replace("*[]", "*");
    return s;
}

static string StripAggregateKeyword(string typeName)
{
    if (typeName.StartsWith("struct "))
        return typeName[7..];
    if (typeName.StartsWith("union "))
        return typeName[6..];
    return typeName;
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
        current = LibClang.clang_Type_getNamedType(current);

    var value = LibClang.clang_Type_getSizeOf(current);
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

static string CTypeKindToCSharp(int kind) => kind switch
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

// Resolve a pointer's pointee type to the correct C# pointer type string.
// Preserves signedness: int* vs uint*, short* vs ushort*, etc.
static string ResolvePointerType(int pointeeKind, string pointeeSpelling)
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
        var named = LibClang.clang_Type_getNamedType(type);
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

        var canonical = LibClang.clang_getCanonicalType(type);

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
        var pointee = LibClang.clang_getPointeeType(type);

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

            var ptCanon = LibClang.clang_getCanonicalType(pointee);

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
            var inner = LibClang.clang_Type_getNamedType(pointee);
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
        var elemType = LibClang.clang_getArrayElementType(type);
        return ResolveCType(elemType, inField, context);
    }

    // Incomplete array (pointer semantics)
    if (kind == CXType_IncompleteArray)
    {
        var elemType = LibClang.clang_getArrayElementType(type);
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

// ─────────────────────────────────────────────────────────────────────────────
// Parsing
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("=== Parasolid Header -> C# API Layer Generator ===");
Console.WriteLine($"libclang: {libclangPath}");
Console.WriteLine($"sysroot:  {sysroot}");
Console.WriteLine($"partial:  {allowPartial}");
Console.WriteLine();

Console.Write("Parsing parasolid_kernel.h ... ");

var idx = LibClang.clang_createIndex(0, 0);

unsafe
{
    var arg0 = Marshal.StringToHGlobalAnsi("-xc");
    var arg1 = Marshal.StringToHGlobalAnsi("-std=c11");
    var arg2 = Marshal.StringToHGlobalAnsi("-isysroot");
    var arg3 = Marshal.StringToHGlobalAnsi(sysroot);
    var arg4 = Marshal.StringToHGlobalAnsi("-I" + incDir);
    var arg5 = Marshal.StringToHGlobalAnsi("-Wno-everything");

    var cmdArgs = stackalloc byte*[6];
    cmdArgs[0] = (byte*)arg0;
    cmdArgs[1] = (byte*)arg1;
    cmdArgs[2] = (byte*)arg2;
    cmdArgs[3] = (byte*)arg3;
    cmdArgs[4] = (byte*)arg4;
    cmdArgs[5] = (byte*)arg5;

    var tu = LibClang.clang_parseTranslationUnit(idx, mainHeader, cmdArgs, 6, IntPtr.Zero, 0, 0);
    if (tu == IntPtr.Zero)
    {
        Console.WriteLine("FAILED to parse translation unit");
        return 1;
    }

    var cursor = LibClang.clang_getTranslationUnitCursor(tu);

    // Pass 1: Collect all function pointer typedefs first (struct fields may reference them)
    CXCursorVisitor funcPtrCollector = (child, parent, _) =>
    {
        var kind = LibClang.clang_getCursorKind(child);
        if (kind != CXCursor_TypedefDecl) return CXChildVisitResult.Continue;

        var underlying = LibClang.clang_getTypedefDeclUnderlyingType(child);
        var funcType = underlying;
        if (underlying.kind == CXType_Pointer)
        {
            var pt = LibClang.clang_getPointeeType(underlying);
            if (pt.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                funcType = pt;
        }

        if (funcType.kind is CXType_FunctionProto or CXType_FunctionNoProto)
        {
            var name = GetSpelling(child);
            var retType = LibClang.clang_getResultType(funcType);
            var csRet = CTypeKindToCSharp(retType.kind);
            if (csRet.Length == 0)
            {
                var retSpell = GetTypeSpelling(retType);
                csRet = retSpell.Length > 0 ? retSpell : "void";
            }

            var fparams = new List<(string Name, string Type)>();
            var numArgs = LibClang.clang_getNumArgTypes(funcType);

            var paramNames = new List<string>();
            CXCursorVisitor fpParamVisitor = (pc, _, _) =>
            {
                if (LibClang.clang_getCursorKind(pc) == CXCursor_ParmDecl)
                    paramNames.Add(GetSpelling(pc));
                return CXChildVisitResult.Continue;
            };
            LibClang.clang_visitChildren(child, fpParamVisitor, IntPtr.Zero);

            for (uint i = 0; i < (uint)numArgs; i++)
            {
                var argType = LibClang.clang_getArgType(funcType, i);
                var pname = i < paramNames.Count ? paramNames[(int)i] : $"arg{i}";
                if (string.IsNullOrEmpty(pname)) pname = $"arg{i}";
                var csType = CTypeKindToCSharp(argType.kind);
                if (csType.Length == 0)
                {
                    // Array params in C are pointers; resolve element type
                    if (argType.kind is CXType_ConstantArray or CXType_IncompleteArray)
                    {
                        var elemType = LibClang.clang_getArrayElementType(argType);
                        var elemCs = CTypeKindToCSharp(elemType.kind);
                        csType = elemCs.Length > 0 ? elemCs + "*" : "nint";
                    }
                    else
                    {
                        var argSpell = GetTypeSpelling(argType);
                        csType = argSpell.Length > 0 ? argSpell : "nint";
                    }
                }
                fparams.Add((pname, csType));
            }

            funcPtrTypedefs.Add((name, csRet, fparams));
        }
        return CXChildVisitResult.Continue;
    };
    LibClang.clang_visitChildren(cursor, funcPtrCollector, IntPtr.Zero);

    // Pass 2: Collect typedefs, structs, functions, and constants
    CXCursorVisitor topLevelVisitor = (child, parent, _) =>
    {
        var kind = LibClang.clang_getCursorKind(child);
        var spelling = GetSpelling(child);

        if (kind == CXCursor_TypedefDecl)
        {
            var underlying = LibClang.clang_getTypedefDeclUnderlyingType(child);

            // Skip function pointer typedefs — already collected in Pass 1
            if (funcPtrTypedefs.Exists(f => f.Name == spelling))
                return CXChildVisitResult.Continue;

            var canonical = LibClang.clang_getCanonicalType(underlying);
            var underlyingSp = GetTypeSpelling(underlying);

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
                typedefs.Add((spelling, csType.Length > 0 ? csType : underlyingSp));
            }
        }
        else if (kind == CXCursor_StructDecl || kind == CXCursor_UnionDecl)
        {
            if (spelling.Length == 0)
                return CXChildVisitResult.Continue;

            var fields = new List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsArray, long ArrSize, string Comment)>();

            CXCursorVisitor fieldVisitor = (fieldCursor, _, _) =>
            {
                var fieldKind = LibClang.clang_getCursorKind(fieldCursor);

                // Handle union children in structs
                if (fieldKind == CXCursor_UnionDecl)
                {
                    var unionFieldName = GetSpelling(fieldCursor);
                    if (string.IsNullOrEmpty(unionFieldName) || unionFieldName.Contains(' ') || unionFieldName.Contains('('))
                        unionFieldName = $"_union_{fields.Count}";

                    var unionType = LibClang.clang_getCursorType(fieldCursor);
                    if (TryGetClangTypeSize(unionType, out var unionSize))
                    {
                        fields.Add((unionFieldName, "byte", false, false, true, unionSize,
                            $"ABI storage for anonymous union ({unionSize} bytes)"));
                    }
                    else
                    {
                        fields.Add((unionFieldName, "nint", true, false, false, 0, ""));
                        RecordUnresolved(GetTypeSpelling(unionType), $"{spelling}.{unionFieldName}", "struct-field");
                    }
                    return CXChildVisitResult.Continue;
                }

                if (fieldKind != CXCursor_FieldDecl)
                    return CXChildVisitResult.Continue;

                var fname = GetSpelling(fieldCursor);
                var ftype = LibClang.clang_getCursorType(fieldCursor);
                var fkind = ftype.kind;
                var isConst = LibClang.clang_isConstQualifiedType(ftype) != 0;

                if (fkind == CXType_Elaborated)
                {
                    ftype = LibClang.clang_Type_getNamedType(ftype);
                    fkind = ftype.kind;
                }

                // Step 4a: Flatten multi-dimensional arrays
                if (fkind == CXType_ConstantArray)
                {
                    long totalSize = 1;
                    var currentType = ftype;
                    while (currentType.kind == CXType_ConstantArray)
                    {
                        totalSize *= LibClang.clang_getArraySize(currentType);
                        currentType = LibClang.clang_getArrayElementType(currentType);
                    }
                    // Unwrap elaborated element type
                    if (currentType.kind == CXType_Elaborated)
                        currentType = LibClang.clang_Type_getNamedType(currentType);
                    var csElem = ResolveCType(currentType, true, $"{spelling}.{fname}");
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
                        return CXChildVisitResult.Continue;
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
                    return CXChildVisitResult.Continue;
                }

                // Check for function pointer field (pointer to function or pointer to function typedef)
                if (fkind == CXType_Pointer)
                {
                    var pointee = LibClang.clang_getPointeeType(ftype);

                    // Direct function pointer: void (*)(int) — generate a named delegate
                    if (pointee.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                    {
                        var delegateName = $"{spelling}_{fname}_f_t";
                        var retType = LibClang.clang_getResultType(pointee);
                        var csRet = CTypeKindToCSharp(retType.kind);
                        if (csRet.Length == 0)
                        {
                            var retSpell = GetTypeSpelling(retType);
                            csRet = retSpell.Length > 0 ? retSpell : "void";
                        }

                        var fpParams = new List<(string Name, string Type)>();
                        var numArgs = LibClang.clang_getNumArgTypes(pointee);

                        // Collect parameter names from cursor children
                        var paramNames = new List<string>();
                        CXCursorVisitor fpParamVisitor = (pc, _, _) =>
                        {
                            if (LibClang.clang_getCursorKind(pc) == CXCursor_ParmDecl)
                                paramNames.Add(GetSpelling(pc));
                            return CXChildVisitResult.Continue;
                        };
                        LibClang.clang_visitChildren(fieldCursor, fpParamVisitor, IntPtr.Zero);

                        for (uint ai = 0; ai < (uint)numArgs; ai++)
                        {
                            var argType = LibClang.clang_getArgType(pointee, ai);
                            var pname = ai < paramNames.Count ? paramNames[(int)ai] : $"arg{ai}";
                            if (string.IsNullOrEmpty(pname)) pname = $"arg{ai}";
                            var csArgType = CTypeKindToCSharp(argType.kind);
                            if (csArgType.Length == 0)
                            {
                                if (argType.kind is CXType_ConstantArray or CXType_IncompleteArray)
                                {
                                    var elemType = LibClang.clang_getArrayElementType(argType);
                                    var elemCs = CTypeKindToCSharp(elemType.kind);
                                    csArgType = elemCs.Length > 0 ? elemCs + "*" : "nint";
                                }
                                else if (argType.kind == CXType_Pointer)
                                {
                                    var pt = LibClang.clang_getPointeeType(argType);
                                    if (pt.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                                        csArgType = "nint";
                                    else
                                        csArgType = ResolveCType(argType, false, $"{spelling}.{fname}");
                                }
                                else
                                {
                                    var argSpell = GetTypeSpelling(argType);
                                    csArgType = argSpell.Length > 0 ? argSpell : "nint";
                                }
                            }
                            fpParams.Add((pname, csArgType));
                        }

                        directFuncPtrs.Add((delegateName, csRet, fpParams));
                        fields.Add((fname, delegateName, true, isConst, false, 0, ""));
                        return CXChildVisitResult.Continue;
                    }

                    // Typedef'd function pointer: PK_SESSION_start_f_t
                    if (pointee.kind == CXType_Typedef)
                    {
                        var ptSpell = GetTypeSpelling(pointee);
                        if (funcPtrTypedefs.Exists(f => f.Name == ptSpell))
                        {
                            fields.Add((fname, ptSpell, true, isConst, false, 0, ""));
                            return CXChildVisitResult.Continue;
                        }
                        // Typedef resolves to function pointer?
                        var ptCanon = LibClang.clang_getCanonicalType(pointee);
                        if (ptCanon.kind is CXType_FunctionProto or CXType_FunctionNoProto)
                        {
                            fields.Add((fname, ptSpell, true, isConst, false, 0, ""));
                            return CXChildVisitResult.Continue;
                        }
                    }
                }

                var csType = ResolveCType(ftype, true, $"{spelling}.{fname}");
                if (csType.Contains("union") || csType.Contains("unnamed") || csType.Contains('('))
                {
                    if (TryGetClangTypeSize(ftype, out var unionSize))
                    {
                        fields.Add((fname, "byte", false, isConst, true, unionSize,
                            $"ABI storage for {NormalizeReportText(GetTypeSpelling(ftype), repoRoot)} ({unionSize} bytes)"));
                        return CXChildVisitResult.Continue;
                    }

                    RecordUnresolved(GetTypeSpelling(ftype), $"{spelling}.{fname}", "struct-field");
                    csType = "nint";
                }
                var isPtr = csType.Contains('*') || csType == "nint";
                fields.Add((fname, csType, isPtr, isConst, false, 0, ""));

                return CXChildVisitResult.Continue;
            };
            LibClang.clang_visitChildren(child, fieldVisitor, IntPtr.Zero);

            if (kind == CXCursor_StructDecl)
                structs.Add((spelling, fields));
            else
                unions.Add((spelling, fields));
        }
        else if (kind == CXCursor_FunctionDecl)
        {
            if (!spelling.StartsWith("PK_"))
                return CXChildVisitResult.Continue;

            var funcType = LibClang.clang_getCursorType(child);
            var retType = LibClang.clang_getResultType(funcType);
            var csRet = CTypeKindToCSharp(retType.kind);
            if (csRet.Length == 0)
            {
                var retSpell = GetTypeSpelling(retType);
                csRet = retSpell.Length > 0 ? retSpell : "int";
            }

            var numArgs = LibClang.clang_getNumArgTypes(funcType);

            var paramNames = new List<string>();
            CXCursorVisitor paramVisitor = (paramCursor, _, _) =>
            {
                if (LibClang.clang_getCursorKind(paramCursor) == CXCursor_ParmDecl)
                    paramNames.Add(GetSpelling(paramCursor));
                return CXChildVisitResult.Continue;
            };
            LibClang.clang_visitChildren(child, paramVisitor, IntPtr.Zero);

            var parameters = new List<(string Name, string Type, bool IsPtr, bool IsConst, bool IsDoublePtr)>();

            for (uint i = 0; i < (uint)numArgs; i++)
            {
                var argType = LibClang.clang_getArgType(funcType, i);
                var pname = i < paramNames.Count ? paramNames[(int)i] : $"arg{i}";
                if (string.IsNullOrEmpty(pname)) pname = $"arg{i}";

                var isConst = LibClang.clang_isConstQualifiedType(argType) != 0;
                var isDoublePtr = false;

                if (argType.kind == CXType_Pointer)
                {
                    var pointee = LibClang.clang_getPointeeType(argType);
                    if (pointee.kind == CXType_Pointer)
                        isDoublePtr = true;
                }

                var pCsType = ResolveCType(argType, false, $"{spelling}({pname})");
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

        return CXChildVisitResult.Continue;
    };
    LibClang.clang_visitChildren(cursor, topLevelVisitor, IntPtr.Zero);

    LibClang.clang_disposeTranslationUnit(tu);

    Marshal.FreeHGlobal(arg0);
    Marshal.FreeHGlobal(arg1);
    Marshal.FreeHGlobal(arg2);
    Marshal.FreeHGlobal(arg3);
    Marshal.FreeHGlobal(arg4);
    Marshal.FreeHGlobal(arg5);
}

LibClang.clang_disposeIndex(idx);

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
    if (depth > 10) return name;
    if (!typedefMap.TryGetValue(name, out var target)) return name;
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

void GenerateUsingAliases(StringBuilder sb)
{
    foreach (var td in typedefs)
    {
        var target = ResolveChain(td.Name);
        if (target == td.Name) continue;

        if (target is "int" or "uint" or "nuint" or "byte" or "short" or "ushort"
            or "long" or "ulong" or "float" or "double" or "nint")
        {
            sb.AppendLine($"using {td.Name} = {target};");
        }
        else if (!target.Contains('*'))
        {
            if (target.Contains(' ') || target.Contains("union") || target.Contains("unnamed") || target.Contains('(')
                || target.StartsWith("struct "))
            {
                sb.AppendLine($"using {td.Name} = nint;");
                RecordUnresolved(target, td.Name, "typedef-alias");
            }
            else
            {
                sb.AppendLine($"using {td.Name} = {target};");
            }
        }
    }
}

void GenerateDelegates(StringBuilder sb, string access)
{
    // Typedef'd function pointers
    foreach (var fp in funcPtrTypedefs)
    {
        sb.AppendLine($"    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
        var parms = string.Join(", ", fp.Params.Select(p => $"{p.Type} {SanitizeName(p.Name)}"));
        var hasPtr = fp.RetType.Contains('*') || fp.Params.Any(p => p.Type.Contains('*'));
        var unsafeKw = hasPtr ? "unsafe " : "";
        sb.AppendLine($"    {access} {unsafeKw}delegate {fp.RetType} {fp.Name}({parms});");
        sb.AppendLine();
    }

    // Direct function pointer delegates (generated for struct fields)
    foreach (var fp in directFuncPtrs)
    {
        sb.AppendLine($"    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
        var parms = string.Join(", ", fp.Params.Select(p => $"{p.Type} {SanitizeName(p.Name)}"));
        var hasPtr = fp.RetType.Contains('*') || fp.Params.Any(p => p.Type.Contains('*'));
        var unsafeKw = hasPtr ? "unsafe " : "";
        sb.AppendLine($"    {access} {unsafeKw}delegate {fp.RetType} {fp.Name}({parms});");
        sb.AppendLine();
    }
}

void GenerateStructs(StringBuilder sb, string access)
{
    foreach (var s in structs.OrderBy(s => StructSortKeyByName(s.Name)).ThenBy(s => s.Name))
    {
        var isUnsafe = s.Fields.Exists(f => f.IsPtr || f.IsArray);
        sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
        sb.AppendLine(isUnsafe ? $"    {access} unsafe struct {s.Name}" : $"    {access} struct {s.Name}");
        sb.AppendLine("    {");
        foreach (var f in s.Fields)
        {
            var fname = SanitizeName(f.Name);
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

void GenerateUnions(StringBuilder sb, string access)
{
    foreach (var u in unions.OrderBy(u => u.Name))
    {
        var isUnsafe = u.Fields.Exists(f => f.IsPtr || f.IsArray);
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
    sb.AppendLine("// Parasolid ABI types, delegates, constants, and export metadata.");
    sb.AppendLine("// This file is the primary generated artifact for the NativeAOT shared library.");
    sb.AppendLine("// It does NOT contain DllImport — the Native project IS the native library.");
    sb.AppendLine();
    sb.AppendLine("using System.Runtime.InteropServices;");
    sb.AppendLine();
    sb.AppendLine($"namespace {ns};");
    sb.AppendLine();

    // Using aliases
    GenerateUsingAliases(sb);
    sb.AppendLine();

    // Delegate types for function pointers
    if (funcPtrTypedefs.Count > 0 || directFuncPtrs.Count > 0)
    {
        sb.AppendLine("    // Function pointer delegate types");
        GenerateDelegates(sb, "internal");
    }

    if (unions.Count > 0)
    {
        sb.AppendLine("    // ABI union definitions");
        GenerateUnions(sb, "internal");
    }

    // Structs
    sb.AppendLine("    // ABI struct definitions");
    GenerateStructs(sb, "internal");

    // Export metadata (function name constants)
    sb.AppendLine("    // Export function name metadata");
    sb.AppendLine("    internal static class ParasolidExports");
    sb.AppendLine("    {");
    sb.AppendLine($"        public const int FunctionCount = {functions.Count};");
    sb.AppendLine();
    foreach (var func in functions.OrderBy(f => f.Name))
        sb.AppendLine($"        public const string {func.Name} = \"{func.Name}\";");
    sb.AppendLine("    }");
    sb.AppendLine();

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

// ─────────────────────────────────────────────────────────────────────────────
// Interop file generator (DllImport wrappers — test/validation only)
// ─────────────────────────────────────────────────────────────────────────────

void GenerateInteropFile(string outputPath, string ns)
{
    var sb = new StringBuilder(1 << 20);
    sb.AppendLine("// <auto-generated />");
    sb.AppendLine("// Parasolid API bindings with DllImport wrappers.");
    sb.AppendLine("// THIS FILE IS FOR TEST/VALIDATION PURPOSES ONLY.");
    sb.AppendLine("// The Native project exports these functions directly via [UnmanagedCallersOnly].");
    sb.AppendLine("// Use this file to P/Invoke into the native library from managed test code.");
    sb.AppendLine();
    sb.AppendLine("using System.Runtime.InteropServices;");
    sb.AppendLine();
    sb.AppendLine($"namespace {ns};");
    sb.AppendLine();

    // Using aliases
    GenerateUsingAliases(sb);
    sb.AppendLine();

    // Delegate types for function pointers
    if (funcPtrTypedefs.Count > 0 || directFuncPtrs.Count > 0)
    {
        sb.AppendLine("    // Function pointer delegate types");
        GenerateDelegates(sb, "public");
    }

    if (unions.Count > 0)
    {
        sb.AppendLine("    // ABI union definitions");
        GenerateUnions(sb, "public");
    }

    // Structs
    sb.AppendLine("    // ABI struct definitions");
    GenerateStructs(sb, "public");

    // DllImport function declarations
    sb.AppendLine("    // DllImport wrappers (test/validation only)");
    sb.AppendLine("    public static unsafe class ParasolidNative");
    sb.AppendLine("    {");
    foreach (var func in functions.OrderBy(f => f.Name))
    {
        var parms = string.Join(", ", func.Params.Select(p =>
        {
            var pt = p.IsDoublePtr ? "nint" : p.Type;
            return $"{pt} {SanitizeName(p.Name)}";
        }));
        sb.AppendLine($"        [DllImport(\"ProjectGmKernel.Native\", EntryPoint = \"{func.Name}\")]");
        sb.AppendLine($"        public static extern {func.RetType} {func.Name}({parms});");
        sb.AppendLine();
    }
    sb.AppendLine("    }");
    sb.AppendLine();

    // Constants
    GenerateConstants(sb, "public");

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
    <ProjectReference Include="../../src/ProjectGmKernel.Interop/ProjectGmKernel.Interop.csproj" />
  </ItemGroup>
</Project>
""";

    var program = """
using System.Runtime.InteropServices;
using ProjectGmKernel.Interop.Generated;

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
var interopTmp = interopOut + ".tmp";

Console.WriteLine("\nGenerating to temp files:");
GenerateNativeFile(nativeTmp, "ProjectGmKernel.Native.Generated");
GenerateInteropFile(interopTmp, "ProjectGmKernel.Interop.Generated");

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
var interopSnapshot = CaptureSnapshot(interopOut);

File.Copy(nativeTmp, nativeOut, true);
File.Copy(interopTmp, interopOut, true);
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

Console.Write("  Building ProjectGmKernel.Interop ... ");
var interopBuild = RunProcess("dotnet", "build src/ProjectGmKernel.Interop/ProjectGmKernel.Interop.csproj --no-restore -v q", repoRoot);
if (interopBuild.Ok)
{
    Console.WriteLine("OK");
}
else
{
    Console.WriteLine("FAILED");
    Console.WriteLine(interopBuild.Output);
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
    File.Delete(interopTmp);
    Console.WriteLine("  Validation passed commit gate; promoted outputs kept.");
}
else
{
    RestoreSnapshot(nativeSnapshot);
    RestoreSnapshot(interopSnapshot);
    File.Delete(nativeTmp);
    File.Delete(interopTmp);
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

// ─────────────────────────────────────────────────────────────────────────────
// libclang P/Invoke types & bindings (must come AFTER top-level statements)
// ─────────────────────────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
struct CXString
{
    public IntPtr data;
    public uint private_flags;
}

[StructLayout(LayoutKind.Sequential)]
struct CXCursor
{
    public int kind;
    public int xdata;
    public IntPtr data0, data1, data2;
}

[StructLayout(LayoutKind.Sequential)]
struct CXType
{
    public int kind;
    public IntPtr data0, data1;
}

enum CXChildVisitResult : int { Break = 0, Continue = 1, Recurse = 2 }

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate CXChildVisitResult CXCursorVisitor(CXCursor cursor, CXCursor parent, IntPtr clientData);

sealed class FileSnapshot
{
    public required string Path { get; init; }
    public required bool Existed { get; init; }
    public string? Content { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
}

static partial class LibClang
{
    const string Dll = "libclang";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr clang_createIndex(int excludeDeclarationsFromPCH, int displayDiagnostics);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void clang_disposeIndex(IntPtr index);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe IntPtr clang_parseTranslationUnit(
        IntPtr cxIdx, string sourceFilename,
        byte** commandLineArgs, int numCommandLineArgs,
        IntPtr unsavedFiles, uint numUnsavedFiles, uint options);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void clang_disposeTranslationUnit(IntPtr tu);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXCursor clang_getTranslationUnitCursor(IntPtr tu);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint clang_visitChildren(CXCursor parent, CXCursorVisitor visitor, IntPtr clientData);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXString clang_getCursorSpelling(CXCursor cursor);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int clang_getCursorKind(CXCursor cursor);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getCursorType(CXCursor cursor);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getTypedefDeclUnderlyingType(CXCursor cursor);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXString clang_getTypeSpelling(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getPointeeType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getArrayElementType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern long clang_getArraySize(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int clang_getNumArgTypes(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getArgType(CXType type, uint index);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getResultType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint clang_isConstQualifiedType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_getCanonicalType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern CXType clang_Type_getNamedType(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern long clang_Type_getSizeOf(CXType type);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr clang_getCString(CXString str);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void clang_disposeString(CXString str);
}
