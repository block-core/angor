using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Test.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Xunit;

namespace Angor.Test;

public class AddInputsAndSignTransactionCoinSelectionTests : AngorTestData
{
    private readonly WalletOperations _sut;

    public AddInputsAndSignTransactionCoinSelectionTests()
    {
        _sut = new WalletOperations(new Mock<IIndexerService>().Object, new HdOperations(),
            new NullLogger<WalletOperations>(), _networkConfiguration.Object);
    }

    private (AccountInfo Account, WalletWords Words, string Change, Transaction Tx) Setup(params long[] utxoValues)
    {
        var network = _networkConfiguration.Object.GetNetwork();
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var account = _sut.BuildAccountInfoForWalletWords(words);
        var ext = ExtPubKey.Parse(account.ExtPubKey, network.BitcoinNetwork);

        string Addr(string path) => ext.Derive(new KeyPath(path)).PubKey
            .GetAddress(ScriptPubKeyType.Segwit, network.BitcoinNetwork).ToString();

        var address = new AddressInfo { Address = Addr("0/0"), HdPath = "m/84'/1'/0'/0/0" };
        account.AddressesInfo.Add(address);
        var change = Addr("1/0");
        account.ChangeAddressesInfo.Add(new AddressInfo { Address = change, HdPath = "m/84'/1'/0'/1/0" });

        for (int i = 0; i < utxoValues.Length; i++)
        {
            address.UtxoData.Add(new UtxoData
            {
                address = address.Address,
                scriptHex = BitcoinAddress.Create(address.Address, network.BitcoinNetwork).ScriptPubKey.ToHex(),
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), i),
                value = utxoValues[i],
                blockIndex = 100 + i
            });
        }

        var tx = network.CreateTransaction();
        var dest = new Key().PubKey.WitHash.GetAddress(network.BitcoinNetwork);
        tx.Outputs.Add(Money.Satoshis(10001), dest.ScriptPubKey);
        return (account, words, change, tx);
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenFirstCoinCoversOutputsButNotFee_AddsSecondCoin()
    {
        // Arrange - the hackathon scenario: first coin leaves 99 sats, below the ~141 sat fee
        var (account, words, change, tx) = Setup(10100, 10100);

        // Act
        var result = _sut.AddInputsAndSignTransaction(change, tx, words, account, 1000);

        // Assert
        Assert.Equal(2, result.Transaction.Inputs.Count);
        Assert.True(result.TransactionFee >= result.Transaction.GetVirtualSize());
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenWalletCannotCoverFee_ThrowsApplicationException()
    {
        var (account, words, change, tx) = Setup(10010);

        var ex = Assert.Throws<ApplicationException>(() =>
            _sut.AddInputsAndSignTransaction(change, tx, words, account, 1000));

        Assert.StartsWith("Not enough funds", ex.Message);
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenNoUtxos_ThrowsApplicationException()
    {
        var (account, words, change, tx) = Setup();

        var ex = Assert.Throws<ApplicationException>(() =>
            _sut.AddInputsAndSignTransaction(change, tx, words, account, 1000));

        Assert.StartsWith("Not enough funds", ex.Message);
    }
}