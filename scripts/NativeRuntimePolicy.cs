using System.Diagnostics;

// Project-owned comparison process policy. Never mutate the user's global
// environment, and never try to set JIT startup knobs after the CLR started.
internal static class NativeRuntimePolicy
{
    internal static void Prepare(ProcessStartInfo info)
    {
        var inherited=info.Environment.ToArray();info.Environment.Clear();
        foreach(var entry in inherited)
            if(entry.Key is "PATH" or "HOME" or "TMPDIR" or "TMP" or "TEMP" or "LANG" or "LC_ALL" or "DOTNET_ROOT" or "DOTNET_ROOT_X64" or "DOTNET_ROOT_ARM64")
                info.Environment[entry.Key]=entry.Value;
        info.Environment["MSBUILDDISABLENODEREUSE"]="1";
        foreach(var name in Names)info.Environment[name]=name=="DOTNET_ReadyToRun"?"0":"1";
    }

    private static readonly string[] Names=["DOTNET_TieredCompilation","DOTNET_TieredPGO","DOTNET_TC_QuickJit","DOTNET_TC_QuickJitForLoops","DOTNET_ReadyToRun"];
    private static readonly string[] Properties=["System.Runtime.TieredCompilation","System.Runtime.TieredPGO","System.Runtime.TieredCompilation.QuickJit","System.Runtime.TieredCompilation.QuickJitForLoops"];

    internal static void VerifyStartup()
    {
        var comparison=Environment.GetEnvironmentVariable("GM_JIT_POLICY_COMPARE")=="fixed-il-abba";
        for(var i=0;i<Names.Length;i++)
        {
            var value=Environment.GetEnvironmentVariable(Names[i]);var expected=i==4?"0":"1";
            if(i==0&&comparison&&value=="0")expected="0";
            if(value!=expected)throw new InvalidOperationException("Start PK/GM comparisons through scripts/RunNativeOracle.cs; startup policy mismatch: "+Names[i]);
            if(i<Properties.Length)
            {
                if(!bool.TryParse(AppContext.GetData(Properties[i])?.ToString(),out var configured))throw new InvalidOperationException("Missing explicit runtimeconfig property "+Properties[i]);
                if(configured!=(value=="1"))throw new InvalidOperationException("Conflicting runtimeconfig/environment property "+Properties[i]);
            }
        }
        foreach(System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            if(((string)entry.Key).StartsWith("COMPlus_",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unqualified COMPlus runtime override");
        Console.WriteLine("RUNTIME_POLICY tiered="+Environment.GetEnvironmentVariable(Names[0])+" pgo=1 quick-jit=1 quick-jit-loops=1 ready-to-run=0 runtimeconfig=matched comparison="+comparison);
    }
}
