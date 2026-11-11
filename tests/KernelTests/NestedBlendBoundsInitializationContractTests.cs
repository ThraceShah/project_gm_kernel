using System.Globalization;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

// Initialization contract for NestedBlendBounds.BoundSegment/IntervalJacobian:
// every produced interval bound, status and witness must be bit-identical
// regardless of stack garbage, call order, prior early exits or cache
// hit/miss history. The dump is written for cross-build bitwise A/B; the
// controls assert intra-binary invariants. See
// docs/Interval_Bounds_Initialization_OptA.md for the dominance inventory.
[Collection("KernelTests")]
public class NestedBlendBoundsInitializationContractTests
{
    [Fact]
    public void EveryBoundEntryIsBitwiseStableUnderStackPollutionRepeatedCallsAndEarlyExits()
    {
        var clean=Dump();
        Assert.Contains("Success",clean);
        PolluteStack(8);
        Assert.Equal(clean,Dump());
        // Early exits (invalid input / non-converged) must not leave state
        // that changes later normal calls.
        var failing=DumpFailures();
        Assert.Contains("InvalidInput",failing);Assert.DoesNotContain("Success",failing);
        Assert.Equal(clean,Dump());
        // Reversed probe order must give identical per-probe results.
        Assert.Equal(SortedLines(clean),SortedLines(Dump(reverse:true)));
        // Repeated dumps stay bit-identical (no reentry or generation drift).
        Assert.Equal(clean,Dump());
        var path=Environment.GetEnvironmentVariable("NESTED_BLEND_BOUNDS_DUMP");
        if(!string.IsNullOrEmpty(path))File.WriteAllText(path,clean+failing);
    }

    [Fact]
    public void OptionalConsumersAreBitwiseStableWithSplineContactReuseDisabled()
    {
        try
        {
            JointNestedBlendEvaluation.ReuseSplineContactBounds=true;var expected=Dump();
            JointNestedBlendEvaluation.ReuseSplineContactBounds=false;Assert.Equal(expected,Dump());
        }
        finally{JointNestedBlendEvaluation.ReuseSplineContactBounds=true;}
    }

    [Fact]
    public void SplineContactCacheHitAndMissHistoryNeverChangesBounds()
    {
        var surfaces=SplineSurfaces();var charts=NestedBlendSeriesTests.Charts();var scalars=SplineScalars();
        var program=new NestedBlendProgram(surfaces,NestedBlendSeriesTests.Spines(),charts,scalars,new double[8192]);
        var first=DumpBox(program,4,new(.2,.35,.15,.8));
        // A failing probe in between must not disturb the next result.
        var ignored=DumpBox(program,4,new(.2,.2,.15,.8));
        Assert.Equal(first,DumpBox(program,4,new(.2,.35,.15,.8)));
        Assert.Equal(first,DumpBox(program,4,new(.2,.35,.15,.8)));
        Assert.NotEqual(first,ignored);
    }

    private static string Dump(bool reverse=false)
    {
        var builder=new StringBuilder();
        DumpFixture(builder,"analytic-nested",new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts()),4,0,reverse);
        DumpFixture(builder,"spline-supports",new NestedBlendProgram(SplineSurfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts(),SplineScalars(),new double[8192]),4,0,reverse);
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};var length=Math.Sqrt(.4);
        BlendSurfaceInstruction[] curved=[new(){Analytic=new(SurfaceClass.Sphere,default,z,x,1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1},new(){Analytic=new(SurfaceClass.Sphere,default,z,x,.8),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,x),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] curvedSpines=[new(){First=0,Second=1,ChartCount=2}];BlendChartInstruction[] curvedCharts=[new(){Position=x,Chord=new(){X=-.2/length,Y=-.6/length},Scale=1},new(){Position=new(){X=.8,Y=-.6},Parameter=length,Scale=1}];
        DumpFixture(builder,"curved-spine",new NestedBlendProgram(curved,curvedSpines,curvedCharts),4,0,reverse);
        BlendSurfaceInstruction[] deep=[new(){Analytic=new(SurfaceClass.Plane,default,x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=.4},x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,x),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1},new(){IsBlend=true,First=4,Second=1,Spine=1,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] deepSpines=[new(){First=0,Second=1,ChartCount=2},new(){First=4,Second=1,ChartCount=2,StartType=LimitType.Artificial,EndType=LimitType.Artificial}];
        BlendChartInstruction[] deepCharts=[new(){Position=default,Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=1},Parameter=1,Scale=1},new(){Position=new(){X=.2},Chord=new(){Y=1},Scale=1},new(){Position=new(){X=.2,Y=1},Parameter=1,Scale=1}];
        DumpFixture(builder,"deep-chain",new NestedBlendProgram(deep,deepSpines,deepCharts),5,1,reverse);
        var singularSurfaces=NestedBlendSeriesTests.Surfaces();singularSurfaces[1]=singularSurfaces[0];
        DumpFixture(builder,"singular",new NestedBlendProgram(singularSurfaces,NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts()),4,0,reverse);
        return builder.ToString();
    }

    private static void DumpFixture(StringBuilder builder,string name,in NestedBlendProgram program,BlendSurfaceIndex surface,int spine,bool reverse)
    {
        var boxes=new SurfaceParameterBox[]{new(.2,.6,.1,.9),new(.2,.202,.35,.45),new(.37,.37,.23,.23),new(-.1,1.1,.05,.95),new(.49,.51,.49,.51)};
        if(reverse)Array.Reverse(boxes);
        foreach(var box in boxes)
        {
            builder.Append(name).Append("|box|").Append(Box(box)).Append('|').Append(DumpBox(program,surface,box)).Append('\n');
            builder.Append(name).Append("|spine|").Append(Box(box)).Append('|').Append(DumpSpine(program,spine,box)).Append('\n');
        }
        var domain=new SurfaceParameterBox(.2,.6,.1,.9);
        for(var side=0;side<2;side++)
            builder.Append(name).Append("|curvature|").Append(side).Append('|')
                .Append(Line(JointNestedBlendEvaluation.BoundSpineCurvature(program,spine,(domain.U0+domain.U1)*.5,side,out var curvature),curvature.Lower,curvature.Upper)).Append('\n');
        builder.Append(name).Append("|acceleration|")
            .Append(Line(JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,spine,domain,new KernelVector3{Z=1},out var acceleration),acceleration.Lower,acceleration.Upper)).Append('\n');
        builder.Append(name).Append("|reach|")
            .Append(JointNestedBlendEvaluation.CertifySplineReach(program,spine,domain.U0,domain.U1).ToString()).Append('\n');
        Span<IntervalNumber> support=stackalloc IntervalNumber[4];
        for(var side=0;side<2;side++)
        {
            var root=program.Spines[spine];if(!program.Surfaces[side==0?root.First:root.Second].IsBlend)continue;
            var status=JointNestedBlendEvaluation.BoundSpineSupportUv(program,spine,.37,.37,side,support);
            builder.Append(name).Append("|support-uv|").Append(side).Append('|').Append(status);
            foreach(var value in support)builder.Append('|').Append(Hex(value.Lower)).Append(',').Append(Hex(value.Upper));
            builder.Append('\n');
            status=JointNestedBlendEvaluation.BoundSpineSupportU(program,spine,.37,.37,side,support[..2]);
            builder.Append(name).Append("|support-u|").Append(side).Append('|').Append(status);
            foreach(var value in support[..2])builder.Append('|').Append(Hex(value.Lower)).Append(',').Append(Hex(value.Upper));
            builder.Append('\n');
        }
    }

    private static string[] SortedLines(string dump)=>dump.Split('\n').Where(l=>l.Length>0).Order(StringComparer.Ordinal).ToArray();

    private static string DumpFailures()
    {
        var builder=new StringBuilder();var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        builder.Append("invalid-box|").Append(JointNestedBlendEvaluation.Bound(program,4,new(.6,.2,.1,.9),bounds)).Append('\n');
        builder.Append("invalid-output|").Append(JointNestedBlendEvaluation.Bound(program,4,new(.2,.6,.1,.9),stackalloc IntervalNumber[8])).Append('\n');
        builder.Append("invalid-spine|").Append(JointNestedBlendEvaluation.BoundSpine(program,-2,new(.2,.6,.1,.9),bounds)).Append('\n');
        var singularSurfaces=NestedBlendSeriesTests.Surfaces();singularSurfaces[1]=singularSurfaces[0];
        builder.Append("singular|").Append(DumpBox(new NestedBlendProgram(singularSurfaces,NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts()),4,new(.2,.3,.1,.9))).Append('\n');
        return builder.ToString();
    }

    private static string DumpBox(in NestedBlendProgram program,BlendSurfaceIndex surface,in SurfaceParameterBox box)
    {
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];Span<double> contacts=stackalloc double[4];
        var status=JointNestedBlendEvaluation.Bound(program,surface,box,bounds);
        var builder=new StringBuilder().Append(status.ToString());
        for(var i=0;i<9;i++)builder.Append('|').Append(Hex(bounds[i].Lower)).Append(',').Append(Hex(bounds[i].Upper));
        var anchor=JointNestedBlendEvaluation.BoundSpineAnchor(program,program.Surfaces[surface].Spine,(box.U0+box.U1)*.5,bounds,contacts);
        builder.Append("|anchor=").Append(anchor.ToString());
        for(var i=0;i<4;i++)builder.Append('|').Append(Hex(contacts[i]));
        Span<IntervalNumber> capture=stackalloc IntervalNumber[9];Span<IntervalNumber> require=stackalloc IntervalNumber[9];
        for(var i=0;i<9;i++)require[i]=bounds[i];
        var witness=JointNestedBlendEvaluation.BoundSharedSpineWitness(program,program.Surfaces[surface].Spine,box,bounds,capture,require);
        builder.Append("|witness=").Append(witness.ToString());
        for(var i=0;i<9;i++)builder.Append('|').Append(Hex(capture[i].Lower)).Append(',').Append(Hex(capture[i].Upper));
        return builder.ToString();
    }

    private static string DumpSpine(in NestedBlendProgram program,int spine,in SurfaceParameterBox box)
    {
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var status=JointNestedBlendEvaluation.BoundSpine(program,spine,box,bounds);
        var builder=new StringBuilder().Append(status.ToString());
        for(var i=0;i<9;i++)builder.Append('|').Append(Hex(bounds[i].Lower)).Append(',').Append(Hex(bounds[i].Upper));
        return builder.ToString();
    }

    internal static BlendSurfaceInstruction[] SplineSurfaces()
    {
        var surfaces=NestedBlendSeriesTests.Surfaces();
        surfaces[0]=new(){IsBSpline=true,SplineBasis=0,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3,Rational=false,NormalSense=1};
        surfaces[2]=surfaces[0];surfaces[2].Offset=-.2;
        return surfaces;
    }
    internal static double[] SplineScalars()
    {
        var scalars=new double[4*3+8];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            var i=(2*u+v)*3;scalars[i+1]=u*2;scalars[i+2]=(2*v-1)*2;
        }
        double[] knots=[0,0,1,1,-1,-1,1,1];knots.CopyTo(scalars,12);
        return scalars;
    }

    private static string Box(in SurfaceParameterBox box)=>$"{Hex(box.U0)},{Hex(box.U1)},{Hex(box.V0)},{Hex(box.V1)}";
    private static string Line(AlgorithmStatus status,params double[] values)
    {
        var builder=new StringBuilder(status.ToString());foreach(var value in values)builder.Append('|').Append(Hex(value));return builder.ToString();
    }
    private static string Hex(double value)=>BitConverter.DoubleToInt64Bits(value).ToString("x16",CultureInfo.InvariantCulture);

    private static void PolluteStack(int depth)
    {
        Span<byte> garbage=stackalloc byte[8192];
        for(var i=0;i<garbage.Length;i++)garbage[i]=(byte)(0xA5^(i*31));
        if(depth>0)PolluteStack(depth-1);
        if(garbage[8191]==0x5A)throw new InvalidOperationException("unreachable");
    }
}
