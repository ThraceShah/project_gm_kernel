#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AssemblyName=KernelTests
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using SplineDegree=System.Int32;
global using BufferCount=System.Int32;
using System.Numerics;
using System.Reflection;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

Bezier();Blocks();ProducerMatrices();Rolling();Active();
Console.WriteLine("PASS independent numerical risks; synthetic views are not product modeling evidence");

static void Bezier()
{
    var nets=0;var checks=0;var negative=0;
    for(SplineDegree p=0;p<=16;p++)foreach(SplineDegree q in new[]{16-p,p})
    {
        var size=4*(p+1)*(q+1);var poles=new IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Array.Fill(poles,new(double.NegativeInfinity,double.PositiveInfinity));
        var exact=new Rational[size];
        for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)
        {
            var at=4*(u*(q+1)+v);double[] h=[u+v*.5,u*u-v,u*v,1+u*.0625+v*.03125];
            for(var d=0;d<4;d++){poles[at+d]=h[d];exact[at+d]=Rational.From(h[d]);}
        }
        var patch=new BezierSurfacePatch(poles,p,q,new(-1,3,.25,1.25));
        var active=new BezierSurfacePatch(poles.AsSpan(0,size),p,q,patch.Domain);
        foreach(var box in new[]{new SurfaceParameterBox(-.5,1,.5,.75),new(.25,.25,.625,.625),new(-1.00390625,-.875,1.125,1.25390625)})
        {
            var result=new IntervalNumber[29];result[0]=123;result[^1]=456;var other=new IntervalNumber[27];
            Require(BezierSurfaceBounds.BoundRectangularSecond(patch,box,result.AsSpan(1,27))==AlgorithmStatus.Success,"Bezier padded bound");
            Require(BezierSurfaceBounds.BoundRectangularSecond(active,box,other)==AlgorithmStatus.Success,"Bezier active bound");
            Require(result[0].Lower==123&&result[^1].Lower==456,"output canaries");
            for(var k=0;k<27;k++)Require(Same(result[k+1],other[k]),"padding independence");
            foreach(var point in new[]{(box.U0,box.V0),((box.U0+box.U1)*.5,(box.V0+box.V1)*.5),(box.U1,box.V1)})
            {
                var jets=ExactJets(exact,p,q,Rational.From((point.Item1+1)*.25),Rational.From(point.Item2-.25));
                for(var k=0;k<27;k++){Require(Contains(other[k],jets[k]),$"exact original-net jet p={p} q={q} k={k}");checks++;}
                if(p>=2&&box.U0==box.U1&&jets[18].Numerator!=0)
                {Require(!Contains(new IntervalNumber(0),jets[18]),"zero-width cropped-net second derivative is a wrong reference");negative++;}
            }
        }
        var shortOutput=new IntervalNumber[26];Require(BezierSurfaceBounds.BoundRectangularSecond(patch,patch.Domain,shortOutput)==AlgorithmStatus.WorkspaceTooSmall,"output length guard");
        Require(BezierSurfaceBounds.Restrict(patch,patch.Domain,new IntervalNumber[size-1])==AlgorithmStatus.WorkspaceTooSmall,"restriction scratch length guard");
        for(var k=size;k<poles.Length;k++)Require(double.IsNegativeInfinity(poles[k].Lower)&&double.IsPositiveInfinity(poles[k].Upper),"source padding immutable");nets++;
    }
    Console.WriteLine($"BEZIER nets={nets} exactRationalJets={checks} zeroWidthDerivativeNegativeControls={negative} degrees=0..16 max=16x16 padding=Infinity extension=true");
}

static Rational[] ExactJets(Rational[] original,SplineDegree p,SplineDegree q,Rational u,Rational v)
{
    var h=new Rational[36];
    for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)
    {
        if(i>p||j>q)continue;var a=(Rational[])original.Clone();var dp=p;var dq=q;
        for(var axis=0;axis<2;axis++)for(var k=0;k<(axis==0?i:j);k++)
        {
            var np=dp-(axis==0?1:0);var nq=dq-(axis==1?1:0);var b=new Rational[4*(np+1)*(nq+1)];
            var scale=axis==0?new Rational(dp,4):new Rational(dq,1);
            for(var x=0;x<=np;x++)for(var y=0;y<=nq;y++)for(var d=0;d<4;d++)
            {var at=4*(x*(dq+1)+y)+d;b[4*(x*(nq+1)+y)+d]=(a[at+(axis==0?4*(dq+1):4)]-a[at])*scale;}
            a=b;dp=np;dq=nq;
        }
        for(var d=0;d<4;d++)
        {
            Rational sum=0;
            for(var x=0;x<=dp;x++)for(var y=0;y<=dq;y++)sum+=a[4*(x*(dq+1)+y)+d]*Bernstein(dp,x,u)*Bernstein(dq,y,v);
            h[4*(3*i+j)+d]=sum;
        }
    }
    var result=new Rational[27];
    for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)for(var d=0;d<3;d++)
    {
        var value=h[4*(3*i+j)+d];
        for(var a=0;a<=i;a++)for(var b=0;b<=j;b++)if(a!=i||b!=j)value-=(i==2&&a==1?2:1)*(j==2&&b==1?2:1)*result[3*(3*a+b)+d]*h[4*(3*(i-a)+(j-b))+3];
        result[3*(3*i+j)+d]=value/h[3];
    }
    return result;
}
static Rational Bernstein(SplineDegree degree,BufferCount index,Rational t)
{
    BigInteger binomial=1;for(var k=1;k<=index;k++)binomial=binomial*(degree-k+1)/k;
    return new Rational(binomial,1)*Rational.Pow(t,index)*Rational.Pow(1-t,degree-index);
}

static void Blocks()
{
    var rng=new Random(1654);var cases=0;var rejected=0;var wrong=0;
    for(var sample=0;sample<80;sample++)
    {
        var matrix=new IntervalNumber[49];var rhs=new IntervalNumber[7];var result=new IntervalNumber[7];var rem=new IntervalNumber[49];var generic=new IntervalNumber[49];var inv=new double[49];
        var a=new double[16];for(var i=0;i<4;i++)for(var j=0;j<4;j++)a[4*i+j]=i==j?(i==3?-3:4):rng.Next(-8,9)/32.0;
        var radius=sample%2==0?0:1e-12;
        for(var i=0;i<4;i++)for(var j=0;j<4;j++){var value=a[4*i+j];matrix[7*Index(i)+Index(j)]=new(value-radius,value+radius);}
        for(var i=3;i<6;i++){matrix[7*i+i]=1;matrix[7*i+6]=rng.Next(-20,21)/8.0;}
        Rational[] expected=[new(1,4),new(-1,2),new(1,8),new(3,4),-1,new(1,4),new(1,2)];
        for(var i=0;i<7;i++){IntervalNumber b=0;for(var j=0;j<7;j++)b+=matrix[7*i+j]*(double)expected[j];rhs[i]=b;}
        var status=JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inv,rem,generic,result,out var nearest,out var referenceNearest);
        Require(status==AlgorithmStatus.Success&&nearest==AlgorithmStatus.Success&&nearest==referenceNearest,"block/nearest regular success");
        for(var i=0;i<49;i++)Require(Same(rem[i],generic[i]),"49-term generic remainder exact identity");
        for(var i=0;i<7;i++)Require(Contains(result[i],expected[i]),"all seven solved derivatives");
        // Deliberately wrong B=0 back-substitution must lose the nonzero coupling.
        for(var i=3;i<6;i++)if(matrix[7*i+6].Magnitude>.5&&!Contains(rhs[i],expected[i]))wrong++;
        foreach(var bad in new[]{new IntervalNumber(0),new(-1,1),new(1),new(double.NegativeInfinity,-1)})
        {
            matrix[48]=bad;JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inv,rem,generic,result,out nearest,out _);
            Require(nearest==AlgorithmStatus.NotConverged,"nonnegative/singular/nonfinite nearest rejected");rejected++;
        }
        cases++;
    }
    Console.WriteLine($"BLOCK cases={cases} remainderEntries={cases*49} allSeven=true nearestNegativeControls={rejected} lostBCouplingExcluded={wrong}");
    static BufferCount Index(BufferCount i)=>i==3?6:i;
}

static void Rolling()
{
    var method=typeof(RollingSectionEvaluation).GetMethod("Weight",BindingFlags.NonPublic|BindingFlags.Static,null,[typeof(IntervalDual),typeof(IntervalDual),typeof(IntervalDual).MakeByRefType()],null)!;
    var weight=method.CreateDelegate<Weight>();var cases=0;var negative=0;
    foreach(var q in new[]{0.0,Math.ScaleB(1,-1022),double.Epsilon,Math.ScaleB(1,-20),.125,.5})
    foreach(var v in new[]{-4.0,-1.125,-.5,0,.5,1,1.125,4.0})
    {
        var status=weight(new(q,.25),new(v,-.125),out var actual);Require(status==AlgorithmStatus.Success,"rolling finite box");
        // Exact rational coefficient/power moments, independent of IntervalDual.
        var qr=Rational.From(q);var vr=Rational.From(v);Rational coefficient=vr,dCoefficient=1,power=1,sum=vr,dq=0,dv=1;
        var count=q<1e-100?8:96;
        for(var k=1;k<=count;k++)
        {
            var den=(Rational)(k*(2*k+1));var f=(k*k-vr*vr)/den;
            dCoefficient=dCoefficient*f-coefficient*2*vr/den;coefficient*=f;
            dq+=k*coefficient*power;power*=qr;sum+=coefficient*power;dv+=dCoefficient*power;
        }
        var ratio=qr/2;var denominator=1-ratio;var last=coefficient*power;
        var lastDerivative=count*coefficient*Rational.Pow(qr,count-1)/4-dCoefficient*power/8;
        var factorDerivative=new Rational(1,8)+qr*2*Rational.Abs(vr)/((count+1)*(2*count+3))/8;
        var tail=Rational.Abs(last)*ratio/denominator;
        var derivativeTail=Rational.Abs(lastDerivative)*ratio/denominator+Rational.Abs(last)*factorDerivative/(denominator*denominator);
        Require(Contains(actual.Value,sum-tail)&&Contains(actual.Value,sum+tail)&&Contains(actual.Derivative,dq/4-dv/8-derivativeTail)&&Contains(actual.Derivative,dq/4-dv/8+derivativeTail),$"exact rolling moments plus complete rational tails q={q:R} v={v:R}");
        if(v==.5&&q==.125){Require(!Contains(actual.Derivative,-dv/8),"omitting q derivative detected");negative++;}cases++;
    }
    foreach(var q in new[]{.125,.5,1.0,1.5,1.9,Math.BitDecrement(2.0)})
    {
        Require(weight(new(q,1),new(.5,0),out var actual)==AlgorithmStatus.Success,"half-angle value/derivative");
        var den=4-2*Rational.From(q);var square=1/den;var derivativeSquare=1/(den*den*den);
        Require(ContainsPositiveSquareRoot(actual.Value,square)&&ContainsPositiveSquareRoot(actual.Derivative,derivativeSquare),$"independent exact half-angle q={q:R} value=[{actual.Value.Lower:R},{actual.Value.Upper:R}] derivative=[{actual.Derivative.Lower:R},{actual.Derivative.Upper:R}]");cases++;
    }
    Require(weight(new(new(0,.5),new(-.5,.25)),new(new(-1.125,1.125),new(-1,.5)),out var wide)==AlgorithmStatus.Success,"wide derivative box");
    foreach(var q in new[]{0.0,.25,.5})foreach(var v in new[]{-1.125,0,1.125})
    {Require(weight(new(q,-.5),new(v,.5),out var point)==AlgorithmStatus.Success,"wide sample");Require(wide.Value.Lower<=point.Value.Lower&&wide.Value.Upper>=point.Value.Upper&&wide.Derivative.Lower<=point.Derivative.Lower&&wide.Derivative.Upper>=point.Derivative.Upper,"wide box encloses point controls");}
    foreach(var pair in new[]{(-.01,.5),(2.0,.5),(.5,65.0),(1.5,129.0)})Require(weight(new(pair.Item1,0),new(pair.Item2,0),out _)!=AlgorithmStatus.Success,"original domain guard");
    Console.WriteLine($"ROLLING exactRationalOrHalfAngleCases={cases} missingDerivativeNegativeControls={negative} zeroSubnormalMinNormal=true wide=true originalGuards=true");
}

static void ProducerMatrices()
{
    var cases=0;
    foreach(var ellipse in new[]{false,true})foreach(var cylinder in new[]{false,true})foreach(var reversed in new[]{false,true})foreach(var sense in new[]{-1.0,1.0})
    {
        BlendSurfaceInstruction[] surfaces=[new(){IsBlend=true,Spine=0,Range0=-.5,Range1=-.5,NormalSense=sense},default,default,
            new(){Analytic=new(cylinder?SurfaceClass.Cylinder:SurfaceClass.Plane,new(){X=-.2,Y=.1,Z=.3},new(){X=.6,Y=.8,Z=1e-6},new(){Z=1},.7),NormalSense=-1}];
        BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=ellipse?CurveClass.Ellipse:CurveClass.Circle,Origin=new(){X=.1,Y=-.2,Z=.3},First=new(){X=.88,Y=.66},Second=new(){X=ellipse?-.42:-.66,Y=ellipse?.56:.88}},ChartCount=2},
            new(){First=reversed?3:0,Second=reversed?0:3,ChartOffset=2,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Parameter=0,Scale=1},new(){Parameter=1,Scale=1},new(){Position=new(){X=1.7},Chord=new(){X=.2,Y=.3,Z=.4},Scale=.8},new(){Parameter=1,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);IntervalNumber[] boxes=[new(2.1-1e-14,2.1+1e-14),new(-.4-1e-14,-.4+1e-14),new(.9-1e-14,.9+1e-14),.2,.3,-.1,new(.31-1e-14,.31+1e-14)];
        var matrix=new IntervalNumber[49];var directional=new IntervalNumber[49];
        Require(JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,boxes,matrix,directional)==AlgorithmStatus.Success,"actual conic producer");
        for(var row=0;row<7;row++)for(var col=0;col<7;col++)
        {
            var a=matrix[row*7+col];var b=directional[row*7+col];Require(a.Lower<=b.Upper&&b.Lower<=a.Upper,"analytic Jacobian versus full directional dual");
            if(col is >=3 and <=5)Require(a.Lower==(row==col?1:0)&&a.Upper==a.Lower,"actual structural Q columns");
            if(row is >=3 and <=5&&col<3)Require(a.Magnitude==0,"actual B only q column");
        }
        var inverse=new double[49];var rem=new IntervalNumber[49];var generic=new IntervalNumber[49];var rhs=new IntervalNumber[7];var solution=new IntervalNumber[7];
        for(var row=0;row<7;row++)for(var col=0;col<7;col++)rhs[row]+=matrix[row*7+col]*((col-3)/8.0);
        Require(JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inverse,rem,generic,solution,out var nearest,out var oldNearest)==AlgorithmStatus.Success,"actual producer block solve");
        Require(nearest==oldNearest,"actual producer nearest agrees with old child solve");
        for(var i=0;i<49;i++)Require(Same(rem[i],generic[i]),"actual producer remainder retains full generic arithmetic");
        for(var i=0;i<7;i++)Require(Contains(solution[i],new Rational(i-3,8)),"actual producer all seven directional outputs");cases++;
    }
    Console.WriteLine($"PRODUCER matrices={cases} all49Remainders=true fullDirectional=true planeCylinder=true supportOrders=true senses=true circleEllipse=true nonUnitStoredAxes=true");
}

static void Active()
{
    var calls=0;
    foreach(var count in new[]{1,2,16,2,1,16,17})
    {
        var plane=count;var cylinder=count+1;var surfaces=new BlendSurfaceInstruction[count+2];var spines=new BlendSpineInstruction[count];var charts=new BlendChartInstruction[2*count];
        surfaces[plane]=new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1};
        surfaces[cylinder]=new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1};
        for(var i=0;i<count;i++)
        {
            surfaces[i]=new(){IsBlend=true,Spine=i,First=i==count-1?cylinder:i+1,Second=plane,Range0=-.125,Range1=-.125,NormalSense=1};
            spines[i]=new(){First=surfaces[i].First,Second=plane,ChartOffset=2*i,ChartCount=2};
            var radius=1+.125*(count-1-i);
            charts[2*i]=new(){Position=new(){X=radius},Chord=new(){Y=1},Parameter=0,Scale=1};
            charts[2*i+1]=new(){Position=new(){X=Math.Sqrt(radius*radius-.01*.01),Y=.01},Parameter=.01,Scale=1};
            if(i==count-1&&count!=1){spines[i].Curve=new(){Kind=CurveClass.Circle,First=new(){X=1},Second=new(){Y=1}};charts[2*i+1].Position=new(){X=Math.Cos(.01),Y=Math.Sin(.01)};}
        }
        var program=new NestedBlendProgram(surfaces,spines,charts);var output=new KernelVector3[3];
        var status=JointNestedBlendEvaluation.EvaluateSpine(program,0,.0025,1,output);
        if(count==17){Require(status==AlgorithmStatus.Unsupported,"unchanged occurrence/variable capacity rejection");continue;}
        Require(status==AlgorithmStatus.Success,$"active point n={4*count-1} status={status}");
        var bound=new IntervalNumber[9];status=JointNestedBlendEvaluation.BoundSpine(program,0,new(.0025,.0025,0,0),bound);
        Require(status==AlgorithmStatus.Success,$"active interval n={4*count-1} status={status}");
        for(var d=0;d<3;d++){Require(Contains(bound[d],Rational.From(d==0?output[0].X:d==1?output[0].Y:output[0].Z)),"point enclosed");Require(Contains(bound[3+d],Rational.From(d==0?output[1].X:d==1?output[1].Y:output[1].Z)),"derivative enclosed");}calls++;
    }
    Console.WriteLine($"ACTIVE alternatingPrograms={calls} dimensions=3,7,63 pointAndInterval=true");
}

static bool ContainsPositiveSquareRoot(IntervalNumber interval,Rational value)=>interval.Finite&&interval.Upper>0&&Rational.From(Math.Max(0,interval.Lower))*Rational.From(Math.Max(0,interval.Lower))<=value&&value<=Rational.From(interval.Upper)*Rational.From(interval.Upper);
static bool Contains(IntervalNumber interval,Rational value)=>interval.Finite&&Rational.From(interval.Lower)<=value&&value<=Rational.From(interval.Upper);
static bool Same(IntervalNumber a,IntervalNumber b)=>BitConverter.DoubleToInt64Bits(a.Lower)==BitConverter.DoubleToInt64Bits(b.Lower)&&BitConverter.DoubleToInt64Bits(a.Upper)==BitConverter.DoubleToInt64Bits(b.Upper);
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
delegate AlgorithmStatus Weight(IntervalDual q,IntervalDual v,out IntervalDual value);

readonly struct Rational
{
    internal readonly BigInteger Numerator;private readonly BigInteger denominator;
    private BigInteger Denominator=>denominator.IsZero?BigInteger.One:denominator;
    internal Rational(BigInteger n,BigInteger d){if(d.IsZero)throw new DivideByZeroException();if(d.Sign<0){n=-n;d=-d;}var g=BigInteger.GreatestCommonDivisor(n,d);Numerator=n/g;denominator=d/g;}
    internal static Rational From(double value){if(!double.IsFinite(value))throw new ArgumentException("finite exact binary64");var bits=BitConverter.DoubleToUInt64Bits(value);var exponent=(long)((bits>>52)&2047);var mantissa=(BigInteger)(bits&0xfffffffffffff);if(exponent!=0)mantissa+=BigInteger.One<<52;var power=(exponent==0?-1022:exponent-1023)-52;if((bits>>63)!=0)mantissa=-mantissa;return power<0?new(mantissa,BigInteger.One<<(int)-power):new(mantissa<<(int)power,1);}
    internal static Rational Pow(Rational x,BufferCount n)=>new(BigInteger.Pow(x.Numerator,n),BigInteger.Pow(x.Denominator,n));
    internal static Rational Abs(Rational x)=>new(BigInteger.Abs(x.Numerator),x.Denominator);
    public static implicit operator Rational(int x)=>new(x,1);
    public static explicit operator double(Rational x)=>(double)x.Numerator/(double)x.Denominator;
    public static Rational operator +(Rational a,Rational b)=>new(a.Numerator*b.Denominator+b.Numerator*a.Denominator,a.Denominator*b.Denominator);
    public static Rational operator -(Rational a,Rational b)=>new(a.Numerator*b.Denominator-b.Numerator*a.Denominator,a.Denominator*b.Denominator);
    public static Rational operator -(Rational a)=>new(-a.Numerator,a.Denominator);
    public static Rational operator *(Rational a,Rational b)=>new(a.Numerator*b.Numerator,a.Denominator*b.Denominator);
    public static Rational operator /(Rational a,Rational b)=>new(a.Numerator*b.Denominator,a.Denominator*b.Numerator);
    public static bool operator <=(Rational a,Rational b)=>a.Numerator*b.Denominator<=b.Numerator*a.Denominator;
    public static bool operator >=(Rational a,Rational b)=>a.Numerator*b.Denominator>=b.Numerator*a.Denominator;
}
