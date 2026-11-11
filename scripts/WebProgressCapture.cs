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
internal static unsafe partial class WebProgressCapture
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
    internal static string Context="";
    private static readonly Dictionary<(bool,string),int> ContextCounts=[];
    private static readonly Dictionary<string,int> ContextSteps=[];
    private static readonly Dictionary<int,OperationRecord> OperationsByOrdinal=[];
    private static string? ObservationOwner;
    private static string CaseIdentity="";
    private static readonly Dictionary<string,int> CaseOccurrences=[];
    private static readonly Dictionary<string,OperationRecord> RequestedOperations=[];
    private static string LogicalContext(string context,string api)=>CaseIdentity.Length==0?context+":"+api:CaseIdentity+"|"+context+":"+api;
    internal static OperationRecord BeginObservedOperation(string id,string name,string role,string api,string context="")
    {
        if(!Enabled)return OperationAcceptanceLedger.Current.BeginOperation(id,name,role,api);
        var contextKey=LogicalContext(context,api);
        var key=contextKey+":"+ContextCounts.GetValueOrDefault((true,contextKey));
        if(ContextSteps.TryGetValue(key,out var ordinal)&&OperationsByOrdinal.TryGetValue(ordinal,out var paired))return paired;
        var op=OperationAcceptanceLedger.Current.BeginOperation(id,name,role,api);
        RequestedOperations.Add(key,op);return op;
    }
    internal static void Checkpoint(){if(Enabled)Observe(Flush);}
    internal static IDisposable? UseCase(string name)=>Enabled?new CaptureCase(name):null;
    private sealed class CaptureCase : IDisposable
    {
        private readonly string _previous=CaseIdentity;
        private readonly string? _owner=ObservationOwner;
        internal CaptureCase(string name)
        {
            var key=_previous+"/"+name;var occurrence=CaseOccurrences.GetValueOrDefault(key);CaseOccurrences[key]=occurrence+1;
            CaseIdentity=key+"#"+occurrence;ObservationOwner=null;
        }
        public void Dispose(){CaseIdentity=_previous;ObservationOwner=_owner;}
    }
    internal static IDisposable UseContext(string context)=>new CaptureContext(context);
    private sealed class CaptureContext : IDisposable
    {
        private readonly string _previous=Context;
        internal CaptureContext(string context){Context=context;}
        public void Dispose(){Context=_previous;}
    }
    private static void SetParameters(bool own,JsonObject parameters)
    {
        if(!OperationsByOrdinal.TryGetValue(Ordinals[own]-1,out var op))return;
        var pair=op.Options??new JsonObject{["gm"]=null,["pk"]=null};
        pair[own?"gm":"pk"]=parameters.DeepClone();op.Options=pair;
    }
    private const double ChordTolerance=.01;
    private static string SanitizeOpId(string s)=>new string(s.Select(c=>char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'-').ToArray()).Trim('-');
    internal static void Before(bool own,BodyTag body,EntityTag[]? selected=null,string operation="PK_BODY_fix_blends",BodyTag[]? tools=null,bool supportBody=false)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            Directory.CreateDirectory(Output!);
            var ordinal=Ordinals[own]++;var side=own?"gm":"pk";
            var contextKey=(own,LogicalContext(Context,operation));var occurrence=ContextCounts.GetValueOrDefault(contextKey);ContextCounts[contextKey]=occurrence+1;
            var logicalKey=contextKey.Item2+":"+occurrence;
            if(!ContextSteps.TryGetValue(logicalKey,out ordinal)){ordinal=Steps.Count;ContextSteps.Add(logicalKey,ordinal);}
            Ordinals[own]=ordinal+1;

            var opId=Context.Length>0?$"op-{ordinal+1}-{SanitizeOpId(Context)}-{operation.Replace("PK_","").Replace("BODY_","").ToLowerInvariant()}":$"op-{ordinal+1}";
            if(!OperationsByOrdinal.TryGetValue(ordinal,out var opRecord))
            {
                var role=Context.Contains("lifecycle",StringComparison.OrdinalIgnoreCase)||Context.Contains("cut",StringComparison.OrdinalIgnoreCase)?"lifecycle":"primary";
                var prov=new JsonObject{["scope"]="integration-step",["constructionSource"]=Environment.GetEnvironmentVariable("GM_WEB_GEOMETRY_AUTHOR"),["context"]=Context,["caseScope"]=CaseIdentity,["gm"]=null,["pk"]=null,["sameInputEvidence"]=null};
                opRecord=RequestedOperations.Remove(logicalKey,out var requested)?requested:OperationAcceptanceLedger.Current.BeginOperation(opId,(Context.Length>0?Context+" / ":"")+operation,role,operation,null,prov);
                opRecord.InputProvenance=prov;
                opRecord.StepRef=ordinal;
                opRecord.PrimaryContract="capture-body-result-v1";
                OperationsByOrdinal[ordinal]=opRecord;
            }
            ObservationOwner=opRecord.Id;
            var toolRefs=new JsonArray();foreach(var tool in tools??[])toolRefs.Add((JsonNode)new JsonObject{["entityTag"]=tool,["kernel"]=side});
            var input=new JsonObject{["entityTag"]=body,["kernel"]=side,["tools"]=toolRefs,["predecessorOperationId"]=OperationsByOrdinal.Where(p=>p.Key<ordinal&&p.Value.EntityReferences[side]?["outputs"] is JsonArray outputs&&outputs.Any(o=>o?["entityTag"]?.GetValue<int>()==body)).OrderByDescending(p=>p.Key).FirstOrDefault().Value?.Id};
            var provenance=opRecord.InputProvenance!.AsObject();provenance[side]=input.DeepClone();opRecord.InputProvenance=provenance;
            opRecord.EntityReferences[side]=new JsonObject{["input"]=input};

            while(Steps.Count<=ordinal)Steps.Add((JsonNode)new JsonObject{["id"]=$"operation-{Steps.Count+1}",["title"]=(Context.Length>0?Context+" / ":"")+operation,["gm"]=new JsonObject{["before"]=null,["after"]=null},["pk"]=new JsonObject{["before"]=null,["after"]=null}});
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
            AddCheck($"{ordinal}-{own}-blend-fault","Blend fault "+(own?"GM":"PK"),fault==PK_blend_fault_no_fault_c?"pass":"fail",own?fault:null,own?null:fault,PK_blend_fault_no_fault_c,"enum","PK_blend_fault_no_fault_c required",ordinal:ordinal,category:"auxiliary");Flush();
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
            Timings.Add((JsonNode)new JsonObject{["step"]=ordinal,["kernel"]=side,["ms"]=Finite(ms),["invalidMeasurement"]=!double.IsFinite(ms)||ms<=0,["error"]=error,["resultStatus"]=status});
            if(OperationsByOrdinal.TryGetValue(ordinal,out var opRec))
            {
                ObservationOwner=opRec.Id;
                if(own)opRec.GmMs=ms;else opRec.PkMs=ms;
                OperationAcceptanceLedger.Current.RecordReturn(opRec.Id,side,error);
                var outputs=new JsonArray();foreach(var body in bodies)outputs.Add((JsonNode)new JsonObject{["kernel"]=side,["entityTag"]=body});
                opRec.EntityReferences[side]!["outputs"]=outputs;
            }
            AddCheck($"{side}-{ordinal}-api",side+" API error",error==0?"pass":"fail",own?error:null,own?null:error,0,"code","API error, zero means success",ordinal:ordinal,category:"auxiliary");
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
        var mesh=new JsonObject{["schemaVersion"]=1,["bodyId"]=(own?"gm:":"pk:")+body,["source"]=new JsonObject{["kernel"]=own?"GM":"PK",["geometryAuthor"]=Environment.GetEnvironmentVariable("GM_WEB_GEOMETRY_AUTHOR")??"independent-construction",["tessellator"]="Parasolid PK_TOPOL_facet / PK_GEOM_render_line",["commit"]=Environment.GetEnvironmentVariable("GM_WEB_COMMIT"),["chordTolerance"]=ChordTolerance,["freshXtReceive"]=own},["faces"]=faces,["edges"]=edges};
        PrepareDisplayReceipt(own,display,mesh,tool?"tool":isBefore?"before":"after");
        return mesh;
    }
    private static void Compare(int ordinal)
    {
        var a=AfterBodies[(true,ordinal)];var b=AfterBodies[(false,ordinal)];
        AddCheck($"{ordinal}-body-count","Result body count",a.Length==b.Length?"pass":"fail",a.Length,b.Length,0,"count","",ordinal:ordinal,category:"primary");
        if(a.Length!=1||b.Length!=1)
        {AddCheck($"{ordinal}-correspondence","Multi-body correspondence","missing",null,null,null,"","No guessed body pairing; oracle log retains its own matching checks.",ordinal:ordinal,category:"primary");return;}
        var received=a[0];var reference=b[0];
        foreach(var pair in new[]{(Body:received,Own:true),(Body:reference,Own:false)})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Guard(PK_BODY_check(pair.Body,&options,&n,&faults),"BODY_check");
            var detail=new JsonArray();try{for(var i=0;i<n;i++)detail.Add((JsonNode)new JsonObject{["state"]=faults[i].state,["entity1"]=faults[i].entity_1,["entity2"]=faults[i].entity_2});}finally{Free(false,faults);}
            AddCheck($"{ordinal}-{pair.Own}-body-check","Full BODY_check "+(pair.Own?"GM fresh XT":"PK"),n==0?"pass":"fail",pair.Own?n:null,pair.Own?null:n,0,"faults",detail.ToJsonString(),ordinal:ordinal,category:"primary");
        }
        for(var kind=0;kind<5;kind++)
        {var x=Count(received,kind);var y=Count(reference,kind);AddCheck($"{ordinal}-count-{kind}",new[]{"Regions","Shells","Faces","Edges","Vertices"}[kind],x==y?"pass":"fail",x,y,0,"count","",ordinal:ordinal,category:"primary");}
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
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,"web strict fresh XT");AddCheck($"{ordinal}-equivalence","Type, regions and strict BODY_compare","pass",null,null,null,"","Only n_cht_pts is diagnostic.",ordinal:ordinal,category:"primary",evidence:$"evidence/compare-{ordinal}.json");}
        catch(Exception e){AddCheck($"{ordinal}-equivalence","Type, regions and strict BODY_compare","fail",null,null,null,"",e.Message,ordinal:ordinal,category:"primary",evidence:$"evidence/compare-{ordinal}.json");}
        var mass=new PK_TOPOL_eval_mass_props_o_t();double* xMass=stackalloc double[15];double* yMass=stackalloc double[15];
        Guard(PK_TOPOL_eval_mass_props(1,&received,.99999999,&mass,xMass,xMass+1,xMass+2,xMass+5,xMass+14),"GM mass");
        Guard(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&mass,yMass,yMass+1,yMass+2,yMass+5,yMass+14),"PK mass");
        for(var i=0;i<15;i++)
        {var delta=Math.Abs(xMass[i]-yMass[i])/(1+Math.Abs(yMass[i]));AddCheck($"{ordinal}-mass-{i}",new[]{"Volume","Mass","Centroid X","Centroid Y","Centroid Z","Inertia XX","Inertia XY","Inertia XZ","Inertia YX","Inertia YY","Inertia YZ","Inertia ZX","Inertia ZY","Inertia ZZ","Area"}[i],double.IsFinite(delta)&&delta<1e-7?"pass":"fail",xMass[i],yMass[i],1e-7,i<2?"model³":i<5?"model":i<14?"model⁵":"model²","abs(GM-PK)/(1+abs(PK)) < threshold; accuracy=.99999999",ordinal:ordinal,category:"primary");}
    }
    private static int Count(BodyTag body,int kind)
    {int n=0;int* tags=null;Guard(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"topology counts");Free(false,tags);return n;}
    private static void AddCheck(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,int? ordinal=null,string category="primary",string? evidence=null)
    {
        string? owner=null;
        if(ordinal.HasValue&&OperationsByOrdinal.TryGetValue(ordinal.Value,out var op))owner=op.Id;
        else if(int.TryParse(id.Split('-')[0],out var parsedOrdinal)&&OperationsByOrdinal.TryGetValue(parsedOrdinal,out var op2))owner=op2.Id;

        owner??=ObservationOwner;
        // Checks before the first modeled operation belong to the scenario.
        // They are not repeated invocations of a fictitious observer API.
        owner??="scenario";
        var checkObj=OperationAcceptanceLedger.Current.RecordCheck(owner,category,id,label,status,gm,pk,threshold,unit,note,evidence);
        Checks.Add(checkObj.DeepClone());
    }
    private static void Flush()
    {
        var version=new PK_SESSION_kernel_version_t();Guard(PK_SESSION_ask_kernel_version(&version),"PK version");double precision=0;Guard(PK_SESSION_ask_precision(&precision),"PK precision");
        var ops=new JsonArray();
        foreach(var op in OperationAcceptanceLedger.Current.Operations)
        {
            ops.Add((JsonNode)op.ToJsonObject());
        }
        var allChecks=new JsonArray();foreach(var check in OperationAcceptanceLedger.Current.AllChecks)allChecks.Add(check.DeepClone());
        Write("capture.json",new JsonObject{["operationSchemaVersion"]="operation-layered-v2",["checkVersion"]=null,["checkDefinition"]=new JsonObject{["version"]="web-capture-compare-v1",["source"]="scripts/WebProgressCapture.cs",["sourceSha256"]=CaptureSourceHash(),["primaryContract"]="capture-body-result-v1",["massNormalizedThreshold"]=1e-7,["bodyCompareDiagnosticWhitelist"]=new JsonArray("n_cht_pts")},["operations"]=ops,["steps"]=Steps.DeepClone(),["checks"]=allChecks,["timings"]=Timings.DeepClone(),["captureErrors"]=Errors.DeepClone(),["pkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}",["sessionPrecision"]=precision});
    }
    private static string? CaptureSourceHash([System.Runtime.CompilerServices.CallerFilePath]string source="")=>File.Exists(source)?OperationAcceptanceLedger.HashBytes(File.ReadAllBytes(source)):null;
    private static void Observe(Action action)
    {try{action();}catch(Exception e){Errors.Add((JsonNode?)JsonValue.Create(e.ToString()));Console.Error.WriteLine("WEB_EXPORT_ERROR "+e);Directory.CreateDirectory(Output!);File.WriteAllText(Path.Combine(Output!,"capture-errors.json"),Sanitize(Errors.ToJsonString()));
        AddCheck("capture-error-"+Errors.Count,"Snapshot or comparison could not complete","fail",null,null,null,"",e.GetType().Name+": "+e.Message,category:"auxiliary");
        try{Flush();}catch(Exception io){File.WriteAllText(Path.Combine(Output!,"export-errors.json"),io.ToString());}}}
    private static void Write(string path,JsonNode node)
    {var file=Path.Combine(Output!,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);var content=Sanitize(node.ToJsonString());File.WriteAllText(file+".tmp",content);File.Move(file+".tmp",file,true);UpdateDisplayReceipt(path,node,content);}
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
        Observe(()=>{
            var paramsObj=new JsonObject{["mark"]=new JsonObject{["name"]=markApi,["radius"]=markApi=="PK_EDGE_set_blend_constant"?JsonValue.Create(radius):null,["range0"]=markApi=="PK_EDGE_set_blend_constant"?null:JsonValue.Create(radius),["range1"]=secondRange,["designatedFace"]=designatedFace,["tolerance"]=markTolerance},["operation"]=new JsonObject{["name"]="PK_BODY_fix_blends",["o_t_version"]=fix.o_t_version,["update"]=fix.update,["tolerance"]=fix.tolerance,["local_check"]=fix.local_check==PK_LOGICAL_true},["otherOptions"]="Unmodified defaults of the pinned PskernelSharp option constructor; see source hash and binding revision."};
            Steps[Ordinals[own]-1]![own?"gm":"pk"]!["apiParameters"]=paramsObj.DeepClone();
            SetParameters(own,paramsObj);
            Flush();
        });
    }
    internal static void BooleanParameters(bool own,PK_BODY_boolean_o_t options)
    {
        if(!Enabled)return;
        Observe(()=>{
            var paramsObj=new JsonObject{["o_t_version"]=options.o_t_version,["function"]=options.function,["default_tol"]=options.default_tol,["max_tol"]=options.max_tol,["tracking"]=options.tracking==PK_LOGICAL_true,["tracking_type"]=options.tracking_type,["track_regions"]=options.track_regions,["merge_imprinted"]=options.merge_imprinted==PK_LOGICAL_true,["allow_disjoint"]=options.allow_disjoint==PK_LOGICAL_true,["fence"]=options.fence,["target_material_side"]=options.target_material_side,["tool_material_side"]=options.tool_material_side,["update"]=options.update,["otherOptions"]="Unmodified defaults of the pinned PskernelSharp option constructor."};
            Steps[Ordinals[own]-1]![own?"gm":"pk"]!["apiParameters"]=paramsObj.DeepClone();
            SetParameters(own,paramsObj);
            Flush();
        });
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
    internal static void RecordBodyCheck(string stage,int count,PK_check_fault_t* faults)
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
            AddCheck("original-body-"+Checks.Count,stage+" full BODY_check",count==0?"pass":"fail",null,null,0,"faults",details.ToJsonString(),category:"auxiliary");
            Checks[^1]!["stage"]=stage;Checks[^1]!["evaluatedKernel"]="PK";Checks[^1]!["faultCount"]=count;Checks[^1]!["faults"]=details;Flush();
        });
    }
    internal static void RecordMass(string stage,double accuracy,double* reference,double* actual)
    {
        if(!Enabled)return;
        Observe(()=>
        {
            for(var i=0;i<15;i++)
            {
                var delta=Math.Abs(actual[i]-reference[i])/(1+Math.Abs(reference[i]));
                AddCheck("original-mass-"+Checks.Count,stage+" mass["+i+"]",double.IsFinite(delta)&&delta<1e-7?"pass":"fail",actual[i],reference[i],1e-7,i<2?"model³":i<5?"model":i<14?"model⁵":"model²","Original final point gate; accuracy="+accuracy.ToString("R",System.Globalization.CultureInfo.InvariantCulture),category:"auxiliary");
                Checks[^1]!["stage"]=stage;
            }
            Flush();
        });
    }
    internal static void FreeBlendTracking(bool own,int count,PK_FACE_array_t* unders,EntityTag* topols)
    {
        if(unders!=null){for(var i=0;i<count;i++)Free(own,unders[i].array);Free(own,unders);}Free(own,topols);
    }
    private static void Free(bool own,void* p){if(p!=null)Guard(own?KernelRuntime.MemoryFree(p):PK_MEMORY_free(p),"free observer buffer");}
    private static void Guard(int error,string label){if(error!=0)throw new InvalidDataException($"{label}: {error}");}
}
