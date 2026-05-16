using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private void FunctionList_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ProjectExplorerViewModel vm) return;

            // Hit-test to find the ListBoxItem that was clicked
            var lb = (ListBox)sender;
            var hit = lb.InputHitTest(e.GetPosition(lb)) as DependencyObject;
            ListBoxItem item = null;
            while (hit != null && item == null)
            {
                if (hit is ListBoxItem li) item = li;
                else hit = VisualTreeHelper.GetParent(hit);
            }

            if (item?.DataContext is not SymbolNode symbol) return;

            item.IsSelected = true;

            var menuStyle = (Style)FindResource("DarkContextMenu");
            var itemStyle = (Style)FindResource("DarkMenuItem");
            var sepStyle  = (Style)FindResource("DarkSeparator");

            var menu = new ContextMenu { Style = menuStyle };

            var setSource = new MenuItem
            {
                Header     = "▶  Set as Source",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 232)),
                Style      = itemStyle,
            };
            setSource.Click += (_, __) => vm.SetAsSourceCommand.Execute(symbol);

            var setTarget = new MenuItem
            {
                Header     = "■  Set as Target",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 184, 0)),
                Style      = itemStyle,
            };
            setTarget.Click += (_, __) => vm.SetAsTargetCommand.Execute(symbol);

            var simulate = new MenuItem
            {
                Header     = "⚡  Simulate",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 255, 160)),
                Style      = itemStyle,
            };
            simulate.Click += (_, __) => vm.SimulateFunctionCommand.Execute(symbol);

            menu.Items.Add(setSource);
            menu.Items.Add(setTarget);
            menu.Items.Add(new Separator { Style = sepStyle });
            menu.Items.Add(simulate);

            item.ContextMenu = menu;
            menu.PlacementTarget = item;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}
