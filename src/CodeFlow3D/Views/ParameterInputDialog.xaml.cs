using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CodeFlow3D.Models;

namespace CodeFlow3D.Views
{
    public partial class ParameterInputDialog : Window
    {
        public Dictionary<string, string> ParameterValues { get; } = new Dictionary<string, string>();

        private readonly ObservableCollection<ParamRow> _rows = new ObservableCollection<ParamRow>();

        public ParameterInputDialog(SymbolNode function, List<ParameterInfo> parameters)
        {
            InitializeComponent();
            FunctionNameBlock.Text = $"{function.ContainingType}.{function.Name}()";
            ParamsHost.ItemsSource = _rows;

            if (parameters.Count == 0)
            {
                NoParamsBlock.Visibility = Visibility.Visible;
                return;
            }

            foreach (var p in parameters)
                _rows.Add(new ParamRow { Name = p.Name, TypeName = p.TypeName, Value = p.DefaultValue ?? "" });
        }

        private void SimulateButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows)
                ParameterValues[row.Name] = row.Value ?? "";

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public class ParamRow : INotifyPropertyChanged
        {
            private string _value;
            public string Name { get; set; }
            public string TypeName { get; set; }
            public string Value
            {
                get => _value;
                set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
