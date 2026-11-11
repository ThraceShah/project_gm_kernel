using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A certified source-line root on a rolling support belongs to that
    // support's contact locus. Keep its source constraint when registering
    // the event, including when it is an interior root rather than a seed
    // vertex. The cut's finite station and the source-root enclosure select
    // the occurrence; the position check cannot join unrelated roots.
    private static bool TryCertifiedContactRootPoint(in SheetCutCurve cut,in RegularBoundaryRoot root,double parameter,
        Span<KernelVector3> workspace,out KernelVector3 point)
    {
        point=default;
        if(root.Certified==0||root.Witness==0||root.Root.Vertex>=0
            ||!double.IsFinite(root.Root.SpatialError)||root.Root.SpatialError<0
            ||parameter<cut.First||parameter>cut.Last)return false;
        var source=Curves[GetCurveSlotByTag(root.Curve)];var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(source.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(source,out var line)
            ||!TryAnalyticBlendContactMap(root.CuttingSurface,target,cut.First,cut.Last,out var section,out _,out _))return false;
        var surface=Surfaces[GetSurfaceSlotByTag(root.CuttingSurface)];var data=BlendedEdgeDataPool[surface.DataIndex];
        var program=GetNestedBlendProgram(data);var rolling=program.Surfaces[data.ProgramRoot];
        var first=(section==1)!=rolling.ReverseSection;var support=first?data.Surface0Tag:data.Surface1Tag;
        if(!TryCertifySupportChart(support,root.ContactSurface,out _)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(support)],out var analytic))return false;
        var p=new IntervalVector(root.Root.Point);
        if(analytic.Kind==SurfaceClass.Cylinder?!PointExactlyOnCylinder(analytic,root.Root.Point)
            :analytic.Kind!=SurfaceClass.Plane||!ExactPointOnPlaneLevel(p,analytic))return false;
        var cross=IntervalVector.Cross(p-new IntervalVector(line.Origin),new IntervalVector(line.First));
        if(cross.X.Value.Magnitude!=0||cross.Y.Value.Magnitude!=0||cross.Z.Value.Magnitude!=0
            ||!CertifiedRootMatchesCutAnchor(root,cut.Curve,parameter,workspace))return false;
        point=root.Root.Point;return true;
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestCertifiedContactRootPoint(CurveTag source,CurveTag target,SurfTag support,SurfTag rolling,
        in KernelVector3 point,double lower,double upper,double error,double first,double last,double parameter,bool witnessed=true)
    {
        Span<KernelVector3> workspace=stackalloc KernelVector3[1];
        var root=new RegularBoundaryRoot{Curve=source,ContactSurface=support,CuttingSurface=rolling,Certified=1,Witness=witnessed?1:0,
            Root=new(){Point=point,Vertex=-1,Parameter=lower+(upper-lower)*.5,Lower=lower,Upper=upper,SpatialError=error}};
        return TryCertifiedContactRootPoint(new(){Curve=target,First=first,Last=last},root,parameter,workspace,out _);
    }
#endif

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
