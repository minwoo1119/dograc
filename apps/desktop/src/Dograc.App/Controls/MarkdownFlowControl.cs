using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Dograc.Core.Models;
using Dograc.Core.Parsing;

namespace Dograc.App.Controls;

/// <summary>
/// Reading-First WPF Markdown renderer with dynamic KDS theme brushes and clickable inline [FileName, p.N] citation badges.
/// </summary>
public sealed partial class MarkdownFlowControl : RichTextBox
{
    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(
            nameof(MarkdownText),
            typeof(string),
            typeof(MarkdownFlowControl),
            new PropertyMetadata(string.Empty, OnRenderPropertyChanged));

    public static readonly DependencyProperty EnableCitationsProperty =
        DependencyProperty.Register(
            nameof(EnableCitations),
            typeof(bool),
            typeof(MarkdownFlowControl),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public static readonly DependencyProperty CitationClickedCommandProperty =
        DependencyProperty.Register(
            nameof(CitationClickedCommand),
            typeof(ICommand),
            typeof(MarkdownFlowControl),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    [GeneratedRegex(@"(\*\*[^*]+\*\*|\*[^*]+\*|`[^`]+`)", RegexOptions.Compiled)]
    private static partial Regex InlineTokenRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$", RegexOptions.Compiled)]
    private static partial Regex OrderedListRegex();

    public MarkdownFlowControl()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        Margin = new Thickness(0);
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        IsUndoEnabled = false;
        SetResourceReference(ForegroundProperty, "Brush.TextPrimary");
        SetResourceReference(FontFamilyProperty, "Font.Primary");
    }

    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public bool EnableCitations
    {
        get => (bool)GetValue(EnableCitationsProperty);
        set => SetValue(EnableCitationsProperty, value);
    }

    public ICommand? CitationClickedCommand
    {
        get => (ICommand?)GetValue(CitationClickedCommandProperty);
        set => SetValue(CitationClickedCommandProperty, value);
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownFlowControl control)
        {
            control.RebuildDocument();
        }
    }

    private void RebuildDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 13.5,
            LineHeight = 22
        };
        doc.SetResourceReference(FlowDocument.FontFamilyProperty, "Font.Primary");
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Brush.TextPrimary");

        var text = (MarkdownText ?? string.Empty).Replace("\r\n", "\n");
        var lines = text.Split('\n');

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Fenced code block
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                var codeLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }

                if (i < lines.Length)
                {
                    i++;
                }

                doc.Blocks.Add(BuildCodeBlock(string.Join(Environment.NewLine, codeLines)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Headings
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                doc.Blocks.Add(BuildHeading(line[4..], 14));
                i++;
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                doc.Blocks.Add(BuildHeading(line[3..], 15));
                i++;
                continue;
            }
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                doc.Blocks.Add(BuildHeading(line[2..], 16.5));
                i++;
                continue;
            }

            // Blockquote
            if (line.TrimStart().StartsWith("> ", StringComparison.Ordinal))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith("> ", StringComparison.Ordinal))
                {
                    quoteLines.Add(lines[i].TrimStart()[2..]);
                    i++;
                }

                doc.Blocks.Add(BuildBlockquote(string.Join(" ", quoteLines)));
                continue;
            }

            // Unordered list
            if (IsBulletLine(line))
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(16, 4, 0, 8), Padding = new Thickness(6, 0, 0, 0) };
                while (i < lines.Length && IsBulletLine(lines[i]))
                {
                    var itemText = lines[i].TrimStart()[2..];
                    var para = CreateBodyParagraph(new Thickness(0, 0, 0, 3));
                    AppendFormattedInlines(para.Inlines, itemText);
                    list.ListItems.Add(new ListItem(para));
                    i++;
                }

                doc.Blocks.Add(list);
                continue;
            }

            // Ordered list
            if (OrderedListRegex().IsMatch(line.TrimStart()))
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Decimal, Margin = new Thickness(18, 4, 0, 8), Padding = new Thickness(6, 0, 0, 0) };
                while (i < lines.Length)
                {
                    var match = OrderedListRegex().Match(lines[i].TrimStart());
                    if (!match.Success)
                    {
                        break;
                    }

                    var para = CreateBodyParagraph(new Thickness(0, 0, 0, 3));
                    AppendFormattedInlines(para.Inlines, match.Groups[1].Value);
                    list.ListItems.Add(new ListItem(para));
                    i++;
                }

                doc.Blocks.Add(list);
                continue;
            }

            // Standard paragraph (collect consecutive non-special lines)
            var paragraphLines = new List<string>();
            while (i < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[i]) &&
                   !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal) &&
                   !lines[i].StartsWith("#", StringComparison.Ordinal) &&
                   !lines[i].TrimStart().StartsWith("> ", StringComparison.Ordinal) &&
                   !IsBulletLine(lines[i]) &&
                   !OrderedListRegex().IsMatch(lines[i].TrimStart()))
            {
                paragraphLines.Add(lines[i]);
                i++;
            }

            var bodyPara = CreateBodyParagraph(new Thickness(0, 0, 0, 8));
            for (var pIdx = 0; pIdx < paragraphLines.Count; pIdx++)
            {
                if (pIdx > 0)
                {
                    bodyPara.Inlines.Add(new LineBreak());
                }

                AppendFormattedInlines(bodyPara.Inlines, paragraphLines[pIdx]);
            }

            doc.Blocks.Add(bodyPara);
        }

        if (doc.Blocks.LastBlock is Block lastBlock)
        {
            lastBlock.Margin = new Thickness(lastBlock.Margin.Left, lastBlock.Margin.Top, lastBlock.Margin.Right, 0);
        }

        Document = doc;
    }

    private Paragraph BuildHeading(string text, double fontSize)
    {
        var para = new Paragraph
        {
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        };
        para.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextPrimary");
        AppendFormattedInlines(para.Inlines, text);
        return para;
    }

    private Block BuildBlockquote(string text)
    {
        var para = CreateBodyParagraph(new Thickness(0));
        para.FontStyle = FontStyles.Italic;
        para.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextSecondary");
        AppendFormattedInlines(para.Inlines, text);

        var section = new Section
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(10, 4, 8, 4),
            Margin = new Thickness(0, 4, 0, 8)
        };
        section.SetResourceReference(Block.BorderBrushProperty, "Brush.Primary");
        section.SetResourceReference(Block.BackgroundProperty, "Brush.SurfaceSubtle");
        section.Blocks.Add(para);
        return section;
    }

    private static Block BuildCodeBlock(string code)
    {
        var para = new Paragraph(new Run(code))
        {
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            LineHeight = 18
        };
        para.SetResourceReference(TextElement.FontFamilyProperty, "Font.Mono");
        para.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextPrimary");
        para.SetResourceReference(Block.BackgroundProperty, "Brush.SurfaceSubtle");
        para.SetResourceReference(Block.BorderBrushProperty, "Brush.Border");
        return para;
    }

    private static Paragraph CreateBodyParagraph(Thickness margin)
    {
        var para = new Paragraph { Margin = margin };
        para.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextPrimary");
        return para;
    }

    private void AppendFormattedInlines(InlineCollection target, string line)
    {
        var lastIndex = 0;
        foreach (Match match in InlineTokenRegex().Matches(line))
        {
            if (match.Index > lastIndex)
            {
                AppendTextWithOptionalCitations(target, line[lastIndex..match.Index]);
            }

            var token = match.Value;
            if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal) && token.Length > 4)
            {
                var bold = new Bold();
                AppendTextWithOptionalCitations(bold.Inlines, token[2..^2]);
                target.Add(bold);
            }
            else if (token.StartsWith('*') && token.EndsWith('*') && token.Length > 2)
            {
                var italic = new Italic();
                AppendTextWithOptionalCitations(italic.Inlines, token[1..^1]);
                target.Add(italic);
            }
            else if (token.StartsWith('`') && token.EndsWith('`') && token.Length > 2)
            {
                var codeRun = new Run($" {token[1..^1]} ")
                {
                    FontSize = 12
                };
                codeRun.SetResourceReference(TextElement.FontFamilyProperty, "Font.Mono");
                codeRun.SetResourceReference(TextElement.ForegroundProperty, "Brush.Primary");
                codeRun.SetResourceReference(TextElement.BackgroundProperty, "Brush.SurfaceSubtle");
                target.Add(codeRun);
            }
            else
            {
                AppendTextWithOptionalCitations(target, token);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < line.Length)
        {
            AppendTextWithOptionalCitations(target, line[lastIndex..]);
        }
    }

    private void AppendTextWithOptionalCitations(InlineCollection target, string text)
    {
        if (!EnableCitations)
        {
            target.Add(new Run(text));
            return;
        }

        foreach (var segment in CitationParser.SplitTextWithCitations(text))
        {
            if (!segment.IsCitation || segment.Citation is null)
            {
                target.Add(new Run(segment.Text));
                continue;
            }

            target.Add(CreateCitationBadge(segment.Citation));
        }
    }

    private InlineUIContainer CreateCitationBadge(CitationReference citation)
    {
        var label = new TextBlock
        {
            Text = citation.DisplayLabel,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Primary");

        var badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 1, 6, 1),
            Child = label
        };
        badgeBorder.SetResourceReference(Border.BackgroundProperty, "Brush.PrimarySubtle");
        badgeBorder.SetResourceReference(Border.BorderBrushProperty, "Brush.PrimaryBorder");

        var button = new Button
        {
            Content = badgeBorder,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = Cursors.Hand,
            ToolTip = $"{citation.DisplayLabel} — 클릭하여 답변 근거 확인",
            Command = CitationClickedCommand,
            CommandParameter = citation
        };

        return new InlineUIContainer(button) { BaselineAlignment = BaselineAlignment.Center };
    }

    private static bool IsBulletLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- ", StringComparison.Ordinal) ||
               trimmed.StartsWith("* ", StringComparison.Ordinal);
    }
}
