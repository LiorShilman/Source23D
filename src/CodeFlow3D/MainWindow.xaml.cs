using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CodeFlow3D.ViewModels;
using CodeFlow3D.Views;

namespace CodeFlow3D
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<MainViewModel>();
        }

        private void SourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentProject?.CallGraph == null)
            {
                MessageBox.Show("Please open a project first.", "No Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new SymbolPickerDialog(ViewModel.CurrentProject.CallGraph, "Select Source Function")
            {
                Owner = this
            };

            if (picker.ShowDialog() == true)
                ViewModel.SourceFunction = picker.SelectedSymbol;
        }

        private void TargetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentProject?.CallGraph == null)
            {
                MessageBox.Show("Please open a project first.", "No Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new SymbolPickerDialog(ViewModel.CurrentProject.CallGraph, "Select Target Function")
            {
                Owner = this
            };

            if (picker.ShowDialog() == true)
                ViewModel.TargetFunction = picker.SelectedSymbol;
        }
    }
}
