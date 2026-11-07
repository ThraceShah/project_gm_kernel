namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // This certificate proves the identity parameter map, including the
    // evaluator's extension, by equality of the complete defining data.
    // It makes no claim about G1 transitions or arbitrary reparameterization.
    private readonly struct IdentitySupportCertificate(SurfTag first,SurfTag second,EntityGeneration firstGeneration,
        EntityGeneration secondGeneration,KernelSense normalRelation)
    {
        internal readonly SurfTag First=first,Second=second;
        internal readonly EntityGeneration FirstGeneration=firstGeneration,SecondGeneration=secondGeneration;
        internal readonly KernelSense NormalRelation=normalRelation;

        internal bool Current
        {
            get
            {
                var a=GetSurfaceSlotByTag(First);var b=GetSurfaceSlotByTag(Second);
                return a>=0&&b>=0&&Surfaces[a].Header.Generation==FirstGeneration&&Surfaces[b].Header.Generation==SecondGeneration;
            }
        }
    }

    private static bool TryCertifyIdentitySupport(SurfTag first,SurfTag second,out IdentitySupportCertificate certificate)
    {
        certificate=default;if(!SameSheetSupport(first,second))return false;
        var a=Surfaces[GetSurfaceSlotByTag(first)];var b=Surfaces[GetSurfaceSlotByTag(second)];
        certificate=new(first,second,a.Header.Generation,b.Header.Generation,LocalSenseSign(a.Sense)*LocalSenseSign(b.Sense));
        return certificate.Current;
    }
}
