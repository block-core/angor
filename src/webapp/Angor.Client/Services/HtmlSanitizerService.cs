using Ganss.Xss;

namespace Angor.Client.Services
{
    public class HtmlSanitizerService : IHtmlSanitizerService
    {
        private readonly HtmlSanitizer _sanitizer;
        
        public HtmlSanitizerService()
        {
            _sanitizer = new HtmlSanitizer();
        }
        
        /// <summary>
        /// Sanitizes HTML content to prevent XSS attacks
        /// </summary>
        /// <param name="html">HTML content to sanitize</param>
        /// <returns>Sanitized HTML content</returns>
        public string Sanitize(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }
            
            return _sanitizer.Sanitize(html);
        }
    }
}
