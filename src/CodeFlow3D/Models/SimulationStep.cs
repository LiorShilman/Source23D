using System.Collections.Generic;

namespace CodeFlow3D.Models
{
    public enum SimStepKind
    {
        Statement,
        VarDecl,
        Assignment,
        Call,
        Return,
        Branch,
        LoopStart,
        Await
    }

    public class SimulationStep
    {
        public int Index { get; set; }
        public int LineNumber { get; set; }
        public string StatementText { get; set; }
        public SimStepKind Kind { get; set; }
        public string CalledMethodName { get; set; }
        public List<VariableState> Variables { get; set; } = new List<VariableState>();
        public List<string> ChangedVarNames { get; set; } = new List<string>();
        public bool IsBranchPoint { get; set; }
        public string BranchCondition { get; set; }
    }

    public class VariableState
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string Value { get; set; }
        public bool IsParameter { get; set; }
        public bool JustChanged { get; set; }
    }

    public class ParameterInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string DefaultValue { get; set; }
    }
}
