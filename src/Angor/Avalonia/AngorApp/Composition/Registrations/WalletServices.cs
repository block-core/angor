using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using AngorApp.Design;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations;

public static class WalletServices
{
     public static IServiceCollection Register(this IServiceCollection services)
     {
          services.AddSingleton<IWalletAppService, WalletAppServiceDesign>();
          services.AddSingleton<Func<BitcoinNetwork>>(() => BitcoinNetwork.Testnet);

          return services;
     }
}