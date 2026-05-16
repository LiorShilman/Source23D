using System;
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

        private static readonly Random _rng = new Random();
        private static readonly string[] _sampleStrings = { "\"Hello\"", "\"World\"", "\"Test\"", "\"Sample\"", "\"CodeFlow\"", "\"Alpha\"" };
        private static readonly string[] _samplePaths   = { @"""C:\Projects\MyApp""", @"""C:\src""", @"""D:\Data""" };
        private static readonly string[] _sampleNames   = { "\"MyProject\"", "\"Service\"", "\"Manager\"", "\"Handler\"" };

        private void RandomButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows)
                row.Value = MakeRandom(row.Name, row.TypeName);
        }

        private static string MakeRandom(string name, string type)
        {
            var t = (type ?? "").ToLowerInvariant();
            var n = (name ?? "").ToLowerInvariant();

            if (t.Contains("bool"))
                return _rng.Next(2) == 0 ? "true" : "false";

            if (t.Contains("int") || t.Contains("long") || t.Contains("short") || t.Contains("byte"))
                return _rng.Next(1, 101).ToString();

            if (t.Contains("double") || t.Contains("float") || t.Contains("decimal"))
                return (_rng.NextDouble() * 100).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            if (t.Contains("string") || t == "var" || t == "")
            {
                if (n.Contains("path") || n.Contains("dir") || n.Contains("folder") || n.Contains("file"))
                    return _samplePaths[_rng.Next(_samplePaths.Length)];
                if (n.Contains("name") || n.Contains("title") || n.Contains("label"))
                    return _sampleNames[_rng.Next(_sampleNames.Length)];
                return _sampleStrings[_rng.Next(_sampleStrings.Length)];
            }

            return _rng.Next(1, 50).ToString();
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
