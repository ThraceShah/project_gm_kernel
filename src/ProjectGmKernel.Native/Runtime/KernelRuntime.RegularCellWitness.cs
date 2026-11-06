using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Boundary jets give a local left witness. A very thin cell can need a
    // point farther from its boundary for solid winding. The UV mean is only
    // a candidate: complete loop winding must prove it belongs to this cell.
    private static bool TryRegularCellInteriorWitness(scoped ref SheetArrangement model,BufferOffset cell,out PK_UV_s uv)
    {
        uv=default;var face=model.Faces[cell];if(face.LoopCount==0)return false;
        var outer=model.Loops[face.FirstLoop];if(outer.CoedgeCount==0)return false;var count=0;
        for(var k=0;k<outer.CoedgeCount;k++)
        {
            var coedge=model.Coedges[outer.FirstCoedge+k];
            for(var sample=0;sample<4;sample++)
            {
                var error=SheetCoedgePoint(face.Surface,model.Edges[coedge.Edge],coedge.Forward,(sample+.375)/4,out var point,out _,out _);if(error!=0)return false;
                count++;uv.param[0]+=(point.param[0]-uv.param[0])/count;uv.param[1]+=(point.param[1]-uv.param[1])/count;
            }
        }
        if(!double.IsFinite(uv.param[0])||!double.IsFinite(uv.param[1]))return false;
        var view=new RegularBoundaryView(model.Edges[..model.NE],model.Coedges[..model.NC],model.Loops[..model.NL],model.Faces[..model.NF]);
        return RegularFaceContains(view,cell,uv,out var inside)==0&&inside;
    }
}
