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

    // These value records have alignment gaps. Byte equality can compare
    // unrelated stack data when identical supports are compiled separately.
    private static bool SameBlendSupportProgram(in NestedBlendProgram a,in NestedBlendProgram b)
    {
        if(a.Surfaces.Length!=b.Surfaces.Length||a.Spines.Length!=b.Spines.Length||a.Charts.Length!=b.Charts.Length
            ||!a.Scalars.SequenceEqual(b.Scalars))return false;
        for(var i=0;i<a.Surfaces.Length;i++)if(!SameBlendSupportInstruction(a.Surfaces[i],b.Surfaces[i]))return false;
        for(var i=0;i<a.Spines.Length;i++)
        {
            ref readonly var x=ref a.Spines[i];ref readonly var y=ref b.Spines[i];
            if(x.First!=y.First||x.Second!=y.Second||x.ChartOffset!=y.ChartOffset||x.ChartCount!=y.ChartCount
                ||x.StartType!=y.StartType||x.EndType!=y.EndType||x.Sense!=y.Sense||x.ChordalError!=y.ChordalError||x.AngularError!=y.AngularError
                ||x.Curve.Kind!=y.Curve.Kind||!ExactVector(x.Curve.Origin,y.Curve.Origin)||!ExactVector(x.Curve.First,y.Curve.First)
                ||!ExactVector(x.Curve.Second,y.Curve.Second))return false;
        }
        for(var i=0;i<a.Charts.Length;i++)
        {
            ref readonly var x=ref a.Charts[i];ref readonly var y=ref b.Charts[i];
            if(!ExactVector(x.Position,y.Position)||!ExactVector(x.Chord,y.Chord)||x.Parameter!=y.Parameter||x.Scale!=y.Scale
                ||!x.FirstU.Equals(y.FirstU)||!x.FirstV.Equals(y.FirstV)||!x.SecondU.Equals(y.SecondU)||!x.SecondV.Equals(y.SecondV))return false;
        }
        return true;
    }

    private static bool SameBlendSupportInstruction(in BlendSurfaceInstruction a,in BlendSurfaceInstruction b)
    {
        if(a.IsBlend!=b.IsBlend||a.IsBSpline!=b.IsBSpline||a.NormalSense!=b.NormalSense)return false;
        if(a.IsBlend)return a.First==b.First&&a.Second==b.Second&&a.Spine==b.Spine&&a.Range0==b.Range0&&a.Range1==b.Range1&&a.ReverseSection==b.ReverseSection;
        if(a.IsBSpline)return a.SplineBasis==b.SplineBasis&&a.ScalarOffset==b.ScalarOffset&&a.UDegree==b.UDegree&&a.VDegree==b.VDegree
            &&a.UCount==b.UCount&&a.VCount==b.VCount&&a.Dimension==b.Dimension&&a.Rational==b.Rational&&a.UPeriodic==b.UPeriodic
            &&a.VPeriodic==b.VPeriodic&&a.Offset==b.Offset;
        var x=a.Analytic;var y=b.Analytic;
        return x.Kind==y.Kind&&ExactVector(x.Origin,y.Origin)&&ExactVector(x.Axis,y.Axis)&&ExactVector(x.X,y.X)&&ExactVector(x.Y,y.Y)
            &&x.Radius==y.Radius&&x.Secondary==y.Secondary;
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestSameBlendSupportProgram(in NestedBlendProgram first,in NestedBlendProgram second)
        =>SameBlendSupportProgram(first,second);
#endif

    private static sbyte SignedPlaneAxis(in AnalyticSurface basis,in KernelVector3 direction)
    {
        var u=ExactDirection(basis.X,direction);return u!=0?(sbyte)u:(sbyte)(2*ExactDirection(basis.Y,direction));
    }

    private static KernelSense ExactDirection(in KernelVector3 a,in KernelVector3 b)
        =>ExactVector(a,b)?1:a.X== -b.X&&a.Y== -b.Y&&a.Z== -b.Z?-1:0;
}
