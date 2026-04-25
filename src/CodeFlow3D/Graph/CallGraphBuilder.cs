using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeFlow3D.Analysis;
using CodeFlow3D.Models;
using CodeFlow3D.Services;

namespace CodeFlow3D.Graph
{
    public class CallGraphBuilder : ICallGraphBuilder
    {
        private readonly AnalyzerFactory _analyzerFactory;

        public CallGraphBuilder(AnalyzerFactory analyzerFactory)
        {
            _analyzerFactory = analyzerFactory;
        }

        public async Task<CallGraph> BuildAsync(string projectPath, IProgress<AnalysisProgress> progress, CancellationToken ct = default)
        {
            var masterGraph = new CallGraph();

            // Group files by language/analyzer
            var files = AnalyzerFactory.AllSupportedExtensions
                .SelectMany(ext => Directory.GetFiles(projectPath, $"*{ext}", SearchOption.AllDirectories))
                .Where(f => !ShouldExclude(f))
                .ToList();

            if (files.Count == 0)
                throw new InvalidOperationException($"No supported source files found in {projectPath}");

            // Check if there are C# files — use CSharp analyzer for those
            var csFiles = files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();
            var otherFiles = files.Where(f => !f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();

            if (csFiles.Count > 0)
            {
                var csAnalyzer = _analyzerFactory.GetAnalyzer(".cs");
                var csGraph = await csAnalyzer.AnalyzeAsync(projectPath, progress, ct);
                masterGraph.Merge(csGraph);
            }

            if (otherFiles.Count > 0)
            {
                var genericAnalyzer = _analyzerFactory.GetAnalyzer(".ts");
                var otherGraph = await genericAnalyzer.AnalyzeAsync(projectPath, progress, ct);
                masterGraph.Merge(otherGraph);
            }

            return masterGraph;
        }

        private static bool ShouldExclude(string filePath)
        {
            var excluded = new[] { "\\bin\\", "\\obj\\", "\\node_modules\\", "\\.git\\", "\\dist\\", "\\build\\", "__pycache__",
                                   "/bin/", "/obj/", "/node_modules/", "/.git/", "/dist/", "/build/" };
            return excluded.Any(e => filePath.Contains(e));
        }
    }
}
