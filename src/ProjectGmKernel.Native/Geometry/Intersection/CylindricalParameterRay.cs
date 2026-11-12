using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

internal static class CylindricalParameterRay
{
    // A cylinder ray is u=qU+slope*(v-qV), v>=qV. Its signed phase
    // avoids atan2 and the periodic seam. The represented native frame's
    // dual, rather than an assumed orthonormal frame, supplies x/y/v.
    internal static AlgorithmStatus Bound(in AnalyticSurface cylinder,double qU,double qV,double slope,Span<IntervalNumber> spatial)
    {
        if(cylinder.Kind!=SurfaceClass.Cylinder||spatial.Length<9||!double.IsFinite(qU)||!double.IsFinite(qV)||!double.IsFinite(slope))
            return AlgorithmStatus.InvalidInput;
        var a=new IntervalVector(cylinder.Axis);var x=new IntervalVector(cylinder.X);var y=new IntervalVector(cylinder.Y);
        var dx=IntervalVector.Cross(y,a);var dy=IntervalVector.Cross(a,x);var dz=IntervalVector.Cross(x,y);
        var determinant=IntervalVector.Dot(x,dx).Value;if(!(determinant.Lower>0))return AlgorithmStatus.Unsupported;
        var p=new IntervalVector(new(spatial[0],spatial[3]),new(spatial[1],spatial[4]),new(spatial[2],spatial[5]))-new IntervalVector(cylinder.Origin);
        var u=IntervalVector.Dot(p,dx)/determinant;var v=IntervalVector.Dot(p,dy)/determinant;var axial=IntervalVector.Dot(p,dz)/determinant;
        var angle=qU+slope*(axial-qV);IntervalDual.SinCos(angle,out var sine,out var cosine);
        var signed=v*cosine-u*sine;var positive=u*cosine+v*sine;
        if(!signed.Value.Finite||!signed.Derivative.Finite||!positive.Value.Finite||!positive.Derivative.Finite||!axial.Value.Finite||!axial.Derivative.Finite)
            return AlgorithmStatus.NumericalFailure;
        spatial[..9].Clear();spatial[0]=signed.Value;spatial[3]=signed.Derivative;
        spatial[1]=positive.Value;spatial[4]=positive.Derivative;spatial[2]=axial.Value;spatial[5]=axial.Derivative;
        return AlgorithmStatus.Success;
    }
}
