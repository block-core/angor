using Angor.Client;
using Angor.Server;
using Angor.Shared;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Shared.Services;
using Blockcore.AtomicSwaps.Server.Controllers;
using DataConfigOptions = Angor.Server.DataConfigOptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<DataConfigOptions>();
builder.Services.AddSingleton<TestStorageService>();
builder.Services.AddSingleton<TestStorageServiceIndexer>();

// Add necessary services for WebSocket proxying and other operations
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
builder.Services
    .AddSingleton<ITestNostrSigningFromRelay,
        TestNostrSigningFromRelay>(); //TODO change this from a test class when the flow is complete

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder
            .WithOrigins("https://test.angor.io", "http://localhost:5062") // Add your client URL here
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // Allow credentials for WebSocket support
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// Enable CORS for WebSocket connections
app.UseCors("AllowSpecificOrigin");

// Enable WebSocket support
app.UseWebSockets();

// Map controllers and pages
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

// Optional: Trigger the relay to send over messages (this can be left as-is)
var storage = app.Services.GetService<TestStorageService>();
var relay = app.Services.GetService<ITestNostrSigningFromRelay>();
var projects = storage.GetAllKeys().Result;
foreach (var project in projects) relay.SignTransactionsFromNostrAsync(project.Key).Wait();

app.Run();