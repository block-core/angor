using Angor.Client;
using Angor.Client.Services;
using Angor.Client.Shared;
using Angor.Client.Storage;
using Angor.Shared;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient();


builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

builder.Services.AddSingleton<ISerializer, Serializer>();
builder.Services.AddSingleton<INetworkConfiguration, NetworkConfiguration>();
builder.Services.AddTransient<IHdOperations, HdOperations>();
builder.Services.AddTransient<IClientStorage, ClientStorage>();
builder.Services.AddTransient<INetworkStorage, ClientStorage>();
builder.Services.AddTransient<IWalletStorage, WalletStorage>();
builder.Services.AddScoped<ICacheStorage, LocalSessionStorage>();
builder.Services.AddTransient<IWalletOperations, WalletOperations>();
builder.Services.AddTransient<IPsbtOperations, PsbtOperations>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IDerivationOperations, DerivationOperations>();
builder.Services.AddScoped<NavMenuState>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<ICurrencyRateService, CurrencyRateService>();
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddScoped<IAngorIndexerService, MempoolIndexerAngorApi>();
builder.Services.AddScoped<MempoolIndexerMappers>();
builder.Services.AddScoped<IIndexerService, MempoolSpaceIndexerApi>();
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

builder.Services.AddScoped<IApplicationLogicService, ApplicationLogicService>();

builder.Services.AddSingleton<INostrCommunicationFactory, NostrCommunicationFactory>();
builder.Services.AddScoped<IRelaySubscriptionsHandling, RelaySubscriptionsHandling>();
builder.Services.AddSingleton<IPasswordCacheService, PasswordCacheService>();
builder.Services.AddTransient<IHtmlStripperService, HtmlStripperService>();
builder.Services.AddTransient<IHtmlSanitizerService, HtmlSanitizerService>();

builder.Services.AddScoped<NostrConversionHelper>();

builder.Services.AddScoped<IconService>();

builder.Services.AddScoped<IWalletUIService, WalletUIService>();

builder.Services.AddScoped<IMessageService, MessageService>();

// to change culture dynamically during startup,
// set <BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
// in the application's project file.)
// CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
// CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var app = builder.Build();

await app.RunAsync();