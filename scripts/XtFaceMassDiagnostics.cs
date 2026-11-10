#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
// Diagnostic: per-face PK mass properties of a reference and an actual XT,
// matched by surface class and nearest centroid. BODYMASS=1 compares bodies.
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("face mass diag",out var session,out var skip)){Console.WriteLine(skip);return 0;}
using(session)unsafe
{
    if(Environment.GetEnvironmentVariable("BODYMASS")=="1"){var ra=Body(args[0]);for(var k=1;k<args.Length;k++){var rb=Body(args[k]);var e=0.0;var w=0;for(var i=0;i<15;i++){var r=Math.Abs(ra[i]-rb[i])/(1+Math.Abs(ra[i]));if(r>e){e=r;w=i;}}Console.WriteLine($"BODY {Path.GetFileName(args[k])} normalized15={e:R} worst={w} {ra[w]:R}/{rb[w]:R} vol={ra[0]:R}/{rb[0]:R}");}return 0;}
    var a=Faces(args[0]);var b=Faces(args[1]);
    foreach(var f in a)
    {
        var best=b.Where(g=>g.Class==f.Class).OrderBy(g=>D(f.C,g.C)).FirstOrDefault();
        if(best==null){Console.WriteLine($"FACE {f.Face} class={f.Class} unmatched");continue;}
        var di=0.0;for(var i=0;i<9;i++)di=Math.Max(di,Math.Abs(f.I[i]-best.I[i]));
        Console.WriteLine($"FACE ref={f.Face} act={best.Face} class={f.Class} area={f.Area:R}/{best.Area:R} dA={best.Area-f.Area:E3} dC={D(f.C,best.C):E3} dI={di:E3} loops={f.Loops}/{best.Loops}");
    }
}
return 0;
static double D(double[] x,double[] y)=>Math.Sqrt((x[0]-y[0])*(x[0]-y[0])+(x[1]-y[1])*(x[1]-y[1])+(x[2]-y[2])*(x[2]-y[2]));
static unsafe int Load(string path)
{
    var bytes=File.ReadAllBytes(path);
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        C(PK_PART_receive_b(block,&o,&n,&parts));return parts[0];
    }
}
static unsafe double[] Body(string path)
{
    var body=Load(path);var options=new PK_TOPOL_eval_mass_props_o_t();var amount=new double[1];var mass=new double[1];var c=new double[3];var m=new double[9];var per=new double[1];
    C(PK_TOPOL_eval_mass_props(1,new[]{body},.99999999,&options,amount,mass,c,m,per));
    var r=new double[15];r[0]=amount[0];r[1]=mass[0];c.CopyTo(r,2);m.CopyTo(r,5);r[14]=per[0];return r;
}
static unsafe List<Rec> Faces(string path)
{
    var bytes=File.ReadAllBytes(path);int body;
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        C(PK_PART_receive_b(block,&o,&n,&parts));body=parts[0];
    }
    int nf;int* faces;C(PK_BODY_ask_faces(body,&nf,&faces));var list=new List<Rec>();
    for(var i=0;i<nf;i++)
    {
        int surf,sc;C(PK_FACE_ask_surf(faces[i],&surf));C(PK_ENTITY_ask_class(surf,&sc));
        var options=new PK_TOPOL_eval_mass_props_o_t();var amount=new double[1];var mass=new double[1];var c=new double[3];var m=new double[9];var per=new double[1];
        C(PK_TOPOL_eval_mass_props(1,new[]{faces[i]},.99999999999,&options,amount,mass,c,m,per));
        int nl;int* loops;C(PK_FACE_ask_loops(faces[i],&nl,&loops));
        list.Add(new Rec{Face=faces[i],Class=sc,Area=amount[0],C=c,I=m,Loops=nl});
    }
    return list;
}
static void C(int e){if(e!=0)throw new Exception("PK error "+e);}
class Rec{public int Face,Class,Loops;public double Area;public double[] C=[];public double[] I=[];}
