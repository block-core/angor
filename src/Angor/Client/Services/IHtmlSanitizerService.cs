namespace Angor.Client.Services
{
    public interface IHtmlSanitizerService
    {
        /// <summary>
        /// Sanitizes HTML content to prevent XSS attacks
        /// </summary>
        /// <param name="html">HTML content to sanitize</param>
        /// <returns>Sanitized HTML content</returns>
        string Sanitize(string html);
    }
}
