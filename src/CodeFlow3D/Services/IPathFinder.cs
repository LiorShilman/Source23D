using System.Collections.Generic;
using CodeFlow3D.Models;

namespace CodeFlow3D.Services
{
    public interface IPathFinder
    {
        FlowPath FindShortestPath(CallGraph graph, string sourceId, string targetId, int maxDepth = 20);
        List<FlowPath> FindAllPaths(CallGraph graph, string sourceId, string targetId, int maxPaths = 5, int maxDepth = 20);
    }
}
