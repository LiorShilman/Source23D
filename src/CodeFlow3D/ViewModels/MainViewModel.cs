using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeFlow3D.Analysis;
using CodeFlow3D.Models;
using CodeFlow3D.Services;

namespace CodeFlow3D.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICallGraphBuilder _graphBuilder;
        private readonly IPathFinder _pathFinder;
        private readonly FunctionSimulatorService _simulatorService;
        private CancellationTokenSource _analysisCts;

        public ProjectExplorerViewModel ProjectExplorer { get; }
        public DiagramViewModel Diagram { get; }
        public CodePreviewViewModel CodePreview { get; }
        public SimulatorViewModel Simulator { get; }

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

        [ObservableProperty]
        private bool _isSimulatorMode;

        public MainViewModel(
            ProjectExplorerViewModel projectExplorer,
            DiagramViewModel diagram,
            CodePreviewViewModel codePreview,
            SimulatorViewModel simulator,
            ICallGraphBuilder graphBuilder,
            IPathFinder pathFinder)
        {
            ProjectExplorer = projectExplorer;
            Diagram = diagram;
            CodePreview = codePreview;
            Simulator = simulator;
            _graphBuilder = graphBuilder;
            _pathFinder = pathFinder;
            _simulatorService = new FunctionSimulatorService();

            ProjectExplorer.FileSelected += OnFileSelected;
            ProjectExplorer.SymbolSelected += OnSymbolSelected;
            ProjectExplorer.SetAsSourceRequested += (_, sym) => SourceFunction = sym;
            ProjectExplorer.SetAsTargetRequested += (_, sym) => TargetFunction = sym;
            Diagram.NodeClicked += OnDiagramNodeClicked;
            Simulator.StepChanged += OnSimulatorStepChanged;
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
            // Immediately show the 3D panel — no matter what state we're in
            IsSimulatorMode = false;
            Simulator.StopPlayback();

            // If a project is already loaded, run auto-find on it
            if (CurrentProject?.CallGraph != null)
            {
                await AutoFindBestPathAsync(runSimulatorAfter: true);
                return;
            }

            // Otherwise, load the app's own source as demo
            var exePath = Assembly.GetExecutingAssembly().Location;
            var dir = System.IO.Path.GetDirectoryName(exePath);

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

            await AutoFindBestPathAsync(runSimulatorAfter: true);
        }

        private async Task AutoFindBestPathAsync(bool runSimulatorAfter = false)
        {
            var graph = CurrentProject.CallGraph;
            var methods = graph.GetMethods().ToList();

            if (methods.Count == 0)
            {
                StatusText = "No methods found in project.";
                return;
            }

            StatusText = "Searching for the most impressive call path...";

            // Find methods with most outgoing edges (good sources)
            var methodsWithOutgoing = methods
                .Select(m => new { Method = m, OutCount = graph.GetOutgoingEdges(m.Id).Count() })
                .Where(x => x.OutCount > 0)
                .OrderByDescending(x => x.OutCount)
                .Take(15)
                .Select(x => x.Method)
                .ToList();

            // Find leaf-ish methods (good targets) — methods with few outgoing but some incoming
            var incomingCount = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var edge in graph.Edges)
            {
                if (!incomingCount.ContainsKey(edge.CalleeId))
                    incomingCount[edge.CalleeId] = 0;
                incomingCount[edge.CalleeId]++;
            }

            var potentialTargets = methods
                .Where(m => incomingCount.ContainsKey(m.Id))
                .OrderByDescending(m => incomingCount[m.Id])
                .Take(20)
                .ToList();

            FlowPath bestPath = null;
            SymbolNode bestSrc = null, bestTgt = null;
            int bestScore = 0;

            foreach (var src in methodsWithOutgoing)
            {
                foreach (var tgt in potentialTargets)
                {
                    if (tgt.Id == src.Id) continue;
                    if (tgt.ContainingType == src.ContainingType) continue;

                    var paths = await Task.Run(() =>
                        _pathFinder.FindAllPaths(graph, src.Id, tgt.Id, 1, 15));

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
                // Reset to diagram view BEFORE AnalyzeFlow so the panel is Visible
                // when RenderScene runs — this ensures ZoomExtents gets a real viewport size.
                if (runSimulatorAfter) { IsSimulatorMode = false; Simulator.StopPlayback(); }
                await AnalyzeFlowAsync();
                if (runSimulatorAfter)
                    await DemoSimulatorAsync(bestSrc);
                return;
            }

            // Fallback: pick any two connected methods in different classes
            foreach (var edge in graph.Edges)
            {
                if (graph.Nodes.TryGetValue(edge.CallerId, out var caller) &&
                    graph.Nodes.TryGetValue(edge.CalleeId, out var callee) &&
                    caller.Kind == SymbolKind.Method && callee.Kind == SymbolKind.Method &&
                    caller.ContainingType != callee.ContainingType)
                {
                    SourceFunction = caller;
                    TargetFunction = callee;
                    if (runSimulatorAfter) { IsSimulatorMode = false; Simulator.StopPlayback(); }
                    await AnalyzeFlowAsync();
                    if (runSimulatorAfter)
                        await DemoSimulatorAsync(caller);
                    return;
                }
            }

            StatusText = "No cross-class paths found. Select Source and Target manually.";
        }

        private async Task DemoSimulatorAsync(SymbolNode fallbackFunction)
        {
            // ── Phase 1: play the 3D sequence diagram ────────────────────────
            // IsSimulatorMode is already false (set before AnalyzeFlowAsync ran).
            // DiagramPanel.IsVisibleChanged will re-apply ZoomExtents automatically.
            Diagram.IsPlaying = false;
            Diagram.CurrentStep = 0;
            StatusText = "Playing 3D call-flow animation...";

            // Wait for the panel to be laid-out and ZoomExtents to fire
            await Task.Delay(900);

            if (Diagram.TotalSteps > 0)
            {
                Diagram.IsPlaying = true;

                // Poll until the timer stops itself (all arrows shown), cap at 15 s
                int waited = 0;
                while (Diagram.IsPlaying && waited < 15000)
                {
                    await Task.Delay(250);
                    waited += 250;
                }
                Diagram.IsPlaying = false;
            }
            else
            {
                // No arrows to animate — show the pillars for a moment
                await Task.Delay(3000);
            }

            // ── Brief pause between phases ────────────────────────────────────
            StatusText = "Switching to Function Simulator...";
            await Task.Delay(1200);

            // ── Phase 2: run Function Simulator ──────────────────────────────
            var showcaseNode = FindShowcaseFunction();
            if (showcaseNode != null)
            {
                StartSimulation(showcaseNode, ShowcaseParams);
            }
            else
            {
                var tempSession = _simulatorService.CreateSession(fallbackFunction, new Dictionary<string, string>());
                var demoParams = new Dictionary<string, string>();
                if (tempSession != null)
                    foreach (var p in tempSession.Parameters)
                        demoParams[p.Name] = MakeDemoValue(p.Name, p.TypeName);
                StartSimulation(fallbackFunction, demoParams);
            }

            await Task.Delay(400);
            Simulator.IsPlaying = true;
        }

        private SymbolNode FindShowcaseFunction()
        {
            var graph = CurrentProject?.CallGraph;
            if (graph == null) return null;
            return graph.Nodes.Values.FirstOrDefault(n =>
                n.Name == "AnalyzeCallGraph" &&
                n.ContainingType != null && n.ContainingType.Contains("SimulatorShowcase"));
        }

        private static readonly Dictionary<string, string> ShowcaseParams = new Dictionary<string, string>
        {
            { "projectName", "\"CodeFlow3D\"" },
            { "nodeCount",   "15" },
            { "verbose",     "true" },
        };

        private static string MakeDemoValue(string name, string typeName)
        {
            var t = typeName.ToLowerInvariant();
            if (t.Contains("string") || t == "var")
            {
                if (name.Contains("path") || name.Contains("dir") || name.Contains("folder"))
                    return @"C:\MyProject\src";
                if (name.Contains("name"))  return "MyProject";
                if (name.Contains("file"))  return "Program.cs";
                return "\"example\"";
            }
            if (t.Contains("int") || t.Contains("long")) return "42";
            if (t.Contains("bool"))   return "true";
            if (t.Contains("double") || t.Contains("float")) return "3.14";
            return "null";
        }

        partial void OnSourceFunctionChanged(SymbolNode value)
        {
            if (value == null) return;
            StatusText = $"Source: {value.DisplayName}";
            if (value.FilePath != null)
                ProjectExplorer.RevealFile(value.FilePath);
        }

        partial void OnTargetFunctionChanged(SymbolNode value)
        {
            if (value == null) return;
            StatusText = $"Target: {value.DisplayName}";
            if (value.FilePath != null)
                ProjectExplorer.RevealFile(value.FilePath);
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

        private void OnSimulatorStepChanged(object sender, SimulationStep step)
        {
            if (step == null) return;
            var session = Simulator.Session;
            if (session?.Function?.FilePath != null)
                CodePreview.LoadFile(session.Function.FilePath, step.LineNumber);
        }

        // Called from MainWindow code-behind with the selected function + param values
        public void StartSimulation(SymbolNode function, System.Collections.Generic.Dictionary<string, string> paramValues)
        {
            StatusText = $"Building simulation for {function.DisplayName}...";
            var session = _simulatorService.CreateSession(function, paramValues);
            if (session == null || session.Steps.Count == 0)
            {
                StatusText = "Could not extract steps from function. Is the source file available?";
                return;
            }

            Simulator.LoadSession(session);
            IsSimulatorMode = true;
            StatusText = $"Simulating {function.DisplayName} — {session.Steps.Count} steps extracted";

            if (function.FilePath != null)
                ProjectExplorer.RevealFile(function.FilePath);
        }

        [RelayCommand]
        private void ExitSimulator()
        {
            IsSimulatorMode = false;
        }
    }
}
