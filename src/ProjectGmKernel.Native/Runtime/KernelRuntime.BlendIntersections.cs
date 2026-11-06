using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus DiscoverBlendIntersections(SurfaceSlot first,SurfaceSlot second,PK_SURF_intersect_surf_o_s* options,
        Span<SurfaceIntersectionNode> nodes,out BufferCount nn,Span<SurfaceIntersectionSegment> edges,out BufferCount ne)
    {
        nn=ne=0;if(options!=null&&options->have_box!=0)return AlgorithmStatus.Unsupported;
        var reverse=Surfaces[first].Class!=SurfaceClass.BlendSurface;var a=Surfaces[reverse?second:first];var b=Surfaces[reverse?first:second];
        var domain=BlendIntersectionDomain(a,options,!reverse);if(!domain.Valid||domain.U1<=domain.U0||domain.V1<=domain.V0)return AlgorithmStatus.InvalidInput;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var memory=CommandScratch.Current.Take(100000);if(memory.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;var localNodes=ModelSpan<SurfaceIntersectionNode>(bytes,ref cursor,GeneralIntersectionNodeLimit);
            var localEdges=ModelSpan<SurfaceIntersectionSegment>(bytes,ref cursor,GeneralIntersectionNodeLimit);var map=ModelSpan<BufferOffset>(bytes,ref cursor,GeneralIntersectionNodeLimit);
            var data=BlendedEdgeDataPool[a.DataIndex];var program=GetNestedBlendProgram(data);var definition=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
            var status=AlgorithmStatus.Success;IntervalBox3 extent=default;
            var analytic=TryPrepareSurface(b,out var support);var otherBlend=b.Class==SurfaceClass.BlendSurface;
            if(!analytic&&!otherBlend&&b.Class!=SurfaceClass.BSurface)return AlgorithmStatus.Unsupported;
            var otherDomain=BlendIntersectionDomain(b,options,reverse);var boundedOther=otherBlend||b.Class==SurfaceClass.BSurface;
            if(boundedOther&&(!otherDomain.Valid||otherDomain.U1<=otherDomain.U0||otherDomain.V1<=otherDomain.V0))return AlgorithmStatus.InvalidInput;
            if(analytic&&options!=null&&(reverse?options->have_uvbox_1:options->have_uvbox_2)!=0)return AlgorithmStatus.Unsupported;
            var bv=b.Class==SurfaceClass.BSurface?GetBSurfaceView(BSurfaceDataStore[b.DataIndex]):default;
            if(b.Class==SurfaceClass.BSurface&&(!GeneralSurfaceContinuity(bv)||bv.UDegree>16||bv.VDegree>16))return AlgorithmStatus.Unsupported;
            var otherData=otherBlend?BlendedEdgeDataPool[b.DataIndex]:default;var otherProgram=otherBlend?GetNestedBlendProgram(otherData):default;
            var otherCharts=otherBlend?otherProgram.Charts.Slice(otherProgram.Spines[otherProgram.Surfaces[otherData.ProgramRoot].Spine].ChartOffset,otherProgram.Spines[otherProgram.Surfaces[otherData.ProgramRoot].Spine].ChartCount):default;
            var count=analytic?AnalyticBezierPatches.Count(support):otherBlend?otherCharts.Length-1:(bv.UVertexCount-bv.UDegree)*(bv.VVertexCount-bv.VDegree);
            BufferCount remaining=ParametricSurfaceIntersection.MaxCells;
            Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],clipped=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
            for(var i=0;i<charts.Length-1;i++)
            {
                var lo=Math.Max(domain.U0,charts[i].Parameter);var hi=Math.Min(domain.U1,charts[i+1].Parameter);if(!(hi>lo))continue;
                var patch=new SurfaceIntersectionPatch(program,data.ProgramRoot,new(lo,hi,domain.V0,domain.V1));
                if(analytic)
                {
                    status=BlendExtent(program,data.ProgramRoot,patch.Domain,out extent);if(status!=AlgorithmStatus.Success)return status;
                }
                for(var j=0;j<count;j++)
                {
                    scoped SurfaceIntersectionPatch other;
                    if(otherBlend)
                    {
                        var lower=Math.Max(otherDomain.U0,otherCharts[j].Parameter);var upper=Math.Min(otherDomain.U1,otherCharts[j+1].Parameter);if(!(upper>lower))continue;
                        other=new(otherProgram,otherData.ProgramRoot,new(lower,upper,otherDomain.V0,otherDomain.V1));
                    }
                    else if(analytic)
                    {
                        var scale=Math.Max(1,Math.Max(Math.Max(Math.Abs(extent.XLo),Math.Abs(extent.XHi)),Math.Max(Math.Max(Math.Abs(extent.YLo),Math.Abs(extent.YHi)),Math.Max(Math.Abs(extent.ZLo),Math.Abs(extent.ZHi)))));
                        status=AnalyticBezierPatches.Create(support,extent,j,poles,out var p,out var q,out var box,1e-10*scale);if(status!=AlgorithmStatus.Success)return status;
                        other=new(new BezierSurfacePatch(poles[..(4*(p+1)*(q+1))],p,q,box));
                    }
                    else
                    {
                        var u=j/(bv.VVertexCount-bv.VDegree)+bv.UDegree;var v=j%(bv.VVertexCount-bv.VDegree)+bv.VDegree;
                        var lower=Math.Max(otherDomain.U0,bv.UKnots[u]);var upper=Math.Min(otherDomain.U1,bv.UKnots[u+1]);var low=Math.Max(otherDomain.V0,bv.VKnots[v]);var high=Math.Min(otherDomain.V1,bv.VKnots[v+1]);if(!(upper>lower&&high>low))continue;
                        status=BezierSurfaceBounds.Extract(bv,u,v,poles);if(status!=AlgorithmStatus.Success)return status;
                        // Keep the extraction's original domain; clipping is
                        // a restriction of the exact homogeneous polynomial.
                        status=BezierSurfaceBounds.Restrict(new(poles[..(4*(bv.UDegree+1)*(bv.VDegree+1))],bv.UDegree,bv.VDegree,new(bv.UKnots[u],bv.UKnots[u+1],bv.VKnots[v],bv.VKnots[v+1])),new(lower,upper,low,high),clipped);if(status!=AlgorithmStatus.Success)return status;
                        clipped[..(4*(bv.UDegree+1)*(bv.VDegree+1))].CopyTo(poles);other=new(new BezierSurfacePatch(poles[..(4*(bv.UDegree+1)*(bv.VDegree+1))],bv.UDegree,bv.VDegree,new(lower,upper,low,high)));
                    }
                    var ln=0;var le=0;status=ParametricSurfaceIntersection.FindProcedural(patch,other,localNodes,ref ln,localEdges,ref le,ref remaining);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<ln;k++)
                    {
                        if(analytic){status=OperationIntersection.Parameters(support,localNodes[k].Point,out localNodes[k].S,out localNodes[k].T);if(status!=AlgorithmStatus.Success)return status;}
                        if(reverse){(localNodes[k].U,localNodes[k].S)=(localNodes[k].S,localNodes[k].U);(localNodes[k].V,localNodes[k].T)=(localNodes[k].T,localNodes[k].V);}
                        // A certified arc reaching an artificial extent already
                        // disproves completeness. Reject before discovering all
                        // remaining patch pairs; no partial graph is published.
                        if((AtArtificialBlendLimit(first,localNodes[k].U,options,true)||AtArtificialBlendLimit(second,localNodes[k].S,options,false))
                            &&!GeneralIntersectionBoundary(first,second,localNodes[k],options))return AlgorithmStatus.NotConverged;
                        var found=-1;for(var m=0;m<nn;m++)if(GeneralIntersectionSameNode(first,second,nodes[m],localNodes[k])){found=m;break;}
                        if(found<0){if(nn==nodes.Length)return AlgorithmStatus.OutputTooSmall;found=nn;nodes[nn++]=localNodes[k];}map[k]=found;
                    }
                    for(var k=0;k<le;k++)
                    {
                        var start=map[localEdges[k].Start];var end=map[localEdges[k].End];if(start==end)continue;var duplicate=false;
                        for(var m=0;m<ne;m++)if(edges[m].Start==start&&edges[m].End==end||edges[m].Start==end&&edges[m].End==start){duplicate=true;break;}
                        if(duplicate)continue;if(ne==edges.Length)return AlgorithmStatus.OutputTooSmall;edges[ne++]=new(){Start=start,End=end,ChordalError=localEdges[k].ChordalError};
                    }
                }
            }
            return AlgorithmStatus.Success;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static bool BlendHasArtificialLimit(SurfaceSlot slot)
    {
        var data=BlendedEdgeDataPool[Surfaces[slot].DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
        return spine.StartType==LimitType.Artificial||spine.EndType==LimitType.Artificial;
    }

    private static bool AtArtificialBlendLimit(SurfaceSlot slot,double parameter,PK_SURF_intersect_surf_o_s* options,bool first)
    {
        var record=Surfaces[slot];if(record.Class!=SurfaceClass.BlendSurface||options!=null&&(first?options->have_uvbox_1:options->have_uvbox_2)!=0)return false;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
        var epsilon=2e-9*Math.Max(1,record.UMax-record.UMin);
        return spine.StartType==LimitType.Artificial&&Math.Abs(parameter-record.UMin)<=epsilon
            ||spine.EndType==LimitType.Artificial&&Math.Abs(parameter-record.UMax)<=epsilon;
    }

    private static SurfaceParameterBox BlendIntersectionDomain(in SurfaceRecord surface,PK_SURF_intersect_surf_o_s* options,bool first)
    {
        var domain=new SurfaceParameterBox(surface.UMin,surface.UMax,surface.VMin,surface.VMax);
        if(options==null||(first?options->have_uvbox_1:options->have_uvbox_2)==0)return domain;
        var box=first?options->uvbox_1:options->uvbox_2;
        if(!double.IsFinite(box.param[0])||!double.IsFinite(box.param[1])||!double.IsFinite(box.param[2])||!double.IsFinite(box.param[3])||box.param[2]<=box.param[0]||box.param[3]<=box.param[1])return new(double.NaN,0,0,0);
        return new(Math.Max(domain.U0,box.param[0]),Math.Min(domain.U1,box.param[2]),Math.Max(domain.V0,box.param[1]),Math.Min(domain.V1,box.param[3]));
    }

    private static AlgorithmStatus BlendExtent(in NestedBlendProgram program,BlendSurfaceIndex surface,in SurfaceParameterBox domain,out IntervalBox3 extent,bool spineOnly=false)
    {
        extent=default;Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[64];pending[0]=domain;var count=1;var visited=0;var first=true;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        while(count>0)
        {
            if(++visited>8192)return AlgorithmStatus.NotConverged;var box=pending[--count];var status=spineOnly?JointNestedBlendEvaluation.BoundSpine(program,surface,box,bounds):JointNestedBlendEvaluation.Bound(program,surface,box,bounds);
            if(status==AlgorithmStatus.NotConverged)
            {
                if(count+2>pending.Length||box.U1-box.U0<1e-12*Math.Max(1,domain.U1-domain.U0))return status;var middle=box.U0+(box.U1-box.U0)*.5;
                pending[count++]=new(box.U0,middle,box.V0,box.V1);pending[count++]=new(middle,box.U1,box.V0,box.V1);continue;
            }
            if(status!=AlgorithmStatus.Success)return status;
            extent=first?new(bounds[0].Lower,bounds[0].Upper,bounds[1].Lower,bounds[1].Upper,bounds[2].Lower,bounds[2].Upper):new(Math.Min(extent.XLo,bounds[0].Lower),Math.Max(extent.XHi,bounds[0].Upper),Math.Min(extent.YLo,bounds[1].Lower),Math.Max(extent.YHi,bounds[1].Upper),Math.Min(extent.ZLo,bounds[2].Lower),Math.Max(extent.ZHi,bounds[2].Upper));first=false;
        }
        return first?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }
}
