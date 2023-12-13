using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
#pragma warning disable CS8618

namespace UniSatWallet
{
    public class UniSatWalletSendFundsRecipients
    {
        public string address { get; set; } = null!;
        public long amount { get; set; }
        public string hint { get; set; } = string.Empty;
    }
    public class UniSatWalletSendFunds
    {
        public List<UniSatWalletSendFundsRecipients> recipients { get; set; } = new();

        public string data { get; set; }
        public string feeRate { get; set; }
        public string network { get; set; }
    }

    public class UniSatWalletSendFundsOut
    {
        public string transactionId { get; set; }
        public string transactionHex { get; set; }
    }

    public class UniSatWalletSwapCoins
    {
        public string walletId { get; set; }
        public string accountId { get; set; }
        public string swapTrxHex { get; set; }
        public string trxToSignHex { get; set; }
        public string redeemScriptHex { get; set; }
        public string secretHashHex { get; set; }
        public string network { get; set; }
        public string message { get; set; }
    }

    public class UniSatWalletSwapCoinsOut
    {
        public string privateKey { get; set; }
        public string transactionId { get; set; }
        public string transactionHex { get; set; }

    }

    public class UniSatWalletMessageOut
    {
        public string key { get; set; }
        public ContentData response { get; set; }

        public class Account
        {
            public string icon { get; set; }
            public string name { get; set; }
            public string id { get; set; }
            public int network { get; set; }
            public string networkType { get; set; }
            public int purpose { get; set; }
            public int purposeAddress { get; set; }
            public string type { get; set; }
            public History history { get; set; }
            public State state { get; set; }
            public NetworkDefinition networkDefinition { get; set; }
        }

        public class Bip32
        {
            public int @public { get; set; }
            public int @private { get; set; }
        }

        public class Change
        {
            public string address { get; set; }
            public int index { get; set; }
        }

        public class Confirmation
        {
            public int low { get; set; }
            public int high { get; set; }
            public int count { get; set; }
        }

        public class History
        {
            public long balance { get; set; }
            public List<HistoryItem> history { get; set; }
            public int unconfirmed { get; set; }
            public List<Unspent> unspent { get; set; }
        }

        public class HistoryItem
        {
            public int blockIndex { get; set; }
            public string calculatedAddress { get; set; }
            public long calculatedValue { get; set; }
            public string entryType { get; set; }
            public int fee { get; set; }
            public bool finalized { get; set; }
            public bool isCoinbase { get; set; }
            public bool isCoinstake { get; set; }
            public int timestamp { get; set; }
            public string transactionHash { get; set; }
        }

        public class NetworkDefinition
        {
            public string id { get; set; }
            public string name { get; set; }
            public string symbol { get; set; }
            public int network { get; set; }
            public int purpose { get; set; }
            public string messagePrefix { get; set; }
            public string bech32 { get; set; }
            public Bip32 bip32 { get; set; }
            public int pubKeyHash { get; set; }
            public int scriptHash { get; set; }
            public int wif { get; set; }
            public int minimumFeeRate { get; set; }
            public int maximumFeeRate { get; set; }
            public bool testnet { get; set; }
            public bool isProofOfStake { get; set; }
            public bool smartContractSupport { get; set; }
            public string type { get; set; }
            public int? purposeAddress { get; set; }
        }

        public class Param
        {
            public object key { get; set; }
        }

        public class Peg
        {
            public string type { get; set; }
            public string address { get; set; }
        }

        public class Receive
        {
            public string address { get; set; }
            public int index { get; set; }
        }


        public class ContentData
        {
            public Wallet wallet { get; set; }
            public List<Account> accounts { get; set; }
        }

        public class State
        {
            public int balance { get; set; }
            public List<Change> change { get; set; }
            public bool completedScan { get; set; }
            public string id { get; set; }
            public DateTime lastScan { get; set; }
            public List<Receive> receive { get; set; }
        }

        public class Unspent
        {
            public string address { get; set; }
            public long balance { get; set; }
            public int index { get; set; }
            public string transactionHash { get; set; }
            public bool unconfirmed { get; set; }
        }

        public class Wallet
        {
            public string id { get; set; }
            public string name { get; set; }
            public string key { get; set; }
        }

    }
}