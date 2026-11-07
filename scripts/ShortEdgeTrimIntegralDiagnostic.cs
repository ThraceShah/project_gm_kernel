#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property AllowUnsafeBlocks=true
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;
using System.Runtime.CompilerServices;

unsafe
{
    const string name="a1-r0.5-t1E-07-reverseFalse-default";
    var referenceFile=args.Length>0?args[0]:name+"-native.x_t";
    var productFile=args.Length>1?args[1]:name+"-self.x_t";
    var rounds=args.Length>2?int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture):3;
    if(rounds is <2 or >8)throw new ArgumentOutOfRangeException(nameof(rounds));
    if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("P_SCHEMA")))
        Environment.SetEnvironmentVariable("P_SCHEMA",Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,"..","third_party","parasolid","schema")));
    var options=new PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&options),"start");
    try
    {
        var reference=Read(referenceFile);var own=Read(productFile);
        Console.WriteLine($"DIAGNOSTIC ONLY: finite trim integral; reference={referenceFile} product={productFile}");
        var previousA=new double[11];var previousB=new double[11];
        for(var i=0;i<rounds;i++)
        {
            var a=Integrate(reference,1<<i);var b=Integrate(own,1<<i);
            Console.WriteLine($"REFINEMENT slices={1<<i} max_compare={Residual(a,b):R} ref_change={(i==0?double.NaN:Residual(a,previousA)):R} self_change={(i==0?double.NaN:Residual(b,previousB)):R}");
            for(var j=0;j<a.Length;j++)Console.WriteLine($"PROPERTY {j} reference={a[j]:R} self={b[j]:R} delta={b[j]-a[j]:R}");
            previousA=a;previousB=b;
        }
        Check(KernelRuntime.EntityDelete(1,&own),"delete self");Check(KernelRuntime.EntityDelete(1,&reference),"delete reference");
    }
    finally{Check(KernelRuntime.SessionStop(),"stop");}
}

static unsafe double[] Integrate(int tag,int slices)
{
    var slot=KernelRuntime.ResolveTagRecord(tag).Slot;
    if(KernelRuntime.Xt.IsOpaque(slot))throw new InvalidOperationException("editable fresh B-rep required");
    var body=KernelRuntime.Bodies[slot];var totals=new double[11];var face=body.FirstFaceBody;
    var owns=KernelRuntime.EnsureCommandScratch();
    try
    {
        for(var f=0;f<body.FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var record=KernelRuntime.Faces[face];var surf=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
            if(surf.Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.BlendSurface))throw new InvalidOperationException($"surface {surf.Class}");
            var areaBefore=totals[10];
            var loop=record.FirstLoop;
            for(var l=0;l<record.LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
            {
                var item=KernelRuntime.Loops[loop];var fin=item.FirstFin;
                for(var k=0;k<item.FinCount;k++,fin=KernelRuntime.Fins[fin].NextInLoop)
                {
                    var use=KernelRuntime.Fins[fin];if(use.Edge<0)continue;
                    var source=KernelRuntime.Edges[use.Edge];var c=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(source.CurveTag)];
                    var lower=source.HasParameterRange!=0?source.ParameterStart:c.TMin;
                    var upper=source.HasParameterRange!=0?source.ParameterEnd:c.TMax;
                    if(source.HasParameterRange==0&&c.Class==CurveClass.Line)
                    {
                        var d=KernelRuntime.LineDataPool[c.DataIndex];var p=Vertex(source.StartVertex);var q=Vertex(source.EndVertex);
                        var o=new KernelVector3{X=d.LocationX,Y=d.LocationY,Z=d.LocationZ};var axis=new KernelVector3{X=d.AxisX,Y=d.AxisY,Z=d.AxisZ};
                        lower=Dot(Sub(p,o),axis);upper=Dot(Sub(q,o),axis);if(lower>upper)(lower,upper)=(upper,lower);
                    }
                    if(!(upper>lower)||!double.IsFinite(upper-lower))throw new InvalidOperationException("unbounded trim");
                    var edge=new KernelRuntime.ModelingEdge{Curve=source.CurveTag,Start=lower,End=upper,StartVertex=source.StartVertex,EndVertex=source.EndVertex};
                    var forward=use.Sense=='+';var increasing=forward==(c.Sense!=ParasolidConstants.PK_TOPOL_sense_negative_c);
                    var limits=new List<double>{0,1};
                    if(c.Class==CurveClass.ICurve)
                    {
                        var data=KernelRuntime.ICurveDataPool[c.DataIndex];
                        if(data.ProgramBlock>0)
                        {
                            var program=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
                            var root=program.Spines[data.ProgramSpine];
                            foreach(var row in program.Charts.Slice(root.ChartOffset,root.ChartCount))if(lower<row.Parameter&&row.Parameter<upper)
                                limits.Add(increasing?(row.Parameter-lower)/(upper-lower):(upper-row.Parameter)/(upper-lower));
                        }
                    }
                    limits.Sort();
                    for(var cell=0;cell+1<limits.Count;cell++)
                        IntegrateFin(record.SurfTag,edge,forward,limits[cell],limits[cell+1],slices,totals);
                }
            }
            totals[10]=areaBefore+Math.Abs(totals[10]-areaBefore);
        }
    }
    finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
    var volume=totals[0];if(!(volume>0))throw new InvalidOperationException($"signed volume {volume:R}");
    var x=totals[1]/volume;var y=totals[2]/volume;var z=totals[3]/volume;
    return [volume,x,y,z,totals[5]+totals[6]-volume*(y*y+z*z),totals[4]+totals[6]-volume*(x*x+z*z),totals[4]+totals[5]-volume*(x*x+y*y),
        volume*x*y-totals[7],volume*x*z-totals[8],volume*y*z-totals[9],totals[10]];
}

static unsafe void IntegrateFin(int surface,in KernelRuntime.ModelingEdge edge,bool forward,double a,double b,int slices,double[] sum)
{
    // Gauss-Legendre on each original finite native CHART cell. Refinement
    // changes quadrature only; no samples decide topology or certify roots.
    // For a moment of degree k, integrate (p·(Su×Sv))*moment(p)/(3+k)
    // over the oriented face (divergence theorem). Green's theorem then
    // reduces the face integral to -∮du times the integral from 0 to v.
    ReadOnlySpan<double> nodes=[-.9602898564975363,-.7966664774136267,-.5255324099163290,-.1834346424956498,.1834346424956498,.5255324099163290,.7966664774136267,.9602898564975363];
    ReadOnlySpan<double> weights=[.1012285362903763,.2223810344533745,.3137066458778873,.3626837833783620,.3626837833783620,.3137066458778873,.2223810344533745,.1012285362903763];
    var half=(b-a)/slices*.5;PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[4];
    for(var k=0;k<slices;k++)for(var i=0;i<nodes.Length;i++)
    {
        var f=a+half*(2*k+1+nodes[i]);
        Check(KernelRuntime.SheetCoedgePoint(surface,edge,forward?(byte)1:(byte)0,f,out var uv,out var tangent,out _),$"boundary UV surface {surface}, curve {edge.Curve} at {f:R}");
        if(tangent.param[0]==0)continue;
        var v=uv.param[1];var factor=-half*weights[i]*(edge.End-edge.Start)*tangent.param[0]*v*.5;
        for(var j=0;j<nodes.Length;j++)
        {
            uv.param[1]=v*.5*(1+nodes[j]);
            Check(KernelRuntime.SurfEval(surface,uv,1,1,0,jet),"surface jet");
            var p=From(jet[0]);var normal=Cross(From(jet[1]),From(jet[2]));var w=factor*weights[j];var flux=Dot(p,normal)*w;
            sum[0]+=flux/3;sum[1]+=flux*p.X/4;sum[2]+=flux*p.Y/4;sum[3]+=flux*p.Z/4;
            sum[4]+=flux*p.X*p.X/5;sum[5]+=flux*p.Y*p.Y/5;sum[6]+=flux*p.Z*p.Z/5;
            sum[7]+=flux*p.X*p.Y/5;sum[8]+=flux*p.X*p.Z/5;sum[9]+=flux*p.Y*p.Z/5;
            sum[10]+=w*Math.Sqrt(Dot(normal,normal));
        }
    }
}

static double Residual(double[] a,double[] b)=>a.Zip(b,(x,y)=>Math.Abs(x-y)/(1+Math.Abs(x))).Max();
static KernelVector3 Vertex(int slot)=>KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[slot].PointTag)].Position;
static unsafe KernelVector3 From(PK_VECTOR_s p)=>new(){X=p.coord[0],Y=p.coord[1],Z=p.coord[2]};
static void Check(int code,string label){if(code!=0)throw new InvalidOperationException($"{label}: {code}");}
static string Source([CallerFilePath]string path="")=>path;
static unsafe int Read(string file)
{
    var bytes=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Source())!,"..","temp_docs","short-edge-blend-oracle",file));
    fixed(byte* p=bytes)
    {
        var memory=new PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var opts=new PK_PART_receive_o_s{o_t_version=1,transmit_format=ParasolidConstants.PK_transmit_format_text_c};
        int count;int* parts;Check(KernelRuntime.PartReceiveB(memory,&opts,&count,&parts),"receive");
        try{if(count!=1)throw new InvalidOperationException("one body required");return parts[0];}
        finally{Check(KernelRuntime.MemoryFree(parts),"free parts");}
    }
}
