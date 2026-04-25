using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeFlow3D.ViewModels
{
    public partial class CodePreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fileContent = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _language = string.Empty;

        [ObservableProperty]
        private int _scrollToLine;

        [ObservableProperty]
        private int _highlightLineStart;

        [ObservableProperty]
        private int _highlightLineEnd;

        [ObservableProperty]
        private string _statsText = string.Empty;

        public void LoadFile(string path, int scrollToLine = -1)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                FilePath = path;
                FileName = Path.GetFileName(path);
                Language = DetectLanguage(path);
                FileContent = File.ReadAllText(path);

                if (scrollToLine > 0)
                {
                    ScrollToLine = scrollToLine;
                    HighlightLineStart = scrollToLine;
                    HighlightLineEnd = scrollToLine;
                }
            }
            catch (Exception ex)
            {
                FileContent = $"// Error loading file: {ex.Message}";
            }
        }

        public void HighlightRange(int start, int end)
        {
            HighlightLineStart = start;
            HighlightLineEnd = end;
            ScrollToLine = start;
        }

        public void UpdateStats(string source, string target, int depth, int steps, int files, int classes, bool isAsync)
        {
            StatsText = $"Source:  {source}\n" +
                        $"Target:  {target}\n" +
                        $"Depth:   {depth} levels\n" +
                        $"Steps:   {steps} calls\n" +
                        $"Files:   {files} files\n" +
                        $"Classes: {classes} classes\n" +
                        (isAsync ? "Async:   Yes" : "");
        }

        private static string DetectLanguage(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            switch (ext)
            {
                case ".cs": return "C#";
                case ".ts": case ".tsx": return "TypeScript";
                case ".js": case ".jsx": return "JavaScript";
                case ".py": return "Python";
                case ".java": return "Java";
                case ".cpp": case ".h": case ".hpp": return "C++";
                case ".xaml": case ".xml": return "XML";
                default: return "Text";
            }
        }
    }
}
