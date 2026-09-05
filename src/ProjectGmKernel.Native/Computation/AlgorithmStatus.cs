namespace ProjectGmKernel.Native.Computation;

/// <summary>
/// Internal execution status, not a PK error code or a geometric classification.
/// Only Success makes the result valid, including a successfully computed empty result.
/// </summary>
internal enum AlgorithmStatus : byte
{
    NotRun = 0,
    Success,
    Unsupported,
    InvalidInput,
    WorkspaceTooSmall,
    OutputTooSmall,
    NotConverged,
    NumericalFailure,
    Cancelled,
}
