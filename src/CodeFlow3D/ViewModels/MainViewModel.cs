using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeFlow3D.Models;
using CodeFlow3D.Services;

namespace CodeFlow3D.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICallGraphBuilder _graphBuilder;
        private readonly IPathFinder _pathFinder;
        private CancellationTokenSource _analysisCts;

        public ProjectExplorerViewModel ProjectExplorer { get; }
        public DiagramViewModel Diagram { get; }
        public CodePreviewViewModel CodePreview { get; }

        [ObservableProperty]
        private ProjectModel _currentProject;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private double _analysisProgress;

        [ObservableProperty]
        private SymbolNode _sourceFunction;

        [ObservableProperty]
        private SymbolNode _targetFunction;

        public MainViewModel(
            ProjectExplorerViewModel projectExplorer,
            DiagramViewModel diagram,
            CodePreviewViewModel codePreview,
            ICallGraphBuilder graphBuilder,
            IPathFinder pathFinder)
        {
            ProjectExplorer = projectExplorer;
            Diagram = diagram;
            CodePreview = codePreview;
            _graphBuilder = graphBuilder;
            _pathFinder = pathFinder;

            ProjectExplorer.FileSelected += OnFileSelected;
            ProjectExplorer.SymbolSelected += OnSymbolSelected;
            Diagram.NodeClicked += OnDiagramNodeClicked;
        }

        [RelayCommand]
        private async Task OpenProjectAsync()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select project folder to analyze",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            await LoadProjectAsync(dialog.SelectedPath);
        }

        public async Task LoadProjectAsync(string path)
        {
            _analysisCts?.Cancel();
            _analysisCts = new CancellationTokenSource();
            var ct = _analysisCts.Token;

            IsAnalyzing = true;
            StatusText = "Scanning project...";

            try
            {
                var progress = new Progress<AnalysisProgress>(p =>
                {
                    AnalysisProgress = p.Percentage;
                    StatusText = $"Analyzing: {p.CurrentFile} ({p.FilesProcessed}/{p.TotalFiles})";
                });

                var graph = await Task.Run(() => _graphBuilder.BuildAsync(path, progress, ct), ct);

                var project = new ProjectModel
                {
                    RootPath = path,
                    Name = System.IO.Path.GetFileName(path),
                    CallGraph = graph,
                    TotalFunctions = 0
                };

                foreach (var node in graph.Nodes.Values)
                {
                    if (node.Kind == SymbolKind.Method || node.Kind == SymbolKind.Constructor)
                        project.TotalFunctions++;
                }

                CurrentProject = project;
                ProjectExplorer.LoadProject(project, graph);
                StatusText = $"Loaded: {project.Name} - {graph.Nodes.Count} symbols, {project.TotalFunctions} functions, {graph.Edges.Count} call edges";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Analysis cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
                AnalysisProgress = 0;
            }
        }

        [RelayCommand]
        private async Task AnalyzeFlowAsync()
        {
            if (CurrentProject?.CallGraph == null)
            {
                StatusText = "Open a project first.";
                return;
            }
            if (SourceFunction == null)
            {
                StatusText = "Select a Source function first.";
                return;
            }
            if (TargetFunction == null)
            {
                StatusText = "Select a Target function first.";
                return;
            }
            if (IsAnalyzing)
                return;

            IsAnalyzing = true;
            StatusText = $"Finding paths: {SourceFunction.DisplayName} -> {TargetFunction.DisplayName}...";

            try
            {
                var paths = await Task.Run(() =>
                    _pathFinder.FindAllPaths(CurrentProject.CallGraph, SourceFunction.Id, TargetFunction.Id));

                if (paths.Count == 0)
                {
                    StatusText = "No path found. Try increasing max depth or selecting different functions.";
                    return;
                }

                Diagram.LoadPaths(paths);
                CodePreview.UpdateStats(
                    SourceFunction.DisplayName,
                    TargetFunction.DisplayName,
                    paths[0].Depth,
                    paths[0].Steps.Count,
                    paths[0].FilesInvolved.Count,
                    paths[0].ClassesInvolved.Count,
                    paths[0].IsAsync);
                StatusText = $"Found {paths.Count} path(s), shortest: {paths[0].Depth} steps";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private async Task RunDemoAsync()
        {
            // Find the project's own source folder by walking up from the exe location
            var exePath = Assembly.GetExecutingAssembly().Location;
            var dir = System.IO.Path.GetDirectoryName(exePath);

            // Walk up to find the src/CodeFlow3D folder
            string srcFolder = null;
            var current = dir;
            for (int i = 0; i < 10 && current != null; i++)
            {
                var candidate = System.IO.Path.Combine(current, "src", "CodeFlow3D");
                if (System.IO.Directory.Exists(candidate))
                {
                    srcFolder = candidate;
                    break;
                }
                current = System.IO.Path.GetDirectoryName(current);
            }

            if (srcFolder == null)
            {
                MessageBox.Show(
                    "Could not locate the CodeFlow3D source folder.\nPlease make sure you're running from the build output.",
                    "Demo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText = "Loading demo - analyzing CodeFlow3D's own source...";
            await LoadProjectAsync(srcFolder);

            if (CurrentProject?.CallGraph == null)
                return;

            // Auto-find the most impressive Source -> Target pair
            // Try many combinations and pick the path with the most steps & distinct classes
            var methods = CurrentProject.CallGraph.GetMethods().ToList();

            // Preferred sources: entry-point-like methods that call many things
            var sourceNames = new[] { "RunDemoAsync", "OpenProjectAsync", "LoadProjectAsync", "AnalyzeFlowAsync" };
            // Preferred targets: deep internal methods in different classes
            var targetNames = new[] { "AnalyzeAsync", "DFS", "FindAllPaths", "BuildAsync",
                                       "Merge", "BuildLayout", "GetAnalyzer", "LoadFile",
                                       "ResolveInterfaceEdges", "ShouldExclude", "LoadProject" };

            FlowPath bestPath = null;
            SymbolNode bestSrc = null, bestTgt = null;
            int bestScore = 0;

            StatusText = "Demo: searching for the most impressive call path...";

            foreach (var srcName in sourceNames)
            {
                var src = methods.FirstOrDefault(m => m.Name == srcName);
                if (src == null) continue;

                foreach (var tgtName in targetNames)
                {
                    var tgt = methods.FirstOrDefault(m => m.Name == tgtName);
                    if (tgt == null || tgt.Id == src.Id) continue;
                    // Must be in a different class
                    if (tgt.ContainingType == src.ContainingType) continue;

                    var paths = await Task.Run(() =>
                        _pathFinder.FindAllPaths(CurrentProject.CallGraph, src.Id, tgt.Id, 1, 15));

                    if (paths.Count > 0)
                    {
                        var path = paths[0];
                        int distinctClasses = new System.Collections.Generic.HashSet<string>(
                            path.Steps.Select(s => s.Node.ContainingType ?? "")).Count;
                        int score = path.Steps.Count * 10 + distinctClasses * 20;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPath = path;
                            bestSrc = src;
                            bestTgt = tgt;
                        }
                    }
                }
            }

            if (bestSrc != null && bestTgt != null)
            {
                SourceFunction = bestSrc;
                TargetFunction = bestTgt;
                await AnalyzeFlowAsync();
                return;
            }

            // Fallback: pick any two connected methods in different classes
            foreach (var edge in CurrentProject.CallGraph.Edges)
            {
                if (CurrentProject.CallGraph.Nodes.TryGetValue(edge.CallerId, out var caller) &&
                    CurrentProject.CallGraph.Nodes.TryGetValue(edge.CalleeId, out var callee) &&
                    caller.Kind == SymbolKind.Method && callee.Kind == SymbolKind.Method &&
                    caller.ContainingType != callee.ContainingType)
                {
                    SourceFunction = caller;
                    TargetFunction = callee;
                    await AnalyzeFlowAsync();
                    return;
                }
            }

            StatusText = "Demo loaded. Select Source and Target functions to analyze.";
        }

        partial void OnSourceFunctionChanged(SymbolNode value)
        {
            if (value != null)
                StatusText = $"Source: {value.DisplayName}";
        }

        partial void OnTargetFunctionChanged(SymbolNode value)
        {
            if (value != null)
                StatusText = $"Target: {value.DisplayName}";
        }

        private void OnFileSelected(object sender, string filePath)
        {
            CodePreview.LoadFile(filePath);
        }

        private void OnSymbolSelected(object sender, SymbolNode symbol)
        {
            CodePreview.LoadFile(symbol.FilePath, symbol.LineStart);
        }

        private void OnDiagramNodeClicked(object sender, SymbolNode node)
        {
            CodePreview.LoadFile(node.FilePath, node.LineStart);
            StatusText = $"Viewing: {node.DisplayName} ({System.IO.Path.GetFileName(node.FilePath)}:{node.LineStart})";
        }
    }
}
