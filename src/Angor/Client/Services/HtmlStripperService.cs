using System.Text.RegularExpressions;

namespace Angor.Client.Services
{
    public class HtmlStripperService : IHtmlStripperService
    {
        public string StripHtmlTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            input = Regex.Replace(input, @"<script.*?>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            input = Regex.Replace(input, @"<style.*?>.*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            input = Regex.Replace(input, @"<([a-zA-Z][^\s>]*)(\s+[^>]*)?>", match =>
            {
                string tag = match.Groups[1].Value;
                string attributes = match.Groups[2].Value;

                attributes = Regex.Replace(attributes, @"\s+(style|class)\s*=\s*""[^""]*""", string.Empty, RegexOptions.IgnoreCase);

                return $"<{tag}{attributes}>";
            }, RegexOptions.IgnoreCase);

            string allowedTagsPattern = @"<(?!\/?(br|p|a|ul|ol|li|strong|em|b|i|u|hr|blockquote|img|div|span|table|thead|tbody|tr|td|th)\b)[^>]+>";
            input = Regex.Replace(input, allowedTagsPattern, string.Empty, RegexOptions.IgnoreCase);

            string[] blockTags = { "h1", "h2", "h3", "h4", "h5", "h6", "p", "div", "section", "article", "footer", "header", "main" };

            foreach (var tag in blockTags)
            {
                input = Regex.Replace(input, $@"<\/?{tag}[^>]*>", "<br />", RegexOptions.IgnoreCase);
            }

            input = Regex.Replace(input, @"<((?!br\s*/?)[^>]+)>", string.Empty);

            input = Regex.Replace(input, @"(\r?\n){2,}", "\n");
            input = Regex.Replace(input, @"(<br />\s*){2,}", "<br />");
            input = Regex.Replace(input, @"^\s*<br />\s*|\s*<br />\s*$", string.Empty);
            input = Regex.Replace(input, @"\s*(<br />)\s*", "$1");

            return input;
        }

    }

}
