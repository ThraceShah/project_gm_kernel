using System.Runtime.InteropServices;

namespace ProjectGmKernel.Xt;

public enum XtSchemaFieldState : byte
{
    Unavailable = 0,
    Null = 1,
    Value = 2,
}

[StructLayout(LayoutKind.Sequential)] public struct XtRange { public XtTableOffset Offset; public XtTableCount Count; }
[StructLayout(LayoutKind.Sequential)] public struct XtVariableRange { public XtSchemaFieldState State; public XtTableOffset Offset; public XtTableCount Count; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaVector { public double X; public double Y; public double Z; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaInterval { public double Low; public double High; }
[StructLayout(LayoutKind.Sequential)] public struct XtSchemaBox { public double XLow; public double XHigh; public double YLow; public double YHigh; public double ZLow; public double ZHigh; }

[StructLayout(LayoutKind.Sequential)] public struct XtField_p { public XtSchemaFieldState State; public XtNodeIndex Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_d { public XtSchemaFieldState State; public long Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_n { public XtSchemaFieldState State; public long Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_w { public XtSchemaFieldState State; public long Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_t { public XtSchemaFieldState State; public long Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_q { public XtSchemaFieldState State; public long Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_u { public XtSchemaFieldState State; public ulong Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_f { public XtSchemaFieldState State; public double Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_c { public XtSchemaFieldState State; public byte Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_l { public XtSchemaFieldState State; public byte Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_v { public XtSchemaFieldState State; public XtSchemaVector Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_h { public XtSchemaFieldState State; public XtSchemaVector Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_i { public XtSchemaFieldState State; public XtSchemaInterval Value; }
[StructLayout(LayoutKind.Sequential)] public struct XtField_b { public XtSchemaFieldState State; public XtSchemaBox Value; }

internal static class XtSchemaFieldCodec
{
    internal static XtField_p To_p(XtFieldValue v) => new() { State = State(v), Value = v.Pointer };
    internal static XtField_d To_d(XtFieldValue v) => new() { State = State(v), Value = v.Integer };
    internal static XtField_n To_n(XtFieldValue v) => new() { State = State(v), Value = v.Integer };
    internal static XtField_w To_w(XtFieldValue v) => new() { State = State(v), Value = v.Integer };
    internal static XtField_t To_t(XtFieldValue v) => new() { State = State(v), Value = v.Integer };
    internal static XtField_q To_q(XtFieldValue v) => new() { State = State(v), Value = v.Integer };
    internal static XtField_u To_u(XtFieldValue v) => new() { State = State(v), Value = unchecked((ulong)v.Integer) };
    internal static XtField_f To_f(XtFieldValue v) => new() { State = State(v), Value = v.Real };
    internal static XtField_c To_c(XtFieldValue v) => new() { State = State(v), Value = checked((byte)v.Character) };
    internal static XtField_l To_l(XtFieldValue v) => new() { State = State(v), Value = (byte)(v.Integer == 0 ? 0 : 1) };
    internal static XtField_v To_v(XtFieldValue v) => new() { State = State(v), Value = new XtSchemaVector { X = v.Vector.X, Y = v.Vector.Y, Z = v.Vector.Z } };
    internal static XtField_h To_h(XtFieldValue v) => new() { State = State(v), Value = new XtSchemaVector { X = v.Vector.X, Y = v.Vector.Y, Z = v.Vector.Z } };
    internal static XtField_i To_i(XtFieldValue v) => new() { State = State(v), Value = new XtSchemaInterval { Low = v.Vector.X, High = v.Vector.Y } };
    internal static XtField_b To_b(XtFieldValue v) => new() { State = State(v), Value = new XtSchemaBox { XLow = v.Vector.X, XHigh = v.Vector.Y, YLow = v.Vector.Z, YHigh = v.Fourth, ZLow = v.Fifth, ZHigh = v.Sixth } };

    internal static XtFieldValue From(XtField_p v) => Empty(v.State) ?? XtFieldValue.Ptr(v.Value);
    internal static XtFieldValue From(XtField_d v) => Empty(v.State) ?? XtFieldValue.Int(v.Value);
    internal static XtFieldValue From(XtField_n v) => Empty(v.State) ?? XtFieldValue.Int(v.Value);
    internal static XtFieldValue From(XtField_w v) => Empty(v.State) ?? XtFieldValue.Int(v.Value);
    internal static XtFieldValue From(XtField_t v) => Empty(v.State) ?? XtFieldValue.Int(v.Value);
    internal static XtFieldValue From(XtField_q v) => Empty(v.State) ?? XtFieldValue.Int(v.Value);
    internal static XtFieldValue From(XtField_u v) => Empty(v.State) ?? XtFieldValue.Unsigned(checked((long)v.Value));
    internal static XtFieldValue From(XtField_f v) => Empty(v.State) ?? XtFieldValue.RealValue(v.Value);
    internal static XtFieldValue From(XtField_c v) => Empty(v.State) ?? XtFieldValue.Char((char)v.Value);
    internal static XtFieldValue From(XtField_l v) => Empty(v.State) ?? XtFieldValue.Logical(v.Value != 0);
    internal static XtFieldValue From(XtField_v v) => Empty(v.State) ?? XtFieldValue.Vec(v.Value.X, v.Value.Y, v.Value.Z);
    internal static XtFieldValue From(XtField_h v) => Empty(v.State) ?? XtFieldValue.Vec(v.Value.X, v.Value.Y, v.Value.Z);
    internal static XtFieldValue From(XtField_i v) => Empty(v.State) ?? XtFieldValue.IntervalValue(v.Value.Low, v.Value.High);
    internal static XtFieldValue From(XtField_b v) => Empty(v.State) ?? XtFieldValue.BoxValue(v.Value.XLow, v.Value.XHigh, v.Value.YLow, v.Value.YHigh, v.Value.ZLow, v.Value.ZHigh);

    internal static void RequireUnavailable(XtSchemaFieldState state, string field)
    {
        if (state != XtSchemaFieldState.Unavailable)
            throw new XtFormatException(XtErrorCode.ModelInvalid, $"Non-transmitted schema field {field} must be Unavailable.");
    }

    internal static void RequireTransmitted(XtSchemaFieldState state, string field)
    {
        if (state == XtSchemaFieldState.Unavailable)
            throw new XtFormatException(XtErrorCode.ModelInvalid, $"Transmitted schema field {field} is Unavailable.");
    }

    private static XtSchemaFieldState State(XtFieldValue value) => value.Kind == XtFieldKind.Empty ? XtSchemaFieldState.Null : XtSchemaFieldState.Value;
    private static XtFieldValue? Empty(XtSchemaFieldState state) => state == XtSchemaFieldState.Null ? XtFieldValue.Null() : state == XtSchemaFieldState.Value ? null : throw new XtFormatException(XtErrorCode.ModelInvalid, "Unavailable schema field cannot be encoded.");
}
