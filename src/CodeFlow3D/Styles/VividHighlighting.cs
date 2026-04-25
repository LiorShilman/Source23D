using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeFlow3D.Styles
{
    public static class VividHighlighting
    {
        public static void ApplyVividColors(IHighlightingDefinition definition)
        {
            if (definition == null) return;

            foreach (var color in definition.NamedHighlightingColors)
            {
                ApplyColorOverride(color);
            }
        }

        private static void ApplyColorOverride(HighlightingColor color)
        {
            var name = color.Name?.ToLowerInvariant() ?? "";

            // Keywords: bright magenta/pink
            if (name.Contains("keyword") || name.Contains("modifier") || name.Contains("visibility")
                || name.Contains("getterset") || name.Contains("contextual"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xFF, 0x79, 0xC6)); // #FF79C6 bright pink
                color.FontWeight = FontWeights.Bold;
                return;
            }

            // Strings: bright green
            if (name.Contains("string") || name.Contains("char"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x50, 0xFA, 0x7B)); // #50FA7B green
                return;
            }

            // Comments: medium gray-blue (visible but not dominant)
            if (name.Contains("comment"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x6C, 0x7B, 0x95)); // #6C7B95
                color.FontStyle = System.Windows.FontStyles.Italic;
                return;
            }

            // Numbers: bright orange
            if (name.Contains("number") || name.Contains("digit"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xFF, 0xB8, 0x6C)); // #FFB86C orange
                return;
            }

            // Types, classes, namespaces: bright cyan
            if (name.Contains("type") || name.Contains("class") || name.Contains("namespace")
                || name.Contains("reference") || name.Contains("nameoftype"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x8B, 0xE9, 0xFD)); // #8BE9FD cyan
                return;
            }

            // Preprocessor: bright yellow
            if (name.Contains("preprocessor") || name.Contains("directive"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xF1, 0xFA, 0x8C)); // #F1FA8C yellow
                return;
            }

            // Method calls: bright blue
            if (name.Contains("method") || name.Contains("function"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x66, 0xD9, 0xEF)); // #66D9EF bright blue
                return;
            }

            // Punctuation, operators
            if (name.Contains("punctuation") || name.Contains("operator"))
            {
                color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xE8, 0xEA, 0xFF)); // white
                return;
            }
        }
    }

    public class SimpleHighlightingBrush : HighlightingBrush
    {
        private readonly SolidColorBrush _brush;

        public SimpleHighlightingBrush(Color color)
        {
            _brush = new SolidColorBrush(color);
            _brush.Freeze();
        }

        public override Brush GetBrush(ITextRunConstructionContext context)
        {
            return _brush;
        }

        public override string ToString()
        {
            return _brush.Color.ToString();
        }
    }
}
