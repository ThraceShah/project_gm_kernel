using static parasolid;
using System.Text.Json.Nodes;

internal enum MassAgreement : byte { Verified, Different, Unresolved }

// Native integration uncertainty is separate from B-rep equivalence.
internal static unsafe class ParasolidMassProperties
{
    internal static int UnresolvedCount { get; private set; }
    internal static Action<JsonObject>? Observer;
    private static string? evidencePath;

    internal static void BeginEvidence(string output)
    {
        evidencePath=Path.Combine(output,"mass-properties.csv");
        using var file=new FileStream(evidencePath,FileMode.CreateNew,FileAccess.Write);
        using var writer=new StreamWriter(file);
        writer.WriteLine("label,property,accuracy,reference,reference_modulus,actual,actual_modulus,normalized_lower,normalized_upper,status");
    }

    internal static MassAgreement Compare(PK_BODY_t reference,PK_BODY_t actual,string label,double accuracy=.99999999999)
    {
        var observation=Observer==null?null:new JsonObject{["label"]=label,["referenceBody"]=reference,["actualBody"]=actual,["accuracy"]=accuracy,["threshold"]=1e-7,["properties"]=new JsonArray()};
        try
        {
            var result=CompareCore(reference,actual,label,accuracy,observation);
            if(observation!=null)observation["agreementStatus"]=result.ToString();
            return result;
        }
        catch(Exception error)
        {
            if(observation!=null){observation["agreementStatus"]="Unresolved";observation["error"]=error.Message;}
            throw;
        }
        finally
        {
            if(observation!=null)try{Observer?.Invoke(observation);}catch(Exception error){Console.Error.WriteLine("MASS_OBSERVATION_ERROR "+error);}
        }
    }

    private static MassAgreement CompareCore(PK_BODY_t reference,PK_BODY_t actual,string label,double accuracy,JsonObject? observation)
    {
        var options=new PK_TOPOL_eval_mass_props_o_t{bound=PK_mass_bound_modulus_c};
        double* a=stackalloc double[20];double* b=stackalloc double[20];
        var referenceError=PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+2,a+4,a+8,a+18);
        if(observation!=null)observation["referenceError"]=referenceError;
        ParasolidScriptHost.Check(referenceError,label+" reference mass bounds");
        var actualError=PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+2,b+4,b+8,b+18);
        if(observation!=null)observation["actualError"]=actualError;
        ParasolidScriptHost.Check(actualError,label+" actual mass bounds");
        ReadOnlySpan<int> values=[0,2,4,5,6,8,9,10,11,12,13,14,15,16,18];
        ReadOnlySpan<int> errors=[1,3,7,7,7,17,17,17,17,17,17,17,17,17,19];
        ReadOnlySpan<string> names=["volume","mass","cog.x","cog.y","cog.z","inertia.xx","inertia.xy","inertia.xz","inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
        var aggregate=MassAgreement.Verified;
        for(var i=0;i<values.Length;i++)
        {
            var v=values[i];var e=errors[i];
            var result=Classify(a[v],a[e],b[v],b[e],out var lower,out var upper);
            observation?["properties"]!.AsArray().Add((JsonNode)new JsonObject{["property"]=names[i],["referenceEstimate"]=a[v],["referenceModulus"]=a[e],
                ["actualEstimate"]=b[v],["actualModulus"]=b[e],["normalizedLower"]=lower,["normalizedUpper"]=upper,["agreementStatus"]=result.ToString()});
            if(result==MassAgreement.Different)aggregate=result;
            else if(result==MassAgreement.Unresolved&&aggregate==MassAgreement.Verified)aggregate=result;
            Console.WriteLine($"MASS_BOUND {label} property={names[i]} accuracy={accuracy:R} reference={a[v]:R} reference_modulus={a[e]:R} actual={b[v]:R} actual_modulus={b[e]:R} normalized_lower={lower:R} normalized_upper={upper:R} status={result}");
            if(evidencePath!=null)
                File.AppendAllText(evidencePath,FormattableString.Invariant($"\"{label.Replace("\"","\"\"")}\",{names[i]},{accuracy:R},{a[v]:R},{a[e]:R},{b[v]:R},{b[e]:R},{lower:R},{upper:R},{result}\n"));
        }
        Console.WriteLine($"MASS_AGREEMENT {label} status={aggregate} threshold=1E-07 geometry_separate=True");
        if(aggregate==MassAgreement.Unresolved)UnresolvedCount++;
        return aggregate;
    }

    internal static MassAgreement Classify(double a,double radiusA,double b,double radiusB,out double lower,out double upper)
    {
        if(!double.IsFinite(a)||!double.IsFinite(b)||!double.IsFinite(radiusA)||!double.IsFinite(radiusB)||radiusA<0||radiusB<0)
            throw new InvalidOperationException("Invalid native mass estimate or error modulus");
        var a0=Math.BitDecrement(a-radiusA);var a1=Math.BitIncrement(a+radiusA);
        var b0=Math.BitDecrement(b-radiusB);var b1=Math.BitIncrement(b+radiusB);
        if(!double.IsFinite(a0)||!double.IsFinite(a1)||!double.IsFinite(b0)||!double.IsFinite(b1))
            throw new InvalidOperationException("Unbounded native mass interval");
        var minAbs=a0<=0&&a1>=0?0:Math.Min(Math.Abs(a0),Math.Abs(a1));
        var maxAbs=Math.Max(Math.Abs(a0),Math.Abs(a1));
        var gap=Math.Max(0,Math.BitDecrement(Math.Max(a0-b1,b0-a1)));
        var diameter=Math.BitIncrement(Math.Max(Math.Abs(a0-b1),Math.Abs(a1-b0)));
        lower=Math.Max(0,Math.BitDecrement(gap/Math.BitIncrement(1+maxAbs)));
        upper=Math.BitIncrement(diameter/Math.BitDecrement(1+minAbs));
        if(upper<1e-7)return MassAgreement.Verified;
        return lower>=1e-7?MassAgreement.Different:MassAgreement.Unresolved;
    }
}
