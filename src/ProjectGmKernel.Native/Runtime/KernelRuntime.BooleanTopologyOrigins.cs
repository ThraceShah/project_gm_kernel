using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Operation-private links. Original tags/classes/owners are captured
    // before a nested operation consumes the corresponding input topology.
    private struct BooleanTopologyOrigin
    {
        internal EntityTag Product,Original;
        internal KernelClassCode Class;
        internal BodyTag Owner;
    }

    private static int ReadIdentityBooleanOrigins(scoped ReadOnlySpan<BodyTag> inputs,
        scoped Span<BooleanTopologyOrigin> output,out BufferCount count)
    {
        count=0;
        foreach(var tag in inputs)
        {
            var body=Bodies[TagRec(tag).Slot];var face=body.FirstFaceBody;var edge=body.FirstEdgeBody;
            for(var i=0;i<body.FaceCountBody;i++,face=Faces[face].NextInBody)
            {
                if(count==output.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                output[count++]=new(){Product=Faces[face].Header.Tag,Original=Faces[face].Header.Tag,Class=ParasolidConstants.PK_CLASS_face,Owner=tag};
            }
            for(var i=0;i<body.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
            {
                if(count==output.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                output[count++]=new(){Product=Edges[edge].Header.Tag,Original=Edges[edge].Header.Tag,Class=ParasolidConstants.PK_CLASS_edge,Owner=tag};
            }
        }
        return 0;
    }

    private static int ComposeBooleanOrigins(BodyTag target,scoped ReadOnlySpan<BooleanTopologyOrigin> union,
        scoped ReadOnlySpan<BooleanTopologyOrigin> inherited,PK_TOPOL_track_r_s* tracking,PK_boolean_r_s* result)
    {
        Span<EntityTag> tags=stackalloc EntityTag[RegularInputEdges+RegularInputFaces];
        Span<KernelClassCode> classes=stackalloc KernelClassCode[tags.Length];Span<BodyTag> owners=stackalloc BodyTag[tags.Length];
        PK_TOPOL_track_r_s composed=default;var records=0;var originals=0;
        for(var pass=0;pass<2;pass++)
        {
            var index=0;var offset=0;
            for(var b=0;b<result->n_bodies;b++)
            {
                var body=Bodies[TagRec(result->bodies[b]).Slot];var edge=body.FirstEdgeBody;
                for(var e=0;e<body.EdgeCountBody;e++,edge=Edges[edge].NextInBody)
                {
                    var product=Edges[edge].Header.Tag;var count=0;
                    for(var r=0;r<tracking->n_track_records;r++)
                    {
                        var row=tracking->track_records[r];if(row.n_product_topols!=1)return ParasolidConstants.PK_ERROR_not_implemented;
                        if(row.product_topols[0]!=product)continue;
                        for(var s=0;s<row.n_original_topols;s++)
                        {
                            var mapped=false;
                            foreach(var origin in union)if(origin.Product==row.original_topols[s])
                            {mapped=true;if(!AddBooleanOrigin(origin,tags,classes,owners,ref count))return ParasolidConstants.PK_ERROR_not_implemented;}
                            if(!mapped&&!AddBooleanOrigin(new(){Original=row.original_topols[s],Class=row.original_classes[s],Owner=target},tags,classes,owners,ref count))
                                return ParasolidConstants.PK_ERROR_not_implemented;
                        }
                    }
                    // Include union intersections that survive the target
                    // subtraction, including every split native fragment.
                    foreach(var use in inherited)
                    {
                        if(use.Original==0)break;if(use.Product!=product)continue;
                        foreach(var origin in union)if(origin.Product==use.Original
                            &&!AddBooleanOrigin(origin,tags,classes,owners,ref count))return ParasolidConstants.PK_ERROR_not_implemented;
                    }
                    // Prefer a finite original edge to its own incident faces.
                    for(var s=count-1;s>=0;s--)if(classes[s]==ParasolidConstants.PK_CLASS_face)
                    {
                        var specific=false;for(var k=0;k<count;k++)specific|=owners[k]==owners[s]&&classes[k]==ParasolidConstants.PK_CLASS_edge;
                        if(specific){count--;tags[s]=tags[count];classes[s]=classes[count];owners[s]=owners[count];}
                    }
                    var multiple=false;for(var s=1;s<count;s++)multiple|=owners[s]!=owners[0];if(!multiple)continue;
                    if(pass!=0)
                    {
                        tags[..count].CopyTo(new Span<EntityTag>(composed.internal_origs+offset,count));
                        classes[..count].CopyTo(new Span<KernelClassCode>(composed.internal_classes+offset,count));
                        composed.internal_prods[index]=product;
                        composed.track_records[index]=new(){track=ParasolidConstants.PK_TOPOL_track_derive_c,n_original_topols=count,
                            original_topols=composed.internal_origs+offset,original_classes=composed.internal_classes+offset,
                            n_product_topols=1,product_topols=composed.internal_prods+index};
                    }
                    index++;offset+=count;
                }
            }
            if(pass!=0)break;records=index;originals=offset;
            if(records==0)break;
            var block=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(
                (nuint)(records*sizeof(PK_TOPOL_track_record_r_s)+(2*originals+records)*sizeof(EntityTag)));
            if(block==null)return ParasolidConstants.PK_ERROR_memory_full;
            var source=(EntityTag*)(block+records);
            composed=new(){n_track_records=records,track_records=block,internal_origs=source,internal_classes=source+originals,internal_prods=source+2*originals};
        }
        TopolTrackResultFree(tracking);*tracking=composed;return 0;
    }

    private static bool AddBooleanOrigin(in BooleanTopologyOrigin origin,scoped Span<EntityTag> tags,
        scoped Span<KernelClassCode> classes,scoped Span<BodyTag> owners,ref BufferCount count)
    {
        if(tags[..count].Contains(origin.Original))return true;if(count==tags.Length)return false;
        tags[count]=origin.Original;classes[count]=origin.Class;owners[count++]=origin.Owner;return true;
    }

    private static int ReadPlanarEdgeOrigins(scoped ReadOnlySpan<BodyTag> products,
        scoped Span<BooleanTopologyOrigin> lineage,ref BufferCount count)
    {
        var faceCount=count;
        foreach(var body in products)
        {
            var record=Bodies[TagRec(body).Slot];var edge=record.FirstEdgeBody;
            for(var e=0;e<record.EdgeCountBody;e++,edge=Edges[edge].NextInBody)
            {
                var first=Edges[edge].FirstFinEdge;if(first<0)return ParasolidConstants.PK_ERROR_bad_loop;
                var second=Fins[first].Other;if(second<0||second==first)return ParasolidConstants.PK_ERROR_bad_loop;
                var a=Faces[Loops[Fins[first].Loop].Face].Header.Tag;var b=Faces[Loops[Fins[second].Loop].Face].Header.Tag;
                var ia=-1;var ib=-1;
                for(var f=0;f<faceCount;f++){if(lineage[f].Product==a)ia=f;if(lineage[f].Product==b)ib=f;}
                if(ia<0||ib<0)return ParasolidConstants.PK_ERROR_not_implemented;
                var left=lineage[ia];var right=lineage[ib];var tag=Edges[edge].Header.Tag;
                if(left.Owner!=right.Owner)
                {
                    if(count>lineage.Length-2)return ParasolidConstants.PK_ERROR_not_implemented;
                    left.Product=right.Product=tag;lineage[count++]=left;lineage[count++]=right;continue;
                }
                // Same-body incident faces must retain an actual finite
                // input edge. A pair of surface names alone is insufficient
                // when two faces share more than one disconnected edge.
                var sourceFace=TagRec(left.Original).Slot;var found=-1;var loop=Faces[sourceFace].FirstLoop;
                for(var l=0;l<Faces[sourceFace].LoopCount;l++,loop=Loops[loop].NextInFace)
                {
                    var fin=Loops[loop].FirstFin;
                    for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
                    {
                        var source=Fins[fin].Edge;var opposite=Fins[fin].Other;
                        if(source<0||opposite<0||Faces[Loops[Fins[opposite].Loop].Face].Header.Tag!=right.Original
                            ||!NativeLineFragmentContained(edge,source))continue;
                        if(found>=0&&found!=source)return ParasolidConstants.PK_ERROR_not_implemented;found=source;
                    }
                }
                if(found<0)return ParasolidConstants.PK_ERROR_not_implemented;
                if(count==lineage.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                lineage[count++]=new(){Product=tag,Original=Edges[found].Header.Tag,Class=ParasolidConstants.PK_CLASS_edge,Owner=left.Owner};
            }
        }
        return 0;
    }

    private static bool NativeLineFragmentContained(EdgeSlot fragment,EdgeSlot source)
    {
        var a=Curves[GetCurveSlotByTag(Edges[fragment].CurveTag)];var b=Curves[GetCurveSlotByTag(Edges[source].CurveTag)];
        if(a.Class!=CurveClass.Line||b.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(a,out var line)
            ||!TryReadExplicitSpineCurve(b,out var original)||!TryProfileEdgeRange(Edges[fragment],out var lo,out var hi)
            ||!TryProfileEdgeRange(Edges[source],out var first,out var last))return false;
        var axis=new IntervalVector(original.First);var norm=IntervalVector.Dot(axis,axis).Value;if(!(norm.Lower>0))return false;
        ReadOnlySpan<double> ends=stackalloc double[2]{lo,hi};foreach(var end in ends)
        {
            var point=new IntervalVector(line.Origin)+new IntervalVector(line.First)*end;
            var t=IntervalVector.Dot(point-new IntervalVector(original.Origin),axis).Value/norm;
            if(!t.Finite)return false;
            var station=Math.Clamp((t.Lower+t.Upper)*.5,first,last);
            var residual=point-new IntervalVector(original.Origin)-axis*station;
            if(residual.Norm().Value.Upper>OperationIntersection.DistanceTolerance)return false;
        }
        // Affine interpolation of the paired finite endpoints bounds every
        // intermediate station and the complete line correspondence error.
        return true;
    }
}
