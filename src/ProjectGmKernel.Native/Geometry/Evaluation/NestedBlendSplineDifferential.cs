using ProjectGmKernel.Native.Computation.Numerics;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // The two native UV directions share every value operation. Keep each
    // derivative's original IntervalDual expression and outward rounding.
    private readonly struct SplineDifferential(IntervalNumber value,IntervalNumber u,IntervalNumber v)
    {
        internal readonly IntervalNumber Value=value,U=u,V=v;
        internal bool Finite=>Value.Finite&&U.Finite&&V.Finite;
        public static implicit operator SplineDifferential(double x)=>new(x,0,0);
        public static SplineDifferential operator +(SplineDifferential a,SplineDifferential b)=>new(a.Value+b.Value,a.U+b.U,a.V+b.V);
        public static SplineDifferential operator -(SplineDifferential a)=>new(-a.Value,-a.U,-a.V);
        public static SplineDifferential operator -(SplineDifferential a,SplineDifferential b)=>a+-b;
        public static SplineDifferential operator *(SplineDifferential a,SplineDifferential b)=>new(a.Value*b.Value,a.U*b.Value+a.Value*b.U,a.V*b.Value+a.Value*b.V);
        public static SplineDifferential operator /(SplineDifferential a,SplineDifferential b)
        {
            var square=IntervalNumber.Square(b.Value);
            return new(a.Value/b.Value,(a.U*b.Value-a.Value*b.U)/square,(a.V*b.Value-a.Value*b.V)/square);
        }
        internal static SplineDifferential Square(SplineDifferential x)=>new(IntervalNumber.Square(x.Value),2*x.Value*x.U,2*x.Value*x.V);
        internal static SplineDifferential Sqrt(SplineDifferential x)
        {
            var lower=Math.Sqrt(Math.Max(0,x.Value.Lower));var upper=Math.Sqrt(x.Value.Upper);
            var value=new IntervalNumber(lower==0?0:Math.BitDecrement(lower),Math.BitIncrement(upper));var denominator=2*value;
            return new(value,x.U/denominator,x.V/denominator);
        }
    }

    private readonly struct SplineDifferentialVector(SplineDifferential x,SplineDifferential y,SplineDifferential z)
    {
        internal readonly SplineDifferential X=x,Y=y,Z=z;
        internal bool Finite=>X.Finite&&Y.Finite&&Z.Finite;
        public static SplineDifferentialVector operator -(SplineDifferentialVector a,SplineDifferentialVector b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static SplineDifferentialVector operator *(SplineDifferentialVector a,SplineDifferential b)=>new(a.X*b,a.Y*b,a.Z*b);
        public static SplineDifferentialVector operator /(SplineDifferentialVector a,SplineDifferential b)=>new(a.X/b,a.Y/b,a.Z/b);
        internal static SplineDifferentialVector Cross(SplineDifferentialVector a,SplineDifferentialVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        internal SplineDifferential Norm()=>SplineDifferential.Sqrt(SplineDifferential.Square(X)+SplineDifferential.Square(Y)+SplineDifferential.Square(Z));
    }

    private static SplineDifferentialVector SplineUvVector(ReadOnlySpan<IntervalNumber> jets,BufferOffset p,BufferOffset du,BufferOffset dv)
        =>new(SplineUvComponent(jets,p,du,dv),SplineUvComponent(jets,p+1,du+1,dv+1),SplineUvComponent(jets,p+2,du+2,dv+2));

    private static SplineDifferential SplineUvComponent(ReadOnlySpan<IntervalNumber> jets,BufferOffset p,BufferOffset du,BufferOffset dv)
        =>new(jets[p],jets[du]*new IntervalNumber(1)+jets[dv]*new IntervalNumber(0),jets[du]*new IntervalNumber(0)+jets[dv]*new IntervalNumber(1));
}
