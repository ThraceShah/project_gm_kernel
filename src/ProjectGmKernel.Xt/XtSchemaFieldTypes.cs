using System.Runtime.InteropServices;

namespace ProjectGmKernel.Xt;

[StructLayout(LayoutKind.Sequential)] public struct XtRange { public XtTableOffset Offset; public XtTableCount Count; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaVector { public double X; public double Y; public double Z; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaInterval { public double Low; public double High; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaBox { public double XLow; public double XHigh; public double YLow; public double YHigh; public double ZLow; public double ZHigh; }

/// <summary>
/// Null sentinels for strongly-typed generated schema rows (PGM_XT ABI v2).
/// A schema field is null when its member equals the sentinel for its field kind;
/// members of fields a schema does not transmit are not maintained.
/// </summary>
public static class XtSchemaField
{
    public const int NullPointer = -1;
    public const long NullInteger = long.MinValue;
    public const ulong NullUnsigned = ulong.MaxValue;
    public const double NullReal = double.NaN;
    public const byte NullCharacter = 0xFF;
    public const byte NullLogical = 0xFF;
}

internal static class XtSchemaFieldCodec
{
    internal static int To_p(XtFieldValue value, string field)
    {
        if (IsNull(value)) return XtSchemaField.NullPointer;
        if (value.Pointer < 0) throw Invalid(field, $"pointer {value.Pointer} collides with the null sentinels.");
        return value.Pointer;
    }

    internal static long To_d(XtFieldValue value, string field)
    {
        if (IsNull(value)) return XtSchemaField.NullInteger;
        if (value.Integer == XtSchemaField.NullInteger) throw Invalid(field, "integer value collides with the null sentinel.");
        return value.Integer;
    }

    internal static long To_n(XtFieldValue value, string field) => To_d(value, field);
    internal static long To_w(XtFieldValue value, string field) => To_d(value, field);
    internal static long To_t(XtFieldValue value, string field) => To_d(value, field);
    internal static long To_q(XtFieldValue value, string field) => To_d(value, field);

    internal static ulong To_u(XtFieldValue value, string field)
    {
        if (IsNull(value)) return XtSchemaField.NullUnsigned;
        var raw = unchecked((ulong)value.Integer);
        if (raw == XtSchemaField.NullUnsigned) throw Invalid(field, "unsigned value collides with the null sentinel.");
        return raw;
    }

    internal static double To_f(XtFieldValue value, string field)
    {
        if (IsNull(value)) return XtSchemaField.NullReal;
        EnsureReal(value.Real, field);
        return value.Real;
    }

    internal static byte To_c(XtFieldValue value, string field)
    {
        if (IsNull(value)) return XtSchemaField.NullCharacter;
        var raw = checked((byte)value.Character);
        if (raw == XtSchemaField.NullCharacter) throw Invalid(field, "character value collides with the null sentinel.");
        return raw;
    }

    internal static byte To_l(XtFieldValue value, string field) => IsNull(value) ? XtSchemaField.NullLogical : (byte)(value.Integer == 0 ? 0 : 1);

    internal static XtSchemaVector To_v(XtFieldValue value, string field)
    {
        if (IsNull(value)) return NullVector();
        EnsureReal(value.Vector.X, field);
        EnsureReal(value.Vector.Y, field);
        EnsureReal(value.Vector.Z, field);
        return new XtSchemaVector { X = value.Vector.X, Y = value.Vector.Y, Z = value.Vector.Z };
    }

    internal static XtSchemaVector To_h(XtFieldValue value, string field) => To_v(value, field);

    internal static XtSchemaInterval To_i(XtFieldValue value, string field)
    {
        if (IsNull(value)) return new XtSchemaInterval { Low = XtSchemaField.NullReal, High = XtSchemaField.NullReal };
        EnsureReal(value.Vector.X, field);
        EnsureReal(value.Vector.Y, field);
        return new XtSchemaInterval { Low = value.Vector.X, High = value.Vector.Y };
    }

    internal static XtSchemaBox To_b(XtFieldValue value, string field)
    {
        if (IsNull(value)) return NullBox();
        EnsureReal(value.Vector.X, field);
        EnsureReal(value.Vector.Y, field);
        EnsureReal(value.Vector.Z, field);
        EnsureReal(value.Fourth, field);
        EnsureReal(value.Fifth, field);
        EnsureReal(value.Sixth, field);
        return new XtSchemaBox { XLow = value.Vector.X, XHigh = value.Vector.Y, YLow = value.Vector.Z, YHigh = value.Fourth, ZLow = value.Fifth, ZHigh = value.Sixth };
    }

    internal static XtFieldValue From_p(int value) => value < 0 ? XtFieldValue.Null() : XtFieldValue.Ptr(value);
    internal static XtFieldValue From_d(long value) => value == XtSchemaField.NullInteger ? XtFieldValue.Null() : XtFieldValue.Int(value);
    internal static XtFieldValue From_n(long value) => From_d(value);
    internal static XtFieldValue From_w(long value) => From_d(value);
    internal static XtFieldValue From_t(long value) => From_d(value);
    internal static XtFieldValue From_q(long value) => From_d(value);
    internal static XtFieldValue From_u(ulong value) => value == XtSchemaField.NullUnsigned ? XtFieldValue.Null() : XtFieldValue.Unsigned(checked((long)value));
    internal static XtFieldValue From_f(double value) => double.IsNaN(value) ? XtFieldValue.Null() : XtFieldValue.RealValue(value);
    internal static XtFieldValue From_c(byte value) => value == XtSchemaField.NullCharacter ? XtFieldValue.Null() : XtFieldValue.Char((char)value);
    internal static XtFieldValue From_l(byte value) => value == XtSchemaField.NullLogical ? XtFieldValue.Null() : XtFieldValue.Logical(value != 0);
    internal static XtFieldValue From_v(XtSchemaVector value) => double.IsNaN(value.X) ? XtFieldValue.Null() : XtFieldValue.Vec(value.X, value.Y, value.Z);
    internal static XtFieldValue From_h(XtSchemaVector value) => From_v(value);
    internal static XtFieldValue From_i(XtSchemaInterval value) => double.IsNaN(value.Low) ? XtFieldValue.Null() : XtFieldValue.IntervalValue(value.Low, value.High);
    internal static XtFieldValue From_b(XtSchemaBox value) => double.IsNaN(value.XLow) ? XtFieldValue.Null() : XtFieldValue.BoxValue(value.XLow, value.XHigh, value.YLow, value.YHigh, value.ZLow, value.ZHigh);

    private static bool IsNull(XtFieldValue value) => value.Kind == XtFieldKind.Empty;
    private static XtSchemaVector NullVector() => new() { X = XtSchemaField.NullReal, Y = XtSchemaField.NullReal, Z = XtSchemaField.NullReal };
    private static XtSchemaBox NullBox() => new() { XLow = XtSchemaField.NullReal, XHigh = XtSchemaField.NullReal, YLow = XtSchemaField.NullReal, YHigh = XtSchemaField.NullReal, ZLow = XtSchemaField.NullReal, ZHigh = XtSchemaField.NullReal };
    private static void EnsureReal(double value, string field)
    {
        if (double.IsNaN(value)) throw Invalid(field, "real value collides with the null sentinel.");
    }
    private static XtFormatException Invalid(string field, string message) => new(XtErrorCode.ModelInvalid, $"Schema field {field}: {message}");
}
