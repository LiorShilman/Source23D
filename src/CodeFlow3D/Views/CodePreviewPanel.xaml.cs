using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
using CodeFlow3D.Styles;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class CodePreviewPanel : UserControl
    {
        public CodePreviewPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
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
                    ApplyHighlighting(vm.Language);
                    break;

                case nameof(CodePreviewViewModel.ScrollToLine):
                    if (vm.ScrollToLine > 0)
                        CodeEditor.ScrollToLine(vm.ScrollToLine);
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
                var definition = HighlightingManager.Instance.GetDefinition(hlName);
                VividHighlighting.ApplyVividColors(definition);
                CodeEditor.SyntaxHighlighting = definition;
            }
            else
            {
                CodeEditor.SyntaxHighlighting = null;
            }
        }
    }
}
