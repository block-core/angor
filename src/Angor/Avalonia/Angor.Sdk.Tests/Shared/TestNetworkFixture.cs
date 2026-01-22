using Angor.Shared;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Angor.Sdk.Tests.Shared;

/// <summary>
/// Shared fixture providing pre-configured network services and real crypto implementations.
/// Use with IClassFixture&lt;TestNetworkFixture&gt; to share across test classes.
/// </summary>
public class TestNetworkFixture
{
    public INetworkConfiguration NetworkConfiguration { get; }
    public Network Network { get; }
    public IDerivationOperations DerivationOperations { get; }
    public IInvestorTransactionActions InvestorTransactionActions { get; }
    public IFounderTransactionActions FounderTransactionActions { get; }
    public IWalletOperations WalletOperations { get; }
    public IHdOperations HdOperations { get; }
    public IProjectScriptsBuilder ProjectScriptsBuilder { get; }
    public IInvestmentScriptBuilder InvestmentScriptBuilder { get; }
    public ITaprootScriptBuilder TaprootScriptBuilder { get; }
    public ISpendingTransactionBuilder SpendingTransactionBuilder { get; }
    public IInvestmentTransactionBuilder InvestmentTransactionBuilder { get; }
    
    /// <summary>
    /// Standard test wallet words - use for deterministic test keys
    /// </summary>
    public const string TestWalletWords = "sorry poet adapt sister barely loud praise spray option oxygen hero surround";
    
    /// <summary>
    /// Alternative test wallet words - use for founder/investor separation
    /// </summary>
    public const string AlternateWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public TestNetworkFixture()
    {
        NetworkConfiguration = new Angor.Sdk.Common.NetworkConfiguration();
        NetworkConfiguration.SetNetwork(Angor.Shared.Networks.Networks.Bitcoin.Testnet());
        Network = NetworkConfiguration.GetNetwork();
        
        HdOperations = new HdOperations();
        
        DerivationOperations = new DerivationOperations(
            HdOperations,
            new NullLogger<DerivationOperations>(),
            NetworkConfiguration);

        TaprootScriptBuilder = new TaprootScriptBuilder();
        InvestmentScriptBuilder = new InvestmentScriptBuilder(new SeederScriptTreeBuilder());
        ProjectScriptsBuilder = new ProjectScriptsBuilder(DerivationOperations);
        
        SpendingTransactionBuilder = new SpendingTransactionBuilder(
            NetworkConfiguration,
            ProjectScriptsBuilder,
            InvestmentScriptBuilder);
        
        InvestmentTransactionBuilder = new InvestmentTransactionBuilder(
            NetworkConfiguration,
            ProjectScriptsBuilder,
            InvestmentScriptBuilder,
            TaprootScriptBuilder);

        InvestorTransactionActions = new InvestorTransactionActions(
            new NullLogger<InvestorTransactionActions>(),
            InvestmentScriptBuilder,
            ProjectScriptsBuilder,
            SpendingTransactionBuilder,
            InvestmentTransactionBuilder,
            TaprootScriptBuilder,
            NetworkConfiguration);

        FounderTransactionActions = new FounderTransactionActions(
            new NullLogger<FounderTransactionActions>(),
            NetworkConfiguration,
            ProjectScriptsBuilder,
            InvestmentScriptBuilder,
            TaprootScriptBuilder);
    }

    /// <summary>
    /// Creates WalletOperations with a mock indexer service.
    /// Use this when you need to control UTXO retrieval.
    /// </summary>
    public IWalletOperations CreateWalletOperations(Angor.Shared.Services.IIndexerService mockIndexerService)
    {
        return new WalletOperations(
            mockIndexerService,
            HdOperations,
            new NullLogger<WalletOperations>(),
            NetworkConfiguration);
    }
}
