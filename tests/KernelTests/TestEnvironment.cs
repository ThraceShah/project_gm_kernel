using System.Runtime.CompilerServices;

namespace KernelTests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void ConfigurePrivateSchemaDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("P_SCHEMA")))
            return;
        var directory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "third_party", "parasolid", "schema"));
        if (Directory.Exists(directory))
            Environment.SetEnvironmentVariable("P_SCHEMA", directory);
    }
}
