using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Angor.Sdk.Wasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure services if needed
// builder.Services.AddSingleton<IProjectService, ProjectService>();

Console.WriteLine("Angor SDK WASM initialized");

await builder.Build().RunAsync();
