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
        writer.WriteLine("label,property,accuracy,reference,reference_modulus,actual,actual_modulus,scale_lower,scale_upper,component_normalized_lower,component_normalized_upper,quantity_normalized_lower,quantity_normalized_upper,status");
    }

    internal static MassAgreement Compare(PK_BODY_t reference,PK_BODY_t actual,string label,double accuracy=.99999999999)
    {
        var observation=Observer==null?null:new JsonObject{["label"]=label,["referenceBody"]=reference,["actualBody"]=actual,["accuracy"]=accuracy,["threshold"]=1e-7,
            ["normalizationContract"]="relative-volume-mass-area; cog-Euclidean/radius-of-gyration; inertia-Frobenius/trace; reference-scales-with-outward-bounds",
            ["properties"]=new JsonArray()};
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
        return Assess(new ReadOnlySpan<double>(a,20),new ReadOnlySpan<double>(b,20),label,accuracy,observation);
    }

    internal static MassAgreement Assess(ReadOnlySpan<double> a,ReadOnlySpan<double> b,string label,double accuracy,JsonObject? observation)
    {
        if(a.Length!=20||b.Length!=20)throw new ArgumentException("exact native modulus layout required");
        ReadOnlySpan<int> values=[0,2,4,5,6,8,9,10,11,12,13,14,15,16,18];
        ReadOnlySpan<int> errors=[1,3,7,7,7,17,17,17,17,17,17,17,17,17,19];
        ReadOnlySpan<string> names=["volume","mass","cog.x","cog.y","cog.z","inertia.xx","inertia.xy","inertia.xz","inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
        // trace(I)=2*M*Rg^2 about the centre of gravity. These scales have
        // the same dimensions as their quantities and are invariant under
        // translation, rotation and a uniform change of length units.
        var massScale=PositiveScale(a[2],a[3]);
        var trace=(Lower:0.0,Upper:0.0);
        foreach(var diagonal in new[]{8,12,16})
        {
            var lower=Math.Max(0,Math.BitDecrement(a[diagonal]-a[17]));var upper=Math.BitIncrement(a[diagonal]+a[17]);
            trace=(Math.BitDecrement(trace.Lower+lower),Math.BitIncrement(trace.Upper+upper));
        }
        if(!(trace.Lower>0)||!double.IsFinite(trace.Upper))throw new InvalidOperationException("Unresolved positive inertia trace scale");
        var radius=(Lower:Math.BitDecrement(Math.Sqrt(Math.BitDecrement(trace.Lower/Math.BitIncrement(2*massScale.Upper)))),
            Upper:Math.BitIncrement(Math.Sqrt(Math.BitIncrement(trace.Upper/Math.BitDecrement(2*massScale.Lower)))));
        if(!(radius.Lower>0))throw new InvalidOperationException("Positive radius of gyration is required for solid mass certification");
        var cog=QuantityBounds(a,b,4,3,7,radius.Lower,radius.Upper);
        var inertia=QuantityBounds(a,b,8,9,17,trace.Lower,trace.Upper);
        var cogEstimate=QuantityBounds(a,b,4,3,7,radius.Lower,radius.Upper,includeModuli:false);
        var inertiaEstimate=QuantityBounds(a,b,8,9,17,trace.Lower,trace.Upper,includeModuli:false);
        var aggregate=MassAgreement.Verified;
        for(var i=0;i<values.Length;i++)
        {
            var v=values[i];var e=errors[i];
            var scale=i==0?PositiveScale(a[0],a[1]):i==1?massScale:i==14?PositiveScale(a[18],a[19]):i<5?radius:trace;
            var result=Classify(a[v],a[e],b[v],b[e],scale.Lower,scale.Upper,out var componentLower,out var componentUpper);
            var lower=componentLower;var upper=componentUpper;
            Classify(a[v],0,b[v],0,scale.Lower,scale.Upper,out var estimateLower,out var estimateUpper);
            if(i is >=2 and <5){lower=cog.Lower;upper=cog.Upper;result=Agreement(lower,upper);}
            if(i is >=5 and <14){lower=inertia.Lower;upper=inertia.Upper;result=Agreement(lower,upper);}
            if(i is >=2 and <5){estimateLower=cogEstimate.Lower;estimateUpper=cogEstimate.Upper;}
            if(i is >=5 and <14){estimateLower=inertiaEstimate.Lower;estimateUpper=inertiaEstimate.Upper;}
            var modulusNorm=i is >=2 and <5?Math.BitIncrement(Math.Sqrt(3)):i is >=5 and <14?3:1;
            observation?["properties"]!.AsArray().Add((JsonNode)new JsonObject{["property"]=names[i],["referenceEstimate"]=a[v],["referenceModulus"]=a[e],
                ["actualEstimate"]=b[v],["actualModulus"]=b[e],["scaleLower"]=scale.Lower,["scaleUpper"]=scale.Upper,
                ["estimateNormalizedLower"]=estimateLower,["estimateNormalizedUpper"]=estimateUpper,
                ["referenceModulusNormalizedUpper"]=Math.BitIncrement(Math.BitIncrement(modulusNorm*a[e])/scale.Lower),
                ["actualModulusNormalizedUpper"]=Math.BitIncrement(Math.BitIncrement(modulusNorm*b[e])/scale.Lower),
                ["componentNormalizedLower"]=componentLower,["componentNormalizedUpper"]=componentUpper,
                ["normalizedLower"]=lower,["normalizedUpper"]=upper,["agreementStatus"]=result.ToString()});
            if(result==MassAgreement.Different)aggregate=result;
            else if(result==MassAgreement.Unresolved&&aggregate==MassAgreement.Verified)aggregate=result;
            Console.WriteLine($"MASS_BOUND {label} property={names[i]} accuracy={accuracy:R} reference={a[v]:R} reference_modulus={a[e]:R} actual={b[v]:R} actual_modulus={b[e]:R} scale=[{scale.Lower:R},{scale.Upper:R}] quantity_normalized_lower={lower:R} quantity_normalized_upper={upper:R} status={result}");
            if(evidencePath!=null)
                File.AppendAllText(evidencePath,FormattableString.Invariant($"\"{label.Replace("\"","\"\"")}\",{names[i]},{accuracy:R},{a[v]:R},{a[e]:R},{b[v]:R},{b[e]:R},{scale.Lower:R},{scale.Upper:R},{componentLower:R},{componentUpper:R},{lower:R},{upper:R},{result}\n"));
        }
        Console.WriteLine($"MASS_AGREEMENT {label} status={aggregate} threshold=1E-07 geometry_separate=True");
        if(aggregate==MassAgreement.Unresolved)UnresolvedCount++;
        return aggregate;
    }

    internal static MassAgreement Classify(double a,double radiusA,double b,double radiusB,double scaleLower,double scaleUpper,out double lower,out double upper)
    {
        if(!(scaleLower>0)||scaleUpper<scaleLower||!double.IsFinite(scaleUpper))throw new InvalidOperationException("Invalid physical mass scale");
        var difference=DifferenceBounds(a,radiusA,b,radiusB);
        lower=Math.Max(0,Math.BitDecrement(difference.Lower/scaleUpper));
        upper=Math.BitIncrement(difference.Upper/scaleLower);
        return Agreement(lower,upper);
    }

    private static MassAgreement Agreement(double lower,double upper)
        =>upper<1e-7?MassAgreement.Verified:lower>=1e-7?MassAgreement.Different:MassAgreement.Unresolved;

    private static (double Lower,double Upper) PositiveScale(double estimate,double modulus)
    {
        var lower=Math.BitDecrement(estimate-modulus);var upper=Math.BitIncrement(estimate+modulus);
        if(!(lower>0)||!double.IsFinite(upper)||modulus<0)throw new InvalidOperationException("Unresolved positive physical mass scale");
        return(lower,upper);
    }

    private static (double Lower,double Upper) QuantityBounds(ReadOnlySpan<double> a,ReadOnlySpan<double> b,int start,int count,int modulus,double scaleLower,double scaleUpper,bool includeModuli=true)
    {
        double lower=0,upper=0;
        for(var i=0;i<count;i++)
        {
            var difference=DifferenceBounds(a[start+i],includeModuli?a[modulus]:0,b[start+i],includeModuli?b[modulus]:0);
            lower=Math.Max(0,Math.BitDecrement(lower+Math.Max(0,Math.BitDecrement(difference.Lower*difference.Lower))));
            upper=Math.BitIncrement(upper+Math.BitIncrement(difference.Upper*difference.Upper));
        }
        return(Math.Max(0,Math.BitDecrement(Math.BitDecrement(Math.Sqrt(lower))/scaleUpper)),
            Math.BitIncrement(Math.BitIncrement(Math.Sqrt(upper))/scaleLower));
    }

    private static (double Lower,double Upper) DifferenceBounds(double a,double radiusA,double b,double radiusB)
    {
        if(!double.IsFinite(a)||!double.IsFinite(b)||!double.IsFinite(radiusA)||!double.IsFinite(radiusB)||radiusA<0||radiusB<0)
            throw new InvalidOperationException("Invalid native mass estimate or error modulus");
        var a0=Math.BitDecrement(a-radiusA);var a1=Math.BitIncrement(a+radiusA);
        var b0=Math.BitDecrement(b-radiusB);var b1=Math.BitIncrement(b+radiusB);
        if(!double.IsFinite(a0)||!double.IsFinite(a1)||!double.IsFinite(b0)||!double.IsFinite(b1))
            throw new InvalidOperationException("Unbounded native mass interval");
        var gap=Math.Max(0,Math.BitDecrement(Math.Max(a0-b1,b0-a1)));
        var diameter=Math.BitIncrement(Math.Max(Math.Abs(a0-b1),Math.Abs(a1-b0)));
        return(gap,diameter);
    }
}
