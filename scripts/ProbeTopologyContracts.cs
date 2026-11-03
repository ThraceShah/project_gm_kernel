#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Topology contracts", out var host, out var reason))
    throw new InvalidOperationException(reason);
using (host) Run();

static unsafe void Run()
{
    PK_BODY_t block, sheet, wire, apexCone;
    Check(PK_BODY_create_solid_block(2,3,4,null,&block));
    Check(PK_BODY_create_sheet_circle(2,null,&sheet));
    Check(PK_BODY_create_solid_cone(0,5,.25,null,&apexCone));
    var sf = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(1,2,3),new PK_VECTOR1_t(0,0,1)));
    PK_CURVE_t line; Check(PK_LINE_create(&sf,&line));
    var bounds = new PK_INTERVAL_t(0,3);
    var options = new PK_CURVE_make_wire_body_o_t();
    int n; PK_EDGE_t* edges; int* indices;
    Check(PK_CURVE_make_wire_body_2(1,&line,&bounds,&options,&wire,&n,&edges,&indices));
    foreach (var pair in new[]{("block",block),("sheet",sheet),("wire",wire),("apex-cone",apexCone)})
        foreach (var wantFins in new[]{false,true}) Dump(pair.Item1,pair.Item2,wantFins);
}

static unsafe void Dump(string name, PK_BODY_t body, bool wantFins)
{
    var options = new PK_BODY_ask_topology_o_t { want_fins = (byte)(wantFins ? 1:0) };
    int count, relations; int* topols; int* classes; int* parents; int* children; int* senses;
    Check(PK_BODY_ask_topology(body,&options,&count,&topols,&classes,&relations,&parents,&children,&senses));
    Console.WriteLine($"{name} fins={wantFins}: topols={count}, relations={relations}");
    var groups = new Dictionary<string,int>();
    for(var i=0;i<relations;i++)
    {
        var key=$"{Class(classes[parents[i]])}->{Class(classes[children[i]])} sense={senses[i]}";
        groups[key]=groups.GetValueOrDefault(key)+1;
    }
    foreach(var pair in groups) Console.WriteLine($"  {pair.Key}: {pair.Value}");
    if(name=="apex-cone"&&wantFins)
        for(var i=0;i<count;i++)if(classes[i]==PK_CLASS_loop)
        {
            int nFins;PK_FIN_t* fins;
            Check(PK_LOOP_ask_fins(topols[i],&nFins,&fins));
            Console.WriteLine($"  loop {topols[i]} public fins={nFins}");
            if(fins!=null)Check(PK_MEMORY_free(fins));
        }
    for(var i=0;i<count;i++) if(classes[i]==PK_CLASS_edge)
    {
        int nFins; PK_FIN_t* fins;
        Check(PK_EDGE_ask_fins(topols[i],&nFins,&fins));
        Console.WriteLine($"  first edge public fins={nFins}");
        if(fins!=null) Check(PK_MEMORY_free(fins));
        break;
    }
    Check(PK_MEMORY_free(topols)); Check(PK_MEMORY_free(classes)); Check(PK_MEMORY_free(parents));
    Check(PK_MEMORY_free(children)); Check(PK_MEMORY_free(senses));
}
static string Class(int type) => type switch
{
    PK_CLASS_body=>"body",PK_CLASS_region=>"region",PK_CLASS_shell=>"shell",PK_CLASS_face=>"face",
    PK_CLASS_loop=>"loop",PK_CLASS_fin=>"fin",PK_CLASS_edge=>"edge",PK_CLASS_vertex=>"vertex",_=>type.ToString(),
};
static void Check(int error) { if(error!=0)throw new InvalidOperationException($"PK error {error}"); }
