using Angor.Server;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Shared.ProtocolNew;
using DataConfigOptions = Angor.Server.DataConfigOptions;
using Angor.Client;
using Angor.Shared;
using Blockcore.AtomicSwaps.Server.Controllers;
using Angor.Client.Services;
using Angor.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<DataConfigOptions>();
builder.Services.AddSingleton<TestStorageService>();
builder.Services.AddSingleton<TestStorageServiceIndexer>();

// types needed to build investor sigs
builder.Services.AddSingleton<INetworkConfiguration, NetworkConfiguration>();
builder.Services.AddSingleton<INetworkService, NetworkServiceMock>();
builder.Services.AddSingleton<IHdOperations, HdOperations>();
builder.Services.AddSingleton<IWalletOperations, WalletOperations>();
builder.Services.AddSingleton<IIndexerService, IndexerService>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<IDerivationOperations, DerivationOperations>();
builder.Services.AddSingleton<IFounderTransactionActions, FounderTransactionActions>();
builder.Services.AddSingleton<ISeederTransactionActions, SeederTransactionActions>();
builder.Services.AddSingleton<IInvestorTransactionActions, InvestorTransactionActions>();
builder.Services.AddSingleton<IInvestmentScriptBuilder, InvestmentScriptBuilder>();
builder.Services.AddSingleton<IProjectScriptsBuilder, ProjectScriptsBuilder>();
builder.Services.AddSingleton<ISpendingTransactionBuilder, SpendingTransactionBuilder>();
builder.Services.AddSingleton<IInvestmentTransactionBuilder, InvestmentTransactionBuilder>();
builder.Services.AddSingleton<ISeederScriptTreeBuilder, SeederScriptTreeBuilder>();
builder.Services.AddSingleton<ITaprootScriptBuilder, TaprootScriptBuilder>();
builder.Services.AddSingleton<ITestNostrSigningFromRelay, TestNostrSigningFromRelay>(); //TODO change this from a test class when the flow is complete


// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("https://test.angor.io")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Add this middleware before other middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

app.UseRouting();

// Use the CORS policy
app.UseCors("AllowSpecificOrigin");

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

// trigger the relay to send over messages
var storage = app.Services.GetService<TestStorageService>();
var relay = app.Services.GetService<ITestNostrSigningFromRelay>();
var projects = storage.GetAllKeys().Result;
foreach (var project in projects)
{
    relay.SignTransactionsFromNostrAsync(project.Key).Wait();
}

app.Run();
