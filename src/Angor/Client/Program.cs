using Angor.Client;
using Angor.Client.Services;
using Angor.Client.Shared;
using Angor.Client.Storage;
using Angor.Shared;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddTransient<INetworkConfiguration, NetworkConfiguration>();
builder.Services.AddTransient<IHdOperations, HdOperations>();
builder.Services.AddTransient <IClientStorage, ClientStorage>();
builder.Services.AddTransient <IWalletStorage, WalletStorage>();
builder.Services.AddTransient<IWalletOperations, WalletOperations>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<IDerivationOperations, DerivationOperations>();
builder.Services.AddScoped<NavMenuState>();
builder.Services.AddScoped<InvestmentOperations>();

builder.Services.AddScoped<IIndexerService, IndexerService>();
builder.Services.AddScoped<IRelayService, RelayService>();

builder.Services.AddTransient<IFounderTransactionActions, FounderTransactionActions>();
builder.Services.AddTransient<ISeederTransactionActions, SeederTransactionActions>();
builder.Services.AddTransient<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
builder.Services.AddTransient<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
builder.Services.AddTransient<IProjectScriptsBuilder, ProjectScriptsBuilder>();
builder.Services.AddTransient<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
builder.Services.AddTransient<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
builder.Services.AddTransient<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
builder.Services.AddTransient<ITaprootScriptBuilder, TaprootScriptBuilder>();

    



await builder.Build().RunAsync();
