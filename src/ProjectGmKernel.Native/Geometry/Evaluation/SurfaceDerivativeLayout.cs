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

    public SurfaceDerivativeLayout(DerivativeOrder uOrder, DerivativeOrder vOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(uOrder);
        ArgumentOutOfRangeException.ThrowIfNegative(vOrder);
        if (((long)uOrder + 1) * ((long)vOrder + 1) > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(vOrder), "Derivative layout exceeds span capacity.");

        UOrder = uOrder;
        VOrder = vOrder;
    }

    public BufferOffset GetIndex(DerivativeOrder uOrder, DerivativeOrder vOrder)
    {
        if ((uint)uOrder > (uint)UOrder)
            throw new ArgumentOutOfRangeException(nameof(uOrder));
        if ((uint)vOrder > (uint)VOrder)
            throw new ArgumentOutOfRangeException(nameof(vOrder));

        return uOrder * (VOrder + 1) + vOrder;
    }
}
