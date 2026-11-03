using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.LocalOperations;

internal struct LocalPlane
{
    internal KernelVector3 Normal;
    internal double Distance;
}

internal struct LocalPolygon
{
    internal BufferOffset First;
    internal BufferCount Count;
}

/// <summary>
/// Exact plane intersections for bounded convex local operations. Curved blends
/// are built from these support planes; these polygons are never a faceting of
/// curved geometry. All storage belongs to the caller's command workspace.
/// </summary>
internal static class ConvexLocalModel
{
    internal const int PlaneLimit=64;
    internal const int VertexLimit=256;
    internal const int IndexLimit=1024;

    internal static KernelVector3 Add(KernelVector3 a,KernelVector3 b)=>new(){X=a.X+b.X,Y=a.Y+b.Y,Z=a.Z+b.Z};
    internal static KernelVector3 Sub(KernelVector3 a,KernelVector3 b)=>new(){X=a.X-b.X,Y=a.Y-b.Y,Z=a.Z-b.Z};
    internal static KernelVector3 Scale(KernelVector3 a,double s)=>new(){X=a.X*s,Y=a.Y*s,Z=a.Z*s};
    internal static double Dot(KernelVector3 a,KernelVector3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static KernelVector3 Cross(KernelVector3 a,KernelVector3 b)=>new(){X=a.Y*b.Z-a.Z*b.Y,Y=a.Z*b.X-a.X*b.Z,Z=a.X*b.Y-a.Y*b.X};
    internal static double Norm(KernelVector3 v)=>Math.Sqrt(Dot(v,v));
    internal static KernelVector3 Unit(KernelVector3 v)=>Scale(v,1/Norm(v));
    internal static bool Finite(KernelVector3 v)=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z);

    internal static bool Intersect(LocalPlane a,LocalPlane b,LocalPlane c,out KernelVector3 p)
    {
        var bc=Cross(b.Normal,c.Normal);var determinant=Dot(a.Normal,bc);
        if(Math.Abs(determinant)<=1e-11){p=default;return false;}
        p=Scale(Add(Add(Scale(bc,a.Distance),Scale(Cross(c.Normal,a.Normal),b.Distance)),
            Scale(Cross(a.Normal,b.Normal),c.Distance)),1/determinant);
        return Finite(p);
    }

    internal static bool Build(ReadOnlySpan<LocalPlane> planes,Span<KernelVector3> vertices,
        Span<ulong> incidence,Span<BufferOffset> indices,Span<LocalPolygon> polygons,
        out BufferCount vertexCount,out BufferCount indexCount,double tolerance)
    {
        vertexCount=indexCount=0;
        if(planes.Length<4||planes.Length>PlaneLimit||polygons.Length<planes.Length||incidence.Length<vertices.Length)return false;
        var scale=1.0;
        for(var i=0;i<planes.Length;i++)scale=Math.Max(scale,Math.Abs(planes[i].Distance));
        // The caller's modeling tolerance is not a license to merge distinct
        // vertices. Only round-off of three-plane solves is used here.
        var epsilon=Math.Max(1e-10,scale*2e-13);
        if(!double.IsFinite(scale)||!double.IsFinite(tolerance))return false;
        polygons[..planes.Length].Clear();
        for(var i=0;i<planes.Length-2;i++)for(var j=i+1;j<planes.Length-1;j++)for(var k=j+1;k<planes.Length;k++)
        {
            if(!Intersect(planes[i],planes[j],planes[k],out var p))continue;
            var inside=true;ulong mask=0;
            for(var f=0;f<planes.Length;f++)
            {
                var residual=Dot(planes[f].Normal,p)-planes[f].Distance;
                if(residual>epsilon){inside=false;break;}
                if(Math.Abs(residual)<=epsilon)mask|=1UL<<f;
            }
            if(!inside)continue;
            var found=-1;
            for(var v=0;v<vertexCount;v++)if(Norm(Sub(vertices[v],p))<=epsilon){found=v;break;}
            if(found>=0){incidence[found]|=mask;continue;}
            if(vertexCount==vertices.Length)return false;
            vertices[vertexCount]=p;incidence[vertexCount++]=mask;
        }
        if(vertexCount<4)return false;
        Span<double> angles=stackalloc double[VertexLimit];
        for(var f=0;f<planes.Length;f++)
        {
            var first=indexCount;var centroid=new KernelVector3();
            for(var v=0;v<vertexCount;v++)if((incidence[v]&(1UL<<f))!=0)
            {
                if(indexCount==indices.Length)return false;
                indices[indexCount++]=v;centroid=Add(centroid,vertices[v]);
            }
            var count=indexCount-first;
            if(count<3){indexCount=first;continue;}
            centroid=Scale(centroid,1.0/count);
            var x=Unit(Sub(vertices[indices[first]],centroid));var y=Cross(planes[f].Normal,x);
            for(var i=0;i<count;i++)
            {
                var d=Sub(vertices[indices[first+i]],centroid);
                angles[i]=Math.Atan2(Dot(d,y),Dot(d,x));
            }
            for(var i=1;i<count;i++)
            {
                var index=indices[first+i];var angle=angles[i];var j=i;
                while(j>0&&angles[j-1]>angle){indices[first+j]=indices[first+j-1];angles[j]=angles[j-1];j--;}
                indices[first+j]=index;angles[j]=angle;
            }
            var area=0.0;
            for(var i=0;i<count;i++)area+=Dot(Cross(Sub(vertices[indices[first+i]],centroid),
                Sub(vertices[indices[first+(i+1)%count]],centroid)),planes[f].Normal);
            if(!(area>epsilon*epsilon))return false;
            polygons[f]=new(){First=first,Count=count};
        }
        // Every polygon edge must have exactly two incident support faces.
        // This also rejects open/unbounded arrangements and collapsed results.
        for(var f=0;f<planes.Length;f++)
        {
            var polygon=polygons[f];
            for(var i=0;i<polygon.Count;i++)
            {
                var a=indices[polygon.First+i];var b=indices[polygon.First+(i+1)%polygon.Count];
                var common=incidence[a]&incidence[b];
                if(System.Numerics.BitOperations.PopCount(common)!=2)return false;
            }
        }
        return true;
    }
}
