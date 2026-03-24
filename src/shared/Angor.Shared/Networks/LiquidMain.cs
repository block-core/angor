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
    public class LiquidMain : BitcoinMain
    {
        public LiquidMain()
        {
            this.Name = "Liquid";
            this.AdditionalNames = new List<string> { "liquid", "liquid-mainnet", "liquid-main" };
            this.NetworkType = NetworkType.Mainnet;
            this.Magic = 0xdab5bffa; // Liquid mainnet magic from NBitcoin
            this.DefaultPort = 7042; // Liquid P2P port
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultRPCPort = 7041; // Liquid RPC port
            this.DefaultAPIPort = 38220;
            this.CoinTicker = "LBTC";
            this.DefaultBanTimeSeconds = 60 * 60 * 24;

            var consensusFactory = new ConsensusFactory();

            // Liquid Genesis Block
            this.GenesisTime = 1296688602; // Liquid genesis time
            this.GenesisNonce = 414098458; // Liquid genesis nonce
            this.GenesisBits = 0x1d00ffff; // Liquid genesis difficulty
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero; // Liquid genesis has no reward

            consensusFactory.Protocol = new ConsensusProtocol()
            {
                ProtocolVersion = ProtocolVersion.FEEFILTER_VERSION,
                MinProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION,
            };

            // Liquid (Elements) consensus parameters from NBitcoin
            this.Consensus = new Blockcore.Consensus.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(),
                coinType: 1776, // Liquid BIP44 coin type
                hashGenesisBlock: new uint256("d767f204777d8ebd0825f4f26c3d773c0d3f40268dc6afb3632a0fcbd49fde45"), // Correct Liquid genesis hash
                subsidyHalvingInterval: 150,
                majorityEnforceBlockUpgrade: 51, // Elements consensus
                majorityRejectBlockOutdated: 75, // Elements consensus
                majorityWindow: 144, // Elements consensus window
                buriedDeployments: null,
                bip9Deployments: null,
                bip34Hash: null,
                minerConfirmationWindow: 144, // Elements confirmation window
                maxReorgLength: 0,
                defaultAssumeValid: null,
                maxMoney: 21000000 * Money.COIN,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Zero, // Liquid doesn't use PoW rewards like Bitcoin
                targetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // 2 weeks
                targetSpacing: TimeSpan.FromSeconds(1 * 60), // 1 minute block time
                powAllowMinDifficultyBlocks: true, // Liquid allows minimum difficulty
                posNoRetargeting: false,
                powNoRetargeting: true, 
                powLimit: new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), // Elements PoW limit
                minimumChainWork: uint256.Zero, 
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero,
                proofOfStakeTimestampMask: 0
            );

            // Liquid address prefixes from NBitcoin
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 57 }; // "L" prefix
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 39 }; // "H" prefix
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { 128 }; // WIF prefix
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { 0x04, 0x88, 0xB2, 0x1E }; // xpub
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { 0x04, 0x88, 0xAD, 0xE4 }; // xprv

        
            var encoder = new Bech32Encoder("ex");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.SeedNodes = new List<NetworkAddress>();
            this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();
        }
    }
}