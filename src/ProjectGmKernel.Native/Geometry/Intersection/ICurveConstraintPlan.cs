using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Regular-interval constraint plans (spec §7, task T07). Auto defers to the
/// preparation-stage selection rules; explicit values force a plan for
/// equivalence testing and for diagnosed plan switches (§12.4, §14.6).
/// </summary>
internal enum ICurveConstraintPlan : byte
{
    Auto = 0,
    /// <summary>Parametric/parametric, 4×4 baseline (§7.1).</summary>
    P4 = 1,
    /// <summary>Parametric/implicit, 2×2 (§7.2).</summary>
    P2 = 2,
    /// <summary>Implicit/implicit, 3×3 (§7.3).</summary>
    I3 = 3,
    /// <summary>Implicit/implicit with the parameter plane eliminated, 2×2 (§7.4).</summary>
    I2 = 4,
    /// <summary>One support is a plane: line + scalar solve (§7.5).</summary>
    I1 = 5,
}

/// <summary>
/// Deterministic plan selection from support capabilities (§7.6). The stated
/// preference — analytic supports without inner solves prefer I1/I2/P2 over
/// the general plans — is applied as: a plane support plus an implicit-capable
/// other selects I1; any parametric support with an implicit-capable other
/// selects P2; two implicit-only supports select I3 (I2 stays available as the
/// elimination alternative and cross-check). Plan choice never changes the
/// parameter plane, the branch or the reported parameter.
/// </summary>
internal static class ICurveConstraintPlanRules
{
    internal static ICurveConstraintPlan Select(in ICurveView view)
    {
        var plane0 = view.Support0.Kind == SurfaceClass.Plane;
        var plane1 = view.Support1.Kind == SurfaceClass.Plane;
        // Both analytic supports carry parametric and implicit capabilities in
        // this slice; B-surface supports arrive with T09+ and would extend the
        // capability checks here (P4 for parametric pairs without implicit).
        if ((plane0 || plane1))
            return ICurveConstraintPlan.I1;
        return ICurveConstraintPlan.P2;
    }
}
