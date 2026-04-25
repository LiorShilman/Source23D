using System.Collections.Generic;
using System.Linq;
using CodeFlow3D.Models;
using CodeFlow3D.Services;

namespace CodeFlow3D.Graph
{
    public class PathFinder : IPathFinder
    {
        public FlowPath FindShortestPath(CallGraph graph, string sourceId, string targetId, int maxDepth = 20)
        {
            var paths = FindAllPaths(graph, sourceId, targetId, 1, maxDepth);
            return paths.FirstOrDefault();
        }

        public List<FlowPath> FindAllPaths(CallGraph graph, string sourceId, string targetId, int maxPaths = 5, int maxDepth = 20)
        {
            var results = new List<FlowPath>();
            var visited = new HashSet<string>();
            var currentPath = new List<FlowStep>();

            if (!graph.Nodes.ContainsKey(sourceId) || !graph.Nodes.ContainsKey(targetId))
                return results;

            currentPath.Add(new FlowStep
            {
                Node = graph.Nodes[sourceId],
                Edge = null,
                NestingDepth = 0
            });

            DFS(graph, sourceId, targetId, visited, currentPath, results, maxPaths, maxDepth, 0);

            // Sort by path length
            results.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return results;
        }

        private void DFS(
            CallGraph graph,
            string currentId,
            string targetId,
            HashSet<string> visited,
            List<FlowStep> currentPath,
            List<FlowPath> results,
            int maxPaths,
            int maxDepth,
            int depth)
        {
            if (results.Count >= maxPaths)
                return;

            if (depth > maxDepth)
                return;

            if (currentId == targetId && depth > 0)
            {
                var path = new FlowPath
                {
                    Steps = new List<FlowStep>(currentPath),
                    HasCycles = false
                };
                results.Add(path);
                return;
            }

            visited.Add(currentId);

            foreach (var edge in graph.GetOutgoingEdges(currentId))
            {
                if (visited.Contains(edge.CalleeId))
                    continue;

                if (!graph.Nodes.TryGetValue(edge.CalleeId, out var calleeNode))
                    continue;

                currentPath.Add(new FlowStep
                {
                    Node = calleeNode,
                    Edge = edge,
                    NestingDepth = depth + 1
                });

                DFS(graph, edge.CalleeId, targetId, visited, currentPath, results, maxPaths, maxDepth, depth + 1);

                currentPath.RemoveAt(currentPath.Count - 1);
            }

            visited.Remove(currentId);
        }
    }
}
