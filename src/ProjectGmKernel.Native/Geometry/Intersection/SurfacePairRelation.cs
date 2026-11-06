namespace ProjectGmKernel.Native.Geometry.Intersection;

// Area coincidence is a two-dimensional domain relation, never an empty
// intersection graph. Point/curve contact does not imply material crossing.
internal enum SurfacePairRelation:byte
{
    Unresolved, Empty, RegularCurves, PointContact, CurveContact, CoincidentArea,
}
