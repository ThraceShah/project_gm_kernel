global using BufferOffset=System.Int32;
using System.Reflection;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace KernelTests;

// Bind the actual private value types/methods: no copied layout, predicate,
// invalidation algorithm, production test hook or diagnostic state.
[Collection("KernelTests")]
public class NestedBlendCacheEmptyReadSetTests
{
    private const BindingFlags PrivateStatic=BindingFlags.NonPublic|BindingFlags.Static;
    private delegate bool UvPredicate<TNode>(ReadOnlySpan<TNode> nodes);
    private delegate AlgorithmStatus Jacobian<TNode,TContact>(in NestedBlendProgram program,ReadOnlySpan<TNode> nodes,
        ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,
        Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> charts,Span<TContact> cache);

    [Fact]
    public void GuardScansBothSidesOfEveryOccurrenceAndTreatsAnyNonnegativeIndexAsLive()
        =>Run(nameof(CheckPredicate));

    [Fact]
    public void ActualJacobianSkipsOnlyEmptyReadSetsAndInvalidatesTheWholeCacheBeforeFailedFill()
        =>Run(nameof(CheckInvalidation));

    [Fact]
    public void SharedUvFailedFillRetryAndDifferentProgramCannotReuseStaleReadyValues()
        =>Run(nameof(CheckHistory));

    [Fact]
    public void SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled()
    {
        using var fixture=new BlendIntersectionTests();
        try
        {
            JointNestedBlendEvaluation.ReuseSplineContactBounds=false;
            fixture.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative();
        }
        finally{JointNestedBlendEvaluation.ReuseSplineContactBounds=true;}
    }

    private static void Run(string method)
    {
        var owner=typeof(JointNestedBlendEvaluation);
        var test=typeof(NestedBlendCacheEmptyReadSetTests).GetMethod(method,PrivateStatic)!;
        test.MakeGenericMethod(owner.GetNestedType("Occurrence",BindingFlags.NonPublic)!,
            owner.GetNestedType("IntervalSplineContact",BindingFlags.NonPublic)!).Invoke(null,null);
    }

    private static void CheckPredicate<TNode,TContact>() where TNode:unmanaged where TContact:unmanaged
    {
        var predicate=typeof(JointNestedBlendEvaluation).GetMethod("HasAnyUv",PrivateStatic)!.CreateDelegate<UvPredicate<TNode>>();
        var nodes=Nodes<TNode>(16);Assert.False(predicate(nodes));Assert.False(predicate([]));
        foreach(var side in new[]{"FirstUv","SecondUv"})for(var i=0;i<nodes.Length;i++)
        foreach(var uv in new[]{0,3,61,int.MaxValue})
        {
            Set(ref nodes[i],side,uv);Assert.True(predicate(nodes));
            Set(ref nodes[i],side,-7);Assert.False(predicate(nodes));
        }
    }

    private static void CheckInvalidation<TNode,TContact>() where TNode:unmanaged where TContact:unmanaged
    {
        var jac=Bind<TNode,TContact>();var cache=new TContact[63];var nodes=Nodes<TNode>(1);
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        IntervalNumber[] boxes=[0,.37,0];var matrix=new IntervalNumber[9];
        MemoryMarshal.AsBytes(cache.AsSpan()).Fill(0xA5);var poison=MemoryMarshal.AsBytes(cache.AsSpan()).ToArray();
        Assert.Equal(AlgorithmStatus.Success,jac(program,nodes,boxes,.37,matrix,new IntervalDual[3],new IntervalDual[3],[0],cache));
        Assert.Equal(poison,MemoryMarshal.AsBytes(cache.AsSpan()).ToArray());
        // Manufactured plane/plane gauge has rows dX, dZ, dY, independently
        // of the cache and of a second invocation of the Jacobian routine.
        double[] expected=[1,0,0,0,0,1,0,1,0];
        for(var i=0;i<9;i++)Assert.InRange(expected[i],matrix[i].Lower,matrix[i].Upper);

        var spline=SplineProgram(0,periodic:true);nodes=Nodes<TNode>(16);Set(ref nodes[15],"SecondUv",61);
        boxes=new IntervalNumber[63];boxes[61]=new(1e15);
        Assert.Equal(AlgorithmStatus.NotConverged,jac(spline,nodes,boxes,.37,new IntervalNumber[63*63],new IntervalDual[63],new IntervalDual[63],new BufferOffset[16],cache));
        Assert.All(MemoryMarshal.AsBytes(cache.AsSpan()).ToArray(),value=>Assert.Equal((byte)0,value));
    }

    private static void CheckHistory<TNode,TContact>() where TNode:unmanaged where TContact:unmanaged
    {
        var jac=Bind<TNode,TContact>();var cache=new TContact[63];var nodes=Nodes<TNode>(1);
        Set(ref nodes[0],"FirstUv",3);Set(ref nodes[0],"SecondUv",3);
        IntervalNumber[] boxes=[0,.37,0,.2,.1];var matrix=new IntervalNumber[25];
        var variables=new IntervalDual[5];var equations=new IntervalDual[5];Span<IntervalNumber> hull=stackalloc IntervalNumber[18];
        foreach(var translation in new[]{0.0,7.0,-3.0})
        {
            var program=SplineProgram(translation);
            Assert.Equal(AlgorithmStatus.Success,jac(program,nodes,boxes,.37,matrix,variables,equations,[0],cache));
            Assert.True((bool)Field<TContact>("Ready").GetValue(cache[3])!);
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.IntervalSplineBounds(program,program.Surfaces[0],boxes[3],boxes[4],hull));
            var values=Field<TContact>("Values").GetValue(cache[3])!;
            var first=(IntervalNumber)values.GetType().GetField("value",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(values)!;
            Assert.Equal(BitConverter.DoubleToInt64Bits(hull[0].Lower),BitConverter.DoubleToInt64Bits(first.Lower));
            Assert.Equal(BitConverter.DoubleToInt64Bits(hull[0].Upper),BitConverter.DoubleToInt64Bits(first.Upper));
            // The same boxes/key with a different scalar payload must use
            // the new program, then a failed fill must clear old Ready.
            var failing=SplineProgram(translation,periodic:true);boxes[3]=new(1e15);
            Assert.Equal(AlgorithmStatus.NotConverged,jac(failing,nodes,boxes,.37,matrix,variables,equations,[0],cache));
            Assert.All(MemoryMarshal.AsBytes(cache.AsSpan()).ToArray(),value=>Assert.Equal((byte)0,value));
            boxes[3]=new(.2);
            Assert.Equal(AlgorithmStatus.Success,jac(program,nodes,boxes,.37,matrix,variables,equations,[0],cache));
        }
    }

    private static Jacobian<TNode,TContact> Bind<TNode,TContact>()
        =>typeof(JointNestedBlendEvaluation).GetMethod("IntervalJacobian",PrivateStatic)!.CreateDelegate<Jacobian<TNode,TContact>>();
    private static FieldInfo Field<T>(string name)=>typeof(T).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!;
    private static void Set<T>(ref T item,string name,BufferOffset value) where T:struct
    {object boxed=item;Field<T>(name).SetValue(boxed,value);item=(T)boxed;}
    private static T[] Nodes<T>(BufferCount count) where T:struct
    {
        var nodes=new T[count];
        for(var i=0;i<count;i++)foreach(var name in new[]{"Parent","FirstChild","SecondChild","FirstUv","SecondUv"})Set(ref nodes[i],name,-1);
        return nodes;
    }
    private static NestedBlendProgram SplineProgram(double translation,bool periodic=false)
    {
        var source=NestedBlendBoundsInitializationContractTests.SplineSurfaces()[0];source.UPeriodic=periodic;
        var scalars=NestedBlendBoundsInitializationContractTests.SplineScalars();
        for(var i=0;i<4;i++)scalars[3*i]=translation;
        return new(new[]{source,source},NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts(),scalars,new double[8192]);
    }
}
