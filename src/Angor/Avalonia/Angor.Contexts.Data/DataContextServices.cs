using Angor.Contexts.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Angor.Contexts.Data;

public class DataContextServices
{
    public static ServiceCollection Register(ServiceCollection services, IConfigurationRoot? configuration = null, ILogger? logger = null)
    {
        // Registering the DbContext
        services.AddDbContext<AngorDbContext>(options =>
            options.UseSqlite(configuration?.GetConnectionString("AngorConnection") ?? "Data Source=angor.db"));


        // Registering services
        services.AddScoped<INostrService, NostrService>();
        services.AddScoped<IProjectEventService, ProjectEventService>();
        
        //
        // // Registering logging
        // services.AddLogging(builder => builder.AddSerilog(logger));
        
        return services;
    }
}