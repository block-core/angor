using System.Linq.Expressions;
using Angor.Shared.Models;
using Script = Script;

namespace Angor.Shared.Protocol.Scripts;

public interface ITaprootScriptBuilder
{
    Script CreateStage(Network network, ProjectScripts scripts);

    public Script CreateControlBlock(ProjectScripts scripts, Expression<Func<ProjectScripts, Script>> func);

    (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts, int threshold,
        Key[] secrets);
}