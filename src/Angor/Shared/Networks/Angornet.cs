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
    public class Angornet : BitcoinMain
    {
        public Angornet()
        {
            this.Name = "Angornet";
            this.AdditionalNames = new List<string> { "angornet" };
            this.NetworkType = NetworkType.Testnet;
            this.Magic = GetSignetMagic();
            this.DefaultPort = 48333;
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultRPCPort = 48332;
            this.DefaultAPIPort = 38220;
            this.CoinTicker = "TBTC";
            this.DefaultBanTimeSeconds = 60 * 60 * 24; // 24 Hours

            var consensusFactory = new ConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1296688602;
            this.GenesisNonce = 414098458;
            this.GenesisBits = 0x1d00ffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            consensusFactory.Protocol = new ConsensusProtocol()
            {
                ProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
                MinProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION,
            };

            this.Consensus = new Blockcore.Consensus.Consensus(
                    // BIP34Hash = new uint256(),
                    // RuleChangeActivationThreshold = 1916,
                    // CoinbaseMaturity = 100,
                    // SupportSegwit = true,
                    // SupportTaproot = true,
                    consensusFactory: consensusFactory,
                    consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                    coinType: 1,
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

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };

            var encoder = new Bech32Encoder("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;
            this.Bech32Encoders[(int)2] = encoder; //WITNESS_SCRIPT_ADDRESS until we add taproot to blockcore

            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();

        }

        private static uint GetSignetMagic()
        {
            var challengeBytes = Encoders.Hex.DecodeData("512102b57c4413a0354bcc360a37e035f26670deda14bab613c28fbd30fe52b2deccc151ae");
            var challenge = new Script(challengeBytes);
            MemoryStream ms = new MemoryStream();
            BitcoinStream bitcoinStream = new BitcoinStream(ms, true);
            bitcoinStream.ReadWrite(challenge);
            var h = Hashes.SHA256(ms.ToArray(), 0, (int)ms.Length);
            return Utils.ToUInt32(h, true);
        }

    }

}