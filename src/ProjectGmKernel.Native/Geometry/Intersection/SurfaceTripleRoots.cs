using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>Complete regular roots of A(u,v)=B(s,t)=C(p,q). Charts select
/// geometry; interval exclusion and Krawczyk inclusion certify every root.</summary>
internal struct SurfaceTripleRootCertificate
{
    internal SurfaceParameterBox First,Second,Third,FirstDomain,SecondDomain,ThirdDomain;
    internal readonly bool MayShareRoot(in SurfaceTripleRootCertificate other)
        =>IsolatedRootCertificate.Overlaps(First,other.First)&&IsolatedRootCertificate.Overlaps(Second,other.Second)&&IsolatedRootCertificate.Overlaps(Third,other.Third);
    internal readonly bool ProvesSameRoot(in SurfaceTripleRootCertificate other)
        =>Contains(FirstDomain,SecondDomain,ThirdDomain,other)||Contains(other.FirstDomain,other.SecondDomain,other.ThirdDomain,this);
    private static bool Contains(in SurfaceParameterBox a,in SurfaceParameterBox b,in SurfaceParameterBox c,in SurfaceTripleRootCertificate root)
        =>IsolatedRootCertificate.Contains(a,root.First)&&IsolatedRootCertificate.Contains(b,root.Second)&&IsolatedRootCertificate.Contains(c,root.Third);
}

internal static class SurfaceTripleRoots
{
    [InlineArray(6)] private struct Parameters{private double value;}
    private struct Cell{internal Parameters Lower,Upper;internal BufferCount Depth;}

    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch aSource,in SurfaceIntersectionPatch bSource,in SurfaceIntersectionPatch cSource,
        Span<SurfaceIntersectionNode> roots,out BufferCount count,Span<SurfaceTripleRootCertificate> certificates=default)
    {
        Span<SurfaceTripleRootCertificate> accepted=certificates.IsEmpty?stackalloc SurfaceTripleRootCertificate[roots.Length]:certificates;
        count=0;Span<SurfaceBoundCacheEntry> ac=stackalloc SurfaceBoundCacheEntry[128],bc=stackalloc SurfaceBoundCacheEntry[128],cc=stackalloc SurfaceBoundCacheEntry[128];ac.Clear();bc.Clear();cc.Clear();
        var a=new SurfaceIntersectionPatch(aSource,ac);var b=new SurfaceIntersectionPatch(bSource,bc);var c=new SurfaceIntersectionPatch(cSource,cc);
        var domain=new Cell();
        domain.Lower[0]=a.Domain.U0;domain.Lower[1]=a.Domain.V0;domain.Lower[2]=b.Domain.U0;domain.Lower[3]=b.Domain.V0;domain.Lower[4]=c.Domain.U0;domain.Lower[5]=c.Domain.V0;
        domain.Upper[0]=a.Domain.U1;domain.Upper[1]=a.Domain.V1;domain.Upper[2]=b.Domain.U1;domain.Upper[3]=b.Domain.V1;domain.Upper[4]=c.Domain.U1;domain.Upper[5]=c.Domain.V1;
        Span<Cell> pending=stackalloc Cell[256];pending[0]=domain;var n=1;var visited=0;
        Span<IntervalNumber> f=stackalloc IntervalNumber[6],jac=stackalloc IntervalNumber[36],cf=stackalloc IntervalNumber[6],cj=stackalloc IntervalNumber[36],position=stackalloc IntervalNumber[9];
        Span<double> inverse=stackalloc double[36];
        while(n>0)
        {
            if(++visited>100000)return AlgorithmStatus.NotConverged;
            var cell=pending[--n];var status=Bounds(a,b,c,cell,f,jac);if(status==AlgorithmStatus.NotConverged)goto Split;if(status!=AlgorithmStatus.Success)return status;
            var empty=false;for(var r=0;r<6;r++)empty|=!f[r].ContainsZero;if(empty)continue;
            var point=cell;for(var d=0;d<6;d++)point.Lower[d]=point.Upper[d]=cell.Lower[d]+(cell.Upper[d]-cell.Lower[d])*.5;
            if(Invert(jac,inverse)&&Contraction(jac,inverse,cell)<.8)
            {
                for(var iteration=0;iteration<14;iteration++)
                {
                    status=Bounds(a,b,c,point,cf,cj);if(status==AlgorithmStatus.NotConverged)goto Split;if(status!=AlgorithmStatus.Success)return status;
                    if(!Invert(cj,inverse))break;var change=0.0;
                    for(var r=0;r<6;r++)
                    {
                        var step=0.0;for(var k=0;k<6;k++)step+=inverse[6*r+k]*cf[k].Midpoint;
                        var next=Math.Clamp(point.Lower[r]-step,cell.Lower[r],cell.Upper[r]);change=Math.Max(change,Math.Abs(next-point.Lower[r]));point.Lower[r]=point.Upper[r]=next;
                    }
                    if(change<1e-14)break;
                }
                status=Bounds(a,b,c,point,cf,cj);if(status==AlgorithmStatus.NotConverged)goto Split;if(status!=AlgorithmStatus.Success)return status;
                var extended=cell;
                for(var d=0;d<6;d++)
                {
                    var margin=Math.Max((cell.Upper[d]-cell.Lower[d])*.05,1e-11*Math.Max(1,domain.Upper[d]-domain.Lower[d]));
                    extended.Lower[d]-=margin;extended.Upper[d]+=margin;
                }
                status=Bounds(a,b,c,extended,f,jac);if(status==AlgorithmStatus.NotConverged)goto Split;if(status!=AlgorithmStatus.Success)return status;
                var unique=Invert(cj,inverse);empty=false;var enclosure=point;
                for(var r=0;r<6&&unique;r++)
                {
                    IntervalNumber image=point.Lower[r];for(var k=0;k<6;k++)image-=inverse[6*r+k]*cf[k];
                    for(var d=0;d<6;d++)
                    {
                        IntervalNumber m=r==d?1:0;for(var k=0;k<6;k++)m-=inverse[6*r+k]*jac[6*k+d];
                        image+=m*(new IntervalNumber(extended.Lower[d],extended.Upper[d])-point.Lower[d]);
                    }
                    enclosure.Lower[r]=image.Lower;enclosure.Upper[r]=image.Upper;
                    empty|=image.Upper<cell.Lower[r]||image.Lower>cell.Upper[r];
                    unique&=image.Lower>extended.Lower[r]&&image.Upper<extended.Upper[r];
                }
                if(empty)continue;var residual=0.0;for(var r=0;r<6;r++)residual=Math.Max(residual,cf[r].Magnitude);
                if(unique&&residual<1e-9)
                {
                    if(Contraction(jac,inverse,extended)>=1)goto Split;
                    for(var d=0;d<6;d++)if(enclosure.Lower[d]<domain.Lower[d]||enclosure.Upper[d]>domain.Upper[d])goto Split;
                    status=Refine(a,b,c,point,inverse,cf,ref enclosure,f,jac);if(status!=AlgorithmStatus.Success)return status;
                    var certificate=new SurfaceTripleRootCertificate{First=Box(enclosure,0),Second=Box(enclosure,2),Third=Box(enclosure,4),
                        FirstDomain=Box(extended,0),SecondDomain=Box(extended,2),ThirdDomain=Box(extended,4)};
                    status=a.Bound(new(point.Lower[0],point.Lower[0],point.Lower[1],point.Lower[1]),position);if(status!=AlgorithmStatus.Success)return status;
                    var root=new SurfaceIntersectionNode{Point=new(){X=position[0].Midpoint,Y=position[1].Midpoint,Z=position[2].Midpoint},U=point.Lower[0],V=point.Lower[1],S=point.Lower[2],T=point.Lower[3]};
                    var duplicate=false;for(var i=0;i<count;i++)
                    {
                        if(!certificate.MayShareRoot(accepted[i]))continue;
                        if(!certificate.ProvesSameRoot(accepted[i]))goto Split;duplicate=true;
                    }
                    if(!duplicate){if(count==roots.Length||count==accepted.Length)return AlgorithmStatus.OutputTooSmall;accepted[count]=certificate;roots[count++]=root;}continue;
                }
            }
            Split:
            if(cell.Depth>=64||n+2>pending.Length)return AlgorithmStatus.NotConverged;
            var axis=0;var widest=0.0;
            for(var d=0;d<6;d++){var width=(cell.Upper[d]-cell.Lower[d])/(domain.Upper[d]-domain.Lower[d]);if(width>widest){axis=d;widest=width;}}
            var middle=cell.Lower[axis]+(cell.Upper[axis]-cell.Lower[axis])*.4931471805599453;
            if(!(middle>cell.Lower[axis]&&middle<cell.Upper[axis]))return AlgorithmStatus.NumericalFailure;
            var right=cell;cell.Upper[axis]=middle;right.Lower[axis]=middle;cell.Depth++;right.Depth=cell.Depth;pending[n++]=right;pending[n++]=cell;
        }
        return AlgorithmStatus.Success;
    }

    private static SurfaceParameterBox Box(in Cell cell,BufferOffset axis)
        =>new(cell.Lower[axis],cell.Upper[axis],cell.Lower[axis+1],cell.Upper[axis+1]);

    private static AlgorithmStatus Refine(in SurfaceIntersectionPatch a,in SurfaceIntersectionPatch b,in SurfaceIntersectionPatch c,
        in Cell point,ReadOnlySpan<double> inverse,ReadOnlySpan<IntervalNumber> residual,ref Cell enclosure,Span<IntervalNumber> f,Span<IntervalNumber> jac)
    {
        for(var iteration=0;iteration<12;iteration++)
        {
            var hull=enclosure;
            for(var d=0;d<6;d++){hull.Lower[d]=Math.Min(hull.Lower[d],point.Lower[d]);hull.Upper[d]=Math.Max(hull.Upper[d],point.Lower[d]);}
            var status=Bounds(a,b,c,hull,f,jac);if(status!=AlgorithmStatus.Success)return status;
            var next=enclosure;var shrank=false;
            for(var r=0;r<6;r++)
            {
                IntervalNumber image=point.Lower[r];for(var k=0;k<6;k++)image-=inverse[6*r+k]*residual[k];
                for(var d=0;d<6;d++)
                {
                    IntervalNumber m=r==d?1:0;for(var k=0;k<6;k++)m-=inverse[6*r+k]*jac[6*k+d];
                    image+=m*(new IntervalNumber(enclosure.Lower[d],enclosure.Upper[d])-point.Lower[d]);
                }
                var lo=Math.Max(enclosure.Lower[r],image.Lower);var hi=Math.Min(enclosure.Upper[r],image.Upper);
                if(!double.IsFinite(lo)||!double.IsFinite(hi)||hi<lo)return AlgorithmStatus.NotConverged;
                next.Lower[r]=lo;next.Upper[r]=hi;shrank|=hi-lo<.75*(enclosure.Upper[r]-enclosure.Lower[r]);
            }
            enclosure=next;if(!shrank)break;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Bounds(in SurfaceIntersectionPatch a,in SurfaceIntersectionPatch b,in SurfaceIntersectionPatch c,in Cell cell,Span<IntervalNumber> f,Span<IntervalNumber> jac)
    {
        Span<IntervalNumber> av=stackalloc IntervalNumber[9],bv=stackalloc IntervalNumber[9],cv=stackalloc IntervalNumber[9];
        var status=a.Bound(new(cell.Lower[0],cell.Upper[0],cell.Lower[1],cell.Upper[1]),av);if(status!=AlgorithmStatus.Success)return status;
        status=b.Bound(new(cell.Lower[2],cell.Upper[2],cell.Lower[3],cell.Upper[3]),bv);if(status!=AlgorithmStatus.Success)return status;
        status=c.Bound(new(cell.Lower[4],cell.Upper[4],cell.Lower[5],cell.Upper[5]),cv);if(status!=AlgorithmStatus.Success)return status;
        jac.Clear();
        for(var r=0;r<3;r++)
        {
            f[r]=av[r]-bv[r];f[r+3]=av[r]-cv[r];
            jac[6*r]=jac[6*(r+3)]=av[3+r];jac[6*r+1]=jac[6*(r+3)+1]=av[6+r];
            jac[6*r+2]=-bv[3+r];jac[6*r+3]=-bv[6+r];jac[6*(r+3)+4]=-cv[3+r];jac[6*(r+3)+5]=-cv[6+r];
        }
        return AlgorithmStatus.Success;
    }
    private static bool Invert(ReadOnlySpan<IntervalNumber> matrix,Span<double> inverse)
    {
        Span<double> lu=stackalloc double[36],rhs=stackalloc double[6];Span<BufferOffset> pivots=stackalloc BufferOffset[6];
        for(var i=0;i<36;i++)lu[i]=matrix[i].Midpoint;
        if(SmallLinearSolve.LuFactorize(lu,6,pivots)!=AlgorithmStatus.Success)return false;
        for(var c=0;c<6;c++){rhs.Clear();rhs[c]=1;if(SmallLinearSolve.LuSolveInPlace(lu,6,pivots,rhs)!=AlgorithmStatus.Success)return false;for(var r=0;r<6;r++)inverse[6*r+c]=rhs[r];}
        return true;
    }
    private static double Contraction(ReadOnlySpan<IntervalNumber> matrix,ReadOnlySpan<double> inverse,in Cell cell)
    {
        var norm=0.0;
        for(var r=0;r<6;r++)
        {
            var row=0.0;for(var c=0;c<6;c++){IntervalNumber m=r==c?1:0;for(var k=0;k<6;k++)m-=inverse[6*r+k]*matrix[6*k+c];row=Math.BitIncrement(row+(new IntervalNumber(m.Magnitude)*(new IntervalNumber(cell.Upper[c])-cell.Lower[c])/Math.Max(cell.Upper[r]-cell.Lower[r],1e-30)).Upper);}
            norm=Math.Max(norm,row);
        }
        return norm;
    }
}
