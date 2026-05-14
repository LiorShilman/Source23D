using System.Collections.Generic;

namespace CodeFlow3D.Models
{
    public class SimulationSession
    {
        public SymbolNode Function { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
        public Dictionary<string, string> ParameterValues { get; set; } = new Dictionary<string, string>();
        public List<SimulationStep> Steps { get; set; } = new List<SimulationStep>();
        public string SourceCode { get; set; }
        public int FunctionStartLine { get; set; }
        public int FunctionEndLine { get; set; }
    }
}
