using AngorApp.UI.Sections.Browse;
using AngorApp.UI.Sections.Founder;
using AngorApp.UI.Sections.Home;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Settings;
using AngorApp.UI.Sections.Wallet.Main;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.UI;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public static class Sections
{
    // Registers application sections for the shell
    public static IServiceCollection AddAppSections(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton<IEnumerable<ISection>>(provider =>
        {
            var homeSection = CreateContentSection<IHomeSectionViewModel>(provider, "Home", new Icon("svg:/Assets/angor-icon.svg"));
            var dynamicHome = new DynamicContentSection<IHomeSectionViewModel>(homeSection)
            {
                NarrowVisibility = false,
                WideVisibility = true,
                WideSortOrder = 0,
                NarrowSortOrder = 100,
            };
            
            return
            [
                dynamicHome,
                CreateContentSection<IWalletSectionViewModel>(provider, "Wallet", new Icon("svg:/Assets/wallet.svg")),
                CreateContentSection<IBrowseSectionViewModel>(provider, "Browse", new Icon("svg:/Assets/browse.svg")),
                CreateContentSection<IPortfolioSectionViewModel>(provider, "Portfolio", new Icon("svg:/Assets/portfolio.svg")),
                CreateContentSection<IFounderSectionViewModel>(provider, "Founder", new Icon("svg:/Assets/user.svg")),
                CreateContentSection<ISettingsSectionViewModel>(provider, "Settings", new Icon("svg:/Assets/settings.svg")),
            ];
        });
        
        return services;
    }

    private static ContentSection<T> CreateContentSection<T>(IServiceProvider provider, string name, Icon icon) where T : class
    {
        var content = Observable.Defer(() => Observable.Return(provider.GetRequiredService<T>()));
        return new ContentSection<T>(name, content, icon, navigator => navigator.Go(typeof(T)));
    }
}
