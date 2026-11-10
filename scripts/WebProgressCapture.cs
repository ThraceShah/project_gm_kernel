using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Nodes;
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
    private static readonly Dictionary<(bool,int),HashSet<EntityTag>> Supports=[];
    private static readonly JsonArray Timings=[];
    private const double ChordTolerance=.01;
    internal static void Before(bool own,BodyTag body,EntityTag[]? selected=null,string operation="PK_BODY_fix_blends",BodyTag[]? tools=null)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            Directory.CreateDirectory(Output!);
            var ordinal=Ordinals[own]++;var side=own?"gm":"pk";
            while(Steps.Count<=ordinal)Steps.Add((JsonNode)new JsonObject{["id"]=$"operation-{Steps.Count+1}",["title"]=operation,["gm"]=new JsonObject{["before"]=null,["after"]=null},["pk"]=new JsonObject{["before"]=null,["after"]=null}});
            var support=new HashSet<int>();
            foreach(var edge in selected??[])
            {
                int n=0;int* faces=null;
                Guard(own?KernelRuntime.EdgeAskFins(edge,&n,&faces):PK_EDGE_ask_faces(edge,&n,&faces),"selection incidence");
                try{for(var i=0;i<n;i++){var face=faces[i];if(own)Guard(KernelRuntime.FinAskFace(faces[i],&face),"fin face");support.Add(face);}}finally{Free(own,faces);}
            }
            Supports[(own,ordinal)]=support;
            var mesh=Mesh(own,body,selected??[],support,[],isBefore:true);BeforeMeshes[(own,ordinal)]=mesh;
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
            var afters=new JsonArray();var allIds=new HashSet<int>();
            for(var b=0;b<bodies.Length;b++)
            {
                var support=Supports.GetValueOrDefault((own,ordinal))??[];
                var mesh=Mesh(own,bodies[b],[],products.Count>0?products:support,blends);
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
            if(error==0)AfterBodies[(own,ordinal)]=bodies;
            if(AfterBodies.ContainsKey((true,ordinal))&&AfterBodies.ContainsKey((false,ordinal)))Compare(ordinal);
            Flush();
        });
    }
    private static JsonObject Mesh(bool own,BodyTag body,EntityTag[] selected,IEnumerable<EntityTag> modified,EntityTag[] blends,bool tool=false,bool isBefore=false)
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
            faces.Add((JsonNode)new JsonObject{["id"]=Id(own,entity),["entityTag"]=entity.EntityTag,["identifier"]=entity.Identifier,["positions"]=positions,["normals"]=normals,["indices"]=indices,["role"]=Role(entity)});
        }
        foreach(var group in display.EdgeSegments.GroupBy(t=>t.Edge.EntityTag))
        {
            var positions=new JsonArray();var first=true;Vector3 previous=default;
            foreach(var segment in group){if(first||segment.A!=previous)Vector(positions,segment.A);Vector(positions,segment.B);previous=segment.B;first=false;}
            var entity=group.First().Edge;edges.Add((JsonNode)new JsonObject{["id"]=Id(own,entity),["entityTag"]=entity.EntityTag,["identifier"]=entity.Identifier,["positions"]=positions,["role"]=Role(entity)});
        }
        return new JsonObject{["schemaVersion"]=1,["bodyId"]=(own?"gm:":"pk:")+body,["source"]=new JsonObject{["kernel"]=own?"GM":"PK",["geometryAuthor"]=Environment.GetEnvironmentVariable("GM_WEB_GEOMETRY_AUTHOR")??"independent-construction",["tessellator"]="Parasolid PK_TOPOL_facet / PK_GEOM_render_line",["commit"]=Environment.GetEnvironmentVariable("GM_WEB_COMMIT"),["chordTolerance"]=ChordTolerance,["freshXtReceive"]=own},["faces"]=faces,["edges"]=edges};
    }
    private static void Compare(int ordinal)
    {
        var a=AfterBodies[(true,ordinal)];var b=AfterBodies[(false,ordinal)];
        AddCheck($"{ordinal}-body-count","Result body count",a.Length==b.Length?"pass":"fail",a.Length,b.Length,0,"count","");
        if(a.Length!=1||b.Length!=1)
        {AddCheck($"{ordinal}-correspondence","Multi-body correspondence","missing",null,null,null,"","No guessed body pairing; oracle log retains its own matching checks.");return;}
        using var actual=ParasolidDisplayBridge.Create(a[0],ChordTolerance);var received=actual.ParasolidBodyTag;var reference=b[0];
        foreach(var pair in new[]{(Body:received,Own:true),(Body:reference,Own:false)})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Guard(PK_BODY_check(pair.Body,&options,&n,&faults),"BODY_check");
            var detail=new JsonArray();try{for(var i=0;i<n;i++)detail.Add((JsonNode)new JsonObject{["state"]=faults[i].state,["entity1"]=faults[i].entity_1,["entity2"]=faults[i].entity_2});}finally{Free(false,faults);}
            AddCheck($"{ordinal}-{pair.Own}-body-check","Full BODY_check "+(pair.Own?"GM fresh XT":"PK"),n==0?"pass":"fail",pair.Own?n:null,pair.Own?null:n,0,"faults",detail.ToJsonString());
        }
        for(var kind=0;kind<5;kind++)
        {var x=Count(received,kind);var y=Count(reference,kind);AddCheck($"{ordinal}-count-{kind}",new[]{"Regions","Shells","Faces","Edges","Vertices"}[kind],x==y?"pass":"fail",x,y,0,"count","");}
        var mask=new PK_DEBUG_BODY_compare_o_t{max_diffs=1024,all_tests=PK_LOGICAL_false,acc_dev_tests=PK_LOGICAL_false,tol_dev_tests=PK_LOGICAL_true,non_match_tests=PK_LOGICAL_true};PK_DEBUG_BODY_compare_r_t result=default;
        Guard(PK_DEBUG_BODY_compare(reference,received,&mask,&result),"raw compare");
        var diffs=new JsonArray();var globals=new JsonArray();
        try
        {
            for(var i=0;i<result.n_global_diffs;i++){var d=result.global_diffs[i];globals.Add((JsonNode)new JsonObject{["type"]=d.diff,["masters"]=d.n_masters,["similars"]=d.n_similars});}
            for(var f=0;f<result.n_face_pairs;f++)for(var i=0;i<result.face_pairs[f].n_local_diffs;i++)
            {var p=result.face_pairs[f];var d=p.local_diffs[i];diffs.Add((JsonNode)new JsonObject{["masterFace"]=p.master_face,["similarFace"]=p.similar_face,["type"]=d.diff,["masterEntity"]=d.master_entity,["similarEntity"]=d.similar_entity,["masterClass"]=d.master_class,["similarClass"]=d.similar_class,["masterInt"]=d.master_int,["similarInt"]=d.similar_int,["masterDouble"]=Finite(d.master_double),["similarDouble"]=Finite(d.similar_double),["diagnostic"]=d.diff==PK_DEBUG_diff_n_cht_pts_c});}
            Write($"evidence/compare-{ordinal}.json",new JsonObject{["globalResult"]=result.global_result,["localResult"]=result.local_result,["facePairs"]=result.n_face_pairs,["globalDiffs"]=globals,["localDiffs"]=diffs,["mask"]=new JsonObject{["max_diffs"]=mask.max_diffs,["all_tests"]=false,["num_tests"]=mask.num_tests==PK_LOGICAL_true,["acc_dev_tests"]=false,["tol_dev_tests"]=true,["tol_tests"]=mask.tol_tests==PK_LOGICAL_true,["class_tests"]=mask.class_tests==PK_LOGICAL_true,["sense_tests"]=mask.sense_tests==PK_LOGICAL_true,["missing_tests"]=mask.missing_tests==PK_LOGICAL_true,["non_match_tests"]=true},["diagnosticWhitelist"]=new JsonArray("n_cht_pts")});
        }
        finally{PK_DEBUG_BODY_compare_r_f(&result);}
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,"web strict fresh XT");AddCheck($"{ordinal}-equivalence","Type, regions and strict BODY_compare","pass",null,null,null,"","Only n_cht_pts is diagnostic.");}
        catch(Exception e){AddCheck($"{ordinal}-equivalence","Type, regions and strict BODY_compare","fail",null,null,null,"",e.Message);}
        var mass=new PK_TOPOL_eval_mass_props_o_t();double* xMass=stackalloc double[15];double* yMass=stackalloc double[15];
        Guard(PK_TOPOL_eval_mass_props(1,&received,.99999999,&mass,xMass,xMass+1,xMass+2,xMass+5,xMass+14),"GM mass");
        Guard(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&mass,yMass,yMass+1,yMass+2,yMass+5,yMass+14),"PK mass");
        for(var i=0;i<15;i++)
        {var delta=Math.Abs(xMass[i]-yMass[i])/(1+Math.Abs(yMass[i]));AddCheck($"{ordinal}-mass-{i}",new[]{"Volume","Mass","Centroid X","Centroid Y","Centroid Z","Inertia XX","Inertia XY","Inertia XZ","Inertia YX","Inertia YY","Inertia YZ","Inertia ZX","Inertia ZY","Inertia ZZ","Area"}[i],double.IsFinite(delta)&&delta<1e-7?"pass":"fail",xMass[i],yMass[i],1e-7,i<2?"model³":i<5?"model":i<14?"model⁵":"model²","abs(GM-PK)/(1+abs(PK)) < threshold; accuracy=.99999999");}
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
    {try{action();}catch(Exception e){Errors.Add((JsonNode?)JsonValue.Create(e.ToString()));Console.Error.WriteLine("WEB_EXPORT_ERROR "+e);Directory.CreateDirectory(Output!);File.WriteAllText(Path.Combine(Output!,"capture-errors.json"),Errors.ToJsonString());
        AddCheck("capture-error-"+Errors.Count,"Snapshot or comparison could not complete","fail",null,null,null,"",e.GetType().Name+": "+e.Message);
        try{Flush();}catch(Exception io){File.WriteAllText(Path.Combine(Output!,"export-errors.json"),io.ToString());}}}
    private static void Write(string path,JsonNode node)
    {var file=Path.Combine(Output!,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file+".tmp",node.ToJsonString());File.Move(file+".tmp",file,true);}
    private static void Vector(JsonArray array,Vector3 v){array.Add((JsonNode?)JsonValue.Create(v.X));array.Add((JsonNode?)JsonValue.Create(v.Y));array.Add((JsonNode?)JsonValue.Create(v.Z));}
    private static string Id(bool own,DisplayEntity entity)=>(own?"gm:":"pk:")+entity.Kind.ToString().ToLowerInvariant()+":"+entity.EntityTag;
    private static JsonNode? Finite(double? value)=>value.HasValue&&double.IsFinite(value.Value)?JsonValue.Create(value.Value):null;
    internal static void FreeBlendTracking(bool own,int count,PK_FACE_array_t* unders,EntityTag* topols)
    {
        if(unders!=null){for(var i=0;i<count;i++)Free(own,unders[i].array);Free(own,unders);}Free(own,topols);
    }
    private static void Free(bool own,void* p){if(p!=null)Guard(own?KernelRuntime.MemoryFree(p):PK_MEMORY_free(p),"free observer buffer");}
    private static void Guard(int error,string label){if(error!=0)throw new InvalidDataException($"{label}: {error}");}
}
