using AngorApp.Model;
using CSharpFunctionalExtensions;
using NBitcoin;
using Network = NBitcoin.Network;

public class HdWallet : IWallet
{
    private readonly Mnemonic mnemonic;
    private readonly ExtKey masterKey;
    private readonly Network network;
    private readonly List<IBroadcastedTransaction> history;

    public HdWallet(Network network, string mnemonic = null, string passphrase = null)
    {
        this.network = network;
        history = new List<IBroadcastedTransaction>();

        // Crear o restaurar cartera
        this.mnemonic = string.IsNullOrEmpty(mnemonic)
            ? CreateNewMnemonic()
            : new Mnemonic(mnemonic);

        var seed = this.mnemonic.DeriveSeed(passphrase);
        masterKey = ExtKey.CreateFromBytes(seed);
    }

    private static Mnemonic CreateNewMnemonic()
    {
        var entropy = new byte[16];
        new Random().NextBytes(entropy);
        return new Mnemonic(Wordlist.English, entropy);
    }

    public IEnumerable<IBroadcastedTransaction> History => history;
    public long? Balance { get; set; }

    public BitcoinNetwork Network
    {
        get
        {
            if (network == NBitcoin.Network.Main)
            {
                return BitcoinNetwork.Mainnet;
            }

            if (network == NBitcoin.Network.TestNet)
            {
                return BitcoinNetwork.Testnet;
            }

            throw new ArgumentOutOfRangeException();
        }
    }

    public string ReceiveAddress => GetAddress(isChange: false, 0);

    public Task<Result<IUnsignedTransaction>> CreateTransaction(long amount, string address, long feerate)
    {
        try
        {
            if (!IsAddressValid(address).IsSuccess)
            {
                return Task.FromResult(Result.Failure<IUnsignedTransaction>("Invalid address"));
            }

            var tx = new UnsignedWalletTx(
                network: network,
                amount: amount,
                toAddress: address,
                changeAddress: GetAddress(isChange: true, 0),
                feeRate: feerate
            );

            return Task.FromResult(Result.Success<IUnsignedTransaction>(tx));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<IUnsignedTransaction>(ex.Message));
        }
    }

    public Result IsAddressValid(string address)
    {
        try
        {
            BitcoinAddress.Create(address, network);
            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Invalid address");
        }
    }

    private string GetAddress(bool isChange, uint index)
    {
        // BIP44: m/44'/0'/0'/0/i para receive, m/44'/0'/0'/1/i para change
        var path = new KeyPath($"m/44'/0'/0'/{(isChange ? 1 : 0)}/{index}");
        var key = masterKey.Derive(path);
        return key.PrivateKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString();
    }

    internal Key GetPrivateKey(string path)
    {
        return masterKey.Derive(new KeyPath(path)).PrivateKey;
    }
}

public class UnsignedWalletTx : IUnsignedTransaction
{
    private readonly Network network;
    private readonly long amount;
    private readonly string toAddress;
    private readonly string changeAddress;
    private readonly long feeRate;

    public UnsignedWalletTx(
        Network network,
        long amount,
        string toAddress,
        string changeAddress,
        long feeRate)
    {
        this.network = network;
        this.amount = amount;
        this.toAddress = toAddress;
        this.changeAddress = changeAddress;
        this.feeRate = feeRate;
    }

    public long TotalFee { get; set; }

    public Task<Result<IBroadcastedTransaction>> Broadcast()
    {
        try
        {
            var tx = network.CreateTransaction();

            // Agregar output principal
            var receiverAddress = BitcoinAddress.Create(toAddress, network);
            tx.Outputs.Add(new Money(amount), receiverAddress.ScriptPubKey);

            // Quien llame a Broadcast() debería:
            // 1. Obtener UTXOs disponibles (de indexer, storage local, etc)
            // 2. Seleccionar inputs apropiados
            // 3. Firmar inputs con las claves privadas correspondientes
            // 4. Transmitir la tx a la red
            // 5. Guardar la tx en su storage

            var broadcastedTx = new WalletBroadcastedTransaction
            {
                Address = toAddress,
                Amount = (uint)amount,
                FeeRate = feeRate,
                TotalFee = TotalFee,
                Path = tx.GetHash().ToString(),
                UtxoCount = tx.Inputs.Count,
                //ViewRawJson = tx.ToJson()
            };

            return Task.FromResult(Result.Success<IBroadcastedTransaction>(broadcastedTx));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<IBroadcastedTransaction>(ex.Message));
        }
    }
}

public class WalletBroadcastedTransaction : IBroadcastedTransaction
{
    public string Address { get; set; }
    public decimal FeeRate { get; set; }
    public decimal TotalFee { get; set; }
    public uint Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
}