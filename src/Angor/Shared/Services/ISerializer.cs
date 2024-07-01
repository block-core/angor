namespace Angor.Shared.Services;

public interface ISerializer
{
    string Serialize<T>(T data);

    T? Deserialize<T>(string str);
}