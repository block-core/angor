namespace Angor.Data.Documents.Models;

public class Document<T> : BaseDocument where T : class
{
    public T Data { get; set; } = default!;
    
    // Constructor for easy creation
    public Document() { }
    
    public Document(T data, string id)
    {
        Data = data;
        Id = id;
    }
}