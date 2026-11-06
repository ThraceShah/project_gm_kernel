using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation.Numerics;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool CornerZeroSection(CurveTag section,SurfTag cap,double radius)
    {
        var curve=Curves[GetCurveSlotByTag(section)];if(curve.Class!=CurveClass.Circle||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cap)],out var plane)||plane.Kind!=SurfaceClass.Plane)return false;
        var circle=CircleDataPool[curve.DataIndex];var axis=new KernelVector3{X=circle.AxisX,Y=circle.AxisY,Z=circle.AxisZ};var center=new KernelVector3{X=circle.CenterX,Y=circle.CenterY,Z=circle.CenterZ};
        var x=new KernelVector3{X=circle.RefDirX,Y=circle.RefDirY,Z=circle.RefDirZ};var y=ProfileCross(axis,x);
        IntervalNumber constant=0,cosine=0,sine=0;for(var d=0;d<3;d++)
        {
            var n=d==0?plane.Axis.X:d==1?plane.Axis.Y:plane.Axis.Z;var p=d==0?center.X:d==1?center.Y:center.Z;var q=d==0?plane.Origin.X:d==1?plane.Origin.Y:plane.Origin.Z;
            constant+=(new IntervalNumber(p)-q)*n;cosine+=new IntervalNumber(d==0?x.X:d==1?x.Y:x.Z)*n;sine+=new IntervalNumber(d==0?y.X:d==1?y.Y:y.Z)*n;
        }
        // Enclose the whole circle's affine plane residual. Vanishing is
        // accepted only inside the modeling coincidence tolerance.
        var bound=constant+circle.Radius*(cosine*new IntervalNumber(-1,1)+sine*new IntervalNumber(-1,1));var tolerance=1e-9*Math.Max(1,radius);
        return bound.Lower>=-tolerance&&bound.Upper<=tolerance;
    }

    private static int ReduceCornerEvent(ref SheetArrangement model,BufferCount originalFaces,ReadOnlySpan<FaceSlot> sources,Span<FaceSlot> retained,out BufferCount retainedCount)
    {
        retainedCount=0;Span<BufferOffset> canonical=stackalloc BufferOffset[model.NE];Span<byte> reversed=stackalloc byte[model.NE];
        canonical.Fill(-1);reversed.Clear();
        for(var e=0;e<model.NE;e++)
        {
            var edge=model.Edges[e];if(edge.StartVertex==edge.EndVertex)continue;canonical[e]=e;
            for(var other=0;other<e;other++)
            {
                if(canonical[other]!=other)continue;var candidate=model.Edges[other];var same=edge.StartVertex==candidate.StartVertex&&edge.EndVertex==candidate.EndVertex;
                var opposite=edge.StartVertex==candidate.EndVertex&&edge.EndVertex==candidate.StartVertex;
                if(Curves[GetCurveSlotByTag(edge.Curve)].Class==CurveClass.Circle&&(Math.Abs(edge.End-edge.Start)>=Math.PI-1e-10||Math.Abs(candidate.End-candidate.Start)>=Math.PI-1e-10))continue;
                if((same||opposite)&&CornerEquivalentCarriers(edge.Curve,candidate.Curve)){canonical[e]=other;reversed[e]=opposite?(byte)1:(byte)0;break;}
            }
        }
        var nc=0;var nf=0;var nl=0;Span<ModelingCoedge> cycle=stackalloc ModelingCoedge[model.NC];
        for(var f=0;f<model.NF;f++)
        {
            var face=model.Faces[f];if(face.LoopCount!=1)return CornerReject(BlendCornerEvent.UnsupportedGraph);var old=model.Loops[face.FirstLoop];var count=0;
            for(var c=0;c<old.CoedgeCount;c++)
            {
                var coedge=model.Coedges[old.FirstCoedge+c];var edge=canonical[coedge.Edge];if(edge<0)continue;
                coedge.Forward^=reversed[coedge.Edge];coedge.Edge=edge;cycle[count++]=coedge;
            }
            var changed=true;while(changed&&count>1)
            {
                changed=false;for(var c=0;c<count;c++)
                {
                    var next=(c+1)%count;if(cycle[c].Edge!=cycle[next].Edge||cycle[c].Forward==cycle[next].Forward)continue;
                    if(next==0){cycle[1..(count-1)].CopyTo(cycle);count-=2;}else{cycle[(next+1)..count].CopyTo(cycle[c..]);count-=2;}
                    changed=true;break;
                }
            }
            if(count==0)continue;if(count<3)return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
            var error=OrderCornerCycle(model.Edges,cycle[..count]);if(error!=0)return error;
            cycle[..count].CopyTo(model.Coedges[nc..]);model.Loops[nl]=new(){FirstCoedge=nc,CoedgeCount=count};nc+=count;
            face.FirstLoop=nl++;model.Faces[nf]=face;model.Sources[nf]=model.Sources[f];if(f<originalFaces)retained[retainedCount++]=sources[f];nf++;
        }
        model.NF=nf;model.NL=nl;model.NC=nc;return 0;
    }

    private static bool CornerEquivalentCarriers(CurveTag first,CurveTag second)
    {
        var a=Curves[GetCurveSlotByTag(first)];var b=Curves[GetCurveSlotByTag(second)];if(a.Class!=b.Class)return false;
        if(a.Class==CurveClass.Line)
        {
            var x=LineDataPool[a.DataIndex];var y=LineDataPool[b.DataIndex];var u=new KernelVector3{X=x.AxisX,Y=x.AxisY,Z=x.AxisZ};var v=new KernelVector3{X=y.AxisX,Y=y.AxisY,Z=y.AxisZ};
            return CornerParallelWithin(u,v,1e-10)&&CornerParallelWithin(new(){X=x.LocationX-y.LocationX,Y=x.LocationY-y.LocationY,Z=x.LocationZ-y.LocationZ},u,1e-9);
        }
        if(a.Class==CurveClass.Circle)
        {
            var x=CircleDataPool[a.DataIndex];var y=CircleDataPool[b.DataIndex];var radial=new IntervalNumber(x.Radius)-y.Radius;return radial.Lower>=-1e-10&&radial.Upper<=1e-10&&CornerPointsWithin(new(){X=x.CenterX,Y=x.CenterY,Z=x.CenterZ},new(){X=y.CenterX,Y=y.CenterY,Z=y.CenterZ},1e-9)&&CornerParallelWithin(new(){X=x.AxisX,Y=x.AxisY,Z=x.AxisZ},new(){X=y.AxisX,Y=y.AxisY,Z=y.AxisZ},1e-10);
        }
        return first==second;
    }
    private static bool CornerPointsWithin(in KernelVector3 first,in KernelVector3 second,double tolerance)
    {
        var x=new IntervalNumber(first.X)-second.X;var y=new IntervalNumber(first.Y)-second.Y;var z=new IntervalNumber(first.Z)-second.Z;
        return (IntervalNumber.Square(x)+IntervalNumber.Square(y)+IntervalNumber.Square(z)).Upper<=tolerance*tolerance;
    }
    private static bool CornerParallelWithin(in KernelVector3 first,in KernelVector3 second,double tolerance)
    {
        var x=new IntervalNumber(first.Y)*second.Z-new IntervalNumber(first.Z)*second.Y;var y=new IntervalNumber(first.Z)*second.X-new IntervalNumber(first.X)*second.Z;var z=new IntervalNumber(first.X)*second.Y-new IntervalNumber(first.Y)*second.X;
        return (IntervalNumber.Square(x)+IntervalNumber.Square(y)+IntervalNumber.Square(z)).Upper<=tolerance*tolerance;
    }

}
