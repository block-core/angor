using AngorApp.Core;
using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell.Sections;
using AngorApp.Sections.Wallet;
using AngorApp.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.Avalonia.Controls.Navigation;
using Separator = AngorApp.Sections.Shell.Sections.Separator;

namespace AngorApp.Composition;

public class SectionsFactory(IServiceProvider serviceProvider) : ISectionsFactory
{
    public IEnumerable<SectionBase> CreateSections()
    {
        return
        [
            Section.Create("Home", Get<IHomeSectionViewModel>, "svg:/Assets/angor-icon.svg"),
            new Separator(),
            Section.Create("Wallet", Get<IWalletSectionViewModel>, "fa-wallet"),
            Section.Create("Browse", GetWithNavigation<IBrowseSectionViewModel>, "fa-magnifying-glass"),
            Section.Create("Portfolio", Get<IPortfolioSectionViewModel>, "fa-hand-holding-dollar"),
            Section.Create("Founder", Get<IFounderSectionViewModel>, "fa-money-bills"),
            new Separator(),
            Section.Create("Settings", () => new object(), "fa-gear"),
            new CommandSection("Angor Hub",
                ReactiveCommand.CreateFromTask(() =>
                    Get<UIServices>().LauncherService.LaunchUri(Constants.AngorHubUri)),
                "fa-magnifying-glass") { IsPrimary = false }
        ];
    }
    
    private T Get<T>() where T : notnull
    {
        return serviceProvider.GetRequiredService<T>();
    }
    
    /// <summary>
    /// Creates an instance of <see cref="NavigationViewModel"/> with a specific ViewModel and an <see cref="INavigator"/> that provides scoped navigation.
    /// </summary>
    /// <typeparam name="T">The type of the ViewModel to be created.</typeparam>
    /// <returns>A new instance of <see cref="NavigationViewModel"/>.</returns>
    /// <remarks>
    /// This method creates a new service scope to obtain an instance of the specified ViewModel and the <see cref="INavigator"/>.
    /// It then uses these services to create and return an instance of <see cref="NavigationViewModel"/>.
    /// This is useful to create navigation withing that scope, allowing the user to go back and forth between ViewModels
    /// </remarks>
    private NavigationViewModel GetWithNavigation<T>() where T : notnull
    {
        using var scope = serviceProvider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<T>();
        return new NavigationViewModel(scope.ServiceProvider.GetRequiredService<INavigator>(), () => vm);
    }
}