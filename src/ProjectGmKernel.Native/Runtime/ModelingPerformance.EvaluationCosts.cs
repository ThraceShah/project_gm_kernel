using System.Diagnostics;
using System.Runtime.CompilerServices;
namespace ProjectGmKernel.Native.Runtime;

// Diagnostic branch only. Elapsed and child-subtracted elapsed are wall ticks,
// not exclusive CPU. No math, source identity or proof demand is changed.
internal static unsafe partial class ModelingPerformance
{
    private struct EvaluationCostFrame
    {
        internal Context* Context;
        internal EvaluationCost Kind;
        internal long Started,Children;
    }
    [InlineArray(64)] private struct EvaluationCostStack{private EvaluationCostFrame value;}
    [ThreadStatic] private static EvaluationCostStack evaluationStack;
    [ThreadStatic] private static BufferCount evaluationDepth;
    [ThreadStatic] internal static BufferCount EvaluationMaxDepth;
    [ThreadStatic] internal static long EvaluationDropped;
    internal readonly struct EvaluationScope:IDisposable
    {
        private readonly BufferOffset level;
        internal EvaluationScope(EvaluationCost kind)
        {
            level=-1;if(current==null||!current->Capture)return;
            if(evaluationDepth>=64){EvaluationDropped++;return;}
            level=evaluationDepth++;EvaluationMaxDepth=Math.Max(EvaluationMaxDepth,evaluationDepth);
            evaluationStack[level]=new(){Context=current,Kind=kind,Started=Stopwatch.GetTimestamp()};
        }
        public void Dispose()
        {
            if(level<0)return;var ended=Stopwatch.GetTimestamp();var frame=evaluationStack[level];var elapsed=ended-frame.Started;var kind=(byte)frame.Kind;
            frame.Context->Profile.EvaluationCalls[kind]++;frame.Context->Profile.EvaluationElapsed[kind]+=elapsed;frame.Context->Profile.EvaluationOwnElapsed[kind]+=elapsed-frame.Children;
            evaluationDepth=level;if(level>0)evaluationStack[level-1].Children+=elapsed;
        }
    }
}
