using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using CodeFlow3D.Analysis;
using CodeFlow3D.Models;

namespace CodeFlow3D.Tests.Analysis
{
    public class CSharpAnalyzerTests
    {
        private readonly ITestOutputHelper _output;

        public CSharpAnalyzerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private string FindProjectSource()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "src", "CodeFlow3D");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        [Fact]
        public async Task AnalyzeOwnSource_FindsMethodsAndEdges()
        {
            var srcPath = FindProjectSource();
            if (srcPath == null)
            {
                _output.WriteLine("SKIP: Could not find source folder");
                return;
            }

            var analyzer = new CSharpAnalyzer();
            var progress = new Progress<AnalysisProgress>(p =>
                _output.WriteLine($"  {p.Phase}: {p.CurrentFile} ({p.FilesProcessed}/{p.TotalFiles})"));

            var graph = await analyzer.AnalyzeAsync(srcPath, progress);

            var methods = graph.GetMethods().ToList();
            _output.WriteLine($"\n=== RESULTS ===");
            _output.WriteLine($"Nodes: {graph.Nodes.Count}");
            _output.WriteLine($"Methods: {methods.Count}");
            _output.WriteLine($"Edges: {graph.Edges.Count}");

            _output.WriteLine($"\n=== SAMPLE METHODS ===");
            foreach (var m in methods.Take(20))
                _output.WriteLine($"  [{m.Kind}] {m.Id}  (Name={m.Name}, Type={m.ContainingType})");

            _output.WriteLine($"\n=== SAMPLE EDGES ===");
            foreach (var e in graph.Edges.Take(30))
                _output.WriteLine($"  {e.CallerId}  -->  {e.CalleeId}");

            _output.WriteLine($"\n=== METHODS WITH OUTGOING EDGES ===");
            var callers = graph.Edges.Select(e => e.CallerId).Distinct().ToList();
            _output.WriteLine($"  {callers.Count} methods have outgoing calls");

            // Check specific methods we care about for Demo
            var loadProject = methods.FirstOrDefault(m => m.Name == "LoadProjectAsync");
            var buildAsync = methods.FirstOrDefault(m => m.Name == "BuildAsync");
            var analyzeFlow = methods.FirstOrDefault(m => m.Name == "AnalyzeFlowAsync");
            var findAllPaths = methods.FirstOrDefault(m => m.Name == "FindAllPaths");

            _output.WriteLine($"\n=== DEMO CANDIDATE METHODS ===");
            _output.WriteLine($"  LoadProjectAsync: {loadProject?.Id ?? "NOT FOUND"}");
            _output.WriteLine($"  BuildAsync: {buildAsync?.Id ?? "NOT FOUND"}");
            _output.WriteLine($"  AnalyzeFlowAsync: {analyzeFlow?.Id ?? "NOT FOUND"}");
            _output.WriteLine($"  FindAllPaths: {findAllPaths?.Id ?? "NOT FOUND"}");

            if (loadProject != null)
            {
                var outgoing = graph.GetOutgoingEdges(loadProject.Id).ToList();
                _output.WriteLine($"\n  LoadProjectAsync outgoing edges ({outgoing.Count}):");
                foreach (var e in outgoing)
                    _output.WriteLine($"    -> {e.CalleeId}");
            }

            Assert.True(graph.Nodes.Count > 0, "Should find nodes");
            Assert.True(methods.Count > 0, "Should find methods");
            Assert.True(graph.Edges.Count > 0, $"Should find edges, but found 0. Methods: {methods.Count}");
        }
    }
}
