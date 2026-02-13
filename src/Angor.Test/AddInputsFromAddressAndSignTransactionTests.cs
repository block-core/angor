using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Angor.Test.Protocol;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Test;

public class AddInputsFromAddressAndSignTransactionTests : AngorTestData
{
    private readonly WalletOperations _sut;
    private readonly Mock<IIndexerService> _indexerService;
    private readonly InvestorTransactionActions _investorTransactionActions;
    private readonly Network _network;

    public AddInputsFromAddressAndSignTransactionTests()
    {
        _indexerService = new Mock<IIndexerService>();
        _network = _networkConfiguration.Object.GetNetwork();

        _sut = new WalletOperations(
            _indexerService.Object,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            _networkConfiguration.Object);

        _investorTransactionActions = new InvestorTransactionActions(
            new NullLogger<InvestorTransactionActions>(),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            new ProjectScriptsBuilder(_derivationOperations),
            new SpendingTransactionBuilder(_networkConfiguration.Object,
                new ProjectScriptsBuilder(_derivationOperations),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            new InvestmentTransactionBuilder(_networkConfiguration.Object,
                new ProjectScriptsBuilder(_derivationOperations),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new TaprootScriptBuilder()),
            new TaprootScriptBuilder(),
            _networkConfiguration.Object);
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithValidAddress_SignsSuccessfully()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add an address to the account (BuildAccountInfoForWalletWords doesn't populate addresses)
        var hdPath = "m/84'/1'/0'/0/0";
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = hdPath });

        // Add a second address for destination
        var hdPath1 = "m/84'/1'/0'/0/1";
        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = hdPath1 });

        // Add a change address
        var changeHdPath = "m/84'/1'/0'/1/0";
        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = changeHdPath });

        // Add UTXOs to the first address
        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 3, 100000000); // 3 UTXOs of 1 BTC each

        // Create a simple transaction
        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(250000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
                fundingAddress.Address,
                accountInfo.GetNextChangeReceiveAddress()!,
                transaction,
                walletSigner,
                accountInfo,
                3000);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Transaction);
        Assert.True(result.Transaction.Inputs.Count > 0);
        Assert.True(result.TransactionFee > 0);

        // Verify all inputs are from the funding address
        foreach (var input in result.Transaction.Inputs)
        {
            var utxo = fundingAddress.UtxoData.FirstOrDefault(u => u.outpoint.ToString() == input.PrevOut.ToString());
            Assert.NotNull(utxo);
        }
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithInsufficientFunds_ThrowsException()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 1, 10000000); // Only 0.1 BTC

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(500000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey); // Trying to send 5 BTC

        // Act & Assert
        var walletSigner = new WalletSigner(words, _network);
        var exception = Assert.Throws<ApplicationException>(() =>
            _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000));

        Assert.Contains("Insufficient funds", exception.Message);
        Assert.Contains(fundingAddress.Address, exception.Message);
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithInvalidAddress_ThrowsException()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(100000000),
              BitcoinAddress.Create(accountInfo.AddressesInfo[0].Address, _network).ScriptPubKey);

        // Act & Assert
        var walletSigner = new WalletSigner(words, _network);
        var exception = Assert.Throws<ApplicationException>(() =>
            _sut.AddInputsFromAddressAndSignTransaction(
            "bc1qinvalidaddressnotinwallet",
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000));

        Assert.Contains("not found in account", exception.Message);
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithReservedUtxos_ExcludesThem()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 3, 100000000);

        // Reserve one UTXO
        accountInfo.UtxoReservedForInvestment.Add(fundingAddress.UtxoData[0].outpoint.ToString());

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(150000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000);

        // Assert
        Assert.NotNull(result);

        // Verify the reserved UTXO was not used
        var reservedOutpoint = fundingAddress.UtxoData[0].outpoint.ToString();
        Assert.DoesNotContain(result.Transaction.Inputs,
      input => input.PrevOut.ToString() == reservedOutpoint);
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_CreatesChangeOutput_WhenNeeded()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 2, 100000000); // 2 UTXOs of 1 BTC each

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(50000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000);

        // Assert
        Assert.NotNull(result);

        // Should have original output + change output
        Assert.True(result.Transaction.Outputs.Count >= 2, "Should have change output");

        // Change should go to change address
        var changeAddr = BitcoinAddress.Create(accountInfo.GetNextChangeReceiveAddress()!, _network);
        var hasChangeOutput = result.Transaction.Outputs.Any(o =>
          o.ScriptPubKey.GetDestinationAddress(_network)?.ToString() == changeAddr.ToString());
        Assert.True(hasChangeOutput, "Should have change output to change address");
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithPendingSpentUtxos_ExcludesThem()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 3, 100000000);

        // Mark one as pending spent
        fundingAddress.UtxoData[0].PendingSpent = true;

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(150000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000);

        // Assert
        Assert.NotNull(result);

        // Verify the pending spent UTXO was not used
        var pendingSpentOutpoint = fundingAddress.UtxoData[0].outpoint.ToString();
        Assert.DoesNotContain(result.Transaction.Inputs,
            input => input.PrevOut.ToString() == pendingSpentOutpoint);
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_WithMultipleUtxos_UsesAllWhenNeeded()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 5, 20000000); // 5 UTXOs of 0.2 BTC each

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(95000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Transaction.Inputs.Count == 5, "Should use all 5 UTXOs");
    }

    [Fact]
    public void AddInputsFromAddressAndSignTransaction_ProducesValidSignatures()
    {
        // Arrange
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        // Manually add addresses to the account
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = extPubKey.Derive(new KeyPath("0/0")).PubKey;
        var address = pubKey.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address, HdPath = "m/84'/1'/0'/0/0" });

        var pubKey1 = extPubKey.Derive(new KeyPath("0/1")).PubKey;
        var address1 = pubKey1.GetSegwitAddress(_network).ToString();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = address1, HdPath = "m/84'/1'/0'/0/1" });

        var changePubKey = extPubKey.Derive(new KeyPath("1/0")).PubKey;
        var changeAddress = changePubKey.GetSegwitAddress(_network).ToString();
        accountInfo.ChangeAddressesInfo.Add(new AddressInfo { Address = changeAddress, HdPath = "m/84'/1'/0'/1/0" });

        var fundingAddress = accountInfo.AddressesInfo[0];
        AddUtxosToAddress(fundingAddress, 2, 100000000);

        var destinationAddress = accountInfo.AddressesInfo[1].Address;
        var transaction = _network.CreateTransaction();
        transaction.AddOutput(Money.Satoshis(50000000), BitcoinAddress.Create(destinationAddress, _network).ScriptPubKey);

        // Act
        var walletSigner = new WalletSigner(words, _network);
        var result = _sut.AddInputsFromAddressAndSignTransaction(
            fundingAddress.Address,
            accountInfo.GetNextChangeReceiveAddress()!,
            transaction,
            walletSigner,
            accountInfo,
            3000);

        // Assert
        Assert.NotNull(result);

        // Verify all inputs have witness scripts (signatures)
        foreach (var input in result.Transaction.Inputs)
        {
            Assert.NotNull(input.WitScript);
            Assert.NotEqual(WitScript.Empty, input.WitScript);
            Assert.True(input.WitScript.PushCount >= 2, "Should have signature and pubkey");
        }
    }

    private void AddUtxosToAddress(AddressInfo addressInfo, int count, long valuePerUtxo)
    {
        var network = _networkConfiguration.Object.GetNetwork();

        for (int i = 0; i < count; i++)
        {
            var txId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
            var outpoint = new Outpoint(txId, i);

            addressInfo.UtxoData.Add(new UtxoData
            {
                address = addressInfo.Address,
                scriptHex = BitcoinAddress.Create(addressInfo.Address, network).ScriptPubKey.ToHex(),
                outpoint = outpoint,
                value = valuePerUtxo,
                blockIndex = 100 + i // Confirmed
            });
        }
    }
}

