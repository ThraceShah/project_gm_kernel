using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Prepared source identity for one immutable command. This is algorithm work,
// never geometry or a substitute for the native CHART/UV definition.
internal readonly struct JointTubeSource(GeomTag tag,EntityGeneration generation,KernelSense sense,byte kind,DataSlot block)
{
    internal readonly GeomTag Tag=tag;
    internal readonly EntityGeneration Generation=generation;
    internal readonly KernelSense Sense=sense;
    internal readonly byte Kind=kind;
    internal readonly DataSlot Block=block;
    internal bool Same(in JointTubeSource other)=>Tag==other.Tag&&Generation==other.Generation&&Sense==other.Sense&&Kind==other.Kind&&Block==other.Block;
}
[InlineArray(63)] internal struct JointTubeValues{private IntervalNumber value;}
[InlineArray(16)] internal struct JointTubeNodes{private JointNestedBlendEvaluation.Occurrence value;}
internal struct JointTubeEntry
{
    internal JointTubeSource Source;
    internal BlendSpineIndex Spine;
    internal BufferCount Surfaces,Spines,Charts,Scalars,Workspace,N,Nodes;
    internal long First,Last;
    internal JointTubeNodes Occurrences;
    internal JointTubeValues Boxes,Derivative;
}

internal static partial class JointNestedBlendEvaluation
{
    private static BufferOffset TubeIndex(in NestedBlendProgram program,BlendSpineIndex spine,in SurfaceParameterBox domain)
    {
        unchecked
        {
            var source=program.TubeSource;var hash=(ulong)(uint)source.Tag|((ulong)(uint)source.Generation<<32);
            hash^=(ulong)BitConverter.DoubleToInt64Bits(domain.U0);hash=(hash^(hash>>30))*0xbf58476d1ce4e5b9UL;
            hash^=(ulong)BitConverter.DoubleToInt64Bits(domain.U1)^(uint)spine;hash=(hash^(hash>>27))*0x94d049bb133111ebUL;
            return (BufferOffset)((hash^(hash>>31))%(uint)program.TubeWork.Length);
        }
    }
    private static bool SameTube(in JointTubeEntry entry,in NestedBlendProgram program,BlendSpineIndex spine,in SurfaceParameterBox domain)
        =>entry.Source.Same(program.TubeSource)&&entry.Spine==spine&&entry.First==BitConverter.DoubleToInt64Bits(domain.U0)&&entry.Last==BitConverter.DoubleToInt64Bits(domain.U1)
        &&entry.Surfaces==program.Surfaces.Length&&entry.Spines==program.Spines.Length&&entry.Charts==program.Charts.Length&&entry.Scalars==program.Scalars.Length&&entry.Workspace==program.Workspace.Length;

    private static bool ReadTube(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,
        Span<IntervalNumber> output,out AlgorithmStatus status)
    {
        status=AlgorithmStatus.NotRun;if(program.TubeSource.Tag<=0||program.TubeWork.IsEmpty)return false;
        var at=TubeIndex(program,spine,domain);ref var entry=ref program.TubeWork[at];if(!SameTube(entry,program,spine,domain))return false;
        status=TubeFrame(program,surface,domain,((ReadOnlySpan<Occurrence>)entry.Occurrences)[..entry.Nodes],((ReadOnlySpan<IntervalNumber>)entry.Boxes)[..entry.N],((ReadOnlySpan<IntervalNumber>)entry.Derivative)[..entry.N],output);
        return true;
    }
    private static void WriteTube(in NestedBlendProgram program,BlendSpineIndex spine,in SurfaceParameterBox domain,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalNumber> boxes,ReadOnlySpan<IntervalNumber> derivative)
    {
        if(program.TubeSource.Tag<=0||program.TubeWork.IsEmpty)return;ref var entry=ref program.TubeWork[TubeIndex(program,spine,domain)];
        entry.Source=program.TubeSource;entry.Spine=spine;entry.First=BitConverter.DoubleToInt64Bits(domain.U0);entry.Last=BitConverter.DoubleToInt64Bits(domain.U1);
        entry.Surfaces=program.Surfaces.Length;entry.Spines=program.Spines.Length;entry.Charts=program.Charts.Length;entry.Scalars=program.Scalars.Length;entry.Workspace=program.Workspace.Length;entry.N=boxes.Length;entry.Nodes=nodes.Length;
        nodes.CopyTo(entry.Occurrences);boxes.CopyTo(entry.Boxes);derivative.CopyTo(entry.Derivative);
    }
    private static AlgorithmStatus TubeFrame(in NestedBlendProgram program,BlendSurfaceIndex surface,in SurfaceParameterBox domain,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalNumber> boxes,ReadOnlySpan<IntervalNumber> derivative,Span<IntervalNumber> output)
    {
        if(surface<0){for(var i=0;i<3;i++){output[i]=boxes[i];output[3+i]=derivative[i];output[6+i]=0;}return AlgorithmStatus.Success;}
        Span<IntervalDual> variables=stackalloc IntervalDual[boxes.Length];for(var i=0;i<variables.Length;i++)variables[i]=new(boxes[i],derivative[i]);var v=new IntervalNumber(domain.V0,domain.V1);
        var status=IntervalFrame(program,surface,nodes,variables,v,out var frame);if(status!=AlgorithmStatus.Success)return status;
        output[0]=frame.X.Value;output[1]=frame.Y.Value;output[2]=frame.Z.Value;output[3]=frame.X.Derivative;output[4]=frame.Y.Derivative;output[5]=frame.Z.Derivative;
        for(var i=0;i<variables.Length;i++)variables[i]=new(boxes[i],0);
        status=IntervalFrame(program,surface,nodes,variables,new(v,1),out frame);if(status!=AlgorithmStatus.Success)return status;
        output[6]=frame.X.Derivative;output[7]=frame.Y.Derivative;output[8]=frame.Z.Derivative;return AlgorithmStatus.Success;
    }
}
