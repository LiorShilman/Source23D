using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeFlow3D.Models;

namespace CodeFlow3D.ViewModels
{
    public partial class ProjectExplorerViewModel : ObservableObject
    {
        public event EventHandler<string> FileSelected;
        public event EventHandler<SymbolNode> SymbolSelected;
        public event EventHandler<SymbolNode> SetAsSourceRequested;
        public event EventHandler<SymbolNode> SetAsTargetRequested;
        public event EventHandler<SymbolNode> SimulateRequested;

        [ObservableProperty]
        private ObservableCollection<FileTreeNode> _fileTree = new ObservableCollection<FileTreeNode>();

        [ObservableProperty]
        private ObservableCollection<SymbolNode> _allFunctions = new ObservableCollection<SymbolNode>();

        [ObservableProperty]
        private FileTreeNode _selectedFileNode;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _projectStats = string.Empty;

        [ObservableProperty]
        private string _filteredFileName = string.Empty;

        private CallGraph _callGraph;
        private List<SymbolNode> _allFunctionsCache = new List<SymbolNode>();
        private string _fileFilter;

        public void LoadProject(ProjectModel project, CallGraph graph)
        {
            _callGraph = graph;
            _fileFilter = null;
            FilteredFileName = string.Empty;
            FileTree.Clear();
            _allFunctionsCache.Clear();

            var root = BuildFileTree(project.RootPath, graph);
            foreach (var child in root.Children)
                FileTree.Add(child);

            _allFunctionsCache = graph.GetMethods().OrderBy(m => m.DisplayName).ToList();

            var langStats = _allFunctionsCache.GroupBy(f => f.Language ?? "unknown")
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            ProjectStats = $"{graph.Nodes.Count} symbols | {_allFunctionsCache.Count} functions | {string.Join(", ", langStats)}";

            RefreshFunctions();
        }

        private void RefreshFunctions()
        {
            AllFunctions.Clear();
            var source = _allFunctionsCache;

            if (!string.IsNullOrEmpty(_fileFilter))
                source = source.Where(f => f.FilePath == _fileFilter).ToList();

            if (!string.IsNullOrWhiteSpace(SearchText))
                source = source.Where(f =>
                    f.DisplayName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var fn in source)
                AllFunctions.Add(fn);
        }

        partial void OnSearchTextChanged(string value) => RefreshFunctions();

        partial void OnSelectedFileNodeChanged(FileTreeNode value)
        {
            // Clear the programmatic reveal indicator when the user picks a different file
            if (_revealedNode != null && _revealedNode != value)
            {
                _revealedNode.IsRevealed = false;
                _revealedNode = null;
            }

            if (value?.IsFile == true)
            {
                FileSelected?.Invoke(this, value.FullPath);
                _fileFilter = value.FullPath;
                FilteredFileName = "— " + Path.GetFileName(value.FullPath);
            }
            else
            {
                _fileFilter = null;
                FilteredFileName = string.Empty;
            }
            RefreshFunctions();
        }

        [RelayCommand]
        private void SelectSymbol(SymbolNode symbol)
        {
            if (symbol != null)
                SymbolSelected?.Invoke(this, symbol);
        }

        [RelayCommand]
        private void SetAsSource(SymbolNode symbol)
        {
            if (symbol != null)
                SetAsSourceRequested?.Invoke(this, symbol);
        }

        [RelayCommand]
        private void SetAsTarget(SymbolNode symbol)
        {
            if (symbol != null)
                SetAsTargetRequested?.Invoke(this, symbol);
        }

        [RelayCommand]
        private void SimulateFunction(SymbolNode symbol)
        {
            if (symbol != null)
                SimulateRequested?.Invoke(this, symbol);
        }

        private FileTreeNode BuildFileTree(string rootPath, CallGraph graph)
        {
            var root = new FileTreeNode
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath,
                IsFile = false
            };

            var fileNodes = graph.Nodes.Values
                .Where(n => n.Kind == SymbolKind.File)
                .GroupBy(n => Path.GetDirectoryName(n.FilePath))
                .OrderBy(g => g.Key);

            var dirMap = new Dictionary<string, FileTreeNode> { { rootPath, root } };

            foreach (var node in graph.Nodes.Values.Where(n => n.FilePath != null).Select(n => n.FilePath).Distinct().OrderBy(p => p))
            {
                var relativePath = node.StartsWith(rootPath)
                    ? node.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : node;

                var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var currentPath = rootPath;
                var currentNode = root;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                    bool isFile = i == parts.Length - 1;

                    if (!dirMap.TryGetValue(currentPath, out var child))
                    {
                        child = new FileTreeNode
                        {
                            Name = parts[i],
                            FullPath = currentPath,
                            IsFile = isFile,
                            Language = isFile ? GetLanguage(parts[i]) : null
                        };
                        currentNode.Children.Add(child);
                        dirMap[currentPath] = child;
                    }
                    currentNode = child;
                }
            }

            SortTree(root);
            return root;
        }

        private void SortTree(FileTreeNode node)
        {
            var sorted = node.Children
                .OrderBy(c => c.IsFile)
                .ThenBy(c => c.Name)
                .ToList();
            node.Children.Clear();
            foreach (var c in sorted)
            {
                node.Children.Add(c);
                SortTree(c);
            }
        }

        private static string GetLanguage(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
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

        private FileTreeNode _revealedNode;

        // Expand tree ancestors and highlight the file — does NOT change the function filter or code preview.
        public void RevealFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (_revealedNode != null)
                _revealedNode.IsRevealed = false;

            ExpandAncestors(filePath, FileTree);

            _revealedNode = FindFileNode(filePath, FileTree);
            if (_revealedNode != null)
                _revealedNode.IsRevealed = true;
        }

        private static FileTreeNode FindFileNode(string filePath, IEnumerable<FileTreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.IsFile && string.Equals(n.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
                    return n;
                var found = FindFileNode(filePath, n.Children);
                if (found != null) return found;
            }
            return null;
        }

        private static bool ExpandAncestors(string filePath, IEnumerable<FileTreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.IsFile && string.Equals(n.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (ExpandAncestors(filePath, n.Children))
                {
                    n.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }
    }

    public class FileTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isExpanded;
        private bool _isRevealed;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
        public string Language { get; set; }
        public ObservableCollection<FileTreeNode> Children { get; set; } = new ObservableCollection<FileTreeNode>();

        public string Icon => IsFile ? GetFileIcon() : "\U0001F4C1";

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public bool IsRevealed
        {
            get => _isRevealed;
            set
            {
                if (_isRevealed == value) return;
                _isRevealed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRevealed)));
            }
        }

        private string GetFileIcon()
        {
            switch (Language)
            {
                case "csharp": return "\U0001F7E3";
                case "typescript": case "javascript": return "\U0001F7E1";
                case "python": return "\U0001F7E2";
                case "java": return "\U0001F7E0";
                default: return "\U0001F4C4";
            }
        }
    }
}
