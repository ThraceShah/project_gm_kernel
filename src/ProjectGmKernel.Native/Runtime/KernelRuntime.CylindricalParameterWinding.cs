using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CylindricalLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s query,out BufferCount winding)
    {
        var error=AxialCylindricalLoopWinding(surface,edges,loop,query,out winding);
        if(error!=ParasolidConstants.PK_ERROR_eval_failure)return error;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var cylinder))return error;
        var x=new IntervalVector(cylinder.X);var y=new IntervalVector(cylinder.Y);var a=new IntervalVector(cylinder.Axis);
        var axial=IntervalVector.Cross(x,y);var determinant=IntervalVector.Dot(axial,a).Value;
        if(!(determinant.Lower>0))return error;
        var maximum=query.param[1];
        foreach(var use in loop)
        {
            var edge=edges[use.Edge];var record=Curves[GetCurveSlotByTag(edge.Curve)];
            if(!TryFiniteCarrierExtent(new(){Curve=edge.Curve,First=edge.Start,Last=edge.End},record,out var box))return error;
            var p=new IntervalVector(new IntervalNumber(box.XLo,box.XHi),new IntervalNumber(box.YLo,box.YHi),new IntervalNumber(box.ZLo,box.ZHi));
            var v=IntervalVector.Dot(p-new IntervalVector(cylinder.Origin),axial).Value/determinant;if(!v.Finite)return error;
            maximum=Math.Max(maximum,v.Upper);
        }
        var height=maximum-query.param[1];if(!(height>0)||!double.IsFinite(height))return error;
        // Keep the exact query and change only the ray. The total angular
        // displacement before its certified axial horizon is bounded.
        for(var attempt=0;attempt<3;attempt++)
        {
            var slope=(attempt==0?.25:attempt==1?-.5:.75)/height;
            var status=ObliqueCylindricalLoopWinding(cylinder,edges,loop,query,slope,out winding);
            if(status==AlgorithmStatus.Success)return 0;
            if(status!=AlgorithmStatus.NotConverged&&status!=AlgorithmStatus.Unsupported)return IntersectionError(status);
        }
        return error;
    }

    private static AlgorithmStatus ObliqueCylindricalLoopWinding(in AnalyticSurface cylinder,ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> loop,PK_UV_s query,double slope,out BufferCount winding)
    {
        winding=0;Span<AnalyticSurface> support=stackalloc AnalyticSurface[1]{cylinder};
        Span<BlendSpineInstruction> explicitSpine=stackalloc BlendSpineInstruction[1];Span<BlendChartInstruction> chart=stackalloc BlendChartInstruction[2];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[IntersectionPointCapacity];Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var level=new AnalyticSurface(SurfaceClass.Plane,default,new(){X=1},new(){Y=1});
        foreach(var use in loop)
        {
            var edge=edges[use.Edge];var record=Curves[GetCurveSlotByTag(edge.Curve)];scoped NestedBlendProgram program=default;BlendSpineIndex spine=0;
            if(TryReadExplicitSpineCurve(record,out var curve))
            {
                explicitSpine[0]=new(){Curve=curve,ChartCount=2};chart[0]=new(){Parameter=edge.Start};chart[1]=new(){Parameter=edge.End};
                program=new(default,explicitSpine,chart);
            }
            else if(record.Class==CurveClass.ICurve)
            {
                var data=ICurveDataPool[record.DataIndex];if(data.ProgramBlock<=0||data.IsBlendContact!=0)return AlgorithmStatus.Unsupported;
                program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);spine=data.ProgramSpine;
            }
            else return AlgorithmStatus.Unsupported;
            var spatial=new SurfaceIntersectionPatch(program,spine,edge.Start,edge.End);
            var coordinate=new SurfaceIntersectionPatch(spatial,support,query.param[0],query.param[1],slope);var n=0;
            var status=SurfaceIsoPlaneRoots.Find(coordinate,level,edge.Start,edge.End,roots,ref n,0,true,isolation,use:RootIsolationUse.NativeSupportCoordinate);
            if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<n;i++)
            {
                var root=isolation[i];if(!root.Certified)return AlgorithmStatus.NotConverged;
                status=coordinate.Bound(new(root.Root.Lower,root.Root.Upper,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                if(bounds[1].Upper<0||bounds[2].Upper<query.param[1])continue;
                if(bounds[1].Lower<=0||bounds[2].Lower<=query.param[1])return AlgorithmStatus.NotConverged;
                if(i>0&&record.Class is CurveClass.Circle or CurveClass.Ellipse&&edge.End-edge.Start==Math.Tau
                    &&edge.StartVertex==edge.EndVertex&&isolation[0].Root.Lower<=edge.Start&&isolation[0].Root.Upper>=edge.Start
                    &&root.Root.Lower<=edge.End&&root.Root.Upper>=edge.End)continue;
                var sign=(use.Forward!=0)==(LocalSenseSign(record.Sense)>0)?1:-1;
                winding+=root.Increasing?-sign:sign;
            }
        }
        return AlgorithmStatus.Success;
    }
}
