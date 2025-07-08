using Blockcore.Base.Deployments;
using Blockcore.Consensus;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Networks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BitcoinCore;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin.Protocol;
using Blockcore.Consensus.Checkpoints;
using Blockcore.NBitcoin.Crypto;

namespace Angor.Shared.Networks
{
    public class LiquidTestnet : Network
    {
        public LiquidTestnet()
        {
            this.Name = "LiquidTestnet";
            this.AdditionalNames = new List<string> { "liquidtestnet", "liquid-testnet" };
            this.NetworkType = NetworkType.Testnet;

            
            this.Magic = 0x043587CF;
            this.DefaultPort = 18891;
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultRPCPort = 18891;
            this.DefaultAPIPort = 38220;
            this.MinTxFee = 100;
            this.MaxTxFee = Money.Coins(0.1m).Satoshi;
            this.FallbackFee = 1000;
            this.MinRelayTxFee = 100;
            this.CoinTicker = "L-BTC";
            this.DefaultBanTimeSeconds = 60 * 60 * 24; 

            var consensusFactory = new ConsensusFactory();

            
            this.GenesisTime = 1598918400; 
            this.GenesisNonce = 0;
            this.GenesisBits = 0x1d00ffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            consensusFactory.Protocol = new ConsensusProtocol()
            {
                ProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
                MinProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION,
            };

            this.Consensus = new Blockcore.Consensus.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(),
                coinType: 1, 
                hashGenesisBlock: new uint256("a771da8e52ee6ad581ed1e9a99825e5b3b7992225534eaa2ae23244fe26ab1c1"), // Liquid testnet genesis hash
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 51,
                majorityRejectBlockOutdated: 75,
                majorityWindow: 100,
                buriedDeployments: null,
                bip9Deployments: null,
                bip34Hash: null,
                minerConfirmationWindow: 2016,
                maxReorgLength: 0,
                defaultAssumeValid: null,
                maxMoney: 21000000 * Money.COIN,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Coins(50),
                targetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), 
                targetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: true,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: null,
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero,
                proofOfStakeTimestampMask: 0
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (36) }; 
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (19) }; 
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x04, 0x35, 0x83, 0x94 };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };

            // Liquid testnet uses "tlq" bech32 prefix for confidential addresses
            var encoder = new Bech32Encoder("tlq");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new LiquidStandardScriptsRegistry();
        }

        public class LiquidStandardScriptsRegistry : StandardScriptsRegistry
        {
            public const int MaxOpReturnRelay = 83;

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