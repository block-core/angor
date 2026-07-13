using Angor.Shared.Models;
using Angor.Shared.Networks;
using NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface ITaprootScriptBuilder
{
    Script CreateStage(AngorNetwork network, ProjectScripts scripts);

    public Script CreateControlBlock(ProjectScripts scripts, Func<ProjectScripts, Script> func);

    (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts, int threshold,
        Key[] secrets);
}
