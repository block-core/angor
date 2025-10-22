using Microsoft.Extensions.DependencyInjection;
using Angor.Data.Documents.Interfaces;
using Microsoft.Extensions.Logging;

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
        services.AddScoped<IAngorDocumentDatabaseFactory>(provider => new LiteDbDocumentDatabaseFactory(
            provider.GetRequiredService<ILogger<LiteDbDocumentDatabase>>(),
            profileName));

        // Register the database with profile-specific configuration
        services.AddScoped<IAngorDocumentDatabase>(provider => 
        {
            var factory = provider.GetRequiredService<IAngorDocumentDatabaseFactory>();
            return factory.CreateDatabase(profileName);
        });
        
        services.AddScoped(typeof(IGenericDocumentCollection<>), typeof(LiteDbGenericDocumentCollection<>));

        
        return services;
    }
}