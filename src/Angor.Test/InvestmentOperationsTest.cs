using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.Consensus.BlockInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;

namespace Angor.Test
{
    public class InvestmentOperationsTest
    {
        [Fact]
        public void BuildStage()
        {

            var funderKey = new Key();
            var investorKey = new Key();
            var secret = new Key();



            var scripts = ScriptBuilder.BuildSeederScript(funderKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), Hashes.Hash256(secret.ToBytes()).ToString(), 5, 10);

            var adress = AngorScripts.CreateStageSeeder(Networks.Bitcoin.Testnet(), scripts.founder, scripts.recover, scripts.endOfProject);

        }



        [Fact]
        public void SpendFounderStageTest()
        {
            var angorKey = new Key();
            var funderReceiveCoinsKey = new Key();
            var funderKey = new Key();
            var investorKey = new Key();
            var secret = new Key();

            InvestmentOperations operations = new InvestmentOperations();

            InvestorContext context = new InvestorContext();
            context.ProjectInvestmentInfo = new ProjectInvestmentInfo();
            context.ProjectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, NumberOfBLocks = 10 },
                new Stage { AmountToRelease = 1, NumberOfBLocks = 20 },
                new Stage { AmountToRelease = 1, NumberOfBLocks = 30 }
            };
            context.ProjectInvestmentInfo.FounderKey = Encoders.Hex.EncodeData(funderKey.PubKey.ToBytes());
            context.ProjectInvestmentInfo.AngorFeeKey = Encoders.Hex.EncodeData(angorKey.PubKey.ToBytes());
            context.InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());
            context.ProjectInvestmentInfo.TargetAmount = 3;

            var invtrx = operations.CreateInvestmentTransaction(Networks.Bitcoin.Testnet(), context, 3);

            context.TransactionHex = invtrx.ToHex();

            var foundertrx = operations.SpendFounderStage(Networks.Bitcoin.Testnet(), context, 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes()));

        }
    }
}