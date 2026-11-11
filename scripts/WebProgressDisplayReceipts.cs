using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ProjectGmKernel.DisplayBridge;
using static parasolid;

internal static unsafe partial class WebProgressCapture
{
    private static readonly Dictionary<string,JsonObject> DisplayReceipts=[];
    private static void PrepareDisplayReceipt(bool own,DisplayBody display,JsonObject mesh,string stage,[CallerFilePath]string source="")
    {
        if(Environment.GetEnvironmentVariable("GM_WEB_DISPLAY_RECEIPTS")!="1")return;
        var snapshot="snapshot-"+DisplayReceipts.Count;
        var relative="display/"+snapshot+".receipt.json";
        mesh["displayReceipt"]="../"+relative;
        var ordinal=Ordinals[own]-1;
        var receipt=new JsonObject
        {
            ["schemaVersion"]=1,["status"]="unavailable",["sourceRun"]=Environment.GetEnvironmentVariable("GM_WEB_RUN_ID"),
            ["sourceSnapshot"]=snapshot,["kernel"]=own?"gm":"pk",["caseId"]=Environment.GetEnvironmentVariable("GM_WEB_CASE_ID"),
            ["sampleId"]=Environment.GetEnvironmentVariable("GM_WEB_SAMPLE_ID"),["stepId"]=$"operation-{ordinal+1}",["stage"]=stage,
            ["parameterHash"]=OperationsByOrdinal.GetValueOrDefault(ordinal)?.ParameterHash,
            ["captureSource"]="scripts/WebProgressDisplayReceipts.cs",["captureCodeSha256"]=File.Exists(source)?OperationAcceptanceLedger.HashBytes(File.ReadAllBytes(source)):null,
            ["capturedAt"]=DateTimeOffset.UtcNow.ToString("O"),["sourceEntityTag"]=display.BodyTag,
            ["meshPath"]=null,["meshSha256"]=null,["xtPath"]=null,["xtSha256"]=null,["faceBindings"]=null,
            ["surfaceTrimVerification"]="required-by-consumer"
        };
        DisplayReceipts.Add(relative,receipt);
        try
        {
            if(receipt["captureCodeSha256"]==null||receipt["sourceRun"]==null||receipt["caseId"]==null)throw new InvalidDataException("Capture source or run/case identity is unavailable.");
            var faces=display.Entities.Where(e=>e.Kind==DisplayEntityKind.Face).ToArray();
            if(faces.Any(e=>e.Identifier<=0)||faces.Select(e=>e.Identifier).Distinct().Count()!=faces.Length)throw new InvalidDataException("Missing or ambiguous face identifiers.");
            var bindings=new JsonArray();
            foreach(var face in faces)
            {
                int resolved=0;Guard(PK_PART_find_entity_by_ident(display.ParasolidBodyTag,PK_CLASS_face,face.Identifier,&resolved),"receipt identifier binding");
                if(resolved!=face.ParasolidTag)throw new InvalidDataException("Face identifier does not resolve to the mesh snapshot face.");
                bindings.Add((JsonNode)new JsonObject{["meshFaceId"]=Id(own,face),["entityTag"]=face.EntityTag,["xtFaceIdentifier"]=face.Identifier,["bindingKind"]="persistent-identifier"});
            }
            byte[] bytes;
            if(own)
            {
                if(string.IsNullOrWhiteSpace(display.XtText))throw new InvalidDataException("GM snapshot has no original XT text.");
                bytes=Encoding.UTF8.GetBytes(display.XtText);
            }
            else
            {
                // Transmit the same real PK body whose immutable mesh buffers
                // were just captured; no reconstructed or GM-substituted body.
                var body=display.ParasolidBodyTag;
                var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};
                PK_MEMORY_block_t block=default;Guard(PK_PART_transmit_b(1,&body,&options,&block),"receipt PK transmit");
                try
                {
                    using var stream=new MemoryStream();
                    for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));
                    bytes=stream.ToArray();
                }
                finally{Guard(PK_MEMORY_block_f(&block),"receipt PK XT free");}
            }
            var file=snapshot+".x_t";var path=Path.Combine(Output!,"display",file);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);
            receipt["xtPath"]=file;receipt["xtSha256"]=OperationAcceptanceLedger.HashBytes(bytes);receipt["faceBindings"]=bindings;
            receipt["bindingStatus"]="identifier-verified";receipt["status"]="available";receipt["reason"]=null;
        }
        catch(Exception e)
        {
            receipt["reason"]=e.GetType().Name+": "+e.Message;
            // Optional display evidence must never change modeling results,
            // primary checks, raw returns or measured API intervals.
        }
    }

    private static void UpdateDisplayReceipt(string meshPath,JsonNode mesh,string content)
    {
        if(mesh is not JsonObject obj||obj["displayReceipt"] is not JsonValue value||!value.TryGetValue<string>(out var reference))return;
        var relative=Path.GetRelativePath(Output!,Path.GetFullPath(reference,Path.GetDirectoryName(Path.Combine(Output!,meshPath))!)).Replace('\\','/');
        if(!DisplayReceipts.TryGetValue(relative,out var receipt))return;
        receipt["meshPath"]=Path.GetRelativePath("display",meshPath).Replace('\\','/');
        receipt["meshSha256"]=OperationAcceptanceLedger.HashString(content);
        var path=Path.Combine(Output!,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,Sanitize(receipt.ToJsonString()));
    }
}
