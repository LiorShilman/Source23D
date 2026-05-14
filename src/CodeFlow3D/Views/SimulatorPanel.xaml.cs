using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodeFlow3D.Models;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class SimulatorPanel : UserControl
    {
        private SimulatorViewModel _vm;

        // Per-kind accent colors
        private static readonly Dictionary<SimStepKind, Color> KindColors = new Dictionary<SimStepKind, Color>
        {
            { SimStepKind.Call,       Color.FromRgb(0, 220, 255) },
            { SimStepKind.Await,      Color.FromRgb(0, 220, 255) },
            { SimStepKind.VarDecl,    Color.FromRgb(80, 245, 120) },
            { SimStepKind.Assignment, Color.FromRgb(255, 185, 100) },
            { SimStepKind.Return,     Color.FromRgb(255, 120, 200) },
            { SimStepKind.Branch,     Color.FromRgb(255, 200, 50) },
            { SimStepKind.LoopStart,  Color.FromRgb(190, 150, 255) },
        };

        private static readonly Dictionary<SimStepKind, string> KindLabels = new Dictionary<SimStepKind, string>
        {
            { SimStepKind.Call,       "CALL" },
            { SimStepKind.Await,      "AWAIT" },
            { SimStepKind.VarDecl,    "VAR" },
            { SimStepKind.Assignment, "SET" },
            { SimStepKind.Return,     "RETURN" },
            { SimStepKind.Branch,     "IF" },
            { SimStepKind.LoopStart,  "LOOP" },
        };

        public SimulatorPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SimulatorViewModel oldVm) oldVm.PropertyChanged -= OnVmChanged;
            if (e.NewValue is SimulatorViewModel newVm) { _vm = newVm; newVm.PropertyChanged += OnVmChanged; }
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SimulatorViewModel.Session):
                    if (_vm.Session != null)
                    {
                        EmptyState.Visibility = Visibility.Collapsed;
                        RenderStep(_vm.CurrentStepData);
                    }
                    break;

                case nameof(SimulatorViewModel.CurrentStep):
                case nameof(SimulatorViewModel.CurrentStepData):
                    RenderStep(_vm.CurrentStepData);
                    break;

                case nameof(SimulatorViewModel.IsPlaying):
                    PlayBtnText.Text = _vm.IsPlaying ? "⏸  Pause" : "▶  Play";
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private void RenderStep(SimulationStep step)
        {
            if (_vm?.Session == null || step == null) return;
            var session = _vm.Session;

            var color = GetKindColor(step.Kind);

            // Header
            HeaderFunc.Text = $"{session.Function.ContainingType}  ·  {session.Function.Name}()";
            HeaderStep.Text = $"Step {_vm.CurrentStep + 1} / {_vm.TotalSteps}";

            // Progress dots
            DrawProgressDots(step.Index, _vm.TotalSteps);

            // Statement card
            ApplyAccentColor(color);
            KindText.Text = GetKindLabel(step.Kind);
            StmtText.Text = step.StatementText;

            if (!string.IsNullOrEmpty(step.CalledMethodName))
            {
                CalledText.Text = step.CalledMethodName + "()";
                CalledPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CalledPanel.Visibility = Visibility.Collapsed;
            }

            LineNumText.Text = $"line {step.LineNumber}";

            // Control bar info
            StepKindBlock.Text = step.Kind.ToString();
            CalledMethodBlock.Text = step.CalledMethodName != null ? $"→ {step.CalledMethodName}()" : "";
            StepCounterBlock.Text = $"{_vm.CurrentStep + 1} / {_vm.TotalSteps}";

            // Animate card pulse first, then variables
            AnimateCardPulse(color);
            RenderVariables(step);
        }

        private void ApplyAccentColor(Color c)
        {
            AccentBrush.Color = c;
            KindBadgeBg.Color = Color.FromArgb(60, c.R, c.G, c.B);
            StmtBorderBrush.Color = Color.FromArgb(80, c.R, c.G, c.B);
        }

        // ── Progress dots ─────────────────────────────────────────────────────

        private void DrawProgressDots(int currentIdx, int total)
        {
            ProgressDots.Items.Clear();
            int n = Math.Min(total, 40);
            for (int i = 0; i < n; i++)
            {
                bool isCurrent = i == currentIdx;
                bool isDone    = i <  currentIdx;

                var dot = new Border
                {
                    Width  = isCurrent ? 10 : 6,
                    Height = isCurrent ? 10 : 6,
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(2, 2, 2, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = isCurrent ? new SolidColorBrush(Color.FromRgb(0, 220, 255))
                               : isDone    ? new SolidColorBrush(Color.FromRgb(0, 100, 145))
                                           : new SolidColorBrush(Color.FromArgb(50, 50, 70, 110)),
                };
                ProgressDots.Items.Add(dot);
            }
        }

        // ── Variable table ────────────────────────────────────────────────────

        private void RenderVariables(SimulationStep step)
        {
            VarsPanel.Children.Clear();

            if (step.Variables.Count == 0)
            {
                VarsSection.Visibility = Visibility.Collapsed;
                return;
            }

            VarsSection.Visibility = Visibility.Visible;

            // Sort: changed first, then params, then rest (stable within each group)
            var sorted = new List<VariableState>(step.Variables);
            sorted.Sort((a, b) =>
            {
                int pa = a.JustChanged ? 0 : a.IsParameter ? 1 : 2;
                int pb = b.JustChanged ? 0 : b.IsParameter ? 1 : 2;
                return pa.CompareTo(pb);
            });

            bool odd = false;
            foreach (var v in sorted)
            {
                VarsPanel.Children.Add(MakeVarRow(v, odd));
                odd = !odd;
            }
        }

        private FrameworkElement MakeVarRow(VariableState v, bool oddRow)
        {
            Color accent = VarAccentColor(v);

            // Row background
            Color rowBg = v.JustChanged
                ? Color.FromArgb(35, 255, 165, 30)
                : oddRow ? Color.FromArgb(12, 80, 100, 160) : Color.FromArgb(0, 0, 0, 0);

            var row = new Grid
            {
                Background = new SolidColorBrush(rowBg),
                ToolTip = $"{v.TypeName ?? "var"}  {v.Name}  =  {v.Value ?? "null"}",
            };

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            // Left accent bar (colored stripe for changed/param rows)
            if (v.JustChanged || v.IsParameter)
            {
                var bar = new Border
                {
                    Width = 2,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(accent),
                };
                Grid.SetColumnSpan(bar, 3);
                row.Children.Add(bar);
            }

            // Name cell
            var nameFg = v.IsParameter
                ? Color.FromRgb(110, 220, 140)
                : Color.FromRgb(185, 205, 235);
            var nameBlock = new TextBlock
            {
                Text = v.Name,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = v.JustChanged || v.IsParameter ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(nameFg),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(9, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(nameBlock, 0);
            row.Children.Add(nameBlock);

            // Type cell
            var typeBlock = new TextBlock
            {
                Text = TruncType(v.TypeName ?? ""),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 140, 165, 210)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(typeBlock, 1);
            row.Children.Add(typeBlock);

            // Value cell
            Color valueFg = v.JustChanged
                ? Color.FromRgb(255, 210, 60)
                : Color.FromRgb(130, 175, 255);
            var valueRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            valueRow.Children.Add(new TextBlock
            {
                Text = TruncValue(v.Value, 28),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(valueFg),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 4, 4, 4),
            });
            if (v.JustChanged)
                valueRow.Children.Add(new TextBlock
                {
                    Text = " ●",
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 175, 0)),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            Grid.SetColumn(valueRow, 2);
            row.Children.Add(valueRow);

            // Bottom separator line
            var sep = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(18, 100, 130, 200)),
            };
            Grid.SetColumnSpan(sep, 3);
            row.Children.Add(sep);

            if (v.JustChanged) AnimateChangedCard(row);
            return row;
        }

        private static Color VarAccentColor(VariableState v) =>
            v.JustChanged  ? Color.FromRgb(255, 165, 30)
          : v.IsParameter  ? Color.FromRgb(70, 215, 115)
                           : Color.FromRgb(80, 130, 220);

        private static string BuildVarTooltip(VariableState v)
        {
            var t = string.IsNullOrEmpty(v.TypeName) ? "" : $"({v.TypeName}) ";
            var val = string.IsNullOrEmpty(v.Value) ? "—" : v.Value;
            return $"{t}{v.Name} = {val}";
        }

        // ── Animations ────────────────────────────────────────────────────────

        private void AnimateCardPulse(Color accentColor)
        {
            var anim = new ColorAnimation(
                Color.FromArgb(140, accentColor.R, accentColor.G, accentColor.B),
                Color.FromArgb(80, accentColor.R, accentColor.G, accentColor.B),
                new Duration(TimeSpan.FromMilliseconds(400)));
            StmtBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        private static void AnimateChangedCard(FrameworkElement el)
        {
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = new Duration(TimeSpan.FromMilliseconds(280));
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = new ScaleTransform(1, 1);
            var st = (ScaleTransform)el.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.03, 1.0, dur) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.03, 1.0, dur) { EasingFunction = ease });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Color GetKindColor(SimStepKind k)
        {
            Color c;
            return KindColors.TryGetValue(k, out c) ? c : Color.FromRgb(140, 160, 210);
        }

        private static string GetKindLabel(SimStepKind k)
        {
            string s;
            return KindLabels.TryGetValue(k, out s) ? s : "►";
        }

        private static string TruncType(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Strip generic noise: Dictionary<K,V> → Dictionary
            int lt = s.IndexOf('<');
            if (lt > 0) s = s.Substring(0, lt) + "<…>";
            return s.Length > 14 ? s.Substring(0, 13) + "…" : s;
        }

        private static string TruncValue(string s, int max = 18)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            s = s.Replace("\n", " ").Replace("\r", "").Trim();
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void PlayBtn_Click(object sender, RoutedEventArgs e)     => _vm?.TogglePlayCommand.Execute(null);
        private void StepBackBtn_Click(object sender, RoutedEventArgs e)  => _vm?.StepBackCommand.Execute(null);
        private void StepFwdBtn_Click(object sender, RoutedEventArgs e)   => _vm?.StepForwardCommand.Execute(null);
        private void RestartBtn_Click(object sender, RoutedEventArgs e)   => _vm?.RestartCommand.Execute(null);
    }
}
