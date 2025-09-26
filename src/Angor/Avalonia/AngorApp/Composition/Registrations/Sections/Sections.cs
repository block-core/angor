using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Settings;
using AngorApp.Sections.Wallet;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public static class Sections
{
    // Registers application sections for the shell
    public static IServiceCollection AddAppSections(this IServiceCollection services, ILogger logger)
     {
        services.AddSingleton<IEnumerable<ISection>>(provider =>
        {
            var dynamicHome = new DynamicContentSection(new ContentSection<IHomeSectionViewModel>("Home", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IHomeSectionViewModel>())), new Icon("svg:/Assets/angor-icon.svg")))
            {
                NarrowVisibility = false,
                WideVisibility = true,
                WideSortOrder = -1,
                NarrowSortOrder = 100,
            };
            
            return
            [
                dynamicHome,
                new SectionSeparator(),
                new ContentSection<IWalletSectionViewModel>("Wallet", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IWalletSectionViewModel>())), new Icon("svg:/Assets/wallet.svg")),
                new ContentSection<IBrowseSectionViewModel>("Browse", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IBrowseSectionViewModel>())), new Icon("svg:/Assets/browse.svg")),
                new ContentSection<IPortfolioSectionViewModel>("Portfolio", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IPortfolioSectionViewModel>())), new Icon("svg:/Assets/portfolio.svg")),
                new ContentSection<IFounderSectionViewModel>("Founder", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IFounderSectionViewModel>())), new Icon("svg:/Assets/user.svg")),
                new SectionSeparator(),
                new ContentSection<ISettingsSectionViewModel>("Settings", Observable.Defer(() => Observable.Return(provider.GetRequiredService<ISettingsSectionViewModel>())), new Icon("svg:/Assets/settings.svg")),
                new CommandSection("Angor Hub", ReactiveCommand.CreateFromTask(() => provider.GetRequiredService<ILauncherService>().LaunchUri(new Uri("https://hub.angor.io"))), new Icon("svg:/Assets/browse.svg")) { IsPrimary = false },
            ];
        });
        
        return services;
    }
}
