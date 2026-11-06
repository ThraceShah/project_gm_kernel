#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;

unsafe
{
 if(!ParasolidScriptHost.TryStartSession("corner mass point and error-bound diagnostic",out var host,out var message))throw new Exception(message);
 using(host)
 {
  var prefix=args.Length==0?"review-extended-lifecycle-native-author":args[0];var reference=Receive(prefix,"reference");var candidate=Receive(prefix,"self");
  try
  {
   FullCheck(reference,"reference");FullCheck(candidate,"candidate");double* a=stackalloc double[20];double* b=stackalloc double[20];
   var options=new PK_TOPOL_eval_mass_props_o_t{bound=PK_mass_bound_modulus_c};
   Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&options,a,a+2,a+4,a+8,a+18));
   Check(PK_TOPOL_eval_mass_props(1,&candidate,.99999999,&options,b,b+2,b+4,b+8,b+18));
   var maximum=0.0;var overlapping=true;
   for(var property=0;property<15;property++)
   {
    var value=property==0?0:property==1?2:property<5?property+2:property<14?property+3:18;
    var error=property==0?1:property==1?3:property<5?7:property<14?17:19;
    var residual=Math.Abs(a[value]-b[value])/(1+Math.Abs(a[value]));var overlap=a[value]-a[error]<=b[value]+b[error]&&b[value]-b[error]<=a[value]+a[error];
    if(!double.IsFinite(a[error])||!double.IsFinite(b[error])||a[error]<0||b[error]<0)throw new Exception("invalid native mass error modulus");
    maximum=Math.Max(maximum,residual);overlapping&=overlap;
    Console.WriteLine($"property={property}, values={a[value]:R}/{b[value]:R}, error_moduli={a[error]:R}/{b[error]:R}, normalized_point_residual={residual:R}, intervals_overlap={overlap}");
   }
   Console.WriteLine($"DIAGNOSTIC ONLY: max point residual={maximum:R}, unchanged point threshold=1e-7, all error intervals overlap={overlapping}");
   // Error bounds explain uncertainty. They never override the established
   // point-value gate or turn this diagnostic into a geometry acceptance run.
   if(!(maximum<1e-7))Environment.ExitCode=1;
  }
  finally{Check(PK_ENTITY_delete(1,&reference));Check(PK_ENTITY_delete(1,&candidate));}
 }
}
static unsafe int Receive(string prefix,string suffix)
{
 var path=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","trivalent-corner-oracle",prefix+"-"+suffix+".x_t");var bytes=File.ReadAllBytes(path);
 fixed(byte* data=bytes){var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(memory,&options,&count,&parts));try{if(count!=1)throw new Exception("one diagnostic body required");return parts[0];}finally{PK_MEMORY_free(parts);}}
}
static unsafe void FullCheck(int body,string label)
{
 var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults));try{for(var i=0;i<count;i++)Console.WriteLine($"{label} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");if(count!=0)throw new Exception(label+" full BODY_check failure");Console.WriteLine(label+" full BODY_check faults=0");}finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static void Check(int error){if(error!=0)throw new Exception("PK error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;
