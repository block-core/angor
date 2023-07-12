using Angor.Client;
using Angor.Client.Services;
using Angor.Client.Storage;
using Angor.Shared;
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

await builder.Build().RunAsync();
