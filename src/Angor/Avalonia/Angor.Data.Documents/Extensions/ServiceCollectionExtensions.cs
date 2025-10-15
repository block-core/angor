using Microsoft.Extensions.DependencyInjection;
using Angor.Data.Documents.Interfaces;

namespace Angor.Data.Documents.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentStorage(this IServiceCollection services, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        // Register the database with profile-specific configuration
        services.AddScoped<IAngorDocumentDatabase>(provider => 
        {
            // The implementation will use the profileName to create profile-specific database files
            // e.g., "angor-documents-Default.db", "angor-documents-Alice.db", etc.
            return provider.GetRequiredService<IAngorDocumentDatabaseFactory>()
                .CreateDatabase(profileName);
        });

        // Register the factory (will be implemented in the LiteDB project)
        services.AddScoped<IAngorDocumentDatabaseFactory>();
        
        return services;
    }
}