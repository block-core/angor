using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Test.DataBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;

namespace Angor.Test
{
    public class ScriptTest
    {
        [Fact]
        public void BuildStageScriptsSeeder()
        {
            var funderKey = new Key();
            var funderRecoveryKey = new Key();
            var investorKey = new Key();
            var secret = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();

            var scripts = ScriptBuilder.BuildScripts(funderKey.PubKey.ToHex(), funderRecoveryKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), Hashes.Hash256(secret.ToBytes()).ToString(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1), projectSeeders);

            var adress = AngorScripts.CreateStage(Networks.Bitcoin.Testnet(), scripts);
        }

        [Fact]
        public void BuildStageScriptsInvestor()
        {
            var funderKey = new Key();
            var funderRecoveryKey = new Key();
            var investorKey = new Key();
           

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());

            var scripts = ScriptBuilder.BuildScripts(funderKey.PubKey.ToHex(), funderRecoveryKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), projectSeeders);

            var adress = AngorScripts.CreateStage(Networks.Bitcoin.Testnet(), scripts);
        }

        [Fact]
        public void BuildStageScriptsInvestorNoSeedHashes()
        {
            var funderKey = new Key();
            var funderRecoveryKey = new Key();
            var investorKey = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();

            var scripts = ScriptBuilder.BuildScripts(funderKey.PubKey.ToHex(), funderRecoveryKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), projectSeeders);

            var adress = AngorScripts.CreateStage(Networks.Bitcoin.Testnet(), scripts);
        }
    }
}
