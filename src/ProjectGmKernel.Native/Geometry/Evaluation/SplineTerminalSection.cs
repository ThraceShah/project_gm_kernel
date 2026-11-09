using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal readonly struct SplineTerminalProof(byte mode, double ub, double vb, double ue, double ve,
    in SurfaceParameterBox box, in KernelVector3 along, in KernelVector3 across, double tolerance,double departure,
    double residualToPoint=0)
{
    internal readonly byte Mode=mode;
    internal readonly double UB=ub,VB=vb,UE=ue,VE=ve,Tolerance=tolerance,Departure=departure;
    internal readonly double ResidualToPoint=residualToPoint;
    internal readonly SurfaceParameterBox Box=box;
    internal readonly KernelVector3 Along=along,Across=across;
    internal bool IsPresent=>Mode!=0;
    internal bool UsesSpline=>Mode==2;
}

internal static class SplineTerminalSection
{
    internal static bool RestoreBoundaryWitness(in ICurveSupportView spline,in AnalyticSurface plane,
        in KernelVector3 point,ref double u,ref double v)
    {
        ref readonly var bs=ref spline.BSpline;
        if(bs.Rational||bs.UPeriodic||bs.VPeriodic||bs.UVertexCount!=bs.UDegree+1||bs.VVertexCount!=bs.VDegree+1
            ||!double.IsFinite(u)||!double.IsFinite(v))return false;
        var x=Math.Clamp(u,bs.UStart,bs.UEnd);var y=Math.Clamp(v,bs.VStart,bs.VEnd);
        var roundoff=64*SmallLinearSolve.MachineEpsilon;
        var du=(new IntervalNumber(u)-x).Magnitude;var dv=(new IntervalNumber(v)-y).Magnitude;
        if(!(du<=roundoff*(bs.UEnd-bs.UStart)&&dv<=roundoff*(bs.VEnd-bs.VStart)))return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        if(BezierSurfaceBounds.Extract(bs,bs.UDegree,bs.VDegree,poles)!=AlgorithmStatus.Success)return false;
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*bs.UVertexCount*bs.VVertexCount)],
            bs.UDegree,bs.VDegree,new(bs.UStart,bs.UEnd,bs.VStart,bs.VEnd)));
        var tolerance=Math.Min(OperationIntersection.DistanceTolerance,ICurveEvaluation.ResidualTolerance*spline.LengthScale);
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        if(patch.Bound(new(x,x,y,y),jet)!=AlgorithmStatus.Success
            ||!AffineSplineTerminator.BoundEndpoint(patch,plane,x,y,point,tolerance))return false;
        var motion=new IntervalNumber(0);
        for(var i=0;i<3;i++)motion+=new IntervalNumber(du)*jet[3+i].Magnitude+new IntervalNumber(dv)*jet[6+i].Magnitude;
        if(!(motion.Upper<=tolerance))return false;
        u=x;v=y;return true;
    }

    internal static bool Certify(in ICurveSupportView first,in ICurveSupportView second,
        in TerminatorLimit limit,in KernelVector3 chartPoint,in SampleWitness at,in SampleWitness previous,
        out SplineTerminalProof proof)
    {
        proof=default;
        if(first.IsBSurface==second.IsBSurface||limit.TermUse is not (LimitTermUse.First or LimitTermUse.Second)
            ||OperationIntersection.Norm(Sub(chartPoint,limit.BranchPoint))!=0)return false;
        ref readonly var spline=ref (first.IsBSurface?ref first:ref second);
        ref readonly var plane=ref (first.IsBSurface?ref second:ref first);
        ref readonly var bs=ref spline.BSpline;
        if(plane.Analytic.Kind!=SurfaceClass.Plane||bs.Rational||bs.UPeriodic||bs.VPeriodic
            ||bs.UVertexCount!=bs.UDegree+1||bs.VVertexCount!=bs.VDegree+1)return false;
        var flag=first.IsBSurface?SampleWitness.HasUv0:SampleWitness.HasUv1;
        if((at.Flags&previous.Flags&flag)==0)return false;
        var ub=first.IsBSurface?at.U0:at.U1;var vb=first.IsBSurface?at.V0:at.V1;
        var pu=first.IsBSurface?previous.U0:previous.U1;var pv=first.IsBSurface?previous.V0:previous.V1;
        var variesU=ub!=pu&&vb==pv;var variesV=vb!=pv&&ub==pu;
        if(variesU==variesV)return false;
        var chord=Sub(limit.BranchPoint,limit.Endpoint);var length=OperationIntersection.Norm(chord);
        if(!(length>0)||!double.IsFinite(length))return false;
        var along=Scale(chord,1/length);
        var tolerance=Math.Min(OperationIntersection.DistanceTolerance,ICurveEvaluation.ResidualTolerance*spline.LengthScale);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        if(BezierSurfaceBounds.Extract(bs,bs.UDegree,bs.VDegree,poles)!=AlgorithmStatus.Success)return false;
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*bs.UVertexCount*bs.VVertexCount)],
            bs.UDegree,bs.VDegree,new(bs.UStart,bs.UEnd,bs.VStart,bs.VEnd)));
        if(!LocateEnd(spline,patch,variesU,ub,vb,variesU?pu:pv,limit.Endpoint,along,tolerance,out var ue,out var ve)
            ||!AffineSplineTerminator.BoundEndpoint(patch,plane.Analytic,ue,ve,limit.Endpoint,tolerance)
            ||!AffineSplineTerminator.BoundEndpoint(patch,plane.Analytic,ub,vb,limit.BranchPoint,tolerance))return false;
        var useSpline=(limit.TermUse==LimitTermUse.First)==first.IsBSurface;
        var normal=plane.Analytic.Axis;
        if(useSpline&&!ICurveSupportEvaluation.TryNormal(spline,ue,ve,limit.Endpoint,out normal))return false;
        if(TerminatorEvaluation.BuildPlanes(limit.Endpoint,limit.BranchPoint,normal,out var across,out _,out var lift)!=AlgorithmStatus.Success)return false;
        var iso=Hull(ub,vb,ue,ve);
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        if(patch.Bound(iso,jet)!=AlgorithmStatus.Success)return false;
        var d=jet.Slice(variesU?3:6,3);
        if(DotBox(d,along).ContainsZero)return false;
        var width=(variesU?new IntervalNumber(iso.U1)-iso.U0:new IntervalNumber(iso.V1)-iso.V0).Upper;
        var a=DotBox(d,across);var b=DotBox(d,lift);
        var du=new IntervalNumber(a.Upper)-a.Lower;var dv=new IntervalNumber(b.Upper)-b.Lower;
        var lateral=IntervalNumber.Square(du)+IntervalNumber.Square(dv);
        // The deviation of a scalar curve from its endpoint interpolant is
        // at most h/4 times the width of its derivative enclosure.
        var departure=(new IntervalNumber(width)*.25*Math.BitIncrement(Math.Sqrt(lateral.Upper))+tolerance).Upper;
        if(!(departure<=OperationIntersection.DistanceTolerance))return false;
        if(!useSpline){proof=new(1,ub,vb,ue,ve,default,along,across,tolerance,departure);return true;}

        // A preconditioned interval inclusion holds simultaneously for
        // every chord plane between the two persistent hull points.
        var u=ub+(ue-ub)*.5;var v=vb+(ve-vb)*.5;
        var ru=width*(variesU?.75:.25);var rv=width*(variesV?.75:.25);
        var box=new SurfaceParameterBox(Math.Max(bs.UStart,u-ru),Math.Min(bs.UEnd,u+ru),
            Math.Max(bs.VStart,v-rv),Math.Min(bs.VEnd,v+rv));
        if(!Contains(box,ub,vb)||!Contains(box,ue,ve)||patch.Bound(box,jet)!=AlgorithmStatus.Success)return false;
        var j00=DotBox(jet[3..],along);var j01=DotBox(jet[6..],along);
        var j10=DotBox(jet[3..],across);var j11=DotBox(jet[6..],across);
        var det=j00*j11-j01*j10;if(!det.Finite||det.ContainsZero)return false;
        var inverseDet=j00.Midpoint*j11.Midpoint-j01.Midpoint*j10.Midpoint;
        if(!double.IsFinite(inverseDet)||inverseDet==0)return false;
        var m00=j11.Midpoint/inverseDet;var m01=-j01.Midpoint/inverseDet;
        var m10=-j10.Midpoint/inverseDet;var m11=j00.Midpoint/inverseDet;
        IntervalNumber r00=1-(m00*j00+m01*j10),r01=-(m00*j01+m01*j11);
        IntervalNumber r10=-(m10*j00+m11*j10),r11=1-(m10*j01+m11*j11);
        var contraction=Math.Max((new IntervalNumber(r00.Magnitude)+r01.Magnitude).Upper,
            (new IntervalNumber(r10.Magnitude)+r11.Magnitude).Upper);
        if(!(contraction<1))return false;
        var liftRate=(DotBox(jet[3..],lift)*j11-DotBox(jet[6..],lift)*j10)/det;
        // Propagate both hull-point residuals through the same contraction
        // before applying the derivative bound to the interior lift.
        var spanError=(DotVector(chord,along)-length).Magnitude;
        var invNorm=Math.Max((new IntervalNumber(Math.Abs(m00))+Math.Abs(m01)).Upper,
            (new IntervalNumber(Math.Abs(m10))+Math.Abs(m11)).Upper);
        var residualToUv=new IntervalNumber(invNorm)/(1-new IntervalNumber(contraction));
        var uvError=residualToUv*(new IntervalNumber(tolerance)*2+spanError);
        var speedBound=new IntervalNumber(0);
        for(var i=3;i<9;i++)speedBound+=jet[i].Magnitude;
        var residualToPoint=(residualToUv*speedBound).Upper;
        if(!double.IsFinite(residualToPoint))return false;
        var endpointLift=(new IntervalNumber(DotBox(jet[3..],lift).Magnitude)+DotBox(jet[6..],lift).Magnitude)*uvError+2*tolerance;
        var correction=(new IntervalNumber(length)*.5*liftRate.Magnitude+endpointLift).Upper;
        if(!(correction<=OperationIntersection.DistanceTolerance))return false;
        if(patch.Bound(new(u,u,v,v),jet)!=AlgorithmStatus.Success)return false;
        jet[0]-=limit.Endpoint.X;jet[1]-=limit.Endpoint.Y;jet[2]-=limit.Endpoint.Z;
        var y0=new IntervalNumber(0,length)-DotBox(jet,along);var y1=-DotBox(jet,across);
        var x0=new IntervalNumber(box.U0,box.U1)-u;var x1=new IntervalNumber(box.V0,box.V1)-v;
        var k0=u+m00*y0+m01*y1+r00*x0+r01*x1;var k1=v+m10*y0+m11*y1+r10*x0+r11*x1;
        if(!(k0.Lower>box.U0&&k0.Upper<box.U1&&k1.Lower>box.V0&&k1.Upper<box.V1))return false;
        proof=new(2,ub,vb,ue,ve,box,along,across,tolerance,(new IntervalNumber(departure)+correction).Upper,residualToPoint);
        return true;
    }

    private static bool LocateEnd(in ICurveSupportView spline,in SurfaceIntersectionPatch patch,bool variesU,
        double ub,double vb,double prior,in KernelVector3 end,in KernelVector3 along,double tolerance,out double ue,out double ve)
    {
        ue=ub;ve=vb;
        var q=variesU?ub:vb;var start=q;var sign=Math.Sign(start-prior);
        var limit=variesU?(sign>0?patch.Domain.U1:patch.Domain.U0):(sign>0?patch.Domain.V1:patch.Domain.V0);
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);
        Span<KernelVector3> values=stackalloc KernelVector3[4];
        for(var i=0;i<14;i++)
        {
            if(ICurveSupportEvaluation.Evaluate(spline,variesU?q:ub,variesU?vb:q,layout,values)!=AlgorithmStatus.Success)return false;
            var error=Dot(Sub(values[0],end),along);
            if(Math.Abs(error)<=tolerance*.125)
            {
                if((q-start)*sign<=0)return false;
                ue=variesU?q:ub;ve=variesU?vb:q;
                Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
                if(patch.Bound(Hull(ub,vb,ue,ve),bound)!=AlgorithmStatus.Success)return false;
                var rate=DotBox(bound.Slice(variesU?3:6,3),along);
                return sign>0?rate.Upper<0:rate.Lower>0;
            }
            var slope=Dot(values[variesU?2:1],along);
            if(!(slope*sign<0))return false;
            var next=Math.Clamp(q-error/slope,Math.Min(start,limit),Math.Max(start,limit));
            if(next==q)return false;
            q=next;
        }
        return false;
    }

    internal static AlgorithmStatus Evaluate(in ICurveView view,in TerminatorLimit limit,double fraction,double inverseSpan,
        DerivativeOrder order,Span<KernelVector3> output)
    {
        var proof=limit.SplineProof;
        if(!proof.UsesSpline)
        {
            var chord=Sub(limit.Endpoint,limit.BranchPoint);
            output[0]=fraction==1?limit.Endpoint:Add(limit.BranchPoint,Scale(chord,fraction));
            if(order>=1)output[1]=Scale(chord,inverseSpan);
            if(order>=2)output[2]=default;
            return AlgorithmStatus.Success;
        }
        ref readonly var spline=ref (view.Surface0.IsBSurface?ref view.Surface0:ref view.Surface1);
        var u=proof.UB+(proof.UE-proof.UB)*fraction;var v=proof.VB+(proof.VE-proof.VB)*fraction;
        var chordLength=OperationIntersection.Norm(Sub(limit.BranchPoint,limit.Endpoint));
        var target=(1-fraction)*chordLength;
        SurfaceDerivativeLayout.TryCreate(2,2,out var layout);
        Span<KernelVector3> jet=stackalloc KernelVector3[9];
        for(var i=0;i<16;i++)
        {
            if(!Contains(proof.Box,u,v))return AlgorithmStatus.NotConverged;
            var status=ICurveSupportEvaluation.Evaluate(spline,u,v,layout,jet);if(status!=AlgorithmStatus.Success)return status;
            var delta=Sub(jet[0],limit.Endpoint);var f0=Dot(delta,proof.Along)-target;var f1=Dot(delta,proof.Across);
            var su=jet[3];var sv=jet[1];
            var j00=Dot(su,proof.Along);var j01=Dot(sv,proof.Along);
            var j10=Dot(su,proof.Across);var j11=Dot(sv,proof.Across);
            var determinant=j00*j11-j01*j10;
            if(determinant==0||!double.IsFinite(determinant))return AlgorithmStatus.Singular;
            if(Math.Max(Math.Abs(f0),Math.Abs(f1))<=proof.Tolerance)
            {
                if(!CheckRootPosition(spline,proof,limit.Endpoint,u,v,target,jet[0]))return AlgorithmStatus.NotConverged;
                var speed=-chordLength*inverseSpan;
                var du=j11*speed/determinant;var dv=-j10*speed/determinant;
                var first=Add(Scale(su,du),Scale(sv,dv));var second=default(KernelVector3);
                if(order>=2)
                {
                    var h=Add(Add(Scale(jet[6],du*du),Scale(jet[4],2*du*dv)),Scale(jet[2],dv*dv));
                    var h0=Dot(h,proof.Along);var h1=Dot(h,proof.Across);
                    second=Add(h,Add(Scale(su,(j01*h1-j11*h0)/determinant),Scale(sv,(j10*h0-j00*h1)/determinant)));
                }
                if(order>=1&&!IsFinite(first)||order>=2&&!IsFinite(second))return AlgorithmStatus.NumericalFailure;
                output[0]=fraction==1?limit.Endpoint:jet[0];
                if(order>=1)output[1]=first;
                if(order>=2)output[2]=second;
                return AlgorithmStatus.Success;
            }
            u+=(j01*f1-j11*f0)/determinant;v+=(j10*f0-j00*f1)/determinant;
        }
        return AlgorithmStatus.NotConverged;
    }

    private static bool CheckRootPosition(in ICurveSupportView spline,in SplineTerminalProof proof,
        in KernelVector3 origin,double u,double v,double target,in KernelVector3 point)
    {
        ref readonly var bs=ref spline.BSpline;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        if(BezierSurfaceBounds.Extract(bs,bs.UDegree,bs.VDegree,poles)!=AlgorithmStatus.Success)return false;
        var patch=new BezierSurfacePatch(poles[..(4*bs.UVertexCount*bs.VVertexCount)],bs.UDegree,bs.VDegree,
            new(bs.UStart,bs.UEnd,bs.VStart,bs.VEnd));
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        if(BezierSurfaceBounds.Bound(patch,new(u,u,v,v),jet)!=AlgorithmStatus.Success)return false;
        var evaluationError=new IntervalNumber((jet[0]-point.X).Magnitude)+(jet[1]-point.Y).Magnitude+(jet[2]-point.Z).Magnitude;
        jet[0]-=origin.X;jet[1]-=origin.Y;jet[2]-=origin.Z;
        var residual=Math.Max((DotBox(jet,proof.Along)-target).Magnitude,DotBox(jet,proof.Across).Magnitude);
        var error=evaluationError+new IntervalNumber(residual)*proof.ResidualToPoint;
        return error.Upper<=OperationIntersection.DistanceTolerance/16;
    }

    internal static bool TryParameter(in TerminatorLimit limit,double branchParameter,bool end,out double t)
    {
        t=branchParameter;
        if(!limit.HasCertifiedSplineEnd)return false;
        // Native terminal span uses the unscaled physical chord length.
        var length=OperationIntersection.Norm(Sub(limit.Endpoint,limit.BranchPoint));
        t=branchParameter+(end?length:-length);
        return double.IsFinite(t)&&(end?t>branchParameter:t<branchParameter);
    }

    private static IntervalNumber DotBox(ReadOnlySpan<IntervalNumber> xyz,in KernelVector3 axis)
        =>xyz[0]*axis.X+xyz[1]*axis.Y+xyz[2]*axis.Z;
    private static IntervalNumber DotVector(in KernelVector3 a,in KernelVector3 b)
        =>new IntervalNumber(a.X)*b.X+new IntervalNumber(a.Y)*b.Y+new IntervalNumber(a.Z)*b.Z;
    private static bool Contains(in SurfaceParameterBox box,double u,double v)
        =>u>=box.U0&&u<=box.U1&&v>=box.V0&&v<=box.V1;
    private static SurfaceParameterBox Hull(double u,double v,double a,double b)
        =>new(Math.Min(u,a),Math.Max(u,a),Math.Min(v,b),Math.Max(v,b));
}
