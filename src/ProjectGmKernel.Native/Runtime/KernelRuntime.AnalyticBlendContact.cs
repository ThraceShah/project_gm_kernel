using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryAnalyticBlendContactMap(SurfTag surface,in CurveRecord boundary,double first,double last,
        out double section,out double shift,out double scale)
    {
        section=shift=scale=0;var slot=GetSurfaceSlotByTag(surface);if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface
            ||!TryReadExplicitSpineCurve(boundary,out var curve))return false;
        var data=BlendedEdgeDataPool[Surfaces[slot].DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var spine=program.Spines[root.Spine];if(!SpineParameterMap.Explicit(spine))return false;
        var matched=false;
        for(var side=0;side<2;side++)
        {
            var support=program.Surfaces[side==0?root.First:root.Second];
            if(AnalyticSpineContact.Curve(spine.Curve,support,side==0?root.Range0:root.Range1,out var contact)!=AlgorithmStatus.Success
                ||!AnalyticSpineContact.TryParameterMap(contact,curve,1e-10,first,last,out var candidateShift,out var candidateScale))continue;
            if(matched)return false;
            matched=true;shift=candidateShift;scale=candidateScale;
            section=side==0?1:0;if(root.ReverseSection)section=1-section;
            if(curve.Kind is CurveClass.Circle or CurveClass.Ellipse)
            {
                var center=shift+scale*(first+(last-first)*.5);var domain=Surfaces[slot];
                shift+=Math.Tau*Math.Round((domain.UMin+(domain.UMax-domain.UMin)*.5-center)/Math.Tau);
            }
        }
        return matched;
    }

    private static AlgorithmStatus IntersectAnalyticBlendContactRay(SurfTag surface,PK_UV_s query,double du,double dv,in ModelingEdge edge,
        double section,double shift,double scale,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;if(dv==0)return Math.Abs(query.param[1]-section)<1e-12?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
        var along=(section-query.param[1])/dv;if(along<0)return AlgorithmStatus.Success;
        var u=query.param[0]+du*along;var record=Surfaces[GetSurfaceSlotByTag(surface)];
        if(u<record.UMin||u>record.UMax)return AlgorithmStatus.Success;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
        var status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,u,section,layout,point);if(status!=AlgorithmStatus.Success)return status;
        var parameter=(u-shift)/scale;
        if(!OperationIntersection.InInterval(parameter,edge.Start,edge.End))return AlgorithmStatus.Success;
        return OperationIntersection.AddPoint(output,ref count,point[0],along,parameter,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
    }
}
