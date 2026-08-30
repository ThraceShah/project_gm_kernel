namespace ProjectGmKernel.Xt;

public enum XtErrorCode
{
    None = 0,
    InvalidArgument,
    InvalidHeader,
    InvalidData,
    SchemaDirectoryNotFound,
    SchemaNotFound,
    SchemaMalformed,
    SchemaMismatch,
    InvalidText,
    UnsupportedFormat,
    UnsupportedVersion,
    ModelInvalid,
    NotRepresentable,
    OutOfMemory,
    InvalidHandle,
    InvalidState,
}

public readonly record struct XtDiagnostic(
    XtErrorCode Code,
    string Message,
    XtNodeIndex NodeIndex = -1,
    XtNodeType NodeType = 0,
    string? Field = null);

public sealed class XtFormatException : FormatException
{
    public XtFormatException(XtErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public XtFormatException(XtErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public XtErrorCode Code { get; }
    public XtErrorCode ErrorCode => Code;
}
