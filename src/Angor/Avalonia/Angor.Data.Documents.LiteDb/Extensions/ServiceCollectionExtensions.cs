using Microsoft.Extensions.DependencyInjection;
using Angor.Data.Documents.Interfaces;

namespace Angor.Data.Documents.LiteDb.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLiteDbDocumentStorage(this IServiceCollection services, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        // Register the factory
        services.AddScoped<IAngorDocumentDatabaseFactory, LiteDbDocumentDatabaseFactory>();

        // Register the database with profile-specific configuration
        services.AddScoped<IAngorDocumentDatabase>(provider => 
        {
            var factory = provider.GetRequiredService<IAngorDocumentDatabaseFactory>();
            return factory.CreateDatabase(profileName);
        });
        
        return services;
    }
}