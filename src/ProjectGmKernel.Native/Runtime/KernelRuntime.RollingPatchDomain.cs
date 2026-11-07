using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int AppendRollingPatchDomain(in RollingBandCandidate band,ReadOnlySpan<RollingPointContact> contacts,ref SheetArrangement model,
        Span<ModelingEdge> edges,Span<EdgeSlot> originals,Span<ModelingCoedge> coedges,Span<ModelingLoop> loops,Span<ModelingFace> faces,
        ref BufferCount ne,ref BufferCount nc,ref BufferCount nl,ref BufferCount nf)
    {
        if(nf==faces.Length||nl==loops.Length||coedges.Length-nc<4||edges.Length-ne<4)return ParasolidConstants.PK_ERROR_not_implemented;
        var first=nc;var record=Surfaces[GetSurfaceSlotByTag(band.Surface)];var sense=ParasolidConstants.PK_TOPOL_sense_positive_c;
        if(record.Class==SurfaceClass.BlendSurface)
        {
            var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
            sense=root.NormalSense<0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
            Span<BufferOffset> endpoints=stackalloc BufferOffset[2];Span<KernelVector3> value=stackalloc KernelVector3[1];
            for(var end=0;end<2;end++)
            {
                var u=end==0?band.First:band.Last;VertexSlot original=-1;
                foreach(var contact in contacts)if(contact.Band==band.Surface&&u>=contact.First&&u<=contact.Last)
                {if(original>=0&&original!=contact.Vertex)return ParasolidConstants.PK_ERROR_cant_do_intersect;original=contact.Vertex;}
                SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
                var status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,u,0,layout,value);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                var error=AddRollingDomainPoint(ref model,value[0],original,out endpoints[end]);if(error!=0)return error;
            }
            for(var side=0;side<2;side++)
            {
                var curve=side==0?band.FirstContact:band.SecondContact;var cr=Curves[GetCurveSlotByTag(curve)];
                if(!TryAnalyticBlendContactMap(band.Surface,cr,cr.TMin,cr.TMax,out _,out var shift,out var scale))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var a=(band.First-shift)/scale;var b=(band.Last-shift)/scale;var lower=Math.Min(a,b);var upper=Math.Max(a,b);
                var positive=LocalSenseSign(cr.Sense)>0;var along=(scale>0)==positive;
                var error=AppendRollingDomainEdge(curve,lower,upper,along?endpoints[0]:endpoints[1],along?endpoints[1]:endpoints[0],edges,originals,ref ne,out var edge);if(error!=0)return error;
                var increasing=(side==0)==root.ReverseSection;
                coedges[nc++]=new(){Edge=edge,Forward=(increasing?along:!along)?(byte)1:(byte)0};
            }
        }
        else if(record.Class==SurfaceClass.Cylinder)
        {
            var cylinder=CylinderDataPool[record.DataIndex];var origin=Vector(cylinder.LocationX,cylinder.LocationY,cylinder.LocationZ);
            var axis=Vector(cylinder.AxisX,cylinder.AxisY,cylinder.AxisZ);var reference=Vector(cylinder.RefDirX,cylinder.RefDirY,cylinder.RefDirZ);
            Span<BufferOffset> vertices=stackalloc BufferOffset[4];Span<KernelVector3> point=stackalloc KernelVector3[1];
            Span<BufferOffset> lines=stackalloc BufferOffset[2],caps=stackalloc BufferOffset[2];
            for(var side=0;side<2;side++)
            {
                var curve=side==0?band.FirstContact:band.SecondContact;
                for(var end=0;end<2;end++)
                {
                    var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve)],end==0?band.First:band.Last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    var error=AddRollingDomainPoint(ref model,point[0],-1,out vertices[2*side+end]);if(error!=0)return error;
                }
                var positive=LocalSenseSign(Curves[GetCurveSlotByTag(curve)].Sense)>0;
                var added=AppendRollingDomainEdge(curve,band.First,band.Last,vertices[2*side+(positive?0:1)],vertices[2*side+(positive?1:0)],edges,originals,ref ne,out lines[side]);if(added!=0)return added;
            }
            var lowerSide=band.FirstSection==0?0:1;var upperSide=1-lowerSide;
            for(var end=0;end<2;end++)
            {
                var center=Add(origin,Scale(axis,end==0?band.First:band.Last));
                var curve=CreateCircleCurveTag(center.X,center.Y,center.Z,axis.X,axis.Y,axis.Z,reference.X,reference.Y,reference.Z,band.Radius);if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;
                var error=AppendRollingDomainEdge(curve,band.FirstSection,band.LastSection,vertices[2*lowerSide+end],vertices[2*upperSide+end],edges,originals,ref ne,out caps[end]);if(error!=0)return error;
            }
            coedges[nc++]=new(){Edge=caps[0],Forward=1};coedges[nc++]=new(){Edge=lines[upperSide],Forward=LocalSenseSign(Curves[GetCurveSlotByTag(upperSide==0?band.FirstContact:band.SecondContact)].Sense)>0?(byte)1:(byte)0};
            coedges[nc++]=new(){Edge=caps[1],Forward=0};coedges[nc++]=new(){Edge=lines[lowerSide],Forward=LocalSenseSign(Curves[GetCurveSlotByTag(lowerSide==0?band.FirstContact:band.SecondContact)].Sense)>0?(byte)0:(byte)1};
        }
        else return ParasolidConstants.PK_ERROR_not_implemented;
        if(LocalSenseSign(sense)*LocalSenseSign(record.Sense)<0)
        {
            coedges.Slice(first,nc-first).Reverse();for(var c=first;c<nc;c++)coedges[c].Forward^=1;
        }
        loops[nl]=new(){FirstCoedge=first,CoedgeCount=nc-first};faces[nf++]=new(){Surface=band.Surface,Sense=sense,FirstLoop=nl++,LoopCount=1};return 0;
    }

    private static int AddRollingDomainPoint(ref SheetArrangement model,in KernelVector3 point,VertexSlot original,out BufferOffset vertex)
    {
        vertex=original>=0?model.OriginalVertices[..model.NP].IndexOf(original):-1;if(vertex>=0)return 0;
        if(model.NP==model.Points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        vertex=model.NP;model.Points[model.NP]=point;model.OriginalVertices[model.NP]=original;
        if(!model.VertexPrecisions.IsEmpty)model.VertexPrecisions[model.NP]=original>=0?Vertices[original].Tolerance:0;model.NP++;return 0;
    }

    private static int ImprintRollingSubdivisions(BufferCount sourceFaces,ReadOnlySpan<RollingBandCandidate> bands,ReadOnlySpan<RollingSubdivision> subdivisions,
        Span<RegularFaceCut> cuts,ref BufferCount cutCount,Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model)
    {
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<KernelVector3> point=stackalloc KernelVector3[1],sectionPoint=stackalloc KernelVector3[1];
        Span<BoundaryParameter> anchor=stackalloc BoundaryParameter[1];
        foreach(ref readonly var split in subdivisions)
        {
            var band=bands[split.Band];var at=split.Station;var face=sourceFaces+split.Band;
            if(!(split.StationLower>band.First&&split.StationUpper<band.Last))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var record=Surfaces[GetSurfaceSlotByTag(band.Surface)];var elliptical=record.Class==SurfaceClass.BlendSurface;
            CurveTag section;double lower,upper;
            if(elliptical)
            {
                // The certified station belongs to the finite rolling
                // branch. Its fixed-u section is an exact bounded circle.
                var sectionError=NestedBlendIsoparam(band.Surface,true,at,band.FirstSection,band.LastSection,out section,out lower,out upper);
                if(sectionError!=0)return sectionError;
            }
            else
            {
                if(!TryPrepareSurface(record,out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var center=Add(cylinder.Origin,Scale(cylinder.Axis,at));
                section=CreateCircleCurveTag(center.X,center.Y,center.Z,cylinder.Axis.X,cylinder.Axis.Y,cylinder.Axis.Z,
                    cylinder.X.X,cylinder.X.Y,cylinder.X.Z,cylinder.Radius);
                if(section<=0)return ParasolidConstants.PK_ERROR_memory_full;
                lower=band.FirstSection;upper=band.LastSection;
            }
            var data=CircleDataPool[Curves[GetCurveSlotByTag(section)].DataIndex];
            var plane=new AnalyticSurface(SurfaceClass.Plane,Vector(data.CenterX,data.CenterY,data.CenterZ),
                Vector(data.AxisX,data.AxisY,data.AxisZ),Vector(data.RefDirX,data.RefDirY,data.RefDirZ));
            foreach(ref readonly var accepted in cuts[..cutCount])
            {
                if(accepted.First!=face&&accepted.Second!=face)continue;
                var cut=accepted.Cut;if(cut.Curve==band.FirstContact||cut.Curve==band.SecondContact)continue;
                var status=IntersectSheetCurvePlane(cut.Curve,cut.First,cut.Last,plane,roots,out var count);
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                if(count!=0)return ParasolidConstants.PK_ERROR_not_implemented;
            }
            if(cutCount==cuts.Length||ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var imprint=new SheetCutCurve{Curve=section,First=lower,Last=upper};
            var error=AssociateBoundaryCarrierSurface(ref model,section,band.Surface);if(error!=0)return error;
            for(var side=0;side<2;side++)
            {
                var contact=side==0?band.FirstContact:band.SecondContact;
                var angle=side==0?0:band.FirstSection==0?band.LastSection:band.FirstSection;
                var parameter=at;var carrier=Curves[GetCurveSlotByTag(contact)];
                if(elliptical)
                {
                    if(!TryAnalyticBlendContactMap(band.Surface,carrier,carrier.TMin,carrier.TMax,out var v,out var shift,out var scale))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    parameter=(at-shift)/scale;angle=v==band.FirstSection?lower:upper;
                }
                var status=EvaluateCurveCore(in carrier,parameter,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(section)],angle,0,false,sectionPoint,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                if(ProfileDistance(point[0],sectionPoint[0])>model.Tolerance)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                anchor[0]=new(){Carrier=contact,Generation=carrier.Header.Generation,Parameter=parameter,Lower=parameter,Upper=parameter};
                error=model.AddPoint(point[0],-1,out var endpoint,anchor,requireProof:true);if(error!=0)return error;
                error=model.AssociateParameter(endpoint,contact,parameter);if(error!=0)return error;
                error=model.AssociateParameter(endpoint,section,angle);if(error!=0)return error;
                if(angle==lower&&imprint.LowerEvent==0){imprint.LowerEvent=endpoint+1;imprint.NativeLocatorEnds|=1;}
                else if(angle==upper&&imprint.UpperEvent==0){imprint.UpperEvent=endpoint+1;imprint.NativeLocatorEnds|=2;}
                else return ParasolidConstants.PK_ERROR_cant_do_intersect;
            }
            if(imprint.NativeLocatorEnds!=3||imprint.LowerEvent==imprint.UpperEvent)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            cuts[cutCount++]=new(){First=face,Second=-1,Cut=imprint};owned[ownedCount++]=imprint;
            TraceRollingNetwork?.Invoke(new(){Stage=RollingNetworkStage.Subdivision,Source=split.Before,Surface=band.Surface,Curve=section,Vertex=split.Vertex,
                First=split.StationLower,Last=split.StationUpper,Relation=SurfacePairRelation.CurveContact});
        }
        return 0;
    }

    private static int AppendRollingDomainEdge(CurveTag curve,double first,double last,BufferOffset start,BufferOffset end,
        Span<ModelingEdge> edges,Span<EdgeSlot> originals,ref BufferCount count,out BufferOffset index)
    {
        index=-1;if(count==edges.Length||!(last>first))return ParasolidConstants.PK_ERROR_not_implemented;
        index=count;originals[count]=-1;edges[count++]=new(){Curve=curve,Start=first,End=last,StartVertex=start,EndVertex=end};return 0;
    }
}
