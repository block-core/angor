using Angor.Client;
using Angor.Client.Services;
using Angor.Client.Shared;
using Angor.Client.Storage;
using Angor.Shared;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Shared.Services;
using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

builder.Services.AddTransient<INetworkConfiguration, NetworkConfiguration>();
builder.Services.AddTransient<IHdOperations, HdOperations>();
builder.Services.AddTransient <IClientStorage, ClientStorage>();
builder.Services.AddTransient<INetworkStorage, ClientStorage>();
builder.Services.AddTransient <IWalletStorage, WalletStorage>();
builder.Services.AddScoped<ICacheStorage, LocalSessionStorage>();
builder.Services.AddTransient<IWalletOperations, WalletOperations>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IDerivationOperations, DerivationOperations>();
builder.Services.AddScoped<NavMenuState>();

builder.Services.AddScoped<IIndexerService, IndexerService>();
builder.Services.AddScoped<INetworkService, NetworkService>();

builder.Services.AddTransient<IRelayService, RelayService>();
builder.Services.AddTransient<ISignService, SignService>();

builder.Services.AddTransient<IFounderTransactionActions, FounderTransactionActions>();
builder.Services.AddTransient<ISeederTransactionActions, SeederTransactionActions>();
builder.Services.AddTransient<IInvestorTransactionActions, InvestorTransactionActions>();
builder.Services.AddTransient<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
builder.Services.AddTransient<IProjectScriptsBuilder, ProjectScriptsBuilder>();
builder.Services.AddTransient<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
builder.Services.AddTransient<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
builder.Services.AddTransient<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
builder.Services.AddTransient<ITaprootScriptBuilder, TaprootScriptBuilder>();

builder.Services.AddSingleton<INostrCommunicationFactory,NostrCommunicationFactory>();
builder.Services.AddScoped<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
builder.Services.AddSingleton<IPasswordCashService, PasswordCashService>();

await builder.Build().RunAsync();
