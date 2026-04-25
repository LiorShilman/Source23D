using System.Windows.Media.Media3D;
using CodeFlow3D.Models;

namespace CodeFlow3D.Services
{
    public interface I3DRenderer
    {
        void RenderDiagram(Model3DGroup scene, DiagramLayout layout);
        void ClearScene(Model3DGroup scene);
        void HighlightNode(string nodeId);
        void ClearHighlight();
    }
}
