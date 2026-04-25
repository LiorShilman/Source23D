using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeFlow3D.Models;
using CodeFlow3D.Services;

namespace CodeFlow3D.Analysis
{
    public class GenericAnalyzer : IProjectAnalyzer
    {
        public string Language => "generic";
        public string[] SupportedExtensions => new[] { ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".cpp", ".h", ".hpp" };

        private static readonly Dictionary<string, Regex[]> FunctionPatterns = new Dictionary<string, Regex[]>
        {
            ["typescript"] = new[]
            {
                new Regex(@"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(", RegexOptions.Compiled),
                new Regex(@"(?:public|private|protected)?\s*(?:async\s+)?(\w+)\s*\([^)]*\)\s*(?::\s*\S+\s*)?\{", RegexOptions.Compiled),
                new Regex(@"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\(", RegexOptions.Compiled),
            },
            ["python"] = new[]
            {
                new Regex(@"def\s+(\w+)\s*\(", RegexOptions.Compiled),
                new Regex(@"async\s+def\s+(\w+)\s*\(", RegexOptions.Compiled),
            },
            ["java"] = new[]
            {
                new Regex(@"(?:public|private|protected)\s+(?:static\s+)?(?:\w+\s+)?(\w+)\s*\([^)]*\)\s*(?:throws\s+\w+)?\s*\{", RegexOptions.Compiled),
            },
            ["cpp"] = new[]
            {
                new Regex(@"(?:\w+[\s*&]+)?(\w+)\s*\([^)]*\)\s*(?:const)?\s*\{", RegexOptions.Compiled),
            },
        };

        private static readonly Regex CallPattern = new Regex(@"(\w+)\s*\(", RegexOptions.Compiled);

        public async Task<CallGraph> AnalyzeAsync(string projectPath, IProgress<AnalysisProgress> progress, CancellationToken ct = default)
        {
            var graph = new CallGraph();
            var files = AnalyzerFactory.AllSupportedExtensions
                .Where(ext => ext != ".cs")
                .SelectMany(ext => Directory.GetFiles(projectPath, $"*{ext}", SearchOption.AllDirectories))
                .Where(f => !f.Contains("node_modules") && !f.Contains("\\dist\\") && !f.Contains("__pycache__"))
                .ToList();

            int total = files.Count;
            int processed = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var language = AnalyzerFactory.GetLanguage(file);
                var content = await Task.Run(() => File.ReadAllText(file), ct);
                var lines = content.Split('\n');

                var fileNode = new SymbolNode
                {
                    Id = file,
                    Name = Path.GetFileName(file),
                    Kind = SymbolKind.File,
                    FilePath = file,
                    Language = language
                };
                graph.AddNode(fileNode);

                // Find function definitions
                var patterns = FunctionPatterns.ContainsKey(language)
                    ? FunctionPatterns[language]
                    : FunctionPatterns.Values.SelectMany(p => p).ToArray();

                var definedFunctions = new List<(string name, int line)>();

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var pattern in patterns)
                    {
                        var match = pattern.Match(lines[i]);
                        if (match.Success)
                        {
                            var funcName = match.Groups[1].Value;
                            if (IsValidFunctionName(funcName))
                            {
                                definedFunctions.Add((funcName, i + 1));
                                var funcId = $"{file}::{funcName}";
                                var funcNode = new SymbolNode
                                {
                                    Id = funcId,
                                    Name = funcName,
                                    Kind = SymbolKind.Method,
                                    FilePath = file,
                                    LineStart = i + 1,
                                    Language = language,
                                    ContainingType = Path.GetFileNameWithoutExtension(file),
                                    Signature = lines[i].Trim(),
                                    IsAsync = lines[i].Contains("async")
                                };
                                graph.AddNode(funcNode);
                                fileNode.Children.Add(funcNode);
                            }
                        }
                    }
                }

                // Find call sites (naive: look for functionName() patterns within function bodies)
                foreach (var (funcName, startLine) in definedFunctions)
                {
                    var funcId = $"{file}::{funcName}";
                    int endLine = FindFunctionEnd(lines, startLine - 1);

                    for (int i = startLine; i < endLine && i < lines.Length; i++)
                    {
                        var calls = CallPattern.Matches(lines[i]);
                        foreach (Match call in calls)
                        {
                            var calledName = call.Groups[1].Value;
                            if (calledName == funcName || !IsValidFunctionName(calledName))
                                continue;

                            // Try to resolve the call to a known function
                            var calledId = ResolveCall(graph, file, calledName);
                            if (calledId != null)
                            {
                                graph.AddEdge(new CallEdge
                                {
                                    CallerId = funcId,
                                    CalleeId = calledId,
                                    CallSiteFile = file,
                                    CallSiteLine = i + 1,
                                    Confidence = 0.6
                                });
                            }
                        }
                    }
                }

                processed++;
                progress?.Report(new AnalysisProgress
                {
                    Phase = $"Analyzing {language} files",
                    CurrentFile = Path.GetFileName(file),
                    FilesProcessed = processed,
                    TotalFiles = total
                });
            }

            return graph;
        }

        public Task<CallGraph> AnalyzeFileAsync(string filePath, CancellationToken ct = default) =>
            AnalyzeAsync(Path.GetDirectoryName(filePath), null, ct);

        private static string ResolveCall(CallGraph graph, string currentFile, string calledName)
        {
            // First look in same file
            var sameFileId = $"{currentFile}::{calledName}";
            if (graph.Nodes.ContainsKey(sameFileId))
                return sameFileId;

            // Then look across all files
            var match = graph.Nodes.Values.FirstOrDefault(n =>
                n.Kind == SymbolKind.Method && n.Name == calledName);
            return match?.Id;
        }

        private static int FindFunctionEnd(string[] lines, int startIndex)
        {
            int braceCount = 0;
            bool foundOpen = false;
            for (int i = startIndex; i < lines.Length; i++)
            {
                foreach (var ch in lines[i])
                {
                    if (ch == '{') { braceCount++; foundOpen = true; }
                    if (ch == '}') braceCount--;
                    if (foundOpen && braceCount == 0) return i + 1;
                }
            }
            return Math.Min(startIndex + 50, lines.Length);
        }

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "if", "else", "for", "while", "switch", "case", "return", "new", "typeof", "sizeof",
            "catch", "throw", "try", "finally", "class", "struct", "enum", "interface", "namespace",
            "import", "export", "from", "require", "console", "print", "var", "let", "const", "this"
        };

        private static bool IsValidFunctionName(string name) =>
            name.Length > 1 && !Keywords.Contains(name) && char.IsLetter(name[0]);
    }
}
