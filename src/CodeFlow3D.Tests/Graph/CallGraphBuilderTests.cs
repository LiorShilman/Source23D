using Xunit;
using CodeFlow3D.Models;

namespace CodeFlow3D.Tests.Graph
{
    public class CallGraphTests
    {
        [Fact]
        public void AddNode_PreventssDuplicates()
        {
            var graph = new CallGraph();
            var node = new SymbolNode { Id = "A", Name = "MethodA" };

            graph.AddNode(node);
            graph.AddNode(node);

            Assert.Single(graph.Nodes);
        }

        [Fact]
        public void AddEdge_UpdatesCalledSymbolIds()
        {
            var graph = new CallGraph();
            graph.AddNode(new SymbolNode { Id = "A", Name = "MethodA" });
            graph.AddNode(new SymbolNode { Id = "B", Name = "MethodB" });

            graph.AddEdge(new CallEdge { CallerId = "A", CalleeId = "B" });

            Assert.Contains("B", graph.Nodes["A"].CalledSymbolIds);
        }

        [Fact]
        public void GetMethods_ReturnsOnlyMethods()
        {
            var graph = new CallGraph();
            graph.AddNode(new SymbolNode { Id = "f", Name = "file.cs", Kind = SymbolKind.File });
            graph.AddNode(new SymbolNode { Id = "m", Name = "Method", Kind = SymbolKind.Method });
            graph.AddNode(new SymbolNode { Id = "c", Name = "Ctor", Kind = SymbolKind.Constructor });

            var methods = graph.GetMethods();
            Assert.Equal(2, System.Linq.Enumerable.Count(methods));
        }

        [Fact]
        public void Merge_CombinesGraphs()
        {
            var g1 = new CallGraph();
            g1.AddNode(new SymbolNode { Id = "A", Name = "A" });

            var g2 = new CallGraph();
            g2.AddNode(new SymbolNode { Id = "B", Name = "B" });
            g2.AddEdge(new CallEdge { CallerId = "B", CalleeId = "A" });

            g1.Merge(g2);

            Assert.Equal(2, g1.Nodes.Count);
            Assert.Single(g1.Edges);
        }
    }
}
