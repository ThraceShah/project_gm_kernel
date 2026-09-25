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
/// Deterministic plan selection from support capabilities (§7.6). Preference
/// order prefers cheaper analytic plans that still carry the required
/// residual/Jacobian capabilities — never changes the parameter plane, branch,
/// or reported parameter.
/// </summary>
internal static class ICurveConstraintPlanRules
{
    /// <summary>
    /// Select the Auto plan: plane + implicit → I1; both implicit-capable without
    /// a plane prefer I2 (chord-plane eliminated) over I3; otherwise P2. P4 stays
    /// available as an explicit/cross-check plan, not Auto default.
    /// </summary>
    /// <summary>
    /// Explicit plans must be a known enumerator and capable on these supports
    /// before a cached sample may be published. Auto is legal; the selected
    /// plan is checked when it is actually forced. I1 needs exactly one plane.
    /// </summary>
    internal static AlgorithmStatus ValidateRequest(in ICurveView view, ICurveConstraintPlan plan)
    {
        switch (plan)
        {
            case ICurveConstraintPlan.Auto:
            case ICurveConstraintPlan.I2:
            case ICurveConstraintPlan.I3:
            case ICurveConstraintPlan.P2:
            case ICurveConstraintPlan.P4:
                return AlgorithmStatus.Success;
            case ICurveConstraintPlan.I1:
                var plane0 = view.Support0.Kind == SurfaceClass.Plane;
                var plane1 = view.Support1.Kind == SurfaceClass.Plane;
                return plane0 != plane1 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
            default:
                return AlgorithmStatus.InvalidInput;
        }
    }

    internal static ICurveConstraintPlan Select(in ICurveView view)
    {
        var plane0 = view.Support0.Kind == SurfaceClass.Plane;
        var plane1 = view.Support1.Kind == SurfaceClass.Plane;
        if (plane0 != plane1)
            return ICurveConstraintPlan.I1;
        // Analytic supports in this slice are both parametric and implicit.
        // Prefer the cheaper in-plane I2 over the 3×3 I3 when both work (§7.6).
        return ICurveConstraintPlan.I2;
    }

    /// <summary>
    /// Ordered alternate plans for diagnosed switches after Singular/stagnation
    /// (§14.6). Excludes Auto and the currently failing plan.
    /// </summary>
    internal static BufferCount Alternates(in ICurveView view, ICurveConstraintPlan failed,
        Span<ICurveConstraintPlan> destination)
    {
        Span<ICurveConstraintPlan> preference = stackalloc ICurveConstraintPlan[5];
        BufferCount n = 0;
        var plane0 = view.Support0.Kind == SurfaceClass.Plane;
        var plane1 = view.Support1.Kind == SurfaceClass.Plane;
        if (plane0 != plane1)
        {
            preference[n++] = ICurveConstraintPlan.I1;
            preference[n++] = ICurveConstraintPlan.I2;
            preference[n++] = ICurveConstraintPlan.P2;
            preference[n++] = ICurveConstraintPlan.I3;
            preference[n++] = ICurveConstraintPlan.P4;
        }
        else
        {
            preference[n++] = ICurveConstraintPlan.I2;
            preference[n++] = ICurveConstraintPlan.P2;
            preference[n++] = ICurveConstraintPlan.I3;
            preference[n++] = ICurveConstraintPlan.P4;
            preference[n++] = ICurveConstraintPlan.I1;
        }

        BufferCount written = 0;
        for (BufferOffset i = 0; i < n && written < destination.Length; i++)
        {
            if (preference[i] == failed || preference[i] == ICurveConstraintPlan.Auto) continue;
            destination[written++] = preference[i];
        }
        return written;
    }
}
