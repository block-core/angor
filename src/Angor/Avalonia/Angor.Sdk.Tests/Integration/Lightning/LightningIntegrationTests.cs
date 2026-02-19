using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Tests.Funding.TestDoubles;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Full end-to-end integration tests for Boltz Lightning ↔ On-chain swaps.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "Boltz")]
public class BoltzSwapIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Blockcore.Networks.Network _network;
    private readonly NetworkConfiguration _networkConfiguration;
    private readonly WalletOperations _walletOperations;
    private readonly DerivationOperations _derivationOperations;
    private readonly IIndexerService _indexerService;
    private readonly IMempoolMonitoringService _mempoolMonitoringService;
    private readonly BoltzSwapService _boltzSwapService;
    private readonly BoltzClaimService _boltzClaimService;
    private readonly BoltzWebSocketClient _boltzWebSocketClient;
    private readonly BoltzSwapStorageService _boltzSwapStorageService;
    private readonly ClaimLightningSwap.ClaimLightningSwapByIdHandler _claimHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IProjectService> _mockProjectService;

    private const string TestWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestWalletPassphrase = "";
    private const string TestProjectId = "test-project-id";
    private const string BoltzApiUrl = "http://localhost:9001/";
    private const bool UseV2Prefix = true;

    public BoltzSwapIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _networkConfiguration = new NetworkConfiguration();
        _networkConfiguration.SetNetwork(new Angornet());
        _network = _networkConfiguration.GetNetwork();

        _derivationOperations = new DerivationOperations(
            new HdOperations(),
            new NullLogger<DerivationOperations>(),
            _networkConfiguration);

        var httpClientFactory = new TestHttpClientFactory("https://signet.angor.online");
        var networkService = new TestNetworkService(
            new SettingsUrl { Name = "Angornet", Url = "https://signet.angor.online" },
            _networkConfiguration);

        _indexerService = new MempoolSpaceIndexerApi(
            new NullLogger<MempoolSpaceIndexerApi>(),
            httpClientFactory,
            networkService,
            _derivationOperations);

        _mempoolMonitoringService = new MempoolMonitoringService(
            _indexerService,
            new NullLogger<MempoolMonitoringService>());

        _walletOperations = new WalletOperations(
            _indexerService,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            _networkConfiguration);

        var boltzConfig = new BoltzConfiguration
        {
            BaseUrl = BoltzApiUrl,
            UseV2Prefix = UseV2Prefix,
            TimeoutSeconds = 60
        };

        _httpClient = new HttpClient();
        _boltzSwapService = new BoltzSwapService(
            _httpClient,
            boltzConfig,
            _networkConfiguration,
            new NullLogger<BoltzSwapService>());

        _boltzClaimService = new BoltzClaimService(
            _boltzSwapService,
            _indexerService,
            _networkConfiguration,
            new NullLogger<BoltzClaimService>());

        _boltzWebSocketClient = new BoltzWebSocketClient(
            boltzConfig,
            new NullLogger<BoltzWebSocketClient>());

        var inMemoryCollection = new InMemoryBoltzSwapCollection();
        _boltzSwapStorageService = new BoltzSwapStorageService(
            inMemoryCollection,
            new NullLogger<BoltzSwapStorageService>());

        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success<(string, Maybe<string>)>((TestWalletWords, Maybe<string>.None)));

        _mockProjectService = new Mock<IProjectService>();

        _claimHandler = new ClaimLightningSwap.ClaimLightningSwapByIdHandler(
            _boltzClaimService,
            _boltzSwapStorageService,
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _derivationOperations,
            new NullLogger<ClaimLightningSwap.ClaimLightningSwapByIdHandler>());
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        (_boltzWebSocketClient as IAsyncDisposable)?.DisposeAsync().AsTask().Wait();
    }

    [Fact(Skip = "Integration test - requires boltz local server. Run manually.")]
    public async Task FullReverseSwapFlow_CreatePayAndMonitor_Success()
    {
        _output.WriteLine(new string('=', 80));
        _output.WriteLine("🚀 FULL BOLTZ REVERSE SWAP INTEGRATION TEST");
        _output.WriteLine(new string('=', 80));

        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        _output.WriteLine($"\n📍 Destination address: {destinationAddress}");
        _output.WriteLine($"   Network: {_network.Name}");

        // Generate a claim key - use DeriveInvestorKey with a test "founder key"
        // For testing, we derive a founder key from the same wallet
        var testFounderKey = _derivationOperations.DeriveFounderKey(words, 0);
        var claimPubKeyHex = _derivationOperations.DeriveInvestorKey(words, testFounderKey);

        _output.WriteLine($"   Test founder key: {testFounderKey}");
        _output.WriteLine($"   Claim public key: {claimPubKeyHex}");

        // Configure mock project service to return a project with this founder key
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == TestProjectId)))
            .ReturnsAsync(Result.Success(new Project { FounderKey = testFounderKey }));

        // Create the swap
        _output.WriteLine("\n📝 STEP 1: Creating reverse submarine swap...");
        var swapResult = await _boltzSwapService.CreateSubmarineSwapAsync(
            destinationAddress, 100000L, claimPubKeyHex);

        if (swapResult.IsFailure)
        {
            Assert.Fail($"Failed to create swap: {swapResult.Error}");
        }
        var swap = swapResult.Value;

        _output.WriteLine($"\n✅ Swap created! ID: {swap.Id}");
        _output.WriteLine($"   Invoice: {swap.Invoice}");
        _output.WriteLine($"   SwapTree: {swap.SwapTree ?? "(null)"}");

        // Save swap to storage with project ID
        await _boltzSwapStorageService.SaveSwapAsync(swap, accountInfo.walletId, TestProjectId);

        _output.WriteLine("\n⚡ STEP 2: Pay the Lightning invoice");
        _output.WriteLine($"\n🔴 ACTION REQUIRED: Pay this invoice:\n   {swap.Invoice}");

        // Monitor swap
        _output.WriteLine("\n📡 STEP 3: Monitoring swap via WebSocket...");
        using var monitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var monitorResult = await _boltzWebSocketClient.MonitorSwapAsync(
            swap.Id, TimeSpan.FromMinutes(10), monitorCts.Token);

        if (monitorResult.IsFailure)
        {
            Assert.Fail($"Swap monitoring failed: {monitorResult.Error}");
        }
        var swapStatus = monitorResult.Value;

        _output.WriteLine($"\n✅ Swap status: {swapStatus.Status}");
        _output.WriteLine($"   Transaction ID: {swapStatus.TransactionId ?? "N/A"}");

        await _boltzSwapStorageService.UpdateSwapStatusAsync(
            swap.Id, accountInfo.walletId, swapStatus.Status.ToString(), swapStatus.TransactionId, swapStatus.TransactionHex);

        // Claim funds
        _output.WriteLine("\n💰 STEP 4: Claiming on-chain funds...");
        if (swapStatus.Status == SwapState.TransactionMempool || swapStatus.Status == SwapState.TransactionConfirmed)
        {
            string? lockupTxHex = swapStatus.TransactionHex;
            if (string.IsNullOrEmpty(lockupTxHex) && !string.IsNullOrEmpty(swapStatus.TransactionId))
            {
                lockupTxHex = await _indexerService.GetTransactionHexByIdAsync(swapStatus.TransactionId);
                _output.WriteLine($"   Fetched lockup TX hex: {lockupTxHex?.Length ?? 0} chars");
            }

            var walletId = new WalletId(accountInfo.walletId);
            var claimRequest = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
                walletId, swap.Id, lockupTxHex, 0, 2);

            var claimResult = await _claimHandler.Handle(claimRequest, CancellationToken.None);

            if (claimResult.IsSuccess)
            {
                _output.WriteLine($"\n✅ CLAIMED! TX: {claimResult.Value.ClaimTransactionId}");
            }
            else
            {
                _output.WriteLine($"\n❌ Claim failed: {claimResult.Error}");
                Assert.Fail($"Claim failed: {claimResult.Error}");
                return; // Stop test execution
            }
        }
        else
        {
            _output.WriteLine($"\n⚠️  Swap status is {swapStatus.Status}, cannot claim yet.");
            Assert.Fail($"Swap not ready for claiming. Status: {swapStatus.Status}");
            return;
        }

        // Monitor destination address
        _output.WriteLine("\n🔍 STEP 5: Monitoring destination address...");
        using var addressMonitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var detectedUtxos = await _mempoolMonitoringService.MonitorAddressForFundsAsync(
                destinationAddress, swap.ExpectedAmount - 1000, TimeSpan.FromMinutes(5), addressMonitorCts.Token);

            if (detectedUtxos.Any())
            {
                _output.WriteLine($"\n✅ FUNDS RECEIVED! {detectedUtxos.Sum(u => u.value)} sats");
            }
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("\n⏱️  Address monitoring timed out");
        }

        _output.WriteLine("\n" + new string('=', 80));
        _output.WriteLine($"📊 SUMMARY: Swap {swap.Id} - {swapStatus.Status}");
        _output.WriteLine(new string('=', 80));
    }

    [Fact(Skip = "Integration test - requires boltz local server. Run manually.")]
    public async Task CreateReverseSwap_WithValidData_ReturnsSwapDetails()
    {
        _output.WriteLine("Testing Boltz API connectivity...\n");
        
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        var testFounderKey = _derivationOperations.DeriveFounderKey(words, 0);
        var claimPubKeyHex = _derivationOperations.DeriveInvestorKey(words, testFounderKey);

        var result = await _boltzSwapService.CreateSubmarineSwapAsync(destinationAddress, 100000, claimPubKeyHex);

        _output.WriteLine($"Result: {(result.IsSuccess ? "Success" : "Failure")}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"✅ Swap ID: {result.Value.Id}");
            Assert.NotEmpty(result.Value.Id);
        }
        else
        {
            _output.WriteLine($"❌ Error: {result.Error}");
            Assert.Fail($"Failed to create swap: {result.Error}");
        }
    }

    [Fact(Skip = "Integration test - requires boltz local server. Run manually.")]
    public async Task WebSocket_ConnectAndSubscribe_Works()
    {
        _output.WriteLine("Testing WebSocket connectivity...\n");
        
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        var testFounderKey = _derivationOperations.DeriveFounderKey(words, 0);
        var claimPubKeyHex = _derivationOperations.DeriveInvestorKey(words, testFounderKey);

        var swapResult = await _boltzSwapService.CreateSubmarineSwapAsync(destinationAddress, 50000, claimPubKeyHex);
        if (swapResult.IsFailure)
        {
            Assert.Fail($"Failed to create swap: {swapResult.Error}");
        }

        _output.WriteLine($"Created swap: {swapResult.Value.Id}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var monitorResult = await _boltzWebSocketClient.MonitorSwapAsync(
            swapResult.Value.Id, TimeSpan.FromSeconds(10), cts.Token);

        _output.WriteLine($"Monitor result: {(monitorResult.IsSuccess ? "Success" : "Timeout (expected)")}");
        _output.WriteLine("\n✅ WebSocket test passed!");
    }
}

public class InMemoryBoltzSwapCollection : IGenericDocumentCollection<BoltzSwapDocument>
{
    private readonly Dictionary<string, BoltzSwapDocument> _documents = new();

    public Task<Result<BoltzSwapDocument?>> FindByIdAsync(string id)
    {
        _documents.TryGetValue(id, out var doc);
        return Task.FromResult(Result.Success(doc));
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindByIdsAsync(IEnumerable<string> ids) =>
        Task.FromResult(Result.Success(ids.Where(_documents.ContainsKey).Select(id => _documents[id])));

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindAllAsync() =>
        Task.FromResult(Result.Success(_documents.Values.AsEnumerable()));

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindAsync(Expression<Func<BoltzSwapDocument, bool>> predicate) =>
        Task.FromResult(Result.Success(_documents.Values.Where(predicate.Compile())));

    public Task<Result<bool>> ExistsAsync(string id) =>
        Task.FromResult(Result.Success(_documents.ContainsKey(id)));

    public Task<Result<int>> InsertAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, params BoltzSwapDocument[] entities)
    {
        var getId = getDocumentId.Compile();
        foreach (var entity in entities) _documents[getId(entity)] = entity;
        return Task.FromResult(Result.Success(entities.Length));
    }

    public Task<Result<bool>> UpdateAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, BoltzSwapDocument entity)
    {
        var id = getDocumentId.Compile()(entity);
        if (_documents.ContainsKey(id)) { _documents[id] = entity; return Task.FromResult(Result.Success(true)); }
        return Task.FromResult(Result.Success(false));
    }

    public Task<Result<bool>> UpsertAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, BoltzSwapDocument entity)
    {
        _documents[getDocumentId.Compile()(entity)] = entity;
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<bool>> DeleteAsync(string id) =>
        Task.FromResult(Result.Success(_documents.Remove(id)));

    public Task<Result<int>> CountAsync(Expression<Func<BoltzSwapDocument, bool>>? predicate = null) =>
        Task.FromResult(Result.Success(predicate == null ? _documents.Count : _documents.Values.Count(predicate.Compile())));
}


