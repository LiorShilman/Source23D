using System.Collections.Generic;
using System.Linq;

namespace CodeFlow3D.Models
{
    public class CallGraph
    {
        public Dictionary<string, SymbolNode> Nodes { get; set; } = new Dictionary<string, SymbolNode>();
        public List<CallEdge> Edges { get; set; } = new List<CallEdge>();

        public void AddNode(SymbolNode node)
        {
            if (!Nodes.ContainsKey(node.Id))
                Nodes[node.Id] = node;
        }

        public void AddEdge(CallEdge edge)
        {
            Edges.Add(edge);
            if (Nodes.TryGetValue(edge.CallerId, out var caller))
            {
                if (!caller.CalledSymbolIds.Contains(edge.CalleeId))
                    caller.CalledSymbolIds.Add(edge.CalleeId);
            }
        }

        public IEnumerable<SymbolNode> GetMethods() =>
            Nodes.Values.Where(n => n.Kind == SymbolKind.Method || n.Kind == SymbolKind.Constructor);

        public IEnumerable<CallEdge> GetOutgoingEdges(string nodeId) =>
            Edges.Where(e => e.CallerId == nodeId);

        public IEnumerable<CallEdge> GetIncomingEdges(string nodeId) =>
            Edges.Where(e => e.CalleeId == nodeId);

        public void Merge(CallGraph other)
        {
            foreach (var node in other.Nodes.Values)
                AddNode(node);
            foreach (var edge in other.Edges)
                AddEdge(edge);
        }
    }
}
