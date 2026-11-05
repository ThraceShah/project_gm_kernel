using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Immutable instructions compiled from a Blend dependency DAG.
/// Analytic primitives retain their exact definitions; Blend instructions
/// retain their actual supports and procedural spine.</summary>
internal struct BlendSurfaceInstruction
{
    internal AnalyticSurface Analytic;
    internal bool IsBlend;
    internal bool IsBSpline;
    internal BlendSurfaceIndex First,Second;
    internal BlendSpineIndex Spine;
    internal double Range0,Range1;
    internal double NormalSense;
    internal BlendSurfaceIndex SplineBasis;
    internal BufferOffset ScalarOffset;
    internal SplineDegree UDegree,VDegree;
    internal BufferCount UCount,VCount,Dimension;
    internal bool Rational,UPeriodic,VPeriodic;
    internal double Offset;
}

internal struct BlendSpineInstruction
{
    internal BlendSurfaceIndex First,Second;
    internal BufferOffset ChartOffset;
    internal BufferCount ChartCount;
    internal LimitType StartType,EndType;
    internal double ChordalError,AngularError;
}

internal struct BlendChartInstruction
{
    internal KernelVector3 Position,Chord;
    internal double Parameter,Scale;
    internal double FirstU,FirstV,SecondU,SecondV;
}

internal readonly ref struct NestedBlendProgram(ReadOnlySpan<BlendSurfaceInstruction> surfaces,
    ReadOnlySpan<BlendSpineInstruction> spines,ReadOnlySpan<BlendChartInstruction> charts,
    ReadOnlySpan<double> scalars=default,Span<double> workspace=default)
{
    internal ReadOnlySpan<BlendSurfaceInstruction> Surfaces{get;}=surfaces;
    internal ReadOnlySpan<BlendSpineInstruction> Spines{get;}=spines;
    internal ReadOnlySpan<BlendChartInstruction> Charts{get;}=charts;
    internal ReadOnlySpan<double> Scalars{get;}=scalars;
    internal Span<double> Workspace{get;}=workspace;
    internal BSurfaceView Spline(in BlendSurfaceInstruction node)
    {
        var poles=node.UCount*node.VCount*node.Dimension;var nu=node.UCount+node.UDegree+1;var nv=node.VCount+node.VDegree+1;
        var data=Scalars[node.ScalarOffset..];
        return new(node.UDegree,node.VDegree,node.UCount,node.VCount,node.Dimension,node.Rational,node.UPeriodic,node.VPeriodic,data[..poles],data.Slice(poles,nu),data.Slice(poles+nu,nv));
    }
}
