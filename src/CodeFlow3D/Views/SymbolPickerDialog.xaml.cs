using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CodeFlow3D.Models;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class SymbolPickerDialog : Window
    {
        private readonly SymbolPickerViewModel _vm;

        public SymbolNode SelectedSymbol => _vm.SelectedSymbol;

        public SymbolPickerDialog(CallGraph graph, string title)
        {
            InitializeComponent();
            _vm = new SymbolPickerViewModel();
            _vm.Initialize(graph, title);
            DataContext = _vm;

            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
            };
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedSymbol != null)
                DialogResult = true;
        }

        private void SymbolList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm.SelectedSymbol != null)
                DialogResult = true;
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is PickerFolderNode node)
                _vm.SelectedFolderNode = node;
        }
    }
}
