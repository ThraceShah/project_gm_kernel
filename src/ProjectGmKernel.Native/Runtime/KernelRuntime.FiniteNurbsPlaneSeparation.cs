using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // This certificate covers the full natural rectangle. A trimmed face,
    // arbitrary ICurve border, or unproved exterior continuation cannot use it.
    private static bool RegularWholeNaturalFace(in RegularBoundaryView input,BufferOffset face)
    {
        var f=input.Faces[face];var slot=GetSurfaceSlotByTag(f.Surface);if(Surfaces[slot].Class!=SurfaceClass.BSurface||f.LoopCount!=1)return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]);if(view.UPeriodic||view.VPeriodic)return false;
        for(var k=0;k<=view.UDegree;k++)if(view.UKnots[k]!=view.UStart||view.UKnots[view.UKnots.Length-1-k]!=view.UEnd)return false;
        for(var k=0;k<=view.VDegree;k++)if(view.VKnots[k]!=view.VStart||view.VKnots[view.VKnots.Length-1-k]!=view.VEnd)return false;
        var loop=input.Loops[f.FirstLoop];if(loop.CoedgeCount!=4)return false;var mask=0;
        for(var e=0;e<4;e++)
        {
            var edge=input.Edges[input.Coedges[loop.FirstCoedge+e].Edge];var record=Curves[GetCurveSlotByTag(edge.Curve)];if(record.Class!=CurveClass.BCurve)return false;
            var curve=GetBCurveView(BCurveDataStore[record.DataIndex]);if(edge.Start!=curve.Start||edge.End!=curve.End)return false;
            var matched=-1;
            for(var side=0;side<4;side++)
            {
                if((mask&(1<<side))!=0)continue;var fixedU=side<2;var count=fixedU?view.VVertexCount:view.UVertexCount;var knots=fixedU?view.VKnots:view.UKnots;
                if(curve.Degree!=(fixedU?view.VDegree:view.UDegree)||curve.VertexCount!=count||curve.Knots.Length!=knots.Length)continue;
                for(var reverse=0;reverse<2;reverse++)
                {
                    var same=true;for(var k=0;k<knots.Length;k++)same&=curve.Knots[k]==(reverse==0?knots[k]:knots[0]+knots[^1]-knots[knots.Length-1-k]);if(!same)continue;
                    var ratio=0.0;
                    for(var k=0;k<count&&same;k++)
                    {
                        var index=reverse==0?k:count-1-k;var u=fixedU?(side==0?0:view.UVertexCount-1):index;var v=fixedU?index:(side==2?0:view.VVertexCount-1);
                        var a=curve.Dimension*k;var b=view.Dimension*(u*view.VVertexCount+v);var cw=curve.Rational?curve.Vertices[a+3]:1;var sw=view.Rational?view.Vertices[b+3]:1;
                        if(!(cw>0&&sw>0)){same=false;break;}if(k==0)ratio=cw/sw;else same&=cw==ratio*sw;
                        for(var d=0;d<3;d++)same&=curve.Vertices[a+d]==ratio*view.Vertices[b+d];
                    }
                    if(same){matched=side;break;}
                }
                if(matched>=0)break;
            }
            if(matched<0)return false;mask|=1<<matched;
        }
        return mask==15;
    }

    private static bool RegularNaturalNurbsSeparatedFromPlane(in RegularBoundaryView input,BufferOffset curved,BufferOffset planar,double tolerance)
    {
        var planeSlot=GetSurfaceSlotByTag(input.Faces[planar].Surface);if(!RegularPlanarSupport(planeSlot,out var plane,out var deviation)||!RegularWholeNaturalFace(input,curved))return false;
        var slot=GetSurfaceSlotByTag(input.Faces[curved].Surface);if(RegularSurfaceExtent(slot,out var extent)!=AlgorithmStatus.Success)return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bounds=stackalloc IntervalNumber[9];
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[32];Span<BufferCount> depths=stackalloc BufferCount[32];var budget=4096;var sign=0;
        for(var patchIndex=0;patchIndex<RegularSurfacePatchCount(slot);patchIndex++)
        {
            if(RegularSurfacePatch(slot,patchIndex,extent,poles,out var patch)!=AlgorithmStatus.Success)return false;
            pending[0]=patch.Domain;depths[0]=0;var n=1;
            while(n>0)
            {
                if(--budget<0)return false;var box=pending[--n];var depth=depths[n];if(patch.Bound(box,bounds)!=AlgorithmStatus.Success)return false;
                var distance=(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
                var side=distance.Lower>tolerance+deviation?1:distance.Upper< -tolerance-deviation?-1:0;
                if(side!=0){if(sign!=0&&sign!=side)return false;sign=side;continue;}
                if(depth==16)return false;
                if(box.U1-box.U0>=box.V1-box.V0){var mid=box.U0+(box.U1-box.U0)*.5;pending[n]=new(box.U0,mid,box.V0,box.V1);pending[n+1]=new(mid,box.U1,box.V0,box.V1);}
                else{var mid=box.V0+(box.V1-box.V0)*.5;pending[n]=new(box.U0,box.U1,box.V0,mid);pending[n+1]=new(box.U0,box.U1,mid,box.V1);}
                depths[n]=depths[n+1]=depth+1;n+=2;
            }
        }
        return sign!=0;
    }
}
