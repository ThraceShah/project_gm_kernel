using ProjectGmKernel.Native.Runtime;

internal static partial class CorpusManagedKernel
{
    public static byte[] RoundTrip(
        byte[] source,
        int transmitVersion,
        bool userFields = true,
        bool keepCompound = false)
        => XtCorpusInspection.RoundTrip(source, transmitVersion, userFields, keepCompound);
}
