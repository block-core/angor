using HtmlAgilityPack;
using System.Text;

namespace Angor.Client.Services
{
    public class HtmlStripperService : IHtmlStripperService
    {
        private readonly string[] blockTags = { "br", "h1", "h2", "h3", "h4", "h5", "h6", "p", "div", "section", "article", "footer", "header", "main" };

        public string StripHtmlTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(input);

            // Use a StringBuilder to build the cleaned text
            var cleanedText = new StringBuilder();

            foreach (var node in doc.DocumentNode.ChildNodes)
            {
                ProcessNode(node, cleanedText);
            }

            // Replace multiple consecutive <br> tags with a single one
            var result = cleanedText.ToString();
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<br>\s*){2,}", "<br>");

            return result;
        }

        private void ProcessNode(HtmlNode node, StringBuilder output)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                // Append the text content of the node
                AppendText(node.InnerText, output);
            }
            else if (node.NodeType == HtmlNodeType.Element)
            {
                if (blockTags.Contains(node.Name.ToLower()))
                {
                    // Add a line break before processing the content of block tags
                    output.Append("<br>");
                    foreach (var child in node.ChildNodes)
                    {
                        ProcessNode(child, output);
                    }
                    output.Append("<br>");
                }
                else
                {
                    // Recursively process other element nodes
                    foreach (var child in node.ChildNodes)
                    {
                        ProcessNode(child, output);
                    }
                }
            }
        }

        private void AppendText(string text, StringBuilder output)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Clean up and normalize the text
                var normalizedText = text;
                output.Append(normalizedText);
            }
        }
    }
}
