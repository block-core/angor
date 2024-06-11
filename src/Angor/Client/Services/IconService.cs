using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Angor.Client.Services
{
    public class IconService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _icons = new Dictionary<string, string>();

        public IconService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetIcon(string iconName, int width, int height, string color)
        {
            if (!_icons.TryGetValue(iconName, out var svgContent))
            {
                svgContent = await LoadIconFromServer(iconName);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    _icons[iconName] = svgContent;
                }
            }

            if (!string.IsNullOrEmpty(svgContent))
            {
                var svg = XElement.Parse(svgContent);
                svg.SetAttributeValue("width", $"{width}px");
                svg.SetAttributeValue("height", $"{height}px");

                // Change icon color
                foreach (var element in svg.Descendants())
                {
                    var strokeAttribute = element.Attribute("stroke");
                    if (strokeAttribute != null)
                    {
                        strokeAttribute.Value = color;
                    }

                    var fillAttribute = element.Attribute("fill");
                    if (fillAttribute != null)
                    {
                        fillAttribute.Value = color;
                    }
                }

                return svg.ToString();
            }

            return string.Empty; // or return a default icon
        }

        private async Task<string> LoadIconFromServer(string iconName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"assets/icons/{iconName}.svg");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
                // Handle exceptions (e.g., file not found, network issues)
                return string.Empty;
            }
        }

    }
}
