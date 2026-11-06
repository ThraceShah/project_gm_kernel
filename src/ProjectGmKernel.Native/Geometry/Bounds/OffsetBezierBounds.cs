using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;

namespace ProjectGmKernel.Native.Geometry.Bounds;

/// <summary>Outward position and Jacobian enclosure of S+d unit(Su x Sv).
/// A cell whose normal cannot be certified is subdivided by its caller.</summary>
internal static class OffsetBezierBounds
{
    internal static AlgorithmStatus Bound(in BezierSurfacePatch patch,in SurfaceParameterBox box,double distance,Span<IntervalNumber> output)
    {
        if(distance==0)return BezierSurfaceBounds.Bound(patch,box,output);
        if(!double.IsFinite(distance)||output.Length<9)return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> jets=stackalloc IntervalNumber[18];var status=BezierSurfaceBounds.BoundSecond(patch,box,jets);
        if(status!=AlgorithmStatus.Success)return status;
        for(var axis=0;axis<2;axis++)
        {
            var su=new IntervalVector(new(jets[3],jets[axis==0?9:12]),new(jets[4],jets[axis==0?10:13]),new(jets[5],jets[axis==0?11:14]));
            var sv=new IntervalVector(new(jets[6],jets[axis==0?12:15]),new(jets[7],jets[axis==0?13:16]),new(jets[8],jets[axis==0?14:17]));
            var cross=IntervalVector.Cross(su,sv);var length=cross.Norm();
            if(!length.Finite||!(length.Value.Lower>0))return AlgorithmStatus.NotConverged;
            var normal=cross/length;if(!normal.Finite)return AlgorithmStatus.NotConverged;
            if(axis==0){output[0]=jets[0]+distance*normal.X.Value;output[1]=jets[1]+distance*normal.Y.Value;output[2]=jets[2]+distance*normal.Z.Value;}
            var at=3+3*axis;output[at]=jets[at]+distance*normal.X.Derivative;output[at+1]=jets[at+1]+distance*normal.Y.Derivative;output[at+2]=jets[at+2]+distance*normal.Z.Derivative;
        }
        for(var i=0;i<9;i++)if(!output[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }
}
