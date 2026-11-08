using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int TransformNestedProgram(DataSlot block,BufferCount ns,BufferCount nc,BufferCount nh,BufferCount nd,
        PartitionSlot partition,in KernelAffineTransform matrix,double scale,double parity)
    {
        if(!SnapshotPayload(block,NestedBlendProgramBytes(ns,nc,nh,nd),partition))return Generated.ParasolidConstants.PK_ERROR_memory_full;
        var pointer=(byte*)DereferenceBlock(block);var surfaces=new Span<BlendSurfaceInstruction>(pointer,ns);pointer+=ns*sizeof(BlendSurfaceInstruction);
        var spines=new Span<BlendSpineInstruction>(pointer,nc);pointer+=nc*sizeof(BlendSpineInstruction);
        var charts=new Span<BlendChartInstruction>(pointer,nh);pointer+=nh*sizeof(BlendChartInstruction);var scalars=new Span<double>(pointer,nd);
        for(var i=0;i<ns;i++)
        {
            ref var node=ref surfaces[i];
            if(node.IsBlend)
            {
                node.Range0*=scale;node.Range1*=scale;
                if(parity<0){(node.First,node.Second)=(node.Second,node.First);(node.Range0,node.Range1)=(node.Range1,node.Range0);}
                continue;
            }
            if(node.IsBSpline)
            {
                var already=false;for(var j=0;j<i;j++)if(surfaces[j].IsBSpline&&surfaces[j].ScalarOffset==node.ScalarOffset){already=true;break;}
                if(!already)
                {
                    var error=TransformPoles(scalars.Slice(node.ScalarOffset,node.UCount*node.VCount*node.Dimension),node.Dimension,node.Rational,matrix);if(error!=0)return error;
                }
                node.Offset*=scale*parity;node.NormalSense*=parity;continue;
            }
            var a=node.Analytic;node.Analytic=new(a.Kind,matrix.TransformPoint(a.Origin),matrix.TransformUnitDirection(a.Axis,scale),matrix.TransformUnitDirection(a.X,scale),a.Radius*scale,a.Kind==SurfaceClass.Cone?a.Secondary:a.Secondary*scale);
        }
        for(var i=0;i<nc;i++)
        {
            if(spines[i].Curve.Kind==CurveClass.BCurve)
            {
                var spine=spines[i];var poles=(spine.ChartCount-1)*spine.SpineDegree+1;
                var error=TransformPoles(scalars.Slice(spine.SpineScalarOffset,4*poles),4,true,matrix);if(error!=0)return error;
            }
            else if(SpineParameterMap.Explicit(spines[i]))
            {
                ref var curve=ref spines[i].Curve;curve.Origin=matrix.TransformPoint(curve.Origin);
                curve.First=matrix.TransformUnitDirection(curve.First,curve.Kind==CurveClass.Line?scale:1);
                curve.Second=matrix.TransformVector(curve.Second);
                if(curve.Kind==CurveClass.Line)
                    for(var j=0;j<spines[i].ChartCount;j++)
                    {
                        ref var chart=ref charts[spines[i].ChartOffset+j];chart.Parameter*=scale;chart.Scale*=scale;
                    }
            }
            spines[i].ChordalError*=scale;if(parity<0)(spines[i].First,spines[i].Second)=(spines[i].Second,spines[i].First);
        }
        for(var i=0;i<nh;i++)
        {
            charts[i].Position=matrix.TransformPoint(charts[i].Position);charts[i].Chord=matrix.TransformUnitDirection(charts[i].Chord,scale);charts[i].Scale/=scale;
            if(parity<0){(charts[i].FirstU,charts[i].SecondU)=(charts[i].SecondU,charts[i].FirstU);(charts[i].FirstV,charts[i].SecondV)=(charts[i].SecondV,charts[i].FirstV);}
            if(!FiniteVector(charts[i].Position)||!FiniteVector(charts[i].Chord)||!double.IsFinite(charts[i].Scale))return Generated.ParasolidConstants.PK_ERROR_failed_to_transform;
        }
        return 0;
    }
}
