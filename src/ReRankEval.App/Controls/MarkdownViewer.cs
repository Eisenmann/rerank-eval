using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ReRankEval.App.Controls;

/// <summary>
/// Code-only UserControl that renders a subset of Markdown: H1/H2/H3, code blocks,
/// unordered lists, blank lines, and plain paragraphs.
/// </summary>
public sealed class MarkdownViewer : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    static MarkdownViewer()
    {
        TextProperty.Changed.AddClassHandler<MarkdownViewer>((v, _) => v.Rebuild());
    }

    public MarkdownViewer()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        var markdown = Text;
        if (string.IsNullOrEmpty(markdown))
        {
            Content = null;
            return;
        }

        var panel = new StackPanel { Spacing = 2 };
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool inCode = false;
        var codeBuffer = new List<string>();
        string codeLang = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("```"))
            {
                if (!inCode)
                {
                    inCode = true;
                    codeLang = line.Length > 3 ? line[3..].Trim() : "";
                }
                else
                {
                    panel.Children.Add(BuildCodeBlock(codeBuffer));
                    codeBuffer.Clear();
                    inCode = false;
                }
                continue;
            }

            if (inCode) { codeBuffer.Add(rawLine.TrimEnd()); continue; }

            if (line.StartsWith("# "))
                panel.Children.Add(Heading(line[2..], 22, FontWeight.Bold, new Thickness(0, 12, 0, 4)));
            else if (line.StartsWith("## "))
                panel.Children.Add(Heading(line[3..], 18, FontWeight.SemiBold, new Thickness(0, 8, 0, 2)));
            else if (line.StartsWith("### "))
                panel.Children.Add(Heading(line[4..], 15, FontWeight.SemiBold, new Thickness(0, 6, 0, 2)));
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                panel.Children.Add(BulletItem(line[2..]));
            else if (string.IsNullOrWhiteSpace(line))
                panel.Children.Add(new Border { Height = 6 });
            else
                panel.Children.Add(Para(line));
        }

        // Flush unterminated code block
        if (inCode && codeBuffer.Count > 0)
            panel.Children.Add(BuildCodeBlock(codeBuffer));

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = panel
        };
    }

    private static TextBlock Heading(string text, double size, FontWeight weight, Thickness margin) =>
        new()
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin
        };

    private static TextBlock Para(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static StackPanel BulletItem(string text) =>
        new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 0, 0, 0),
            Children =
            {
                new TextBlock { Text = "•", VerticalAlignment = VerticalAlignment.Top },
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
            }
        };

    private static Border BuildCodeBlock(List<string> lines) =>
        new()
        {
            Background = new SolidColorBrush(Color.Parse("#1A808080")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 4),
            Child = new TextBlock
            {
                Text = string.Join("\n", lines),
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };
}
