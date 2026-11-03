namespace ProjectGmKernel.Native.Runtime;

/// <summary>Affine column-vector map, using the SDK's row-major 4-by-4 convention.</summary>
internal struct KernelAffineTransform
{
    public double M00, M01, M02, M03;
    public double M10, M11, M12, M13;
    public double M20, M21, M22, M23;

    internal static KernelAffineTransform Identity => new() { M00 = 1, M11 = 1, M22 = 1 };
    internal static KernelAffineTransform Translation(in KernelVector3 delta)
        => new() { M00 = 1, M11 = 1, M22 = 1, M03 = delta.X, M13 = delta.Y, M23 = delta.Z };
    internal static KernelAffineTransform Rotation(in KernelVector3 origin, in KernelVector3 axis, double angle)
    {
        var c = Math.Cos(angle); var s = Math.Sin(angle); var d = 1 - c;
        var x = axis.X; var y = axis.Y; var z = axis.Z;
        var m = new KernelAffineTransform
        {
            M00 = c + d*x*x, M01 = d*x*y - s*z, M02 = d*x*z + s*y,
            M10 = d*y*x + s*z, M11 = c + d*y*y, M12 = d*y*z - s*x,
            M20 = d*z*x - s*y, M21 = d*z*y + s*x, M22 = c + d*z*z,
        };
        var p = m.TransformVector(origin);
        m.M03 = origin.X - p.X; m.M13 = origin.Y - p.Y; m.M23 = origin.Z - p.Z;
        return m;
    }
    internal readonly KernelVector3 TransformPoint(in KernelVector3 p)
        => new() { X=M00*p.X+M01*p.Y+M02*p.Z+M03, Y=M10*p.X+M11*p.Y+M12*p.Z+M13, Z=M20*p.X+M21*p.Y+M22*p.Z+M23 };
    internal readonly KernelVector3 TransformVector(in KernelVector3 p)
        => new() { X=M00*p.X+M01*p.Y+M02*p.Z, Y=M10*p.X+M11*p.Y+M12*p.Z, Z=M20*p.X+M21*p.Y+M22*p.Z };
    // Divide coefficients before multiplication: 1 / scale can overflow for a
    // valid subnormal similarity, even though its orthogonal part is finite.
    internal readonly KernelVector3 TransformUnitDirection(in KernelVector3 p, double scale)
        => new() { X=(M00/scale)*p.X+(M01/scale)*p.Y+(M02/scale)*p.Z,
            Y=(M10/scale)*p.X+(M11/scale)*p.Y+(M12/scale)*p.Z,
            Z=(M20/scale)*p.X+(M21/scale)*p.Y+(M22/scale)*p.Z };
    internal readonly double Determinant => M00*(M11*M22-M12*M21)-M01*(M10*M22-M12*M20)+M02*(M10*M21-M11*M20);
    internal readonly bool IsFinite => double.IsFinite(M00) && double.IsFinite(M01) && double.IsFinite(M02) && double.IsFinite(M03)
        && double.IsFinite(M10) && double.IsFinite(M11) && double.IsFinite(M12) && double.IsFinite(M13)
        && double.IsFinite(M20) && double.IsFinite(M21) && double.IsFinite(M22) && double.IsFinite(M23);
    internal readonly bool TrySimilarity(out double scale, out double parity)
    {
        var max = Math.Max(Math.Max(Math.Max(Math.Abs(M00),Math.Abs(M01)),Math.Max(Math.Abs(M02),Math.Abs(M10))),
            Math.Max(Math.Max(Math.Abs(M11),Math.Abs(M12)),Math.Max(Math.Abs(M20),Math.Max(Math.Abs(M21),Math.Abs(M22)))));
        scale = 0; parity = 1;
        if (!IsFinite || max == 0) return false;
        var a=M00/max; var b=M01/max; var c=M02/max; var d=M10/max; var e=M11/max; var f=M12/max; var g=M20/max; var h=M21/max; var i=M22/max;
        var n=a*a+d*d+g*g; var n1=b*b+e*e+h*h; var n2=c*c+f*f+i*i;
        const double roundoff = 64 * 2.2204460492503131e-16;
        if (Math.Abs(n-n1)>roundoff*n || Math.Abs(n-n2)>roundoff*n || Math.Abs(a*b+d*e+g*h)>roundoff*n
            || Math.Abs(a*c+d*f+g*i)>roundoff*n || Math.Abs(b*c+e*f+h*i)>roundoff*n) return false;
        scale = max*Math.Sqrt(n); parity = a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g) < 0 ? -1 : 1;
        return double.IsFinite(scale) && scale > 0;
    }
}
