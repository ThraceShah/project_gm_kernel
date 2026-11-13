using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int LocateNativeContactEndpoint(in SheetCutCurve cut,in RegularBoundaryRoot root,
        out double parameter,out IntervalNumber isolation,out bool handled)
    {
        parameter=0;isolation=UncertifiedStation;handled=false;
        var source=Curves[GetCurveSlotByTag(root.Curve)];var target=Curves[GetCurveSlotByTag(cut.Curve)];
        if(source.Class!=CurveClass.ICurve||target.Class!=CurveClass.ICurve)return 0;
        var from=ICurveDataPool[source.DataIndex];var to=ICurveDataPool[target.DataIndex];
        if(from.IsBlendContact==0||to.IsBlendContact!=0||to.ProgramBlock<=0
            ||!SameSheetSupport(from.Surface1Tag,root.ContactSurface))return 0;
        if(!(SameSheetSupport(to.Surface0Tag,root.ContactSurface)&&SameSheetSupport(to.Surface1Tag,root.CuttingSurface)
            ||SameSheetSupport(to.Surface1Tag,root.ContactSurface)&&SameSheetSupport(to.Surface0Tag,root.CuttingSurface)))return 0;
        var program=GetNestedBlendProgram(to.ProgramBlock,to.ProgramSurfaceCount,to.ProgramSpineCount,to.ProgramChartCount,to.ProgramScalarCount);
        var definition=program.Spines[to.ProgramSpine];if(SpineParameterMap.Explicit(definition))return 0;
        var first=program.Surfaces[definition.First];var second=program.Surfaces[definition.Second];
        if(first.IsBlend==second.IsBlend)return 0;
        var other=first.IsBlend?second:first;
        if(other.IsBSpline||other.Analytic.Kind!=SurfaceClass.Plane)return 0;
        var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);var v=BlendContactV(from);
        var support=Surfaces[GetSurfaceSlotByTag(from.Surface1Tag)];var blend=BlendedEdgeDataPool[support.DataIndex];
        Span<IntervalNumber> contact=stackalloc IntervalNumber[9],bound=stackalloc IntervalNumber[9];
        var native=root.ContactIsolation;
        if(!native.Certified||!native.Root.Finite||!native.MonotoneDomain.Finite)return 0;
        for(var end=0;end<2;end++)
        {
            var t=end==0?cut.First:cut.Last;var node=end==0?charts[0]:charts[^1];if(t!=node.Parameter)continue;
            var u=first.IsBlend?node.FirstU:node.SecondU;var section=first.IsBlend?node.FirstV:node.SecondV;
            // Both producers name the same native fixed-V contact and one
            // monotone root domain. The entire native strip from that root
            // to the stored end must fit the same precision neighbourhood;
            // the end is a finite representative, not an exact plane root.
            var strip=IntervalNumber.Hull(native.Root,new(u));
            if(section!=v||strip.Lower<native.MonotoneDomain.Lower||strip.Upper>native.MonotoneDomain.Upper)continue;
            if(JointNestedBlendEvaluation.Bound(GetNestedBlendProgram(blend,support),blend.ProgramRoot,
                new(strip.Lower,strip.Upper,v,v),contact)!=AlgorithmStatus.Success)continue;
            if(JointNestedBlendEvaluation.BoundSpine(program,to.ProgramSpine,new(t,t,0,0),bound)!=AlgorithmStatus.Success)continue;
            var error=new IntervalNumber(-root.Root.SpatialError,root.Root.SpatialError);
            var x=IntervalNumber.Hull(bound[0],IntervalNumber.Hull(contact[0],root.Root.Point.X+error));
            var y=IntervalNumber.Hull(bound[1],IntervalNumber.Hull(contact[1],root.Root.Point.Y+error));
            var z=IntervalNumber.Hull(bound[2],IntervalNumber.Hull(contact[2],root.Root.Point.Z+error));
            var diameter=new IntervalVector(new IntervalNumber(x.Upper)-x.Lower,new IntervalNumber(y.Upper)-y.Lower,new IntervalNumber(z.Upper)-z.Lower).Norm().Value;
            if(!diameter.Finite||diameter.Upper>OperationIntersection.DistanceTolerance)continue;
            if(handled)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            handled=true;parameter=t;isolation=new(t);
            if(TraceFiniteBoundary!=null)
            {
                PK_UV_s start=default,last=default;start.param[0]=native.Root.Lower;last.param[0]=native.Root.Upper;
                start.param[1]=last.param[1]=v;
                TraceFiniteBoundary(new(){Stage=BoundaryTraceStage.NativeContactEndpoint,Surface=root.ContactSurface,OtherSurface=root.CuttingSurface,
                    Curve=root.Curve,ProductCurve=cut.Curve,Generation=source.Header.Generation,SurfaceGeneration=support.Header.Generation,
                    First=t,Last=t,UvStart=start,UvEnd=last,SpatialError=diameter.Upper,InputCoedge=root.InputCoedge,Witness=root.Witness});
            }
        }
        return 0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestNativeContactEnd(EdgeSlot edgeSlot,CurveTag target,SurfTag plane,double first,double last,byte alter,out bool handled)
    {
        handled=false;var edge=Edges[edgeSlot];if(!TryProfileEdgeRange(edge,out var lower,out var upper))return ParasolidConstants.PK_ERROR_bad_parameter;
        var use=new ModelingEdge{Curve=edge.CurveTag,Start=lower,End=upper};
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> native=stackalloc ScalarRootIsolation[IntersectionPointCapacity],contact=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> errors=stackalloc double[IntersectionPointCapacity];
        var checkpoint=EnsureCommandScratch();
        try
        {
            if(!TryNativeCurvePlaneEvents(use,plane,roots,native,errors,out var count,contact)||count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(alter==4)
            {
                use.End=roots[0].First;
                var complete=TryNativeCurvePlaneEvents(use,plane,roots,native,errors,out _,contact);
                handled=complete||contact[0].Certified;return 0;
            }
            var data=ICurveDataPool[Curves[GetCurveSlotByTag(edge.CurveTag)].DataIndex];
            var root=new RegularBoundaryRoot{Curve=edge.CurveTag,ContactSurface=data.Surface1Tag,CuttingSurface=plane,Certified=NativePlaneCertificate,
                ContactIsolation=contact[0],Root=new(){Point=roots[0].Point,Parameter=roots[0].First,Lower=native[0].Root.Lower,Upper=native[0].Root.Upper,SpatialError=errors[0]}};
            if(alter==1)root.ContactIsolation.Root+=1;
            else if(alter==2)root.ContactIsolation.Certified=false;
            else if(alter==3)root.Root.Point.X+=.01;
            return LocateNativeContactEndpoint(new(){Curve=target,First=first,Last=last},root,out _,out _,out handled);
        }
        finally{if(checkpoint)ReleaseCommandScratch(State.Session);}
    }
#endif
}
