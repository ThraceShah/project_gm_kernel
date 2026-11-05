using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class NestedSplineEvaluation
{
    internal struct ContactJet
    {
        internal KernelVector3 Point,Du,Dv,Duu,Duv,Dvv,Normal,NormalU,NormalV;
    }
    internal static AlgorithmStatus Contact(in NestedBlendProgram program,in BlendSurfaceInstruction node,double u,double v,out ContactJet contact)
    {
        contact=default;Span<KernelVector3> jets=stackalloc KernelVector3[9];var status=NumericJets(program,node,u,v,jets);if(status!=AlgorithmStatus.Success)return status;
        contact.Point=jets[0];contact.Du=jets[3];contact.Dv=jets[1];contact.Duu=jets[6];contact.Duv=jets[4];contact.Dvv=jets[2];
        var cross=Cross(contact.Du,contact.Dv);var length=Math.Sqrt(Dot(cross,cross));if(!(length>1e-12))return AlgorithmStatus.Singular;
        var normal=Scale(cross,1/length);var uDerivative=Add(Cross(contact.Duu,contact.Dv),Cross(contact.Du,contact.Duv));var vDerivative=Add(Cross(contact.Duv,contact.Dv),Cross(contact.Du,contact.Dvv));
        contact.Normal=normal;contact.NormalU=Scale(Sub(uDerivative,Scale(normal,Dot(normal,uDerivative))),1/length);contact.NormalV=Scale(Sub(vDerivative,Scale(normal,Dot(normal,vDerivative))),1/length);
        return AlgorithmStatus.Success;
    }
    internal static AlgorithmStatus Jets(in NestedBlendProgram program,in BlendSurfaceInstruction node,
        TaylorJet u,TaylorJet v,out TaylorVector point,out TaylorVector du,out TaylorVector dv)
    {
        point=du=dv=default;var order=Math.Max(u.Order,v.Order);var source=program.Spline(node);
        if(order>TaylorJet.MaxOrder||!double.IsFinite(u[0])||!double.IsFinite(v[0]))return AlgorithmStatus.InvalidInput;
        SurfaceDerivativeLayout.TryCreate(order+1,order+1,out var layout);Span<KernelVector3> jets=stackalloc KernelVector3[121];
        var status=BSurfaceEvaluation.Evaluate(source,u[0],v[0],layout,program.Workspace,jets);if(status!=AlgorithmStatus.Success)return status;
        Span<TaylorJet> up=stackalloc TaylorJet[TaylorJet.MaxOrder+1],vp=stackalloc TaylorJet[TaylorJet.MaxOrder+1];
        up[0]=vp[0]=new(1,order);var x=u-u[0];var y=v-v[0];
        for(var k=1;k<=order;k++){up[k]=up[k-1]*x;vp[k]=vp[k-1]*y;}
        point=du=dv=TaylorVector.Constant(default,order);
        double fi=1;
        for(var i=0;i<=order;i++)
        {
            if(i>0)fi*=i;double fj=1;
            for(var j=0;j<=order-i;j++)
            {
                if(j>0)fj*=j;var weight=up[i]*vp[j]/(fi*fj);
                point+=TaylorVector.Constant(jets[layout.GetIndex(i,j)],order)*weight;
                du+=TaylorVector.Constant(jets[layout.GetIndex(i+1,j)],order)*weight;
                dv+=TaylorVector.Constant(jets[layout.GetIndex(i,j+1)],order)*weight;
            }
        }
        return point.Finite&&du.Finite&&dv.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus NumericJets(in NestedBlendProgram program,in BlendSurfaceInstruction node,
        double u,double v,Span<KernelVector3> jets)
    {
        var source=program.Spline(node);SurfaceDerivativeLayout.TryCreate(2,2,out var layout);
        return BSurfaceEvaluation.Evaluate(source,u,v,layout,program.Workspace,jets);
    }

    internal static AlgorithmStatus Closest(in NestedBlendProgram program,in BlendSurfaceInstruction node,in KernelVector3 point,
        out double u,out double v)
    {
        u=v=0;var source=program.Spline(node);var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        for(var i=source.UDegree;i<source.UVertexCount;i++)for(var j=source.VDegree;j<source.VVertexCount;j++)
        {
            if(!(source.UKnots[i+1]>source.UKnots[i]&&source.VKnots[j+1]>source.VKnots[j]))continue;
            var status=BezierSurfaceBounds.Extract(source,i,j,poles);if(status!=AlgorithmStatus.Success)return status;
            var patch=new BezierSurfacePatch(poles[..(4*(source.UDegree+1)*(source.VDegree+1))],source.UDegree,source.VDegree,new(source.UKnots[i],source.UKnots[i+1],source.VKnots[j],source.VKnots[j+1]));
            status=BSplineProjection.Closest(patch,point,1e-4,ref best);if(status!=AlgorithmStatus.Success)return status;
        }
        if(!double.IsFinite(best.DistanceUpper))return AlgorithmStatus.NotConverged;u=best.U;v=best.V;
        return Refine(program,node,point,ref u,ref v);
    }

    private static AlgorithmStatus Refine(in NestedBlendProgram program,in BlendSurfaceInstruction node,in KernelVector3 point,ref double u,ref double v)
    {
        Span<KernelVector3> jets=stackalloc KernelVector3[9];
        for(var iteration=0;iteration<16;iteration++)
        {
            var status=NumericJets(program,node,u,v,jets);if(status!=AlgorithmStatus.Success)return status;
            var q=Sub(point,jets[0]);var su=jets[3];var sv=jets[1];var gu=Dot(q,su);var gv=Dot(q,sv);
            var scale=Math.Max(Math.Max(Math.Abs(point.X),Math.Max(Math.Abs(point.Y),Math.Abs(point.Z))),Math.Max(Math.Abs(jets[0].X),Math.Max(Math.Abs(jets[0].Y),Math.Abs(jets[0].Z))));
            scale=Math.Max(scale,Math.Sqrt(Math.Max(Dot(su,su),Dot(sv,sv))));var tolerance=64*SmallLinearSolve.MachineEpsilon*Math.Max(scale,double.Epsilon);
            if(Math.Max(Math.Abs(gu)/Math.Sqrt(Dot(su,su)),Math.Abs(gv)/Math.Sqrt(Dot(sv,sv)))<tolerance)return AlgorithmStatus.Success;
            var a=Dot(su,su)-Dot(q,jets[6]);var b=Dot(su,sv)-Dot(q,jets[4]);var c=Dot(sv,sv)-Dot(q,jets[2]);var determinant=a*c-b*b;
            if(!(a>0&&c>0&&determinant>1e-12*a*c))return AlgorithmStatus.Singular;
            var nextU=u+(gu*c-gv*b)/determinant;var nextV=v+(gv*a-gu*b)/determinant;
            if(!double.IsFinite(nextU)||!double.IsFinite(nextV))return AlgorithmStatus.NumericalFailure;
            u=nextU;v=nextV;
        }
        return AlgorithmStatus.NotConverged;
    }

    internal static AlgorithmStatus Distance(in NestedBlendProgram program,in BlendSurfaceInstruction node,in TaylorVector path,
        out TaylorJet distance,out TaylorVector normal)
        =>Distance(program,node,path,out distance,out normal,out _,out _);

    internal static AlgorithmStatus Distance(in NestedBlendProgram program,in BlendSurfaceInstruction node,in TaylorVector path,
        out TaylorJet distance,out TaylorVector normal,out double witnessU,out double witnessV)
    {
        distance=default;normal=default;witnessU=witnessV=0;var status=Closest(program,node,path.Value,out var u0,out var v0);if(status!=AlgorithmStatus.Success)return status;
        witnessU=u0;witnessV=v0;
        return DistanceAtFoot(program,node,path,u0,v0,out distance,out normal);
    }

    internal static AlgorithmStatus DistanceFromSeed(in NestedBlendProgram program,in BlendSurfaceInstruction node,in TaylorVector path,
        ref double u,ref double v,out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;var status=Refine(program,node,path.Value,ref u,ref v);if(status!=AlgorithmStatus.Success)return status;
        return DistanceAtFoot(program,node,path,u,v,out distance,out normal);
    }

    private static AlgorithmStatus DistanceAtFoot(in NestedBlendProgram program,in BlendSurfaceInstruction node,in TaylorVector path,
        double u0,double v0,out TaylorJet distance,out TaylorVector normal)
    {
        distance=default;normal=default;
        var order=path.X.Order;var u=new TaylorJet(u0,order);var v=new TaylorJet(v0,order);
        Span<KernelVector3> jets=stackalloc KernelVector3[9];var status=NumericJets(program,node,u0,v0,jets);if(status!=AlgorithmStatus.Success)return status;
        var q=Sub(path.Value,jets[0]);var a=Dot(jets[3],jets[3])-Dot(q,jets[6]);var b=Dot(jets[3],jets[1])-Dot(q,jets[4]);var c=Dot(jets[1],jets[1])-Dot(q,jets[2]);var determinant=a*c-b*b;
        if(!(a>0&&c>0&&determinant>1e-12*a*c))return AlgorithmStatus.Singular;
        for(var k=1;k<=order;k++)
        {
            status=Jets(program,node,u.Truncate(k),v.Truncate(k),out var point,out var su,out var sv);if(status!=AlgorithmStatus.Success)return status;
            var delta=path.Truncate(k)-point;var gu=TaylorVector.Dot(delta,su);var gv=TaylorVector.Dot(delta,sv);
            u[k]=(gu[k]*c-gv[k]*b)/determinant;v[k]=(gv[k]*a-gu[k]*b)/determinant;
        }
        status=Jets(program,node,u,v,out var foot,out var du,out var dv);if(status!=AlgorithmStatus.Success)return status;
        var cross=TaylorVector.Cross(du,dv);if(!(cross.Norm()[0]>1e-12))return AlgorithmStatus.Singular;
        normal=cross.Unit()*node.NormalSense;distance=TaylorVector.Dot(path-foot,normal)-node.Offset*node.NormalSense;
        return distance.Finite&&normal.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }
}
