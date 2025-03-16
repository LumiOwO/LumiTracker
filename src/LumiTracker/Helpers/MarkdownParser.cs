using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Swordfish.NET.Collections.Auxiliary;
using LumiTracker.Config;
using Wpf.Ui.Controls;

public class MarkdownParser
{
    private static readonly Regex HeaderRegex     
        = new Regex(@"^(#+)\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex BoldRegex       
        = new Regex(@"(\*\*)([^*]+)(\*\*)", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex     
        = new Regex(@"(\*)([^*]+)(\*)", RegexOptions.Compiled);
    private static readonly Regex ColorRegex      
        = new Regex(@"\$\{\\color\{(?<color>#[0-9a-fA-F]{6})\}\{\\textbf\{(?<text>.*?)\}\}\}\$", RegexOptions.Compiled);
    private static readonly Regex HyperlinkRegex
        = new Regex(@"\[(?<text>[^]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled);

    private static readonly double TextFontSize   = 16;
    private static readonly double HeaderFontSize = 18;

    public static void ParseMarkdown(FlowDocument document, string markdown)
    {
        ELanguage lang = Configuration.GetELanguage();

        document.Blocks.Clear();
        document.PagePadding = new Thickness(0);
        document.TextAlignment = (lang == ELanguage.en_US ? TextAlignment.Justify : TextAlignment.Left);
        document.FontSize = TextFontSize;

        // Remove all '\r' characters
        markdown  = markdown.Replace("\r", string.Empty);
        var lines = markdown.Split('\n');

        bool newline = false;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                newline = true;
                continue;
            }

            if (newline)
            {
                document.Blocks.Add(new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                });
                newline = false;
            }

            Paragraph paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0);
            paragraph.Padding = new Thickness(0);

            // Handle headers (one or more #)
            var headerMatch = HeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                // Add header
                int headerLevel = headerMatch.Groups[1].Value.Length;
                string headerText = headerMatch.Groups[2].Value;
                paragraph.Inlines.Add(new Run(headerText) 
                { 
                    FontWeight = FontWeights.Bold,
                    FontSize   = HeaderFontSize,
                });

                // Add some space before header if not at the beginning of the document
                double top = document.Blocks.IsEmpty() ? 0 : 15;
                paragraph.Margin = new Thickness(0, top, 0, 5);
                document.Blocks.Add(paragraph);
                continue;
            }

            // Handle list items (-) with indentation
            if (line.TrimStart().StartsWith("-"))
            {
                // Determine indentation level
                var leadingSpaces = line.TakeWhile(c => c == ' ').Count();
                var listItemText  = line.TrimStart('-', ' ').Trim();

                // Create list item and apply padding based on indentation level
                paragraph.Inlines.Add(ProcessMarkdownText(listItemText));
                var listItem = new ListItem(paragraph);
                var list = new List { ListItems = { listItem } };

                // Apply padding
                var margin = new Thickness(leadingSpaces * 15, 2, 0, 2); // Adjust the multiplier as needed
                list.Margin = margin;

                document.Blocks.Add(list);
                continue;
            }

            // Plain text
            paragraph.Inlines.Add(ProcessMarkdownText(line));
            document.Blocks.Add(paragraph);
        }

        // Add some space at the end of the document
        document.Blocks.Add(new Paragraph());
    }

    private static Inline ProcessMarkdownText(string text)
    {
        text = text.Replace("`", "**");

        var result = new Span();
        result.BaselineAlignment = BaselineAlignment.TextBottom;
        int lastIndex = 0;

        // Define regular expressions for bold and italic formatting
        var formattingMatches = new List<Match>();
        formattingMatches.AddRange(BoldRegex.Matches(text).Cast<Match>());
        formattingMatches.AddRange(ItalicRegex.Matches(text).Cast<Match>());
        formattingMatches.AddRange(ColorRegex.Matches(text).Cast<Match>());
        formattingMatches.AddRange(HyperlinkRegex.Matches(text).Cast<Match>());

        // Sort matches by index to process them in order
        formattingMatches = formattingMatches.OrderBy(m => m.Index).ToList();

        // Process each match in the sorted list
        foreach (var match in formattingMatches)
        {
            // avoid overlap
            if (match.Index < lastIndex)
            {
                continue;
            }
            // Add any text between the last match and the current match
            if (match.Index > lastIndex)
            {
                result.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
            }

            // Add the formatted text based on the type of match
            if (match.Groups["url"].Success) // Hyperlink
            {
                var hyperlink = new HyperlinkButton
                {
                    Content = new TextBlock 
                    { 
                        Text = match.Groups["text"].Value,
                        FontSize = TextFontSize,
                        FontWeight = FontWeights.Bold,
                    },
                    NavigateUri = match.Groups["url"].Value,
                    Padding = new Thickness(3, 0, 3, 0),
                    Margin = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                result.Inlines.Add(hyperlink);
            }
            else if (match.Groups["color"].Success) // Colored text
            {
                result.Inlines.Add(new Run(match.Groups["text"].Value)
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(match.Groups["color"].Value)),
                });
            }
            else if (match.Value.StartsWith("**")) // Bold
            {
                result.Inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold });
            }
            else if (match.Value.StartsWith("*")) // Italic
            {
                result.Inlines.Add(new Run(match.Groups[2].Value) { FontStyle = FontStyles.Italic });
            }

            lastIndex = match.Index + match.Length;
        }

        // Add any remaining text after the last match
        if (lastIndex < text.Length)
        {
            result.Inlines.Add(new Run(text.Substring(lastIndex)));
        }

        return result;
    }
}
