using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // Orthogonal projection onto the plane is injective on the rectangle
    // if a fixed preconditioner M makes ||I-M D(project)||_inf < 1
    // everywhere. The segment between any two UVs remains in this box.
    internal static AlgorithmStatus CertifyPlanarProjection(in NestedBlendProgram program,BlendSurfaceIndex blend,
        in SurfaceParameterBox box,in AnalyticSurface plane,double distance,out bool certified,out bool positive)
    {
        certified=positive=false;
        if(!box.Valid||box.U0>=box.U1||box.V0>=box.V1||!(distance>0)||!double.IsFinite(distance)||plane.Kind!=SurfaceClass.Plane)
            return AlgorithmStatus.InvalidInput;
        var x=new IntervalVector(plane.X);var y=new IntervalVector(plane.Y);var normal=new IntervalVector(plane.Axis);
        var normalLength=normal.Norm().Value;var origin=new IntervalVector(plane.Origin);
        if(!normalLength.Finite||normalLength.Lower<=0)return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var u=box.U0+(box.U1-box.U0)*.5;var v=box.V0+(box.V1-box.V0)*.5;
        var status=Bound(program,blend,new(u,u,v,v),bounds);if(status!=AlgorithmStatus.Success)return status;
        PlaneJacobian(bounds,x,y,out var a,out var b,out var c,out var d);
        var determinant=a.Midpoint*d.Midpoint-b.Midpoint*c.Midpoint;
        if(determinant==0||!double.IsFinite(determinant))return AlgorithmStatus.NotConverged;
        var m00=d.Midpoint/determinant;var m01=-b.Midpoint/determinant;
        var m10=-c.Midpoint/determinant;var m11=a.Midpoint/determinant;
        if(!double.IsFinite(m00)||!double.IsFinite(m01)||!double.IsFinite(m10)||!double.IsFinite(m11))return AlgorithmStatus.NotConverged;
        var detM=new IntervalNumber(m00)*m11-new IntervalNumber(m01)*m10;
        if(!detM.Finite||detM.ContainsZero)return AlgorithmStatus.NotConverged;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[48];pending[0]=box;var count=1;var visited=0;
        while(count>0)
        {
            if(++visited>1024)return AlgorithmStatus.NotConverged;
            var cell=pending[--count];status=Bound(program,blend,cell,bounds);
            if(status==AlgorithmStatus.Success)
            {
                var point=new IntervalVector(bounds[0],bounds[1],bounds[2]);
                var signed=IntervalVector.Dot(point-origin,normal).Value/normalLength;
                if(signed.Lower>distance||signed.Upper< -distance)return AlgorithmStatus.Success;
                PlaneJacobian(bounds,x,y,out a,out b,out c,out d);
                var first=new IntervalNumber((1-m00*a-m01*c).Magnitude)+(m00*b+m01*d).Magnitude;
                var second=new IntervalNumber((m10*a+m11*c).Magnitude)+(1-m10*b-m11*d).Magnitude;
                if(signed.Magnitude<=distance&&first.Upper<1&&second.Upper<1)continue;
            }
            else if(status!=AlgorithmStatus.NotConverged)return status;
            if(count+2>pending.Length)return AlgorithmStatus.NotConverged;
            if((cell.U1-cell.U0)/(box.U1-box.U0)>=(cell.V1-cell.V0)/(box.V1-box.V0))
            {
                u=cell.U0+(cell.U1-cell.U0)*.5;if(!(u>cell.U0&&u<cell.U1))return AlgorithmStatus.NotConverged;
                pending[count++]=new(cell.U0,u,cell.V0,cell.V1);pending[count++]=new(u,cell.U1,cell.V0,cell.V1);
            }
            else
            {
                v=cell.V0+(cell.V1-cell.V0)*.5;if(!(v>cell.V0&&v<cell.V1))return AlgorithmStatus.NotConverged;
                pending[count++]=new(cell.U0,cell.U1,cell.V0,v);pending[count++]=new(cell.U0,cell.U1,v,cell.V1);
            }
        }
        certified=true;positive=detM.Lower>0;return AlgorithmStatus.Success;

        static void PlaneJacobian(ReadOnlySpan<IntervalNumber> bound,in IntervalVector x,in IntervalVector y,
            out IntervalNumber a,out IntervalNumber b,out IntervalNumber c,out IntervalNumber d)
        {
            var du=new IntervalVector(bound[3],bound[4],bound[5]);var dv=new IntervalVector(bound[6],bound[7],bound[8]);
            a=IntervalVector.Dot(x,du).Value;b=IntervalVector.Dot(x,dv).Value;
            c=IntervalVector.Dot(y,du).Value;d=IntervalVector.Dot(y,dv).Value;
        }
    }

    // Project along the normal to the retained plane, with lambda in [0,1].
    // One wall has one u-root for every v; along that root the second wall
    // is monotone in v by det(D(F,G))/F_u. The four brackets give existence
    // for every lambda, including the exact projected three-plane corner.
    internal static AlgorithmStatus CertifyProjectedPlaneCorner(in NestedBlendProgram program,BlendSurfaceIndex blend,
        in SurfaceParameterBox box,in AnalyticSurface projection,in AnalyticSurface first,in AnalyticSurface second,
        SurfaceParameterBox? completeDomain=null)
    {
        if(!box.Valid||box.U0>=box.U1||box.V0>=box.V1||projection.Kind!=SurfaceClass.Plane
            ||first.Kind!=SurfaceClass.Plane||second.Kind!=SurfaceClass.Plane)return AlgorithmStatus.InvalidInput;
        var domain=completeDomain??box;
        if(!domain.Valid||domain.U0>box.U0||domain.U1<box.U1||domain.V0>box.V0||domain.V1<box.V1)return AlgorithmStatus.InvalidInput;
        var normal=new IntervalVector(projection.Axis);var origin=new IntervalVector(projection.Origin);
        var ns=IntervalVector.Dot(normal,normal).Value;if(!ns.Finite||ns.Lower<=0)return AlgorithmStatus.InvalidInput;
        var a=new IntervalVector(first.Axis);var b=new IntervalVector(second.Axis);
        var lambda=new IntervalNumber(0,1);
        var alpha=lambda*(IntervalVector.Dot(a,normal).Value/ns);var beta=lambda*(IntervalVector.Dot(b,normal).Value/ns);
        var projectedA=a-normal*alpha;var projectedB=b-normal*beta;
        var firstOrigin=new IntervalVector(first.Origin);var secondOrigin=new IntervalVector(second.Origin);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var status=Bound(program,blend,domain,bounds);if(status!=AlgorithmStatus.Success)return status;
        var du=new IntervalVector(bounds[3],bounds[4],bounds[5]);var dv=new IntervalVector(bounds[6],bounds[7],bounds[8]);
        var fu=IntervalVector.Dot(projectedA,du).Value;var fv=IntervalVector.Dot(projectedA,dv).Value;
        var gu=IntervalVector.Dot(projectedB,du).Value;var gv=IntervalVector.Dot(projectedB,dv).Value;
        var det=fu*gv-fv*gu;
        if(!fu.Finite||fu.ContainsZero||!gv.Finite||gv.ContainsZero||!det.Finite||det.ContainsZero)return AlgorithmStatus.NotConverged;
        Span<IntervalNumber> walls=stackalloc IntervalNumber[4];
        for(var side=0;side<4;side++)
        {
            var u=side==0?box.U0:box.U1;var v=side==2?box.V0:box.V1;
            var boundary=side<2?new SurfaceParameterBox(u,u,box.V0,box.V1):new SurfaceParameterBox(box.U0,box.U1,v,v);
            status=Bound(program,blend,boundary,bounds);if(status!=AlgorithmStatus.Success)return status;
            var point=new IntervalVector(bounds[0],bounds[1],bounds[2]);
            var height=IntervalVector.Dot(point-origin,normal).Value;
            walls[side]=side<2?IntervalVector.Dot(point-firstOrigin,a).Value-alpha*height
                :IntervalVector.Dot(point-secondOrigin,b).Value-beta*height;
        }
        return Opposite(walls[0],walls[1])&&Opposite(walls[2],walls[3])?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;

        static bool Opposite(IntervalNumber a,IntervalNumber b)=>a.Upper<0&&b.Lower>0||a.Lower>0&&b.Upper<0;
    }
}
