namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Managed side table associating kernel body slots with received XT
/// documents. The adapter layer owns the documents; the kernel only stores
/// the association. Cleared on session stop; capacity grows with the body
/// pool. This is XT/schema adapter storage, not kernel model data.
/// </summary>
internal sealed class XtAssociationTable
{
    private XtDocument?[] documents = new XtDocument?[16];
    private ProjectGmKernel.Xt.IXtSchemaModel?[] models = new ProjectGmKernel.Xt.IXtSchemaModel?[16];
    private int[] rootIndexes = new int[16];
    private byte[] opaque = new byte[16];

    public void Reset()
    {
        Array.Clear(documents);
        Array.Clear(models);
        Array.Clear(rootIndexes);
        Array.Clear(opaque);
    }

    public XtDocument? GetDocument(int slot) => slot < documents.Length ? documents[slot] : null;
    public ProjectGmKernel.Xt.IXtSchemaModel? GetModel(int slot) => slot < models.Length ? models[slot] : null;
    public int GetRootIndex(int slot) => slot < rootIndexes.Length ? rootIndexes[slot] : 0;
    public bool IsOpaque(int slot) => slot < opaque.Length ? opaque[slot] != 0 : false;

    public void Attach(int slot, XtDocument document, ProjectGmKernel.Xt.IXtSchemaModel? model, int rootIndex, bool isOpaque)
    {
        Ensure(slot + 1);
        documents[slot] = document;
        models[slot] = model;
        rootIndexes[slot] = rootIndex;
        opaque[slot] = isOpaque ? (byte)1 : (byte)0;
    }

    public void Drop(int slot)
    {
        if (slot >= documents.Length) return;
        documents[slot] = null;
        models[slot] = null;
        rootIndexes[slot] = 0;
        opaque[slot] = 0;
    }

    private void Ensure(int capacity)
    {
        if (capacity <= documents.Length) return;
        var doubled = documents.Length;
        while (doubled < capacity) doubled *= 2;
        Array.Resize(ref documents, doubled);
        Array.Resize(ref models, doubled);
        Array.Resize(ref rootIndexes, doubled);
        Array.Resize(ref opaque, doubled);
    }
}
