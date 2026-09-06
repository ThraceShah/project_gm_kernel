namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Internal rectangular derivative layout: index = uOrder * (VOrder + 1) + vOrder.
/// The default value requests position only. PK output layout adaptation belongs at the API boundary.
/// </summary>
internal readonly struct SurfaceDerivativeLayout
{
    public DerivativeOrder UOrder { get; }
    public DerivativeOrder VOrder { get; }
    public BufferCount Count => (BufferCount)(((long)UOrder + 1) * ((long)VOrder + 1));

    private SurfaceDerivativeLayout(DerivativeOrder uOrder, DerivativeOrder vOrder)
    {
        UOrder = uOrder;
        VOrder = vOrder;
    }

    public static bool TryCreate(DerivativeOrder uOrder, DerivativeOrder vOrder, out SurfaceDerivativeLayout layout)
    {
        layout = default;
        if (uOrder < 0 || vOrder < 0 || ((long)uOrder + 1) * ((long)vOrder + 1) > int.MaxValue)
            return false;
        layout = new(uOrder, vOrder);
        return true;
    }

    public BufferOffset GetIndex(DerivativeOrder uOrder, DerivativeOrder vOrder)
    {
        if ((uint)uOrder > (uint)UOrder || (uint)vOrder > (uint)VOrder)
            return -1;

        return uOrder * (VOrder + 1) + vOrder;
    }
}
