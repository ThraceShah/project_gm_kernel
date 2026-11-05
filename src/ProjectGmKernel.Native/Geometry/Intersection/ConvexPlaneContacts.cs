using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>Certifies isolated plane contacts when the homogeneous plane
/// numerator is strictly convex/concave on each unresolved span. Exact zero
/// value and gradient are required; near-zero regular loops are never snapped
/// to contact points. Other singular loci remain with the general solver.</summary>
internal static class ConvexPlaneContacts
{
    internal static AlgorithmStatus Find(in BSurfaceView surface,in AnalyticSurface plane,Span<KernelVector3> points,out BufferCount count)
    {
        count=0;if(plane.Kind!=SurfaceClass.Plane)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> source=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],scalar=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> du=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],dv=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> value=stackalloc IntervalNumber[9],uJet=stackalloc IntervalNumber[9],vJet=stackalloc IntervalNumber[9];
        var p=surface.UDegree;var q=surface.VDegree;
        for(var u=p;u<surface.UVertexCount;u++)for(var v=q;v<surface.VVertexCount;v++)
        {
            if(!(surface.UKnots[u+1]>surface.UKnots[u]&&surface.VKnots[v+1]>surface.VKnots[v]))continue;
            var status=BezierSurfaceBounds.Extract(surface,u,v,source);if(status!=AlgorithmStatus.Success)return status;
            var domain=new SurfaceParameterBox(surface.UKnots[u],surface.UKnots[u+1],surface.VKnots[v],surface.VKnots[v+1]);
            var size=4*(p+1)*(q+1);scalar[..size].Clear();
            for(var i=0;i<size;i+=4)
            {
                scalar[i]=(source[i]-plane.Origin.X*source[i+3])*plane.Axis.X+(source[i+1]-plane.Origin.Y*source[i+3])*plane.Axis.Y+(source[i+2]-plane.Origin.Z*source[i+3])*plane.Axis.Z;
                scalar[i+3]=1;
            }
            var patch=new BezierSurfacePatch(scalar[..size],p,q,domain);
            status=BezierSurfaceBounds.Bound(patch,domain,value);if(status!=AlgorithmStatus.Success)return status;
            if(!value[0].ContainsZero)continue;
            du[..(4*p*(q+1))].Clear();dv[..(4*(p+1)*q)].Clear();
            var us=p/(new IntervalNumber(domain.U1)-domain.U0);var vs=q/(new IntervalNumber(domain.V1)-domain.V0);
            for(var i=0;i<p;i++)for(var j=0;j<=q;j++)
            {var at=4*(i*(q+1)+j);du[at]=us*(scalar[at+4*(q+1)]-scalar[at]);du[at+3]=1;}
            for(var i=0;i<=p;i++)for(var j=0;j<q;j++)
            {var at=4*(i*q+j);var original=4*(i*(q+1)+j);dv[at]=vs*(scalar[original+4]-scalar[original]);dv[at+3]=1;}
            var up=new BezierSurfacePatch(du[..(4*p*(q+1))],p-1,q,domain);var vp=new BezierSurfacePatch(dv[..(4*(p+1)*q)],p,q-1,domain);
            status=BezierSurfaceBounds.Bound(up,domain,uJet);if(status!=AlgorithmStatus.Success)return status;
            status=BezierSurfaceBounds.Bound(vp,domain,vJet);if(status!=AlgorithmStatus.Success)return status;
            var mixed=IntervalNumber.Hull(uJet[6],vJet[3]);var determinant=uJet[3]*vJet[6]-IntervalNumber.Square(mixed);
            if(!(determinant.Lower>0&&(uJet[3].Lower>0&&vJet[6].Lower>0||uJet[3].Upper<0&&vJet[6].Upper<0)))return AlgorithmStatus.Unsupported;
            var x=domain.U0+(domain.U1-domain.U0)*.5;var y=domain.V0+(domain.V1-domain.V0)*.5;
            for(var iteration=0;iteration<24;iteration++)
            {
                var singleton=new SurfaceParameterBox(x,x,y,y);
                status=BezierSurfaceBounds.Bound(up,singleton,uJet);if(status!=AlgorithmStatus.Success)return status;
                status=BezierSurfaceBounds.Bound(vp,singleton,vJet);if(status!=AlgorithmStatus.Success)return status;
                var aa=uJet[3].Midpoint;var ab=uJet[6].Midpoint;var bb=vJet[6].Midpoint;var det=aa*bb-ab*ab;
                if(!(det>0))return AlgorithmStatus.Unsupported;
                var nx=Math.Clamp(x-(uJet[0].Midpoint*bb-vJet[0].Midpoint*ab)/det,domain.U0,domain.U1);
                var ny=Math.Clamp(y-(vJet[0].Midpoint*aa-uJet[0].Midpoint*ab)/det,domain.V0,domain.V1);
                if(nx==x&&ny==y)break;x=nx;y=ny;
            }
            var atRoot=new SurfaceParameterBox(x,x,y,y);
            status=BezierSurfaceBounds.Bound(patch,atRoot,value);if(status!=AlgorithmStatus.Success)return status;
            status=BezierSurfaceBounds.Bound(up,atRoot,uJet);if(status!=AlgorithmStatus.Success)return status;
            status=BezierSurfaceBounds.Bound(vp,atRoot,vJet);if(status!=AlgorithmStatus.Success)return status;
            if(value[0].Magnitude!=0||uJet[0].Magnitude!=0||vJet[0].Magnitude!=0)return AlgorithmStatus.Unsupported;
            status=BezierSurfaceBounds.Bound(new(source[..size],p,q,domain),atRoot,value);if(status!=AlgorithmStatus.Success)return status;
            var point=new KernelVector3{X=value[0].Midpoint,Y=value[1].Midpoint,Z=value[2].Midpoint};
            var duplicate=false;for(var i=0;i<count;i++)if(OperationIntersection.Norm(EvaluationMath.Sub(point,points[i]))<OperationIntersection.DistanceTolerance)duplicate=true;
            if(!duplicate){if(count==points.Length)return AlgorithmStatus.OutputTooSmall;points[count++]=point;}
        }
        return AlgorithmStatus.Success;
    }
}
