using Angor.Client;
using Angor.Client.Services;
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
builder.Services.AddTransient <IClientStorage, Storage>();
builder.Services.AddTransient<IWalletOperations, WalletOperations>();
builder.Services.AddScoped<IClipboardService, ClipboardService>();

await builder.Build().RunAsync();
