using CodeFlow3D.Models;

namespace CodeFlow3D.Services
{
    public interface ILayoutEngine
    {
        DiagramLayout ComputeLayout(FlowPath path);
    }
}
