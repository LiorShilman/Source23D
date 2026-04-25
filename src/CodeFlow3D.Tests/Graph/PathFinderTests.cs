using System.Linq;
using Xunit;
using CodeFlow3D.Graph;
using CodeFlow3D.Models;

namespace CodeFlow3D.Tests.Graph
{
    public class PathFinderTests
    {
        private readonly PathFinder _pathFinder = new PathFinder();

        private CallGraph CreateSimpleGraph()
        {
            var graph = new CallGraph();

            graph.AddNode(new SymbolNode { Id = "A", Name = "MethodA", Kind = SymbolKind.Method, ContainingType = "ClassA" });
            graph.AddNode(new SymbolNode { Id = "B", Name = "MethodB", Kind = SymbolKind.Method, ContainingType = "ClassB" });
            graph.AddNode(new SymbolNode { Id = "C", Name = "MethodC", Kind = SymbolKind.Method, ContainingType = "ClassC" });
            graph.AddNode(new SymbolNode { Id = "D", Name = "MethodD", Kind = SymbolKind.Method, ContainingType = "ClassD" });

            graph.AddEdge(new CallEdge { CallerId = "A", CalleeId = "B" });
            graph.AddEdge(new CallEdge { CallerId = "B", CalleeId = "C" });
            graph.AddEdge(new CallEdge { CallerId = "C", CalleeId = "D" });
            graph.AddEdge(new CallEdge { CallerId = "A", CalleeId = "C" }); // shortcut A->C

            return graph;
        }

        [Fact]
        public void FindShortestPath_ReturnsShortestRoute()
        {
            var graph = CreateSimpleGraph();
            var path = _pathFinder.FindShortestPath(graph, "A", "D");

            Assert.NotNull(path);
            Assert.True(path.Depth <= 4);
            Assert.Equal("A", path.Steps.First().Node.Id);
            Assert.Equal("D", path.Steps.Last().Node.Id);
        }

        [Fact]
        public void FindAllPaths_ReturnsMultiplePaths()
        {
            var graph = CreateSimpleGraph();
            var paths = _pathFinder.FindAllPaths(graph, "A", "D");

            Assert.True(paths.Count >= 2);
            // All paths should start at A and end at D
            foreach (var path in paths)
            {
                Assert.Equal("A", path.Steps.First().Node.Id);
                Assert.Equal("D", path.Steps.Last().Node.Id);
            }
        }

        [Fact]
        public void FindShortestPath_ReturnsNull_WhenNoPath()
        {
            var graph = CreateSimpleGraph();
            var path = _pathFinder.FindShortestPath(graph, "D", "A");

            Assert.Null(path);
        }

        [Fact]
        public void FindAllPaths_RespectsMaxDepth()
        {
            var graph = CreateSimpleGraph();
            var paths = _pathFinder.FindAllPaths(graph, "A", "D", maxDepth: 2);

            // A->C->D is 2 hops, should be found
            // A->B->C->D is 3 hops, should NOT be found
            foreach (var path in paths)
            {
                Assert.True(path.Depth <= 3); // 3 nodes = 2 hops
            }
        }

        [Fact]
        public void FindAllPaths_RespectsMaxPaths()
        {
            var graph = CreateSimpleGraph();
            var paths = _pathFinder.FindAllPaths(graph, "A", "D", maxPaths: 1);

            Assert.Single(paths);
        }

        [Fact]
        public void FindAllPaths_HandlesNonexistentNodes()
        {
            var graph = CreateSimpleGraph();
            var paths = _pathFinder.FindAllPaths(graph, "X", "Y");

            Assert.Empty(paths);
        }
    }
}
