using System.Text.Json;

namespace AngorApp.UI.Sections.Wallet.Main;

public class TransactionJsonViewModel(string json)
{
    public string Json => FormatJson(json);
    
    
    private string FormatJson(string json)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var element = jsonDocument.RootElement;
            return JsonSerializer.Serialize(element.Clone(), options);
        }
        catch (JsonException)
        {
            return "Invalid JSON: " + json;
        }
    }
}