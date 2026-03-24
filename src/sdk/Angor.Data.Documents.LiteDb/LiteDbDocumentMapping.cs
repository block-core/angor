using Angor.Data.Documents.Models;
using LiteDB;

public class LiteDbDocumentMapping
{
    public static void ConfigureMappings()
    {
        BsonMapper.Global.Entity<BaseDocument>()
            .Id(x => x.Id)
            .Field(x => x.CreatedAt, "created_at")
            .Field(x => x.UpdatedAt, "updated_at");
    }
}