using System.Collections.Generic;

namespace CodeFlow3D.Models
{
    public enum SymbolKind
    {
        File,
        Namespace,
        Class,
        Struct,
        Interface,
        Method,
        Property,
        Constructor
    }

    public class SymbolNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public SymbolKind Kind { get; set; }
        public string FilePath { get; set; }
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public string Language { get; set; }
        public string ContainingType { get; set; }
        public string Signature { get; set; }
        public bool IsAsync { get; set; }
        public List<SymbolNode> Children { get; set; } = new List<SymbolNode>();
        public List<string> CalledSymbolIds { get; set; } = new List<string>();

        public string DisplayName => Kind == SymbolKind.Method || Kind == SymbolKind.Constructor
            ? $"{ContainingType}.{Name}"
            : Name;

        public override string ToString() => DisplayName;
    }
}
