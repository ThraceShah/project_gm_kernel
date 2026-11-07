#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using System.Text;

if(!ParasolidScriptHost.TryStartSession("separated rolling components",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");KernelRuntime.ForbidLegacyRegularBlendAssembly=true;
    try
    {
        var reference=Block(false,0,0,0,1,1,1);var own=Receive(true,Transmit(false,reference));
        Fix(false,reference);Fix(true,own);Require(KernelRuntime.LastRollingNetworkStage==KernelRuntime.RollingNetworkStage.Publish,"shared network publication");
        Compare(reference,own,"separated components");
        int copy;Check(KernelRuntime.EntityCopy(own,&copy),"copy separated components");Check(KernelRuntime.EntityDelete(1,&own),"delete component source");own=copy;
        own=Cut(true,own,Block(true,.5,0,-1,1,4,4));reference=Cut(false,reference,Block(false,.5,0,-1,1,4,4));
        Compare(reference,own,"component source deletion and cut");
        var bytes=Transmit(true,own);Check(KernelRuntime.EntityDelete(1,&own),"delete cut before reload");own=Receive(true,bytes);
        own=Cut(true,own,Block(true,0,.5,-1,4,1,4));reference=Cut(false,reference,Block(false,0,.5,-1,4,1,4));
        Compare(reference,own,"reloaded components and second cut");
        Check(KernelRuntime.EntityDelete(1,&own),"delete own result");Check(PK_ENTITY_delete(1,&reference),"delete native result");
        Require(KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0,"component geometry closure released");
        Console.WriteLine("PASS separated nonadjacent components, fresh strict PK, source deletion, reload and subsequent Boolean");
    }
    finally{KernelRuntime.ForbidLegacyRegularBlendAssembly=false;Check(KernelRuntime.SessionStop(),"managed stop");}
}
static unsafe int Block(bool own,double x,double y,double z,double dx,double dy,double dz)
{
    var axis=new PK_AXIS2_sf_t(new PK_VECTOR_t(x,y,z),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0));int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(dx,dy,dz,(M.PK_AXIS2_sf_s*)&axis,&body):PK_BODY_create_solid_block(dx,dy,dz,&axis,&body),"component block");return body;
}
static unsafe void Fix(bool own,int body)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"component edges");
    Span<int> selected=stackalloc int[2];selected.Clear();var ends=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var i=0;i<n;i++)
        {
            if(own)
            {
                var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edges[i]).Slot];
                for(var e=0;e<2;e++)
                {var vertex=e==0?edge.StartVertex:edge.EndVertex;var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position;ends[e]=new(point.X,point.Y,point.Z);}
            }
            else{int curve,type;PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&type,ends,&range,&sense),"component geometry");}
            if(ends[0].coord[1]!=ends[1].coord[1]||ends[0].coord[2]!=ends[1].coord[2])continue;
            if(ends[0].coord[1]==-.5&&ends[0].coord[2]==0)selected[0]=edges[i];
            if(ends[0].coord[1]==.5&&ends[0].coord[2]==1)selected[1]=edges[i];
        }
    }
    finally{Free(own,edges);}
    Require(selected[0]!=0&&selected[1]!=0,"two nonadjacent parallel occurrences");
    var mark=new PK_EDGE_set_blend_constant_o_t();mark.properties.tolerance=1e-7;int count;int* marked;
    fixed(int* p=selected)Check(own?KernelRuntime.EdgeSetBlendConstant(2,p,.1,(M.PK_EDGE_set_blend_constant_o_s*)&mark,&count,&marked):PK_EDGE_set_blend_constant(2,p,.1,&mark,&count,&marked),"component mark");Free(own,marked);
    var options=new PK_BODY_fix_blends_o_t{tolerance=1e-7};int fault,edgeFault,topolFault;
    var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&options,&count,null,null,null,&fault,&edgeFault,&topolFault):PK_BODY_fix_blends(body,&options,&count,null,null,null,&fault,&edgeFault,&topolFault);
    Console.WriteLine($"FIX {(own?"self":"PK")} error={error} faces={count} network={KernelRuntime.LastRollingNetworkStage}");Check(error,"component fix");Require(count==2,"two component products");
}
static unsafe int Cut(bool own,int body,int tool)
{
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7};int n;int* products;
    Check(own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&products):PK_BODY_boolean(body,1,&tool,&options,&n,&products),"component cut");
    try{Require(n==1,"one cut component result");return products[0];}finally{Free(own,products);}
}
static unsafe void Compare(int reference,int own,string label)
{
    var actual=Receive(false,Transmit(true,own));
    try
    {
        foreach(var body in new[]{reference,actual})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"component full check");
            try{for(var i=0;i<n;i++)Console.WriteLine($"FAULT {label} state={faults[i].state} entity={faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,label+" full BODY_check");}finally{Free(false,faults);}
        }
        ParasolidBodyCompare.AssertEquivalent(reference,actual,label);
    }
    finally{Check(PK_ENTITY_delete(1,&actual),"delete fresh component check");}
}
static unsafe byte[] Transmit(bool own,int body)
{
    if(own)SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(body).Slot);
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"component transmit");
    try{using var output=new MemoryStream();for(var p=&block;p!=null;p=p->next)output.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return output.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block),"component transmit free");}
}
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"component receive");
        try{Require(n==1,"one component body");return parts[0];}finally{Free(own,parts);}
    }
}
static unsafe void Free(bool own,void* p){if(p!=null)Check(own?KernelRuntime.MemoryFree(p):PK_MEMORY_free(p),"component array free");}
static void Check(int error,string label)=>ParasolidScriptHost.Check(error,label);
static void Require(bool value,string label){if(!value)throw new Exception(label);}
