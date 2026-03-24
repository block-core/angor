using System.Text.Json;
using Angor.Shared.Utilities;

namespace Angor.Shared.Services;

public class Serializer : ISerializer
{
    public string Serialize<T>(T data)
    {
        return JsonSerializer.Serialize(data, settings);
    }

    public T? Deserialize<T>(string str)
    {
        return JsonSerializer.Deserialize<T>(str, settings);
    }
    
    
    public static JsonSerializerOptions settings =>  new ()
    {
        // Equivalent to Formatting = Formatting.None
        WriteIndented = false,

        // Equivalent to NullValueHandling = NullValueHandling.Ignore
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

        // PropertyNamingPolicy equivalent to CamelCasePropertyNamesContractResolver
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        Converters = { new UnixDateTimeConverter() }
    };
}