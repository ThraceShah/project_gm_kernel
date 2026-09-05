global using XtDocument = ProjectGmKernel.Xt.XtDocument;
global using XtNode = ProjectGmKernel.Xt.XtNode;
global using XtFieldValue = ProjectGmKernel.Xt.XtFieldValue;
global using XtVector = ProjectGmKernel.Xt.XtVector;
global using XtFieldKind = ProjectGmKernel.Xt.XtFieldKind;
global using XtSchemaDefinition = ProjectGmKernel.Xt.XtSchemaDefinition;
global using XtNodeDescriptor = ProjectGmKernel.Xt.XtNodeDescriptor;
global using XtFieldDescriptor = ProjectGmKernel.Xt.XtFieldDescriptor;
global using XtSemanticModel = ProjectGmKernel.Xt.XtSemanticModel;
global using XtSemanticKind = ProjectGmKernel.Xt.XtSemanticKind;

namespace ProjectGmKernel.Native.Runtime;

internal enum XtNodeTypes : XtNodeType
{
    Terminator=1,PartTransmitBlock=176,Body=12,Shell=13,Face=14,Loop=15,Edge=16,
    Halfedge=17,Vertex=18,Region=19,Point=29,Line=30,Circle=31,Plane=50,
    Cylinder=51,Cone=52,Sphere=53,Torus=54,
    BSplineVertices=45,KnotMultiplicities=127,KnotSet=128,BCurve=134,CurveData=135,NurbsCurve=136,
}
