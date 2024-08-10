using System.Windows.Documents;
using System.Text.RegularExpressions;

public class MarkdownParser
{
    private static readonly Regex HeaderRegex     = new Regex(@"^(#+)\s*(.*)$");
    private static readonly Regex BoldRegex       = new Regex(@"(\*\*)([^*]+)(\*\*)");
    private static readonly Regex ItalicRegex     = new Regex(@"(\*)([^*]+)(\*)");

    public static void ParseMarkdown(FlowDocument document, string markdown)
    {
        document.Blocks.Clear();

        // Remove all '\r' characters
        markdown  = markdown.Replace("\r", string.Empty);
        var lines = markdown.Split('\n');

        foreach (var line in lines)
        {
            Paragraph paragraph = new Paragraph();

            // Handle headers (one or more #)
            var headerMatch = HeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                int headerLevel = headerMatch.Groups[1].Value.Length;
                string headerText = headerMatch.Groups[2].Value;
                paragraph.Inlines.Add(new Run(headerText) 
                { 
                    FontWeight = FontWeights.Bold,
                    FontSize   = 18,
                });
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
                var listItem = new ListItem(new Paragraph(ProcessMarkdownText(listItemText)));
                var list = new List { ListItems = { listItem } };

                // Apply padding
                var margin = new Thickness(leadingSpaces * 5, 0, 0, 0); // Adjust the multiplier as needed
                listItem.Margin = margin;

                document.Blocks.Add(list);
                continue;
            }

            // Plain text
            paragraph.Inlines.Add(ProcessMarkdownText(line));
            document.Blocks.Add(paragraph);
        }
    }

    private static Inline ProcessMarkdownText(string text)
    {
        text = text.Replace("`", string.Empty);

        var result = new Span();
        int lastIndex = 0;

        // Define regular expressions for bold and italic formatting
        var formattingMatches = new List<Match>();
        formattingMatches.AddRange(BoldRegex.Matches(text).Cast<Match>());
        formattingMatches.AddRange(ItalicRegex.Matches(text).Cast<Match>());

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
            if (BoldRegex.IsMatch(match.Value))
            {
                result.Inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold });
            }
            else if (ItalicRegex.IsMatch(match.Value)) 
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
