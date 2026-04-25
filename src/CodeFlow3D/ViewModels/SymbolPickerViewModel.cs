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
    public partial class SymbolPickerViewModel : ObservableObject
    {
        private List<SymbolNode> _allSymbols = new List<SymbolNode>();
        private string _projectRoot;

        [ObservableProperty]
        private string _title = "Select Function";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SymbolNode> _filteredSymbols = new ObservableCollection<SymbolNode>();

        [ObservableProperty]
        private ObservableCollection<PickerFolderNode> _folderTree = new ObservableCollection<PickerFolderNode>();

        [ObservableProperty]
        private PickerFolderNode _selectedFolderNode;

        [ObservableProperty]
        private SymbolNode _selectedSymbol;

        [ObservableProperty]
        private bool _isConfirmed;

        // The currently selected filter path (null = all files)
        private string _selectedFilterPath;

        public void Initialize(CallGraph graph, string title)
        {
            Title = title;
            _allSymbols = graph.GetMethods().OrderBy(m => m.DisplayName).ToList();

            // Determine project root from common path prefix
            var allPaths = _allSymbols.Where(s => s.FilePath != null).Select(s => s.FilePath).Distinct().ToList();
            _projectRoot = FindCommonRoot(allPaths);

            BuildFolderTree();
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        partial void OnSelectedFolderNodeChanged(PickerFolderNode value)
        {
            if (value == null)
            {
                _selectedFilterPath = null;
            }
            else
            {
                _selectedFilterPath = value.FullPath;
            }
            ApplyFilter();
        }

        [RelayCommand]
        private void Confirm()
        {
            IsConfirmed = SelectedSymbol != null;
        }

        [RelayCommand]
        private void ClearFolderFilter()
        {
            SelectedFolderNode = null;
            _selectedFilterPath = null;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredSymbols.Clear();

            var filtered = _allSymbols.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText;
                filtered = filtered.Where(s =>
                    s.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (s.Signature != null && s.Signature.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (_selectedFilterPath != null)
            {
                filtered = filtered.Where(s =>
                    s.FilePath != null && s.FilePath.StartsWith(_selectedFilterPath, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var s in filtered.Take(300))
                FilteredSymbols.Add(s);
        }

        private void BuildFolderTree()
        {
            FolderTree.Clear();

            var allPaths = _allSymbols
                .Where(s => s.FilePath != null)
                .Select(s => s.FilePath)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (allPaths.Count == 0) return;

            var root = new PickerFolderNode
            {
                Name = Path.GetFileName(_projectRoot) ?? _projectRoot,
                FullPath = _projectRoot,
                IsFile = false
            };

            var dirMap = new Dictionary<string, PickerFolderNode>(StringComparer.OrdinalIgnoreCase)
            {
                { _projectRoot, root }
            };

            foreach (var filePath in allPaths)
            {
                var relativePath = filePath.StartsWith(_projectRoot, StringComparison.OrdinalIgnoreCase)
                    ? filePath.Substring(_projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : filePath;

                var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var currentPath = _projectRoot;
                var currentNode = root;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                    bool isFile = i == parts.Length - 1;

                    if (!dirMap.TryGetValue(currentPath, out var child))
                    {
                        var funcCount = isFile
                            ? _allSymbols.Count(s => s.FilePath != null && s.FilePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                            : 0;

                        child = new PickerFolderNode
                        {
                            Name = parts[i],
                            FullPath = currentPath,
                            IsFile = isFile,
                            FunctionCount = funcCount
                        };
                        currentNode.Children.Add(child);
                        dirMap[currentPath] = child;
                    }
                    currentNode = child;
                }
            }

            // Count functions per folder
            CountFolderFunctions(root);

            // Sort: folders first, then files
            SortTree(root);

            // Expand root by default
            root.IsExpanded = true;
            foreach (var child in root.Children.Where(c => !c.IsFile))
                child.IsExpanded = true;

            FolderTree.Add(root);
        }

        private int CountFolderFunctions(PickerFolderNode node)
        {
            if (node.IsFile) return node.FunctionCount;

            int total = 0;
            foreach (var child in node.Children)
                total += CountFolderFunctions(child);
            node.FunctionCount = total;
            return total;
        }

        private void SortTree(PickerFolderNode node)
        {
            var sorted = node.Children
                .OrderBy(c => c.IsFile)
                .ThenBy(c => c.Name)
                .ToList();
            node.Children.Clear();
            foreach (var c in sorted)
            {
                node.Children.Add(c);
                if (!c.IsFile)
                    SortTree(c);
            }
        }

        private static string FindCommonRoot(List<string> paths)
        {
            if (paths.Count == 0) return "";
            if (paths.Count == 1) return Path.GetDirectoryName(paths[0]) ?? "";

            var first = paths[0];
            var common = Path.GetDirectoryName(first) ?? "";

            foreach (var path in paths.Skip(1))
            {
                while (!string.IsNullOrEmpty(common) &&
                       !path.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                       !path.StartsWith(common + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                       !path.Equals(common, StringComparison.OrdinalIgnoreCase))
                {
                    common = Path.GetDirectoryName(common) ?? "";
                }
            }

            return common;
        }
    }

    public class PickerFolderNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
        public int FunctionCount { get; set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<PickerFolderNode> Children { get; set; } = new ObservableCollection<PickerFolderNode>();

        public string DisplayText => IsFile
            ? $"{Name}  ({FunctionCount})"
            : $"{Name}  ({FunctionCount})";
    }
}
