using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Identity of the defining data or an exact signed permutation of
    // planar basis vectors. The map does not identify the two face trims.
    private readonly struct SupportChartCertificate(SurfTag first,SurfTag second,EntityGeneration firstGeneration,
        EntityGeneration secondGeneration,KernelSense normalRelation,sbyte uAxis,sbyte vAxis)
    {
        internal readonly SurfTag First=first,Second=second;
        internal readonly EntityGeneration FirstGeneration=firstGeneration,SecondGeneration=secondGeneration;
        internal readonly KernelSense NormalRelation=normalRelation;
        internal bool Identity=>uAxis==1&&vAxis==2;

        internal PK_UV_s Map(in PK_UV_s source)
        {
            var uv=new PK_UV_s();var u=source.param[0];var v=source.param[1];
            uv.param[0]=uAxis switch{1=>u,-1=>-u,2=>v,_=>-v};
            uv.param[1]=vAxis switch{1=>u,-1=>-u,2=>v,_=>-v};return uv;
        }

        internal bool Current
        {
            get
            {
                var a=GetSurfaceSlotByTag(First);var b=GetSurfaceSlotByTag(Second);
                return a>=0&&b>=0&&Surfaces[a].Header.Generation==FirstGeneration&&Surfaces[b].Header.Generation==SecondGeneration;
            }
        }
    }

    private static bool TryCertifySupportChart(SurfTag first,SurfTag second,out SupportChartCertificate certificate)
    {
        certificate=default;var aSlot=GetSurfaceSlotByTag(first);var bSlot=GetSurfaceSlotByTag(second);
        if(aSlot<0||bSlot<0)return false;
        var a=Surfaces[aSlot];var b=Surfaces[bSlot];var normal=LocalSenseSign(a.Sense)*LocalSenseSign(b.Sense);
        sbyte u=1,v=2;
        if(!SameSheetSupport(first,second))
        {
            if(a.Class!=SurfaceClass.Plane||b.Class!=SurfaceClass.Plane||!TryPrepareSurface(a,out var p)||!TryPrepareSurface(b,out var q)
                ||!ExactVector(p.Origin,q.Origin))return false;
            var axis=ExactDirection(p.Axis,q.Axis);if(axis==0)return false;
            u=SignedPlaneAxis(p,q.X);v=SignedPlaneAxis(p,q.Y);
            if(u==0||v==0||Math.Abs(u)==Math.Abs(v))return false;
            normal*=axis;
        }
        certificate=new(first,second,a.Header.Generation,b.Header.Generation,normal,u,v);
        return certificate.Current;
    }

    private static sbyte SignedPlaneAxis(in AnalyticSurface basis,in KernelVector3 direction)
    {
        var u=ExactDirection(basis.X,direction);return u!=0?(sbyte)u:(sbyte)(2*ExactDirection(basis.Y,direction));
    }

    private static KernelSense ExactDirection(in KernelVector3 a,in KernelVector3 b)
        =>ExactVector(a,b)?1:a.X== -b.X&&a.Y== -b.Y&&a.Z== -b.Z?-1:0;
}
