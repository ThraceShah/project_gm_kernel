#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("Profile API contracts",out var host,out var message))throw new Exception(message);
using(host)
unsafe
{
    var circleDefinition=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0)),2);
    int circle=0,body=0,n=0,face=0,surface=0;int* edges=null;
    Check(PK_CIRCLE_create(&circleDefinition,&circle),"circle");
    Check(PK_CURVE_make_wire_body(circle,new PK_INTERVAL_t(0,Math.Tau),&body),"circle wire");
    Check(PK_BODY_ask_edges(body,&n,&edges),"wire edges");
    PK_LOGICAL_t sense=PK_LOGICAL_true; int shared=-1;
    Check(PK_EDGE_make_faces_from_wire(1,edges,&sense,&shared,&face),"make faces");
    Check(PK_FACE_ask_surf(face,&surface),"ask face surface");
    Console.WriteLine($"circle face={face}, surface={surface}");
    int nf=0;int* fins=null;Check(PK_EDGE_ask_fins(edges[0],&nf,&fins),"sheet edge fins");Console.WriteLine($"sheet boundary fins={nf}");for(var f=0;f<nf;f++){int loop=0;Check(PK_FIN_ask_loop(fins[f],&loop),"fin loop");Console.WriteLine($"fin={fins[f]} loop={loop}");}
    int ns=0,nr=0;int* shells=null;int* regions=null;Check(PK_BODY_ask_shells(body,&ns,&shells),"sheet shells");Check(PK_BODY_ask_regions(body,&nr,&regions),"sheet regions");Console.WriteLine($"sheet shells={ns} regions={nr}");
    int vertices=0;int* vertexList=null;Check(PK_BODY_ask_vertices(body,&vertices,&vertexList),"vertices");Console.WriteLine($"circle wire vertices={vertices}");
    foreach(var angle in new[]{.8,Math.Tau})
    {
        PK_VECTOR_t* rectangle=stackalloc PK_VECTOR_t[4]{new(2,0,0),new(4,0,0),new(4,0,3),new(2,0,3)};
        var sheetOptions=new PK_BODY_create_sheet_planar_o_t();sheetOptions.plane.axis=new PK_VECTOR1_t(0,1,0);
        int sheet=0;Check(PK_BODY_create_sheet_planar(4,rectangle,&sheetOptions,&sheet),"spin base sheet");Console.WriteLine($"options basis after=({sheetOptions.basis_set.axis.coord[0]},{sheetOptions.basis_set.axis.coord[1]},{sheetOptions.basis_set.axis.coord[2]}) reference=({sheetOptions.basis_set.ref_direction.coord[0]},{sheetOptions.basis_set.ref_direction.coord[1]},{sheetOptions.basis_set.ref_direction.coord[2]}) location=({sheetOptions.basis_set.location.coord[0]},{sheetOptions.basis_set.location.coord[1]},{sheetOptions.basis_set.location.coord[2]})");
        int nv=0;int* vtx=null;Check(PK_BODY_ask_vertices(sheet,&nv,&vtx),"ask spin vertices");for(var j=0;j<nv;j++){int pt=0;PK_POINT_sf_t point=default;Check(PK_VERTEX_ask_point(vtx[j],&pt),"vertex point");Check(PK_POINT_ask(pt,&point),"point");Console.WriteLine($"spin input vertex=({point.position.coord[0]},{point.position.coord[1]},{point.position.coord[2]})");}
        int nf2=0;int* fs=null;Check(PK_BODY_ask_faces(sheet,&nf2,&fs),"spin faces");int surf=0;PK_PLANE_sf_t plane=default;Check(PK_FACE_ask_surf(fs[0],&surf),"spin surface");Check(PK_PLANE_ask(surf,&plane),"spin plane");Console.WriteLine($"spin plane axis=({plane.basis_set.axis.coord[0]},{plane.basis_set.axis.coord[1]},{plane.basis_set.axis.coord[2]})");
        var axis=new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1));int count=0;int* laterals=null;int* bases=null;PK_local_check_t result=default;
        var spinError=PK_BODY_spin(sheet,&axis,angle,PK_LOGICAL_true,&count,&laterals,&bases,&result);Console.WriteLine("spin error="+spinError);if(spinError!=0)continue;Console.WriteLine($"spin angle={angle} check={result} laterals={count}");int ne=0,nvout=0,nfout=0;int* es=null;int* vs=null;int* fsout=null;Check(PK_BODY_ask_edges(sheet,&ne,&es),"spin output edges");Check(PK_BODY_ask_vertices(sheet,&nvout,&vs),"spin output vertices");Check(PK_BODY_ask_faces(sheet,&nfout,&fsout),"spin output faces");Console.WriteLine($"spin output V={nvout} E={ne} F={nfout}");
        for(var i=0;i<count;i++){int cls=0;var err=PK_ENTITY_ask_class(bases[i],&cls);int curve=0,curveClass=0;Check(PK_EDGE_ask_curve(bases[i],&curve),"base curve");Check(PK_ENTITY_ask_class(curve,&curveClass),"base curve class");Console.WriteLine($"base{i}={bases[i]} class={cls} error={err} lateral={laterals[i]} curveClass={curveClass}");}
    }
    for(var mode=0;mode<2;mode++)
    {
        PK_VECTOR_t* points=stackalloc PK_VECTOR_t[8]{new(-3,-2,0),new(3,-2,0),new(3,2,0),new(-3,2,0),new(-1,-.5,0),new(1,-.5,0),new(1,.5,0),new(-1,.5,0)};
        int* labels=stackalloc int[8]{mode,mode,mode,mode,mode+1,mode+1,mode+1,mode+1};
        var options=new PK_BODY_create_sheet_planar_o_t();options.loops=labels;
        int sheet=0;var error=PK_BODY_create_sheet_planar(8,points,&options,&sheet);Console.WriteLine($"hole labels offset={mode}: error={error},body={sheet}");
    }
}
static void Check(int error,string operation){Console.WriteLine($"{operation}: {error}");if(error!=0)throw new Exception(operation+": "+error);}
