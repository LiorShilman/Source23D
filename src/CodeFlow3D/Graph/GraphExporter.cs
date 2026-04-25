using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeFlow3D.Models;

namespace CodeFlow3D.Graph
{
    public static class GraphExporter
    {
        public static string ToPlantUml(FlowPath path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@startuml");
            sb.AppendLine("skinparam style strictuml");

            var participants = new HashSet<string>();
            foreach (var step in path.Steps)
            {
                var name = step.Node.ContainingType ?? step.Node.Name;
                if (participants.Add(name))
                    sb.AppendLine($"participant \"{name}\"");
            }

            for (int i = 0; i < path.Steps.Count - 1; i++)
            {
                var caller = path.Steps[i].Node.ContainingType ?? path.Steps[i].Node.Name;
                var callee = path.Steps[i + 1].Node.ContainingType ?? path.Steps[i + 1].Node.Name;
                var method = path.Steps[i + 1].Node.Name;
                sb.AppendLine($"\"{caller}\" -> \"{callee}\": {method}()");
            }

            sb.AppendLine("@enduml");
            return sb.ToString();
        }

        public static string ToDot(CallGraph graph)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph CallGraph {");
            sb.AppendLine("  rankdir=LR;");
            sb.AppendLine("  node [shape=box, style=filled, fillcolor=\"#1A1D2E\", fontcolor=\"#E8EAFF\", color=\"#2A3050\"];");
            sb.AppendLine("  edge [color=\"#9B59FF\"];");

            foreach (var edge in graph.Edges)
            {
                var caller = graph.Nodes.ContainsKey(edge.CallerId) ? graph.Nodes[edge.CallerId].DisplayName : edge.CallerId;
                var callee = graph.Nodes.ContainsKey(edge.CalleeId) ? graph.Nodes[edge.CalleeId].DisplayName : edge.CalleeId;
                sb.AppendLine($"  \"{caller}\" -> \"{callee}\";");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string ToJson(FlowPath path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"depth\": {path.Depth},");
            sb.AppendLine($"  \"hasAsync\": {path.IsAsync.ToString().ToLower()},");
            sb.AppendLine("  \"steps\": [");

            for (int i = 0; i < path.Steps.Count; i++)
            {
                var step = path.Steps[i];
                var comma = i < path.Steps.Count - 1 ? "," : "";
                sb.AppendLine($"    {{ \"name\": \"{step.Node.DisplayName}\", \"file\": \"{step.Node.FilePath?.Replace("\\", "\\\\")}\", \"line\": {step.Node.LineStart} }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
