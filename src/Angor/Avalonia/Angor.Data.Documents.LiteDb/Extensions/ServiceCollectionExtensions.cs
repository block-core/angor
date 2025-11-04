using System;
using Angor.Contests.CrossCutting;
using Angor.Data.Documents.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLiteDbDocumentStorage(this IServiceCollection services, ProfileContext profileContext)
    {
        ArgumentNullException.ThrowIfNull(profileContext);

        services.AddSingleton(profileContext);

        services.AddScoped<IAngorDocumentDatabaseFactory>(provider => new LiteDbDocumentDatabaseFactory(
            provider.GetRequiredService<ILogger<LiteDbDocumentDatabase>>(),
            provider.GetRequiredService<IApplicationStorage>(),
            profileContext));

        services.AddScoped<IAngorDocumentDatabase>(provider =>
        {
            var factory = provider.GetRequiredService<IAngorDocumentDatabaseFactory>();
            return factory.CreateDatabase(profileContext.ProfileName);
        });

        services.AddScoped(typeof(IGenericDocumentCollection<>), typeof(LiteDbGenericDocumentCollection<>));

        return services;
    }
}
