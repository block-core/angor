using HtmlAgilityPack;

namespace Angor.Client.Services
{
    public class HtmlStripperService : IHtmlStripperService
    {
        public string StripHtmlTags(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(input);

            doc.DocumentNode.SelectNodes("//script|//style")?.ToList().ForEach(node => node.Remove());

            var output = string.Join("\n", doc.DocumentNode.SelectNodes("//text()[normalize-space()]")
                ?.Select(node => HtmlEntity.DeEntitize(node.InnerText.Trim()))
                .Where(text => !string.IsNullOrEmpty(text)) ?? Enumerable.Empty<string>());

            return output.Trim();
        }
    }

}
