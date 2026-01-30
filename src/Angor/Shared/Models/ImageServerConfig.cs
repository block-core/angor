namespace Angor.Shared.Models;

public class ImageServerConfig
{
    public required string Name { get; set; }
    public required string UploadUrl { get; set; }
    public required string Description { get; set; }
    public bool IsCustom { get; set; }

    public static List<ImageServerConfig> GetDefaultServers()
    {
        return new List<ImageServerConfig>
        {
            new ImageServerConfig
            {
                Name = "nostr.build",
                UploadUrl = "https://nostr.build/api/v2/upload/files",
                Description = "Popular Nostr image hosting service"
            },
            new ImageServerConfig
            {
                Name = "void.cat",
                UploadUrl = "https://void.cat/upload",
                Description = "Decentralized file hosting"
            },
            new ImageServerConfig
            {
                Name = "nostpic.com",
                UploadUrl = "https://nostpic.com/api/upload",
                Description = "Nostr picture hosting"
            },
            new ImageServerConfig
            {
                Name = "nostrimg.com",
                UploadUrl = "https://nostrimg.com/api/upload",
                Description = "Nostr image server"
            },
            new ImageServerConfig
            {
                Name = "Custom Server",
                UploadUrl = "",
                Description = "Use your own image server",
                IsCustom = true
            }
        };
    }
}
