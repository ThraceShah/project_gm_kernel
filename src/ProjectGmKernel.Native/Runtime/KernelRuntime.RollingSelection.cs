using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int SelectRollingReplacement(BodySlot body,ReadOnlySpan<FaceSlot> originals,ReadOnlySpan<EdgeSlot> selected,
        ReadOnlySpan<RollingBandCandidate> bands,ReadOnlySpan<RollingSubdivision> subdivisions,
        in RegularBoundaryView input,ReadOnlySpan<SheetSourceEdge> fragments,ref SheetArrangement model,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;Span<BufferOffset> components=stackalloc BufferOffset[model.NF];
        Span<byte> removed=stackalloc byte[model.NF];removed.Clear();
        Span<BufferOffset> heads=stackalloc BufferOffset[model.NE],next=stackalloc BufferOffset[model.NC],owners=stackalloc BufferOffset[model.NC];heads.Fill(-1);
        for(var f=0;f<model.NF;f++)
        {
            components[f]=f;var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {var c=loop.FirstCoedge+k;var edge=model.Coedges[c].Edge;owners[c]=f;next[c]=heads[edge];heads[edge]=c;}
            }
        }
        // Across an unchanged source-boundary fragment the two adjacent
        // source cells share removal status. Candidate cuts are barriers.
        foreach(var source in fragments)if(source.Source>=0)
            for(var e=source.First;e<source.First+source.Count;e++)
            {
                var first=-1;
                for(var c=heads[e];c>=0;c=next[c])
                {
                    var f=owners[c];if(model.Sources[f]<0)continue;
                    if(first<0)first=f;else components[RollingComponent(components,f)]=RollingComponent(components,first);
                }
            }
        foreach(var source in fragments)if(source.Source>=0&&selected.Contains(source.Source))
            for(var e=source.First;e<source.First+source.Count;e++)
                for(var c=heads[e];c>=0;c=next[c])if(model.Sources[owners[c]]>=0)removed[RollingComponent(components,owners[c])]=1;
        Span<sbyte> state=stackalloc sbyte[model.NF];state.Fill(-1);
        for(var f=0;f<model.NF;f++)if(model.Sources[f]>=0)state[f]=removed[RollingComponent(components,f)]!=0?(sbyte)0:(sbyte)1;
        Span<BufferOffset> trail=stackalloc BufferOffset[model.NF];var trailCount=0;
        var propagated=PropagateRollingSelection(model.Coedges[..model.NC],heads,next,owners,state,trail,ref trailCount,out var possible);
        if(propagated!=0)return propagated;if(!possible)return ParasolidConstants.PK_ERROR_bad_loop;trailCount=0;
        Span<BufferOffset> bodyFaces=stackalloc BufferOffset[originals.Length];for(var i=0;i<bodyFaces.Length;i++)bodyFaces[i]=i;
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
        for(var f=0;f<model.NF;f++)
        {
            LastRollingFirstFace=f;
            if(model.Sources[f]>=0||state[f]==0)continue;
            var uv=model.Witnesses[f];var face=model.Faces[f];var winding=0;var error=0;
            for(var attempt=0;attempt<2;attempt++)
            {
                var found=EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(face.Surface)],uv.param[0],uv.param[1],in layout,point,0);if(found!=AlgorithmStatus.Success)return EvaluationError(found);
                error=RegularClassify(input,bodyFaces,default,point[0],out winding,out _);
                if(error==0)break;
                if(attempt!=0||!TryRegularCellInteriorWitness(ref model,f,out uv))return error;
            }
            if(winding is not (0 or 1))return ParasolidConstants.PK_ERROR_bad_loop;
            if(winding==0){if(state[f]==1)return ParasolidConstants.PK_ERROR_cant_do_intersect;state[f]=0;}
        }
        if(TraceRollingNetwork!=null)for(var f=0;f<model.NF;f++)TraceRollingNetwork(new(){Stage=RollingNetworkStage.Select,Cell=f,CellSource=model.Sources[f],Surface=model.Faces[f].Surface,Material=state[f]});
        Span<sbyte> solution=stackalloc sbyte[model.NF];var solutions=0;var budget=4096;
        var solve=SolveRollingSelection(model.Coedges[..model.NC],heads,next,owners,state,solution,trail,ref trailCount,ref solutions,ref budget);
        if(solve!=0)return solve;if(solutions!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(TraceRollingNetwork!=null)for(var f=0;f<model.NF;f++)TraceRollingNetwork(new(){Stage=RollingNetworkStage.Build,Cell=f,CellSource=model.Sources[f],Surface=model.Faces[f].Surface,Material=solution[f]});
        Span<BufferOffset> indices=stackalloc BufferOffset[model.NF];Span<FaceSlot> preserved=stackalloc FaceSlot[originals.Length];var count=0;var keepCount=0;
        foreach(var original in originals)
            for(var f=0;f<model.NF;f++)if(solution[f]!=0&&model.Sources[f]==original)
            {indices[count++]=f;preserved[keepCount++]=original;break;}
        if(keepCount==0)return ParasolidConstants.PK_ERROR_bad_loop;
        for(var f=0;f<model.NF;f++)if(solution[f]!=0&&!indices[..count].Contains(f))indices[count++]=f;
        if(count>products.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        foreach(var band in bands)
        {var covered=false;foreach(var f in indices[..count])covered|=model.Faces[f].Surface==band.Surface;if(!covered)return ParasolidConstants.PK_ERROR_cant_do_intersect;}
        ReuseRollingBoundaryCarriers(fragments,solution,heads,next,owners,ref model);
        Span<byte> reversed=stackalloc byte[model.NF],cavities=stackalloc byte[model.NF];reversed.Clear();cavities.Clear();
        LastRollingNetworkStage=RollingNetworkStage.Build;
        var errorBuild=CloneRegularBoundaryBody(ref model,indices[..count],reversed,cavities,out var output,products[..count]);if(errorBuild!=0)return errorBuild;
        for(var band=0;band<bands.Length;band++)
            for(var i=0;i<count;i++)if(model.Faces[indices[i]].Surface==bands[band].Surface)
            {
                if(made==descriptions.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                var axial=Surfaces[GetSurfaceSlotByTag(bands[band].Surface)].Class==SurfaceClass.Cylinder;
                var source=RollingCellSource(bands[band].Source,band,subdivisions,model.Witnesses[indices[i]].param[axial?1:0]);
                if(source<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                descriptions[made++]=new(){Edge=Edges[source].Header.Tag,Sequence=Edges[source].PendingBlendSequence,
                    First=Faces[bands[band].FirstSupport].Header.Tag,Second=Faces[bands[band].SecondSupport].Header.Tag,Result=Faces[products[i]].Header.Tag};
            }
        for(var i=1;i<made;i++)for(var j=i;j>0&&descriptions[j].Sequence<descriptions[j-1].Sequence;j--)
            (descriptions[j],descriptions[j-1])=(descriptions[j-1],descriptions[j]);
        LastRollingNetworkStage=RollingNetworkStage.Publish;
        return ReplaceLocalBody(body,output,preserved[..keepCount],products[..count]);
    }

    private static EdgeSlot RollingCellSource(EdgeSlot representative,BufferOffset band,ReadOnlySpan<RollingSubdivision> subdivisions,double station)
    {
        EdgeSlot before=-1,after=-1;var lo=double.NegativeInfinity;var hi=double.PositiveInfinity;
        foreach(ref readonly var split in subdivisions)if(split.Band==band)
        {
            if(station>=split.StationLower&&station<=split.StationUpper)return split.Continued?split.Before:-1;
            if(station>split.StationUpper&&split.StationUpper>lo){lo=split.StationUpper;after=split.After;}
            if(station<split.StationLower&&split.StationLower<hi){hi=split.StationLower;before=split.Before;}
        }
        if(before>=0&&after>=0&&before!=after)return -1;
        return before>=0?before:after>=0?after:representative;
    }

    private static BufferOffset RollingComponent(Span<BufferOffset> components,BufferOffset face)
    {while(components[face]!=face){components[face]=components[components[face]];face=components[face];}return face;}

    // Each edge permits either zero selected uses, or exactly two opposite
    // selected uses. Propagate those finite incidence constraints, and require
    // a unique complete assignment after original-material classification.
    private static int SolveRollingSelection(ReadOnlySpan<ModelingCoedge> coedges,ReadOnlySpan<BufferOffset> heads,ReadOnlySpan<BufferOffset> next,
        ReadOnlySpan<BufferOffset> owners,Span<sbyte> state,Span<sbyte> solution,Span<BufferOffset> trail,
        ref BufferCount trailCount,ref BufferCount solutions,ref BufferCount budget)
    {
        if(--budget<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var checkpoint=trailCount;var error=PropagateRollingSelection(coedges,heads,next,owners,state,trail,ref trailCount,out var possible);
        if(error!=0)return error;
        if(possible)
        {
            var unknown=state.IndexOf((sbyte)-1);
            if(unknown<0){if(solutions++==0)state.CopyTo(solution);}
            else for(sbyte value=0;value<=1&&solutions<2;value++)
            {
                state[unknown]=value;var before=trailCount;
                error=SolveRollingSelection(coedges,heads,next,owners,state,solution,trail,ref trailCount,ref solutions,ref budget);
                if(error!=0)return error;while(trailCount>before)state[trail[--trailCount]]=-1;state[unknown]=-1;
            }
        }
        while(trailCount>checkpoint)state[trail[--trailCount]]=-1;return 0;
    }

    private static int PropagateRollingSelection(ReadOnlySpan<ModelingCoedge> coedges,ReadOnlySpan<BufferOffset> heads,ReadOnlySpan<BufferOffset> next,
        ReadOnlySpan<BufferOffset> owners,Span<sbyte> state,Span<BufferOffset> trail,ref BufferCount trailCount,out bool possible)
    {
        possible=true;Span<BufferOffset> uses=stackalloc BufferOffset[8];var changed=true;
        while(changed)
        {
            changed=false;
            for(var e=0;e<heads.Length;e++)
            {
                var n=0;for(var c=heads[e];c>=0;c=next[c]){if(n==uses.Length)return ParasolidConstants.PK_ERROR_not_implemented;uses[n++]=c;}
                var positive=0;var negative=0;var accepted=0;
                for(var mask=0;mask<(1<<n);mask++)
                {
                    var balance=0;var count=0;var valid=true;
                    for(var i=0;i<n;i++)
                    {
                        var selected=(mask>>i)&1;var face=owners[uses[i]];
                        if(state[face]>=0&&state[face]!=selected){valid=false;break;}
                        for(var j=0;j<i;j++)if(owners[uses[j]]==face&&((mask>>j)&1)!=selected)valid=false;
                        count+=selected;balance+=selected*(coedges[uses[i]].Forward!=0?1:-1);
                    }
                    if(!valid||balance!=0||count is not (0 or 2))continue;
                    accepted++;positive|=mask;negative|=((1<<n)-1)^mask;
                }
                if(accepted==0){possible=false;return 0;}
                for(var i=0;i<n;i++)
                {
                    var face=owners[uses[i]];if(state[face]>=0)continue;
                    if((positive&(1<<i))==0||(negative&(1<<i))==0)
                    {state[face]=(positive&(1<<i))==0?(sbyte)0:(sbyte)1;trail[trailCount++]=face;changed=true;}
                }
            }
        }
        return 0;
    }
}
