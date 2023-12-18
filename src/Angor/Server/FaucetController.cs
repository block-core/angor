using Angor.Server;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.BIP39;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Angor.Client.Services;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;
using ExtPubKey = Blockcore.NBitcoin.BIP32.ExtPubKey;
using Network = Blockcore.Networks.Network;
using static System.Runtime.InteropServices.JavaScript.JSType;
using TransactionBuilder = Blockcore.Consensus.TransactionInfo.TransactionBuilder;
using BitcoinWitPubKeyAddress = Blockcore.NBitcoin.BitcoinWitPubKeyAddress;
using Money = Blockcore.NBitcoin.Money;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Blockcore.AtomicSwaps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaucetController : ControllerBase
    {
        private readonly IWalletOperations _walletOperations;
        private readonly IIndexerService _indexerService;
        private readonly IHdOperations _hdOperations;
        private readonly INetworkConfiguration _networkConfiguration;

        private List<UtxoData> PendingUtxo = new ();

        public FaucetController(IWalletOperations walletOperations, IIndexerService indexerService, IHdOperations hdOperations, INetworkConfiguration networkConfiguration)
        {
            _walletOperations = walletOperations;
            _indexerService = indexerService;
            _hdOperations = hdOperations;
            _networkConfiguration = networkConfiguration;
        }

        [HttpGet]
        [Route("send/{address}/{amount?}")]
        public async Task<IActionResult> Send(string address, long? amount)
        {
            var network = _networkConfiguration.GetNetwork();

            //var mnemonic = new Mnemonic(wrods);
            var words = new WalletWords { Words = "margin radio diamond leg loud street announce guitar video shiver speed eyebrow" };

            var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);

            // the miners address with all the utxos
            var addressInfo = GenerateAddressFromPubKey(0, _networkConfiguration.GetNetwork(), false, ExtPubKey.Parse(accountInfo.ExtPubKey, _networkConfiguration.GetNetwork()));

            List<UtxoDataWithPath> list = new();

            if (!PendingUtxo.Any())
            {
                // we assume a miner wallet so for now just ignore amounts and send a utxo to the request address  
                var utxos = await _indexerService.FetchUtxoAsync(addressInfo.Address, 510, 5);

                lock (PendingUtxo)
                {
                    if (!PendingUtxo.Any())
                    {
                        PendingUtxo.AddRange(utxos);
                    }
                }
            }

            lock (PendingUtxo)
            {
                list = new() { new UtxoDataWithPath { HdPath = addressInfo.HdPath, UtxoData = PendingUtxo.First() } };
                PendingUtxo.Remove(PendingUtxo.First());
            }

            var (coins, keys) = _walletOperations.GetUnspentOutputsForTransaction(words, list);

            Transaction trx = network.CreateTransaction();
            trx.AddOutput(Money.Satoshis(list.First().UtxoData.value) - Money.Satoshis(10000), BitcoinWitPubKeyAddress.Create(address, network));
            trx.AddInput(new TxIn { PrevOut = list.First().UtxoData.outpoint.ToOutPoint() });

            var signedTransaction = new TransactionBuilder(network)
                .AddCoins(coins)
                .AddKeys(keys.ToArray())
                .SignTransaction(trx);
           
            var res = await _walletOperations.PublishTransactionAsync(network, signedTransaction);

            if (res.Success)
            {
                return Ok(signedTransaction.ToHex(_networkConfiguration.GetNetwork().Consensus.ConsensusFactory));
            }
                
            return BadRequest(res.Message);
        }

        private AddressInfo GenerateAddressFromPubKey(int scanIndex, Network network, bool isChange, ExtPubKey accountExtPubKey)
        {
            var pubKey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
            var path = _hdOperations.CreateHdPath(84, network.Consensus.CoinType, 0, isChange, scanIndex);
            var address = pubKey.GetSegwitAddress(network).ToString();

            return new AddressInfo { Address = address, HdPath = path };
        }
    }

    public class NetworkServiceMock : INetworkService
    {
        public Task CheckServices(bool force = false)
        {
            throw new NotImplementedException();
        }

        public void AddSettingsIfNotExist()
        {
            throw new NotImplementedException();
        }

        public SettingsUrl GetPrimaryIndexer()
        {
            return new SettingsUrl { Url = "https://tbtc.indexer.angor.io" };
        }

        public SettingsUrl GetPrimaryRelay()
        {
            throw new NotImplementedException();
        }

        public List<SettingsUrl> GetRelays()
        {
            throw new NotImplementedException();
        }

        public void CheckAndHandleError(HttpResponseMessage httpResponseMessage)
        {
           
        }

        public void HandleException(Exception exception)
        {
            throw exception;
        }
    }
}