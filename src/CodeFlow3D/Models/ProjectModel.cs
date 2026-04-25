using System.Collections.Generic;

namespace CodeFlow3D.Models
{
    public class ProjectModel
    {
        public string RootPath { get; set; }
        public string Name { get; set; }
        public List<ProjectFile> Files { get; set; } = new List<ProjectFile>();
        public CallGraph CallGraph { get; set; }
        public Dictionary<string, int> LanguageStats { get; set; } = new Dictionary<string, int>();
        public int TotalFunctions { get; set; }
    }

    public class ProjectFile
    {
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public string Language { get; set; }
        public List<SymbolNode> Symbols { get; set; } = new List<SymbolNode>();
    }
}
