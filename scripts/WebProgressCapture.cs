using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using ProjectGmKernel.DisplayBridge;
using ProjectGmKernel.Native.Runtime;
using static parasolid;
using EntityTag=System.Int32;
using BodyTag=System.Int32;

// Opt-in observer only. No modeling results flow from PK back into GM.
// Called outside the measured API interval; writes checkpoints before every API.
internal static unsafe class WebProgressCapture
{
    internal static readonly string? Output=Environment.GetEnvironmentVariable("GM_WEB_CAPTURE");
    internal static bool Select(string label)=>Environment.GetEnvironmentVariable("GM_WEB_SELECT") is not string filter||label==filter;
    internal static bool Enabled=>!string.IsNullOrWhiteSpace(Output);
    private static readonly JsonArray Steps=[];
    private static readonly JsonArray Checks=[];
    private static readonly JsonArray Errors=[];
    private static readonly Dictionary<bool,int> Ordinals=new(){{true,0},{false,0}};
    private static readonly Dictionary<(bool,int),JsonObject> BeforeMeshes=[];
    private static readonly Dictionary<(bool,int),BodyTag[]> AfterBodies=[];
    private static readonly Dictionary<(BodyTag Reference,BodyTag Actual),JsonObject> QualitySources=[];
    private static JsonObject? identity;
    private static readonly Dictionary<(bool,int),HashSet<EntityTag>> Supports=[];
    private static readonly JsonArray Timings=[];
    internal static string Context="";
    internal static JsonObject? ContextSource;
    private static readonly Dictionary<(bool,string),int> ContextCounts=[];
    private static readonly Dictionary<string,int> ContextSteps=[];
    private const double ChordTolerance=.01;
    static WebProgressCapture()
    {
        if(!Enabled)return;
        ParasolidBodyCompare.Observer=e=>Observe(()=>RecordComparison(e));
        ParasolidMassProperties.Observer=e=>Observe(()=>RecordMass(e));
    }
    internal static void Initialize() { }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string CapturePath([CallerFilePath]string path="")=>path;
    private static JsonObject SourceIdentity()
    {
        var first=identity==null;
        var path=Path.GetDirectoryName(CapturePath())!;
        identity??=new JsonObject{["calculationCommit"]=Environment.GetEnvironmentVariable("GM_WEB_COMMIT"),["captureCommit"]=Environment.GetEnvironmentVariable("GM_WEB_CAPTURE_COMMIT"),
            ["captureSourceFileSha256"]=Hash(File.ReadAllBytes(CapturePath())),["bodyCompareSourceFileSha256"]=Hash(File.ReadAllBytes(Path.Combine(path,"ParasolidBodyCompare.cs"))),
            ["massSourceFileSha256"]=Hash(File.ReadAllBytes(Path.Combine(path,"ParasolidMassProperties.cs"))),
            ["loadedFileAppAssemblySha256"]=Hash(File.ReadAllBytes(typeof(WebProgressCapture).Assembly.Location)),["loadedKernelAssemblySha256"]=Hash(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))};
        if(first)
        {
            var directory=Path.Combine(Output!,"identity");Directory.CreateDirectory(directory);
            foreach(var (role,assembly) in new[]{("kernel",typeof(KernelRuntime).Assembly),("oracle",typeof(WebProgressCapture).Assembly)})
            {
                var dll=File.ReadAllBytes(assembly.Location);File.WriteAllBytes(Path.Combine(directory,role+".dll"),dll);
                var pdb=Path.ChangeExtension(assembly.Location,".pdb");
                identity[role+"Mvid"]=assembly.ManifestModule.ModuleVersionId.ToString();
                identity[role+"DllSha256"]=Hash(dll);
                identity[role+"PdbSha256"]=File.Exists(pdb)?Hash(File.ReadAllBytes(pdb)):null;
                if(File.Exists(pdb))File.Copy(pdb,Path.Combine(directory,role+".pdb"));
            }
            File.WriteAllText(Path.Combine(directory,"loaded.json"),identity.ToJsonString());
        }
        if(first&&(string.IsNullOrWhiteSpace(identity["calculationCommit"]?.GetValue<string>())||string.IsNullOrWhiteSpace(identity["captureCommit"]?.GetValue<string>())))
            AddCheck("capture-source-identity","Pinned calculation/capture source identity","missing",null,null,null,"","Both source commits must be supplied; actual loaded assembly and source-file hashes are still retained.");
        return (JsonObject)identity.DeepClone();
    }
    internal static void BindQuality(string stage,BodyTag gm,BodyTag reference,BodyTag fresh,byte[] gmXt,byte[] nativeXt)
    {
        if(!Enabled)return;
        Observe(()=>QualitySources[(reference,fresh)]=new JsonObject{["kind"]="GM-fresh-vs-native",["actualOriginKernel"]="GM",["stage"]=stage,["context"]=Context,
            ["gmLiveBody"]=gm,["referenceNativeBody"]=reference,["freshNativeBody"]=fresh,["gmXtSha256"]=Hash(gmXt),["referenceXtSha256"]=Hash(nativeXt),["identity"]=SourceIdentity()});
    }
    internal static void BindArchivedQuality(string stage,BodyTag reference,BodyTag fresh,byte[] gmXt,byte[] nativeXt)
    {
        if(!Enabled)return;
        Observe(()=>QualitySources[(reference,fresh)]=new JsonObject{["kind"]="GM-archived-XT-vs-native",["actualOriginKernel"]="GM",["stage"]=stage,
            ["referenceNativeBody"]=reference,["freshNativeBody"]=fresh,["gmXtSha256"]=Hash(gmXt),["referenceXtSha256"]=Hash(nativeXt),
            ["sameInstanceDisplayQualified"]=false,["identity"]=SourceIdentity()});
    }
    internal static void BindNativeQuality(string stage,BodyTag reference,BodyTag actual,byte[] referenceXt,byte[] actualXt)
    {
        if(!Enabled)return;
        Observe(()=>QualitySources[(reference,actual)]=new JsonObject{["kind"]="native-control",["actualOriginKernel"]="PK",["stage"]=stage,["referenceNativeBody"]=reference,
            ["actualNativeBody"]=actual,["referenceXtSha256"]=Hash(referenceXt),["actualXtSha256"]=Hash(actualXt),["identity"]=SourceIdentity()});
    }
    private static JsonObject? QualitySource(JsonObject evidence)
    {
        if(!QualitySources.TryGetValue((evidence["referenceBody"]!.GetValue<int>(),evidence["actualBody"]!.GetValue<int>()),out var source))return null;
        var label=evidence["label"]!.GetValue<string>();var stage=source["stage"]!.GetValue<string>();
        return label==stage||label==stage+" strict"?(JsonObject)source.DeepClone():null;
    }
    internal static void Before(bool own,BodyTag body,EntityTag[]? selected=null,string operation="PK_BODY_fix_blends",BodyTag[]? tools=null,bool supportBody=false)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            Directory.CreateDirectory(Output!);
            var ordinal=Ordinals[own]++;var side=own?"gm":"pk";
            {
                var contextKey=(own,Context+":"+operation);var occurrence=ContextCounts.GetValueOrDefault(contextKey);ContextCounts[contextKey]=occurrence+1;
                var logicalKey=Context+":"+operation+":"+occurrence;
                if(!ContextSteps.TryGetValue(logicalKey,out ordinal)){ordinal=Steps.Count;ContextSteps.Add(logicalKey,ordinal);}
                Ordinals[own]=ordinal+1;
            }
            while(Steps.Count<=ordinal)Steps.Add((JsonNode)new JsonObject{["id"]=$"operation-{Steps.Count+1}",["title"]=(Context.Length>0?Context+" / ":"")+operation,["gm"]=new JsonObject{["before"]=null,["after"]=null},["pk"]=new JsonObject{["before"]=null,["after"]=null}});
            Steps[ordinal]!["sourceBinding"]=ContextSource?.DeepClone();
            var support=new HashSet<int>();
            foreach(var edge in selected??[])
            {
                int n=0;int* faces=null;
                Guard(own?KernelRuntime.EdgeAskFins(edge,&n,&faces):PK_EDGE_ask_faces(edge,&n,&faces),"selection incidence");
                try{for(var i=0;i<n;i++){var face=faces[i];if(own)Guard(KernelRuntime.FinAskFace(faces[i],&face),"fin face");support.Add(face);}}finally{Free(own,faces);}
            }
            Supports[(own,ordinal)]=support;
            var mesh=Mesh(own,body,selected??[],support,[],tool:supportBody,isBefore:true);BeforeMeshes[(own,ordinal)]=mesh;
            var path=$"meshes/{side}-{ordinal}-before.json";Write(path,mesh);
            Steps[ordinal]![side]!["before"]=path;
            // Tool snapshots retain their own IDs and are explicit extra input steps.
            var toolPaths=new JsonArray();
            foreach(var tool in tools??[])
            {
                var toolPath=$"meshes/{side}-{ordinal}-tool-{toolPaths.Count}.json";
                Write(toolPath,Mesh(own,tool,[],[],[],true));toolPaths.Add((JsonNode?)JsonValue.Create(toolPath));
            }
            Steps[ordinal]![side]!["tools"]=toolPaths;
            var selection=new JsonArray();foreach(var tag in selected??[])selection.Add((JsonNode?)JsonValue.Create(tag));Steps[ordinal]![side]!["selectedEdgeTags"]=selection;
            Flush();
        });
    }
    internal static void After(bool own,BodyTag body,double ms,int error,int count,EntityTag* blends,int fault,PK_FACE_array_t* unders=null,EntityTag* topols=null)
    {
        if(!Enabled)return;
        var made=error==0&&blends!=null?new ReadOnlySpan<int>(blends,count).ToArray():[];
        Complete(own,error==0?[body]:[],ms,error,fault,made,null);
        Observe(()=>
        {
            var ordinal=Ordinals[own]-1;var records=new JsonArray();
            for(var i=0;i<made.Length;i++)
            {
                var support=new JsonArray();if(unders!=null)for(var j=0;j<unders[i].length;j++)support.Add((JsonNode?)JsonValue.Create(unders[i].array[j]));
                records.Add((JsonNode)new JsonObject{["newBlendFace"]=made[i],["replacedTopol"]=topols==null?null:JsonValue.Create(topols[i]),["underlyingEntities"]=support});
            }
            Steps[ordinal]![own?"gm":"pk"]!["blendTracking"]=records;
            AddCheck($"{ordinal}-{own}-blend-fault","Blend fault "+(own?"GM":"PK"),fault==PK_blend_fault_no_fault_c?"pass":"fail",own?fault:null,own?null:fault,PK_blend_fault_no_fault_c,"enum","PK_blend_fault_no_fault_c required");Flush();
        });
    }
    internal static void BooleanAfter(bool own,BodyTag[] bodies,double ms,int error,int status,PK_TOPOL_track_r_t* tracking)
        =>Complete(own,bodies,ms,error,status,[],tracking);
    private static void Complete(bool own,BodyTag[] bodies,double ms,int error,int status,EntityTag[] blends,PK_TOPOL_track_r_t* tracking)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            var ordinal=Ordinals[own]-1;var side=own?"gm":"pk";
            Timings.Add((JsonNode)new JsonObject{["step"]=ordinal,["kernel"]=side,["ms"]=ms,["error"]=error,["resultStatus"]=status});
            AddCheck($"{side}-{ordinal}-api",side+" API error",error==0?"pass":"fail",own?error:null,own?null:error,0,"code","API error, zero means success");
            var tracks=new JsonArray();var products=new HashSet<int>();var deleted=new HashSet<int>();
            if(tracking!=null)for(var i=0;i<tracking->n_track_records;i++)
            {
                var r=tracking->track_records[i];var orig=new JsonArray();var prod=new JsonArray();
                for(var j=0;j<r.n_original_topols;j++)
                {
                    orig.Add((JsonNode)new JsonObject{["id"]=r.original_topols[j],["class"]=r.original_classes[j]});
                    if(r.track==PK_TOPOL_track_delete_c)deleted.Add(r.original_topols[j]);
                }
                for(var j=0;j<r.n_product_topols;j++){prod.Add((JsonNode?)JsonValue.Create(r.product_topols[j]));products.Add(r.product_topols[j]);}
                tracks.Add((JsonNode)new JsonObject{["event"]=r.track,["originals"]=orig,["products"]=prod});
            }
            Steps[ordinal]![side]!["tracking"]=tracks;
            var trackingRequested=Steps[ordinal]![side]?["apiParameters"]?["tracking"]?.GetValue<bool>();
            Steps[ordinal]![side]!["semanticCoverage"]=new JsonObject{["status"]=blends.Length>0||tracking!=null&&trackingRequested!=false?"pass":"missing",["note"]=blends.Length>0?"New blend faces from API outputs; support incidence and replaced IDs retained.":tracking!=null&&trackingRequested!=false?"Original/product generic topology tracking copied before free.":"This oracle path returns no generic tracking. Exact ID deletion is known; other modification roles remain unknown, shown neutrally."};
            var afters=new JsonArray();var allIds=new HashSet<int>();
            for(var b=0;b<bodies.Length;b++)
            {
                var support=Supports.GetValueOrDefault((own,ordinal))??[];
                var mesh=Mesh(own,bodies[b],[],products.Count>0?products:support,blends,roleKnown:blends.Length>0||tracking!=null&&trackingRequested!=false);
                foreach(var item in mesh["faces"]!.AsArray().Concat(mesh["edges"]!.AsArray()))allIds.Add(item!["entityTag"]!.GetValue<int>());
                var path=$"meshes/{side}-{ordinal}-after-{b}.json";Write(path,mesh);afters.Add((JsonNode?)JsonValue.Create(path));
            }
            Steps[ordinal]![side]!["after"]=afters.Count>0?afters[0]!.DeepClone():null;
            Steps[ordinal]![side]!["afterBodies"]=afters;
            if(BeforeMeshes.TryGetValue((own,ordinal),out var before))
            {
                foreach(var item in before["faces"]!.AsArray().Concat(before["edges"]!.AsArray()))
                {
                    var tag=item!["entityTag"]!.GetValue<int>();
                    if(error==0&&(!allIds.Contains(tag)||deleted.Contains(tag)))
                    {item["inputRole"]=item["role"]!.DeepClone();item["deleted"]=true;if(item["role"]!.GetValue<string>()!="selected")item["role"]="deleted";}
                }
                Write($"meshes/{side}-{ordinal}-before.json",before);
            }
            if(error==0)
            {
                // Later modeling steps may delete/modify both producers' bodies.
                // Retain independent PK snapshot copies for deferred paired checks.
                var immutable=new int[bodies.Length];
                for(var i=0;i<bodies.Length;i++)
                {
                    var copy=0;
                    if(own){using var snapshot=ParasolidDisplayBridge.Create(bodies[i],ChordTolerance);Guard(PK_ENTITY_copy(snapshot.ParasolidBodyTag,&copy),"retain GM fresh XT snapshot");}
                    else Guard(PK_ENTITY_copy(bodies[i],&copy),"retain PK snapshot");
                    immutable[i]=copy;
                }
                AfterBodies[(own,ordinal)]=immutable;
            }
            if(AfterBodies.ContainsKey((true,ordinal))&&AfterBodies.ContainsKey((false,ordinal)))
            {
                try{Compare(ordinal);}
                finally{foreach(var sideOwn in new[]{true,false}){foreach(var saved in AfterBodies[(sideOwn,ordinal)]){var tag=saved;PK_ENTITY_delete(1,&tag);}AfterBodies.Remove((sideOwn,ordinal));}}
            }
            Flush();
        });
    }
    private static JsonObject Mesh(bool own,BodyTag body,EntityTag[] selected,IEnumerable<EntityTag> modified,EntityTag[] blends,bool tool=false,bool isBefore=false,bool roleKnown=true)
    {
        using var display=own?ParasolidDisplayBridge.Create(body,ChordTolerance):ParasolidDisplayBridge.CreateParasolidSnapshot(body,ChordTolerance);
        var selectedSet=selected.ToHashSet();var changed=modified.ToHashSet();var newBlends=blends.ToHashSet();
        string Role(DisplayEntity e)=>newBlends.Contains(e.EntityTag)?"newBlend":selectedSet.Contains(e.EntityTag)?"selected":changed.Contains(e.EntityTag)?(isBefore?"support":"modified"):tool?"support":"unchanged";
        // Caller supplies support incidence before and exact tracking products after.
        var faces=new JsonArray();var edges=new JsonArray();
        foreach(var group in display.Triangles.GroupBy(t=>t.Face.EntityTag))
        {
            var positions=new JsonArray();var normals=new JsonArray();var indices=new JsonArray();var index=0;
            foreach(var t in group){Vector(positions,t.A);Vector(positions,t.B);Vector(positions,t.C);Vector(normals,t.NormalA);Vector(normals,t.NormalB);Vector(normals,t.NormalC);indices.Add((JsonNode?)JsonValue.Create(index++));indices.Add((JsonNode?)JsonValue.Create(index++));indices.Add((JsonNode?)JsonValue.Create(index++));}
            var entity=group.First().Face;
            faces.Add((JsonNode)new JsonObject{["id"]=Id(own,entity),["entityTag"]=entity.EntityTag,["identifier"]=entity.Identifier,["positions"]=positions,["normals"]=normals,["indices"]=indices,["role"]=Role(entity),["roleKnown"]=isBefore||tool||Role(entity)!="unchanged"&&roleKnown});
        }
        foreach(var group in display.EdgeSegments.GroupBy(t=>t.Edge.EntityTag))
        {
            var positions=new JsonArray();var first=true;Vector3 previous=default;
            foreach(var segment in group){if(first||segment.A!=previous)Vector(positions,segment.A);Vector(positions,segment.B);previous=segment.B;first=false;}
            var entity=group.First().Edge;edges.Add((JsonNode)new JsonObject{["id"]=Id(own,entity),["entityTag"]=entity.EntityTag,["identifier"]=entity.Identifier,["positions"]=positions,["role"]=Role(entity),["roleKnown"]=isBefore||tool||Role(entity)!="unchanged"&&roleKnown});
        }
        var bytes=own?System.Text.Encoding.UTF8.GetBytes(display.XtText):NativeSnapshotBytes(body);var hash=Hash(bytes);var xtPath=$"evidence/source-{hash}.x_t";
        var full=Path.Combine(Output!,xtPath);Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if(!File.Exists(full))File.WriteAllBytes(full,bytes);else if(Hash(File.ReadAllBytes(full))!=hash)throw new InvalidDataException("immutable source XT changed");
        return new JsonObject{["schemaVersion"]=1,["bodyId"]=(own?"gm:":"pk:")+body,["source"]=new JsonObject{["kernel"]=own?"GM":"PK",
            ["geometryAuthor"]=ContextSource?["kind"]?.GetValue<string>() switch
            {
                "same-byte-fork"=>"Validated GM upstream product and tool XT",
                { } kind=>kind,
                _=>Environment.GetEnvironmentVariable("GM_WEB_GEOMETRY_AUTHOR")??"Unspecified input provenance"
            },
            ["tessellator"]="Parasolid PK_TOPOL_facet / PK_GEOM_render_line",["commit"]=Environment.GetEnvironmentVariable("GM_WEB_COMMIT"),["chordTolerance"]=ChordTolerance,["freshXtReceive"]=own,
            ["sourceBodyTag"]=body,["snapshotNativeBodyTag"]=display.ParasolidBodyTag,["xtSha256"]=hash,["xtArtifact"]=xtPath,["contextSource"]=ContextSource?.DeepClone(),["captureIdentity"]=SourceIdentity()},["faces"]=faces,["edges"]=edges};
    }
    internal static byte[] NativeSnapshotBytes(BodyTag body)
    {
        var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
        Guard(PK_PART_transmit_b(1,&body,&options,&block),"native source XT snapshot");
        try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
        finally{Guard(PK_MEMORY_block_f(&block),"native source snapshot free");}
    }
    private static void Compare(int ordinal)
    {
        var a=AfterBodies[(true,ordinal)];var b=AfterBodies[(false,ordinal)];
        AddCheck($"{ordinal}-body-count","Result body count",a.Length==b.Length?"pass":"fail",a.Length,b.Length,0,"count","");
        if(a.Length!=1||b.Length!=1)
        {AddCheck($"{ordinal}-correspondence","Multi-body correspondence","missing",null,null,null,"","No guessed body pairing; oracle log retains its own matching checks.");return;}
        var received=a[0];var reference=b[0];
        foreach(var pair in new[]{(Body:received,Own:true),(Body:reference,Own:false)})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Guard(PK_BODY_check(pair.Body,&options,&n,&faults),"BODY_check");
            var detail=new JsonArray();try{for(var i=0;i<n;i++)detail.Add((JsonNode)new JsonObject{["state"]=faults[i].state,["entity1"]=faults[i].entity_1,["entity2"]=faults[i].entity_2});}finally{Free(false,faults);}
            AddCheck($"{ordinal}-{pair.Own}-body-check","Full BODY_check "+(pair.Own?"GM fresh XT":"PK"),n==0?"pass":"fail",pair.Own?n:null,pair.Own?null:n,0,"faults",detail.ToJsonString());
        }
        for(var kind=0;kind<5;kind++)
        {var x=Count(received,kind);var y=Count(reference,kind);AddCheck($"{ordinal}-count-{kind}",new[]{"Regions","Shells","Faces","Edges","Vertices"}[kind],x==y?"pass":"fail",x,y,0,"count","");}
        QualitySources[(reference,received)]=new JsonObject{["kind"]="same-instance-snapshot-pair",["stage"]="web strict fresh XT",["step"]=ordinal,["context"]=Context,
            ["gmMesh"]=Steps[ordinal]!["gm"]!["after"]?.DeepClone(),["pkMesh"]=Steps[ordinal]!["pk"]!["after"]?.DeepClone(),["identity"]=SourceIdentity()};
        // This single current helper call publishes its own mask, differences
        // and conditional proofs. Mass comes only from the oracle's actual
        // bounded comparison; observation never runs another integration.
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,"web strict fresh XT");}
        catch(Exception error){AddCheck("snapshot-rejected-"+ordinal,"Same-instance snapshot comparison","fail",null,null,null,"",error.Message);Flush();}
    }
    private static int Count(BodyTag body,int kind)
    {int n=0;int* tags=null;Guard(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"topology counts");Free(false,tags);return n;}
    private static void AddCheck(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note)
        =>Checks.Add((JsonNode)new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=Finite(gm),["pk"]=Finite(pk),["absDelta"]=gm.HasValue&&pk.HasValue?Finite(Math.Abs(gm.Value-pk.Value)):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Finite(Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value))):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=null,["note"]=note});
    private static void Flush()
    {
        var version=new PK_SESSION_kernel_version_t();Guard(PK_SESSION_ask_kernel_version(&version),"PK version");double precision=0;Guard(PK_SESSION_ask_precision(&precision),"PK precision");
        Write("capture.json",new JsonObject{["steps"]=Steps.DeepClone(),["checks"]=Checks.DeepClone(),["timings"]=Timings.DeepClone(),["captureErrors"]=Errors.DeepClone(),["pkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}",["sessionPrecision"]=precision});
    }
    private static void Observe(Action action)
    {try{action();}catch(Exception e){Errors.Add((JsonNode?)JsonValue.Create(e.ToString()));Console.Error.WriteLine("WEB_EXPORT_ERROR "+e);Directory.CreateDirectory(Output!);File.WriteAllText(Path.Combine(Output!,"capture-errors.json"),Sanitize(Errors.ToJsonString()));
        AddCheck("capture-error-"+Errors.Count,"Snapshot or comparison could not complete","fail",null,null,null,"",e.GetType().Name+": "+e.Message);
        try{Flush();}catch(Exception io){File.WriteAllText(Path.Combine(Output!,"export-errors.json"),io.ToString());}}}
    private static void Write(string path,JsonNode node)
    {var file=Path.Combine(Output!,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file+".tmp",Sanitize(node.ToJsonString()));File.Move(file+".tmp",file,true);}
    private static string Sanitize(string value)
    {
        var root=Environment.GetEnvironmentVariable("GM_WEB_ROOT");if(!string.IsNullOrEmpty(root))value=value.Replace(root,".");
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);return string.IsNullOrEmpty(home)?value:value.Replace(home,"~");
    }
    private static void Vector(JsonArray array,Vector3 v){array.Add((JsonNode?)JsonValue.Create(v.X));array.Add((JsonNode?)JsonValue.Create(v.Y));array.Add((JsonNode?)JsonValue.Create(v.Z));}
    private static string Id(bool own,DisplayEntity entity)=>(own?"gm:":"pk:")+entity.Kind.ToString().ToLowerInvariant()+":"+entity.EntityTag;
    private static JsonNode? Finite(double? value)=>value.HasValue&&double.IsFinite(value.Value)?JsonValue.Create(value.Value):null;
    internal static void BlendParameters(bool own,PK_BODY_fix_blends_o_t fix,double radius,double markTolerance,string markApi="PK_EDGE_set_blend_constant",double? secondRange=null,EntityTag? designatedFace=null)
    {
        if(!Enabled)return;
        Observe(()=>{Steps[Ordinals[own]-1]![own?"gm":"pk"]!["apiParameters"]=new JsonObject{["mark"]=new JsonObject{["name"]=markApi,["radius"]=markApi=="PK_EDGE_set_blend_constant"?JsonValue.Create(radius):null,["range0"]=markApi=="PK_EDGE_set_blend_constant"?null:JsonValue.Create(radius),["range1"]=secondRange,["designatedFace"]=designatedFace,["tolerance"]=markTolerance},["operation"]=new JsonObject{["name"]="PK_BODY_fix_blends",["o_t_version"]=fix.o_t_version,["update"]=fix.update,["tolerance"]=fix.tolerance,["local_check"]=fix.local_check==PK_LOGICAL_true},["otherOptions"]="Unmodified defaults of the pinned PskernelSharp option constructor; see source hash and binding revision."};Flush();});
    }
    internal static void BooleanParameters(bool own,PK_BODY_boolean_o_t options)
    {
        if(!Enabled)return;
        Observe(()=>{Steps[Ordinals[own]-1]![own?"gm":"pk"]!["apiParameters"]=new JsonObject{["o_t_version"]=options.o_t_version,["function"]=options.function,["default_tol"]=options.default_tol,["max_tol"]=options.max_tol,["tracking"]=options.tracking==PK_LOGICAL_true,["tracking_type"]=options.tracking_type,["track_regions"]=options.track_regions,["merge_imprinted"]=options.merge_imprinted==PK_LOGICAL_true,["allow_disjoint"]=options.allow_disjoint==PK_LOGICAL_true,["fence"]=options.fence,["target_material_side"]=options.target_material_side,["tool_material_side"]=options.tool_material_side,["update"]=options.update,["otherOptions"]="Unmodified defaults of the pinned PskernelSharp option constructor."};Flush();});
    }
    internal static void BeforeSurfaces(bool own,EntityTag first,EntityTag second)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            var bodies=new int[2];
            try
            {
                var inputs=new[]{first,second};
                for(var i=0;i<2;i++)
                {
                    int copy=0,body=0;
                    Guard(own?KernelRuntime.EntityCopy(inputs[i],&copy):PK_ENTITY_copy(inputs[i],&copy),"copy display input support");
                    var box=new PK_UVBOX_t();box.param[0]=box.param[1]=-1;box.param[2]=box.param[3]=1;
                    Guard(own?KernelRuntime.SurfMakeSheetBody(copy,*(ProjectGmKernel.Native.Generated.PK_UVBOX_s*)&box,&body):PK_SURF_make_sheet_body(copy,box,&body),"bounded input support display");
                    bodies[i]=body;
                }
                Before(own,bodies[0],[],"PK_SURF_create_blend",[bodies[1]],true);
                Steps[Ordinals[own]-1]![own?"gm":"pk"]!["inputDisplayBounds"]=new JsonObject{["uv"]=new JsonArray(-1,-1,1,1),["note"]="Real auxiliary sheet copies of the input supports, clipped for finite visualization only; original surface API inputs are not modified."};Flush();
            }
            finally{foreach(var item in bodies)if(item!=0){var body=item;if(own)KernelRuntime.EntityDelete(1,&body);else PK_ENTITY_delete(1,&body);}}
        });
    }
    internal static void SurfaceAfter(bool own,BodyTag body,double ms)
    {
        if(!Enabled)return;int count=0;int* faces=null;
        Guard(own?KernelRuntime.BodyAskFaces(body,&count,&faces):PK_BODY_ask_faces(body,&count,&faces),"output sheet face IDs");
        try{Complete(own,[body],ms,0,0,new ReadOnlySpan<int>(faces,count).ToArray(),null);}finally{Free(own,faces);}
    }
    internal static void RecordBodyCheck(string stage,BodyTag body,int count,PK_check_fault_t* faults)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            var details=new JsonArray();
            for(var i=0;i<count;i++)
            {
                var cls=0;PK_ENTITY_ask_class(faults[i].entity_1,&cls);var support=0;var supportClass=0;
                if(cls==PK_CLASS_face&&PK_FACE_ask_surf(faults[i].entity_1,&support)==0)PK_ENTITY_ask_class(support,&supportClass);
                details.Add((JsonNode)new JsonObject{["state"]=faults[i].state,["entity1"]=faults[i].entity_1,["entity2"]=faults[i].entity_2,["entityClass"]=cls,["support"]=support,["supportClass"]=supportClass});
            }
            AddCheck("original-body-"+Checks.Count,stage+" full BODY_check",count==0?"pass":"fail",null,null,0,"faults",details.ToJsonString());
            Checks[^1]!["stage"]=stage;Checks[^1]!["evaluatedKernel"]="PK";Checks[^1]!["nativeBody"]=body;
            Checks[^1]!["captureIdentity"]=SourceIdentity();Checks[^1]!["faultCount"]=count;Checks[^1]!["faults"]=details;Flush();
        });
    }
    private static void RecordComparison(JsonObject evidence)
    {
        var source=QualitySource(evidence);var label=evidence["label"]!.GetValue<string>();var path=$"evidence/quality-compare-{Checks.Count}.json";
        evidence["sourceBinding"]=source;evidence["captureIdentity"]=SourceIdentity();
        evidence["adjudicationPolicy"]=new JsonObject{["diagnostic"]="n_cht_pts",["curveSense"]="Only the complete current finite analytic direction/trim/endpoint/two-fin proof can establish equivalence; raw differences remain.",
            ["otherDifferences"]="Hard conditions; see exact mask and helper result."};
        Write(path,evidence);
        AddCheck("quality-compare-"+Checks.Count,label+" / current AssertEquivalent",evidence["status"]?.GetValue<string>()=="Verified"?"pass":"fail",null,null,null,"",evidence["error"]?.GetValue<string>()??"Same-call raw native mask, differences and each conditional finite proof retained.");
        Checks[^1]!["evidence"]=path;Checks[^1]!["sourceBinding"]=source?.DeepClone();
        if(source==null)AddCheck("quality-source-"+Checks.Count,label+" source binding","missing",null,null,null,"","Actual native pair is recorded; no GM live/source binding was supplied.");
        Flush();
    }
    private static void RecordMass(JsonObject evidence)
    {
        var source=QualitySource(evidence);var label=evidence["label"]!.GetValue<string>();var path=$"evidence/quality-mass-{Checks.Count}.json";
        evidence["sourceBinding"]=source;evidence["captureIdentity"]=SourceIdentity();Write(path,evidence);
        foreach(var item in evidence["properties"]!.AsArray())
        {
            var status=item!["agreementStatus"]!.GetValue<string>();var knownGm=source?["actualOriginKernel"]?.GetValue<string>()=="GM";
            AddCheck("quality-mass-"+Checks.Count,label+" / "+item["property"]!.GetValue<string>(),status=="Verified"?"pass":status=="Different"?"fail":"missing",
                knownGm?item["actualEstimate"]!.GetValue<double>():null,knownGm?item["referenceEstimate"]!.GetValue<double>():null,1e-7,"",status+" from native error moduli; no point-value qualification.");
            var check=Checks[^1]!.AsObject();check["evidence"]=path;check["stage"]=label;check["accuracy"]=evidence["accuracy"]!.DeepClone();
            foreach(var field in new[]{"referenceEstimate","referenceModulus","actualEstimate","actualModulus","normalizedLower","normalizedUpper","agreementStatus"})check[field]=item[field]?.DeepClone();
            check["sourceBinding"]=source?.DeepClone();
        }
        if(evidence["error"]!=null)AddCheck("quality-mass-error-"+Checks.Count,label+" mass observation","fail",null,null,null,"",evidence["error"]!.GetValue<string>());
        if(source==null)AddCheck("quality-source-"+Checks.Count,label+" source binding","missing",null,null,null,"","Actual bounded native pair recorded; no inferred GM source identity.");
        Flush();
    }
    internal static void RecordMaterial(string label,PK_VECTOR_t point,int referenceError,PK_enclosure_t reference,int actualError,PK_enclosure_t actual,string kind,BodyTag referenceBody,BodyTag actualBody)
    {
        if(!Enabled)return;
        var x=point.coord[0];var y=point.coord[1];var z=point.coord[2];
        Observe(()=>
        {
            Span<PK_enclosure_t> values=stackalloc PK_enclosure_t[4]{reference,reference,actual,actual};
            var agreement=referenceError!=0||actualError!=0?ProbeAgreement.Different:ParasolidProbeAgreement.Classify(values);
            AddCheck("quality-material-"+Checks.Count,label,agreement==ProbeAgreement.Verified?"pass":agreement==ProbeAgreement.Different?"fail":"missing",null,null,null,"",agreement+" from actual API errors and classifications.");
            var check=Checks[^1]!.AsObject();check["agreementStatus"]=agreement.ToString();check["referenceError"]=referenceError;check["actualError"]=actualError;
            check["referenceClass"]=reference;check["actualClass"]=actual;check["point"]=new JsonArray(x,y,z);check["sourceKind"]=kind;
            check["referenceBody"]=referenceBody;check["actualBody"]=actualBody;check["captureIdentity"]=SourceIdentity();
            var matches=QualitySources.Values.Where(s=>s["stage"]?.GetValue<string>()==label&&(kind=="native-control"
                ?s["referenceNativeBody"]?.GetValue<int>()==referenceBody&&s["actualNativeBody"]?.GetValue<int>()==actualBody
                :s["gmLiveBody"]?.GetValue<int>()==actualBody&&s["freshNativeBody"]?.GetValue<int>()==referenceBody)).ToArray();
            check["sourceBinding"]=matches.Length==1?matches[0].DeepClone():null;Flush();
        });
    }
    internal static void FreeBlendTracking(bool own,int count,PK_FACE_array_t* unders,EntityTag* topols)
    {
        if(unders!=null){for(var i=0;i<count;i++)Free(own,unders[i].array);Free(own,unders);}Free(own,topols);
    }
    private static void Free(bool own,void* p){if(p!=null)Guard(own?KernelRuntime.MemoryFree(p):PK_MEMORY_free(p),"free observer buffer");}
    private static void Guard(int error,string label){if(error!=0)throw new InvalidDataException($"{label}: {error}");}
}
