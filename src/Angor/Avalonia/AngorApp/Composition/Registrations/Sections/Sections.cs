using AngorApp.UI.Sections.New;
using AngorApp.UI.Sections.Wallet.Main;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public static class Sections
{
    // Registers application sections for the shell
    public static IServiceCollection AddAppSections(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton<IDictionary<string, ISection>>(provider => new Dictionary<string, ISection>
        {
            ["home"] = new SimpleSection()
            {
                ContentValue = new HomeView(),
                FriendlyName = "Home",
                Name = "Home",
                Icon = new Icon("fa-home"),
                SortOrder = 0,
            },
            ["wallet"] = new LazySection<IWalletSectionViewModel>("Wallet", new SectionGroup(), new Icon("svg:/Assets/wallet.svg"), () => provider.GetService<IWalletSectionViewModel>()), 
            // CreateContentSection<IWalletSectionViewModel>(provider, "Wallet", new Icon("svg:/Assets/wallet.svg"), new SectionGroup("Home", "")),
            // CreateContentSection<IBrowseSectionViewModel>(provider, "Browse", new Icon("svg:/Assets/browse.svg"), new SectionGroup("Investor", "Investor")),
            // CreateContentSection<IPortfolioSectionViewModel>(provider, "Portfolio", new Icon("svg:/Assets/portfolio.svg"), new SectionGroup("Investor", "Investor")),
            // CreateContentSection<IFounderSectionViewModel>(provider, "Founder", new Icon("svg:/Assets/user.svg"), new SectionGroup("Founder", "Founder")),
            //CreateContentSection<ISettingsSectionViewModel>(provider, "Settings", new Icon("svg:/Assets/settings.svg")),
        });

        return services;
    }
}