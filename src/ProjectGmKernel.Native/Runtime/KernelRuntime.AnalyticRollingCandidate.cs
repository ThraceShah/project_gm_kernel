using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RollingBandCandidate
    {
        internal EdgeSlot Source;
        internal VertexSlot SourceFirst,SourceLast;
        internal FaceSlot FirstSupport,SecondSupport;
        internal SurfTag Surface;
        internal CurveTag FirstContact,SecondContact;
        internal double SourceStart,SourceEnd;
        internal double First,Last,Radius;
        internal double FirstSection,LastSection;
    }

    // Plane pairs and equal-radius intersecting cylinder pairs have exact
    // offset spines related to their selected carrier by an affine map. The
    // compiler independently checks the complete offset equations before use.
    private static int CreateAnalyticRollingCandidate(BodySlot body,EdgeSlot selected,out RollingBandCandidate candidate)
    {
        candidate=default;var edge=Edges[selected];
        if(edge.PendingBlendKind!=1||!LocalEdgeFaces(selected,out var fa,out var fb)
            ||!TryProfileEdgeRange(edge,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
        var sa=Faces[fa].SurfTag;var sb=Faces[fb].SurfTag;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(sa)],out var a)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(sb)],out var b))return ParasolidConstants.PK_ERROR_not_implemented;
        var source=Curves[GetCurveSlotByTag(edge.CurveTag)];
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var status=EvaluateCurveCore(in source,first+(last-first)*.5,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        var error=RegularBlendNormal(fa,jet[0],out var na);if(error!=0)return error;
        error=RegularBlendNormal(fb,jet[0],out var nb);if(error!=0)return error;
        var fin=edge.FirstFinEdge;if(Loops[Fins[fin].Loop].Face!=fa)fin=Fins[fin].Other;
        var orientation=(Fins[fin].Sense=='+'?1:-1)*LocalSenseSign(source.Sense);
        var turn=Dot(ProfileCross(na,nb),jet[1])*orientation;
        if(Math.Abs(turn)<1e-10*OperationIntersection.Norm(jet[1]))return ParasolidConstants.PK_ERROR_not_implemented;
        var direction=turn>0?-1:1;var radius=edge.PendingBlendFirst;
        if(direction>=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var r0=direction*LocalSenseSign(Faces[fa].Orientation)*radius;var r1=direction*LocalSenseSign(Faces[fb].Orientation)*radius;
        status=OffsetAnalyticBlendSupport(a,r0*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(sa)].Sense),out var oa);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        status=OffsetAnalyticBlendSupport(b,r1*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(sb)].Sense),out var ob);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        CurveTag spine=0;var lower=first;var upper=last;
        if(a.Kind==SurfaceClass.Plane&&b.Kind==SurfaceClass.Plane&&source.Class==CurveClass.Line)
        {
            var cosine=Dot(a.Axis,b.Axis);var determinant=1-cosine*cosine;if(!(determinant>1e-14))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var d0=Dot(Sub(oa.Origin,a.Origin),a.Axis);var d1=Dot(Sub(ob.Origin,b.Origin),b.Axis);
            var displacement=Add(Scale(a.Axis,(d0-cosine*d1)/determinant),Scale(b.Axis,(d1-cosine*d0)/determinant));
            var line=LineDataPool[source.DataIndex];var origin=Add(Vector(line.LocationX,line.LocationY,line.LocationZ),displacement);var axis=Vector(line.AxisX,line.AxisY,line.AxisZ);
            error=FiniteRollingLineWindow(body,origin,axis,radius,out lower,out upper);if(error!=0)return error;
            var radial0=Scale(a.Axis,-Math.Sign(r0*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(sa)].Sense)));
            var radial1=Scale(b.Axis,-Math.Sign(r1*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(sb)].Sense)));
            var cylinder=CreateCylinderSurfaceTag(origin.X,origin.Y,origin.Z,axis.X,axis.Y,axis.Z,radial0.X,radial0.Y,radial0.Z,radius);
            if(cylinder<=0)return ParasolidConstants.PK_ERROR_memory_full;
            var p0=Add(origin,Scale(radial0,radius));var p1=Add(origin,Scale(radial1,radius));
            // These are translations of the selected line, with the same
            // native linear gauge. Renormalizing a transported direction a
            // second time destroys the exact parallel-frame certificate.
            error=TranslateRollingLine(edge.CurveTag,p0,out var c0);if(error!=0)return error;
            error=TranslateRollingLine(edge.CurveTag,p1,out var c1);if(error!=0)return error;
            var lineContactSense=orientation>0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
            Curves[GetCurveSlotByTag(c0)].Sense=lineContactSense;Curves[GetCurveSlotByTag(c1)].Sense=FlipLocalSense(lineContactSense);
            var angle=Math.Atan2(Dot(radial1,ProfileCross(axis,radial0)),Dot(radial1,radial0));
            candidate=new(){Source=selected,FirstSupport=fa,SecondSupport=fb,Surface=cylinder,FirstContact=c0,SecondContact=c1,First=lower,Last=upper,Radius=radius,FirstSection=Math.Min(0,angle),LastSection=Math.Max(0,angle)};return 0;
        }
        else if(a.Kind==SurfaceClass.Cylinder&&b.Kind==SurfaceClass.Cylinder&&source.Class==CurveClass.Ellipse
            &&a.Radius==b.Radius&&oa.Radius==ob.Radius)
        {
            // Scaling about the conic centre maps both complete cylinder
            // equations onto their equal signed offsets. A skew-axis or
            // wrong-centre input fails the coefficient certificates below.
            var ellipse=EllipseDataPool[source.DataIndex];var scale=oa.Radius/a.Radius;
            var definition=new PK_ELLIPSE_sf_s{R1=ellipse.R1*scale,R2=ellipse.R2*scale};
            WriteVector(&definition.basis_set.location,Vector(ellipse.CenterX,ellipse.CenterY,ellipse.CenterZ));
            var axis=Vector(ellipse.AxisX,ellipse.AxisY,ellipse.AxisZ);
            var refDir=Vector(ellipse.RefDirX,ellipse.RefDirY,ellipse.RefDirZ);
            WriteVector(&definition.basis_set.axis,axis);
            WriteVector(&definition.basis_set.ref_direction,refDir);
            error=EllipseCreateImplementation(&definition,&spine);if(error!=0)return error;
            Curves[GetCurveSlotByTag(spine)].Sense=source.Sense;
            var center=Vector(ellipse.CenterX,ellipse.CenterY,ellipse.CenterZ);
            var x=refDir;var y=ProfileCross(axis,x);
            var dMid=Sub(jet[0],center);
            var midParam=Math.Atan2(Dot(dMid,y)/definition.R2,Dot(dMid,x)/definition.R1);
            var perpendicular=ProfileCross(a.Axis,b.Axis);if(!ProfileNormalize(ref perpendicular))return ParasolidConstants.PK_ERROR_not_implemented;
            var collapse=Add(center,Scale(perpendicular,oa.Radius));
            var dCollapse=Sub(collapse,center);
            var parameter=Math.Atan2(Dot(dCollapse,y)/definition.R2,Dot(dCollapse,x)/definition.R1);
            lower=parameter+Math.PI*Math.Floor((midParam-parameter)/Math.PI);upper=lower+Math.PI;
            if(edge.StartVertex>=0&&edge.EndVertex>=0)
            {
                var v0=Points[GetPointSlotByTag(Vertices[edge.StartVertex].PointTag)].Position;
                var v1=Points[GetPointSlotByTag(Vertices[edge.EndVertex].PointTag)].Position;
                var d0=Sub(v0,center);var d1=Sub(v1,center);
                var t0=Math.Atan2(Dot(d0,y)/definition.R2,Dot(d0,x)/definition.R1);
                var t1=Math.Atan2(Dot(d1,y)/definition.R2,Dot(d1,x)/definition.R1);
                t0+=Math.Tau*Math.Round((midParam-t0)/Math.Tau);t1+=Math.Tau*Math.Round((midParam-t1)/Math.Tau);
                if(!OperationIntersection.InInterval(t0,lower,upper)||!OperationIntersection.InInterval(t1,lower,upper))return ParasolidConstants.PK_ERROR_not_implemented;
            }
        }
        else return ParasolidConstants.PK_ERROR_not_implemented;
        Curves[GetCurveSlotByTag(spine)].Sense=source.Sense;
        Span<GeomTag> supports=stackalloc GeomTag[2]{sa,sb},copies=stackalloc GeomTag[2];
        error=TryCloneEntitiesCore(supports,copies);if(error!=0)return error;SurfTag surface=0;
        error=CompileAndBindNestedBlend(copies[0],copies[1],spine,r0,r1,&surface,lower,upper);if(error!=0)return error;
        var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(surface)].DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var instruction=program.Spines[root.Spine];
        status=AnalyticSpineContact.Curve(instruction.Curve,program.Surfaces[root.First],r0,out var firstContact);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        status=AnalyticSpineContact.Curve(instruction.Curve,program.Surfaces[root.Second],r1,out var secondContact);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        error=BindAnalyticContact(firstContact,out var ca);if(error!=0)return error;
        error=BindAnalyticContact(secondContact,out var cb);if(error!=0)return error;
        // Where the two axial charts oppose one another, use their signed
        // projections of the selected conic's major direction. These are
        // unchanged by rigid motion or reflection, and do not depend on
        // which fin is first or where the selected conic was split.
        var major=new IntervalVector(instruction.Curve.First);
        var axialA=IntervalVector.Dot(major,new IntervalVector(a.Axis)).Value;
        var axialB=IntervalVector.Dot(major,new IntervalVector(b.Axis)).Value;
        var contactSense=axialA.Lower>0&&axialB.Upper<0?ParasolidConstants.PK_TOPOL_sense_positive_c
            :axialA.Upper<0&&axialB.Lower>0?ParasolidConstants.PK_TOPOL_sense_negative_c:source.Sense;
        Curves[GetCurveSlotByTag(ca)].Sense=contactSense;Curves[GetCurveSlotByTag(cb)].Sense=FlipLocalSense(contactSense);
        candidate=new(){Source=selected,FirstSupport=fa,SecondSupport=fb,Surface=surface,FirstContact=ca,SecondContact=cb,First=lower,Last=upper,Radius=radius,FirstSection=0,LastSection=1};return 0;
    }

    private static int TranslateRollingLine(CurveTag source,in KernelVector3 origin,out CurveTag curve)
    {
        var error=TryCloneEntityCore(source,out curve);if(error!=0)return error;
        ref var data=ref LineDataPool[Curves[GetCurveSlotByTag(curve)].DataIndex];
        data.LocationX=origin.X;data.LocationY=origin.Y;data.LocationZ=origin.Z;return 0;
    }

    private static int FiniteRollingLineWindow(BodySlot body,in KernelVector3 origin,in KernelVector3 axis,double radius,out double first,out double last)
    {
        first=double.PositiveInfinity;last=double.NegativeInfinity;var face=Bodies[body].FirstFaceBody;
        for(var f=0;f<Bodies[body].FaceCountBody;f++,face=Faces[face].NextInBody)
        {
            var status=RegularFiniteFaceExtent(face,out var extent);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var projected=(new IntervalNumber(extent.XLo,extent.XHi)-origin.X)*axis.X
                +(new IntervalNumber(extent.YLo,extent.YHi)-origin.Y)*axis.Y+(new IntervalNumber(extent.ZLo,extent.ZHi)-origin.Z)*axis.Z;
            if(!projected.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            first=Math.Min(first,projected.Lower-radius);last=Math.Max(last,projected.Upper+radius);
        }
        return first<last?0:ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    private static int BindAnalyticContact(in ParametricSpineCurve contact,out CurveTag curve)
    {
        curve=0;if(contact.Kind==CurveClass.Line)
        {curve=CreateLineCurveTag(contact.Origin.X,contact.Origin.Y,contact.Origin.Z,contact.First.X,contact.First.Y,contact.First.Z);return curve>0?0:ParasolidConstants.PK_ERROR_memory_full;}
        if(contact.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return ParasolidConstants.PK_ERROR_not_implemented;
        // Principal axes are an exact change of the two trigonometric basis
        // coefficients. No point fitting changes the conic or its carrier.
        var phase=.5*Math.Atan2(2*Dot(contact.First,contact.Second),Dot(contact.First,contact.First)-Dot(contact.Second,contact.Second));
        var major=Add(Scale(contact.First,Math.Cos(phase)),Scale(contact.Second,Math.Sin(phase)));
        var minor=Sub(Scale(contact.Second,Math.Cos(phase)),Scale(contact.First,Math.Sin(phase)));
        var r0=OperationIntersection.Norm(major);var r1=OperationIntersection.Norm(minor);var axis=ProfileCross(major,minor);
        if(!(r0>=r1&&r1>0)||!ProfileNormalize(ref axis)||!ProfileNormalize(ref major))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var definition=new PK_ELLIPSE_sf_s{R1=r0,R2=r1};WriteVector(&definition.basis_set.location,contact.Origin);WriteVector(&definition.basis_set.axis,axis);WriteVector(&definition.basis_set.ref_direction,major);
        fixed(CurveTag* result=&curve)return EllipseCreateImplementation(&definition,result);
    }
}
