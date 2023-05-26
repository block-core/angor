using Angor.Client.Shared.Types;
using Blockcore.Consensus;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BitcoinCore;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin.Protocol;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public Network GetNetwork()
    {
        return new BitcoinMain();
    }

    public IndexerUrl getIndexerUrl()
    {
        return new IndexerUrl{Symbol = "", Url = "http://localhost:9910"};
    }

    public class BitcoinMain : Network
    {
        public BitcoinMain()
        {
            {
                this.Name = "Signet";
                this.AdditionalNames = new List<string> { "bitcoin-signet","btc-signet" };
                this.NetworkType = NetworkType.Mainnet;

                // The message start string is designed to be unlikely to occur in normal data.
                // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
                // a large 4-byte int at any alignment.
                this.Magic = GetSignetMagic();
                this.DefaultPort = 38333;
                this.DefaultMaxOutboundConnections = 8;
                this.DefaultMaxInboundConnections = 117;
                this.DefaultRPCPort = 38332;
                this.DefaultAPIPort = 37220;
                this.MinTxFee = 1000;
                this.MaxTxFee = Money.Coins(0.1m).Satoshi;
                this.FallbackFee = 20000;
                this.MinRelayTxFee = 1000;
                this.CoinTicker = "BTC";
                this.DefaultBanTimeSeconds = 60 * 60 * 24; // 24 Hours

                var consensusFactory = new ConsensusFactory();

                consensusFactory.Protocol = new ConsensusProtocol()
                {
                    ProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
                    MinProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION,
                };

                this.Consensus = new Consensus(
                    // BIP34Hash = new uint256(),
                    // RuleChangeActivationThreshold = 1916,
                    // CoinbaseMaturity = 100,
                    // SupportSegwit = true,
                    // SupportTaproot = true,
                    consensusFactory: consensusFactory,
                    consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                    coinType: 0,
                    hashGenesisBlock: null,
                    subsidyHalvingInterval: 210000,
                    majorityEnforceBlockUpgrade: 750,
                    majorityRejectBlockOutdated: 950,
                    majorityWindow: 1000,
                    buriedDeployments: null,
                    bip9Deployments: null,
                    bip34Hash: null,
                    minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                    maxReorgLength: 0,
                    defaultAssumeValid: null, // 629000
                    maxMoney: 21000000 * Money.COIN,
                    coinbaseMaturity: 100,
                    premineHeight: 0,
                    premineReward: Money.Zero,
                    proofOfWorkReward: Money.Coins(50),
                    targetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                    targetSpacing: TimeSpan.FromSeconds(10 * 60),
                    powAllowMinDifficultyBlocks: false,
                    posNoRetargeting: false,
                    powNoRetargeting: false,
                    powLimit: new Target(new uint256("00000377ae000000000000000000000000000000000000000000000000000000")),
                    minimumChainWork: null,
                    isProofOfStake: false,
                    lastPowBlock: default(int),
                    proofOfStakeLimit: null,
                    proofOfStakeLimitV2: null,
                    proofOfStakeReward: Money.Zero,
                    proofOfStakeTimestampMask: 0
                );

                this.Base58Prefixes = new byte[12][];
                this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
                this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
                this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
                // this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
                // this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
                this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { 0x04, 0x35, 0x87, 0xCF };
                this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
                this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] =
                    new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
                this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x04, 0x35, 0x83, 0x94 };
                this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };

                var encoder = new Bech32Encoder("tb");
                this.Bech32Encoders = new Bech32Encoder[2];
                this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
                this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

                this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();
            }
        }
        
        private static uint GetSignetMagic()
        {
            var challengeBytes =  Encoders.Hex.DecodeData("512103ad5e0edad18cb1f0fc0d28a3d4f1f3e445640337489abb10404f2d1e086be430210359ef5021964fe22d6f8e05b2463c9540ce96883fe3b278760f048f5189f2e6c452ae");
            var challenge = new Script(challengeBytes);
            MemoryStream ms = new MemoryStream();
            BitcoinStream bitcoinStream = new BitcoinStream(ms, true);
            bitcoinStream.ReadWrite(challenge);
            var h = Hashes.SHA256(ms.ToArray(), 0, (int)ms.Length);
            return Utils.ToUInt32(h, true);
        }
        public class BitcoinStandardScriptsRegistry : StandardScriptsRegistry
        {
            // See MAX_OP_RETURN_RELAY in Bitcoin Core, <script/standard.h.>
            // 80 bytes of data, +1 for OP_RETURN, +2 for the pushdata opcodes.
            public const int MaxOpReturnRelay = 83;

            // Need a network-specific version of the template list
            private readonly List<ScriptTemplate> standardTemplates = new List<ScriptTemplate>
            {
                PayToPubkeyHashTemplate.Instance,
                PayToPubkeyTemplate.Instance,
                PayToScriptHashTemplate.Instance,
                PayToMultiSigTemplate.Instance,
                new TxNullDataTemplate(MaxOpReturnRelay),
                PayToWitTemplate.Instance
            };

            public override List<ScriptTemplate> GetScriptTemplates => this.standardTemplates;

            public override void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate)
            {
                if (!this.standardTemplates.Any(template => (template.Type == scriptTemplate.Type)))
                {
                    this.standardTemplates.Add(scriptTemplate);
                }
            }

            public override bool IsStandardTransaction(Transaction tx, Network network)
            {
                return base.IsStandardTransaction(tx, network);
            }

            public override bool AreOutputsStandard(Network network, Transaction tx)
            {
                return base.AreOutputsStandard(network, tx);
            }

            public override ScriptTemplate GetTemplateFromScriptPubKey(Script script)
            {
                return this.standardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(script));
            }

            public override bool IsStandardScriptPubKey(Network network, Script scriptPubKey)
            {
                return base.IsStandardScriptPubKey(network, scriptPubKey);
            }

            public override bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView)
            {
                return base.AreInputsStandard(network, tx, coinsView);
            }
        }
    }
}