using System;
using System.IO;
using CodeFlow3D.Services;

namespace CodeFlow3D.Analysis
{
    public class AnalyzerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public AnalyzerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IProjectAnalyzer GetAnalyzer(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".cs":
                    return (IProjectAnalyzer)_serviceProvider.GetService(typeof(CSharpAnalyzer));
                default:
                    return (IProjectAnalyzer)_serviceProvider.GetService(typeof(GenericAnalyzer));
            }
        }

        public static string GetLanguage(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".cs": return "csharp";
                case ".ts": case ".tsx": return "typescript";
                case ".js": case ".jsx": return "javascript";
                case ".py": return "python";
                case ".java": return "java";
                case ".cpp": case ".h": case ".hpp": return "cpp";
                default: return "unknown";
            }
        }

        public static readonly string[] AllSupportedExtensions =
        {
            ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".cpp", ".h", ".hpp"
        };
    }
}
