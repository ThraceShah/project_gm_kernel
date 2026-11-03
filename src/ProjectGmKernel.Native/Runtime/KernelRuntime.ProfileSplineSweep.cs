using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Build the exact tensor product of a B-curve and a linear path or
    /// piecewise rational quadratic circular path. The path is the u direction
    /// and the original profile is v; no sampled fitting is involved.
    /// </summary>
    private static int CreateProfileSplineLateral(in BCurveData profile,bool spin,
        in KernelVector3 origin,in KernelVector3 direction,double extent,out SurfTag surface)
    {
        surface=0;
        if(profile.IsPeriodic!=0||profile.NVertices>512)return ParasolidConstants.PK_ERROR_not_implemented;
        var full=spin&&Math.Abs(extent)==Math.Tau;
        var segments=spin?(int)Math.Ceiling(Math.Abs(extent)/(Math.PI/2)):1;
        var pathCount=spin?2*segments+1:2;
        var dimension=spin||profile.IsRational!=0?4:3;
        Span<double> poles=stackalloc double[pathCount*profile.NVertices*dimension];
        Span<double> knots=stackalloc double[segments+1];
        Span<int> multiplicities=stackalloc int[knots.Length];
        for(var i=0;i<knots.Length;i++)
        {
            knots[i]=(double)i/segments;
            multiplicities[i]=spin?(i==0||i==segments?3:2):2;
        }
        var source=profile.PolesSpan();
        for(var i=0;i<pathCount;i++)
        {
            if(full&&i==2*segments)
            {
                // A clamped periodic circle has the same point and tangent at
                // either end. Copy its homogeneous endpoint exactly rather
                // than introducing the roundoff of sin(2*pi).
                var rowSize=profile.NVertices*dimension;
                poles[..rowSize].CopyTo(poles.Slice(i*rowSize,rowSize));
                continue;
            }
            var angle=spin?extent*i/(2*segments):0;
            var pathWeight=spin&&(i&1)!=0?Math.Cos(extent/(2*segments)):1;
            var cosine=Math.Cos(angle);var sine=Math.Sin(angle);
            for(var j=0;j<profile.NVertices;j++)
            {
                var sourceIndex=j*profile.VertexDim;
                var weight=profile.IsRational!=0?source[sourceIndex+3]:1;
                var point=Vector(source[sourceIndex],source[sourceIndex+1],source[sourceIndex+2]);
                KernelVector3 homogeneous;
                if(spin)
                {
                    var weightedOrigin=ProfileScale(origin,weight);
                    var center=ProfileAdd(weightedOrigin,ProfileScale(direction,
                        ProfileDot(ProfileSubtract(point,weightedOrigin),direction)));
                    var radial=ProfileSubtract(point,center);
                    homogeneous=ProfileAdd(ProfileScale(center,pathWeight),
                        ProfileAdd(ProfileScale(radial,cosine),ProfileScale(ProfileCross(direction,radial),sine)));
                }
                else
                    homogeneous=ProfileAdd(point,ProfileScale(direction,i*extent*weight));
                var target=(i*profile.NVertices+j)*dimension;
                poles[target]=homogeneous.X;poles[target+1]=homogeneous.Y;poles[target+2]=homogeneous.Z;
                if(dimension==4)poles[target+3]=weight*pathWeight;
            }
        }
        var profileKnots=profile.KnotsSpan();var profileMultiplicities=profile.KnotMultsSpan();
        fixed(double* vertices=poles,uKnots=knots,vKnots=profileKnots)
        fixed(int* uMultiplicities=multiplicities,vMultiplicities=profileMultiplicities)
        {
            var definition=new PK_BSURF_sf_s
            {
                u_degree=spin?2:1,v_degree=profile.Degree,n_u_vertices=pathCount,n_v_vertices=profile.NVertices,
                vertex_dim=dimension,is_rational=(byte)(dimension==4?1:0),vertex=vertices,
                form=spin?ParasolidConstants.PK_BSURF_form_revolved_c:ParasolidConstants.PK_BSURF_form_swept_c,
                n_u_knots=knots.Length,n_v_knots=profile.NKnots,u_knot=uKnots,v_knot=vKnots,
                u_knot_mult=uMultiplicities,v_knot_mult=vMultiplicities,
                u_knot_type=spin?ParasolidConstants.PK_knot_piecewise_bezier_c:ParasolidConstants.PK_knot_bezier_ends_c,
                v_knot_type=profile.KnotType,is_u_closed=(byte)(full?1:0),is_u_periodic=(byte)(full?1:0),
                is_v_closed=profile.IsClosed,self_intersecting=ParasolidConstants.PK_self_intersect_false_c,
                convexity=ParasolidConstants.PK_convexity_unset_c,
            };
            SurfTag created=0;
            var error=BSurfCreateImplementation(&definition,&created);
            surface=created;
            return error;
        }
    }
}
