using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Exact analytic signed distance and normal pulled back along a
/// Taylor-valued spatial path. No differencing of nested evaluations.</summary>
internal static class AnalyticDistanceSeries
{
    internal static AlgorithmStatus Evaluate(in AnalyticSurface surface,in TaylorVector point,
        out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;var order=point.X.Order;
        var relative=point-TaylorVector.Constant(surface.Origin,order);
        var axis=TaylorVector.Constant(surface.Axis,order);var z=TaylorVector.Dot(relative,axis);
        switch(surface.Kind)
        {
            case SurfaceClass.Plane:distance=z;normal=axis;break;
            case SurfaceClass.Sphere:
                var length=relative.Norm();if(!(length[0]>0))return AlgorithmStatus.Singular;
                distance=length-surface.Radius;normal=relative/length;break;
            case SurfaceClass.Cylinder:
            case SurfaceClass.Cone:
            case SurfaceClass.Torus:
                var radial=relative-axis*z;var rho=radial.Norm();if(!(rho[0]>0))return AlgorithmStatus.Singular;
                var unit=radial/rho;
                if(surface.Kind==SurfaceClass.Cylinder){distance=rho-surface.Radius;normal=unit;}
                else if(surface.Kind==SurfaceClass.Cone)
                {
                    if(!(surface.Radius+surface.Secondary*z[0]>0))return AlgorithmStatus.Singular;
                    var factor=1/Math.Sqrt(1+surface.Secondary*surface.Secondary);
                    distance=(rho-surface.Radius-z*surface.Secondary)*factor;normal=(unit-axis*surface.Secondary)*factor;
                }
                else
                {
                    if(!(surface.Radius>surface.Secondary&&surface.Secondary>0))return AlgorithmStatus.Unsupported;
                    var a=rho-surface.Radius;var profile=TaylorJet.Sqrt(a*a+z*z);if(!(profile[0]>0))return AlgorithmStatus.Singular;
                    distance=profile-surface.Secondary;normal=(unit*a+axis*z)/profile;
                }
                break;
            default:return AlgorithmStatus.Unsupported;
        }
        return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }
}
