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
        services.AddScoped<INostrClientWrapper, NostrClientWrapper>();
        services.AddScoped<INostrService, NostrService>();
        services.AddScoped<IProjectEventService, ProjectEventService>();
        services.AddScoped<IUserEventService, UserEventService>();
        
        
        
        //
        // // Registering logging
        // services.AddLogging(builder => builder.AddSerilog(logger));
        
        return services;
    }
    
    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AngorDbContext>();
        dbContext.Database.Migrate();
    }
}