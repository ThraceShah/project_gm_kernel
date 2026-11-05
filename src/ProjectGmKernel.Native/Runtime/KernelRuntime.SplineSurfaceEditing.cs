using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int EditBSurface(SurfTag surface,SplineEditKind kind,SplineDegree increment,double knot,bool alongV,PK_BSURF_reparameterise_o_s* options)
    {
        var error=EditableSpline(surface,true,out var slot);if(error!=0)return error;
        var data=BSurfaceDataStore[Surfaces[slot].DataIndex];
        if(data.IsUPeriodic!=0||data.IsVPeriodic!=0||data.UDegree>16||data.VDegree>16)return ParasolidConstants.PK_ERROR_not_implemented;
        if((long)data.NUVertices*data.NVVertices>8192)return ParasolidConstants.PK_ERROR_not_implemented;
        var view=GetBSurfaceView(in data);
        if(view.UKnots[0]!=view.UStart||view.UKnots[^1]!=view.UEnd||view.VKnots[0]!=view.VStart||view.VKnots[^1]!=view.VEnd)return ParasolidConstants.PK_ERROR_not_implemented;
        if(kind==SplineEditKind.Reparameterise)return ReparameteriseBSurface(surface,in data,options);
        var degree=alongV?data.VDegree:data.UDegree;var oldCount=alongV?data.NVVertices:data.NUVertices;
        if(kind==SplineEditKind.RaiseDegree&&(increment<1||increment>16-degree))return ParasolidConstants.PK_ERROR_bad_parameter;
        var target=kind==SplineEditKind.RaiseDegree?degree+increment:degree;
        var knotCount=alongV?data.NVKnots:data.NUKnots;
        var count=kind==SplineEditKind.RaiseDegree?oldCount+increment*(knotCount-1):oldCount+1;
        var capacity=kind==SplineEditKind.RaiseDegree?(knotCount-1)*target+1:count;
        var nu=alongV?data.NUVertices:count;var nv=alongV?count:data.NVVertices;
        if(capacity>FreeformPoleLimit||(long)nu*nv>8192)return ParasolidConstants.PK_ERROR_not_implemented;
        var dimension=data.VertexDim+(data.IsRational!=0?0:1);if(dimension!=4)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> input=stackalloc double[oldCount*dimension],line=stackalloc double[capacity*dimension],knots=stackalloc double[capacity+target+1];
        Span<double> poles=stackalloc double[nu*nv*data.VertexDim];var source=data.PolesSpan();var inputKnots=alongV?view.VKnots:view.UKnots;
        var across=alongV?data.NUVertices:data.NVVertices;
        for(var c=0;c<across;c++)
        {
            for(var i=0;i<oldCount;i++)
            {
                var at=(alongV?c*data.NVVertices+i:i*data.NVVertices+c)*data.VertexDim;
                source.Slice(at,data.VertexDim).CopyTo(input[(i*dimension)..]);if(data.IsRational==0)input[i*dimension+3]=1;
            }
            var lineCount=oldCount;
            if(kind==SplineEditKind.RaiseDegree)
            {if(!SplineEditingMath.Raise(input,inputKnots,dimension,degree,increment,line,knots,out lineCount))return ParasolidConstants.PK_ERROR_cant_make_bspline;}
            else
            {input.CopyTo(line);inputKnots.CopyTo(knots);error=InsertEditingKnot(line,knots,dimension,degree,ref lineCount,knot);if(error!=0)return error;}
            if(lineCount!=count)return ParasolidConstants.PK_ERROR_cant_make_bspline;
            for(var i=0;i<count;i++)
            {
                var at=(alongV?c*nv+i:i*nv+c)*data.VertexDim;var w=line[i*dimension+3];if(!(w>0))return ParasolidConstants.PK_ERROR_weight_le_0;
                for(var d=0;d<data.VertexDim;d++)
                {var value=data.IsRational!=0?line[i*dimension+d]:line[i*dimension+d]/w;if(!double.IsFinite(value))return ParasolidConstants.PK_ERROR_bad_vertex;poles[at+d]=value;}
            }
        }
        Span<double> compact=stackalloc double[count+target+1];Span<int> mults=stackalloc int[compact.Length];var nk=CompactEditingKnots(knots[..(count+target+1)],compact,mults);
        SurfTag edited=0;error=CreateFreeformSurface(alongV?data.UDegree:target,alongV?target:data.VDegree,nu,nv,poles,data.VertexDim,data.IsRational!=0,
            alongV?data.UKnotsSpan():compact[..nk],alongV?data.UKnotMultsSpan():mults[..nk],alongV?compact[..nk]:data.VKnotsSpan(),alongV?mults[..nk]:data.VKnotMultsSpan(),data.IsUClosed!=0,data.IsVClosed!=0,&edited);
        return error!=0?error:ReplaceEditedSpline(surface,edited,true);
    }

    private static int ReparameteriseBSurface(SurfTag surface,in BSurfaceData data,PK_BSURF_reparameterise_o_s* options)
    {
        if(options->have_u_interval>1||options->have_v_interval>1||options->transpose>1||options->reverse_u>1||options->reverse_v>1)return ParasolidConstants.PK_ERROR_bad_parameter;
        var nu=options->transpose!=0?data.NVVertices:data.NUVertices;var nv=options->transpose!=0?data.NUVertices:data.NVVertices;
        Span<double> poles=stackalloc double[nu*nv*data.VertexDim];var source=data.PolesSpan();
        for(var u=0;u<nu;u++)for(var v=0;v<nv;v++)
        {
            var mappedU=options->reverse_u!=0?nu-1-u:u;var mappedV=options->reverse_v!=0?nv-1-v:v;
            var a=options->transpose!=0?mappedV:mappedU;var b=options->transpose!=0?mappedU:mappedV;
            source.Slice((a*data.NVVertices+b)*data.VertexDim,data.VertexDim).CopyTo(poles.Slice((u*nv+v)*data.VertexDim,data.VertexDim));
        }
        var view=GetBSurfaceView(in data);Span<double> originalU=stackalloc double[view.UKnots.Length],originalV=stackalloc double[view.VKnots.Length];view.UKnots.CopyTo(originalU);view.VKnots.CopyTo(originalV);
        var uk=options->transpose!=0?originalV:originalU;var vk=options->transpose!=0?originalU:originalV;
        var ud=options->transpose!=0?data.VDegree:data.UDegree;var vd=options->transpose!=0?data.UDegree:data.VDegree;
        if(options->reverse_u!=0)ReverseEditingKnots(uk,ud,nu);
        if(options->reverse_v!=0)ReverseEditingKnots(vk,vd,nv);
        if(options->have_u_interval!=0&&!RemapEditingKnots(uk,ud,nu,options->u_interval)||options->have_v_interval!=0&&!RemapEditingKnots(vk,vd,nv,options->v_interval))return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<double> uKnots=stackalloc double[uk.Length],vKnots=stackalloc double[vk.Length];Span<int> um=stackalloc int[uk.Length],vm=stackalloc int[vk.Length];
        var nku=CompactEditingKnots(uk,uKnots,um);var nkv=CompactEditingKnots(vk,vKnots,vm);SurfTag edited=0;
        var error=CreateFreeformSurface(ud,vd,nu,nv,poles,data.VertexDim,data.IsRational!=0,uKnots[..nku],um[..nku],vKnots[..nkv],vm[..nkv],
            options->transpose!=0?data.IsVClosed!=0:data.IsUClosed!=0,options->transpose!=0?data.IsUClosed!=0:data.IsVClosed!=0,&edited);
        return error!=0?error:ReplaceEditedSpline(surface,edited,true);
    }

    private static void ReverseEditingKnots(Span<double> knots,SplineDegree degree,BufferCount count)
    {
        var first=knots[degree];var last=knots[count];knots.Reverse();
        for(var i=0;i<knots.Length;i++){var t=knots[i];knots[i]=t==first?last:t==last?first:first+(last-t);}
    }
}
