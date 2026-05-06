using System.Linq.Expressions;
using Angor.Primitives.Network;
using Angor.Shared.Models;
using Script = NBitcoin.Script;
using NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface ITaprootScriptBuilder
{
    Script CreateStage(AngorNetwork network, ProjectScripts scripts);

    public Script CreateControlBlock(ProjectScripts scripts, Expression<Func<ProjectScripts, Script>> func);

    (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts, int threshold,
        Key[] secrets);
}