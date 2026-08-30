using System.Text;
using ManagedXt = ProjectGmKernel.Xt;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static class XtText
{
    internal static string Encode(IReadOnlyList<XtNode> nodes)=>EncodeCurrent(nodes,371);
    internal static string EncodeCurrent(IReadOnlyList<XtNode> nodes,int transmitVersion)
    {
        var schema=XtSchemaRegistry.ResolveCurrent();var array=nodes as XtNode[]??nodes.ToArray();
        var document=new XtDocument{VersionText=$": TRANSMIT FILE created by modeller version {schema.ModelerVersion}",HeaderSchemaIdentity=schema.Identity,Schema=schema,Nodes=array};
        document=ManagedXt.XtGeneratedModelCodec.RoundTrip(document);
        if(transmitVersion==0)return ManagedXt.XtCodec.EncodeWithBaseSchema(document,XtSchemaRegistry.ResolveEmbeddedBase());
        if(!XtSchemaRegistry.TryResolveTransmitVersion(transmitVersion,out var target))throw new NotSupportedException($"Unsupported XT transmit version {transmitVersion}.");
        if(target.SchemaNumber!=schema.SchemaNumber)document=ManagedXt.XtVersionConverter.Transcode(document,target);
        var identity=transmitVersion==371?$"SCH_3701000_{target.SchemaNumber}":transmitVersion==380?$"SCH_3800150_{target.SchemaNumber}":target.Identity;
        document=Clone(document,identity);
        return Encoding.UTF8.GetString(ManagedXt.XtCodec.Write(XtSchemaRegistry.Catalog,document));
    }
    internal static bool TryEncodeForTransmitVersion(XtDocument document,int transmitVersion,out string text,bool includeUserFields=true)
    {
        try
        {
            document=ManagedXt.XtCodec.SelectUserFields(document,includeUserFields);
            if(transmitVersion==0)
            {
                if(document.BaseSchema is not null){text=Encoding.UTF8.GetString(ManagedXt.XtCodec.Write(XtSchemaRegistry.Catalog,document));return true;}
                var current=XtSchemaRegistry.ResolveCurrent();
                if(document.Schema.SchemaNumber!=current.SchemaNumber)document=ManagedXt.XtVersionConverter.Transcode(document,current);
                text=ManagedXt.XtCodec.EncodeWithBaseSchema(document,XtSchemaRegistry.ResolveEmbeddedBase());return true;
            }
            if(!XtSchemaRegistry.TryResolveTransmitVersion(transmitVersion,out var target)){text="";return false;}
            if(target.SchemaNumber!=document.Schema.SchemaNumber)document=ManagedXt.XtVersionConverter.Transcode(document,target);
            if(target.ModelerVersion/100000 is >=30 and <=38)document=ManagedXt.XtGeneratedModelCodec.RoundTrip(document);
            var identity=transmitVersion==371?$"SCH_3701000_{target.SchemaNumber}":transmitVersion==380?$"SCH_3800150_{target.SchemaNumber}":target.Identity;
            text=Encoding.UTF8.GetString(ManagedXt.XtCodec.Write(XtSchemaRegistry.Catalog,Clone(document,identity)));return true;
        }
        catch(NotSupportedException){text="";return false;}
    }
    internal static XtDocument SelectUserFields(XtDocument document,bool include)=>ManagedXt.XtCodec.SelectUserFields(document,include);
    internal static bool TrySelectPartRoots(XtDocument document,ReadOnlySpan<XtNodeIndex> roots,out XtDocument selected)=>ManagedXt.XtCodec.TrySelectPartRoots(document,roots,out selected);
    internal static string Encode(XtDocument document)=>Encoding.UTF8.GetString(ManagedXt.XtCodec.Write(XtSchemaRegistry.Catalog,document));
    internal static string EncodeWithBaseSchema(XtDocument document,XtSchemaDefinition baseSchema)=>ManagedXt.XtCodec.EncodeWithBaseSchema(document,baseSchema);
    internal static XtDocument DecodeDocument(string text)=>ManagedXt.XtCodec.Read(XtSchemaRegistry.Catalog,Encoding.UTF8.GetBytes(text));
    private static XtDocument Clone(XtDocument source,string identity)=>new(){PhysicalHeader=source.PhysicalHeader,VersionText=source.VersionText,HeaderSchemaIdentity=identity,Schema=source.Schema,BaseSchema=source.BaseSchema,EmbeddedMaxNodeType=source.EmbeddedMaxNodeType,UserFieldSize=source.UserFieldSize,Nodes=source.Nodes};
}

internal static class XtPartGraph
{
    internal static XtNodeIndex[] GetRootIndexes(XtDocument document)=>ManagedXt.XtPartGraph.GetRootIndexes(document);
    internal static int ApplyCompoundReceiveMode(XtDocument document,ReadOnlySpan<XtNodeIndex> selectedRoots,int receiveCompound,out XtDocument result,out XtNodeIndex[] roots)
    {
        var mode=receiveCompound switch{ParasolidConstants.PK_receive_compound_keep_c=>ManagedXt.XtCompoundReceiveMode.Keep,ParasolidConstants.PK_receive_compound_fail_c=>ManagedXt.XtCompoundReceiveMode.Fail,_=>ManagedXt.XtCompoundReceiveMode.Split};
        return ManagedXt.XtPartGraph.ApplyCompoundReceiveMode(document,selectedRoots,mode,out result,out roots)?ParasolidConstants.PK_ERROR_no_errors:ParasolidConstants.PK_ERROR_compound_body;
    }
}
