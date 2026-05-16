using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CodeFlow3D.Analysis;
using CodeFlow3D.Models;
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
            ViewModel.ProjectExplorer.SimulateRequested += OnSimulateDirectly;
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

        private void OnSimulateDirectly(object sender, SymbolNode function)
        {
            var tempService = new FunctionSimulatorService();
            var tempSession = tempService.CreateSession(function, new());

            var paramDlg = new ParameterInputDialog(function,
                tempSession?.Parameters ?? new List<ParameterInfo>())
            {
                Owner = this
            };

            if (paramDlg.ShowDialog() != true)
                return;

            ViewModel.StartSimulation(function, paramDlg.ParameterValues);
        }

        private void SimulateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentProject?.CallGraph == null)
            {
                MessageBox.Show("Open a project first (or click Demo).", "No Project",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picker = new SymbolPickerDialog(ViewModel.CurrentProject.CallGraph, "Select Function to Simulate")
            {
                Owner = this
            };

            if (picker.ShowDialog() != true || picker.SelectedSymbol == null)
                return;

            var function = picker.SelectedSymbol;

            // Build a temporary session to discover parameters
            var tempService = new FunctionSimulatorService();
            var tempSession = tempService.CreateSession(function, new());

            var paramDlg = new ParameterInputDialog(function,
                tempSession?.Parameters ?? new List<ParameterInfo>())
            {
                Owner = this
            };

            if (paramDlg.ShowDialog() != true)
                return;

            ViewModel.StartSimulation(function, paramDlg.ParameterValues);
        }
    }
}
