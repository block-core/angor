using Angor.Server;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Shared.ProtocolNew;
using Blockcore.AtomicSwaps.Server;
using Microsoft.AspNetCore.ResponseCompression;
using DataConfigOptions = Angor.Server.DataConfigOptions;
using Angor.Client;
using Angor.Shared;
using Blockcore.AtomicSwaps.Server.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<DataConfigOptions>();
builder.Services.AddSingleton<TestStorageService>();
builder.Services.AddSingleton<TestStorageServiceIndexer>();

// types needed to build investor sigs
builder.Services.AddSingleton<INetworkConfiguration, NetworkConfiguration>();
builder.Services.AddSingleton<IHdOperations, HdOperations>();
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();


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
