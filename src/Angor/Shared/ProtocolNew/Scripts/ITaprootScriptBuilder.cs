using System.Linq.Expressions;
using Angor.Shared.Models;
using Script = Blockcore.Consensus.ScriptInfo.Script;

namespace Angor.Shared.ProtocolNew.Scripts;

public interface ITaprootScriptBuilder
{
    public Script CreateControlBlock(ProjectScripts scripts, Expression<Func<ProjectScripts, Script>> func);

    (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts,
        Blockcore.NBitcoin.Key[] secrets);
}