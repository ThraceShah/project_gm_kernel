namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>Closed floating-point enclosure. Arithmetic rounds outwards.</summary>
internal readonly struct IntervalNumber(double lower, double upper)
{
    internal readonly double Lower = lower;
    internal readonly double Upper = upper;
    internal IntervalNumber(double value) : this(value, value) { }
    internal bool ContainsZero => Lower <= 0 && Upper >= 0;
    internal double Midpoint => Lower + (Upper - Lower) * .5;
    internal double Magnitude => Math.Max(Math.Abs(Lower), Math.Abs(Upper));
    internal bool Finite => double.IsFinite(Lower) && double.IsFinite(Upper) && Lower <= Upper;
    public static implicit operator IntervalNumber(double value) => new(value);
    public static IntervalNumber operator +(IntervalNumber a, IntervalNumber b)
    {
        if (a.Lower == 0 && a.Upper == 0) return b;
        if (b.Lower == 0 && b.Upper == 0) return a;
        return new(Math.BitDecrement(a.Lower + b.Lower), Math.BitIncrement(a.Upper + b.Upper));
    }
    public static IntervalNumber operator -(IntervalNumber a) => new(-a.Upper, -a.Lower);
    public static IntervalNumber operator -(IntervalNumber a, IntervalNumber b)
        => a.Lower == a.Upper && b.Lower == b.Upper && a.Lower == b.Lower ? new(0) : a + -b;
    public static IntervalNumber operator *(IntervalNumber a, IntervalNumber b)
    {
        if (a.Magnitude == 0 || b.Magnitude == 0) return new(0);
        if (a.Lower == 1 && a.Upper == 1) return b;
        if (b.Lower == 1 && b.Upper == 1) return a;
        var p = a.Lower * b.Lower; var q = a.Lower * b.Upper;
        var r = a.Upper * b.Lower; var s = a.Upper * b.Upper;
        return new(Math.BitDecrement(Math.Min(Math.Min(p, q), Math.Min(r, s))),
            Math.BitIncrement(Math.Max(Math.Max(p, q), Math.Max(r, s))));
    }
    public static IntervalNumber operator /(IntervalNumber a, IntervalNumber b)
        => b.Lower == 1 && b.Upper == 1 ? a : b.ContainsZero ? new(double.NegativeInfinity, double.PositiveInfinity)
            : a * new IntervalNumber(Math.BitDecrement(1 / b.Upper), Math.BitIncrement(1 / b.Lower));
    internal static IntervalNumber Hull(IntervalNumber a, IntervalNumber b)
        => new(Math.Min(a.Lower, b.Lower), Math.Max(a.Upper, b.Upper));
    internal static IntervalNumber Square(IntervalNumber a)
    {
        var lo = a.ContainsZero ? 0 : Math.Min(a.Lower * a.Lower, a.Upper * a.Upper);
        return new(lo == 0 ? 0 : Math.BitDecrement(lo), Math.BitIncrement(a.Magnitude * a.Magnitude));
    }
}
