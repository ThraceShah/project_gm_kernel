using System.Numerics;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Only the native nested-blend interval residual consumes this arithmetic.
// A single tube owns this record and its derivative scratch.
// Values and outward-rounded divisors are invariant across directional seeds.
// Zero retains the actual evaluation with every seed zero: an independent
// expression is not assumed to have a finite, exactly-zero derivative.
internal enum IntervalExpressionOperation : byte { Constant, Variable, Add, Negate, Multiply, Divide, Square, Sqrt, Sin, Cos, WithValue }
internal struct IntervalExpressionNode
{
    internal IntervalDual Zero;
    internal IntervalNumber Factor;
    internal ulong Dependencies;
    internal BufferOffset Left,Right,DerivativeOffset;
    internal IntervalExpressionOperation Operation;
    internal bool RequireFinite,RequirePositiveValue;
}

internal readonly ref struct IntervalExpression
{
    private readonly Span<IntervalExpressionNode> nodes;
    private readonly Span<BufferCount> count;
    private readonly BufferOffset index;
    private readonly IntervalDual literal;
    private IntervalExpression(IntervalDual literal){this.literal=literal;nodes=default;count=default;index=-1;}
    private IntervalExpression(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,BufferOffset index)
    {this.nodes=nodes;this.count=count;this.index=index;literal=default;}
    private IntervalDual Zero=>index<0?literal:nodes[index].Zero;
    internal IntervalNumber Value=>Zero.Value;
    internal bool Finite
    {
        get{if(index>=0)nodes[index].RequireFinite=true;return Zero.Finite;}
    }
    private ulong Dependencies=>index<0?0:nodes[index].Dependencies;
    public static implicit operator IntervalExpression(double value)=>new((IntervalDual)value);
    public static implicit operator IntervalExpression(IntervalNumber value)=>new((IntervalDual)value);
    internal static IntervalExpression FromNode(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,BufferOffset index)=>new(nodes,count,index);
    internal static IntervalExpression Variable(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,IntervalNumber box,BufferOffset column)
        =>Append(nodes,count,new(){Zero=new(box,0),Dependencies=1UL<<column,Left=column,Operation=IntervalExpressionOperation.Variable});
    private static IntervalExpression Append(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,IntervalExpressionNode node)
    {
        // The caller sizes from the bounded number of occurrences. Exhaustion
        // is explicit; no partial record can be consumed as a Jacobian.
        if(count[0]>=nodes.Length){count[0]=nodes.Length+1;return new(new IntervalDual(new(double.NaN),new(double.NaN)));}
        var index=count[0]++;nodes[index]=node;return new(nodes,count,index);
    }
    private static BufferOffset Operand(Span<IntervalExpressionNode> nodes,Span<BufferCount> count,IntervalExpression value)
        =>value.index>=0?value.index:Append(nodes,count,new(){Zero=value.literal,Operation=IntervalExpressionOperation.Constant}).index;
    private static IntervalExpression Binary(IntervalExpression a,IntervalExpression b,IntervalExpressionOperation operation,IntervalDual zero,IntervalNumber factor=default)
    {
        if(a.index<0&&b.index<0)return new(zero);
        var nodes=a.index>=0?a.nodes:b.nodes;var count=a.index>=0?a.count:b.count;
        var left=Operand(nodes,count,a);var right=Operand(nodes,count,b);
        return Append(nodes,count,new(){Zero=zero,Factor=factor,Dependencies=a.Dependencies|b.Dependencies,Left=left,Right=right,Operation=operation});
    }
    private static IntervalExpression Unary(IntervalExpression a,IntervalExpressionOperation operation,IntervalDual zero,IntervalNumber factor=default)
        =>a.index<0?new(zero):Append(a.nodes,a.count,new(){Zero=zero,Factor=factor,Dependencies=a.Dependencies,Left=a.index,Operation=operation});
    public static IntervalExpression operator +(IntervalExpression a,IntervalExpression b)=>Binary(a,b,IntervalExpressionOperation.Add,a.Zero+b.Zero);
    public static IntervalExpression operator -(IntervalExpression a)=>Unary(a,IntervalExpressionOperation.Negate,-a.Zero);
    public static IntervalExpression operator -(IntervalExpression a,IntervalExpression b)=>a+-b;
    public static IntervalExpression operator *(IntervalExpression a,IntervalExpression b)=>Binary(a,b,IntervalExpressionOperation.Multiply,a.Zero*b.Zero);
    public static IntervalExpression operator /(IntervalExpression a,IntervalExpression b)=>Binary(a,b,IntervalExpressionOperation.Divide,a.Zero/b.Zero,IntervalNumber.Square(b.Value));
    internal static IntervalExpression Square(IntervalExpression a)=>Unary(a,IntervalExpressionOperation.Square,IntervalDual.Square(a.Zero),2*a.Value);
    internal static IntervalExpression Sqrt(IntervalExpression a)
    {
        var zero=IntervalDual.Sqrt(a.Zero);return Unary(a,IntervalExpressionOperation.Sqrt,zero,2*zero.Value);
    }
    internal static void SinCos(IntervalExpression a,out IntervalExpression sin,out IntervalExpression cos)
    {
        IntervalDual.SinCos(a.Zero,out var s,out var c);
        sin=Unary(a,IntervalExpressionOperation.Sin,s,c.Value);
        cos=Unary(a,IntervalExpressionOperation.Cos,c,-s.Value);
    }
    // Spline hulls supply a value and two partials. Their directional rule is
    // exactly IntervalSplineVector's du*u' + dv*v', with its original grouping.
    internal static IntervalExpression Spline(IntervalNumber value,IntervalNumber du,IntervalNumber dv,IntervalExpression u,IntervalExpression v)
    {
        var derivative=du*u+dv*v;
        return Unary(derivative,IntervalExpressionOperation.WithValue,new(value,derivative.Zero.Derivative));
    }
    internal bool PositiveValue()
    {
        if(index>=0)nodes[index].RequirePositiveValue=true;
        return Value.Lower>0;
    }
    // Rebind only variables of an already complete record. All constants,
    // support definitions and branch/chart choices belong to its owning tube.
    internal static AlgorithmStatus Rebind(Span<IntervalExpressionNode> nodes,ReadOnlySpan<IntervalNumber> boxes)
    {
        for(var i=0;i<nodes.Length;i++)
        {
            ref var node=ref nodes[i];
            if(node.Operation==IntervalExpressionOperation.Variable)node.Zero=new(boxes[node.Left],0);
            else if(node.Operation!=IntervalExpressionOperation.Constant)
            {
                var a=nodes[node.Left].Zero;var b=nodes[node.Right].Zero;
                switch(node.Operation)
                {
                    case IntervalExpressionOperation.Add:node.Zero=a+b;break;
                    case IntervalExpressionOperation.Negate:node.Zero=-a;break;
                    case IntervalExpressionOperation.Multiply:node.Zero=a*b;break;
                    case IntervalExpressionOperation.Divide:node.Zero=a/b;node.Factor=IntervalNumber.Square(b.Value);break;
                    case IntervalExpressionOperation.Square:node.Zero=IntervalDual.Square(a);node.Factor=2*a.Value;break;
                    case IntervalExpressionOperation.Sqrt:node.Zero=IntervalDual.Sqrt(a);node.Factor=2*node.Zero.Value;break;
                    case IntervalExpressionOperation.Sin:
                    case IntervalExpressionOperation.Cos:
                        IntervalDual.SinCos(a,out var sin,out var cos);var sine=node.Operation==IntervalExpressionOperation.Sin;
                        node.Zero=sine?sin:cos;node.Factor=sine?cos.Value:-sin.Value;break;
                }
            }
            if(node.RequirePositiveValue&&!(node.Zero.Value.Lower>0)||node.RequireFinite&&!node.Zero.Finite)return AlgorithmStatus.NotConverged;
        }
        return AlgorithmStatus.Success;
    }
    internal static BufferCount PrepareDerivatives(Span<IntervalExpressionNode> nodes)
    {
        var count=0;
        for(var i=0;i<nodes.Length;i++){nodes[i].DerivativeOffset=count;count+=BitOperations.PopCount(nodes[i].Dependencies);}
        return count;
    }
    private static IntervalNumber Derivative(ReadOnlySpan<IntervalExpressionNode> nodes,ReadOnlySpan<IntervalNumber> scratch,BufferOffset index,ulong seed)
    {
        ref readonly var node=ref nodes[index];
        return (node.Dependencies&seed)==0?node.Zero.Derivative:scratch[node.DerivativeOffset+BitOperations.PopCount(node.Dependencies&(seed-1))];
    }
    internal static AlgorithmStatus Differentiate(ReadOnlySpan<IntervalExpressionNode> nodes,ReadOnlySpan<BufferOffset> outputs,
        BufferCount variables,Span<IntervalNumber> scratch,Span<IntervalNumber> matrix)
    {
        // Node-major sparse forward propagation reads each primal instruction
        // once. Independent derivatives use the actual zero-seed evaluation;
        // they consume neither arithmetic nor a scratch slot.
        for(var i=0;i<nodes.Length;i++)
        {
            ref readonly var node=ref nodes[i];var remaining=node.Dependencies;var at=node.DerivativeOffset;
            if(node.RequireFinite&&!node.Zero.Finite)return AlgorithmStatus.NotConverged;
            while(remaining!=0)
            {
                var seed=remaining&(~remaining+1);remaining&=remaining-1;
                IntervalNumber derivative;
                if(node.Operation==IntervalExpressionOperation.Variable)derivative=1;
                else
                {
                    var a=node.Left;var b=node.Right;var da=Derivative(nodes,scratch,a,seed);var db=Derivative(nodes,scratch,b,seed);
                    derivative=node.Operation switch
                    {
                        IntervalExpressionOperation.Add=>da+db,
                        IntervalExpressionOperation.Negate=>-da,
                        IntervalExpressionOperation.Multiply=>da*nodes[b].Zero.Value+nodes[a].Zero.Value*db,
                        IntervalExpressionOperation.Divide=>(da*nodes[b].Zero.Value-nodes[a].Zero.Value*db)/node.Factor,
                        IntervalExpressionOperation.Square=>node.Factor*da,
                        IntervalExpressionOperation.Sqrt=>da/node.Factor,
                        IntervalExpressionOperation.Sin or IntervalExpressionOperation.Cos=>node.Factor*da,
                        IntervalExpressionOperation.WithValue=>da,
                        _=>node.Zero.Derivative,
                    };
                }
                scratch[at++]=derivative;
                if(node.RequireFinite&&!derivative.Finite)return AlgorithmStatus.NotConverged;
            }
        }
        for(var row=0;row<outputs.Length;row++)for(var column=0;column<variables;column++)
            matrix[row*variables+column]=Derivative(nodes,scratch,outputs[row],1UL<<column);
        return AlgorithmStatus.Success;
    }
    // Store attaches literals first; every published output has a record slot.
    internal BufferOffset OutputIndex()=>index;
    internal IntervalExpression Attach(Span<IntervalExpressionNode> nodes,Span<BufferCount> count)
        =>index>=0?this:Append(nodes,count,new(){Zero=literal,Operation=IntervalExpressionOperation.Constant});
}

internal readonly ref struct IntervalExpressionVector(IntervalExpression x,IntervalExpression y,IntervalExpression z)
{
    internal readonly IntervalExpression X=x,Y=y,Z=z;
    internal IntervalExpressionVector(scoped in KernelVector3 point):this(point.X,point.Y,point.Z){}
    internal bool Finite=>X.Finite&&Y.Finite&&Z.Finite;
    public static IntervalExpressionVector operator +(IntervalExpressionVector a,IntervalExpressionVector b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static IntervalExpressionVector operator -(IntervalExpressionVector a,IntervalExpressionVector b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static IntervalExpressionVector operator *(IntervalExpressionVector a,IntervalExpression b)=>new(a.X*b,a.Y*b,a.Z*b);
    public static IntervalExpressionVector operator /(IntervalExpressionVector a,IntervalExpression b)=>new(a.X/b,a.Y/b,a.Z/b);
    internal static IntervalExpression Dot(IntervalExpressionVector a,IntervalExpressionVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static IntervalExpressionVector Cross(IntervalExpressionVector a,IntervalExpressionVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    internal IntervalExpression Norm()=>IntervalExpression.Sqrt(IntervalExpression.Square(X)+IntervalExpression.Square(Y)+IntervalExpression.Square(Z));
    internal IntervalExpressionVector Unit()=>this/Norm();
}
