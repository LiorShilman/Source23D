using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private CallGraph _callGraph;
        private List<SymbolNode> _allFunctionsCache = new List<SymbolNode>();

        public void LoadProject(ProjectModel project, CallGraph graph)
        {
            _callGraph = graph;
            FileTree.Clear();
            AllFunctions.Clear();
            _allFunctionsCache.Clear();

            var root = BuildFileTree(project.RootPath, graph);
            foreach (var child in root.Children)
                FileTree.Add(child);

            _allFunctionsCache = graph.GetMethods().OrderBy(m => m.DisplayName).ToList();
            foreach (var fn in _allFunctionsCache)
                AllFunctions.Add(fn);

            var langStats = _allFunctionsCache.GroupBy(f => f.Language ?? "unknown")
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            ProjectStats = $"{graph.Nodes.Count} symbols | {_allFunctionsCache.Count} functions | {string.Join(", ", langStats)}";
        }

        partial void OnSearchTextChanged(string value)
        {
            AllFunctions.Clear();
            var filtered = string.IsNullOrWhiteSpace(value)
                ? _allFunctionsCache
                : _allFunctionsCache.Where(f =>
                    f.DisplayName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var fn in filtered)
                AllFunctions.Add(fn);
        }

        partial void OnSelectedFileNodeChanged(FileTreeNode value)
        {
            if (value?.IsFile == true)
                FileSelected?.Invoke(this, value.FullPath);
        }

        [RelayCommand]
        private void SelectSymbol(SymbolNode symbol)
        {
            if (symbol != null)
                SymbolSelected?.Invoke(this, symbol);
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
    }

    public class FileTreeNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
        public string Language { get; set; }
        public ObservableCollection<FileTreeNode> Children { get; set; } = new ObservableCollection<FileTreeNode>();

        public string Icon => IsFile ? GetFileIcon() : "\U0001F4C1";

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
