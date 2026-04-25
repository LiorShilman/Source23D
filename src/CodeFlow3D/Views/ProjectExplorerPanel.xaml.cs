using System.Windows;
using System.Windows.Controls;
using CodeFlow3D.Models;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class ProjectExplorerPanel : UserControl
    {
        public ProjectExplorerPanel()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ProjectExplorerViewModel vm && e.NewValue is FileTreeNode node)
                vm.SelectedFileNode = node;
        }

        private void FunctionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProjectExplorerViewModel vm && e.AddedItems.Count > 0 && e.AddedItems[0] is SymbolNode symbol)
                vm.SelectSymbolCommand.Execute(symbol);
        }
    }
}
