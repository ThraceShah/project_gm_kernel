using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryAffineCurve(in IntersectionCurveSpan span,out KernelVector3 origin,out KernelVector3 direction,
        out double baseParameter,out double scale,out double first,out double last)
    {
        origin=direction=default;baseParameter=first=last=0;scale=1;var curve=Curves[span.Slot];
        if(curve.Class==CurveClass.Line){GetIntersectionLine(curve,out origin,out direction);first=span.Lower;last=span.Upper;return true;}
        if(curve.Class!=CurveClass.BCurve)return false;var view=GetBCurveView(BCurveDataStore[curve.DataIndex]);
        if(view.VertexCount!=view.Degree+1||view.Dimension!=(view.Rational?4:3)||view.Degree<1)return false;
        for(var i=0;i<=view.Degree;i++)if(view.Knots[i]!=view.Start||view.Knots[view.Knots.Length-1-i]!=view.End)return false;
        var p=view.Vertices;var dim=view.Dimension;var w=view.Rational?p[3]:1;if(!(w>0))return false;
        origin=ProfileEuclideanPole(p,dim,0);var end=ProfileEuclideanPole(p,dim,view.Degree);direction=Sub(end,origin);
        var length=OperationIntersection.Norm(direction);if(!(length>0))return false;
        for(var i=0;i<=view.Degree;i++)
        {
            if(view.Rational&&p[dim*i+3]!=w)return false;var at=ProfileEuclideanPole(p,dim,i);
            if(at.X*view.Degree!=origin.X*(view.Degree-i)+end.X*i||at.Y*view.Degree!=origin.Y*(view.Degree-i)+end.Y*i||at.Z*view.Degree!=origin.Z*(view.Degree-i)+end.Z*i)return false;
        }
        direction=Scale(direction,1/length);baseParameter=view.Start;scale=length/(view.End-view.Start);
        first=(span.Lower-baseParameter)*scale;last=(span.Upper-baseParameter)*scale;return true;
    }

    private static bool AffineCurveOnSurfaceBoundary(SurfaceSlot surface,in IntersectionCurveSpan span)
    {
        if(Surfaces[surface].Class!=SurfaceClass.BSurface||!TryAffineCurve(span,out var origin,out var direction,out _,out _,out var lo,out var hi))return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[surface].DataIndex]);var p=view.Vertices;var dim=view.Dimension;
        if(view.UVertexCount!=view.UDegree+1||view.VVertexCount!=view.VDegree+1)return false;
        for(var i=0;i<=view.UDegree;i++)if(view.UKnots[i]!=view.UStart||view.UKnots[view.UKnots.Length-1-i]!=view.UEnd)return false;
        for(var i=0;i<=view.VDegree;i++)if(view.VKnots[i]!=view.VStart||view.VKnots[view.VKnots.Length-1-i]!=view.VEnd)return false;
        var x=OperationIntersection.Reference(direction);var y=ProfileCross(direction,x);
        for(var boundary=0;boundary<4;boundary++)
        {
            var constantU=boundary<2;var n=constantU?view.VVertexCount:view.UVertexCount;var previous=double.NegativeInfinity;var increasing=true;var decreasing=true;var valid=true;var first=0.0;var last=0.0;
            for(var i=0;i<n;i++)
            {
                var u=constantU?(boundary==0?0:view.UVertexCount-1):i;var v=constantU?i:(boundary==2?0:view.VVertexCount-1);var at=dim*(u*view.VVertexCount+v);
                IntervalNumber weight=view.Rational?p[at+3]:1;if(weight.Lower<=0){valid=false;break;}
                var rx=new IntervalNumber(p[at])/weight-origin.X;var ry=new IntervalNumber(p[at+1])/weight-origin.Y;var rz=new IntervalNumber(p[at+2])/weight-origin.Z;
                // Positive rational basis functions preserve this complete
                // control-hull tube around the carrier, including its ends.
                if((rx*x.X+ry*x.Y+rz*x.Z).Magnitude>1e-12||(rx*y.X+ry*y.Y+rz*y.Z).Magnitude>1e-12){valid=false;break;}
                var value=ProfileEuclideanPole(p,dim,u*view.VVertexCount+v);var along=Dot(Sub(value,origin),direction);
                if(i==0)first=along;else{increasing&=along>=previous;decreasing&=along<=previous;}previous=last=along;
            }
            if(valid&&(increasing||decreasing)&&lo>=Math.Min(first,last)-1e-12&&hi<=Math.Max(first,last)+1e-12)return true;
        }
        return false;
    }
}
