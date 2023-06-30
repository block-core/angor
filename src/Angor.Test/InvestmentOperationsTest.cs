using Angor.Shared;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin.Crypto;
using NBitcoin;

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
    }
}