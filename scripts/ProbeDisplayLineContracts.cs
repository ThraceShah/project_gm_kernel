#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("display polyline callback contracts",out var host,out var reason))throw new Exception(reason);
using(host) Run();
static unsafe void Run()
{
    var form=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0)),2);
    PK_CURVE_t circle;Check(PK_CIRCLE_create(&form,&circle));
    PK_SESSION_frustrum_t previous;Check(PK_SESSION_ask_frustrum(&previous));var current=previous;
    current.goopsg=&Graphics.Open;current.gosgmt=&Graphics.Segment;current.goclsg=&Graphics.Close;
    Check(PK_SESSION_register_frustrum(&current));
    try
    {
        foreach(var tolerance in new[]{.01,.001})
        {
            PK_CURVE_t proxy;PK_LOGICAL_t exact;
            Check(PK_CURVE_make_bcurve(circle,new PK_INTERVAL_t(0,Math.Tau),PK_LOGICAL_false,PK_LOGICAL_false,tolerance*.1,&proxy,&exact));
            Console.WriteLine($"CIRCLE radius2, chord tolerance {tolerance}, exact {exact}");
            PK_INTERVAL_t interval;Check(PK_CURVE_ask_interval(proxy,&interval));
            Console.WriteLine($"Proxy interval {interval.value[0]:R}..{interval.value[1]:R}");
            var options=new PK_GEOM_render_line_o_t{bcurve=PK_render_bcurve_polyline_c,is_curve_chord_tol=PK_LOGICAL_true,curve_chord_tol=tolerance};
            Check(PK_GEOM_render_line(1,&proxy,null,&options));Check(PK_ENTITY_delete(1,&proxy));
        }
        var lineForm=new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(1,2,3),new PK_VECTOR1_t(-1,0,0)));
        PK_CURVE_t line,lineProxy;PK_LOGICAL_t lineExact;
        Check(PK_LINE_create(&lineForm,&line));
        Check(PK_CURVE_make_bcurve(line,new PK_INTERVAL_t(-1,2),PK_LOGICAL_false,PK_LOGICAL_false,1e-4,&lineProxy,&lineExact));
        Console.WriteLine("LINE proxy for P(t)=(1-t,2,3), t=-1..2; expected endpoints (2,2,3),(-1,2,3)");
        var lineOptions=new PK_GEOM_render_line_o_t{bcurve=PK_render_bcurve_polyline_c,is_curve_chord_tol=PK_LOGICAL_true,curve_chord_tol=.01};
        Check(PK_GEOM_render_line(1,&lineProxy,null,&lineOptions));
        Check(PK_ENTITY_delete(1,&lineProxy));Check(PK_ENTITY_delete(1,&line));Check(PK_ENTITY_delete(1,&circle));
    }
    finally{Check(PK_SESSION_register_frustrum(&previous));}
}
static void Check(int error){if(error!=0)throw new Exception($"PK error {error}");}
static unsafe class Graphics
{
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Open(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    {Dump("OPEN",type,count,tags,n,data,nl,lt);*fail=CONTIN;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Segment(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    {Dump("SEG",type,count,tags,n,data,nl,lt);*fail=CONTIN;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Close(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    {Dump("CLOSE",type,count,tags,n,data,nl,lt);*fail=CONTIN;}
    private static void Dump(string callback,int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt)
    {
        Console.WriteLine($"{callback} type={*type}, n={*n}, tags={string.Join(',',new ReadOnlySpan<int>(tags,*count).ToArray())}, lt={string.Join(',',new ReadOnlySpan<int>(lt,*nl).ToArray())}");
        if(*n<0||*n>10000)throw new Exception("unexpected count");
        var polyline=callback=="SEG"&&*nl>=2&&lt[1]==L3TPPY;
        var scalars=polyline?checked(*n*3):*n;
        if(scalars%3!=0)return;
        var length=0.0;
        var maximumMidpointError=0.0;
        for(var i=0;i<scalars;i+=3)
        {
            var x=data[i];var y=data[i+1];var z=data[i+2];
            if(i>=3){var dx=x-data[i-3];var dy=y-data[i-2];var dz=z-data[i-1];length+=Math.Sqrt(dx*dx+dy*dy+dz*dz);}
            if(polyline&&i>=3){var mx=(x+data[i-3])*.5;var my=(y+data[i-2])*.5;maximumMidpointError=Math.Max(maximumMidpointError,Math.Abs(2-Math.Sqrt(mx*mx+my*my)));}
            if(i<12||i>=scalars-12||i==(*n/3-1)*3)Console.WriteLine($"  scalar {i} point=({x:R},{y:R},{z:R}), angle={Math.Atan2(y,x):R}, radius={Math.Sqrt(x*x+y*y):R}");
        }
        Console.WriteLine($"  {(polyline?"polyline-point":"scalar")}-count interpretation: {scalars/3} triples, length={length:R}, max midpoint error={maximumMidpointError:R}");
    }
}
