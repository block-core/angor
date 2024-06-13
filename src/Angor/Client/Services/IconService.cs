using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Angor.Client.Services
{
    public class IconService
    {
        private readonly Dictionary<string, string> _icons = new Dictionary<string, string>();

        public IconService()
        {
            LoadIcons();
        }

        public Task<string> GetIcon(string iconName, int width, int height, string color)
        {
            if (_icons.TryGetValue(iconName, out var svgContent))
            {
                if (!string.IsNullOrEmpty(svgContent))
                {
                    var svg = XElement.Parse(svgContent);
                    svg.SetAttributeValue("width", $"{width}px");
                    svg.SetAttributeValue("height", $"{height}px");

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

                    return Task.FromResult(svg.ToString());
                }
            }

            return Task.FromResult(string.Empty); 
        }

        private void LoadIcons()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                if (resourceName.EndsWith(".svg"))
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    using (var reader = new StreamReader(stream))
                    {
                        // Extract the icon name from the resource name
                        var iconName = resourceName.Split('.')
                                                   .Reverse()
                                                   .Skip(1)
                                                   .First();
                        _icons[iconName] = reader.ReadToEnd();
                    }
                }
            }
        }

    }
}
