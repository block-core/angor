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
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Full end-to-end integration tests for Boltz Liquid → BTC swaps.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "Boltz-Liquid")]
public class BoltzLiquidSwapIntegrationTests : IDisposable
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
    private const string BoltzApiUrl = "https://boltz.thedude.cloud/";
    private const bool UseV2Prefix = true;

    public BoltzLiquidSwapIntegrationTests(ITestOutputHelper output)
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

    //[Fact(Skip = "Integration test - requires Boltz server and L-BTC testnet funds. Run manually.")]
    [Fact]
    public async Task FullLiquidSwapFlow_CreatePayAndMonitor_Success()
    {
        _output.WriteLine(new string('=', 80));
        _output.WriteLine("FULL BOLTZ LIQUID -> BTC SWAP INTEGRATION TEST");
        _output.WriteLine(new string('=', 80));

        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        _output.WriteLine($"\nDestination address: {destinationAddress}");
        _output.WriteLine($"   Network: {_network.Name}");

        var testFounderKey = _derivationOperations.DeriveFounderKey(words, 0);
        var pubKeyHex = _derivationOperations.DeriveInvestorKey(words, testFounderKey);

        _output.WriteLine($"   Test founder key: {testFounderKey}");
        _output.WriteLine($"   Public key (claim & refund): {pubKeyHex}");

        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == TestProjectId)))
            .ReturnsAsync(Result.Success(new Project { FounderKey = testFounderKey }));

        // Step 1: Create the Liquid chain swap
        _output.WriteLine("\nSTEP 1: Creating Liquid -> BTC chain swap...");
        var swapResult = await _boltzSwapService.CreateLiquidToBtcSwapAsync(
            destinationAddress, 100000L, pubKeyHex, pubKeyHex);

        if (swapResult.IsFailure)
        {
            Assert.Fail($"Failed to create Liquid swap: {swapResult.Error}");
        }
        var swap = swapResult.Value;

        _output.WriteLine($"\nSwap created! ID: {swap.Id}");
        _output.WriteLine($"   Lockup Address (Liquid): {swap.LockupAddress}");
        _output.WriteLine($"   Blinding Key: {swap.BlindingKey ?? "(null)"}");
        _output.WriteLine($"   Expected On-chain Amount: {swap.ExpectedAmount} sats");
        _output.WriteLine($"   Invoice (should be empty): '{swap.Invoice}'");
        _output.WriteLine($"   SwapTree: {swap.SwapTree ?? "(null)"}");

        // Validate Liquid-specific fields
        Assert.NotEmpty(swap.LockupAddress);
        Assert.Empty(swap.Invoice);
        Assert.True(swap.IsChainSwap, "Swap should be marked as chain swap");
        Assert.NotEmpty(swap.LockupSwapTree);
        Assert.NotEmpty(swap.LockupServerPublicKey);
        Assert.NotEmpty(swap.ClaimLockupAddress);

        _output.WriteLine($"   IsChainSwap: {swap.IsChainSwap}");
        _output.WriteLine($"   ClaimLockupAddress (BTC): {swap.ClaimLockupAddress}");
        _output.WriteLine($"   LockupServerPublicKey: {swap.LockupServerPublicKey}");

        // Save swap to storage with project ID
        await _boltzSwapStorageService.SaveSwapAsync(swap, accountInfo.walletId, TestProjectId);

        // Step 2: Pay L-BTC to the Liquid lockup address
        _output.WriteLine("\nSTEP 2: Pay L-BTC to the Liquid lockup address");
        _output.WriteLine($"\n   ACTION REQUIRED: Send L-BTC to this Liquid address:");
        _output.WriteLine($"   {swap.LockupAddress}");
        if (!string.IsNullOrEmpty(swap.BlindingKey))
        {
            _output.WriteLine($"   Blinding Key: {swap.BlindingKey}");
        }
        _output.WriteLine($"\n   Use a Liquid testnet faucet or wallet with testnet L-BTC funds.");

        // Step 3: Monitor chain swap - wait for Boltz's BTC server lockup
        _output.WriteLine("\nSTEP 3: Monitoring chain swap via WebSocket (waiting for server BTC lockup)...");
        using var monitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var monitorResult = await _boltzWebSocketClient.MonitorChainSwapAsync(
            swap.Id, TimeSpan.FromMinutes(10), monitorCts.Token);

        if (monitorResult.IsFailure)
        {
            Assert.Fail($"Swap monitoring failed: {monitorResult.Error}");
        }
        var swapStatus = monitorResult.Value;

        _output.WriteLine($"\nSwap status: {swapStatus.Status}");
        _output.WriteLine($"   Transaction ID: {swapStatus.TransactionId ?? "N/A"}");

        await _boltzSwapStorageService.UpdateSwapStatusAsync(
            swap.Id, accountInfo.walletId, swapStatus.Status.ToString(), swapStatus.TransactionId, swapStatus.TransactionHex);

        // Step 4: Claim funds via cooperative chain claim
        _output.WriteLine("\nSTEP 4: Claiming on-chain BTC funds via chain swap cooperative claim...");
        if (swapStatus.Status == SwapState.TransactionServerMempool ||
            swapStatus.Status == SwapState.TransactionServerConfirmed)
        {
            // For chain swaps, the WebSocket returns Boltz's BTC lockup tx (server side)
            string? serverLockupTxHex = swapStatus.TransactionHex;
            if (string.IsNullOrEmpty(serverLockupTxHex) && !string.IsNullOrEmpty(swapStatus.TransactionId))
            {
                serverLockupTxHex = await _indexerService.GetTransactionHexByIdAsync(swapStatus.TransactionId);
                _output.WriteLine($"   Fetched server lockup TX hex: {serverLockupTxHex?.Length ?? 0} chars");
            }

            if (string.IsNullOrEmpty(serverLockupTxHex))
            {
                Assert.Fail("Server lockup transaction hex not available");
                return;
            }

            _output.WriteLine($"   Server lockup TX ID: {swapStatus.TransactionId}");
            _output.WriteLine($"   Server lockup TX hex length: {serverLockupTxHex.Length}");

            // Derive the claim private key
            var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(
                new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase },
                testFounderKey);
            var privateKeyHex = Encoders.Hex.EncodeData(investorPrivateKey.ToBytes());

            // Use ClaimChainSwapAsync directly
            var claimResult = await _boltzClaimService.ClaimChainSwapAsync(
                swap, privateKeyHex, serverLockupTxHex, 0, 2);

            if (claimResult.IsSuccess)
            {
                _output.WriteLine($"\nCHAIN SWAP CLAIMED! TX: {claimResult.Value.ClaimTransactionId}");

                // Update storage
                await _boltzSwapStorageService.MarkSwapClaimedAsync(
                    swap.Id, accountInfo.walletId, claimResult.Value.ClaimTransactionId);
            }
            else
            {
                _output.WriteLine($"\nChain swap claim failed: {claimResult.Error}");
                Assert.Fail($"Chain swap claim failed: {claimResult.Error}");
                return;
            }
        }
        else
        {
            _output.WriteLine($"\nSwap status is {swapStatus.Status}, expected TransactionServerMempool or TransactionServerConfirmed.");
            Assert.Fail($"Chain swap not ready for claiming. Status: {swapStatus.Status}");
            return;
        }

        // Step 5: Monitor destination address
        _output.WriteLine("\nSTEP 5: Monitoring destination address...");
        using var addressMonitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var detectedUtxos = await _mempoolMonitoringService.MonitorAddressForFundsAsync(
                destinationAddress, swap.ExpectedAmount - 1000, TimeSpan.FromMinutes(5), addressMonitorCts.Token);

            if (detectedUtxos.Any())
            {
                _output.WriteLine($"\nFUNDS RECEIVED! {detectedUtxos.Sum(u => u.value)} sats");
            }
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("\nAddress monitoring timed out");
        }

        _output.WriteLine("\n" + new string('=', 80));
        _output.WriteLine($"SUMMARY: Swap {swap.Id} - {swapStatus.Status}");
        _output.WriteLine(new string('=', 80));
    }

    [Fact(Skip = "Integration test - requires Boltz server. Run manually.")]
    public async Task CreateLiquidSwap_WithValidData_ReturnsSwapDetails()
    {
        _output.WriteLine("Testing Boltz Liquid chain swap API...\n");

        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        var testFounderKey = _derivationOperations.DeriveFounderKey(words, 0);
        var pubKeyHex = _derivationOperations.DeriveInvestorKey(words, testFounderKey);

        var result = await _boltzSwapService.CreateLiquidToBtcSwapAsync(destinationAddress, 100000, pubKeyHex, pubKeyHex);

        _output.WriteLine($"Result: {(result.IsSuccess ? "Success" : "Failure")}");
        if (result.IsSuccess)
        {
            var swap = result.Value;
            _output.WriteLine($"Swap ID: {swap.Id}");
            _output.WriteLine($"Lockup Address (Liquid): {swap.LockupAddress}");
            _output.WriteLine($"Blinding Key: {swap.BlindingKey ?? "(null)"}");
            _output.WriteLine($"Expected Amount: {swap.ExpectedAmount} sats");
            _output.WriteLine($"Invoice (should be empty): '{swap.Invoice}'");

            Assert.NotEmpty(swap.Id);
            Assert.NotEmpty(swap.LockupAddress);
            Assert.Empty(swap.Invoice);
        }
        else
        {
            _output.WriteLine($"Error: {result.Error}");
            Assert.Fail($"Failed to create Liquid swap: {result.Error}");
        }
    }

    [Fact(Skip = "Integration test - requires Boltz server. Run manually.")]
    public async Task GetLiquidSwapFees_ReturnsValidFees()
    {
        _output.WriteLine("Testing Boltz Liquid swap fee retrieval...\n");

        var result = await _boltzSwapService.GetLiquidToBtcSwapFeesAsync();

        _output.WriteLine($"Result: {(result.IsSuccess ? "Success" : "Failure")}");
        if (result.IsSuccess)
        {
            var fees = result.Value;
            _output.WriteLine($"Percentage: {fees.Percentage}%");
            _output.WriteLine($"Miner Fees: {fees.MinerFees} sats");
            _output.WriteLine($"Min Amount: {fees.MinAmount} sats");
            _output.WriteLine($"Max Amount: {fees.MaxAmount} sats");

            Assert.True(fees.Percentage > 0, "Fee percentage should be positive");
            Assert.True(fees.MinerFees > 0, "Miner fees should be positive");
            Assert.True(fees.MinAmount > 0, "Min amount should be positive");
            Assert.True(fees.MaxAmount > fees.MinAmount, "Max amount should be greater than min amount");
        }
        else
        {
            _output.WriteLine($"Error: {result.Error}");
            Assert.Fail($"Failed to get Liquid swap fees: {result.Error}");
        }
    }

    [Fact(Skip = "Integration test - requires Boltz server. Run manually.")]
    public async Task CalculateLiquidAmount_ReturnsCorrectAmount()
    {
        _output.WriteLine("Testing Liquid amount calculation...\n");

        const long desiredOnChainAmount = 100000; // 100k sats

        var result = await _boltzSwapService.CalculateLiquidAmountAsync(desiredOnChainAmount);

        _output.WriteLine($"Result: {(result.IsSuccess ? "Success" : "Failure")}");
        if (result.IsSuccess)
        {
            var liquidAmount = result.Value;
            _output.WriteLine($"Desired on-chain amount: {desiredOnChainAmount} sats");
            _output.WriteLine($"Required Liquid amount: {liquidAmount} sats");
            _output.WriteLine($"Difference (fees): {liquidAmount - desiredOnChainAmount} sats");

            Assert.True(liquidAmount > desiredOnChainAmount,
                "Liquid amount should be greater than desired on-chain amount (to cover fees)");
        }
        else
        {
            _output.WriteLine($"Error: {result.Error}");
            Assert.Fail($"Failed to calculate Liquid amount: {result.Error}");
        }
    }
}
