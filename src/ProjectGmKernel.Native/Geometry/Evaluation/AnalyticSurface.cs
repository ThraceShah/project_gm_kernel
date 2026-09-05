using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Prepared analytic data. Owns no entity handles or session state.</summary>
internal readonly struct AnalyticSurface(
    SurfaceClass kind, KernelVector3 origin, KernelVector3 axis, KernelVector3 reference,
    double radius = 0, double secondary = 0)
{
    internal readonly SurfaceClass Kind = kind;
    internal readonly KernelVector3 Origin = origin;
    internal readonly KernelVector3 Axis = axis;
    internal readonly KernelVector3 X = reference;
    internal readonly KernelVector3 Y = EvaluationMath.Cross(axis, reference);
    internal readonly double Radius = radius;
    // Cone: tan(semi-angle). Torus: minor radius; Radius is its major radius.
    internal readonly double Secondary = secondary;
}
