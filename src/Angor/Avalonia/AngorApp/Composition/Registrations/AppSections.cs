using AngorApp.Sections.Browse;
using AngorApp.Sections.Home;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zafiro.UI;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations;

public class AppSections
{
    public static void Register(ServiceCollection services, ILogger logger)
     {
        services.AddSingleton<IEnumerable<ISection>>(provider =>
        {
            var dynamicHome = new DynamicContentSection(new ContentSection<IHomeSectionViewModel>("Home", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IHomeSectionViewModel>())), new Icon("svg:/Assets/angor-icon.svg")))
            {
                NarrowVisibility = false,
                WideVisibility = true,
            };
            
            // var homeSection = new ContentSection<IHomeSectionViewModel>("Home", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IHomeSectionViewModel>())), new Icon("svg:/Assets/angor-icon.svg"));
            //
            // var sq = ((bool[])[true, false]).ToObservable().Select(b => Observable.Return(b).Delay(3.Seconds())).Concat();
            // sq.Subscribe(b => homeSection.IsVisible = b);
            
            return
            [
                dynamicHome,
                new ContentSection<IBrowseSectionViewModel>("Browse", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IBrowseSectionViewModel>())), new Icon("svg:/Assets/browse.svg"))
            ];
        });

        // services.RegisterSections(builder => builder.Add(new Section())
        //         .Add<IHomeSectionViewModel>("Home", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Add<Lightweight1>("Lightweight 1", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Add<Lightweight2>("Lightweight 2", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Separator()
        //         .Add<IWalletSectionViewModel>("Wallet", new Icon { Source = "svg:/Assets/wallet.svg" })
        //         .Add<IBrowseSectionViewModel>("Browse", new Icon { Source = "svg:/Assets/browse.svg" })
        //         .Add<IPortfolioSectionViewModel>("Portfolio",  new Icon { Source = "svg:/Assets/portfolio.svg" })
        //         .Add<IFounderSectionViewModel>("Founder", new Icon { Source = "svg:/Assets/user.svg" })
        //         .Separator()
        //         .Add<ISettingsSectionViewModel>("Settings", new Icon { Source = "svg:/Assets/settings.svg" })
        //         .Command("Angor Hub", provider => ReactiveCommand.CreateFromTask(() => provider.GetRequiredService<ILauncherService>().LaunchUri(new Uri("https://hub.angor.io"))), new Icon { Source = "svg:/Assets/browse.svg" } , false)
        //     , logger);
    }
}