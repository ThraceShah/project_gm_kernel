using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;

namespace ProjectGmKernel.Native.Geometry.Construction;

internal static class SplineEditingMath
{
    /// <summary>Bezier elevation followed by inverse Boehm removal restores
    /// the original knot continuities instead of introducing nominal creases.</summary>
    internal static bool Raise(ReadOnlySpan<double> input,ReadOnlySpan<double> knots,BufferCount dimension,SplineDegree degree,SplineDegree increment,
        Span<double> output,Span<double> outputKnots,out BufferCount count)
    {
        count=0;var oldCount=input.Length/dimension;var target=degree+increment;
        if(increment<1||target>16||knots.Length!=oldCount+degree+1||knots[0]!=knots[degree]||knots[oldCount]!=knots[^1])return false;
        Span<double> breaks=stackalloc double[knots.Length];Span<int> mults=stackalloc int[knots.Length];var nb=0;
        foreach(var knot in knots)
        {if(nb>0&&breaks[nb-1]==knot)mults[nb-1]++;else{breaks[nb]=knot;mults[nb++]=1;}}
        count=(nb-1)*target+1;
        if(output.Length<count*dimension||outputKnots.Length<count+target+1)return false;
        Span<double> work=stackalloc double[((nb-1)*degree+1)*dimension];Span<double> workKnots=stackalloc double[(nb-1)*degree+degree+2];
        if(!FreeformSplineMath.MatchBezier(input,dimension,knots,degree,breaks[..nb],target,output,work,workKnots))return false;
        var polynomial=true;for(var i=dimension-1;i<input.Length;i+=dimension)polynomial&=input[i]==1;
        if(polynomial)for(var i=dimension-1;i<count*dimension;i+=dimension)output[i]=1;
        var nk=0;
        for(var i=0;i<nb;i++)
        {var multiplicity=i==0||i==nb-1?target+1:target;outputKnots.Slice(nk,multiplicity).Fill(breaks[i]);nk+=multiplicity;}
        for(var i=1;i<nb-1;i++)
            for(var remove=target-(mults[i]+increment);remove>0;remove--)
                if(!RemoveOne(output,outputKnots,dimension,target,ref count,breaks[i],1e-10))return false;
        return true;
    }

    /// <summary>Invert a single knot insertion with a local overdetermined QR
    /// solve. Acceptance is a conservative all-parameter rational distance
    /// bound after reinsertion in the original common basis.</summary>
    internal static bool RemoveOne(Span<double> poles,Span<double> knots,BufferCount dimension,SplineDegree degree,
        ref BufferCount count,double knot,double tolerance)
    {
        if(!(tolerance>=0)||!double.IsFinite(tolerance)||count<=degree+1||knot<=knots[degree]||knot>=knots[count])return false;
        var nk=count+degree+1;var remove=-1;
        for(var i=degree+1;i<count;i++)if(knots[i]==knot){remove=i;break;}
        if(remove<0)return false;
        var newCount=count-1;
        Span<double> reducedKnots=stackalloc double[nk-1];knots[..remove].CopyTo(reducedKnots);knots.Slice(remove+1,nk-remove-1).CopyTo(reducedKnots[remove..]);
        var span=degree;while(span+1<newCount&&reducedKnots[span+1]<=knot)span++;
        var multiplicity=0;foreach(var value in reducedKnots)if(value==knot)multiplicity++;
        var left=span-degree;var right=span-multiplicity;var unknowns=right-left-1;var rows=unknowns+1;
        if(unknowns<0||rows>17)return false;
        Span<double> candidate=stackalloc double[newCount*dimension];
        poles[..((left+1)*dimension)].CopyTo(candidate);
        poles.Slice((right+1)*dimension,(newCount-right)*dimension).CopyTo(candidate[(right*dimension)..]);
        Span<double> matrix=stackalloc double[17*16],rhs=stackalloc double[17],solution=stackalloc double[16],tau=stackalloc double[16];Span<int> pivots=stackalloc int[16];
        Span<double> alpha=stackalloc double[17];matrix.Clear();
        for(var row=0;row<rows;row++)
        {
            var i=left+1+row;var denominator=reducedKnots[i+degree]-reducedKnots[i];if(!(denominator>0))return false;
            alpha[row]=(knot-reducedKnots[i])/denominator;
            if(row<unknowns)matrix[row*unknowns+row]=alpha[row];
            if(row>0)matrix[row*unknowns+row-1]=1-alpha[row];
        }
        if(unknowns>0)
        {
            if(SmallLinearSolve.QrFactorize(matrix,rows,unknowns,tau,pivots,1e-14,out var rank)!=AlgorithmStatus.Success||rank!=unknowns)return false;
            for(var d=0;d<dimension;d++)
            {
                for(var row=0;row<rows;row++)
                {
                    var i=left+1+row;rhs[row]=poles[i*dimension+d];
                    if(row==0)rhs[row]-=(1-alpha[row])*candidate[left*dimension+d];
                    if(row==rows-1)rhs[row]-=alpha[row]*candidate[right*dimension+d];
                }
                if(SmallLinearSolve.QrLeastSquares(matrix,rows,unknowns,tau,rank,pivots,rhs,solution)!=AlgorithmStatus.Success)return false;
                for(var i=0;i<unknowns;i++)candidate[(left+1+i)*dimension+d]=solution[i];
            }
        }
        var polynomial=true;for(var i=dimension-1;i<count*dimension;i+=dimension)polynomial&=poles[i]==1;
        if(polynomial)for(var i=dimension-1;i<candidate.Length;i+=dimension)candidate[i]=1;
        // Certification uses interval insertion, not a floating reinsertion
        // which could round away the very discrepancy being checked.
        Span<IntervalNumber> rebuilt=stackalloc IntervalNumber[count*dimension];
        for(var i=0;i<candidate.Length;i++)rebuilt[i]=candidate[i];
        for(var i=newCount-1;i>=span-multiplicity;i--)rebuilt.Slice(i*dimension,dimension).CopyTo(rebuilt.Slice((i+1)*dimension,dimension));
        for(var i=span-multiplicity;i>span-degree;i--)
        {
            var weight=(new IntervalNumber(knot)-reducedKnots[i])/(new IntervalNumber(reducedKnots[i+degree])-reducedKnots[i]);
            for(var d=0;d<dimension;d++)rebuilt[i*dimension+d]=weight*rebuilt[i*dimension+d]+(1-weight)*rebuilt[(i-1)*dimension+d];
        }
        if(!(CommonBasisDistance(poles[..(count*dimension)],rebuilt,dimension)<=tolerance))return false;
        candidate.CopyTo(poles);reducedKnots.CopyTo(knots);count=newCount;return true;
    }

    internal static double CommonBasisDistance(ReadOnlySpan<double> first,ReadOnlySpan<IntervalNumber> second,BufferCount dimension)
    {
        if(first.Length!=second.Length||first.Length%dimension!=0||dimension is not (2 or 3 or 4))return double.PositiveInfinity;
        // Homogeneous dimensions 3 and 4 are disambiguated by the caller:
        // ordinary 3D poles are lifted to weight 1 before this routine.
        var coordinates=dimension-1;var minWeight=double.PositiveInfinity;var deltaWeight=0.0;var deltaPosition=0.0;var extent=0.0;
        Span<double> origin=stackalloc double[3];var w0=first[dimension-1];if(!(w0>0))return double.PositiveInfinity;
        Span<IntervalNumber> difference=stackalloc IntervalNumber[3],position=stackalloc IntervalNumber[3];
        for(var d=0;d<coordinates;d++)origin[d]=first[d]/w0;
        for(var i=0;i<first.Length;i+=dimension)
        {
            var wa=first[i+coordinates];var wb=second[i+coordinates];if(!(wa>0&&wb.Lower>0))return double.PositiveInfinity;
            minWeight=Math.Min(minWeight,wa);deltaWeight=Math.Max(deltaWeight,(new IntervalNumber(wa)-wb).Magnitude);
            for(var d=0;d<coordinates;d++)
            {
                var a=new IntervalNumber(first[i+d])-origin[d]*new IntervalNumber(wa);
                var b=second[i+d]-origin[d]*wb;
                difference[d]=a-b;position[d]=b/wb;
            }
            deltaPosition=Math.Max(deltaPosition,NormUpper(difference[..coordinates]));extent=Math.Max(extent,NormUpper(position[..coordinates]));
        }
        return ((new IntervalNumber(deltaPosition)+new IntervalNumber(extent)*deltaWeight)/minWeight).Upper;
    }

    private static double NormUpper(ReadOnlySpan<IntervalNumber> values)
    {
        var scale=0.0;foreach(var value in values)scale=Math.Max(scale,value.Magnitude);if(scale==0)return 0;
        IntervalNumber square=0;foreach(var value in values)square+=IntervalNumber.Square(value/scale);
        return (new IntervalNumber(scale)*Math.BitIncrement(Math.Sqrt(square.Upper))).Upper;
    }
}
