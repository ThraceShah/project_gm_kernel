#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=2||Path.IsPathRooted(args[1]))throw new ArgumentException("new output name and project-relative native XT input");
var output=Path.Combine(root,"temp_docs","native-acceptance-contract",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Existing evidence is immutable");
Directory.CreateDirectory(output);
ParasolidMassProperties.BeginEvidence(output);
if(!ParasolidScriptHost.TryStartSession("native acceptance contract",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    double precision;Check(PK_SESSION_ask_precision(&precision));
    Console.WriteLine($"PRECISION {precision:R}");
    ProbePrimitive(precision);
    Require(ParasolidMassProperties.Classify(1,0,1,0,out _,out _)==MassAgreement.Verified,"exact numeric control");
    Require(ParasolidMassProperties.Classify(1,100,2,100,out _,out _)==MassAgreement.Unresolved,"overlapping broad bounds must not verify equality");
    Require(ParasolidMassProperties.Classify(1,0,2,0,out _,out _)==MassAgreement.Different,"separated numeric control");
    PK_BODY_t correct,wrong;Check(PK_BODY_create_solid_block(2,2,2,null,&correct));Check(PK_BODY_create_solid_block(2.1,2,2,null,&wrong));
    try
    {
        Require(ParasolidMassProperties.Compare(correct,wrong,"wrong-size-control")==MassAgreement.Different,"wrong size must fail mass agreement");
        var correctReload=Receive(Transmit(correct));var wrongReload=Receive(Transmit(wrong));
        try
        {
            var p=new PK_VECTOR_t(1.025,0,1);PK_TOPOL_t owner;Span<PK_enclosure_t> values=stackalloc PK_enclosure_t[4];
            fixed(PK_enclosure_t* v=values)
            {
                Check(PK_BODY_contains_vector(correct,p,v,&owner));Check(PK_BODY_contains_vector(correctReload,p,v+1,&owner));
                Check(PK_BODY_contains_vector(wrong,p,v+2,&owner));Check(PK_BODY_contains_vector(wrongReload,p,v+3,&owner));
            }
            Require(values[0]==PK_enclosure_outside_c&&values[2]==PK_enclosure_inside_c,"wrong-size material negative is separated");
            Require(ParasolidProbeAgreement.Classify(values)==ProbeAgreement.Different,"definite material flip must fail");
            values[2]=PK_enclosure_on_c;values[3]=PK_enclosure_on_c;
            Require(ParasolidProbeAgreement.Classify(values)==ProbeAgreement.Unresolved,"On disagreement must remain pending");
            values[2]=-157;Require(ParasolidProbeAgreement.Classify(values)==ProbeAgreement.Different,"API failure must fail");
            Console.WriteLine("PROBE_CONTROLS definite_material_flip=Different On_disagreement=Unresolved API_error=Different");
        }
        finally{Check(PK_ENTITY_delete(1,&wrongReload));Check(PK_ENTITY_delete(1,&correctReload));}
    }
    finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    Check(PK_BODY_create_solid_sphere(1,null,&correct));Check(PK_BODY_create_solid_sphere(1.01,null,&wrong));
    try{RejectGeometry(correct,wrong,output,"native-wrong-radius","body geometry");}
    finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    var line=new PK_LINE_sf_t(new PK_AXIS1_sf_t(new(0,0,0),new(1,0,0)));PK_CURVE_t carrier;
    Check(PK_LINE_create(&line,&carrier));Check(PK_CURVE_make_wire_body(carrier,new(0,1),&correct));
    Check(PK_CURVE_make_wire_body(carrier,new(0,1.01),&wrong));
    try{RejectGeometry(correct,wrong,output,"native-wrong-finite-trim","unmatched wire edge");}
    finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    var bytes=File.ReadAllBytes(Path.Combine(root,args[1]));Save(output,"input.x_t",bytes);
    for(var axis=0;axis<3;axis++)
    {
        var reference=Receive(bytes);var split=Receive(bytes);PK_PLANE_t plane=0;
        try
        {
            Full(reference);Full(split);var supports=Supports(split);var topology=Counts(split);
            PK_BOX_t box;Check(PK_TOPOL_find_box(split,&box));var frame=new PK_AXIS2_sf_t();
            for(var d=0;d<3;d++)frame.location.coord[d]=box.coord[d]*.5+box.coord[d+3]*.5;
            frame.axis.coord[axis]=1;frame.ref_direction.coord[(axis+1)%3]=1;
            var definition=new PK_PLANE_sf_t(frame);Check(PK_PLANE_create(&definition,&plane));
            int count;int* edges;var error=PK_BODY_imprint_plane(split,plane,precision,&count,&edges);
            Console.WriteLine($"IMPRINT axis={axis} error={error} edges={count} point=({frame.location.coord[0]:R},{frame.location.coord[1]:R},{frame.location.coord[2]:R})");
            if(edges!=null)Check(PK_MEMORY_free(edges));Check(error);Require(count>0,"imprint produced no partition");
            Full(split);var after=Counts(split);
            Require(after.Regions==topology.Regions&&after.Shells==topology.Shells&&after.Faces>topology.Faces,"partition topology");
            var result=Transmit(split);Save(output,$"partition-{axis}.x_t",result);
            foreach(var support in Supports(split))
            {
                var found=false;
                foreach(var original in supports)
                {
                    PK_LOGICAL_t coincident;Check(PK_GEOM_is_coincident(support.Surface,original.Surface,&coincident));
                    if(coincident==PK_LOGICAL_true&&support.Sense==original.Sense){found=true;break;}
                }
                Console.WriteLine($"SUPPORT surface={support.Surface} positive={support.Sense==PK_LOGICAL_true} matched={found}");
                Require(found,"imprint changed a supporting surface or orientation");
            }
            Console.WriteLine($"PARTITION regions={topology.Regions}/{after.Regions} shells={topology.Shells}/{after.Shells} faces={topology.Faces}/{after.Faces} original_oriented_supports=True");
            var fresh=Receive(result);
            try{Full(fresh);Mass(reference,fresh,$"partition-{axis}");ParasolidMassProperties.Compare(reference,fresh,$"partition-{axis}");}
            finally{Check(PK_ENTITY_delete(1,&fresh));}
        }
        finally{if(plane!=0)Check(PK_ENTITY_delete(1,&plane));Check(PK_ENTITY_delete(1,&split));Check(PK_ENTITY_delete(1,&reference));}
    }
}

static string Source([CallerFilePath]string path="")=>path;
static void Check(int error)=>ParasolidScriptHost.Check(error,"native contract");
static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
static void Save(string output,string name,byte[] bytes)
{
    File.WriteAllBytes(Path.Combine(output,name),bytes);
    var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));Console.WriteLine($"ARTIFACT {name} sha256={hash}");
    File.AppendAllText(Path.Combine(output,"artifacts.sha256"),$"{hash}  {name}\n");
}
static unsafe PK_BODY_t Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};
        int count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts));
        try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts));}
    }
}
static unsafe byte[] Transmit(PK_BODY_t body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block));
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return bytes.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block));}
}
static unsafe void Full(PK_BODY_t body)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;
    Check(PK_BODY_check(body,&options,&count,&faults));
    try{Console.WriteLine($"FULL faults={count}");Require(count==0,"full BODY_check");}finally{if(faults!=null)Check(PK_MEMORY_free(faults));}
}
static unsafe void RejectGeometry(PK_BODY_t reference,PK_BODY_t wrong,string output,string label,string expectedReason)
{
    Full(reference);Full(wrong);Save(output,label+"-reference.x_t",Transmit(reference));var bytes=Transmit(wrong);
    Save(output,label+".x_t",bytes);var received=Receive(bytes);
    try
    {
        Full(received);var rejected=false;
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,label);}
        catch(InvalidOperationException error)when(error.Message.Contains(expectedReason,StringComparison.Ordinal)){rejected=true;}
        Require(rejected,label+" must fail geometry independently of mass uncertainty");
        Console.WriteLine($"GEOMETRY_NEGATIVE {label} rejected=True");
    }
    finally{Check(PK_ENTITY_delete(1,&received));}
}
static unsafe (int Regions,int Shells,int Faces) Counts(PK_BODY_t body)
{
    int regions,shells,faces;PK_TOPOL_t* p;Check(PK_BODY_ask_regions(body,&regions,&p));Check(PK_MEMORY_free(p));
    Check(PK_BODY_ask_shells(body,&shells,&p));Check(PK_MEMORY_free(p));Check(PK_BODY_ask_faces(body,&faces,&p));Check(PK_MEMORY_free(p));
    return(regions,shells,faces);
}
static unsafe HashSet<(PK_SURF_t Surface,PK_LOGICAL_t Sense)> Supports(PK_BODY_t body)
{
    int n;PK_FACE_t* faces;Check(PK_BODY_ask_faces(body,&n,&faces));var result=new HashSet<(PK_SURF_t,PK_LOGICAL_t)>();
    try{for(var i=0;i<n;i++){PK_SURF_t surface;PK_LOGICAL_t sense;Check(PK_FACE_ask_oriented_surf(faces[i],&surface,&sense));result.Add((surface,sense));}return result;}
    finally{Check(PK_MEMORY_free(faces));}
}
static unsafe void Mass(PK_BODY_t reference,PK_BODY_t actual,string label)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    foreach(var accuracy in new[]{.99999999,.99999999999})
    {
        Check(PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+1,a+2,a+5,a+14));
        Check(PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+1,b+2,b+5,b+14));
        var worst=0d;var component=0;
        for(var i=0;i<15;i++){var residual=Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i]));Require(double.IsFinite(residual),"finite mass");if(residual>worst){worst=residual;component=i;}}
        Console.WriteLine($"MASS {label} accuracy={accuracy:R} old_point_residual={worst:R} component={component} values={a[component]:R}/{b[component]:R} old_point_rejects={worst>=1e-7}");
    }
}
static unsafe void ProbePrimitive(double precision)
{
    PK_BODY_t body;Check(PK_BODY_create_solid_block(2,2,2,null,&body));
    try
    {
        int n;PK_VERTEX_t* vertices;Check(PK_BODY_ask_vertices(body,&n,&vertices));var vertex=vertices[0];Check(PK_MEMORY_free(vertices));
        double radius;Check(PK_VERTEX_ask_precision(vertex,&radius));PK_POINT_t point;PK_POINT_sf_t geometry;
        Check(PK_VERTEX_ask_point(vertex,&point));Check(PK_POINT_ask(point,&geometry));
        Console.WriteLine($"VERTEX precision={radius:R} point=({geometry.position.coord[0]:R},{geometry.position.coord[1]:R},{geometry.position.coord[2]:R})");
        PK_BOX_t box;Check(PK_TOPOL_find_box(body,&box));var sign=geometry.position.coord[2]>(box.coord[2]+box.coord[5])*.5?1:-1;
        foreach(var fraction in new[]{0d,.25,.49,.51,.99,1.01,2})
        {
            var p=geometry.position;p.coord[2]+=sign*precision*fraction;PK_enclosure_t enclosure;PK_TOPOL_t topology;PK_CLASS_t type;
            Check(PK_BODY_contains_vector(body,p,&enclosure,&topology));Check(PK_ENTITY_ask_class(topology,&type));
            Console.WriteLine($"VERTEX_PROBE offset={precision*fraction:R} enclosure={enclosure} owner_class={type}");
        }
    }
    finally{Check(PK_ENTITY_delete(1,&body));}
}
