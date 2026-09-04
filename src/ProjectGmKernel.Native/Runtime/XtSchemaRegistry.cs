using ManagedXt = ProjectGmKernel.Xt;

namespace ProjectGmKernel.Native.Runtime;

internal readonly record struct XtSchemaRegistration(string Identity,string ResourceFileName,int ModelerVersion,int SchemaNumber,int NodeCount,int FieldCount);

internal static class XtSchemaRegistry
{
    private static readonly object Sync=new();
    private static ManagedXt.XtSchemaCatalog? catalog; private static string? catalogDirectory;
    private static XtSchemaRegistration[] registrations=[]; private static XtSchemaDefinition?[] cache=[];
    internal static int Count{get{EnsureConfigured();return registrations.Length;}}
    internal static ReadOnlySpan<XtSchemaRegistration> Registrations{get{EnsureConfigured();return registrations;}}
    internal static ManagedXt.XtSchemaCatalog Catalog{get{EnsureConfigured();return catalog!;}}

    internal static bool ConfigureFromEnvironment()
    {
        var directory=Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")??Environment.GetEnvironmentVariable("P_SCHEMA");
        if(string.IsNullOrWhiteSpace(directory)||!Directory.Exists(directory))return false;
        lock(Sync)
        {
            var full=Path.GetFullPath(directory);
            if(catalog is not null&&string.Equals(catalogDirectory,full,StringComparison.Ordinal))return true;
            var next=ManagedXt.XtSchemaCatalog.OpenDirectory(full);var infos=next.Schemas;
            var nextRegistrations=new XtSchemaRegistration[infos.Count];
            for(var i=0;i<infos.Count;i++){var info=infos[i];nextRegistrations[i]=new(info.Identity,info.FileName,info.ModelerVersion,info.SchemaNumber,info.NodeCount,info.FieldCount);}
            catalog=next;catalogDirectory=full;registrations=nextRegistrations;cache=new XtSchemaDefinition?[infos.Count];return true;
        }
    }

    internal static bool TryResolve(string identity,out XtSchemaDefinition schema)
    {
        EnsureConfigured();
        if(!catalog!.TryResolve(identity,out var managed)){schema=null!;return false;}schema=Adapt(managed);return true;
    }
    internal static XtSchemaDefinition Resolve(string identity)=>TryResolve(identity,out var schema)?schema:throw new FormatException($"Unsupported XT schema {identity}.");
    internal static XtSchemaDefinition ResolveBySchemaNumber(int number){EnsureConfigured();return Adapt(catalog!.ResolveBySchemaNumber(number));}
    internal static XtSchemaDefinition ResolveCurrent()=>Extreme(newest:true);
    internal static XtSchemaDefinition ResolveOldest()=>Extreme(newest:false);
    internal static XtSchemaDefinition ResolveEmbeddedBase()
    {
        // Real Parasolid embeds schema definitions against the fixed v13 base
        // schema 13006 regardless of the transmitted schema version.
        EnsureConfigured();
        for(var i=0;i<registrations.Length;i++)
            if(registrations[i].SchemaNumber==13006)
                return GetByIndex(i);
        var match=-1;
        for(var i=0;i<registrations.Length;i++)
        {
            var major=registrations[i].ModelerVersion/100000;
            if(major>13)continue;
            if(match<0||registrations[i].ModelerVersion>registrations[match].ModelerVersion)match=i;
        }
        return match<0?ResolveOldest():GetByIndex(match);
    }
    private static XtSchemaDefinition Extreme(bool newest)
    { EnsureConfigured();if(registrations.Length==0)throw new FormatException("No XT schemas are available.");var index=0;for(var i=1;i<registrations.Length;i++)if(newest?registrations[i].ModelerVersion>registrations[index].ModelerVersion:registrations[i].ModelerVersion<registrations[index].ModelerVersion)index=i;return GetByIndex(index); }

    internal static bool TryResolveTransmitVersion(int transmitVersion,out XtSchemaDefinition schema)
    {
        EnsureConfigured();var targetModeler=-1;
        var schemaNumber=transmitVersion switch
        {
            0 or 380=>ResolveCurrent().SchemaNumber,10=>1000,11=>SetModeler(110,0,ref targetModeler),12=>SetModeler(120,0,ref targetModeler),13=>SetModeler(130,0,ref targetModeler),14=>SetModeler(140,0,ref targetModeler),
            20=>1012,21=>SetModeler(210,1012,ref targetModeler),30=>3000,40=>4039,50=>5059,60=>6021,70=>7007,80=>8008,90 or 91=>9008,
            100 or 101=>10004,110 or 111=>11004,120=>12006,121=>12103,130 or 132=>13006,140 or 141=>14000,150=>15003,151=>15102,
            160=>16004,161 or 170=>16100,171=>17106,180=>18007,181=>18106,190 or 191=>19008,200 or 210 or 220 or 221 or 230 or 231 or 240 or 241=>20000,
            250 or 251 or 260=>25001,261 or 270 or 271=>26105,280=>28002,281 or 290 or 291=>28101,300=>30000,301=>30100,
            310=>31001,311=>31100,320 or 321 or 330=>32001,331=>33103,340=>34001,341=>34101,350=>35001,351=>35102,
            360 or 361 or 370=>36001,371=>37102,_=>-1,
        };
        if(schemaNumber<0){schema=null!;return false;}var match=-1;
        for(var i=0;i<registrations.Length;i++)
        {
            if(registrations[i].SchemaNumber!=schemaNumber||targetModeler>=0&&registrations[i].ModelerVersion!=targetModeler)continue;
            if(match<0||registrations[i].ModelerVersion>registrations[match].ModelerVersion)match=i;
        }
        if(match<0){schema=null!;return false;}schema=GetByIndex(match);return true;
    }
    private static int SetModeler(int modeler,int schemaNumber,ref int target){target=modeler;return schemaNumber;}
    internal static XtSchemaDefinition GetByIndex(int index)
    {
        EnsureConfigured();if((uint)index>=(uint)registrations.Length)throw new ArgumentOutOfRangeException(nameof(index));
        var value=cache[index];if(value is not null)return value;
        lock(Sync){value=cache[index];if(value is not null)return value;value=Adapt(catalog!.Resolve(registrations[index].Identity));cache[index]=value;return value;}
    }
    private static XtSchemaDefinition Adapt(ManagedXt.XtSchemaDefinition source)=>source;
    private static void EnsureConfigured(){if(catalog is null&&!ConfigureFromEnvironment())throw new FormatException("P_SCHEMA does not name an accessible XT schema directory.");}
}
