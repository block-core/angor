using AngorApp.Core;
using AngorApp.Sections;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Separator = AngorApp.Sections.Shell.Separator;

namespace AngorApp.Composition;

public class SectionsFactory(IServiceProvider serviceProvider) : ISectionsFactory
{
    public IEnumerable<SectionBase> CreateSections()
    {
        return new SectionBase[]
        {
            Section.Create("Home", Get<IHomeSectionViewModel>(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            Section.Create("Wallet", Get<IWalletSectionViewModel>(), "fa-wallet"),
            Section.Create("Browse", Get<NavigationViewModel>(), "fa-magnifying-glass"),
            Section.Create("Portfolio", Get<IPortfolioSectionViewModel>(), "fa-hand-holding-dollar"),
            Section.Create("Founder", Get<IFounderSectionViewModel>(), "fa-money-bills"),
            new Separator(),
            Section.Create("Settings", () => new object(), "fa-gear"),
            new CommandSection("Angor Hub",
                ReactiveCommand.CreateFromTask(() =>
                    Get<UIServices>()().LauncherService.LaunchUri(Constants.AngorHubUri)),
                "fa-magnifying-glass") { IsPrimary = false }
        };

        Func<T> Get<T>() where T : notnull
        {
            return serviceProvider.GetRequiredService<T>;
        }
    }
}