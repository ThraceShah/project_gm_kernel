using static parasolid;

internal enum ProbeAgreement : byte { Verified, Different, Unresolved }

internal static class ParasolidProbeAgreement
{
    internal static int UnresolvedCount { get; set; }

    internal static ProbeAgreement Classify(ReadOnlySpan<PK_enclosure_t> values)
    {
        if(values.Length!=4)throw new ArgumentException("four actual classifications are required");
        var inside=false;var outside=false;var equal=true;
        foreach(var value in values)
        {
            if(value!=PK_enclosure_inside_c&&value!=PK_enclosure_outside_c&&value!=PK_enclosure_on_c)return ProbeAgreement.Different;
            inside|=value==PK_enclosure_inside_c;outside|=value==PK_enclosure_outside_c;equal&=value==values[0];
        }
        if(inside&&outside)return ProbeAgreement.Different;
        // An On disagreement is pending finite-boundary/precision review,
        // never a successful equality assertion or a native roundtrip excuse.
        return equal?ProbeAgreement.Verified:ProbeAgreement.Unresolved;
    }
}
