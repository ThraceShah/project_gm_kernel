using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Public graph/loop/manifold/missing-geometry checker. Unsupported
    /// geometric intersection tests are rejected explicitly, not reported clean.</summary>
    public static int BodyCheck(EntityTag body,PK_BODY_check_o_s* options,int* count,PK_check_fault_s** faults)
    {
        if(Dispatcher.IsExecuting)return BodyCheckCore(body,options,count,faults);
        var command=new ProfileCheckCommand{Body=body,Options=options,Count=count,Faults=faults};
        return Dispatch(ApiId.BodyCheck,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,body);
    }

    private struct ProfileCheckCommand:IKernelCommand
    {
        internal EntityTag Body;
        internal PK_BODY_check_o_s* Options;
        internal BufferCount* Count;
        internal PK_check_fault_s** Faults;
        public int Execute()=>BodyCheckCore(Body,Options,Count,Faults);
    }

    private static int BodyCheckCore(EntityTag bodyTag,PK_BODY_check_o_s* options,int* count,PK_check_fault_s** faults)
    {
        if(count!=null)*count=0;if(faults!=null)*faults=null;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||count==null||faults==null||options->max_faults<=0||options->max_faults>4096)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=6)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(options->geom!=ParasolidConstants.PK_check_geom_no_c||options->bgeom!=ParasolidConstants.PK_check_bgeom_no_c
            ||options->mesh!=ParasolidConstants.PK_check_mesh_no_c||options->top_geo!=ParasolidConstants.PK_check_top_geo_no_c
            ||options->size_box!=ParasolidConstants.PK_check_size_box_no_c||options->fa_X!=ParasolidConstants.PK_check_fa_X_no_c
            ||options->fa_fa!=ParasolidConstants.PK_check_fa_fa_no_c||options->nmnl_geom!=ParasolidConstants.PK_check_nmnl_geom_no_c
            ||options->attribs!=ParasolidConstants.PK_check_attribs_no_c||options->returns!=ParasolidConstants.PK_check_returns_0_c
            ||options->extra_faults!=ParasolidConstants.PK_check_extra_faults_0_c)
            return ParasolidConstants.PK_ERROR_not_implemented;
        if(!TryResolveBodySlot(bodyTag,out var slot))return ParasolidConstants.PK_ERROR_bad_class;
        if(Xt.IsOpaque(slot))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<PK_check_fault_s> found=stackalloc PK_check_fault_s[options->max_faults];
        var n=0;
        ref readonly var body=ref Bodies[slot];
        var edge=body.FirstEdgeBody;
        for(var i=0;i<body.EdgeCountBody;i++)
        {
            if(!Edges.IsAlive(edge)||Edges[edge].Body!=slot)
            {ProfileFault(found,ref n,ParasolidConstants.PK_BODY_state_corrupt_c,bodyTag);break;}
            ref readonly var e=ref Edges[edge];
            if(options->missing_geom==ParasolidConstants.PK_check_missing_geom_yes_c&&GetCurveSlotByTag(e.CurveTag)<0)
                ProfileFault(found,ref n,ParasolidConstants.PK_TOPOL_state_no_geom_c,e.Header.Tag);
            if(e.StartVertex>=0&&(!Vertices.IsAlive(e.StartVertex)||Vertices[e.StartVertex].Body!=slot)
                ||e.EndVertex>=0&&(!Vertices.IsAlive(e.EndVertex)||Vertices[e.EndVertex].Body!=slot)
                ||(e.StartVertex<0)!=(e.EndVertex<0))
                ProfileFault(found,ref n,ParasolidConstants.PK_EDGE_state_bad_vertex_c,e.Header.Tag);
            if(body.BodyType==ParasolidConstants.PK_BODY_type_solid_c&&e.FinCount!=2)
                ProfileFault(found,ref n,ParasolidConstants.PK_EDGE_state_open_c,e.Header.Tag);
            var fin=e.FirstFinEdge;
            for(var j=0;j<e.FinCount;j++)
            {
                if(!Fins.IsAlive(fin)||Fins[fin].Edge!=edge||!Fins.IsAlive(Fins[fin].NextOfEdge)
                    ||Fins[Fins[fin].NextOfEdge].PrevOfEdge!=fin)
                {ProfileFault(found,ref n,ParasolidConstants.PK_BODY_state_corrupt_c,e.Header.Tag);break;}
                if(Fins[fin].Sense is not ('+' or '-'))ProfileFault(found,ref n,ParasolidConstants.PK_EDGE_state_bad_face_order_c,e.Header.Tag);
                fin=Fins[fin].NextOfEdge;
            }
            if(e.FinCount==2&&Fins.IsAlive(e.FirstFinEdge)&&Fins.IsAlive(e.LastFinEdge)
                &&Fins[e.FirstFinEdge].Sense==Fins[e.LastFinEdge].Sense)
                ProfileFault(found,ref n,ParasolidConstants.PK_EDGE_state_bad_face_order_c,e.Header.Tag);
            edge=e.NextInBody;
        }
        if(body.EdgeCountBody>0&&edge!=body.FirstEdgeBody)ProfileFault(found,ref n,ParasolidConstants.PK_BODY_state_corrupt_c,bodyTag);
        var face=body.FirstFaceBody;
        for(var i=0;i<body.FaceCountBody;i++)
        {
            if(!Faces.IsAlive(face)){ProfileFault(found,ref n,ParasolidConstants.PK_BODY_state_corrupt_c,bodyTag);break;}
            ref readonly var f=ref Faces[face];
            if(options->missing_geom==ParasolidConstants.PK_check_missing_geom_yes_c&&GetSurfaceSlotByTag(f.SurfTag)<0)
                ProfileFault(found,ref n,ParasolidConstants.PK_TOPOL_state_no_geom_c,f.Header.Tag);
            if(!Shells.IsAlive(f.BackShell)||!Shells.IsAlive(f.FrontShell)
                ||Shells[f.BackShell].Body!=slot||Shells[f.FrontShell].Body!=slot)
                ProfileFault(found,ref n,ParasolidConstants.PK_SHELL_state_bad_topol_geom_c,f.Header.Tag);
            var loop=f.FirstLoop;
            for(var j=0;j<f.LoopCount;j++)
            {
                if(!Loops.IsAlive(loop)||Loops[loop].Face!=face)
                {ProfileFault(found,ref n,ParasolidConstants.PK_LOOP_state_invalid_c,f.Header.Tag);break;}
                ref readonly var l=ref Loops[loop];var fin=l.FirstFin;
                for(var k=0;k<l.FinCount;k++)
                {
                    if(!Fins.IsAlive(fin)||Fins[fin].Loop!=loop||!Fins.IsAlive(Fins[fin].NextInLoop)
                        ||Fins[Fins[fin].NextInLoop].PrevInLoop!=fin)
                    {ProfileFault(found,ref n,ParasolidConstants.PK_LOOP_state_invalid_c,l.Header.Tag);break;}
                    var next=Fins[fin].NextInLoop;
                    if(options->loops==ParasolidConstants.PK_check_loops_yes_c&&Edges.IsAlive(Fins[next].Edge))
                    {
                        var nextEdge=Edges[Fins[next].Edge];
                        var start=Fins[next].Sense=='+'?nextEdge.StartVertex:nextEdge.EndVertex;
                        if(Fins[fin].Vertex!=start)ProfileFault(found,ref n,ParasolidConstants.PK_FACE_state_bad_edge_order_c,f.Header.Tag);
                    }
                    fin=Fins[fin].NextInLoop;
                }
                if(l.FinCount==0||fin!=l.FirstFin)ProfileFault(found,ref n,ParasolidConstants.PK_LOOP_state_invalid_c,l.Header.Tag);
                loop=l.NextInFace;
            }
            face=f.NextInBody;
        }
        var shell=body.FirstShell;
        for(var i=0;i<body.ShellCount;i++)
        {
            if(!Shells.IsAlive(shell)||Shells[shell].Body!=slot||!Regions.IsAlive(Shells[shell].Region)
                ||Regions[Shells[shell].Region].Body!=slot)
            {ProfileFault(found,ref n,ParasolidConstants.PK_REGION_state_bad_shells_c,bodyTag);break;}
            shell=Shells[shell].NextInBody;
        }
        var vertex=body.FirstVertexBody;
        for(var i=0;i<body.VertexCountBody;i++)
        {
            if(!Vertices.IsAlive(vertex)||Vertices[vertex].Body!=slot)
            {ProfileFault(found,ref n,ParasolidConstants.PK_BODY_state_corrupt_c,bodyTag);break;}
            if(options->missing_geom==ParasolidConstants.PK_check_missing_geom_yes_c&&GetPointSlotByTag(Vertices[vertex].PointTag)<0)
                ProfileFault(found,ref n,ParasolidConstants.PK_TOPOL_state_no_geom_c,Vertices[vertex].Header.Tag);
            vertex=Vertices[vertex].NextInBody;
        }
        if(n==0)return 0;
        var output=(PK_check_fault_s*)State.Session->Returns.TryAllocate((nuint)(n*sizeof(PK_check_fault_s)));
        if(output==null)return ParasolidConstants.PK_ERROR_memory_full;
        found[..n].CopyTo(new Span<PK_check_fault_s>(output,n));*count=n;*faults=output;
        return 0;
    }

    private static void ProfileFault(Span<PK_check_fault_s> faults,ref int count,int state,EntityTag entity)
    {
        if(count<faults.Length)faults[count++]=new PK_check_fault_s{state=state,entity_1=entity};
    }
}
