using System.Collections.Generic;
using System.Linq;

namespace CodeFlow3D.Models
{
    public class FlowPath
    {
        public List<FlowStep> Steps { get; set; } = new List<FlowStep>();
        public int Depth => Steps.Count;
        public List<string> FilesInvolved => Steps.Select(s => s.Node.FilePath).Distinct().ToList();
        public List<string> ClassesInvolved => Steps.Select(s => s.Node.ContainingType).Where(c => c != null).Distinct().ToList();
        public bool IsAsync => Steps.Any(s => s.Node.IsAsync);
        public bool HasCycles { get; set; }
    }

    public class FlowStep
    {
        public SymbolNode Node { get; set; }
        public CallEdge Edge { get; set; }
        public int NestingDepth { get; set; }
    }
}
