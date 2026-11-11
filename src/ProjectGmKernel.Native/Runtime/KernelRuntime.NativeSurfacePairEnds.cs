using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int MatchNativeSurfacePairEnd(in SheetCutCurve cut,in RegularBoundaryRoot source,out bool matched,out double parameter)
    {
        matched=false;parameter=0;if(cut.Closed!=0)return 0;
        var curve=Curves[GetCurveSlotByTag(cut.Curve)];var data=ICurveDataPool[curve.DataIndex];
        if(data.UvType!=IntersectionUvType.Both||data.IsBlendContact!=0)return 0;
        var side=SameSheetSupport(source.ContactSurface,data.Surface0Tag)?0:1;
        var contact=GetSurfaceSlotByTag(source.ContactSurface);var cutting=GetSurfaceSlotByTag(source.CuttingSurface);
        if(Surfaces[contact].Class!=SurfaceClass.BSurface||Surfaces[cutting].Class!=SurfaceClass.BSurface)return 0;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[contact].DataIndex]);
        var axis=source.NativeBoundaryAxis-1;var fixedValue=axis switch{0=>view.UStart,1=>view.UEnd,2=>view.VStart,_=>view.VEnd};
        var original=new IsolatedRootCertificate{First=source.CuttingDomain,Second=source.SplineDomain,
            FirstDomain=source.CuttingUniqueDomain,SecondDomain=source.SplineUniqueDomain};
        var status=RegularSurfaceExtent(contact,out var extent);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[64];
        Span<IsolatedRootCertificate> proofs=stackalloc IsolatedRootCertificate[64];
        for(var end=0;end<2;end++)
        {
            var limit=end==0?data.StartLimit:data.EndLimit;if(limit.Type!=LimitType.Artificial)continue;
            var at=end==0?curve.TMin:curve.TMax;
            if(at!=(end==0?cut.First:cut.Last))continue;
            var chart=end==0?0:data.ChartCount-1;
            if(!TryReadChartUv(data,chart,out var u0,out var v0,out var u1,out var v1))continue;
            var cu=side==0?u0:u1;var cv=side==0?v0:v1;var ou=side==0?u1:u0;var ov=side==0?v1:v0;
            if((axis<2?cu:cv)!=fixedValue)continue;
            var hull=HvecSpan(data);var h=3*(data.StartLimit.HvecCount+chart);
            var delta=new IntervalVector(source.Root.Point)-new IntervalVector(hull[h],hull[h+1],hull[h+2]);
            if((delta.Norm().Value+source.Root.SpatialError).Upper>data.ChordalError)continue;
            var candidates=0;var identical=false;
            for(var i=0;i<RegularSurfacePatchCount(cutting);i++)
            {
                status=RegularSurfacePatch(cutting,i,extent,ap,out var first);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                if(!(first.Domain.U1>first.Domain.U0&&first.Domain.V1>first.Domain.V0))continue;
                for(var j=0;j<RegularSurfacePatchCount(contact);j++)
                {
                    status=RegularSurfacePatch(contact,j,extent,bp,out var second);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    var a=first.Domain;var b=second.Domain;
                    if(!(b.U1>b.U0&&b.V1>b.V0)||axis<2&&(fixedValue<b.U0||fixedValue>b.U1)||axis>=2&&(fixedValue<b.V0||fixedValue>b.V1))continue;
                    var domain=new ParameterCell{Lower=new(){U=a.U0,V=a.V0,S=b.U0,T=b.V0},Upper=new(){U=a.U1,V=a.V1,S=b.U1,T=b.V1}};
                    var fixedAxis=axis<2?2:3;domain.Lower.SetParameter(fixedAxis,fixedValue);domain.Upper.SetParameter(fixedAxis,fixedValue);
                    status=ParametricSurfaceIntersection.Isolate(first,second,domain,fixedAxis,roots,out var n,proofs);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    for(var r=0;r<n;r++)
                    {
                        var proof=proofs[r];
                        if(!IsolatedRootCertificate.Contains(proof.FirstDomain,new(ou,ou,ov,ov))
                            ||!IsolatedRootCertificate.Contains(proof.SecondDomain,new(cu,cu,cv,cv)))continue;
                        candidates++;identical|=original.MayShareRoot(proof)&&original.ProvesSameRoot(proof);
                    }
                }
            }
            // This provider endpoint came from this same finite boundary
            // equation. Its raw UV selects one isolated branch, while the
            // two certificates establish root identity in native parameters.
            if(candidates!=1||!identical)continue;
            if(matched&&parameter!=at)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            matched=true;parameter=at;
        }
        return 0;
    }
}
