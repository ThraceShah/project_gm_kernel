using ProjectGmKernel.Xt;
using static parasolid;

// Numerical diagnostic for the finite supports of the short-edge oracle.
// Only real PK evaluates curves, fin ranges and surface jets.
internal static unsafe class ShortEdgeNativeTrimIntegral
{
    private const int MomentCount=11;
    private static ReadOnlySpan<double> Nodes => [-.9602898564975363,-.7966664774136267,-.5255324099163290,-.1834346424956498,.1834346424956498,.5255324099163290,.7966664774136267,.9602898564975363];
    private static ReadOnlySpan<double> Weights => [.1012285362903763,.2223810344533745,.3137066458778873,.3626837833783620,.3626837833783620,.3137066458778873,.2223810344533745,.1012285362903763];

    internal static Dictionary<long,XtNode> ReadCharts(XtSchemaCatalog schema,ReadOnlySpan<byte> bytes)
    {
        var doc=XtCodec.Read(schema,bytes);
        var nodes=doc.Nodes.ToDictionary(n=>n.Index);
        return doc.Nodes.Where(n=>doc.Schema.GetNode(n.Type).Name=="INTERSECTION")
            .ToDictionary(n=>n.Fields[0].Integer,n=>nodes[n.Fields[9].Pointer]);
    }

    internal static PK_VECTOR_t ReferenceOrigin(PK_BODY_t reference)
    {
        PK_BOX_t box;Check(PK_TOPOL_find_box(reference,&box),"integration origin box");
        return new(Midpoint(box.coord[0],box.coord[3]),Midpoint(box.coord[1],box.coord[4]),Midpoint(box.coord[2],box.coord[5]));

        static double Midpoint(double a,double b)
            =>double.IsFinite(a)&&double.IsFinite(b)&&b>=a?a*.5+b*.5:throw new InvalidOperationException("invalid integration origin box");
    }

    internal static double[] Measure(PK_BODY_t body,Dictionary<long,XtNode> charts,PK_VECTOR_t origin,int slices)
    {
        var sum=new double[MomentCount];
        var pivot=Vec(origin);
        int n;PK_FACE_t* faces;Check(PK_BODY_ask_faces(body,&n,&faces),"faces");
        try
        {
            for(var f=0;f<n;f++)
            {
                PK_SURF_t surface;PK_CLASS_t kind;
                Check(PK_FACE_ask_surf(faces[f],&surface),"face surface");
                Check(PK_ENTITY_ask_class(surface,&kind),"surface class");
                if(kind is not (PK_CLASS_plane or PK_CLASS_cyl or PK_CLASS_blendsf))
                    throw new InvalidOperationException($"finite integral does not cover surface class {kind}");
                var periodicBlend=false;
                if(kind==PK_CLASS_blendsf)
                {
                    PK_BLENDSF_sf_t definition;PK_CLASS_t spineKind;
                    Check(PK_BLENDSF_ask(surface,&definition),"blend definition");
                    Check(PK_ENTITY_ask_class(definition.spine,&spineKind),"spine class");
                    periodicBlend=spineKind is PK_CLASS_circle or PK_CLASS_ellipse;
                }
                var area=sum[10];
                int count;PK_LOOP_t* loops;Check(PK_FACE_ask_loops(faces[f],&count,&loops),"face loops");
                try
                {
                    if(count==0)throw new InvalidOperationException("finite integral requires oriented face loops");
                    for(var l=0;l<count;l++)
                    {
                        int nf;PK_FIN_t* fins;Check(PK_LOOP_ask_fins(loops[l],&nf,&fins),"loop fins");
                        try
                        {
                            if(nf==0)throw new InvalidOperationException("finite integral requires a directed fin for every boundary use");
                            for(var i=0;i<nf;i++)Fin(surface,fins[i],periodicBlend,charts,pivot,slices,sum);
                        }
                        finally{Free(fins);}
                    }
                }
                finally{Free(loops);}
                sum[10]=area+Math.Abs(sum[10]-area);
            }
        }
        finally{Free(faces);}
        var volume=sum[0];
        if(!(volume>0)||!double.IsFinite(volume))throw new InvalidOperationException($"invalid signed volume {volume:R}");
        var x=sum[1]/volume;var y=sum[2]/volume;var z=sum[3]/volume;
        return [volume,x+pivot.X,y+pivot.Y,z+pivot.Z,sum[5]+sum[6]-volume*(y*y+z*z),sum[4]+sum[6]-volume*(x*x+z*z),sum[4]+sum[5]-volume*(x*x+y*y),
            volume*x*y-sum[7],volume*x*z-sum[8],volume*y*z-sum[9],sum[10]];
    }

    private static void Fin(PK_SURF_t surface,PK_FIN_t fin,bool periodicBlend,Dictionary<long,XtNode> charts,V3 pivot,int slices,Span<double> sum)
    {
        PK_CURVE_t curve;PK_CLASS_t kind;PK_INTERVAL_t limits;PK_LOGICAL_t sense;
        PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
        Check(PK_FIN_ask_geometry(fin,PK_LOGICAL_true,&curve,&kind,ends,&limits,&sense),"finite fin geometry");
        if(curve==0)throw new InvalidOperationException($"missing finite geometry at fin {fin}");
        var first=limits.value[0];var last=limits.value[1];
        if(!(last>first)||!double.IsFinite(last-first))throw new InvalidOperationException($"unbounded fin {fin}");
        XtNode? chart=null;
        if(kind==PK_CLASS_icurve)
        {
            int ident;Check(PK_ENTITY_ask_identifier(curve,&ident),"finite curve identifier");
            if(!charts.TryGetValue(ident,out chart))throw new InvalidOperationException($"native CHART missing for curve {ident}");
        }
        var capacity=2+(chart?.VariableLength??0);
        Span<double> cells=capacity<=128?stackalloc double[capacity]:new double[capacity];
        cells[0]=first;cells[1]=last;var count=2;
        if(chart!=null)for(var i=0;i<chart.VariableLength;i++)
        {
            var p=chart.Fields[7+i].Vector;double t;
            Check(PK_CURVE_parameterise_vector(curve,new(p.X,p.Y,p.Z),&t),"native CHART station");
            if(first<t&&t<last)cells[count++]=t;
        }
        cells[..count].Sort();
        var middle=0.0;
        if(periodicBlend)
        {
            PK_UVBOX_t box;Check(PK_FIN_find_uvbox(fin,&box),"periodic Blend fin box");
            var width=box.param[2]-box.param[0];
            if(!(width>=0&&width<Math.Tau))throw new InvalidOperationException($"periodic Blend fin box does not isolate a lift at fin {fin}");
            middle=box.param[0]+width*.5;
        }
        var direction=sense==PK_LOGICAL_true?1:-1;
        PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* c=stackalloc PK_VECTOR_t[2];
        var nodes=Nodes;var weights=Weights;
        for(var cell=0;cell+1<count;cell++)
        {
            var a=cells[cell];var half=(cells[cell+1]-a)/(2*slices);if(half==0)continue;
            for(var k=0;k<slices;k++)for(var i=0;i<nodes.Length;i++)
            {
                var t=a+half*(2*k+1+nodes[i]);PK_UV_t uv;
                Check(PK_CURVE_eval(curve,t,1,c),"native finite curve jet");
                Check(PK_SURF_parameterise_vector(surface,c[0],&uv),"native fin UV");
                if(periodicBlend)uv.param[0]+=Math.Tau*Math.Round((middle-uv.param[0])/Math.Tau);
                Check(PK_SURF_eval(surface,uv,1,1,PK_LOGICAL_false,jet),"native surface jet");
                var v=Vec(jet[2]);var n=Cross(Vec(jet[1]),v);var norm=Dot(n,n);
                if(!(norm>0))throw new InvalidOperationException($"singular finite jet at fin {fin}, t={t:R}");
                var du=Dot(Cross(Vec(c[1]),v),n)/norm;
                var lastV=uv.param[1];var factor=-direction*half*weights[i]*du*lastV*.5;
                if(factor==0)continue;
                // Divergence gives oriented surface flux for each volume moment;
                // Green reduces the surface integral to -∮du ∫[0,v] flux dv.
                for(var j=0;j<nodes.Length;j++)
                {
                    uv.param[1]=lastV*.5*(1+nodes[j]);
                    Check(PK_SURF_eval(surface,uv,1,1,PK_LOGICAL_false,jet),"native Green primitive jet");
                    // Both bodies use the same nearby origin. Accumulating central
                    // moments this way avoids subtracting large world moments.
                    var p=new V3(jet[0].coord[0]-pivot.X,jet[0].coord[1]-pivot.Y,jet[0].coord[2]-pivot.Z);
                    n=Cross(Vec(jet[1]),Vec(jet[2]));var w=factor*weights[j];var flux=Dot(p,n)*w;
                    sum[0]+=flux/3;sum[1]+=flux*p.X/4;sum[2]+=flux*p.Y/4;sum[3]+=flux*p.Z/4;
                    sum[4]+=flux*p.X*p.X/5;sum[5]+=flux*p.Y*p.Y/5;sum[6]+=flux*p.Z*p.Z/5;
                    sum[7]+=flux*p.X*p.Y/5;sum[8]+=flux*p.X*p.Z/5;sum[9]+=flux*p.Y*p.Z/5;sum[10]+=w*Math.Sqrt(Dot(n,n));
                }
            }
        }
    }

    internal static double Residual(ReadOnlySpan<double> reference,ReadOnlySpan<double> actual)
    {
        var worst=0d;
        for(var i=0;i<reference.Length;i++)worst=Math.Max(worst,Math.Abs(reference[i]-actual[i])/(1+Math.Abs(reference[i])));
        return worst;
    }
    private static V3 Vec(PK_VECTOR_t p)=>new(p.coord[0],p.coord[1],p.coord[2]);
    private static double Dot(V3 a,V3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    private static V3 Cross(V3 a,V3 b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    private static void Free(void* p){if(p!=null)Check(PK_MEMORY_free(p),"free native array");}
    private static void Check(PK_ERROR_code_t error,string label)=>ParasolidScriptHost.Check(error,label);
    private readonly record struct V3(double X,double Y,double Z);
}
