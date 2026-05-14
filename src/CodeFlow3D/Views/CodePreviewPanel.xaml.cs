using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using CodeFlow3D.Styles;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class CodePreviewPanel : UserControl
    {
        private SimLineHighlighter _highlighter;

        public CodePreviewPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) =>
            {
                _highlighter = new SimLineHighlighter();
                CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_highlighter);
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CodePreviewViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is CodePreviewViewModel newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var vm = (CodePreviewViewModel)sender;
            switch (e.PropertyName)
            {
                case nameof(CodePreviewViewModel.FileContent):
                    CodeEditor.Text = vm.FileContent;
                    _highlighter?.ClearExecuted();
                    CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                    ApplyHighlighting(vm.Language);
                    break;

                case nameof(CodePreviewViewModel.ScrollToLine):
                    if (vm.ScrollToLine > 0)
                        CodeEditor.ScrollToLine(vm.ScrollToLine);
                    break;

                case nameof(CodePreviewViewModel.HighlightLineStart):
                    if (_highlighter != null && vm.HighlightLineStart > 0)
                    {
                        _highlighter.SetCurrentLine(vm.HighlightLineStart);
                        CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                        CodeEditor.ScrollToLine(vm.HighlightLineStart);
                    }
                    break;
            }
        }

        private void ApplyHighlighting(string language)
        {
            string hlName = null;
            switch (language)
            {
                case "C#": hlName = "C#"; break;
                case "JavaScript": case "TypeScript": hlName = "JavaScript"; break;
                case "Python": hlName = "Python"; break;
                case "Java": hlName = "Java"; break;
                case "C++": hlName = "C++"; break;
                case "XML": hlName = "XML"; break;
            }
            if (hlName != null)
            {
                var def = HighlightingManager.Instance.GetDefinition(hlName);
                VividHighlighting.ApplyVividColors(def);
                CodeEditor.SyntaxHighlighting = def;
            }
            else
            {
                CodeEditor.SyntaxHighlighting = null;
            }
        }
    }

    internal class SimLineHighlighter : IBackgroundRenderer
    {
        private int _currentLine;
        private readonly HashSet<int> _executed = new HashSet<int>();

        public KnownLayer Layer => KnownLayer.Background;

        public void SetCurrentLine(int line)
        {
            if (_currentLine > 0)
                _executed.Add(_currentLine);
            _currentLine = line;
        }

        public void ClearExecuted()
        {
            _executed.Clear();
            _currentLine = 0;
        }

        public void Draw(ICSharpCode.AvalonEdit.Rendering.TextView textView,
                         DrawingContext drawingContext)
        {
            if (textView.Document == null) return;
            textView.EnsureVisualLines();
            var lines = textView.Document.Lines;

            // Draw executed lines (dim trail)
            foreach (var ln in _executed)
            {
                if (ln == _currentLine || ln < 1 || ln > lines.Count) continue;
                DrawLine(textView, drawingContext, lines[ln - 1],
                    Color.FromArgb(22, 0, 180, 200),
                    Color.FromArgb(0, 0, 0, 0));
            }

            // Draw current line (bright highlight)
            if (_currentLine >= 1 && _currentLine <= lines.Count)
            {
                DrawLine(textView, drawingContext, lines[_currentLine - 1],
                    Color.FromArgb(65, 0, 210, 255),
                    Color.FromArgb(160, 0, 180, 220));
            }
        }

        private static void DrawLine(
            ICSharpCode.AvalonEdit.Rendering.TextView textView,
            DrawingContext dc,
            ICSharpCode.AvalonEdit.Document.DocumentLine line,
            Color bg, Color border)
        {
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
            {
                var r = new System.Windows.Rect(0, rect.Top, textView.ActualWidth, rect.Height);
                if (bg.A > 0)
                    dc.DrawRectangle(new SolidColorBrush(bg), null, r);
                if (border.A > 0)
                {
                    var pen = new Pen(new SolidColorBrush(border), 1.5) { DashStyle = DashStyles.Solid };
                    dc.DrawLine(pen,
                        new System.Windows.Point(0, rect.Top),
                        new System.Windows.Point(textView.ActualWidth, rect.Top));
                }
            }
        }
    }
}
